namespace Drovik.Shipment.Api.Domain;

public enum ShipmentStatus
{
    Pending,
    Assigned,
    InTransit,
    Delayed,
    Delivered,
    Cancelled
}

public enum ShipmentPriority
{
    Normal,
    High
}

public sealed class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AddressLabel { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class Shipment
{
    public Guid Id { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? OriginCustomerId { get; set; }
    public Customer? OriginCustomer { get; set; }
    public string? Origin { get; set; }
    public decimal? OriginLatitude { get; set; }
    public decimal? OriginLongitude { get; set; }
    /// <summary>Only set on shipments created before the origin came from the catalog (ADR 0012).</summary>
    public Guid? LegacyOriginCenterId { get; set; }
    public string Destination { get; set; } = string.Empty;
    public decimal DestinationLatitude { get; set; }
    public decimal DestinationLongitude { get; set; }
    public int WeightKg { get; set; }
    public ShipmentPriority Priority { get; set; }
    public ShipmentStatus Status { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? ReservationId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public ICollection<ShipmentStatusHistory> StatusHistory { get; set; } = new List<ShipmentStatusHistory>();
}

public sealed class ShipmentStatusHistory
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    public ShipmentStatus Status { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string Source { get; set; } = "User";
    public string? Note { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
