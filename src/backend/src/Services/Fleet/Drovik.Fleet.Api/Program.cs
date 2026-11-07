using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Data;
using Drovik.Fleet.Api.Contracts;
using Drovik.Fleet.Api.Domain;
using Drovik.Fleet.Api.Persistence;
using Drovik.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var connectionString = builder.Configuration.GetConnectionString("FleetDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:FleetDatabase must be configured through local secrets.");

builder.Services.AddDbContext<FleetDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxDispatcher<FleetDbContext>>();
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
    await database.SaveChangesAsync(cancellationToken);
    await database.Entry(vehicle).Reference(item => item.DistributionCenter).LoadAsync(cancellationToken);
    return Results.Created($"/api/v1/vehicles/{vehicle.Id}", ToVehicleResponse(vehicle));
}).RequireAuthorization(fleetPolicy);

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
    await database.SaveChangesAsync(cancellationToken);
    await database.Entry(vehicle).Reference(item => item.DistributionCenter).LoadAsync(cancellationToken);
    return Results.Ok(ToVehicleResponse(vehicle));
}).RequireAuthorization(fleetPolicy);

app.MapPatch("/api/v1/vehicles/{id:guid}/status", async (Guid id, ChangeVehicleStatusRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var vehicle = await database.Vehicles.Include(item => item.DistributionCenter).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (vehicle is null) return Results.NotFound();
    vehicle.Status = request.Status;
    vehicle.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create("VehicleAvailabilityChanged",
        new { vehicleId = vehicle.Id, status = vehicle.Status, occurredAtUtc = vehicle.UpdatedAtUtc }));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToVehicleResponse(vehicle));
}).RequireAuthorization(fleetPolicy);

app.MapDelete("/api/v1/vehicles/{id:guid}", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var vehicle = await database.Vehicles.Include(item => item.DistributionCenter).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (vehicle is null) return Results.NotFound();
    vehicle.Status = VehicleStatus.Inactive;
    vehicle.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create("VehicleAvailabilityChanged",
        new { vehicleId = vehicle.Id, status = vehicle.Status, occurredAtUtc = vehicle.UpdatedAtUtc }));
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
        return Conflict("A driver with this name already exists.");
    var driver = new Driver
    {
        Id = Guid.NewGuid(), FullName = request.FullName.Trim(), LicenseCategory = request.LicenseCategory.Trim().ToUpperInvariant(),
        LicenseExpiresOn = request.LicenseExpiresOn, Availability = DriverAvailability.Available, UpdatedAtUtc = DateTime.UtcNow
    };
    database.Drivers.Add(driver);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/v1/drivers/{driver.Id}", ToDriverResponse(driver));
}).RequireAuthorization(fleetPolicy);

app.MapPut("/api/v1/drivers/{id:guid}", async (Guid id, UpdateDriverRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var validation = ValidateDriver(request.FullName, request.LicenseCategory, request.LicenseExpiresOn);
    if (validation is not null) return validation;
    var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (driver is null) return Results.NotFound();
    if (await database.Drivers.AnyAsync(item => item.Id != id && item.FullName == request.FullName.Trim(), cancellationToken))
        return Conflict("A driver with this name already exists.");
    driver.FullName = request.FullName.Trim();
    driver.LicenseCategory = request.LicenseCategory.Trim().ToUpperInvariant();
    driver.LicenseExpiresOn = request.LicenseExpiresOn;
    driver.UpdatedAtUtc = DateTime.UtcNow;
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToDriverResponse(driver));
}).RequireAuthorization(fleetPolicy);

app.MapPatch("/api/v1/drivers/{id:guid}/availability", async (Guid id, ChangeDriverAvailabilityRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (driver is null) return Results.NotFound();
    driver.Availability = request.Availability;
    driver.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create("DriverAvailabilityChanged",
        new { driverId = driver.Id, availability = driver.Availability, occurredAtUtc = driver.UpdatedAtUtc }));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToDriverResponse(driver));
}).RequireAuthorization(fleetPolicy);

app.MapDelete("/api/v1/drivers/{id:guid}", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (driver is null) return Results.NotFound();
    driver.Availability = DriverAvailability.Inactive;
    driver.UpdatedAtUtc = DateTime.UtcNow;
    database.Outbox.Add(OutboxMessageFactory.Create("DriverAvailabilityChanged",
        new { driverId = driver.Id, availability = driver.Availability, occurredAtUtc = driver.UpdatedAtUtc }));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToDriverResponse(driver));
}).RequireAuthorization(fleetPolicy);

