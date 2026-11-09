using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Drovik.Messaging;

public interface IOutboxDbContext
{
    DbSet<OutboxMessage> Outbox { get; }
}

public sealed class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopeFactory,
    IEventPublisher publisher,
    IOptions<RabbitMqOptions> options,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService
    where TContext : DbContext, IOutboxDbContext
{
    private const int BatchSize = 20;
    private const double MaxBackoffSeconds = 60;
    private int consecutiveBrokerFailures;
    private DateTime nextBrokerAttemptUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await DispatchPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Error inesperado en el dispatcher Outbox."); }
        }
    }

    /// <summary>
    /// Publishes one batch in order. A broker outage stops the batch and backs off without spending the
    /// attempts of any message, so events survive outages of any length; only messages the broker path
    /// can never accept consume attempts up to <see cref="RabbitMqOptions.MaxAttempts"/>.
    /// </summary>
    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < nextBrokerAttemptUtc) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = DateTime.UtcNow;
        var pending = await database.Outbox
            .Where(item => item.PublishedAtUtc == null &&
                (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= now) &&
                item.Attempts < options.Value.MaxAttempts)
            .OrderBy(item => item.OccurredAtUtc).Take(BatchSize).ToListAsync(cancellationToken);

        foreach (var item in pending)
        {
            var outcome = publisher.Publish(item);
            if (outcome == PublishOutcome.BrokerUnavailable)
            {
                item.LastError = "RabbitMQ no disponible.";
                consecutiveBrokerFailures++;
                nextBrokerAttemptUtc = DateTime.UtcNow.AddSeconds(BackoffSeconds(consecutiveBrokerFailures));
                logger.LogWarning("RabbitMQ no disponible; los eventos permanecen en Outbox y se reintentará en {Seconds} s.",
                    BackoffSeconds(consecutiveBrokerFailures));
                break;
            }

            consecutiveBrokerFailures = 0;
            item.Attempts++;
            if (outcome == PublishOutcome.Published)
            {
                item.PublishedAtUtc = DateTime.UtcNow;
                item.LastError = null;
                item.NextAttemptAtUtc = null;
            }
            else
            {
                item.LastError = "El evento no se puede convertir en un envelope válido.";
                item.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(BackoffSeconds(item.Attempts));
            }
        }

        if (pending.Count > 0) await database.SaveChangesAsync(cancellationToken);
    }

    private static double BackoffSeconds(int failures) => Math.Min(MaxBackoffSeconds, Math.Pow(2, Math.Min(failures, 16)));
}
