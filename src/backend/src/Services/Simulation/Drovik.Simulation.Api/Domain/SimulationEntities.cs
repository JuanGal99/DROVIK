namespace Drovik.Simulation.Api.Domain;

public enum SimulationRunStatus
{
    Running,
    Paused,
    Completed,
    Cancelled
}

public enum SimulatedVehicleStatus
{
    Moving,
    Arrived,
    /// <summary>The shipment left the route outside this run, for example delivered by hand.</summary>
    Removed
}

public sealed class SimulationRun
{
    public Guid Id { get; set; }
    public SimulationRunStatus Status { get; set; }
    public int SpeedMultiplier { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastTickAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public ICollection<SimulatedVehicle> Vehicles { get; set; } = new List<SimulatedVehicle>();
}

public sealed class SimulatedVehicle
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public SimulationRun Run { get; set; } = null!;
    public Guid ShipmentId { get; set; }
    public Guid? VehicleId { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public string OriginName { get; set; } = string.Empty;
    public string DestinationName { get; set; } = string.Empty;
    public bool IsDelayed { get; set; }
    public bool IsBrokenDown { get; set; }
    public bool IsDetoured { get; set; }
    public string RouteJson { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
    public double ProgressMeters { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public SimulatedVehicleStatus Status { get; set; }
}
