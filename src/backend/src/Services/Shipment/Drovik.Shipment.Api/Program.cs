using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Drovik.Shipment.Api.Contracts;
using Drovik.Shipment.Api.Domain;
using Drovik.Shipment.Api.Persistence;
using Drovik.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var connectionString = builder.Configuration.GetConnectionString("ShipmentDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:ShipmentDatabase must be configured through local secrets.");
builder.Services.AddDbContext<ShipmentDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxDispatcher<ShipmentDbContext>>();
builder.Services.AddHttpClient("fleet", client =>
    client.BaseAddress = new Uri(builder.Configuration["Fleet:BaseUrl"] ?? "http://localhost:5202"));
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

var shipmentPolicy = new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = "Administrator,Operator" };
app.MapGet("/api/v1/shipments", async (
    ShipmentDbContext database, string? search, ShipmentStatus? status, ShipmentPriority? priority,
    int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var query = database.Shipments.AsNoTracking().Include(item => item.StatusHistory).AsQueryable();
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();
        query = query.Where(item => item.TrackingCode.Contains(term) || item.CustomerId.Contains(term) || item.Destination.Contains(term));
    }
    if (status.HasValue) query = query.Where(item => item.Status == status.Value);
    if (priority.HasValue) query = query.Where(item => item.Priority == priority.Value);
    var totalCount = await query.CountAsync(cancellationToken);
    var entities = await query.OrderByDescending(item => item.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
        .ToListAsync(cancellationToken);
    return Results.Ok(new PagedResponse<ShipmentResponse>(entities.Select(ToResponse).ToList(), totalCount, page, pageSize));
}).RequireAuthorization(shipmentPolicy);

app.MapGet("/api/v1/shipments/{id:guid}", async (Guid id, ShipmentDbContext database, CancellationToken cancellationToken) =>
{
    var shipment = await database.Shipments.AsNoTracking().Include(item => item.StatusHistory)
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    return shipment is null ? Results.NotFound() : Results.Ok(ToResponse(shipment));
}).RequireAuthorization(shipmentPolicy);

