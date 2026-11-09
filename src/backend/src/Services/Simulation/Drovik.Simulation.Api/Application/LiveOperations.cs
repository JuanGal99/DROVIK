using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Drovik.Messaging;
using Drovik.Simulation.Api.Domain;
using Drovik.Simulation.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Simulation.Api.Application;

public sealed record StartRunRequest([property: Range(1, 120)] int SpeedMultiplier);
public enum LiveScenario { Congestion, Breakdown, Detour, Repair }
public sealed record ApplyScenarioRequest(Guid ShipmentId, LiveScenario Scenario);
public sealed record LiveRunResponse(Guid Id, string Status, int SpeedMultiplier, DateTime StartedAtUtc);
public sealed record LiveVehicleResponse(Guid ShipmentId, Guid? VehicleId, string TrackingCode, string OriginName, string DestinationName,
    string Status, bool IsDelayed, bool IsBrokenDown, bool IsDetoured, int ProgressPercent, double Latitude, double Longitude, int? RemainingSeconds, double[][]? Route);
public sealed record LiveStateResponse(LiveRunResponse? Run, IReadOnlyList<LiveVehicleResponse> Vehicles);

[Authorize(Roles = "Administrator,Operator")]
public sealed class OperationsHub : Hub;

public sealed record ActiveShipment(Guid Id, string TrackingCode, string CustomerName, string? OriginName, decimal? OriginLatitude, decimal? OriginLongitude,
    decimal DestinationLatitude, decimal DestinationLongitude, Guid? VehicleId, string Status);

/// <summary>Reads the shipments on the road from Shipment's API, on behalf of the operator who made the request.</summary>
public sealed class ShipmentFeed(IHttpClientFactory clients)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private sealed record Page(List<ActiveShipment> Items);

    /// <returns>The shipments on the road, or null when Shipment cannot be reached.</returns>
    public async Task<IReadOnlyList<ActiveShipment>?> GetOnRouteAsync(string? authorization, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient("shipment");
        try
        {
            var results = await Task.WhenAll(ReadAsync(client, "InTransit", authorization, cancellationToken), ReadAsync(client, "Delayed", authorization, cancellationToken));
            return results.SelectMany(items => items).Where(item => item.OriginLatitude.HasValue && item.OriginLongitude.HasValue).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }

    private static async Task<List<ActiveShipment>> ReadAsync(HttpClient client, string status, string? authorization, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/shipments?status={status}&pageSize=100");
        if (!string.IsNullOrEmpty(authorization)) request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Page>(Options, cancellationToken))?.Items ?? [];
    }
}

public static class LiveState
{
    public const double BaseSpeedMetersPerSecond = 11;
    public const double DelayedSpeedFactor = 0.5;
    private static readonly JsonSerializerOptions RouteOptions = new(JsonSerializerDefaults.Web);

    public static string SerializeRoute(IReadOnlyList<RoutePoint> route) => JsonSerializer.Serialize(route, RouteOptions);
    public static IReadOnlyList<RoutePoint> DeserializeRoute(string json) => JsonSerializer.Deserialize<List<RoutePoint>>(json, RouteOptions) ?? [];

    public static double SpeedOf(SimulationRun run, SimulatedVehicle vehicle) =>
        vehicle.IsBrokenDown ? 0 : BaseSpeedMetersPerSecond * run.SpeedMultiplier * (vehicle.IsDelayed ? DelayedSpeedFactor : 1);

    public static LiveStateResponse ToResponse(SimulationRun run, bool includeRoutes) => new(
        new LiveRunResponse(run.Id, run.Status.ToString(), run.SpeedMultiplier, run.StartedAtUtc),
        run.Vehicles.Where(item => item.Status != SimulatedVehicleStatus.Removed).OrderBy(item => item.TrackingCode).Select(item => new LiveVehicleResponse(
            item.ShipmentId, item.VehicleId, item.TrackingCode, item.OriginName, item.DestinationName, item.Status.ToString(), item.IsDelayed, item.IsBrokenDown, item.IsDetoured,
            item.DistanceMeters > 0 ? (int)Math.Round(item.ProgressMeters / item.DistanceMeters * 100) : 100,
            item.Latitude, item.Longitude,
            item.Status == SimulatedVehicleStatus.Moving && !item.IsBrokenDown ? (int)Math.Ceiling((item.DistanceMeters - item.ProgressMeters) / SpeedOf(run, item)) : null,
            includeRoutes ? DeserializeRoute(item.RouteJson).Select(point => new[] { point.Latitude, point.Longitude }).ToArray() : null)).ToList());
}

