using Drovik.Observability;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Data;
using Drovik.Fleet.Api.Application;
using Drovik.Fleet.Api.Contracts;
using Drovik.Fleet.Api.Domain;
using Drovik.Fleet.Api.Messaging;
using Drovik.Fleet.Api.Persistence;
using Drovik.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.AddDrovikObservability("fleet");
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var connectionString = builder.Configuration.GetConnectionString("FleetDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:FleetDatabase must be configured through local secrets.");

builder.Services.AddDbContext<FleetDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxDispatcher<FleetDbContext>>();
builder.Services.AddSingleton<FleetEventConsumer>();
builder.Services.AddHostedService(services => services.GetRequiredService<FleetEventConsumer>());
builder.Services.AddSingleton<ReservationExpiryWorker>();
builder.Services.AddHostedService(services => services.GetRequiredService<ReservationExpiryWorker>());
var reservationTtl = TimeSpan.FromMinutes(Math.Max(1, builder.Configuration.GetValue("Fleet:ReservationTtlMinutes", 10)));
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
        ValidateIssuer = true, ValidIssuer = issuer,
        ValidateAudience = true, ValidAudience = audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new RsaSecurityKey(publicRsa),
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
        NameClaimType = "sub", RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseDrovikRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
    await database.Database.MigrateAsync();
}

var fleetPolicy = new AuthorizeAttribute { Roles = "Administrator,Operator" };

app.MapGet("/api/v1/vehicles", async (
    FleetDbContext database, string? search, VehicleStatus? status, string? type,
    int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var query = database.Vehicles.AsNoTracking().Include(item => item.DistributionCenter).AsQueryable();
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();
        query = query.Where(item => item.Plate.Contains(term) || item.Model.Contains(term) || item.Type.Contains(term)
            || item.DistributionCenter.Name.Contains(term));
    }
    if (status.HasValue) query = query.Where(item => item.Status == status.Value);
    if (!string.IsNullOrWhiteSpace(type)) query = query.Where(item => item.Type == type);
    var totalCount = await query.CountAsync(cancellationToken);
    var entities = await query.OrderBy(item => item.Plate).Skip((page - 1) * pageSize).Take(pageSize)
        .ToListAsync(cancellationToken);
    var items = entities.Select(ToVehicleResponse).ToList();
    return Results.Ok(new PagedResponse<VehicleResponse>(items, totalCount, page, pageSize));
}).RequireAuthorization(fleetPolicy);

// The screens page through the records, so their totals have to come from the whole table.
app.MapGet("/api/v1/vehicles/summary", async (FleetDbContext database, CancellationToken cancellationToken) =>
{
    var counts = await database.Vehicles.AsNoTracking().GroupBy(item => item.Status)
        .Select(group => new { Status = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
    return Results.Ok(new StatusSummaryResponse(counts.Sum(item => item.Count), counts.ToDictionary(item => item.Status.ToString(), item => item.Count)));
}).RequireAuthorization(fleetPolicy);

app.MapGet("/api/v1/vehicles/{id:guid}", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var vehicle = await database.Vehicles.AsNoTracking().Include(item => item.DistributionCenter)
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    return vehicle is null ? Results.NotFound() : Results.Ok(ToVehicleResponse(vehicle));
}).RequireAuthorization(fleetPolicy);

app.MapPost("/api/v1/vehicles", async (CreateVehicleRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    var conflict = await ValidateVehicleReferences(database, request.Plate, request.DistributionCenterId, null, cancellationToken);
    if (conflict is not null) return conflict;
    var vehicle = new Vehicle
    {
        Id = Guid.NewGuid(), Plate = Normalize(request.Plate), Model = request.Model.Trim(), Type = request.Type.Trim(),
        CapacityKg = request.CapacityKg, CurrentOdometer = request.CurrentOdometer,
        DistributionCenterId = request.DistributionCenterId, Status = VehicleStatus.Available, UpdatedAtUtc = DateTime.UtcNow
    };
    database.Vehicles.Add(vehicle);
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.VehicleDetailsChanged, new VehicleDetailsChangedV1(vehicle.Id), vehicle.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    await database.Entry(vehicle).Reference(item => item.DistributionCenter).LoadAsync(cancellationToken);
    return Results.Created($"/api/v1/vehicles/{vehicle.Id}", ToVehicleResponse(vehicle));
}).AddEndpointFilter(TranslateConcurrencyFailure).RequireAuthorization(fleetPolicy);

app.MapPut("/api/v1/vehicles/{id:guid}", async (Guid id, UpdateVehicleRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    var vehicle = await database.Vehicles.Include(item => item.DistributionCenter).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (vehicle is null) return Results.NotFound();
    var conflict = await ValidateVehicleReferences(database, request.Plate, request.DistributionCenterId, id, cancellationToken);
    if (conflict is not null) return conflict;
    vehicle.Plate = Normalize(request.Plate);
    vehicle.Model = request.Model.Trim();
    vehicle.Type = request.Type.Trim();
    vehicle.CapacityKg = request.CapacityKg;
    vehicle.CurrentOdometer = request.CurrentOdometer;
    vehicle.DistributionCenterId = request.DistributionCenterId;
    vehicle.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.VehicleDetailsChanged, new VehicleDetailsChangedV1(vehicle.Id), vehicle.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    await database.Entry(vehicle).Reference(item => item.DistributionCenter).LoadAsync(cancellationToken);
    return Results.Ok(ToVehicleResponse(vehicle));
}).AddEndpointFilter(TranslateConcurrencyFailure).RequireAuthorization(fleetPolicy);

app.MapPatch("/api/v1/vehicles/{id:guid}/status", async (Guid id, ChangeVehicleStatusRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var vehicle = await database.Vehicles.Include(item => item.DistributionCenter).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (vehicle is null) return Results.NotFound();
    if (await ReservationLifecycle.HasActiveReservationAsync(database, id, null, null, cancellationToken))
        return Conflict("El vehículo tiene una reserva activa; libera el envío antes de cambiar su estado.");
    vehicle.Status = request.Status;
    vehicle.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.VehicleAvailabilityChanged,
        new VehicleAvailabilityChangedV1(vehicle.Id, vehicle.Status.ToString())));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToVehicleResponse(vehicle));
}).RequireAuthorization(fleetPolicy);

