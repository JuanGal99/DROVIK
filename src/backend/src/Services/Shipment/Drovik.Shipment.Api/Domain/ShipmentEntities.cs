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

public sealed class Shipment
{
    public Guid Id { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public Guid OriginCenterId { get; set; }
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
