using System.Security.Claims;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Drovik.Shipment.Api.Application;
using Drovik.Shipment.Api.Contracts;
using Drovik.Shipment.Api.Domain;
using Drovik.Shipment.Api.Messaging;
using Drovik.Shipment.Api.Persistence;
using Drovik.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using static Drovik.Shipment.Api.Application.ApiResults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var connectionString = builder.Configuration.GetConnectionString("ShipmentDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:ShipmentDatabase must be configured through local secrets.");
builder.Services.AddDbContext<ShipmentDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxDispatcher<ShipmentDbContext>>();
builder.Services.AddSingleton<ShipmentEventConsumer>();
builder.Services.AddHostedService(services => services.GetRequiredService<ShipmentEventConsumer>());
builder.Services.AddHttpClient("fleet", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Fleet:BaseUrl"] ?? "http://localhost:5202");
    client.Timeout = TimeSpan.FromSeconds(10);
});
var issuer = builder.Configuration["Jwt:Issuer"] ?? "DROVIK.Identity";
var audience = builder.Configuration["Jwt:Audience"] ?? "DROVIK.Services";
var publicKeyText = builder.Configuration["Jwt:PublicKeySpkiBase64"]
    ?? throw new InvalidOperationException("Jwt:PublicKeySpkiBase64 must be configured through local secrets.");
var publicRsa = RSA.Create();
publicRsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyText), out _);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new RsaSecurityKey(publicRsa),
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero, NameClaimType = "sub", RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ShipmentDbContext>().Database.MigrateAsync();
}

app.MapCustomerEndpoints();

var shipmentPolicy = new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = "Administrator,Operator" };
app.MapGet("/api/v1/shipments", async (
    ShipmentDbContext database, string? search, ShipmentStatus? status, ShipmentPriority? priority,
    int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var query = database.Shipments.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();
        query = query.Where(item => item.TrackingCode.Contains(term) || item.Customer.Name.Contains(term) || item.Destination.Contains(term));
    }
    if (status.HasValue) query = query.Where(item => item.Status == status.Value);
    if (priority.HasValue) query = query.Where(item => item.Priority == priority.Value);
    var totalCount = await query.CountAsync(cancellationToken);
    var entities = await query.Include(item => item.Customer).Include(item => item.OriginCustomer).Include(item => item.StatusHistory).AsSplitQuery()
        .OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize)
        .ToListAsync(cancellationToken);
    return Results.Ok(new PagedResponse<ShipmentResponse>(entities.Select(ToResponse).ToList(), totalCount, page, pageSize));
}).RequireAuthorization(shipmentPolicy);

app.MapGet("/api/v1/shipments/{id:guid}", async (Guid id, ShipmentDbContext database, CancellationToken cancellationToken) =>
{
    var shipment = await database.Shipments.AsNoTracking().Include(item => item.Customer).Include(item => item.OriginCustomer).Include(item => item.StatusHistory)
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    return shipment is null ? Results.NotFound() : Results.Ok(ToResponse(shipment));
}).RequireAuthorization(shipmentPolicy);

app.MapPost("/api/v1/shipments", async (CreateShipmentRequest request, ShipmentDbContext database,
    ClaimsPrincipal principal, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    var (origin, destination, routeError) = await ResolveRoute(database, request.OriginCustomerId, request.CustomerId, cancellationToken);
    if (routeError is not null) return routeError;
    var now = DateTime.UtcNow;
    var shipment = new Shipment
    {
        Id = Guid.NewGuid(), TrackingCode = $"SH-{now:yyMMddHHmmss}-{RandomNumberGenerator.GetInt32(100000, 1000000)}",
        WeightKg = request.WeightKg,
        Priority = request.Priority, Status = ShipmentStatus.Pending, CreatedAtUtc = now, UpdatedAtUtc = now
    };
    SetRoute(shipment, origin!, destination!);
    shipment.StatusHistory.Add(NewHistory(shipment.Id, ShipmentStatus.Pending, principal, "Created", null, now));
    database.Shipments.Add(shipment);
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.ShipmentCreated,
        new ShipmentCreatedV1(shipment.Id, shipment.TrackingCode, shipment.CustomerId.ToString(), shipment.Status.ToString()), shipment.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/v1/shipments/{shipment.Id}", ToResponse(shipment));
}).RequireAuthorization(shipmentPolicy);

app.MapPut("/api/v1/shipments/{id:guid}", async (Guid id, UpdateShipmentRequest request, ShipmentDbContext database, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    var shipment = await database.Shipments.Include(item => item.Customer).Include(item => item.OriginCustomer).Include(item => item.StatusHistory).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (shipment is null) return Results.NotFound();
    if (shipment.Status is not ShipmentStatus.Pending) return Conflict("Solo se pueden editar envíos pendientes.");
    var (origin, destination, routeError) = await ResolveRoute(database, request.OriginCustomerId, request.CustomerId, cancellationToken);
    if (routeError is not null) return routeError;
    SetRoute(shipment, origin!, destination!);
    shipment.WeightKg = request.WeightKg;
    shipment.Priority = request.Priority;
    shipment.UpdatedAtUtc = DateTime.UtcNow;
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToResponse(shipment));
}).RequireAuthorization(shipmentPolicy);

