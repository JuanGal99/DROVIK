using Drovik.Fleet.Api.Domain;
using Drovik.Fleet.Api.Persistence;
using Drovik.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Fleet.Api.Application;

/// <summary>State changes shared by the HTTP endpoints, the event consumer and the expiry worker.</summary>
public static class ReservationLifecycle
{
    public static Task<bool> HasActiveReservationAsync(FleetDbContext database, Guid? vehicleId, Guid? driverId,
        Guid? excludedReservationId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return database.Reservations.AnyAsync(item =>
            item.Id != excludedReservationId &&
            (item.VehicleId == vehicleId || item.DriverId == driverId) &&
            (item.Status == ReservationStatus.Confirmed || (item.Status == ReservationStatus.Reserved && item.ExpiresAtUtc > now)), cancellationToken);
    }

    public static bool IsTerminal(FleetReservation reservation) =>
        reservation.Status is ReservationStatus.Released or ReservationStatus.Expired;

    /// <summary>Ends the reservation and frees whichever resources no other active reservation still holds.</summary>
    public static async Task ReleaseAsync(FleetDbContext database, FleetReservation reservation, CancellationToken cancellationToken)
    {
        await FreeResourcesAsync(database, reservation, cancellationToken);
        reservation.Status = ReservationStatus.Released;
    }

    /// <summary>A terminal state: Shipment is told, so an assignment it already persisted can be undone.</summary>
    public static async Task ExpireAsync(FleetDbContext database, FleetReservation reservation, CancellationToken cancellationToken)
    {
        await FreeResourcesAsync(database, reservation, cancellationToken);
        reservation.Status = ReservationStatus.Expired;
        database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.ReservationExpired,
            new ReservationExpiredV1(reservation.Id, reservation.ShipmentId), reservation.ShipmentId.ToString()));
    }

    private static async Task FreeResourcesAsync(FleetDbContext database, FleetReservation reservation, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var vehicleHeldElsewhere = await HasActiveReservationAsync(database, reservation.VehicleId, null, reservation.Id, cancellationToken);
        var driverHeldElsewhere = await HasActiveReservationAsync(database, null, reservation.DriverId, reservation.Id, cancellationToken);
        var vehicle = await database.Vehicles.SingleOrDefaultAsync(item => item.Id == reservation.VehicleId, cancellationToken);
        var driver = await database.Drivers.SingleOrDefaultAsync(item => item.Id == reservation.DriverId, cancellationToken);
        if (!vehicleHeldElsewhere && vehicle is { Status: VehicleStatus.Assigned or VehicleStatus.InTransit })
        {
            vehicle.Status = VehicleStatus.Available;
            vehicle.UpdatedAtUtc = now;
            database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.VehicleAvailabilityChanged,
                new VehicleAvailabilityChangedV1(vehicle.Id, vehicle.Status.ToString(), reservation.ShipmentId), reservation.ShipmentId.ToString()));
        }
        if (!driverHeldElsewhere && driver is { Availability: DriverAvailability.Assigned })
        {
            driver.Availability = DriverAvailability.Available;
            driver.AssignedVehicleId = null;
            driver.UpdatedAtUtc = now;
            database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.DriverAvailabilityChanged,
                new DriverAvailabilityChangedV1(driver.Id, driver.Availability.ToString(), reservation.ShipmentId), reservation.ShipmentId.ToString()));
        }
    }
}
