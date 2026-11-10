using System.ComponentModel.DataAnnotations;
using Drovik.Shipment.Api.Domain;

namespace Drovik.Shipment.Api.Contracts;

public sealed record ShipmentHistoryResponse(Guid Id, ShipmentStatus Status, string ActorId, string Source, string? Note, DateTime OccurredAtUtc);

public sealed record ShipmentResponse(
    Guid Id,
    string TrackingCode,
    Guid CustomerId,
    string CustomerName,
    Guid? OriginCustomerId,
    string? OriginName,
    string? Origin,
    decimal? OriginLatitude,
    decimal? OriginLongitude,
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

public sealed record OverviewRouteResponse(Guid Id, Guid OriginCustomerId, Guid CustomerId, ShipmentStatus Status);
public sealed record OverviewActivityResponse(Guid Id, DateTime OccurredAtUtc, string TrackingCode, ShipmentStatus Status, string? Note);
public sealed record ShipmentOverviewResponse(IReadOnlyList<OverviewRouteResponse> Routes, IReadOnlyList<OverviewActivityResponse> Activity);
public sealed record ShipmentCodeResponse(Guid Id, string TrackingCode);
public sealed record StatusSummaryResponse(int TotalCount, IReadOnlyDictionary<string, int> ByStatus);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record CreateShipmentRequest(
    Guid OriginCustomerId,
    Guid CustomerId,
    [property: Range(1, 100000)] int WeightKg,
    ShipmentPriority Priority);

public sealed record UpdateShipmentRequest(
    Guid OriginCustomerId,
    Guid CustomerId,
    [property: Range(1, 100000)] int WeightKg,
    ShipmentPriority Priority);

public sealed record AssignShipmentRequest(Guid VehicleId, Guid DriverId, [property: Required, StringLength(100, MinimumLength = 8)] string IdempotencyKey);
public sealed record ChangeShipmentStatusRequest(ShipmentStatus Status, [property: StringLength(500)] string? Note);

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string AddressLabel,
    decimal Latitude,
    decimal Longitude,
    bool IsActive);

public sealed record FleetReservationRequest(Guid ShipmentId, Guid VehicleId, Guid DriverId, int WeightKg, string IdempotencyKey);
public sealed record FleetReservationResponse(Guid ReservationId, Guid ShipmentId, Guid VehicleId, Guid DriverId, string Status, DateTime ExpiresAtUtc);
