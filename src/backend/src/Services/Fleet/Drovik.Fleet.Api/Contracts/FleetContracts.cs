using System.ComponentModel.DataAnnotations;
using Drovik.Fleet.Api.Domain;

namespace Drovik.Fleet.Api.Contracts;

public sealed record VehicleResponse(
    Guid Id,
    string Plate,
    string Model,
    string Type,
    int CapacityKg,
    VehicleStatus Status,
    int CurrentOdometer,
    Guid DistributionCenterId,
    string Center,
    DateTime UpdatedAtUtc);

public sealed record DriverResponse(
    Guid Id,
    string FullName,
    string LicenseCategory,
    DateOnly LicenseExpiresOn,
    DriverAvailability Availability,
    Guid? AssignedVehicleId,
    DateTime UpdatedAtUtc);

public sealed record CenterResponse(
    Guid Id,
    string Name,
    string AddressLabel,
    decimal Latitude,
    decimal Longitude,
    bool IsActive);

public sealed record StatusSummaryResponse(int TotalCount, IReadOnlyDictionary<string, int> ByStatus);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record CreateVehicleRequest(
    [property: Required, StringLength(12, MinimumLength = 2)] string Plate,
    [property: Required, StringLength(60, MinimumLength = 2)] string Model,
    [property: Required, StringLength(40, MinimumLength = 2)] string Type,
    [property: Range(1, 100000)] int CapacityKg,
    [property: Range(0, 9999999)] int CurrentOdometer,
    Guid DistributionCenterId);

public sealed record UpdateVehicleRequest(
    [property: Required, StringLength(12, MinimumLength = 2)] string Plate,
    [property: Required, StringLength(60, MinimumLength = 2)] string Model,
    [property: Required, StringLength(40, MinimumLength = 2)] string Type,
    [property: Range(1, 100000)] int CapacityKg,
    [property: Range(0, 9999999)] int CurrentOdometer,
    Guid DistributionCenterId);

public sealed record ChangeVehicleStatusRequest(VehicleStatus Status);

public sealed record CreateDriverRequest(
    [property: Required, StringLength(100, MinimumLength = 3)] string FullName,
    [property: Required, StringLength(12, MinimumLength = 2)] string LicenseCategory,
    DateOnly LicenseExpiresOn);

public sealed record UpdateDriverRequest(
    [property: Required, StringLength(100, MinimumLength = 3)] string FullName,
    [property: Required, StringLength(12, MinimumLength = 2)] string LicenseCategory,
    DateOnly LicenseExpiresOn);

public sealed record ChangeDriverAvailabilityRequest(DriverAvailability Availability);

public sealed record ReserveResourcesRequest(
    Guid ShipmentId,
    Guid VehicleId,
    Guid DriverId,
    int WeightKg,
    [property: Required, StringLength(100, MinimumLength = 8)] string IdempotencyKey);

public sealed record ReservationResponse(
    Guid ReservationId,
    Guid ShipmentId,
    Guid VehicleId,
    Guid DriverId,
    ReservationStatus Status,
    DateTime ExpiresAtUtc);
