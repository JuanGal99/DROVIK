using System.Text.Json;

namespace Drovik.Messaging;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string? CorrelationId { get; set; }
    public int ContractVersion { get; set; } = 1;
    public string Payload { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
}

public sealed class ProcessedEvent
{
    public Guid EventId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
}

public sealed record EventEnvelope(
    Guid EventId,
    string EventType,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    int ContractVersion,
    JsonElement Payload);

public static class OutboxMessageFactory
{
    public static OutboxMessage Create<T>(string eventType, T payload, string? correlationId = null)
    {
        var occurredAtUtc = DateTime.UtcNow;
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            Payload = JsonSerializer.Serialize(payload)
        };
    }
}