app.MapDelete("/api/v1/vehicles/{id:guid}", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var vehicle = await database.Vehicles.Include(item => item.DistributionCenter).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (vehicle is null) return Results.NotFound();
    if (await ReservationLifecycle.HasActiveReservationAsync(database, id, null, null, cancellationToken))
        return Conflict("El vehículo tiene una reserva activa; libera el envío antes de desactivarlo.");
    vehicle.Status = VehicleStatus.Inactive;
    vehicle.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.VehicleAvailabilityChanged,
        new VehicleAvailabilityChangedV1(vehicle.Id, vehicle.Status.ToString())));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToVehicleResponse(vehicle));
}).RequireAuthorization(fleetPolicy);

app.MapGet("/api/v1/drivers", async (
    FleetDbContext database, string? search, DriverAvailability? availability,
    int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var query = database.Drivers.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();
        query = query.Where(item => item.FullName.Contains(term) || item.LicenseCategory.Contains(term));
    }
    if (availability.HasValue) query = query.Where(item => item.Availability == availability.Value);
    var totalCount = await query.CountAsync(cancellationToken);
    var entities = await query.OrderBy(item => item.FullName).Skip((page - 1) * pageSize).Take(pageSize)
        .ToListAsync(cancellationToken);
    var items = entities.Select(ToDriverResponse).ToList();
    return Results.Ok(new PagedResponse<DriverResponse>(items, totalCount, page, pageSize));
}).RequireAuthorization(fleetPolicy);

app.MapGet("/api/v1/drivers/{id:guid}", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var driver = await database.Drivers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    return driver is null ? Results.NotFound() : Results.Ok(ToDriverResponse(driver));
}).RequireAuthorization(fleetPolicy);

app.MapPost("/api/v1/drivers", async (CreateDriverRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var validation = ValidateDriver(request.FullName, request.LicenseCategory, request.LicenseExpiresOn);
    if (validation is not null) return validation;
    if (await database.Drivers.AnyAsync(item => item.FullName == request.FullName.Trim(), cancellationToken))
        return Conflict("Ya existe un conductor con este nombre.");
    var driver = new Driver
    {
        Id = Guid.NewGuid(), FullName = request.FullName.Trim(), LicenseCategory = request.LicenseCategory.Trim().ToUpperInvariant(),
        LicenseExpiresOn = request.LicenseExpiresOn, Availability = DriverAvailability.Available, UpdatedAtUtc = DateTime.UtcNow
    };
    database.Drivers.Add(driver);
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.DriverDetailsChanged, new DriverDetailsChangedV1(driver.Id), driver.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/v1/drivers/{driver.Id}", ToDriverResponse(driver));
}).AddEndpointFilter(TranslateConcurrencyFailure).RequireAuthorization(fleetPolicy);

