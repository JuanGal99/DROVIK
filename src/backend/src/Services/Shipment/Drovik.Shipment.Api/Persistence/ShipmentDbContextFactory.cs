using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Drovik.Shipment.Api.Persistence;

public sealed class ShipmentDbContextFactory : IDesignTimeDbContextFactory<ShipmentDbContext>
{
    public ShipmentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ShipmentDbContext>()
            .UseSqlServer("Server=localhost;Database=DrovikShipment;TrustServerCertificate=True")
            .Options;

        return new ShipmentDbContext(options);
    }
}
