using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Drovik.Fleet.Api.Application;
using Drovik.Fleet.Api.Domain;
using Drovik.Fleet.Api.Messaging;
using Drovik.Fleet.Api.Persistence;
using Drovik.Incident.Api.Persistence;
using Drovik.Messaging;
using Drovik.Shipment.Api.Messaging;
using Drovik.Shipment.Api.Persistence;
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

/// <summary>
/// HTTP-level tests for Fleet, Shipment and Incident. Each service runs in memory over its own SQLite
/// database; Shipment reaches Fleet through the real Fleet endpoints, so the reservation contract is exercised.
/// </summary>
public sealed class DomainApiTests : IAsyncLifetime
{
    private static readonly Guid BogotaCenter = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PuenteArandaCenter = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid SeededCustomer = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid OriginPoint = Guid.Parse("40000000-0000-0000-0000-000000000003");
    private static readonly Guid OtherCustomer = Guid.Parse("40000000-0000-0000-0000-000000000002");
    private static readonly Guid TruckId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid DriverId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly SqliteConnection _fleetConnection = new("Data Source=:memory:");
    private readonly SqliteConnection _shipmentConnection = new("Data Source=:memory:");
    private readonly SqliteConnection _incidentConnection = new("Data Source=:memory:");
    private readonly SwitchableHandler _fleetLink = new();
    private ServiceFactory<FleetDbContext> _fleet = null!;
    private ServiceFactory<ShipmentDbContext> _shipment = null!;
    private ServiceFactory<IncidentDbContext> _incident = null!;
    private HttpClient _fleetClient = null!;
    private HttpClient _shipmentClient = null!;
    private HttpClient _incidentClient = null!;

    public async Task InitializeAsync()
    {
        await _fleetConnection.OpenAsync();
        await _shipmentConnection.OpenAsync();
        await _incidentConnection.OpenAsync();
        _fleet = new ServiceFactory<FleetDbContext>(_rsa, _fleetConnection, "FleetDatabase");
        _shipment = new ServiceFactory<ShipmentDbContext>(_rsa, _shipmentConnection, "ShipmentDatabase", _fleetLink);
        _incident = new ServiceFactory<IncidentDbContext>(_rsa, _incidentConnection, "IncidentDatabase");
        _fleetClient = Authenticated(_fleet.CreateClient());
        _shipmentClient = Authenticated(_shipment.CreateClient());
        _incidentClient = Authenticated(_incident.CreateClient());
        _fleetLink.Inner = _fleet.Server.CreateHandler();
        await EnsureCreated(_fleet);
        await EnsureCreated(_shipment);
        await EnsureCreated(_incident);
    }

    public async Task DisposeAsync()
    {
        _fleetClient.Dispose();
        _shipmentClient.Dispose();
        _incidentClient.Dispose();
        await _shipment.DisposeAsync();
        await _incident.DisposeAsync();
        await _fleet.DisposeAsync();
        await _fleetConnection.DisposeAsync();
        await _shipmentConnection.DisposeAsync();
        await _incidentConnection.DisposeAsync();
        _rsa.Dispose();
    }

