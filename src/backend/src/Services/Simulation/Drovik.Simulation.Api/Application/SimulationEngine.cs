using Drovik.Messaging;
using Drovik.Simulation.Api.Domain;
using Drovik.Simulation.Api.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Simulation.Api.Application;

/// <summary>Server-side authority for vehicle positions. This single process is the only executor of the active run (ADR 0010).</summary>
public sealed class SimulationEngine(
    IServiceScopeFactory scopeFactory,
    IHubContext<OperationsHub> hub,
    TimeProvider clock,
    IConfiguration configuration,
    ILogger<SimulationEngine> logger) : BackgroundService
{
    // A long gap (a suspended laptop, a stalled database) must not teleport vehicles along their route.
    private static readonly TimeSpan MaxStep = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PauseInterruptedRunsAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("Simulation:TickSeconds", 1))));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Error inesperado al avanzar la operación."); }
        }
    }

    /// <summary>A run left active by a previous process resumes only when an operator asks for it.</summary>
    public async Task PauseInterruptedRunsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SimulationDbContext>();
        foreach (var run in await database.Runs.Where(item => item.Status == SimulationRunStatus.Running).ToListAsync(cancellationToken))
            run.Status = SimulationRunStatus.Paused;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SimulationDbContext>();
        var run = await database.Runs.Include(item => item.Vehicles).SingleOrDefaultAsync(item => item.Status == SimulationRunStatus.Running, cancellationToken);
        if (run is null) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var elapsed = TimeSpan.FromTicks(Math.Clamp((now - run.LastTickAtUtc).Ticks, 0, MaxStep.Ticks)).TotalSeconds;
        run.LastTickAtUtc = now;
        foreach (var vehicle in run.Vehicles.Where(item => item.Status == SimulatedVehicleStatus.Moving))
        {
            vehicle.ProgressMeters = Math.Min(vehicle.DistanceMeters, vehicle.ProgressMeters + LiveState.SpeedOf(run, vehicle) * elapsed);
            var position = RouteMath.PointAt(LiveState.DeserializeRoute(vehicle.RouteJson), vehicle.ProgressMeters);
            vehicle.Latitude = position.Latitude;
            vehicle.Longitude = position.Longitude;
            if (vehicle.ProgressMeters < vehicle.DistanceMeters) continue;
            vehicle.Status = SimulatedVehicleStatus.Arrived;
            database.Outbox.Add(OutboxMessageFactory.Create(DomainEvents.ShipmentRouteCompleted,
                new ShipmentRouteCompletedV1(vehicle.ShipmentId, run.Id), vehicle.ShipmentId.ToString()));
        }

        if (run.Vehicles.All(item => item.Status != SimulatedVehicleStatus.Moving))
        {
            run.Status = SimulationRunStatus.Completed;
            run.EndedAtUtc = now;
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogDebug("La operación cambió de estado durante el avance; se reintentará.");
            return;
        }
        await hub.Clients.All.SendAsync("state", LiveState.ToResponse(run, includeRoutes: false), cancellationToken);
    }
}