app.MapPost("/api/v1/shipments/{id:guid}/assign", async (Guid id, AssignShipmentRequest request,
    ShipmentDbContext database, IHttpClientFactory clients, HttpContext context, ClaimsPrincipal principal, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    var shipment = await database.Shipments.Include(item => item.Customer).Include(item => item.OriginCustomer).Include(item => item.StatusHistory).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (shipment is null) return Results.NotFound();
    // Fleet recognizes a retry by its idempotency key and returns the original reservation; any other key gets a conflict.
    var mayBeRetry = shipment.Status == ShipmentStatus.Assigned && shipment.VehicleId == request.VehicleId && shipment.DriverId == request.DriverId;
    if (shipment.Status != ShipmentStatus.Pending && !mayBeRetry) return Conflict("Solo se pueden asignar envíos pendientes.");
    if (shipment.OriginCustomerId is null) return Conflict("Edita el envío y elige un origen del catálogo antes de asignarlo.");
    var fleet = clients.CreateClient("fleet");
    var reservation = await FleetRequest<FleetReservationResponse>(fleet, context,
        HttpMethod.Post, "/api/v1/reservations", new FleetReservationRequest(id, request.VehicleId, request.DriverId, shipment.WeightKg, request.IdempotencyKey.Trim()), cancellationToken);
    if (!reservation.Success) return Results.Json(reservation.ErrorBody, statusCode: reservation.StatusCode);
    if (mayBeRetry)
        return reservation.Value!.ReservationId == shipment.ReservationId
            ? Results.Ok(ToResponse(shipment))
            : Conflict("Solo se pueden asignar envíos pendientes.");

    shipment.VehicleId = request.VehicleId;
    shipment.DriverId = request.DriverId;
    shipment.ReservationId = reservation.Value!.ReservationId;
    shipment.Status = ShipmentStatus.Assigned;
    shipment.UpdatedAtUtc = DateTime.UtcNow;
    database.StatusHistory.Add(NewHistory(id, ShipmentStatus.Assigned, principal, "Assigned", null, shipment.UpdatedAtUtc));
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.ShipmentAssigned,
        new ShipmentAssignedV1(shipment.Id, request.VehicleId, request.DriverId, reservation.Value.ReservationId), shipment.Id.ToString()));
    try
    {
        await database.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        await NotifyFleet(fleet, context, $"/api/v1/reservations/{reservation.Value.ReservationId}/release", logger);
        return Conflict("El envío cambió mientras se asignaba; consulta su estado actual.");
    }
    catch (DbUpdateException)
    {
        await NotifyFleet(fleet, context, $"/api/v1/reservations/{reservation.Value.ReservationId}/release", logger);
        throw;
    }
    // The assignment is durable from here on: if this call fails, Fleet confirms from the ShipmentAssigned event.
    await NotifyFleet(fleet, context, $"/api/v1/reservations/{reservation.Value.ReservationId}/confirm", logger);
    return Results.Ok(ToResponse(shipment));
}).RequireAuthorization(shipmentPolicy);

