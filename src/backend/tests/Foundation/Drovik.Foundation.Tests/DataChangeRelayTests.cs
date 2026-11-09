using Drovik.Messaging;
using Drovik.Simulation.Api.Application;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Drovik.Foundation.Tests;

public sealed class DataChangeRelayTests
{
    private readonly RecordingHub _hub = new();

    private DataChangeRelay Relay() => new(_hub, Options.Create(new RabbitMqOptions()), NullLogger<DataChangeRelay>.Instance);

    [Theory]
    [InlineData(DomainEvents.ShipmentStatusChanged, "shipments")]
    [InlineData(DomainEvents.ShipmentCreated, "shipments")]
    [InlineData(DomainEvents.VehicleAvailabilityChanged, "fleet")]
    [InlineData(DomainEvents.DriverAvailabilityChanged, "fleet")]
    [InlineData(DomainEvents.VehicleDetailsChanged, "fleet")]
    [InlineData(DomainEvents.DriverDetailsChanged, "fleet")]
    [InlineData(DomainEvents.IncidentCreated, "incidents")]
    [InlineData(DomainEvents.IncidentResolved, "incidents")]
    public async Task Tells_screens_which_data_changed(string eventType, string area)
    {
        await Relay().NotifyAsync(DomainEvents.RoutingKey(eventType), CancellationToken.None);

        var (method, arguments) = Assert.Single(_hub.Sent);
        Assert.Equal(DataChangeRelay.HubMessage, method);
        Assert.Equal(area, Assert.Single(arguments));
    }

    // These happen before the owning service applied them; reading now would show the old data.
    [Theory]
    [InlineData(DomainEvents.VehicleBreakdownDetected)]
    [InlineData(DomainEvents.RouteDelayDetected)]
    [InlineData(DomainEvents.ShipmentRouteCompleted)]
    public async Task Stays_quiet_for_events_the_owner_has_not_applied_yet(string eventType)
    {
        await Relay().NotifyAsync(DomainEvents.RoutingKey(eventType), CancellationToken.None);

        Assert.Empty(_hub.Sent);
    }

    private sealed class RecordingHub : IHubContext<OperationsHub>, IHubClients, IClientProxy
    {
        public List<(string Method, object?[] Arguments)> Sent { get; } = [];
        public IHubClients Clients => this;
        public IGroupManager Groups => throw new NotSupportedException();
        public IClientProxy All => this;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => this;
        public IClientProxy Client(string connectionId) => this;
        IClientProxy IHubClients<IClientProxy>.Clients(IReadOnlyList<string> connectionIds) => this;
        public IClientProxy Group(string groupName) => this;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => this;
        IClientProxy IHubClients<IClientProxy>.Groups(IReadOnlyList<string> groupNames) => this;
        public IClientProxy User(string userId) => this;
        public IClientProxy Users(IReadOnlyList<string> userIds) => this;

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Sent.Add((method, args));
            return Task.CompletedTask;
        }
    }
}
