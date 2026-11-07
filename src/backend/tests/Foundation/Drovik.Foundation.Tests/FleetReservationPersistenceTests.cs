using Drovik.Fleet.Api.Domain;
using Drovik.Fleet.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Foundation.Tests;

public sealed class FleetReservationPersistenceTests
{
    [Fact]
    public async Task Idempotency_key_is_unique_in_the_fleet_store()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Reservations.Add(new FleetReservation
        {
            Id = Guid.NewGuid(), IdempotencyKey = "same-key", ShipmentId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(), DriverId = Guid.NewGuid(), Status = ReservationStatus.Reserved,
            CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        });
        await context.SaveChangesAsync();

        context.Reservations.Add(new FleetReservation
        {
            Id = Guid.NewGuid(), IdempotencyKey = "same-key", ShipmentId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(), DriverId = Guid.NewGuid(), Status = ReservationStatus.Reserved,
            CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static FleetDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<FleetDbContext>().UseSqlite(connection).Options);
}
