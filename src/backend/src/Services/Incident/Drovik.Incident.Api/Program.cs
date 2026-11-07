using Microsoft.EntityFrameworkCore;
using Drovik.Incident.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("IncidentDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:IncidentDatabase must be configured through local secrets.");

builder.Services.AddDbContext<IncidentDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddHealthChecks();

var app = builder.Build();

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

app.Run();

public partial class Program { }
