using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Drovik.Incident.Api.Persistence;

public sealed class IncidentDbContextFactory : IDesignTimeDbContextFactory<IncidentDbContext>
{
    public IncidentDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<IncidentDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        // Design-time tools only need a provider to read the model, so a clean machine works without secrets.
        var connectionString = configuration.GetConnectionString("IncidentDatabase")
            ?? "Server=localhost;Database=DrovikIncident;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<IncidentDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new IncidentDbContext(options);
    }
}
