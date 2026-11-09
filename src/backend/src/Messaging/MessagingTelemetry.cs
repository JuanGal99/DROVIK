using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Drovik.Messaging;

public static class MessagingTelemetry
{
    public const string Name = "Drovik.Messaging";
    public const string ConsumedCounter = "drovik.messaging.consumed";

    internal static readonly ActivitySource Source = new(Name);
    private static readonly Meter Meter = new(Name);

    /// <summary>Tagged by consumer and outcome; a growing <c>DeadLetter</c> count means events are waiting in a DLQ.</summary>
    internal static readonly Counter<long> Consumed = Meter.CreateCounter<long>(ConsumedCounter, description: "Eventos procesados por consumidor y resultado.");
}
