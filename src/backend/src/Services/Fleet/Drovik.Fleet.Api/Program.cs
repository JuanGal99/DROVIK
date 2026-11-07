using Microsoft.EntityFrameworkCore;
using Drovik.Fleet.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("FleetDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:FleetDatabase must be configured through local secrets.");

builder.Services.AddDbContext<FleetDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
    await database.Database.MigrateAsync();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (FleetDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "Healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.Run();

public partial class Program { }