public static class LiveOperationsEndpoints
{
    private static readonly SimulationRunStatus[] ActiveStatuses = [SimulationRunStatus.Running, SimulationRunStatus.Paused];

    public static void MapLiveOperations(this WebApplication app)
    {
        var simulations = app.MapGroup("/api/v1/simulations").RequireAuthorization(new AuthorizeAttribute { Roles = "Administrator,Operator" });
        simulations.MapGet("live", GetLiveAsync);
        simulations.MapPost("", StartAsync);
        simulations.MapPost("{id:guid}/sync", SyncAsync);
        simulations.MapPost("{id:guid}/scenarios", ApplyScenarioAsync);
        simulations.MapPost("{id:guid}/speed", ChangeSpeedAsync);
        simulations.MapPost("demo-reset", ResetAsync).RequireAuthorization(new AuthorizeAttribute { Roles = "Administrator" });
        simulations.MapPost("{id:guid}/pause", (Guid id, SimulationDbContext database, IHubContext<OperationsHub> hub, TimeProvider clock, CancellationToken cancellationToken) =>
            ChangeAsync(id, SimulationRunStatus.Running, SimulationRunStatus.Paused, database, hub, clock, cancellationToken));
        simulations.MapPost("{id:guid}/resume", (Guid id, SimulationDbContext database, IHubContext<OperationsHub> hub, TimeProvider clock, CancellationToken cancellationToken) =>
            ChangeAsync(id, SimulationRunStatus.Paused, SimulationRunStatus.Running, database, hub, clock, cancellationToken));
        simulations.MapPost("{id:guid}/cancel", (Guid id, SimulationDbContext database, IHubContext<OperationsHub> hub, TimeProvider clock, CancellationToken cancellationToken) =>
            ChangeAsync(id, null, SimulationRunStatus.Cancelled, database, hub, clock, cancellationToken));
        app.MapHub<OperationsHub>("/hubs/operations");
    }

    private static async Task<IResult> GetLiveAsync(SimulationDbContext database, CancellationToken cancellationToken)
    {
        var run = await database.Runs.AsNoTracking().Include(item => item.Vehicles)
            .SingleOrDefaultAsync(item => ActiveStatuses.Contains(item.Status), cancellationToken);
        return Results.Ok(run is null ? new LiveStateResponse(null, []) : LiveState.ToResponse(run, includeRoutes: true));
    }

