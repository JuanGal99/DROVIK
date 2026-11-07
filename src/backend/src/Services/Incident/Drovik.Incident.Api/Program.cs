using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Drovik.Incident.Api.Contracts;
using Drovik.Incident.Api.Domain;
using Drovik.Incident.Api.Persistence;
using Drovik.Messaging;
using Drovik.Incident.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("IncidentDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:IncidentDatabase must be configured through local secrets.");

builder.Services.AddDbContext<IncidentDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxDispatcher<IncidentDbContext>>();
builder.Services.AddHostedService<IncidentEventConsumer>();
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
    var database = scope.ServiceProvider.GetRequiredService<IncidentDbContext>();
    await database.Database.MigrateAsync();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (IncidentDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "Healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

var incidentPolicy = new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = "Administrator,Operator" };

app.MapGet("/api/v1/incidents", async (
    IncidentDbContext database,
    string? status,
    string? severity,
    string? type,
    DateTime? fromUtc,
    DateTime? toUtc,
    int page,
    int pageSize,
    CancellationToken cancellationToken) =>
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize == 0 ? 20 : pageSize, 1, 100);
    var query = database.Incidents.AsNoTracking().Include(item => item.Activities).AsQueryable();
    if (Enum.TryParse<IncidentStatus>(status, true, out var parsedStatus)) query = query.Where(item => item.Status == parsedStatus);
    if (Enum.TryParse<IncidentSeverity>(severity, true, out var parsedSeverity)) query = query.Where(item => item.Severity == parsedSeverity);
    if (Enum.TryParse<IncidentType>(type, true, out var parsedType)) query = query.Where(item => item.Type == parsedType);
    if (fromUtc.HasValue) query = query.Where(item => item.CreatedAtUtc >= fromUtc.Value.ToUniversalTime());
    if (toUtc.HasValue) query = query.Where(item => item.CreatedAtUtc <= toUtc.Value.ToUniversalTime());
    var totalCount = await query.CountAsync(cancellationToken);
    var items = await query.OrderByDescending(item => item.CreatedAtUtc)
        .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    return Results.Ok(new PagedIncidentsResponse(items.Select(ToResponse).ToList(), totalCount, page, pageSize));
}).RequireAuthorization(incidentPolicy);

app.MapGet("/api/v1/incidents/{id:guid}", async (Guid id, IncidentDbContext database, CancellationToken cancellationToken) =>
{
    var incident = await database.Incidents.AsNoTracking().Include(item => item.Activities)
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    return incident is null ? Results.NotFound() : Results.Ok(ToResponse(incident));
}).RequireAuthorization(incidentPolicy);

app.MapPost("/api/v1/incidents", async (
    CreateIncidentRequest request,
    IncidentDbContext database,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 2000)
        return Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", "La descripción debe tener entre 1 y 2000 caracteres.");
    if (!TryActor(context, out var actorId)) return Results.Unauthorized();
    var now = DateTime.UtcNow;
    var incident = new IncidentRecord
    {
        Id = Guid.NewGuid(), Type = request.Type, Severity = request.Severity, Status = IncidentStatus.Open,
        Description = request.Description.Trim(), VehicleId = request.VehicleId, ShipmentId = request.ShipmentId,
        CreatedAtUtc = now, CreatedBy = actorId,
        Activities = [new IncidentActivity { Id = Guid.NewGuid(), Status = IncidentStatus.Open, ActorId = actorId, OccurredAtUtc = now }]
    };
    database.Incidents.Add(incident);
    database.Outbox.Add(OutboxMessageFactory.Create("IncidentCreated",
        new { incidentId = incident.Id, incident.Type, incident.Severity, incident.Status, incident.VehicleId, incident.ShipmentId },
        incident.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/v1/incidents/{incident.Id}", ToResponse(incident));
}).RequireAuthorization(incidentPolicy);

app.MapPatch("/api/v1/incidents/{id:guid}/status", async (
    Guid id,
    ChangeIncidentStatusRequest request,
    IncidentDbContext database,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (!TryActor(context, out var actorId)) return Results.Unauthorized();
    var incident = await database.Incidents.Include(item => item.Activities).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (incident is null) return Results.NotFound();
    if (!IsValidTransition(incident.Status, request.Status))
        return Problem(StatusCodes.Status409Conflict, "Transición inválida", $"No se puede pasar de {incident.Status} a {request.Status}.");
    var now = DateTime.UtcNow;
    incident.Status = request.Status;
    incident.Activities.Add(new IncidentActivity { Id = Guid.NewGuid(), Status = request.Status, Note = request.Note?.Trim(), ActorId = actorId, OccurredAtUtc = now });
    database.Outbox.Add(OutboxMessageFactory.Create("IncidentStatusChanged",
        new { incidentId = incident.Id, status = incident.Status, note = request.Note }, incident.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToResponse(incident));
}).RequireAuthorization(incidentPolicy);

app.MapPost("/api/v1/incidents/{id:guid}/resolve", async (
    Guid id,
    ResolveIncidentRequest request,
    IncidentDbContext database,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ResolutionNote) || request.ResolutionNote.Length > 1000)
        return Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", "La información de resolución es obligatoria y no puede superar 1000 caracteres.");
    if (!TryActor(context, out var actorId)) return Results.Unauthorized();
    var incident = await database.Incidents.Include(item => item.Activities).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (incident is null) return Results.NotFound();
    if (!IsValidTransition(incident.Status, IncidentStatus.Resolved))
        return Problem(StatusCodes.Status409Conflict, "Transición inválida", $"No se puede resolver un incidente en estado {incident.Status}.");
    var now = DateTime.UtcNow;
    incident.Status = IncidentStatus.Resolved;
    incident.ResolvedAtUtc = now;
    incident.ResolvedBy = actorId;
    incident.ResolutionNote = request.ResolutionNote.Trim();
    incident.Activities.Add(new IncidentActivity { Id = Guid.NewGuid(), Status = IncidentStatus.Resolved, Note = incident.ResolutionNote, ActorId = actorId, OccurredAtUtc = now });
    database.Outbox.Add(OutboxMessageFactory.Create("IncidentResolved",
        new { incidentId = incident.Id, status = incident.Status, resolutionNote = incident.ResolutionNote }, incident.Id.ToString()));
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToResponse(incident));
}).RequireAuthorization(incidentPolicy);

app.Run();

static bool TryActor(HttpContext context, out Guid actorId) =>
    Guid.TryParse(context.User.FindFirstValue("sub"), out actorId);

static bool IsValidTransition(IncidentStatus current, IncidentStatus next) =>
    (current, next) switch
    {
        (IncidentStatus.Open, IncidentStatus.InProgress) => true,
        (IncidentStatus.Open, IncidentStatus.Resolved) => true,
        (IncidentStatus.InProgress, IncidentStatus.Resolved) => true,
        (IncidentStatus.Resolved, IncidentStatus.Closed) => true,
        _ => false
    };

static IncidentResponse ToResponse(IncidentRecord incident) =>
    new(incident.Id, incident.Type, incident.Severity, incident.Status, incident.Description,
        incident.VehicleId, incident.ShipmentId, incident.CreatedAtUtc, incident.ResolvedAtUtc,
        incident.ResolutionNote, incident.CreatedBy, incident.ResolvedBy,
        incident.Activities.OrderBy(item => item.OccurredAtUtc)
            .Select(item => new IncidentActivityResponse(item.Id, item.Status, item.Note, item.ActorId, item.OccurredAtUtc)).ToList());

static IResult Problem(int status, string title, string detail) =>
    Results.Problem(statusCode: status, title: title, detail: detail);

public partial class Program { }
