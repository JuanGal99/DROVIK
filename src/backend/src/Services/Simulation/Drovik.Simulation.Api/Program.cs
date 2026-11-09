using Drovik.Observability;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Drovik.Messaging;
using Drovik.Simulation.Api.Application;
using Drovik.Simulation.Api.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string HubPath = "/hubs/operations";

var builder = WebApplication.CreateBuilder(args);
builder.AddDrovikObservability("simulation");
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var connectionString = builder.Configuration.GetConnectionString("SimulationDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:SimulationDatabase must be configured through local secrets.");

builder.Services.AddDbContext<SimulationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxDispatcher<SimulationDbContext>>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LocalRouteProvider>();
builder.Services.AddSingleton<IRouteProvider, DeliveryNetworkRouteProvider>();
builder.Services.AddScoped<ShipmentFeed>();
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddHostedService(services => services.GetRequiredService<SimulationEngine>());
builder.Services.AddSingleton<DataChangeRelay>();
builder.Services.AddHostedService(services => services.GetRequiredService<DataChangeRelay>());
builder.Services.AddHttpClient("shipment", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Shipment:BaseUrl"] ?? "http://localhost:5203");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSignalR();

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
    // WebSockets cannot carry an Authorization header, so only the hub accepts the token in the query string.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.HttpContext.Request.Path.StartsWithSegments(HubPath))
                context.Token = context.Request.Query["access_token"];
            return Task.CompletedTask;
        }
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
    var database = scope.ServiceProvider.GetRequiredService<SimulationDbContext>();
    await database.Database.MigrateAsync();
}

app.MapLiveOperations();
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (SimulationDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "Healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.Run();

public partial class Program { }