    private static async Task<IResult> StartAsync(StartRunRequest request, SimulationDbContext database, ShipmentFeed feed, IRouteProvider routes,
        TimeProvider clock, HttpContext context, CancellationToken cancellationToken)
    {
        if (request.SpeedMultiplier is < 1 or > 120) return Problem(400, "Velocidad inválida", "La velocidad debe estar entre 1 y 120.");
        if (await database.Runs.AnyAsync(item => ActiveStatuses.Contains(item.Status), cancellationToken))
            return Problem(409, "Conflicto", "Ya hay una operación en curso.");
        var shipments = await feed.GetOnRouteAsync(context.Request.Headers.Authorization, cancellationToken);
        if (shipments is null) return ShipmentsUnavailable();
        if (shipments.Count == 0) return Problem(409, "Sin envíos en ruta", "Inicia la ruta de al menos un envío para comenzar la operación.");

        var now = clock.GetUtcNow().UtcDateTime;
        var run = new SimulationRun { Id = Guid.NewGuid(), Status = SimulationRunStatus.Running, SpeedMultiplier = request.SpeedMultiplier, StartedAtUtc = now, LastTickAtUtc = now };
        Reconcile(run, shipments, routes);
        database.Runs.Add(run);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Problem(409, "Conflicto", "Ya hay una operación en curso.");
        }
        return Results.Created($"/api/v1/simulations/{run.Id}", LiveState.ToResponse(run, includeRoutes: true));
    }

    /// <summary>Brings a running operation up to date with shipments that entered or left the road since it started.</summary>
    private static async Task<IResult> SyncAsync(Guid id, SimulationDbContext database, ShipmentFeed feed, IRouteProvider routes, HttpContext context, CancellationToken cancellationToken)
    {
        var run = await database.Runs.Include(item => item.Vehicles).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (run is null) return Results.NotFound();
        if (!ActiveStatuses.Contains(run.Status)) return Results.Ok(LiveState.ToResponse(run, includeRoutes: true));
        var shipments = await feed.GetOnRouteAsync(context.Request.Headers.Authorization, cancellationToken);
        if (shipments is null) return ShipmentsUnavailable();
        Reconcile(run, shipments, routes, database);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Problem(409, "Conflicto", "La operación cambió mientras se actualizaba; inténtalo de nuevo.");
        }
        return Results.Ok(LiveState.ToResponse(run, includeRoutes: true));
    }

    private static async Task<IResult> ChangeAsync(Guid id, SimulationRunStatus? requiredStatus, SimulationRunStatus nextStatus, SimulationDbContext database,
        IHubContext<OperationsHub> hub, TimeProvider clock, CancellationToken cancellationToken)
    {
        var run = await database.Runs.Include(item => item.Vehicles).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (run is null) return Results.NotFound();
        if (requiredStatus is { } required ? run.Status != required : !ActiveStatuses.Contains(run.Status))
            return Problem(409, "Conflicto", "La operación no admite esta acción en su estado actual.");
        var now = clock.GetUtcNow().UtcDateTime;
        run.Status = nextStatus;
        // Time spent paused must not count as distance travelled.
        run.LastTickAtUtc = now;
        if (nextStatus == SimulationRunStatus.Cancelled) run.EndedAtUtc = now;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(409, "Conflicto", "La operación cambió al mismo tiempo; inténtalo de nuevo.");
        }
        var state = LiveState.ToResponse(run, includeRoutes: false);
        await hub.Clients.All.SendAsync("state", state, cancellationToken);
        return Results.Ok(state);
    }

    private static async Task<IResult> ChangeSpeedAsync(Guid id, StartRunRequest request, SimulationDbContext database, IHubContext<OperationsHub> hub, CancellationToken cancellationToken)
    {
        if (request.SpeedMultiplier is < 1 or > 120) return Problem(400, "Velocidad inválida", "La velocidad debe estar entre 1 y 120.");
        var run = await database.Runs.Include(item => item.Vehicles).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (run is null) return Results.NotFound();
        if (!ActiveStatuses.Contains(run.Status)) return Problem(409, "Conflicto", "La operación ya terminó.");
        run.SpeedMultiplier = request.SpeedMultiplier;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(409, "Conflicto", "La operación cambió al mismo tiempo; inténtalo de nuevo.");
        }
        var state = LiveState.ToResponse(run, includeRoutes: false);
        await hub.Clients.All.SendAsync("state", state, cancellationToken);
        return Results.Ok(state);
    }

    /// <summary>Applies an operator's scenario to one vehicle. Incidents and shipment changes follow from the published event.</summary>
    private static async Task<IResult> ApplyScenarioAsync(Guid id, ApplyScenarioRequest request, SimulationDbContext database, IRouteProvider routes,
        IHubContext<OperationsHub> hub, CancellationToken cancellationToken)
    {
        var run = await database.Runs.Include(item => item.Vehicles).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (run is null) return Results.NotFound();
        if (!ActiveStatuses.Contains(run.Status)) return Problem(409, "Conflicto", "La operación ya terminó.");
        var vehicle = run.Vehicles.SingleOrDefault(item => item.ShipmentId == request.ShipmentId && item.Status == SimulatedVehicleStatus.Moving);
        if (vehicle is null) return Problem(409, "Conflicto", "El vehículo ya no está en ruta.");

        var rejection = request.Scenario switch
        {
            LiveScenario.Congestion => vehicle.IsDelayed ? "El vehículo ya está retrasado." : null,
            LiveScenario.Breakdown => vehicle.IsBrokenDown ? "El vehículo ya está averiado." : null,
            LiveScenario.Repair => vehicle.IsBrokenDown ? null : "El vehículo no está averiado.",
            LiveScenario.Detour when vehicle.IsBrokenDown => "Un vehículo averiado no puede desviarse.",
            LiveScenario.Detour when vehicle.IsDetoured => "El vehículo ya tomó un desvío.",
            _ => null
        };
        if (rejection is not null) return Problem(409, "Conflicto", rejection);

        switch (request.Scenario)
        {
            case LiveScenario.Congestion:
                vehicle.IsDelayed = true;
                Announce(database, DomainEvents.RouteDelayDetected, run, vehicle);
                break;
            case LiveScenario.Breakdown:
                vehicle.IsBrokenDown = true;
                Announce(database, DomainEvents.VehicleBreakdownDetected, run, vehicle);
                break;
            case LiveScenario.Repair:
                vehicle.IsBrokenDown = false;
                break;
            case LiveScenario.Detour:
                var route = LiveState.DeserializeRoute(vehicle.RouteJson);
                var detour = routes.BuildDetour(new RoutePoint(vehicle.Latitude, vehicle.Longitude), route[^1], vehicle.DistanceMeters - vehicle.ProgressMeters);
                if (detour is null) return Problem(409, "Sin desvío disponible", "No hay una vía alterna desde la posición actual.");
                List<RoutePoint> updated = [.. RouteMath.TakeUntil(route, vehicle.ProgressMeters), .. detour];
                vehicle.RouteJson = LiveState.SerializeRoute(updated);
                vehicle.DistanceMeters = RouteMath.Length(updated);
                vehicle.IsDetoured = true;
                Announce(database, DomainEvents.RouteDeviationDetected, run, vehicle);
                break;
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(409, "Conflicto", "La operación cambió al mismo tiempo; inténtalo de nuevo.");
        }
        await hub.Clients.All.SendAsync("state", LiveState.ToResponse(run, includeRoutes: false), cancellationToken);
        return Results.Ok(LiveState.ToResponse(run, includeRoutes: true));
    }

    private static void Announce(SimulationDbContext database, string eventType, SimulationRun run, SimulatedVehicle vehicle) =>
        database.Outbox.Add(OutboxMessageFactory.Create(eventType,
            new RouteScenarioDetectedV1(vehicle.ShipmentId, run.Id, vehicle.VehicleId, vehicle.TrackingCode), vehicle.ShipmentId.ToString()));

    private static async Task<IResult> ResetAsync(SimulationDbContext database, IHubContext<OperationsHub> hub, ClaimsPrincipal principal,
        ILogger<SimulationEngine> logger, CancellationToken cancellationToken)
    {
        await database.Vehicles.ExecuteDeleteAsync(cancellationToken);
        await database.Runs.ExecuteDeleteAsync(cancellationToken);
        await database.Outbox.Where(item => item.PublishedAtUtc == null).ExecuteDeleteAsync(cancellationToken);
        logger.LogWarning("Datos de operación reiniciados por {Actor}.", principal.FindFirst("sub")?.Value);
        await hub.Clients.All.SendAsync("state", new LiveStateResponse(null, []), cancellationToken);
        return Results.NoContent();
    }

    private static void Reconcile(SimulationRun run, IReadOnlyList<ActiveShipment> shipments, IRouteProvider routes, SimulationDbContext? tracked = null)
    {
        var onRoute = shipments.ToDictionary(item => item.Id);
        foreach (var vehicle in run.Vehicles.Where(item => item.Status == SimulatedVehicleStatus.Moving))
        {
            if (onRoute.TryGetValue(vehicle.ShipmentId, out var shipment)) vehicle.IsDelayed |= shipment.Status == "Delayed";
            else vehicle.Status = SimulatedVehicleStatus.Removed;
        }

        foreach (var shipment in shipments.Where(item => run.Vehicles.All(vehicle => vehicle.ShipmentId != item.Id)))
        {
            var route = routes.Build(new RoutePoint((double)shipment.OriginLatitude!, (double)shipment.OriginLongitude!),
                new RoutePoint((double)shipment.DestinationLatitude, (double)shipment.DestinationLongitude));
            var vehicle = new SimulatedVehicle
            {
                Id = Guid.NewGuid(), RunId = run.Id, ShipmentId = shipment.Id, VehicleId = shipment.VehicleId, TrackingCode = shipment.TrackingCode,
                OriginName = shipment.OriginName ?? string.Empty, DestinationName = shipment.CustomerName, IsDelayed = shipment.Status == "Delayed",
                RouteJson = LiveState.SerializeRoute(route), DistanceMeters = RouteMath.Length(route),
                Latitude = route[0].Latitude, Longitude = route[0].Longitude, Status = SimulatedVehicleStatus.Moving
            };
            run.Vehicles.Add(vehicle);
            // A vehicle added through an already tracked run has an assigned key, which EF would otherwise treat as an update.
            tracked?.Vehicles.Add(vehicle);
        }
    }

    private static IResult ShipmentsUnavailable() => Problem(503, "Envíos no disponibles", "No se pudo consultar los envíos; inténtalo de nuevo.");
    private static IResult Problem(int status, string title, string detail) => Results.Problem(statusCode: status, title: title, detail: detail);
}