app.MapPost("/api/v1/shipments/{id:guid}/status", async (Guid id, ChangeShipmentStatusRequest request,
    ShipmentDbContext database, IHttpClientFactory clients, HttpContext context, ClaimsPrincipal principal, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    var shipment = await database.Shipments.Include(item => item.Customer).Include(item => item.OriginCustomer).Include(item => item.StatusHistory).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (shipment is null) return Results.NotFound();
    if (!ValidTransition(shipment.Status, request.Status)) return Conflict($"No se permite la transición {shipment.Status} -> {request.Status}.");
    shipment.Status = request.Status;
    shipment.UpdatedAtUtc = DateTime.UtcNow;
    database.StatusHistory.Add(NewHistory(id, request.Status, principal, "StatusChanged", request.Note, shipment.UpdatedAtUtc));
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.ShipmentStatusChanged,
        new ShipmentStatusChangedV1(shipment.Id, shipment.Status.ToString(), request.Note, shipment.VehicleId, shipment.ReservationId), shipment.Id.ToString()));
    var closesShipment = request.Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled;
    if (closesShipment)
        database.Outbox.Add(OutboxMessageFactory.Create(
            request.Status == ShipmentStatus.Delivered ? DomainEvents.ShipmentDelivered : DomainEvents.ShipmentCancelled,
            new ShipmentClosedV1(shipment.Id, shipment.Status.ToString(), shipment.ReservationId), shipment.Id.ToString()));
    try
    {
        await database.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Conflict("El envío cambió mientras se actualizaba; consulta su estado actual.");
    }
    // The transition is durable from here on: if this call fails, Fleet releases from the closing event.
    if (closesShipment && shipment.ReservationId.HasValue)
        await NotifyFleet(clients.CreateClient("fleet"), context, $"/api/v1/reservations/{shipment.ReservationId}/release", logger);
    return Results.Ok(ToResponse(shipment));
}).RequireAuthorization(shipmentPolicy);

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (ShipmentDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "Healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.Run();

static bool ValidTransition(ShipmentStatus current, ShipmentStatus next) => current switch
{
    ShipmentStatus.Pending => next is ShipmentStatus.Cancelled,
    ShipmentStatus.Assigned => next is ShipmentStatus.InTransit or ShipmentStatus.Cancelled,
    ShipmentStatus.InTransit => next is ShipmentStatus.Delayed or ShipmentStatus.Delivered,
    ShipmentStatus.Delayed => next is ShipmentStatus.InTransit or ShipmentStatus.Delivered,
    _ => false
};
static ShipmentStatusHistory NewHistory(Guid shipmentId, ShipmentStatus status, ClaimsPrincipal principal, string source, string? note, DateTime at) =>
    new() { Id = Guid.NewGuid(), ShipmentId = shipmentId, Status = status, ActorId = principal.FindFirst("sub")?.Value ?? "unknown", Source = source, Note = note, OccurredAtUtc = at };
static Task<Customer?> FindDeliveryPoint(ShipmentDbContext database, Guid customerId, CancellationToken cancellationToken) =>
    database.Customers.SingleOrDefaultAsync(item => item.Id == customerId && item.IsActive, cancellationToken);
static async Task<(Customer? Origin, Customer? Destination, IResult? Error)> ResolveRoute(
    ShipmentDbContext database, Guid originId, Guid destinationId, CancellationToken cancellationToken)
{
    if (originId == destinationId) return (null, null, BadRequest("Ruta inválida", "El origen y el destino deben ser puntos distintos."));
    var origin = await FindDeliveryPoint(database, originId, cancellationToken);
    var destination = await FindDeliveryPoint(database, destinationId, cancellationToken);
    return origin is null || destination is null
        ? (null, null, BadRequest("Ruta inválida", "El origen y el destino deben ser puntos activos del catálogo."))
        : (origin, destination, null);
}
// Copies, so the record of a past delivery does not change if a catalog point is corrected later.
static void SetRoute(Shipment shipment, Customer origin, Customer destination)
{
    shipment.OriginCustomerId = origin.Id;
    shipment.OriginCustomer = origin;
    shipment.Origin = origin.AddressLabel;
    shipment.OriginLatitude = origin.Latitude;
    shipment.OriginLongitude = origin.Longitude;
    shipment.CustomerId = destination.Id;
    shipment.Customer = destination;
    shipment.Destination = destination.AddressLabel;
    shipment.DestinationLatitude = destination.Latitude;
    shipment.DestinationLongitude = destination.Longitude;
}
static ShipmentResponse ToResponse(Shipment item) => new(item.Id, item.TrackingCode, item.CustomerId, item.Customer.Name, item.OriginCustomerId, item.OriginCustomer?.Name, item.Origin, item.OriginLatitude, item.OriginLongitude,
    item.Destination, item.DestinationLatitude, item.DestinationLongitude, item.WeightKg, item.Priority, item.Status,
    item.VehicleId, item.DriverId, item.ReservationId, item.CreatedAtUtc, item.UpdatedAtUtc,
    item.StatusHistory.OrderByDescending(history => history.OccurredAtUtc)
        .Select(history => new ShipmentHistoryResponse(history.Id, history.Status, history.ActorId, history.Source, history.Note, history.OccurredAtUtc)).ToList());

static async Task<FleetCall<T>> FleetRequest<T>(HttpClient client, HttpContext context, HttpMethod method, string path,
    object? body, CancellationToken cancellationToken)
{
    using var request = new HttpRequestMessage(method, path);
    if (body is not null) request.Content = JsonContent.Create(body);
    if (context.Request.Headers.Authorization.Count > 0)
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(context.Request.Headers.Authorization.ToString());
    HttpResponseMessage response;
    try
    {
        response = await client.SendAsync(request, cancellationToken);
    }
    catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
    {
        return new FleetCall<T>(false, StatusCodes.Status503ServiceUnavailable, default,
            new { title = "Flota no disponible", detail = "No se pudo contactar el servicio de Flota; inténtalo de nuevo.", status = StatusCodes.Status503ServiceUnavailable });
    }
    using var ownedResponse = response;
    var text = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        object errorBody;
        try { errorBody = JsonSerializer.Deserialize<JsonElement>(text); }
        catch (JsonException) { errorBody = new { title = "Error de Flota", detail = "El servicio de Flota rechazó la operación.", status = (int)response.StatusCode }; }
        return new FleetCall<T>(false, (int)response.StatusCode, default, errorBody);
    }
    return new FleetCall<T>(true, (int)response.StatusCode,
        string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
        null);
}
// Best effort for immediate consistency, and independent of the caller's connection: the Outbox events are the durable path.
static async Task NotifyFleet(HttpClient client, HttpContext context, string path, ILogger logger)
{
    var result = await FleetRequest<JsonElement>(client, context, HttpMethod.Post, path, null, CancellationToken.None);
    if (!result.Success) logger.LogWarning("Fleet respondió {Status} a {Path}; se completará mediante eventos.", result.StatusCode, path);
}
record FleetCall<T>(bool Success, int StatusCode, T? Value, object? ErrorBody);

public partial class Program { }
