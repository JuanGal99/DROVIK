namespace Drovik.Fleet.Api.Domain;

public enum VehicleStatus
{
    Available,
    Assigned,
    InTransit,
    Maintenance,
    Inactive
}

public enum DriverAvailability
{
    Available,
    Assigned,
    Inactive
}

public sealed class Vehicle
{
    public Guid Id { get; set; }
    public string Plate { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int CapacityKg { get; set; }
    public VehicleStatus Status { get; set; }
    public int CurrentOdometer { get; set; }
    public Guid DistributionCenterId { get; set; }
    public DistributionCenter DistributionCenter { get; set; } = null!;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class Driver
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string LicenseCategory { get; set; } = string.Empty;
    public DateOnly LicenseExpiresOn { get; set; }
    public DriverAvailability Availability { get; set; }
    public Guid? AssignedVehicleId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class DistributionCenter
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AddressLabel { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsActive { get; set; }
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}

public enum ReservationStatus
{
    Reserved,
    Confirmed,
    Released,
    Expired
}

public sealed class FleetReservation
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid ShipmentId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid DriverId { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
