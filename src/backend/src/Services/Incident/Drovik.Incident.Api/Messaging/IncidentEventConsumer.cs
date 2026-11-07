using System.Text.Json;
using Drovik.Incident.Api.Persistence;
using Drovik.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Drovik.Incident.Api.Messaging;

public sealed class IncidentEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<IncidentEventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogWarning("RabbitMQ no está configurado para Incident; el consumidor permanecerá detenido.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException ex)
            {
                logger.LogWarning(ex, "RabbitMQ no está disponible para Incident; se reintentará.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "La conexión RabbitMQ de Incident se perdió; se reintentará.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
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
        var queue = channel.QueueDeclare("drovik.incident.audit.v1", durable: true, exclusive: false, autoDelete: false,
            new Dictionary<string, object> { ["x-dead-letter-exchange"] = settings.DeadLetterExchange }).QueueName;
        foreach (var eventType in new[]
                 {
                     "ShipmentCreated", "ShipmentAssigned", "ShipmentStatusChanged",
                     "ShipmentDelivered", "ShipmentCancelled", "VehicleAvailabilityChanged",
                     "DriverAvailabilityChanged"
                 })
        {
            channel.QueueBind(queue, settings.Exchange, eventType);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<EventEnvelope>(args.Body.Span)
                    ?? throw new JsonException("El envelope de evento está vacío.");
                await using var scope = scopeFactory.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<IncidentDbContext>();
                if (await database.ProcessedEvents.AnyAsync(item => item.EventId == envelope.EventId, stoppingToken))
                {
                    channel.BasicAck(args.DeliveryTag, multiple: false);
                    return;
                }

                database.ProcessedEvents.Add(new ProcessedEvent
                {
                    EventId = envelope.EventId,
                    EventType = envelope.EventType,
                    ProcessedAtUtc = DateTime.UtcNow
                });
                await database.SaveChangesAsync(stoppingToken);
                channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo procesar un evento de Incident.");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
        };
        channel.BasicConsume(queue, autoAck: false, consumer);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
