using System.Data;
using Drovik.Fleet.Api.Domain;
using Drovik.Fleet.Api.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Fleet.Api.Application;

/// <summary>Frees resources held by reservations nobody confirmed before their TTL.</summary>
public sealed class ReservationExpiryWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("Fleet:ReservationSweepSeconds", 30)));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await SweepAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Error inesperado al expirar reservas."); }
        }
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        List<Guid> expiredIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var now = DateTime.UtcNow;
            expiredIds = await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Reservations
                .Where(item => item.Status == ReservationStatus.Reserved && item.ExpiresAtUtc <= now)
                .OrderBy(item => item.ExpiresAtUtc).Select(item => item.Id).Take(BatchSize).ToListAsync(cancellationToken);
        }

        var expiredCount = 0;
        foreach (var id in expiredIds)
        {
            try
            {
                if (await ExpireOneAsync(id, cancellationToken)) expiredCount++;
            }
            catch (Exception ex) when (ex is DbUpdateException or SqlException { Number: 1205 })
            {
                logger.LogInformation("La reserva {ReservationId} cambió durante la expiración; se revisará en el próximo ciclo.", id);
            }
        }
        return expiredCount;
    }

    // One transaction per reservation so a conflict on one of them does not undo the others.
    private async Task<bool> ExpireOneAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = DateTime.UtcNow;
        var reservation = await database.Reservations.SingleOrDefaultAsync(
            item => item.Id == id && item.Status == ReservationStatus.Reserved && item.ExpiresAtUtc <= now, cancellationToken);
        if (reservation is null) return false;
        await ReservationLifecycle.ExpireAsync(database, reservation, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
