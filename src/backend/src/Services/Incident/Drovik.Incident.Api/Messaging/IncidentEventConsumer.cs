using Drovik.Incident.Api.Domain;
using Drovik.Incident.Api.Persistence;
using System.Globalization;
using Drovik.Messaging;
using Microsoft.Extensions.Options;

namespace Drovik.Incident.Api.Messaging;

/// <summary>Audits Fleet and Shipment events in the inbox and opens an incident for what goes wrong on the road.</summary>
public sealed class IncidentEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<IncidentEventConsumer> logger) : DomainEventConsumer<IncidentDbContext>(scopeFactory, options, logger)
{
    protected override string ConsumerName => "incident";
    protected override IReadOnlyCollection<string> Subscriptions { get; } =
    [
        DomainEvents.ShipmentCreated, DomainEvents.ShipmentAssigned, DomainEvents.ShipmentStatusChanged,
        DomainEvents.ShipmentDelivered, DomainEvents.ShipmentCancelled, DomainEvents.VehicleAvailabilityChanged,
        DomainEvents.DriverAvailabilityChanged, DomainEvents.ReservationExpired,
        DomainEvents.RouteDelayDetected, DomainEvents.VehicleBreakdownDetected, DomainEvents.RouteDeviationDetected
    ];

    protected override Task HandleAsync(EventEnvelope envelope, IncidentDbContext database, CancellationToken cancellationToken)
    {
        switch (envelope.EventType)
        {
            case DomainEvents.VehicleBreakdownDetected: Open(envelope, IncidentType.MechanicalFailure, IncidentSeverity.Critical, "Avería del vehículo durante la ruta del envío {0}", database); break;
            case DomainEvents.RouteDelayDetected: Open(envelope, IncidentType.Delay, IncidentSeverity.Medium, "Congestión en la ruta del envío {0}", database); break;
            case DomainEvents.RouteDeviationDetected: Open(envelope, IncidentType.Other, IncidentSeverity.Low, "Desvío de la ruta prevista del envío {0}", database); break;
        }
        return Task.CompletedTask;
    }

    // The inbox commits with this change, so a redelivered event cannot open a second incident.
    private static void Open(EventEnvelope envelope, IncidentType type, IncidentSeverity severity, string description, IncidentDbContext database)
    {
        var data = envelope.ReadData<RouteScenarioDetectedV1>();
        var now = DateTime.UtcNow;
        var incident = new IncidentRecord
        {
            Id = Guid.NewGuid(), Type = type, Severity = severity, Status = IncidentStatus.Open,
            Description = string.Format(CultureInfo.InvariantCulture, description, data.TrackingCode),
            VehicleId = data.VehicleId, ShipmentId = data.ShipmentId, CreatedAtUtc = now, CreatedBy = Guid.Empty,
            Activities = [new IncidentActivity { Id = Guid.NewGuid(), Status = IncidentStatus.Open, ActorId = Guid.Empty, OccurredAtUtc = now }]
        };
        database.Incidents.Add(incident);
        database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.IncidentCreated,
            new IncidentCreatedV1(incident.Id, incident.Type.ToString(), incident.Severity.ToString(), incident.Status.ToString(), incident.VehicleId, incident.ShipmentId),
            incident.Id.ToString()));
    }
}
