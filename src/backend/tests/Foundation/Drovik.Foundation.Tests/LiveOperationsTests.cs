using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Drovik.Messaging;
using Drovik.Simulation.Api.Application;
using Drovik.Simulation.Api.Domain;
using Drovik.Simulation.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Drovik.Foundation.Tests;

public sealed class LiveOperationsTests : IAsyncLifetime
{
    private static readonly RoutePoint Casa = new(4.741, -74.084);
    private static readonly RoutePoint Mercado = new(4.6486, -74.0628);
    private static readonly Guid ShipmentId = Guid.NewGuid();

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly StubShipments _shipments = new();
    private readonly ManualClock _clock = new();
    private Factory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _factory = new Factory(_rsa, _connection, _shipments, _clock);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("Operator"));
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SimulationDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
        _rsa.Dispose();
    }

    private SimulationEngine Engine => _factory.Services.GetRequiredService<SimulationEngine>();

    [Fact]
    public void Routes_are_repeatable_and_join_both_points()
    {
        var provider = new LocalRouteProvider();

        var first = provider.Build(Casa, Mercado);

        Assert.Equal(first, provider.Build(Casa, Mercado));
        Assert.Equal(Casa, first[0]);
        Assert.Equal(Mercado, first[^1]);
        Assert.True(RouteMath.Length(first) > 10_000);
    }

    [Fact]
    public void Position_follows_the_route_by_distance()
    {
        var route = new[] { new RoutePoint(4, -74), new RoutePoint(4.01, -74), new RoutePoint(4.01, -74.01) };

        Assert.Equal(route[0], RouteMath.PointAt(route, 0));
        Assert.Equal(route[^1], RouteMath.PointAt(route, RouteMath.Length(route) + 500));
        Assert.Equal(4.005, RouteMath.PointAt(route, RouteMath.Length(route) / 4).Latitude, 3);
    }

    [Fact]
    public async Task Live_routes_require_an_operational_role()
    {
        using var client = _factory.CreateClient();
        using var anonymous = await client.GetAsync("/api/v1/simulations/live");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("Guest"));
        using var wrongRole = await client.PostAsJsonAsync("/api/v1/simulations", new { speedMultiplier = 30 });
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }

    [Fact]
    public async Task Run_cannot_start_without_shipments_on_the_road()
    {
        using var response = await Start();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Run_reports_503_when_shipments_cannot_be_read()
    {
        _shipments.Fail = true;

        using var response = await Start();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Only_one_run_can_be_active()
    {
        _shipments.Add(ShipmentId);
        using (await Start()) { }

        using var second = await Start();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Vehicle_advances_with_elapsed_server_time()
    {
        _shipments.Add(ShipmentId);
        using (await Start(multiplier: 10)) { }

        _clock.Advance(TimeSpan.FromSeconds(2));
        await Engine.TickAsync(CancellationToken.None);

        var vehicle = await Vehicle();
        Assert.Equal(LiveState.BaseSpeedMetersPerSecond * 10 * 2, vehicle.ProgressMeters, 3);
    }

    [Fact]
    public async Task Paused_time_does_not_count_as_distance()
    {
        _shipments.Add(ShipmentId);
        var run = await StartAndGetId();
        using (await _client.PostAsync($"/api/v1/simulations/{run}/pause", null)) { }

        _clock.Advance(TimeSpan.FromSeconds(3));
        await Engine.TickAsync(CancellationToken.None);
        Assert.Equal(0, (await Vehicle()).ProgressMeters);

        using (await _client.PostAsync($"/api/v1/simulations/{run}/resume", null)) { }
        _clock.Advance(TimeSpan.FromSeconds(1));
        await Engine.TickAsync(CancellationToken.None);
        Assert.Equal(LiveState.BaseSpeedMetersPerSecond * 30, (await Vehicle()).ProgressMeters, 3);
    }

    [Fact]
    public async Task Long_gap_does_not_teleport_the_vehicle()
    {
        _shipments.Add(ShipmentId);
        using (await Start(multiplier: 1)) { }

        _clock.Advance(TimeSpan.FromHours(1));
        await Engine.TickAsync(CancellationToken.None);

        Assert.Equal(LiveState.BaseSpeedMetersPerSecond * 5, (await Vehicle()).ProgressMeters, 3);
    }

    [Fact]
    public async Task Delayed_shipment_moves_at_half_speed()
    {
        _shipments.Add(ShipmentId, "Delayed");
        using (await Start(multiplier: 10)) { }

        _clock.Advance(TimeSpan.FromSeconds(2));
        await Engine.TickAsync(CancellationToken.None);

        Assert.Equal(LiveState.BaseSpeedMetersPerSecond * 10, (await Vehicle()).ProgressMeters, 3);
    }

    [Fact]
    public async Task Arrival_is_announced_once_and_completes_the_run()
    {
        _shipments.Add(ShipmentId);
        using (await Start(multiplier: 120)) { }

        for (var step = 0; step < 40; step++)
        {
            _clock.Advance(TimeSpan.FromSeconds(5));
            await Engine.TickAsync(CancellationToken.None);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SimulationDbContext>();
        Assert.Equal(SimulationRunStatus.Completed, (await database.Runs.SingleAsync()).Status);
        Assert.Equal(1, await database.Outbox.CountAsync(item => item.EventType == DomainEvents.ShipmentRouteCompleted));
        var vehicle = await database.Vehicles.SingleAsync();
        Assert.Equal(Mercado.Latitude, vehicle.Latitude, 4);
    }

    [Fact]
    public async Task Restart_leaves_an_active_run_paused()
    {
        _shipments.Add(ShipmentId);
        using (await Start()) { }

        await Engine.PauseInterruptedRunsAsync(CancellationToken.None);

        using var live = await _client.GetAsync("/api/v1/simulations/live");
        Assert.Equal("Paused", (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("run").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sync_adds_new_shipments_and_drops_those_that_left_the_road()
    {
        _shipments.Add(ShipmentId);
        var run = await StartAndGetId();
        var other = Guid.NewGuid();
        _shipments.Clear();
        _shipments.Add(other);

        using var synced = await _client.PostAsync($"/api/v1/simulations/{run}/sync", null);

        Assert.Equal(HttpStatusCode.OK, synced.StatusCode);
        var vehicles = (await synced.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("vehicles");
        Assert.Equal(other, Assert.Single(vehicles.EnumerateArray()).GetProperty("shipmentId").GetGuid());
    }

    [Fact]
    public async Task Congestion_slows_the_vehicle_and_is_announced_once()
    {
        _shipments.Add(ShipmentId);
        var run = await StartAndGetId();

        using var applied = await Scenario(run, "Congestion");
        using var repeated = await Scenario(run, "Congestion");

        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Equal(1, await CountEvents(DomainEvents.RouteDelayDetected));
        _clock.Advance(TimeSpan.FromSeconds(2));
        await Engine.TickAsync(CancellationToken.None);
        Assert.Equal(LiveState.BaseSpeedMetersPerSecond * 30, (await Vehicle()).ProgressMeters, 3);
    }

    [Fact]
    public async Task Breakdown_stops_the_vehicle_until_it_is_repaired()
    {
        _shipments.Add(ShipmentId);
        var run = await StartAndGetId();
        using (await Scenario(run, "Breakdown")) { }

        _clock.Advance(TimeSpan.FromSeconds(3));
        await Engine.TickAsync(CancellationToken.None);
        Assert.Equal(0, (await Vehicle()).ProgressMeters);
        Assert.Equal(1, await CountEvents(DomainEvents.VehicleBreakdownDetected));

        using (await Scenario(run, "Repair")) { }
        _clock.Advance(TimeSpan.FromSeconds(1));
        await Engine.TickAsync(CancellationToken.None);
        Assert.True((await Vehicle()).ProgressMeters > 0);
    }

    [Fact]
    public async Task Detour_replaces_the_rest_of_the_route_with_a_longer_street_path()
    {
        _shipments.Add(ShipmentId);
        var run = await StartAndGetId();
        _clock.Advance(TimeSpan.FromSeconds(3));
        await Engine.TickAsync(CancellationToken.None);
        var before = await Vehicle();

        using var detour = await Scenario(run, "Detour");
        using var again = await Scenario(run, "Detour");

        Assert.Equal(HttpStatusCode.OK, detour.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var after = await Vehicle();
        Assert.Equal(before.ProgressMeters, after.ProgressMeters, 3);
        Assert.True(after.DistanceMeters > before.DistanceMeters);
        Assert.Equal(1, await CountEvents(DomainEvents.RouteDeviationDetected));
        var route = LiveState.DeserializeRoute(after.RouteJson);
        Assert.Equal(Mercado.Latitude, route[^1].Latitude, 4);
    }

    [Fact]
    public async Task Scenario_requires_a_vehicle_on_the_road()
    {
        _shipments.Add(ShipmentId);
        var run = await StartAndGetId();

        using var response = await _client.PostAsJsonAsync($"/api/v1/simulations/{run}/scenarios", new { shipmentId = Guid.NewGuid(), scenario = "Breakdown" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reset_is_only_for_administrators_and_clears_the_run()
    {
        _shipments.Add(ShipmentId);
        using (await Start()) { }
        using var denied = await _client.PostAsync("/api/v1/simulations/demo-reset", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("Administrator"));
        using var reset = await admin.PostAsync("/api/v1/simulations/demo-reset", null);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<SimulationDbContext>().Runs.CountAsync());
    }

    [Fact]
    public async Task Cancelled_run_stops_being_live()
    {
        _shipments.Add(ShipmentId);
        var run = await StartAndGetId();

        using (await _client.PostAsync($"/api/v1/simulations/{run}/cancel", null)) { }

        using var live = await _client.GetAsync("/api/v1/simulations/live");
        Assert.Equal(JsonValueKind.Null, (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("run").ValueKind);
    }

    private Task<HttpResponseMessage> Scenario(Guid run, string scenario) =>
        _client.PostAsJsonAsync($"/api/v1/simulations/{run}/scenarios", new { shipmentId = ShipmentId, scenario });

    private async Task<int> CountEvents(string eventType)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SimulationDbContext>().Outbox.CountAsync(item => item.EventType == eventType);
    }

    private Task<HttpResponseMessage> Start(int multiplier = 30) =>
        _client.PostAsJsonAsync("/api/v1/simulations", new { speedMultiplier = multiplier });

    private async Task<Guid> StartAndGetId()
    {
        using var response = await Start();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("run").GetProperty("id").GetGuid();
    }

    private async Task<SimulatedVehicle> Vehicle()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SimulationDbContext>().Vehicles.AsNoTracking().SingleAsync();
    }

    private string Token(string role)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken("DROVIK.Identity", "DROVIK.Services",
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", role)], DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(15), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class StubShipments : HttpMessageHandler
    {
        private readonly List<(Guid Id, string Status)> _items = [];
        public bool Fail { get; set; }
        public void Add(Guid id, string status = "InTransit") => _items.Add((id, status));
        public void Clear() => _items.Clear();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail) throw new HttpRequestException("Simulated Shipment outage.");
            var status = request.RequestUri!.Query.Contains("status=Delayed", StringComparison.Ordinal) ? "Delayed" : "InTransit";
            var items = _items.Where(item => item.Status == status).Select(item => new
            {
                id = item.Id, trackingCode = $"SH-{item.Id:N}"[..12], customerName = "Mercado Central", originName = "Casa Verde",
                originLatitude = (decimal)Casa.Latitude, originLongitude = (decimal)Casa.Longitude,
                destinationLatitude = (decimal)Mercado.Latitude, destinationLongitude = (decimal)Mercado.Longitude,
                vehicleId = Guid.NewGuid(), status = item.Status,
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { items }, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class Factory(RSA rsa, SqliteConnection connection, StubShipments shipments, TimeProvider clock) : WebApplicationFactory<SimulationDbContext>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:SimulationDatabase", "Data Source=ignored");
            builder.UseSetting("Jwt:PublicKeySpkiBase64", Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IDbContextOptionsConfiguration<SimulationDbContext>>();
                services.RemoveAll<DbContextOptions<SimulationDbContext>>();
                services.RemoveAll<SimulationDbContext>();
                services.AddDbContext<SimulationDbContext>(options => options.UseSqlite(connection));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
                services.AddHttpClient("shipment").ConfigurePrimaryHttpMessageHandler(() => new NonDisposing(shipments));
            });
        }

        private sealed class NonDisposing(HttpMessageHandler inner) : DelegatingHandler(inner)
        {
            protected override void Dispose(bool disposing) { }
        }
    }
}
