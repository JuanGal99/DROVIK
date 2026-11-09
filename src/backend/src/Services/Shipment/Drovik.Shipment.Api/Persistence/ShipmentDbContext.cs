using Microsoft.EntityFrameworkCore;
using Drovik.Shipment.Api.Domain;
using Drovik.Messaging;

namespace Drovik.Shipment.Api.Persistence;

public sealed class ShipmentDbContext(DbContextOptions<ShipmentDbContext> options) : DbContext(options), IOutboxDbContext, IInboxDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Domain.Shipment> Shipments => Set<Domain.Shipment>();
    public DbSet<ShipmentStatusHistory> StatusHistory => Set<ShipmentStatusHistory>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Customer>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => item.IsActive);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.AddressLabel).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Latitude).HasPrecision(9, 6);
            entity.Property(item => item.Longitude).HasPrecision(9, 6);
        });
        builder.Entity<Domain.Shipment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TrackingCode).IsUnique();
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.CustomerId);
            entity.Property(item => item.TrackingCode).HasMaxLength(30).IsRequired();
            entity.HasOne(item => item.Customer).WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OriginCustomer).WithMany().HasForeignKey(item => item.OriginCustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.Origin).HasMaxLength(200);
            entity.Property(item => item.OriginLatitude).HasPrecision(9, 6);
            entity.Property(item => item.OriginLongitude).HasPrecision(9, 6);
            entity.Property(item => item.Destination).HasMaxLength(200).IsRequired();
            entity.Property(item => item.DestinationLatitude).HasPrecision(9, 6);
            entity.Property(item => item.DestinationLongitude).HasPrecision(9, 6);
            entity.Property(item => item.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
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

        var seededAt = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);
        builder.Entity<Customer>().HasData(
            new Customer { Id = Guid.Parse("40000000-0000-0000-0000-000000000001"), Name = "Mercado Central", AddressLabel = "Chapinero", Latitude = 4.648600m, Longitude = -74.062800m, IsActive = true, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new Customer { Id = Guid.Parse("40000000-0000-0000-0000-000000000002"), Name = "Farmacia Norte", AddressLabel = "Usaquén", Latitude = 4.720900m, Longitude = -74.030100m, IsActive = true, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new Customer { Id = Guid.Parse("40000000-0000-0000-0000-000000000003"), Name = "Distribuidora Sabana", AddressLabel = "Engativá", Latitude = 4.705600m, Longitude = -74.112000m, IsActive = true, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new Customer { Id = Guid.Parse("40000000-0000-0000-0000-000000000004"), Name = "Alimentos del Valle", AddressLabel = "Kennedy", Latitude = 4.628000m, Longitude = -74.153000m, IsActive = true, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new Customer { Id = Guid.Parse("40000000-0000-0000-0000-000000000005"), Name = "Comercial Andina", AddressLabel = "La Candelaria", Latitude = 4.598100m, Longitude = -74.075800m, IsActive = true, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new Customer { Id = Guid.Parse("40000000-0000-0000-0000-000000000006"), Name = "Casa Verde", AddressLabel = "Suba", Latitude = 4.741000m, Longitude = -74.084000m, IsActive = true, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new Customer { Id = Guid.Parse("40000000-0000-0000-0000-000000000007"), Name = "Ferretería del Pacífico", AddressLabel = "Teusaquillo", Latitude = 4.637000m, Longitude = -74.079000m, IsActive = true, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new Customer { Id = Guid.Parse("40000000-0000-0000-0000-000000000008"), Name = "Suministros del Sur", AddressLabel = "Tunjuelito", Latitude = 4.576000m, Longitude = -74.133000m, IsActive = true, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt });
    }
}
