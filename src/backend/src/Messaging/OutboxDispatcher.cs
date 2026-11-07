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
    RabbitMqPublisher publisher,
    IOptions<RabbitMqOptions> options,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService
    where TContext : DbContext, IOutboxDbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await DispatchBatch(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Error inesperado en el dispatcher Outbox."); }
        }
    }

    private async Task DispatchBatch(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = DateTime.UtcNow;
        var pending = await database.Outbox
            .Where(item => item.PublishedAtUtc == null &&
                (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= now) &&
                item.Attempts < options.Value.MaxAttempts)
            .OrderBy(item => item.OccurredAtUtc).Take(20).ToListAsync(cancellationToken);
        foreach (var item in pending)
        {
            item.Attempts++;
            if (publisher.TryPublish(item))
            {
                item.PublishedAtUtc = DateTime.UtcNow;
                item.LastError = null;
            }
            else
            {
                item.LastError = "RabbitMQ no disponible.";
                item.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(Math.Min(60, Math.Pow(2, item.Attempts)));
            }
        }
        if (pending.Count > 0) await database.SaveChangesAsync(cancellationToken);
    }
}