app.MapPut("/api/v1/drivers/{id:guid}", async (Guid id, UpdateDriverRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var validation = ValidateDriver(request.FullName, request.LicenseCategory, request.LicenseExpiresOn);
    if (validation is not null) return validation;
    var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (driver is null) return Results.NotFound();
    if (await database.Drivers.AnyAsync(item => item.Id != id && item.FullName == request.FullName.Trim(), cancellationToken))
        return Conflict("Ya existe un conductor con este nombre.");
    driver.FullName = request.FullName.Trim();
    driver.LicenseCategory = request.LicenseCategory.Trim().ToUpperInvariant();
    driver.LicenseExpiresOn = request.LicenseExpiresOn;
    driver.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.DriverDetailsChanged, new DriverDetailsChangedV1(driver.Id), driver.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToDriverResponse(driver));
}).AddEndpointFilter(TranslateConcurrencyFailure).RequireAuthorization(fleetPolicy);

app.MapPatch("/api/v1/drivers/{id:guid}/availability", async (Guid id, ChangeDriverAvailabilityRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (driver is null) return Results.NotFound();
    if (await ReservationLifecycle.HasActiveReservationAsync(database, null, id, null, cancellationToken))
        return Conflict("El conductor tiene una reserva activa; libera el envío antes de cambiar su disponibilidad.");
    driver.Availability = request.Availability;
    driver.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.DriverAvailabilityChanged,
        new DriverAvailabilityChangedV1(driver.Id, driver.Availability.ToString())));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToDriverResponse(driver));
}).RequireAuthorization(fleetPolicy);

app.MapDelete("/api/v1/drivers/{id:guid}", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (driver is null) return Results.NotFound();
    if (await ReservationLifecycle.HasActiveReservationAsync(database, null, id, null, cancellationToken))
        return Conflict("El conductor tiene una reserva activa; libera el envío antes de desactivarlo.");
    driver.Availability = DriverAvailability.Inactive;
    driver.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.DriverAvailabilityChanged,
        new DriverAvailabilityChangedV1(driver.Id, driver.Availability.ToString())));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToDriverResponse(driver));
}).RequireAuthorization(fleetPolicy);

app.MapPost("/api/v1/vehicles/demo-reset", async (FleetDbContext database, System.Security.Claims.ClaimsPrincipal principal, ILogger<FleetDbContext> logger, CancellationToken cancellationToken) =>
{
    await database.Reservations.ExecuteDeleteAsync(cancellationToken);
    await database.Vehicles.Where(item => item.Status == VehicleStatus.Assigned || item.Status == VehicleStatus.InTransit)
        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, VehicleStatus.Available), cancellationToken);
    await database.Drivers.Where(item => item.Availability == DriverAvailability.Assigned)
        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Availability, DriverAvailability.Available).SetProperty(item => item.AssignedVehicleId, (Guid?)null), cancellationToken);
    await database.Outbox.Where(item => item.PublishedAtUtc == null).ExecuteDeleteAsync(cancellationToken);
    logger.LogWarning("Reservas y disponibilidad reiniciadas por {Actor}.", principal.FindFirst("sub")?.Value);
    return Results.NoContent();
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Administrator" });

app.MapGet("/api/v1/centers", async (FleetDbContext database, CancellationToken cancellationToken) =>
    Results.Ok((await database.DistributionCenters.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name)
        .ToListAsync(cancellationToken)).Select(ToCenterResponse))).RequireAuthorization(fleetPolicy);

