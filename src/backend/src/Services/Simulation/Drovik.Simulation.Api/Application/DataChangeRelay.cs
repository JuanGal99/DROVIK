using Drovik.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Drovik.Simulation.Api.Application;

/// <summary>
/// Tells connected screens that a kind of data changed so they can read it again from its owner.
/// Only events published after the owning service committed are relayed, so a screen never reads ahead of the change.
/// The message is a hint: nothing is stored, and a screen that misses one catches up when it reconnects.
/// </summary>
public sealed class DataChangeRelay(IHubContext<OperationsHub> hub, IOptions<RabbitMqOptions> options, ILogger<DataChangeRelay> logger) : BackgroundService
{
    public const string HubMessage = "changed";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private static readonly Dictionary<string, string> AreaByRoutingKey = new (string EventType, string Area)[]
    {
        (DomainEvents.ShipmentCreated, "shipments"),
        (DomainEvents.ShipmentAssigned, "shipments"),
        (DomainEvents.ShipmentStatusChanged, "shipments"),
        (DomainEvents.ShipmentDelivered, "shipments"),
        (DomainEvents.ShipmentCancelled, "shipments"),
        (DomainEvents.VehicleAvailabilityChanged, "fleet"),
        (DomainEvents.DriverAvailabilityChanged, "fleet"),
        (DomainEvents.IncidentCreated, "incidents"),
        (DomainEvents.IncidentStatusChanged, "incidents"),
        (DomainEvents.IncidentResolved, "incidents"),
    }.ToDictionary(item => DomainEvents.RoutingKey(item.EventType), item => item.Area, StringComparer.Ordinal);

    public Task NotifyAsync(string routingKey, CancellationToken cancellationToken) =>
        AreaByRoutingKey.TryGetValue(routingKey, out var area)
            ? hub.Clients.All.SendAsync(HubMessage, area, cancellationToken)
            : Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogWarning("RabbitMQ no está configurado; las pantallas no recibirán avisos de cambios.");
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
                logger.LogWarning("El aviso de cambios a las pantallas se detuvo y se reintentará: {Reason}", ex.Message);
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
        // A private queue that dies with this process: old hints are worthless, so nothing should wait for a restart.
        var queue = channel.QueueDeclare(queue: "", durable: false, exclusive: true, autoDelete: true).QueueName;
        foreach (var routingKey in AreaByRoutingKey.Keys)
            channel.QueueBind(queue, settings.Exchange, routingKey);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                await NotifyAsync(args.RoutingKey, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("No se pudo avisar a las pantallas del evento {RoutingKey}: {Reason}", args.RoutingKey, ex.Message);
            }
        };
        channel.BasicConsume(queue, autoAck: true, consumer);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
