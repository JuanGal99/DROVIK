using Drovik.Incident.Api.Domain;

namespace Drovik.Incident.Api.Contracts;

public sealed record CreateIncidentRequest(
    IncidentType Type,
    IncidentSeverity Severity,
    string Description,
    Guid? VehicleId,
    Guid? ShipmentId);

public sealed record ChangeIncidentStatusRequest(IncidentStatus Status, string? Note);
public sealed record ResolveIncidentRequest(string ResolutionNote);

public sealed record IncidentActivityResponse(
    Guid Id,
    IncidentStatus Status,
    string? Note,
    Guid ActorId,
    DateTime OccurredAtUtc);

public sealed record IncidentResponse(
    Guid Id,
    IncidentType Type,
    IncidentSeverity Severity,
    IncidentStatus Status,
    string Description,
    Guid? VehicleId,
    Guid? ShipmentId,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionNote,
    Guid CreatedBy,
    Guid? ResolvedBy,
    IReadOnlyList<IncidentActivityResponse> Activities);

public sealed record PagedIncidentsResponse(
    IReadOnlyList<IncidentResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