    [Fact]
    public async Task Incident_contract_uses_string_enums_like_the_angular_client()
    {
        using var created = await _incidentClient.PostAsJsonAsync("/api/v1/incidents",
            new { type = "Delay", severity = "High", description = "Retraso en ruta" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ReadJson(created);
        Assert.Equal("Delay", body.GetProperty("type").GetString());
        Assert.Equal("High", body.GetProperty("severity").GetString());
        Assert.Equal("Open", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Incident_list_paging_parameters_are_optional()
    {
        using var response = await _incidentClient.GetAsync("/api/v1/incidents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(20, (await ReadJson(response)).GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Incident_status_note_over_the_column_limit_is_a_validation_error()
    {
        var id = await CreateIncident();

        using var response = await _incidentClient.PatchAsJsonAsync($"/api/v1/incidents/{id}/status",
            new { status = "InProgress", note = new string('x', 1001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Incident_full_lifecycle_records_activity()
    {
        var id = await CreateIncident();

        using var started = await _incidentClient.PatchAsJsonAsync($"/api/v1/incidents/{id}/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        using var resolved = await _incidentClient.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new { resolutionNote = "Atendido" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal(3, (await ReadJson(resolved)).GetProperty("activities").GetArrayLength());

        using var again = await _incidentClient.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new { resolutionNote = "Otra vez" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Domain_routes_require_a_token_and_an_operational_role()
    {
        using var anonymous = _incident.CreateClient();
        using var noToken = await anonymous.GetAsync("/api/v1/incidents");
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("Guest"));
        using var wrongRole = await anonymous.GetAsync("/api/v1/incidents");
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }

    [Fact]
    public async Task Fleet_update_returns_the_new_distribution_center()
    {
        using var response = await _fleetClient.PutAsJsonAsync($"/api/v1/vehicles/{TruckId}", new
        {
            plate = "TRK-018", model = "Hino 500", type = "Camión", capacityKg = 8000, currentOdometer = 128430,
            distributionCenterId = PuenteArandaCenter,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJson(response);
        Assert.Equal(PuenteArandaCenter, body.GetProperty("distributionCenterId").GetGuid());
        Assert.Equal("Bogotá · Puente Aranda", body.GetProperty("center").GetString());
    }

    [Fact]
    public async Task Fleet_rejects_manual_status_change_while_a_reservation_is_active()
    {
        var shipmentId = Guid.NewGuid();
        using var reserved = await Reserve(shipmentId, "manual-status-key");
        Assert.Equal(HttpStatusCode.Created, reserved.StatusCode);

        using var patch = await _fleetClient.PatchAsJsonAsync($"/api/v1/vehicles/{TruckId}/status", new { status = "Available" });
        Assert.Equal(HttpStatusCode.Conflict, patch.StatusCode);
        using var delete = await _fleetClient.DeleteAsync($"/api/v1/drivers/{DriverId}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        using var second = await Reserve(Guid.NewGuid(), "second-reservation");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Fleet_release_does_not_overwrite_a_vehicle_taken_out_of_service()
    {
        var reservationId = await ReserveAndGetId(Guid.NewGuid(), "maintenance-key");
        await using (var scope = _fleet.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            var vehicle = await database.Vehicles.SingleAsync(item => item.Id == TruckId);
            vehicle.Status = VehicleStatus.Maintenance;
            await database.SaveChangesAsync();
        }

        using var released = await _fleetClient.PostAsync($"/api/v1/reservations/{reservationId}/release", null);

        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        Assert.Equal("Maintenance", await VehicleStatusOf(TruckId));
    }

    [Fact]
    public async Task Shipment_assign_without_idempotency_key_is_a_validation_error()
    {
        var id = await CreateShipment();

        using var response = await _shipmentClient.PostAsJsonAsync($"/api/v1/shipments/{id}/assign", new { vehicleId = TruckId, driverId = DriverId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Shipment_delivery_flow_reserves_and_releases_fleet_resources()
    {
        var id = await CreateShipment();

        using var assigned = await Assign(id, "delivery-flow-key");
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.Equal("Assigned", await VehicleStatusOf(TruckId));

        using var duplicate = await Assign(id, "delivery-flow-key-2");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.False(string.IsNullOrEmpty((await ReadJson(duplicate)).GetProperty("detail").GetString()));

        using var inTransit = await ChangeStatus(id, "InTransit");
        Assert.Equal(HttpStatusCode.OK, inTransit.StatusCode);
        using var delivered = await ChangeStatus(id, "Delivered");
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.Equal("Available", await VehicleStatusOf(TruckId));
    }

    [Fact]
    public async Task Shipment_assign_reports_503_when_fleet_is_unreachable()
    {
        var id = await CreateShipment();
        _fleetLink.Fail = true;

        using var response = await Assign(id, "fleet-down-key");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Customer_catalog_has_the_eight_delivery_points_and_is_searchable()
    {
        using var page = await _shipmentClient.GetAsync("/api/v1/customers?pageSize=2");
        var body = await ReadJson(page);
        Assert.Equal(8, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());

        using var found = await _shipmentClient.GetAsync("/api/v1/customers?search=Usaqu%C3%A9n");
        Assert.Equal("Farmacia Norte", (await ReadJson(found)).GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Every_delivery_point_is_inside_the_operating_city()
    {
        using var response = await _shipmentClient.GetAsync("/api/v1/customers?pageSize=100");

        foreach (var customer in (await ReadJson(response)).GetProperty("items").EnumerateArray())
        {
            Assert.InRange(customer.GetProperty("latitude").GetDecimal(), 4.45m, 4.85m);
            Assert.InRange(customer.GetProperty("longitude").GetDecimal(), -74.25m, -73.98m);
        }
    }

    [Fact]
    public async Task Customer_catalog_is_read_only_and_requires_an_operational_role()
    {
        using var write = await _shipmentClient.PostAsJsonAsync("/api/v1/customers", new { name = "Otro" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, write.StatusCode);

        using var client = _shipment.CreateClient();
        using var anonymous = await client.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("Guest"));
        using var wrongRole = await client.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }

    [Fact]
    public async Task Shipment_takes_its_destination_from_the_catalog_and_ignores_a_typed_one()
    {
        using var response = await _shipmentClient.PostAsJsonAsync("/api/v1/shipments", new
        {
            customerId = SeededCustomer, originCustomerId = OriginPoint, weightKg = 100, priority = "High",
            destination = "Cualquier ciudad", destinationLatitude = 10m, destinationLongitude = -70m,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJson(response);
        Assert.Equal("Chapinero", body.GetProperty("destination").GetString());
        Assert.Equal(4.6486m, body.GetProperty("destinationLatitude").GetDecimal());
        Assert.Equal(-74.0628m, body.GetProperty("destinationLongitude").GetDecimal());
        Assert.Equal("Distribuidora Sabana", body.GetProperty("originName").GetString());
        Assert.Equal(4.7056m, body.GetProperty("originLatitude").GetDecimal());
    }

    [Fact]
    public async Task Shipment_cannot_start_and_end_at_the_same_point()
    {
        using var response = await PostShipment(OriginPoint);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Shipment_cannot_start_outside_the_catalog()
    {
        using var response = await _shipmentClient.PostAsJsonAsync("/api/v1/shipments", new { customerId = SeededCustomer, originCustomerId = BogotaCenter, weightKg = 100, priority = "High" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Shipment_without_a_catalog_origin_cannot_be_assigned()
    {
        var id = await CreateShipment();
        await using (var scope = _shipment.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
            (await database.Shipments.SingleAsync(item => item.Id == id)).OriginCustomerId = null;
            await database.SaveChangesAsync();
        }

        using var response = await Assign(id, "no-origin-key");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Shipment_outside_the_catalog_is_rejected()
    {
        using var response = await PostShipment(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Shipment_rejects_a_delivery_point_taken_out_of_the_catalog()
    {
        await using (var scope = _shipment.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
            (await database.Customers.SingleAsync(item => item.Id == SeededCustomer)).IsActive = false;
            await database.SaveChangesAsync();
        }

        using var response = await PostShipment(SeededCustomer);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editing_a_pending_shipment_moves_it_to_another_catalog_point()
    {
        var id = await CreateShipment();

        using var edited = await _shipmentClient.PutAsJsonAsync($"/api/v1/shipments/{id}", new { originCustomerId = OriginPoint, customerId = OtherCustomer, weightKg = 50, priority = "Normal" });

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var body = await ReadJson(edited);
        Assert.Equal("Farmacia Norte", body.GetProperty("customerName").GetString());
        Assert.Equal("Usaquén", body.GetProperty("destination").GetString());
    }

    [Fact]
    public async Task Shipment_response_names_its_customer_and_search_finds_it()
    {
        await CreateShipment();

        using var response = await _shipmentClient.GetAsync("/api/v1/shipments?search=Mercado");
        var page = await ReadJson(response);

        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal("Mercado Central", page.GetProperty("items")[0].GetProperty("customerName").GetString());
    }

    [Fact]
    public async Task Finished_route_delivers_the_shipment_once_and_frees_fleet()
    {
        var id = await CreateShipment();
        using (await Assign(id, "route-done-key")) { }
        using (await ChangeStatus(id, "InTransit")) { }
        var body = EventEnvelopeSerializer.Serialize(OutboxMessageFactory.Create(DomainEvents.ShipmentRouteCompleted, new ShipmentRouteCompletedV1(id, Guid.NewGuid())));
        var consumer = _shipment.Services.GetRequiredService<ShipmentEventConsumer>();

        Assert.Equal(ConsumeOutcome.Ack, await consumer.ProcessAsync(body, redelivered: false, CancellationToken.None));
        Assert.Equal(ConsumeOutcome.Ack, await consumer.ProcessAsync(body, redelivered: true, CancellationToken.None));

        using var current = await _shipmentClient.GetAsync($"/api/v1/shipments/{id}");
        var shipment = await ReadJson(current);
        Assert.Equal("Delivered", shipment.GetProperty("status").GetString());
        Assert.Equal("RouteCompleted", shipment.GetProperty("statusHistory")[0].GetProperty("source").GetString());
        await DeliverShipmentEventsToFleet();
        Assert.Equal("Available", await VehicleStatusOf(TruckId));
    }

    [Fact]
    public async Task Finished_route_does_not_deliver_a_shipment_that_never_left()
    {
        var id = await CreateShipment();
        var body = EventEnvelopeSerializer.Serialize(OutboxMessageFactory.Create(DomainEvents.ShipmentRouteCompleted, new ShipmentRouteCompletedV1(id, Guid.NewGuid())));

        await _shipment.Services.GetRequiredService<ShipmentEventConsumer>().ProcessAsync(body, redelivered: false, CancellationToken.None);

        using var current = await _shipmentClient.GetAsync($"/api/v1/shipments/{id}");
        Assert.Equal("Pending", (await ReadJson(current)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Delivery_is_durable_when_fleet_is_down_and_the_event_frees_the_resources()
    {
        var id = await CreateShipment();
        using (await Assign(id, "durable-release-key")) { }
        using (await ChangeStatus(id, "InTransit")) { }
        _fleetLink.Fail = true;

        using var delivered = await ChangeStatus(id, "Delivered");

        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.Equal("Assigned", await VehicleStatusOf(TruckId));
        await DeliverShipmentEventsToFleet();
        Assert.Equal("Available", await VehicleStatusOf(TruckId));
    }

    [Fact]
    public async Task Assignment_survives_a_failed_confirmation_and_the_event_confirms_it()
    {
        var id = await CreateShipment();
        _fleetLink.FailPathSuffix = "/confirm";

        using var response = await Assign(id, "confirm-fails-key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReservationStatus.Reserved, await ReservationStatusOf(id));
        await DeliverShipmentEventsToFleet();
        Assert.Equal(ReservationStatus.Confirmed, await ReservationStatusOf(id));
    }

    [Fact]
    public async Task Retrying_an_assignment_whose_response_was_lost_returns_the_same_assignment()
    {
        var id = await CreateShipment();
        using (await Assign(id, "lost-response-key")) { }

        using var retried = await Assign(id, "lost-response-key");

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("Assigned", (await ReadJson(retried)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Vehicle_is_in_transit_once_the_shipment_event_arrives()
    {
        var id = await CreateShipment();
        using (await Assign(id, "in-transit-key")) { }
        using (await ChangeStatus(id, "InTransit")) { }

        await DeliverShipmentEventsToFleet();

        Assert.Equal("InTransit", await VehicleStatusOf(TruckId));
    }

    [Fact]
    public async Task Replaying_shipment_events_does_not_repeat_their_effects()
    {
        var id = await CreateShipment();
        using (await Assign(id, "replay-key")) { }
        using (await ChangeStatus(id, "InTransit")) { }
        await DeliverShipmentEventsToFleet();
        var published = await CountFleetOutbox(DomainEvents.VehicleAvailabilityChanged);

        await DeliverShipmentEventsToFleet();

        Assert.Equal(published, await CountFleetOutbox(DomainEvents.VehicleAvailabilityChanged));
    }

    [Fact]
    public async Task Late_events_do_not_revive_a_reservation_that_already_ended()
    {
        var id = await CreateShipment();
        _fleetLink.FailPathSuffix = "/confirm";
        using (await Assign(id, "late-event-key")) { }
        using (await ChangeStatus(id, "Cancelled")) { }

        await DeliverShipmentEventsToFleet();

        Assert.Equal(ReservationStatus.Released, await ReservationStatusOf(id));
        Assert.Equal("Available", await VehicleStatusOf(TruckId));
    }

    [Fact]
    public async Task Expired_reservation_frees_resources_and_undoes_the_unconfirmed_assignment()
    {
        var id = await CreateShipment();
        _fleetLink.FailPathSuffix = "/confirm";
        using (await Assign(id, "expiry-key")) { }
        await ExpireReservationOf(id);

        Assert.Equal(1, await _fleet.Services.GetRequiredService<ReservationExpiryWorker>().SweepAsync(CancellationToken.None));

        Assert.Equal(ReservationStatus.Expired, await ReservationStatusOf(id));
        Assert.Equal("Available", await VehicleStatusOf(TruckId));
        await DeliverFleetExpiriesToShipment();
        using var current = await _shipmentClient.GetAsync($"/api/v1/shipments/{id}");
        var body = await ReadJson(current);
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("vehicleId").ValueKind);
        Assert.Equal("ReservationExpired", body.GetProperty("statusHistory")[0].GetProperty("source").GetString());
    }

    [Fact]
    public async Task Assignment_event_arriving_after_the_ttl_expires_the_reservation_instead_of_confirming_it()
    {
        var id = await CreateShipment();
        _fleetLink.FailPathSuffix = "/confirm";
        using (await Assign(id, "late-confirm-key")) { }
        await ExpireReservationOf(id);

        await DeliverShipmentEventsToFleet();

        Assert.Equal(ReservationStatus.Expired, await ReservationStatusOf(id));
        Assert.Equal(1, await CountFleetOutbox(DomainEvents.ReservationExpired));
    }

    [Fact]
    public async Task Sweep_leaves_confirmed_reservations_alone()
    {
        var id = await CreateShipment();
        using (await Assign(id, "confirmed-key")) { }
        await ExpireReservationOf(id);

        Assert.Equal(0, await _fleet.Services.GetRequiredService<ReservationExpiryWorker>().SweepAsync(CancellationToken.None));

        Assert.Equal(ReservationStatus.Confirmed, await ReservationStatusOf(id));
    }

    [Fact]
    public async Task Idempotency_key_of_a_released_reservation_cannot_be_reused()
    {
        var shipmentId = Guid.NewGuid();
        var reservationId = await ReserveAndGetId(shipmentId, "released-key");
        using (await _fleetClient.PostAsync($"/api/v1/reservations/{reservationId}/release", null)) { }

        using var retried = await Reserve(shipmentId, "released-key");

        Assert.Equal(HttpStatusCode.Conflict, retried.StatusCode);
    }

    [Fact]
    public async Task Event_for_an_unknown_reservation_is_dead_lettered()
    {
        var message = OutboxMessageFactory.Create(DomainEvents.ShipmentAssigned,
            new ShipmentAssignedV1(Guid.NewGuid(), TruckId, DriverId, Guid.NewGuid()));

        var outcome = await _fleet.Services.GetRequiredService<FleetEventConsumer>()
            .ProcessAsync(EventEnvelopeSerializer.Serialize(message), redelivered: false, CancellationToken.None);

        Assert.Equal(ConsumeOutcome.DeadLetter, outcome);
    }

    [Fact]
    public async Task Shipment_status_note_over_the_column_limit_is_a_validation_error()
    {
        var id = await CreateShipment();

        using var response = await ChangeStatus(id, "Cancelled", new string('x', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient Authenticated(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("Operator"));
        return client;
    }

    private string CreateToken(string role)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken("DROVIK.Identity", "DROVIK.Services",
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", role)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(15), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task EnsureCreated<TContext>(ServiceFactory<TContext> factory) where TContext : DbContext
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreatedAsync();
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<Guid> CreateIncident()
    {
        using var response = await _incidentClient.PostAsJsonAsync("/api/v1/incidents",
            new { type = "Delay", severity = "High", description = "Retraso en ruta" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJson(response)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PostShipment(Guid customerId) =>
        _shipmentClient.PostAsJsonAsync("/api/v1/shipments", new { customerId, originCustomerId = OriginPoint, weightKg = 100, priority = "High" });

    private async Task<Guid> CreateShipment()
    {
        using var response = await PostShipment(SeededCustomer);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJson(response)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> Assign(Guid id, string key) =>
        _shipmentClient.PostAsJsonAsync($"/api/v1/shipments/{id}/assign", new { vehicleId = TruckId, driverId = DriverId, idempotencyKey = key });

    private Task<HttpResponseMessage> ChangeStatus(Guid id, string status, string? note = null) =>
        _shipmentClient.PostAsJsonAsync($"/api/v1/shipments/{id}/status", new { status, note });

    private Task<HttpResponseMessage> Reserve(Guid shipmentId, string key) =>
        _fleetClient.PostAsJsonAsync("/api/v1/reservations", new { shipmentId, vehicleId = TruckId, driverId = DriverId, weightKg = 100, idempotencyKey = key });

    private async Task<Guid> ReserveAndGetId(Guid shipmentId, string key)
    {
        using var response = await Reserve(shipmentId, key);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJson(response)).GetProperty("reservationId").GetGuid();
    }

    /// <summary>Feeds the rows Shipment really wrote to its Outbox through the real Fleet consumer, in order.</summary>
    private async Task DeliverShipmentEventsToFleet()
    {
        string[] subscribed = [DomainEvents.ShipmentAssigned, DomainEvents.ShipmentStatusChanged, DomainEvents.ShipmentDelivered, DomainEvents.ShipmentCancelled];
        await using var scope = _shipment.Services.CreateAsyncScope();
        var events = await scope.ServiceProvider.GetRequiredService<ShipmentDbContext>().Outbox.AsNoTracking()
            .Where(item => subscribed.Contains(item.EventType)).OrderBy(item => item.OccurredAtUtc).ToListAsync();
        var consumer = _fleet.Services.GetRequiredService<FleetEventConsumer>();
        foreach (var message in events)
            Assert.Equal(ConsumeOutcome.Ack, await consumer.ProcessAsync(EventEnvelopeSerializer.Serialize(message), redelivered: false, CancellationToken.None));
    }

    private async Task DeliverFleetExpiriesToShipment()
    {
        await using var scope = _fleet.Services.CreateAsyncScope();
        var events = await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Outbox.AsNoTracking()
            .Where(item => item.EventType == DomainEvents.ReservationExpired).ToListAsync();
        var consumer = _shipment.Services.GetRequiredService<ShipmentEventConsumer>();
        foreach (var message in events)
            Assert.Equal(ConsumeOutcome.Ack, await consumer.ProcessAsync(EventEnvelopeSerializer.Serialize(message), redelivered: false, CancellationToken.None));
    }

    private async Task ExpireReservationOf(Guid shipmentId)
    {
        await using var scope = _fleet.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        var reservation = await database.Reservations.SingleAsync(item => item.ShipmentId == shipmentId);
        reservation.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await database.SaveChangesAsync();
    }

    private async Task<ReservationStatus> ReservationStatusOf(Guid shipmentId)
    {
        await using var scope = _fleet.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Reservations.AsNoTracking()
            .Where(item => item.ShipmentId == shipmentId).Select(item => item.Status).SingleAsync();
    }

    private async Task<int> CountFleetOutbox(string eventType)
    {
        await using var scope = _fleet.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Outbox.CountAsync(item => item.EventType == eventType);
    }

    private async Task<string?> VehicleStatusOf(Guid id)
    {
        using var response = await _fleetClient.GetAsync($"/api/v1/vehicles/{id}");
        return (await ReadJson(response)).GetProperty("status").GetString();
    }

    private sealed class ServiceFactory<TContext>(RSA rsa, SqliteConnection connection, string connectionName, HttpMessageHandler? fleetLink = null)
        : WebApplicationFactory<TContext> where TContext : DbContext
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            // UseSetting reaches the top-level statements of Program.cs, which read configuration before Build().
            builder.UseSetting($"ConnectionStrings:{connectionName}", "Data Source=ignored");
            builder.UseSetting("Jwt:PublicKeySpkiBase64", Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();
                services.RemoveAll<DbContextOptions<TContext>>();
                services.RemoveAll<TContext>();
                services.AddDbContext<TContext>(options => options.UseSqlite(connection));
                if (fleetLink is not null)
                    services.AddHttpClient("fleet").ConfigurePrimaryHttpMessageHandler(() => new NonDisposingHandler(fleetLink));
            });
        }
    }

    /// <summary>Forwards to the in-memory Fleet server and can simulate an outage, entire or per route.</summary>
    private sealed class SwitchableHandler : HttpMessageHandler
    {
        public HttpMessageHandler? Inner { get; set; }
        public bool Fail { get; set; }
        public string? FailPathSuffix { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail || (FailPathSuffix is not null && request.RequestUri!.AbsolutePath.EndsWith(FailPathSuffix, StringComparison.Ordinal)))
                throw new HttpRequestException("Simulated Fleet outage.");
            return new HttpMessageInvoker(Inner!, disposeHandler: false).SendAsync(request, cancellationToken);
        }
    }

    private sealed class NonDisposingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override void Dispose(bool disposing) { }
    }
}
