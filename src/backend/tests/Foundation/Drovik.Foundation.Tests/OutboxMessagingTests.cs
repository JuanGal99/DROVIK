using System.Text.Json;
using Drovik.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Foundation.Tests;

public sealed class OutboxMessagingTests
{
    [Fact]
    public void CreatesVersionedEnvelopeMetadataAndPayload()
    {
        var eventId = Guid.NewGuid();
        var message = OutboxMessageFactory.Create("ShipmentCreated", new { shipmentId = eventId }, eventId.ToString());
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal("ShipmentCreated", message.EventType);
        Assert.Equal(1, message.ContractVersion);
        Assert.Equal(eventId.ToString(), message.CorrelationId);
        Assert.Equal(eventId, payload.GetProperty("shipmentId").GetGuid());
        Assert.Null(message.PublishedAtUtc);
    }

    [Fact]
    public async Task ProcessedEventPrimaryKeyMakesDuplicateDeliveryIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InboxTestDbContext>().UseSqlite(connection).Options;
        await using var database = new InboxTestDbContext(options);
        await database.Database.EnsureCreatedAsync();
        var eventId = Guid.NewGuid();

        database.ProcessedEvents.Add(new ProcessedEvent { EventId = eventId, EventType = "ShipmentCreated", ProcessedAtUtc = DateTime.UtcNow });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        database.ProcessedEvents.Add(new ProcessedEvent { EventId = eventId, EventType = "ShipmentCreated", ProcessedAtUtc = DateTime.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }

    private sealed class InboxTestDbContext(DbContextOptions<InboxTestDbContext> options) : DbContext(options)
    {
        public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

        protected override void OnModelCreating(ModelBuilder builder) =>
            builder.Entity<ProcessedEvent>().HasKey(item => item.EventId);
    }
}
