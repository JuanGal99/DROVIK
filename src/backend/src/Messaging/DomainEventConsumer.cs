using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Drovik.Messaging;

public interface IInboxDbContext
{
    DbSet<ProcessedEvent> ProcessedEvents { get; }
}

public enum ConsumeOutcome
{
    Ack,
    Requeue,
    DeadLetter
}

/// <summary>The event can never be applied, so retrying it is pointless.</summary>
public sealed class PermanentEventException(string message) : Exception(message);

/// <summary>
/// Durable, idempotent event consumer: the handler's changes and the inbox row commit in one transaction,
/// transient failures retry with backoff and get one redelivery, and anything else reaches this consumer's own DLQ.
/// </summary>
public abstract class DomainEventConsumer<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger logger) : BackgroundService
    where TContext : DbContext, IInboxDbContext
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    protected abstract string ConsumerName { get; }
    protected abstract IReadOnlyCollection<string> Subscriptions { get; }

    /// <summary>Applies the event to tracked entities. The base class saves; handlers must not call SaveChanges.</summary>
    protected abstract Task HandleAsync(EventEnvelope envelope, TContext database, CancellationToken cancellationToken);

    protected virtual Task<IDbContextTransaction> BeginTransactionAsync(TContext database, CancellationToken cancellationToken) =>
        database.Database.BeginTransactionAsync(cancellationToken);

    /// <summary>Waits between in-process attempts; the number of entries is the number of retries.</summary>
    protected virtual IReadOnlyList<TimeSpan> RetryDelays { get; } =
        [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    public string QueueName => $"{ConsumerName}.domain-events";
    public string DeadLetterQueueName => $"{QueueName}.dlq";

    public async Task<ConsumeOutcome> ProcessAsync(ReadOnlyMemory<byte> body, bool redelivered, CancellationToken cancellationToken)
    {
        EventEnvelope envelope;
        try
        {
            envelope = EventEnvelopeSerializer.Deserialize(body.Span);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "El consumidor {Consumer} recibió un envelope inválido.", ConsumerName);
            return ConsumeOutcome.DeadLetter;
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await ApplyOnceAsync(envelope, cancellationToken);
                return ConsumeOutcome.Ack;
            }
            catch (Exception ex) when (ex is JsonException or PermanentEventException)
            {
                logger.LogError(ex, "El evento {EventType} {EventId} no se puede aplicar (correlación {CorrelationId}).",
                    envelope.EventType, envelope.EventId, envelope.CorrelationId);
                return ConsumeOutcome.DeadLetter;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Intento {Attempt} fallido para el evento {EventType} {EventId}.", attempt + 1, envelope.EventType, envelope.EventId);
                if (attempt >= RetryDelays.Count) return redelivered ? ConsumeOutcome.DeadLetter : ConsumeOutcome.Requeue;
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
        }
    }

    private async Task ApplyOnceAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TContext>();
        await using var transaction = await BeginTransactionAsync(database, cancellationToken);
        if (await database.ProcessedEvents.AnyAsync(item => item.EventId == envelope.EventId, cancellationToken)) return;

        await HandleAsync(envelope, database, cancellationToken);
        database.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            ProcessedAtUtc = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogWarning("RabbitMQ no está configurado; el consumidor {Consumer} permanecerá detenido.", ConsumerName);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // An escaped exception would stop the whole API host; a broker outage must not take the service down.
                logger.LogWarning("El consumidor {Consumer} se detuvo y se reintentará: {Reason}", ConsumerName, ex.Message);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(RabbitMqOptions settings, CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.Host, Port = settings.Port, VirtualHost = settings.VirtualHost,
            UserName = settings.UserName, Password = settings.Password,
            DispatchConsumersAsync = true, AutomaticRecoveryEnabled = true
        };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(settings.Exchange, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(settings.DeadLetterExchange, ExchangeType.Topic, durable: true);
        // Dead letters are routed by queue name so each consumer's DLQ only receives its own failures.
        channel.QueueDeclare(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(DeadLetterQueueName, settings.DeadLetterExchange, QueueName);
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false, new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = QueueName
        });
        channel.BasicQos(prefetchSize: 0, prefetchCount: 20, global: false);
        foreach (var eventType in Subscriptions)
            channel.QueueBind(QueueName, settings.Exchange, DomainEvents.RoutingKey(eventType));

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            ConsumeOutcome outcome;
            try
            {
                outcome = await ProcessAsync(args.Body, args.Redelivered, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                outcome = ConsumeOutcome.Requeue;
            }

            try
            {
                if (outcome == ConsumeOutcome.Ack) channel.BasicAck(args.DeliveryTag, multiple: false);
                else channel.BasicNack(args.DeliveryTag, multiple: false, requeue: outcome == ConsumeOutcome.Requeue);
            }
            catch (AlreadyClosedException)
            {
                logger.LogWarning("El canal se cerró antes de confirmar el mensaje; RabbitMQ lo entregará de nuevo.");
            }
        };
        channel.BasicConsume(QueueName, autoAck: false, consumer);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