app.MapGet("/api/v1/centers", async (FleetDbContext database, CancellationToken cancellationToken) =>
    Results.Ok((await database.DistributionCenters.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name)
        .ToListAsync(cancellationToken)).Select(ToCenterResponse))).RequireAuthorization(fleetPolicy);

app.MapPost("/api/v1/reservations", async (ReserveResourcesRequest request, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var validation = Validate(request);
    if (validation is not null) return validation;
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var idempotencyKey = request.IdempotencyKey.Trim();
    var existing = await database.Reservations.AsNoTracking()
        .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
    if (existing is not null)
    {
        await transaction.CommitAsync(cancellationToken);
        return existing.ShipmentId == request.ShipmentId && existing.VehicleId == request.VehicleId && existing.DriverId == request.DriverId
            ? Results.Ok(ToReservationResponse(existing))
            : Conflict("The idempotency key is already associated with another reservation.");
    }

    var now = DateTime.UtcNow;
    var expiredReservations = await database.Reservations
        .Where(item => item.Status == ReservationStatus.Reserved && item.ExpiresAtUtc <= now &&
            (item.VehicleId == request.VehicleId || item.DriverId == request.DriverId))
        .ToListAsync(cancellationToken);
    foreach (var expired in expiredReservations) expired.Status = ReservationStatus.Released;

    var vehicle = await database.Vehicles.SingleOrDefaultAsync(item => item.Id == request.VehicleId, cancellationToken);
    var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == request.DriverId, cancellationToken);
    if (vehicle is null || driver is null) return Results.NotFound();
    var activeVehicleReservation = await database.Reservations.AnyAsync(item =>
        item.VehicleId == request.VehicleId &&
        (item.Status == ReservationStatus.Confirmed ||
         (item.Status == ReservationStatus.Reserved && item.ExpiresAtUtc > now)), cancellationToken);
    var activeDriverReservation = await database.Reservations.AnyAsync(item =>
        item.DriverId == request.DriverId &&
        (item.Status == ReservationStatus.Confirmed ||
         (item.Status == ReservationStatus.Reserved && item.ExpiresAtUtc > now)), cancellationToken);
    if (!activeVehicleReservation && vehicle.Status == VehicleStatus.Assigned)
    {
        vehicle.Status = VehicleStatus.Available;
    }
    if (!activeDriverReservation && driver.Availability == DriverAvailability.Assigned)
    {
        driver.Availability = DriverAvailability.Available;
        driver.AssignedVehicleId = null;
    }
    if (vehicle.Status != VehicleStatus.Available || driver.Availability != DriverAvailability.Available)
        return Conflict("The vehicle or driver is not available.");
    if (vehicle.CapacityKg < request.WeightKg)
        return Conflict("The vehicle capacity is insufficient for this shipment.");

    var reservation = new FleetReservation
    {
        Id = Guid.NewGuid(), IdempotencyKey = idempotencyKey, ShipmentId = request.ShipmentId,
        VehicleId = vehicle.Id, DriverId = driver.Id, Status = ReservationStatus.Reserved,
        CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(10)
    };
    vehicle.Status = VehicleStatus.Assigned;
    vehicle.UpdatedAtUtc = now;
    driver.Availability = DriverAvailability.Assigned;
    driver.AssignedVehicleId = vehicle.Id;
    driver.UpdatedAtUtc = now;
    database.Reservations.Add(reservation);
    database.Outbox.Add(OutboxMessageFactory.Create("VehicleAvailabilityChanged",
        new { vehicleId = vehicle.Id, status = vehicle.Status, shipmentId = reservation.ShipmentId }, reservation.ShipmentId.ToString()));
    database.Outbox.Add(OutboxMessageFactory.Create("DriverAvailabilityChanged",
        new { driverId = driver.Id, availability = driver.Availability, shipmentId = reservation.ShipmentId }, reservation.ShipmentId.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Results.Created($"/api/v1/reservations/{reservation.Id}", ToReservationResponse(reservation));
}).RequireAuthorization(fleetPolicy);

app.MapPost("/api/v1/reservations/{id:guid}/confirm", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    var reservation = await database.Reservations.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (reservation is null) return Results.NotFound();
    if (reservation.Status == ReservationStatus.Released) return Conflict("The reservation has already been released.");
    if (reservation.Status == ReservationStatus.Reserved && reservation.ExpiresAtUtc <= DateTime.UtcNow)
        return Conflict("The reservation has expired.");
    if (reservation.Status == ReservationStatus.Confirmed) return Results.Ok(ToReservationResponse(reservation));
    reservation.Status = ReservationStatus.Confirmed;
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToReservationResponse(reservation));
}).RequireAuthorization(fleetPolicy);