app.MapPost("/api/v1/shipments", async (CreateShipmentRequest request, ShipmentDbContext database,
    ClaimsPrincipal principal, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    var now = DateTime.UtcNow;
    var shipment = new Shipment
    {
        Id = Guid.NewGuid(), TrackingCode = $"SH-{now:yyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
        CustomerId = request.CustomerId.Trim(), OriginCenterId = request.OriginCenterId,
        Destination = request.Destination.Trim(), DestinationLatitude = request.DestinationLatitude,
        DestinationLongitude = request.DestinationLongitude, WeightKg = request.WeightKg,
        Priority = request.Priority, Status = ShipmentStatus.Pending, CreatedAtUtc = now, UpdatedAtUtc = now
    };
    shipment.StatusHistory.Add(NewHistory(shipment.Id, ShipmentStatus.Pending, principal, "Created", null, now));
    database.Shipments.Add(shipment);
    database.Outbox.Add(OutboxMessageFactory.Create("ShipmentCreated",
        new { shipmentId = shipment.Id, shipment.TrackingCode, shipment.CustomerId, shipment.Status }, shipment.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/v1/shipments/{shipment.Id}", ToResponse(shipment));
}).RequireAuthorization(shipmentPolicy);

app.MapPut("/api/v1/shipments/{id:guid}", async (Guid id, UpdateShipmentRequest request, ShipmentDbContext database, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    var shipment = await database.Shipments.Include(item => item.StatusHistory).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (shipment is null) return Results.NotFound();
    if (shipment.Status is not ShipmentStatus.Pending) return Results.Conflict("Only pending shipments can be edited.");
    shipment.Destination = request.Destination.Trim();
    shipment.DestinationLatitude = request.DestinationLatitude;
    shipment.DestinationLongitude = request.DestinationLongitude;
    shipment.WeightKg = request.WeightKg;
    shipment.Priority = request.Priority;
    shipment.UpdatedAtUtc = DateTime.UtcNow;
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToResponse(shipment));
}).RequireAuthorization(shipmentPolicy);

app.MapPost("/api/v1/shipments/{id:guid}/assign", async (Guid id, AssignShipmentRequest request,
    ShipmentDbContext database, IHttpClientFactory clients, HttpContext context, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
{
    var shipment = await database.Shipments.Include(item => item.StatusHistory).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (shipment is null) return Results.NotFound();
    if (shipment.Status != ShipmentStatus.Pending) return Results.Conflict("Only pending shipments can be assigned.");
    var reservation = await FleetRequest<FleetReservationResponse>(clients.CreateClient("fleet"), context,
        HttpMethod.Post, "/api/v1/reservations", new FleetReservationRequest(id, request.VehicleId, request.DriverId, shipment.WeightKg, request.IdempotencyKey.Trim()), cancellationToken);
    if (!reservation.Success) return Results.Json(reservation.ErrorBody, statusCode: reservation.StatusCode);
    var confirmed = await FleetRequest<JsonElement>(clients.CreateClient("fleet"), context, HttpMethod.Post,
        $"/api/v1/reservations/{reservation.Value!.ReservationId}/confirm", null, cancellationToken);
    if (!confirmed.Success) return Results.Json(confirmed.ErrorBody, statusCode: confirmed.StatusCode);

    shipment.VehicleId = request.VehicleId;
    shipment.DriverId = request.DriverId;
    shipment.ReservationId = reservation.Value!.ReservationId;
    shipment.Status = ShipmentStatus.Assigned;
    shipment.UpdatedAtUtc = DateTime.UtcNow;
    database.StatusHistory.Add(NewHistory(id, ShipmentStatus.Assigned, principal, "Assigned", null, shipment.UpdatedAtUtc));
    database.Outbox.Add(OutboxMessageFactory.Create("ShipmentAssigned",
        new { shipmentId = shipment.Id, shipment.VehicleId, shipment.DriverId, shipment.ReservationId }, shipment.Id.ToString()));
    try
    {
        await database.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException)
    {
        await FleetRequest<JsonElement>(clients.CreateClient("fleet"), context, HttpMethod.Post,
            $"/api/v1/reservations/{shipment.ReservationId}/release", null, cancellationToken);
        throw;
    }
    return Results.Ok(ToResponse(shipment));
}).RequireAuthorization(shipmentPolicy);

app.MapPost("/api/v1/shipments/{id:guid}/status", async (Guid id, ChangeShipmentStatusRequest request,
    ShipmentDbContext database, IHttpClientFactory clients, HttpContext context, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
{
    var shipment = await database.Shipments.Include(item => item.StatusHistory).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (shipment is null) return Results.NotFound();
    if (!ValidTransition(shipment.Status, request.Status)) return Results.Conflict($"The transition {shipment.Status} -> {request.Status} is not allowed.");
    shipment.Status = request.Status;
    shipment.UpdatedAtUtc = DateTime.UtcNow;
    database.StatusHistory.Add(NewHistory(id, request.Status, principal, "StatusChanged", request.Note, shipment.UpdatedAtUtc));
    database.Outbox.Add(OutboxMessageFactory.Create("ShipmentStatusChanged",
        new { shipmentId = shipment.Id, status = shipment.Status, note = request.Note }, shipment.Id.ToString()));
    if (request.Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled)
        database.Outbox.Add(OutboxMessageFactory.Create(
            request.Status == ShipmentStatus.Delivered ? "ShipmentDelivered" : "ShipmentCancelled",
            new { shipmentId = shipment.Id, status = shipment.Status }, shipment.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    if (request.Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled && shipment.ReservationId.HasValue)
    {
        var released = await FleetRequest<JsonElement>(clients.CreateClient("fleet"), context, HttpMethod.Post,
            $"/api/v1/reservations/{shipment.ReservationId}/release", null, cancellationToken);
        if (!released.Success) return Results.Json(released.ErrorBody, statusCode: released.StatusCode);
    }
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
static IResult? Validate<T>(T request) where T : class
{
    var errors = new List<ValidationResult>();
    return Validator.TryValidateObject(request, new ValidationContext(request), errors, true)
        ? null
        : Results.ValidationProblem(errors.GroupBy(item => item.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(item => item.Key, item => item.Select(error => error.ErrorMessage ?? "Invalid value.").ToArray()));
}
static ShipmentResponse ToResponse(Shipment item) => new(item.Id, item.TrackingCode, item.CustomerId, item.OriginCenterId,
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
    using var response = await client.SendAsync(request, cancellationToken);
    var text = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        object errorBody;
        try { errorBody = JsonSerializer.Deserialize<JsonElement>(text); }
        catch { errorBody = new { title = "Fleet request failed", detail = text }; }
        return new FleetCall<T>(false, (int)response.StatusCode, default, errorBody);
    }
    return new FleetCall<T>(true, (int)response.StatusCode,
        string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
        null);
}
record FleetCall<T>(bool Success, int StatusCode, T? Value, object? ErrorBody);

public partial class Program { }
