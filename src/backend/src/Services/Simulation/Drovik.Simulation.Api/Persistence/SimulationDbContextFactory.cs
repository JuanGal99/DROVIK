using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Drovik.Simulation.Api.Persistence;

public sealed class SimulationDbContextFactory : IDesignTimeDbContextFactory<SimulationDbContext>
{
    public SimulationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SimulationDbContext>()
            .UseSqlServer("Server=localhost;Database=DrovikSimulation;TrustServerCertificate=True")
            .Options;

        return new SimulationDbContext(options);
    }
}
