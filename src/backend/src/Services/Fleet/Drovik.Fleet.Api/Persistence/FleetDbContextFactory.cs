using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Drovik.Fleet.Api.Persistence;

public sealed class FleetDbContextFactory : IDesignTimeDbContextFactory<FleetDbContext>
{
    public FleetDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<FleetDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("FleetDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:FleetDatabase must be configured through local secrets.");
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new FleetDbContext(options);
    }
}
