using Microsoft.EntityFrameworkCore;
using Drovik.Shipment.Api.Domain;
using Drovik.Messaging;

namespace Drovik.Shipment.Api.Persistence;

public sealed class ShipmentDbContext(DbContextOptions<ShipmentDbContext> options) : DbContext(options), IOutboxDbContext
{
    public DbSet<Domain.Shipment> Shipments => Set<Domain.Shipment>();
    public DbSet<ShipmentStatusHistory> StatusHistory => Set<ShipmentStatusHistory>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Domain.Shipment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TrackingCode).IsUnique();
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.CustomerId);
            entity.Property(item => item.TrackingCode).HasMaxLength(30).IsRequired();
            entity.Property(item => item.CustomerId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Destination).HasMaxLength(200).IsRequired();
            entity.Property(item => item.DestinationLatitude).HasPrecision(9, 6);
            entity.Property(item => item.DestinationLongitude).HasPrecision(9, 6);
            entity.Property(item => item.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        });
        builder.Entity<ShipmentStatusHistory>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ActorId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Note).HasMaxLength(500);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(item => item.Shipment).WithMany(item => item.StatusHistory)
                .HasForeignKey(item => item.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.PublishedAtUtc, item.NextAttemptAtUtc, item.OccurredAtUtc });
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Payload).HasMaxLength(10000).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(1000);
        });
        builder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(item => item.EventId);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
        });
    }
}
