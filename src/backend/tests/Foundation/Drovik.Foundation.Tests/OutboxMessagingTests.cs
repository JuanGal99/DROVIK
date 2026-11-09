using System.Text.Json;
using Drovik.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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

    [Fact]
    public void PayloadsUseCamelCaseAndStringEnumsSoTheContractIsStable()
    {
        var message = OutboxMessageFactory.Create("Sample", new { TrackingCode = "SH-1", Status = DayOfWeek.Friday });
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);

        Assert.Equal("SH-1", payload.GetProperty("trackingCode").GetString());
        Assert.Equal("Friday", payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task BrokerOutageKeepsEventsWithoutSpendingAttempts()
    {
        var publisher = new ScriptedPublisher(_ => PublishOutcome.BrokerUnavailable);
        await using var harness = await DispatcherHarness.CreateAsync(publisher);
        var first = await harness.AddAsync("{}");
        await harness.AddAsync("{}");

        await harness.Dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Single(publisher.Attempts);
        var stored = await harness.FindAsync(first.Id);
        Assert.Equal(0, stored.Attempts);
        Assert.Null(stored.PublishedAtUtc);
        Assert.NotNull(stored.LastError);
    }

    [Fact]
    public async Task RejectedMessageDoesNotBlockTheOnesBehindIt()
    {
        var publisher = new ScriptedPublisher(item => item.Payload == "invalid" ? PublishOutcome.Rejected : PublishOutcome.Published);
        await using var harness = await DispatcherHarness.CreateAsync(publisher);
        var poison = await harness.AddAsync("invalid");
        var healthy = await harness.AddAsync("{}");

        await harness.Dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(1, (await harness.FindAsync(poison.Id)).Attempts);
        Assert.Null((await harness.FindAsync(poison.Id)).PublishedAtUtc);
        Assert.NotNull((await harness.FindAsync(healthy.Id)).PublishedAtUtc);
    }

    [Theory]
    [InlineData(DomainEvents.VehicleAvailabilityChanged, "fleet.vehicle-availability-changed.v1")]
    [InlineData(DomainEvents.ReservationExpired, "fleet.reservation-expired.v1")]
    [InlineData(DomainEvents.ShipmentAssigned, "shipment.shipment-assigned.v1")]
    [InlineData(DomainEvents.IncidentResolved, "incident.incident-resolved.v1")]
    public void RoutingKeysFollowTheApprovedTopology(string eventType, string expected) =>
        Assert.Equal(expected, DomainEvents.RoutingKey(eventType));

    [Fact]
    public void AnEventOutsideTheCatalogIsRejectedInsteadOfPublishedBlindly()
    {
        using var publisher = new RabbitMqPublisher(Options.Create(new RabbitMqOptions { UserName = "local", Password = "local" }), NullLogger<RabbitMqPublisher>.Instance);

        var outcome = publisher.Publish(OutboxMessageFactory.Create("NotInTheCatalog", new { value = 1 }));

        Assert.Equal(PublishOutcome.Rejected, outcome);
    }

    [Fact]
    public void EnvelopeUsesTheApprovedFieldNamesInCamelCase()
    {
        var message = OutboxMessageFactory.Create(DomainEvents.ShipmentCreated, new ShipmentCreatedV1(Guid.NewGuid(), "SH-1", "cus", "Pending"), "corr");

        var json = JsonSerializer.Deserialize<JsonElement>(EventEnvelopeSerializer.Serialize(message));

        Assert.Equal(message.Id, json.GetProperty("eventId").GetGuid());
        Assert.Equal("ShipmentCreated", json.GetProperty("eventType").GetString());
        Assert.Equal(1, json.GetProperty("eventVersion").GetInt32());
        Assert.Equal("corr", json.GetProperty("correlationId").GetString());
        Assert.Equal("SH-1", json.GetProperty("data").GetProperty("trackingCode").GetString());
    }

    [Fact]
    public void ContractDataMissingARequiredFieldCannotBeRead()
    {
        var message = OutboxMessageFactory.Create(DomainEvents.ReservationExpired, new { reservationId = Guid.NewGuid() });
        var envelope = EventEnvelopeSerializer.Deserialize(EventEnvelopeSerializer.Serialize(message));

        Assert.Throws<JsonException>(() => envelope.ReadData<ReservationExpiredV1>());
    }

    [Fact]
    public async Task ConsumerRetriesTransientFailuresWithinTheSameDelivery()
    {
        await using var harness = await ConsumerHarness.CreateAsync((attempt, _) => attempt == 1 ? throw new InvalidOperationException() : Task.CompletedTask);

        var outcome = await harness.Consumer.ProcessAsync(harness.NewEvent(), redelivered: false, CancellationToken.None);

        Assert.Equal(ConsumeOutcome.Ack, outcome);
        Assert.Equal(2, harness.Consumer.Attempts);
        Assert.Equal(1, await harness.InboxCountAsync());
    }

    [Theory]
    [InlineData(false, ConsumeOutcome.Requeue)]
    [InlineData(true, ConsumeOutcome.DeadLetter)]
    public async Task ConsumerGetsOneRedeliveryAfterExhaustingRetries(bool redelivered, ConsumeOutcome expected)
    {
        await using var harness = await ConsumerHarness.CreateAsync((_, _) => throw new InvalidOperationException());

        var outcome = await harness.Consumer.ProcessAsync(harness.NewEvent(), redelivered, CancellationToken.None);

        Assert.Equal(expected, outcome);
        Assert.Equal(0, await harness.InboxCountAsync());
    }

    [Fact]
    public async Task ConsumerDeadLettersPermanentFailuresWithoutRetrying()
    {
        await using var harness = await ConsumerHarness.CreateAsync((_, _) => throw new PermanentEventException("inservible"));

        var outcome = await harness.Consumer.ProcessAsync(harness.NewEvent(), redelivered: false, CancellationToken.None);

        Assert.Equal(ConsumeOutcome.DeadLetter, outcome);
        Assert.Equal(1, harness.Consumer.Attempts);
    }

    [Fact]
    public async Task ConsumerDeadLettersBytesThatAreNotAnEnvelope()
    {
        await using var harness = await ConsumerHarness.CreateAsync((_, _) => Task.CompletedTask);

        var outcome = await harness.Consumer.ProcessAsync("esto-no-es-json"u8.ToArray(), redelivered: false, CancellationToken.None);

        Assert.Equal(ConsumeOutcome.DeadLetter, outcome);
        Assert.Equal(0, harness.Consumer.Attempts);
    }

    [Fact]
    public async Task ConsumerAppliesADuplicateEventOnlyOnce()
    {
        await using var harness = await ConsumerHarness.CreateAsync((_, _) => Task.CompletedTask);
        var body = harness.NewEvent();

        Assert.Equal(ConsumeOutcome.Ack, await harness.Consumer.ProcessAsync(body, redelivered: false, CancellationToken.None));
        Assert.Equal(ConsumeOutcome.Ack, await harness.Consumer.ProcessAsync(body, redelivered: true, CancellationToken.None));

        Assert.Equal(1, harness.Consumer.Attempts);
        Assert.Equal(1, await harness.InboxCountAsync());
    }

    private sealed class ConsumerHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;

        private ConsumerHarness(SqliteConnection connection, ServiceProvider services, Func<int, InboxConsumerDbContext, Task> handler)
        {
            _connection = connection;
            _services = services;
            Consumer = new TestConsumer(services.GetRequiredService<IServiceScopeFactory>(), handler);
        }

        public TestConsumer Consumer { get; }

        public static async Task<ConsumerHarness> CreateAsync(Func<int, InboxConsumerDbContext, Task> handler)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection().AddDbContext<InboxConsumerDbContext>(options => options.UseSqlite(connection)).BuildServiceProvider();
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<InboxConsumerDbContext>().Database.EnsureCreatedAsync();
            return new ConsumerHarness(connection, services, handler);
        }

        public byte[] NewEvent() => EventEnvelopeSerializer.Serialize(
            OutboxMessageFactory.Create(DomainEvents.ShipmentCreated, new ShipmentCreatedV1(Guid.NewGuid(), "SH-1", "cus", "Pending")));

        public async Task<int> InboxCountAsync()
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<InboxConsumerDbContext>().ProcessedEvents.CountAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestConsumer(IServiceScopeFactory scopeFactory, Func<int, InboxConsumerDbContext, Task> handler)
        : DomainEventConsumer<InboxConsumerDbContext>(scopeFactory, Options.Create(new RabbitMqOptions()), NullLogger.Instance)
    {
        public int Attempts { get; private set; }
        protected override string ConsumerName => "test";
        protected override IReadOnlyCollection<string> Subscriptions { get; } = [DomainEvents.ShipmentCreated];
        protected override IReadOnlyList<TimeSpan> RetryDelays { get; } = [TimeSpan.Zero, TimeSpan.Zero];

        protected override Task HandleAsync(EventEnvelope envelope, InboxConsumerDbContext database, CancellationToken cancellationToken) =>
            handler(++Attempts, database);
    }

    private sealed class InboxConsumerDbContext(DbContextOptions<InboxConsumerDbContext> options) : DbContext(options), IInboxDbContext
    {
        public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

        protected override void OnModelCreating(ModelBuilder builder) =>
            builder.Entity<ProcessedEvent>().HasKey(item => item.EventId);
    }

    private sealed class ScriptedPublisher(Func<OutboxMessage, PublishOutcome> script) : IEventPublisher
    {
        public List<Guid> Attempts { get; } = [];

        public PublishOutcome Publish(OutboxMessage message)
        {
            Attempts.Add(message.Id);
            return script(message);
        }
    }

    private sealed class OutboxTestDbContext(DbContextOptions<OutboxTestDbContext> options) : DbContext(options), IOutboxDbContext
    {
        public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    }

    private sealed class DispatcherHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private DateTime _clock = DateTime.UtcNow.AddMinutes(-1);

        private DispatcherHarness(SqliteConnection connection, ServiceProvider services, IEventPublisher publisher)
        {
            _connection = connection;
            _services = services;
            Dispatcher = new OutboxDispatcher<OutboxTestDbContext>(
                services.GetRequiredService<IServiceScopeFactory>(), publisher,
                Options.Create(new RabbitMqOptions()), NullLogger<OutboxDispatcher<OutboxTestDbContext>>.Instance);
        }

        public OutboxDispatcher<OutboxTestDbContext> Dispatcher { get; }

        public static async Task<DispatcherHarness> CreateAsync(IEventPublisher publisher)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection().AddDbContext<OutboxTestDbContext>(options => options.UseSqlite(connection)).BuildServiceProvider();
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>().Database.EnsureCreatedAsync();
            return new DispatcherHarness(connection, services, publisher);
        }

        public async Task<OutboxMessage> AddAsync(string payload)
        {
            await using var scope = _services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            _clock = _clock.AddSeconds(1);
            var message = new OutboxMessage { Id = Guid.NewGuid(), EventType = "Sample", OccurredAtUtc = _clock, Payload = payload };
            database.Outbox.Add(message);
            await database.SaveChangesAsync();
            return message;
        }

        public async Task<OutboxMessage> FindAsync(Guid id)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>().Outbox.AsNoTracking().SingleAsync(item => item.Id == id);
        }

        public async ValueTask DisposeAsync()
        {
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class InboxTestDbContext(DbContextOptions<InboxTestDbContext> options) : DbContext(options)
    {
        public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

        protected override void OnModelCreating(ModelBuilder builder) =>
            builder.Entity<ProcessedEvent>().HasKey(item => item.EventId);
    }
}
