using Microsoft.EntityFrameworkCore;
using Drovik.Messaging;
using Drovik.Fleet.Api.Domain;

namespace Drovik.Fleet.Api.Persistence;

public sealed class FleetDbContext(DbContextOptions<FleetDbContext> options) : DbContext(options), IOutboxDbContext, IInboxDbContext
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DistributionCenter> DistributionCenters => Set<DistributionCenter>();
    public DbSet<FleetReservation> Reservations => Set<FleetReservation>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Plate).IsUnique();
            entity.Property(item => item.Plate).HasMaxLength(12).IsRequired();
            entity.Property(item => item.Model).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Type).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(item => item.DistributionCenter)
                .WithMany(item => item.Vehicles)
                .HasForeignKey(item => item.DistributionCenterId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Driver>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.FullName).IsUnique();
            entity.Property(item => item.FullName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.LicenseCategory).HasMaxLength(12).IsRequired();
            entity.Property(item => item.Availability).HasConversion<string>().HasMaxLength(20);
        });
        builder.Entity<DistributionCenter>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(80).IsRequired();
            entity.Property(item => item.AddressLabel).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Latitude).HasPrecision(9, 6);
            entity.Property(item => item.Longitude).HasPrecision(9, 6);
        });
        builder.Entity<FleetReservation>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.IdempotencyKey).IsUnique();
            entity.HasIndex(item => new { item.VehicleId, item.Status });
            entity.HasIndex(item => new { item.DriverId, item.Status });
            entity.Property(item => item.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
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

        var bogota = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var puenteAranda = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var alamos = Guid.Parse("10000000-0000-0000-0000-000000000003");
        builder.Entity<DistributionCenter>().HasData(
            new DistributionCenter { Id = bogota, Name = "Bogotá · Fontibón", AddressLabel = "Zona industrial Fontibón", Latitude = 4.6786m, Longitude = -74.1416m, IsActive = true },
            new DistributionCenter { Id = puenteAranda, Name = "Bogotá · Puente Aranda", AddressLabel = "Zona industrial Puente Aranda", Latitude = 4.6186m, Longitude = -74.1166m, IsActive = true },
            new DistributionCenter { Id = alamos, Name = "Bogotá · Álamos", AddressLabel = "Zona industrial Álamos", Latitude = 4.6970m, Longitude = -74.1170m, IsActive = true });
        builder.Entity<Vehicle>().HasData(
            new Vehicle { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Plate = "TRK-018", Model = "Hino 500", Type = "Camión", CapacityKg = 8000, Status = VehicleStatus.Available, CurrentOdometer = 128430, DistributionCenterId = bogota, UpdatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc) },
            new Vehicle { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Plate = "VAN-006", Model = "Renault Master", Type = "Furgón", CapacityKg = 1600, Status = VehicleStatus.Available, CurrentOdometer = 82450, DistributionCenterId = puenteAranda, UpdatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc) },
            new Vehicle { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Plate = "TRK-021", Model = "JAC HFC", Type = "Camión", CapacityKg = 5000, Status = VehicleStatus.Maintenance, CurrentOdometer = 45920, DistributionCenterId = alamos, UpdatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc) });
        builder.Entity<Driver>().HasData(
            new Driver { Id = Guid.Parse("30000000-0000-0000-0000-000000000001"), FullName = "Laura Méndez", LicenseCategory = "C2", LicenseExpiresOn = new DateOnly(2027, 4, 18), Availability = DriverAvailability.Available, UpdatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc) },
            new Driver { Id = Guid.Parse("30000000-0000-0000-0000-000000000002"), FullName = "Andrés Salazar", LicenseCategory = "C1", LicenseExpiresOn = new DateOnly(2027, 9, 2), Availability = DriverAvailability.Available, UpdatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc) },
            new Driver { Id = Guid.Parse("30000000-0000-0000-0000-000000000003"), FullName = "Camila Rojas", LicenseCategory = "C2", LicenseExpiresOn = new DateOnly(2027, 1, 27), Availability = DriverAvailability.Available, UpdatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc) });
    }
}
