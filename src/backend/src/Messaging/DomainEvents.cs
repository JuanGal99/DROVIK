using System.Text;

namespace Drovik.Messaging;

/// <summary>
/// Catalog of the published events. Routing keys follow ADR 0003: <c>domain.event-name.vN</c>.
/// </summary>
public static class DomainEvents
{
    public const string VehicleAvailabilityChanged = nameof(VehicleAvailabilityChanged);
    public const string DriverAvailabilityChanged = nameof(DriverAvailabilityChanged);
    public const string ReservationExpired = nameof(ReservationExpired);
    public const string ShipmentCreated = nameof(ShipmentCreated);
    public const string ShipmentAssigned = nameof(ShipmentAssigned);
    public const string ShipmentStatusChanged = nameof(ShipmentStatusChanged);
    public const string ShipmentDelivered = nameof(ShipmentDelivered);
    public const string ShipmentCancelled = nameof(ShipmentCancelled);
    public const string IncidentCreated = nameof(IncidentCreated);
    public const string IncidentStatusChanged = nameof(IncidentStatusChanged);
    public const string IncidentResolved = nameof(IncidentResolved);

    private static readonly Dictionary<string, string> Domains = new(StringComparer.Ordinal)
    {
        [VehicleAvailabilityChanged] = "fleet",
        [DriverAvailabilityChanged] = "fleet",
        [ReservationExpired] = "fleet",
        [ShipmentCreated] = "shipment",
        [ShipmentAssigned] = "shipment",
        [ShipmentStatusChanged] = "shipment",
        [ShipmentDelivered] = "shipment",
        [ShipmentCancelled] = "shipment",
        [IncidentCreated] = "incident",
        [IncidentStatusChanged] = "incident",
        [IncidentResolved] = "incident",
    };

    /// <exception cref="ArgumentException">The event is not in the catalog or the version is not positive.</exception>
    public static string RoutingKey(string eventType, int version = 1)
    {
        if (!Domains.TryGetValue(eventType, out var domain))
            throw new ArgumentException($"El evento '{eventType}' no está en el catálogo.", nameof(eventType));
        if (version < 1)
            throw new ArgumentException("La versión del evento debe ser positiva.", nameof(version));
        return $"{domain}.{ToKebabCase(eventType)}.v{version}";
    }

    private static string ToKebabCase(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        foreach (var character in value)
        {
            if (char.IsUpper(character) && builder.Length > 0) builder.Append('-');
            builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }
}

// Version 1 payloads. Additive fields must be optional so older stored events keep deserializing.
public sealed record VehicleAvailabilityChangedV1(Guid VehicleId, string Status, Guid? ShipmentId = null);
public sealed record DriverAvailabilityChangedV1(Guid DriverId, string Availability, Guid? ShipmentId = null);
public sealed record ReservationExpiredV1(Guid ReservationId, Guid ShipmentId);
public sealed record ShipmentCreatedV1(Guid ShipmentId, string TrackingCode, string CustomerId, string Status);
public sealed record ShipmentAssignedV1(Guid ShipmentId, Guid VehicleId, Guid DriverId, Guid ReservationId);
public sealed record ShipmentStatusChangedV1(Guid ShipmentId, string Status, string? Note = null, Guid? VehicleId = null, Guid? ReservationId = null);
public sealed record ShipmentClosedV1(Guid ShipmentId, string Status, Guid? ReservationId = null);
public sealed record IncidentCreatedV1(Guid IncidentId, string Type, string Severity, string Status, Guid? VehicleId = null, Guid? ShipmentId = null);
public sealed record IncidentStatusChangedV1(Guid IncidentId, string Status, string? Note = null);
public sealed record IncidentResolvedV1(Guid IncidentId, string Status, string? ResolutionNote = null);
