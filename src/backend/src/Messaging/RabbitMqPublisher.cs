using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Drovik.Messaging;

public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger) : IDisposable
{
    private readonly RabbitMqOptions settings = options.Value;
    private IConnection? connection;
    private IModel? channel;

    public bool TryPublish(OutboxMessage message)
    {
        try
        {
            EnsureChannel();
            var envelope = new EventEnvelope(
                message.Id, message.EventType, message.OccurredAtUtc, message.CorrelationId,
                message.ContractVersion, JsonSerializer.Deserialize<JsonElement>(message.Payload));
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
            var properties = channel!.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = message.Id.ToString();
            properties.Type = message.EventType;
            properties.ContentType = "application/json";
            channel.BasicPublish(settings.Exchange, message.EventType, properties, body);
            return true;
        }
        catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException ex)
        {
            logger.LogWarning(ex, "RabbitMQ no está disponible para publicar {EventId}.", message.Id);
            DisposeChannel();
            return false;
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "La conexión RabbitMQ falló al publicar {EventId}.", message.Id);
            DisposeChannel();
            return false;
        }
        catch (RabbitMQ.Client.Exceptions.AuthenticationFailureException ex)
        {
            logger.LogError(ex, "Las credenciales de RabbitMQ no son válidas; el evento {EventId} permanece en Outbox.", message.Id);
            DisposeChannel();
            return false;
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "RabbitMQ agotó el tiempo de conexión para el evento {EventId}.", message.Id);
            DisposeChannel();
            return false;
        }
    }

    private void EnsureChannel()
    {
        if (channel is not null && channel.IsOpen) return;
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
            UserName = settings.UserName,
            Password = settings.Password,
            AutomaticRecoveryEnabled = true
        };
        connection = factory.CreateConnection();
        channel = connection.CreateModel();
        channel.ExchangeDeclare(settings.Exchange, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(settings.DeadLetterExchange, ExchangeType.Topic, durable: true);
    }

    private void DisposeChannel()
    {
        channel?.Dispose();
        connection?.Dispose();
        channel = null;
        connection = null;
    }

    public void Dispose() => DisposeChannel();
}
