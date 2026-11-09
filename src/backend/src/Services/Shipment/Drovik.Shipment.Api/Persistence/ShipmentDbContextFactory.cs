using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Drovik.Shipment.Api.Persistence;

public sealed class ShipmentDbContextFactory : IDesignTimeDbContextFactory<ShipmentDbContext>
{
    public ShipmentDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<ShipmentDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        // Design-time tools only need a provider to read the model, so a clean machine works without secrets.
        var connectionString = configuration.GetConnectionString("ShipmentDatabase")
            ?? "Server=localhost;Database=DrovikShipment;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ShipmentDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ShipmentDbContext(options);
    }
}