app.MapPost("/api/v1/reservations", async (ReserveResourcesRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    // Requests for the same vehicle or driver wait their turn instead of deadlocking. The vehicle is always locked first, so two requests cannot wait on each other.
    if (database.Database.IsSqlServer())
        foreach (var resource in new[] { $"fleet:vehicle:{request.VehicleId}", $"fleet:driver:{request.DriverId}" })
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"DECLARE @result int; EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000; IF @result < 0 THROW 51000, 'Resource busy', 1;",
                cancellationToken);
    var idempotencyKey = request.IdempotencyKey.Trim();
    var existing = await database.Reservations.AsNoTracking()
        .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
    if (existing is not null)
    {
        await transaction.CommitAsync(cancellationToken);
        if (existing.ShipmentId != request.ShipmentId || existing.VehicleId != request.VehicleId || existing.DriverId != request.DriverId)
            return Conflict("La clave de idempotencia ya pertenece a otra reserva.");
        return ReservationLifecycle.IsTerminal(existing) || (existing.Status == ReservationStatus.Reserved && existing.ExpiresAtUtc <= DateTime.UtcNow)
            ? Conflict("La reserva de esta clave ya no está vigente; solicita una nueva.")
            : Results.Ok(ToReservationResponse(existing));
    }

    var now = DateTime.UtcNow;
    var expiredReservations = await database.Reservations
        .Where(item => item.Status == ReservationStatus.Reserved && item.ExpiresAtUtc <= now &&
            (item.VehicleId == request.VehicleId || item.DriverId == request.DriverId))
        .ToListAsync(cancellationToken);
    foreach (var expired in expiredReservations) await ReservationLifecycle.ExpireAsync(database, expired, cancellationToken);

    var vehicle = await database.Vehicles.SingleOrDefaultAsync(item => item.Id == request.VehicleId, cancellationToken);
    var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == request.DriverId, cancellationToken);
    if (vehicle is null || driver is null) return Results.NotFound();
    var activeVehicleReservation = await ReservationLifecycle.HasActiveReservationAsync(database, request.VehicleId, null, null, cancellationToken);
    var activeDriverReservation = await ReservationLifecycle.HasActiveReservationAsync(database, null, request.DriverId, null, cancellationToken);
    if (activeVehicleReservation || activeDriverReservation)
        return Conflict("El vehículo o el conductor ya tienen una reserva activa.");
    if (vehicle.Status == VehicleStatus.Assigned)
    {
        vehicle.Status = VehicleStatus.Available;
    }
    if (driver.Availability == DriverAvailability.Assigned)
    {
        driver.Availability = DriverAvailability.Available;
        driver.AssignedVehicleId = null;
    }
    if (vehicle.Status != VehicleStatus.Available || driver.Availability != DriverAvailability.Available)
        return Conflict("El vehículo o el conductor no están disponibles.");
    if (vehicle.CapacityKg < request.WeightKg)
        return Conflict("La capacidad del vehículo es insuficiente para este envío.");

    var reservation = new FleetReservation
    {
        Id = Guid.NewGuid(), IdempotencyKey = idempotencyKey, ShipmentId = request.ShipmentId,
        VehicleId = vehicle.Id, DriverId = driver.Id, Status = ReservationStatus.Reserved,
        CreatedAtUtc = now, ExpiresAtUtc = now.Add(reservationTtl)
    };
    vehicle.Status = VehicleStatus.Assigned;
    vehicle.UpdatedAtUtc = now;
    driver.Availability = DriverAvailability.Assigned;
    driver.AssignedVehicleId = vehicle.Id;
    driver.UpdatedAtUtc = now;
    database.Reservations.Add(reservation);
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.VehicleAvailabilityChanged,
        new VehicleAvailabilityChangedV1(vehicle.Id, vehicle.Status.ToString(), reservation.ShipmentId), reservation.ShipmentId.ToString()));
    database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.DriverAvailabilityChanged,
        new DriverAvailabilityChangedV1(driver.Id, driver.Availability.ToString(), reservation.ShipmentId), reservation.ShipmentId.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Results.Created($"/api/v1/reservations/{reservation.Id}", ToReservationResponse(reservation));
}).AddEndpointFilter(TranslateConcurrencyFailure).RequireAuthorization(fleetPolicy);

app.MapPost("/api/v1/reservations/{id:guid}/confirm", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var reservation = await database.Reservations.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (reservation is null) return Results.NotFound();
    if (reservation.Status == ReservationStatus.Released) return Conflict("La reserva ya fue liberada.");
    if (reservation.Status == ReservationStatus.Expired || (reservation.Status == ReservationStatus.Reserved && reservation.ExpiresAtUtc <= DateTime.UtcNow))
        return Conflict("La reserva expiró.");
    if (reservation.Status == ReservationStatus.Confirmed) return Results.Ok(ToReservationResponse(reservation));
    reservation.Status = ReservationStatus.Confirmed;
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToReservationResponse(reservation));
}).AddEndpointFilter(TranslateConcurrencyFailure).RequireAuthorization(fleetPolicy);