app.MapPost("/api/v1/reservations/{id:guid}/release", async (Guid id, FleetDbContext database, CancellationToken cancellationToken) =>
{
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var reservation = await database.Reservations.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (reservation is null) return Results.NotFound();
    if (reservation.Status == ReservationStatus.Released) return Results.Ok(ToReservationResponse(reservation));
    var now = DateTime.UtcNow;
    var activeOtherReservation = await database.Reservations.AnyAsync(item =>
        item.Id != reservation.Id &&
        (item.Status == ReservationStatus.Confirmed ||
         (item.Status == ReservationStatus.Reserved && item.ExpiresAtUtc > now)) &&
        (item.VehicleId == reservation.VehicleId || item.DriverId == reservation.DriverId), cancellationToken);
    var vehicle = await database.Vehicles.SingleOrDefaultAsync(item => item.Id == reservation.VehicleId, cancellationToken);
    var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == reservation.DriverId, cancellationToken);
    if (!activeOtherReservation && vehicle is not null)
    {
        vehicle.Status = VehicleStatus.Available;
        vehicle.UpdatedAtUtc = DateTime.UtcNow;
        database.Outbox.Add(OutboxMessageFactory.Create("VehicleAvailabilityChanged",
            new { vehicleId = vehicle.Id, status = vehicle.Status, shipmentId = reservation.ShipmentId }, reservation.ShipmentId.ToString()));
    }
    if (!activeOtherReservation && driver is not null)
    {
        driver.Availability = DriverAvailability.Available;
        driver.AssignedVehicleId = null;
        driver.UpdatedAtUtc = DateTime.UtcNow;
        database.Outbox.Add(OutboxMessageFactory.Create("DriverAvailabilityChanged",
            new { driverId = driver.Id, availability = driver.Availability, shipmentId = reservation.ShipmentId }, reservation.ShipmentId.ToString()));
    }
    reservation.Status = ReservationStatus.Released;
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Results.Ok(ToReservationResponse(reservation));
}).RequireAuthorization(fleetPolicy);

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
            .ToDictionary(item => item.Key, item => item.Select(error => error.ErrorMessage ?? "Invalid value.").ToArray()));
}
static IResult? ValidateDriver(string fullName, string licenseCategory, DateOnly expiresOn) =>
    string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 3 || fullName.Trim().Length > 100
        ? Problem(400, "Invalid driver", "Full name must contain between 3 and 100 characters.")
        : string.IsNullOrWhiteSpace(licenseCategory) || licenseCategory.Trim().Length > 12
            ? Problem(400, "Invalid driver", "License category is required.")
            : expiresOn <= DateOnly.FromDateTime(DateTime.UtcNow)
                ? Problem(400, "Invalid driver", "License expiration must be in the future.")
                : null;
static async Task<IResult?> ValidateVehicleReferences(FleetDbContext database, string plate, Guid centerId, Guid? ignoredId, CancellationToken cancellationToken)
{
    var normalizedPlate = Normalize(plate);
    if (await database.Vehicles.AnyAsync(item => item.Plate == normalizedPlate && item.Id != ignoredId, cancellationToken))
        return Conflict("A vehicle with this plate already exists.");
    if (!await database.DistributionCenters.AnyAsync(item => item.Id == centerId && item.IsActive, cancellationToken))
        return Problem(400, "Invalid vehicle", "The distribution center does not exist or is inactive.");
    return null;
}
static IResult Conflict(string detail) => Results.Problem(statusCode: 409, title: "Conflict", detail: detail);
static IResult Problem(int status, string title, string detail) => Results.Problem(statusCode: status, title: title, detail: detail);
static VehicleResponse ToVehicleResponse(Vehicle item) => new(item.Id, item.Plate, item.Model, item.Type, item.CapacityKg, item.Status,
    item.CurrentOdometer, item.DistributionCenterId, item.DistributionCenter.Name, item.UpdatedAtUtc);
static DriverResponse ToDriverResponse(Driver item) => new(item.Id, item.FullName, item.LicenseCategory, item.LicenseExpiresOn,
    item.Availability, item.AssignedVehicleId, item.UpdatedAtUtc);
static CenterResponse ToCenterResponse(DistributionCenter item) => new(item.Id, item.Name, item.AddressLabel, item.Latitude, item.Longitude, item.IsActive);
static ReservationResponse ToReservationResponse(FleetReservation item) => new(item.Id, item.ShipmentId, item.VehicleId,
    item.DriverId, item.Status, item.ExpiresAtUtc);

public partial class Program { }
