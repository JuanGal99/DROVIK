namespace Drovik.Incident.Api.Domain;

public enum IncidentType
{
    MechanicalFailure,
    Delay,
    Safety,
    Delivery,
    Other
}

public enum IncidentSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum IncidentStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public sealed class IncidentRecord
{
    public Guid Id { get; set; }
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public Guid? ShipmentId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ResolvedBy { get; set; }
    public List<IncidentActivity> Activities { get; set; } = [];
}

public sealed class IncidentActivity
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public IncidentRecord Incident { get; set; } = null!;
    public IncidentStatus Status { get; set; }
    public string? Note { get; set; }
    public Guid ActorId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