app.MapPost("/api/v1/reservations/{id:guid}/release", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var reservation = await database.Reservations.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (reservation is null) return Results.NotFound();
    if (ReservationLifecycle.IsTerminal(reservation)) return Results.Ok(ToReservationResponse(reservation));
    await ReservationLifecycle.ReleaseAsync(database, reservation, cancellationToken);
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Results.Ok(ToReservationResponse(reservation));
}).AddEndpointFilter(TranslateConcurrencyFailure).RequireAuthorization(fleetPolicy);

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (FleetDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "Healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.Run();

static string Normalize(string value) => value.Trim().ToUpperInvariant();
static IResult? Validate<T>(T request) where T : class
{
    var context = new ValidationContext(request);
    var errors = new List<ValidationResult>();
    return Validator.TryValidateObject(request, context, errors, true)
        ? null
        : Results.ValidationProblem(errors.GroupBy(item => item.MemberNames.FirstOrDefault() ?? string.Empty)
                .ToDictionary(item => item.Key, item => item.Select(error => error.ErrorMessage ?? "Invalid value.").ToArray()),
            title: "Datos inválidos", detail: "Revisa los campos enviados.");
}
static IResult? ValidateDriver(string fullName, string licenseCategory, DateOnly expiresOn) =>
    string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 3 || fullName.Trim().Length > 100
        ? Problem(400, "Conductor inválido", "El nombre debe tener entre 3 y 100 caracteres.")
        : string.IsNullOrWhiteSpace(licenseCategory) || licenseCategory.Trim().Length > 12
            ? Problem(400, "Conductor inválido", "La categoría de licencia es obligatoria.")
            : expiresOn <= DateOnly.FromDateTime(DateTime.UtcNow)
                ? Problem(400, "Conductor inválido", "La licencia debe vencer en una fecha futura.")
                : null;
static async Task<IResult?> ValidateVehicleReferences(FleetDbContext database, string plate, Guid centerId, Guid? ignoredId, CancellationToken cancellationToken)
{
    var normalizedPlate = Normalize(plate);
    if (await database.Vehicles.AnyAsync(item => item.Plate == normalizedPlate && item.Id != ignoredId, cancellationToken))
        return Conflict("Ya existe un vehículo con esta placa.");
    if (!await database.DistributionCenters.AnyAsync(item => item.Id == centerId && item.IsActive, cancellationToken))
        return Problem(400, "Vehículo inválido", "El centro de distribución no existe o está inactivo.");
    return null;
}
static IResult Conflict(string detail) => Results.Problem(statusCode: 409, title: "Conflicto", detail: detail);
// Serializable reservations resolve races with deadlocks or unique-index violations: the loser is a conflict, not a 500.
static async ValueTask<object?> TranslateConcurrencyFailure(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
{
    const int attempts = 5;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            return await next(context);
        }
        // SQL Server picks a victim when serializable transactions collide. Nothing was saved, so the whole request can run again.
        catch (Exception ex) when (IsDeadlock(ex) && attempt < attempts)
        {
            context.HttpContext.RequestServices.GetRequiredService<FleetDbContext>().ChangeTracker.Clear();
            await Task.Delay(Random.Shared.Next(20, 120) * attempt, context.HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is DbUpdateException || IsDeadlock(ex) || ex is SqlException { Number: 51000 })
        {
            return Conflict("Otra operación modificó estos recursos al mismo tiempo; inténtalo de nuevo.");
        }
    }
}

// EF wraps the SQL error differently depending on whether it happened while reading or saving.
static bool IsDeadlock(Exception exception)
{
    for (var current = exception; current is not null; current = current.InnerException)
        if (current is SqlException { Number: 1205 }) return true;
    return false;
}
static IResult Problem(int status, string title, string detail) => Results.Problem(statusCode: status, title: title, detail: detail);
static VehicleResponse ToVehicleResponse(Vehicle item) => new(item.Id, item.Plate, item.Model, item.Type, item.CapacityKg, item.Status,
    item.CurrentOdometer, item.DistributionCenterId, item.DistributionCenter.Name, item.UpdatedAtUtc);
static DriverResponse ToDriverResponse(Driver item) => new(item.Id, item.FullName, item.LicenseCategory, item.LicenseExpiresOn,
    item.Availability, item.AssignedVehicleId, item.UpdatedAtUtc);
static CenterResponse ToCenterResponse(DistributionCenter item) => new(item.Id, item.Name, item.AddressLabel, item.Latitude, item.Longitude, item.IsActive);
static ReservationResponse ToReservationResponse(FleetReservation item) => new(item.Id, item.ShipmentId, item.VehicleId,
    item.DriverId, item.Status, item.ExpiresAtUtc);

public partial class Program { }
