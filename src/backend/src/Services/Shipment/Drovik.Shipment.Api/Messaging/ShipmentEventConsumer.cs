using Drovik.Messaging;
using Drovik.Shipment.Api.Domain;
using Drovik.Shipment.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Drovik.Shipment.Api.Messaging;

/// <summary>Undoes assignments whose reservation expired and delivers shipments that finished their route.</summary>
public sealed class ShipmentEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<ShipmentEventConsumer> logger) : DomainEventConsumer<ShipmentDbContext>(scopeFactory, options, logger)
{
    private readonly ILogger<ShipmentEventConsumer> log = logger;

    protected override string ConsumerName => "shipment";
    protected override IReadOnlyCollection<string> Subscriptions { get; } = [DomainEvents.ReservationExpired, DomainEvents.ShipmentRouteCompleted];

    protected override Task HandleAsync(EventEnvelope envelope, ShipmentDbContext database, CancellationToken cancellationToken) =>
        envelope.EventType switch
        {
            DomainEvents.ReservationExpired => UndoAssignmentAsync(envelope, database, cancellationToken),
            DomainEvents.ShipmentRouteCompleted => DeliverAsync(envelope, database, cancellationToken),
            _ => throw new PermanentEventException($"Shipment no procesa el evento {envelope.EventType}.")
        };

    private static async Task DeliverAsync(EventEnvelope envelope, ShipmentDbContext database, CancellationToken cancellationToken)
    {
        var data = envelope.ReadData<ShipmentRouteCompletedV1>();
        var shipment = await database.Shipments.SingleOrDefaultAsync(item => item.Id == data.ShipmentId, cancellationToken);
        if (shipment is not { Status: ShipmentStatus.InTransit or ShipmentStatus.Delayed }) return;
        var now = DateTime.UtcNow;
        shipment.Status = ShipmentStatus.Delivered;
        shipment.UpdatedAtUtc = now;
        database.StatusHistory.Add(new ShipmentStatusHistory
        {
            Id = Guid.NewGuid(), ShipmentId = shipment.Id, Status = ShipmentStatus.Delivered,
            ActorId = "system", Source = "RouteCompleted", OccurredAtUtc = now
        });
        database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.ShipmentStatusChanged,
            new ShipmentStatusChangedV1(shipment.Id, shipment.Status.ToString(), null, shipment.VehicleId, shipment.ReservationId), shipment.Id.ToString()));
        database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.ShipmentDelivered,
            new ShipmentClosedV1(shipment.Id, shipment.Status.ToString(), shipment.ReservationId), shipment.Id.ToString()));
    }

    private async Task UndoAssignmentAsync(EventEnvelope envelope, ShipmentDbContext database, CancellationToken cancellationToken)
    {
        var data = envelope.ReadData<ReservationExpiredV1>();
        var shipment = await database.Shipments.SingleOrDefaultAsync(
            item => item.Id == data.ShipmentId && item.ReservationId == data.ReservationId, cancellationToken);
        if (shipment is null) return;
        if (shipment.Status != ShipmentStatus.Assigned)
        {
            log.LogWarning("La reserva {ReservationId} expiró con el envío {ShipmentId} en estado {Status}; requiere revisión operativa.",
                data.ReservationId, shipment.Id, shipment.Status);
            return;
        }

        const string note = "La reserva de recursos expiró antes de confirmarse; asigna el envío de nuevo.";
        var now = DateTime.UtcNow;
        shipment.Status = ShipmentStatus.Pending;
        shipment.VehicleId = null;
        shipment.DriverId = null;
        shipment.ReservationId = null;
        shipment.UpdatedAtUtc = now;
        database.StatusHistory.Add(new ShipmentStatusHistory
        {
            Id = Guid.NewGuid(), ShipmentId = shipment.Id, Status = ShipmentStatus.Pending,
            ActorId = "system", Source = "ReservationExpired", Note = note, OccurredAtUtc = now
        });
        database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.ShipmentStatusChanged,
            new ShipmentStatusChangedV1(shipment.Id, shipment.Status.ToString(), note), shipment.Id.ToString()));
    }
}
