using System.ComponentModel.DataAnnotations;
using Drovik.Shipment.Api.Domain;

namespace Drovik.Shipment.Api.Contracts;

public sealed record ShipmentHistoryResponse(Guid Id, ShipmentStatus Status, string ActorId, string Source, string? Note, DateTime OccurredAtUtc);

public sealed record ShipmentResponse(
    Guid Id,
    string TrackingCode,
    string CustomerId,
    Guid OriginCenterId,
    string Destination,
    decimal DestinationLatitude,
    decimal DestinationLongitude,
    int WeightKg,
    ShipmentPriority Priority,
    ShipmentStatus Status,
    Guid? VehicleId,
    Guid? DriverId,
    Guid? ReservationId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ShipmentHistoryResponse> StatusHistory);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record CreateShipmentRequest(
    [property: Required, StringLength(100)] string CustomerId,
    Guid OriginCenterId,
    [property: Required, StringLength(200, MinimumLength = 3)] string Destination,
    [property: Range(-90, 90)] decimal DestinationLatitude,
    [property: Range(-180, 180)] decimal DestinationLongitude,
    [property: Range(1, 100000)] int WeightKg,
    ShipmentPriority Priority);

public sealed record UpdateShipmentRequest(
    [property: Required, StringLength(200, MinimumLength = 3)] string Destination,
    [property: Range(-90, 90)] decimal DestinationLatitude,
    [property: Range(-180, 180)] decimal DestinationLongitude,
    [property: Range(1, 100000)] int WeightKg,
    ShipmentPriority Priority);

public sealed record AssignShipmentRequest(Guid VehicleId, Guid DriverId, [property: Required, StringLength(100, MinimumLength = 8)] string IdempotencyKey);
public sealed record ChangeShipmentStatusRequest(ShipmentStatus Status, string? Note);

public sealed record FleetReservationRequest(Guid ShipmentId, Guid VehicleId, Guid DriverId, int WeightKg, string IdempotencyKey);
public sealed record FleetReservationResponse(Guid ReservationId, Guid ShipmentId, Guid VehicleId, Guid DriverId, string Status, DateTime ExpiresAtUtc);
