using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Drovik.Fleet.Api.Persistence;

public sealed class FleetDbContextFactory : IDesignTimeDbContextFactory<FleetDbContext>
{
    public FleetDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseSqlServer("Server=localhost;Database=DrovikFleet;TrustServerCertificate=True")
            .Options;

        return new FleetDbContext(options);
    }
}
