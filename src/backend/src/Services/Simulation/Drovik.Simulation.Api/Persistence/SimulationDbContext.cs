using Drovik.Messaging;
using Drovik.Simulation.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Simulation.Api.Persistence;

public sealed class SimulationDbContext(DbContextOptions<SimulationDbContext> options) : DbContext(options), IOutboxDbContext
{
    public DbSet<SimulationRun> Runs => Set<SimulationRun>();
    public DbSet<SimulatedVehicle> Vehicles => Set<SimulatedVehicle>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<SimulationRun>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
            // At most one run can be active: the database settles two simultaneous starts.
            entity.HasIndex(item => item.Status).IsUnique().HasFilter("[Status] IN ('Running', 'Paused')");
        });
        builder.Entity<SimulatedVehicle>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.RunId, item.ShipmentId }).IsUnique();
            entity.Property(item => item.TrackingCode).HasMaxLength(30).IsRequired();
            entity.Property(item => item.OriginName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.DestinationName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.RouteJson).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(item => item.Run).WithMany(item => item.Vehicles).HasForeignKey(item => item.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.PublishedAtUtc, item.NextAttemptAtUtc, item.OccurredAtUtc });
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Payload).HasMaxLength(10000).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(1000);
        });
    }
}
