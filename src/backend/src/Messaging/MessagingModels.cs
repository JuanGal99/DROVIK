using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>Wire format of every event (ADR 0003). Serialized in camelCase like its data.</summary>
public sealed record EventEnvelope(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    JsonElement Data)
{
    /// <exception cref="JsonException">The data does not match the contract; retrying cannot fix it.</exception>
    public T ReadData<T>() where T : class =>
        Data.Deserialize<T>(EventEnvelopeSerializer.ContractOptions)
            ?? throw new JsonException($"El evento {EventType} no tiene datos.");
}

public static class EventEnvelopeSerializer
{
    internal static readonly JsonSerializerOptions ContractOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        RespectRequiredConstructorParameters = true
    };

    /// <exception cref="JsonException">The stored payload is not valid JSON.</exception>
    public static byte[] Serialize(OutboxMessage message) => JsonSerializer.SerializeToUtf8Bytes(
        new EventEnvelope(message.Id, message.EventType, message.ContractVersion, message.OccurredAtUtc, message.CorrelationId,
            JsonSerializer.Deserialize<JsonElement>(message.Payload)),
        ContractOptions);

    /// <exception cref="JsonException">The bytes are not a complete envelope.</exception>
    public static EventEnvelope Deserialize(ReadOnlySpan<byte> body)
    {
        var envelope = JsonSerializer.Deserialize<EventEnvelope>(body, ContractOptions)
            ?? throw new JsonException("El envelope de evento está vacío.");
        if (envelope.EventId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.EventType) || envelope.EventVersion < 1)
            throw new JsonException("El envelope de evento no tiene identificador, tipo o versión.");
        return envelope;
    }
}

public static class OutboxMessageFactory
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static OutboxMessage Create<T>(string eventType, T payload, string? correlationId = null)
    {
        var occurredAtUtc = DateTime.UtcNow;
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            Payload = JsonSerializer.Serialize(payload, PayloadOptions)
        };
    }
}
