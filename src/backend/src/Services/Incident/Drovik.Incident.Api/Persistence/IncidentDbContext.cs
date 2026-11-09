using Microsoft.EntityFrameworkCore;

using Drovik.Incident.Api.Domain;
using Drovik.Messaging;
namespace Drovik.Incident.Api.Persistence;

public sealed class IncidentDbContext(DbContextOptions<IncidentDbContext> options) : DbContext(options), IOutboxDbContext, IInboxDbContext
{
    public DbSet<IncidentRecord> Incidents => Set<IncidentRecord>();
    public DbSet<IncidentActivity> IncidentActivities => Set<IncidentActivity>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IncidentRecord>(entity =>
        {
            entity.ToTable("Incidents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Description).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.ResolutionNote).HasMaxLength(1000);
            entity.Property(item => item.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
            entity.HasIndex(item => new { item.Status, item.Severity, item.CreatedAtUtc });
            entity.HasIndex(item => item.VehicleId);
            entity.HasIndex(item => item.ShipmentId);
            entity.HasMany(item => item.Activities).WithOne(item => item.Incident)
                .HasForeignKey(item => item.IncidentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IncidentActivity>(entity =>
        {
            entity.ToTable("IncidentActivities");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Note).HasMaxLength(1000);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(item => new { item.IncidentId, item.OccurredAtUtc });
        });
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.PublishedAtUtc, item.NextAttemptAtUtc, item.OccurredAtUtc });
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Payload).HasMaxLength(10000).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(1000);
        });
        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(item => item.EventId);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
        });
    }
}
