using System.Data;
using Drovik.Fleet.Api.Application;
using Drovik.Fleet.Api.Domain;
using Drovik.Fleet.Api.Persistence;
using Drovik.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Drovik.Fleet.Api.Messaging;

/// <summary>
/// Durable half of the reservation protocol: whatever the synchronous calls from Shipment could not
/// finish is completed here, so a Fleet outage can neither strand resources nor lose an assignment.
/// </summary>
public sealed class FleetEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<FleetEventConsumer> logger) : DomainEventConsumer<FleetDbContext>(scopeFactory, options, logger)
{
    private const string ShipmentInTransit = "InTransit";

    protected override string ConsumerName => "fleet";
    protected override IReadOnlyCollection<string> Subscriptions { get; } =
    [
        DomainEvents.ShipmentAssigned, DomainEvents.ShipmentStatusChanged,
        DomainEvents.ShipmentDelivered, DomainEvents.ShipmentCancelled
    ];

    // Serializable like the reservation endpoints, which race for the same vehicles and drivers.
    protected override Task<IDbContextTransaction> BeginTransactionAsync(FleetDbContext database, CancellationToken cancellationToken) =>
        database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

    protected override Task HandleAsync(EventEnvelope envelope, FleetDbContext database, CancellationToken cancellationToken) =>
        envelope.EventType switch
        {
            DomainEvents.ShipmentAssigned => ConfirmAsync(envelope.ReadData<ShipmentAssignedV1>(), database, cancellationToken),
            DomainEvents.ShipmentStatusChanged => MarkInTransitAsync(envelope.ReadData<ShipmentStatusChangedV1>(), database, cancellationToken),
            DomainEvents.ShipmentDelivered or DomainEvents.ShipmentCancelled => ReleaseAsync(envelope.ReadData<ShipmentClosedV1>(), database, cancellationToken),
            _ => throw new PermanentEventException($"Fleet no procesa el evento {envelope.EventType}.")
        };

    private static async Task ConfirmAsync(ShipmentAssignedV1 data, FleetDbContext database, CancellationToken cancellationToken)
    {
        var reservation = await FindAsync(data.ReservationId, data.ShipmentId, database, cancellationToken);
        if (reservation.Status != ReservationStatus.Reserved) return;
        if (reservation.ExpiresAtUtc <= DateTime.UtcNow) await ReservationLifecycle.ExpireAsync(database, reservation, cancellationToken);
        else reservation.Status = ReservationStatus.Confirmed;
    }

    private static async Task MarkInTransitAsync(ShipmentStatusChangedV1 data, FleetDbContext database, CancellationToken cancellationToken)
    {
        if (data.Status != ShipmentInTransit || data.ReservationId is not { } reservationId) return;
        var reservation = await FindAsync(reservationId, data.ShipmentId, database, cancellationToken);
        if (reservation.Status != ReservationStatus.Confirmed) return;
        var vehicle = await database.Vehicles.SingleOrDefaultAsync(item => item.Id == reservation.VehicleId, cancellationToken);
        if (vehicle is not { Status: VehicleStatus.Assigned }) return;
        vehicle.Status = VehicleStatus.InTransit;
        vehicle.UpdatedAtUtc = DateTime.UtcNow;
        database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.VehicleAvailabilityChanged,
            new VehicleAvailabilityChangedV1(vehicle.Id, vehicle.Status.ToString(), reservation.ShipmentId), reservation.ShipmentId.ToString()));
    }

    private static async Task ReleaseAsync(ShipmentClosedV1 data, FleetDbContext database, CancellationToken cancellationToken)
    {
        if (data.ReservationId is not { } reservationId) return;
        var reservation = await FindAsync(reservationId, data.ShipmentId, database, cancellationToken);
        if (!ReservationLifecycle.IsTerminal(reservation)) await ReservationLifecycle.ReleaseAsync(database, reservation, cancellationToken);
    }

    private static async Task<FleetReservation> FindAsync(Guid reservationId, Guid shipmentId, FleetDbContext database, CancellationToken cancellationToken) =>
        await database.Reservations.SingleOrDefaultAsync(item => item.Id == reservationId && item.ShipmentId == shipmentId, cancellationToken)
            ?? throw new PermanentEventException($"La reserva {reservationId} no existe para el envío {shipmentId}.");
}
