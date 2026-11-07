using Drovik.Fleet.Api.Persistence;
using Drovik.Identity.Api.Persistence;
using Drovik.Incident.Api.Persistence;
using Drovik.Shipment.Api.Persistence;
using Drovik.Simulation.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Foundation.Tests;

public class InitialMigrationTests
{
    [Fact]
    public void Identity_database_has_initial_and_authentication_migrations()
    {
        using var context = new IdentityDbContextFactory().CreateDbContext([]);
        Assert.Contains("20261107001034_InitialCreate", context.Database.GetMigrations());
        Assert.Contains(context.Database.GetMigrations(), migration => migration.EndsWith("_IdentityAuthentication", StringComparison.Ordinal));
    }

    [Fact]
    public void Fleet_database_has_initial_migration() => AssertInitialMigration(new FleetDbContextFactory().CreateDbContext([]));

    [Fact]
    public void Shipment_database_has_initial_migration() => AssertInitialMigration(new ShipmentDbContextFactory().CreateDbContext([]));

    [Fact]
    public void Simulation_database_has_initial_migration() => AssertInitialMigration(new SimulationDbContextFactory().CreateDbContext([]));

    [Fact]
    public void Incident_database_has_initial_migration() => AssertInitialMigration(new IncidentDbContextFactory().CreateDbContext([]));

    private static void AssertInitialMigration(DbContext context)
    {
        using (context)
        {
            Assert.Single(context.Database.GetMigrations());
        }
    }
}
