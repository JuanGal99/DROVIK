using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Drovik.Messaging;

public enum PublishOutcome
{
    Published,
    /// <summary>The broker could not be reached or did not confirm; the message itself is fine.</summary>
    BrokerUnavailable,
    /// <summary>The message cannot be turned into an envelope and will never be publishable.</summary>
    Rejected
}

public interface IEventPublisher
{
    PublishOutcome Publish(OutboxMessage message);
}

public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger) : IEventPublisher, IDisposable
{
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(5);
    private readonly RabbitMqOptions settings = options.Value;
    private IConnection? connection;
    private IModel? channel;

    public PublishOutcome Publish(OutboxMessage message)
    {
        if (string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.Password))
            return PublishOutcome.BrokerUnavailable;

        byte[] body;
        string routingKey;
        try
        {
            body = EventEnvelopeSerializer.Serialize(message);
            routingKey = DomainEvents.RoutingKey(message.EventType, message.ContractVersion);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            logger.LogError(ex, "El evento {EventId} no cumple el contrato y no se puede publicar.", message.Id);
            return PublishOutcome.Rejected;
        }

        try
        {
            var activeChannel = EnsureChannel();
            var properties = activeChannel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = message.Id.ToString();
            properties.Type = message.EventType;
            properties.ContentType = "application/json";
            activeChannel.BasicPublish(settings.Exchange, routingKey, properties, body);
            activeChannel.WaitForConfirmsOrDie(ConfirmTimeout);
            return PublishOutcome.Published;
        }
        catch (Exception ex) when (ex is RabbitMQClientException or IOException or SocketException or TimeoutException)
        {
            logger.LogWarning("RabbitMQ no confirmó el evento {EventId}: {Reason}", message.Id, ex.Message);
            DisposeChannel();
            return PublishOutcome.BrokerUnavailable;
        }
    }

    private IModel EnsureChannel()
    {
        if (channel is { IsOpen: true }) return channel;
        DisposeChannel();
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
            UserName = settings.UserName,
            Password = settings.Password,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
            AutomaticRecoveryEnabled = false
        };
        connection = factory.CreateConnection();
        channel = connection.CreateModel();
        channel.ExchangeDeclare(settings.Exchange, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(settings.DeadLetterExchange, ExchangeType.Topic, durable: true);
        channel.ConfirmSelect();
        return channel;
    }

    private void DisposeChannel()
    {
        try
        {
            channel?.Dispose();
            connection?.Dispose();
        }
        catch (Exception ex) when (ex is RabbitMQClientException or IOException or SocketException)
        {
            logger.LogDebug(ex, "La conexión RabbitMQ ya estaba cerrada.");
        }
        channel = null;
        connection = null;
    }

    public void Dispose() => DisposeChannel();
}
