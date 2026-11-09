using Drovik.Incident.Api.Persistence;
using Drovik.Messaging;
using Microsoft.Extensions.Options;

namespace Drovik.Incident.Api.Messaging;

/// <summary>Records Fleet and Shipment events in the inbox as an audit trail; it has no business effect yet.</summary>
public sealed class IncidentEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<IncidentEventConsumer> logger) : DomainEventConsumer<IncidentDbContext>(scopeFactory, options, logger)
{
    protected override string ConsumerName => "incident";
    protected override IReadOnlyCollection<string> Subscriptions { get; } =
    [
        DomainEvents.ShipmentCreated, DomainEvents.ShipmentAssigned, DomainEvents.ShipmentStatusChanged,
        DomainEvents.ShipmentDelivered, DomainEvents.ShipmentCancelled, DomainEvents.VehicleAvailabilityChanged,
        DomainEvents.DriverAvailabilityChanged, DomainEvents.ReservationExpired
    ];

    protected override Task HandleAsync(EventEnvelope envelope, IncidentDbContext database, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
