using Drovik.Simulation.Api.Application;

namespace Drovik.Foundation.Tests;

public sealed class DeliveryNetworkRouteTests
{
    private static readonly RoutePoint Verde = new(4.741, -74.084);
    private static readonly RoutePoint Mercado = new(4.6486, -74.0628);
    private readonly DeliveryNetworkRouteProvider _provider = new(new LocalRouteProvider());

    [Fact]
    public void Every_trip_between_the_eight_points_has_a_street_route() =>
        Assert.Equal(56, DeliveryNetworkRouteProvider.KnownRoutes.Count);

    [Fact]
    public void Street_route_starts_and_ends_at_the_exact_points()
    {
        var route = _provider.Build(Verde, Mercado);

        Assert.Equal(Verde, route[0]);
        Assert.Equal(Mercado, route[^1]);
        Assert.True(route.Count > 50, "A street route has many more points than a grid path.");
    }

    [Fact]
    public void Street_route_stays_close_to_the_operating_city()
    {
        foreach (var point in _provider.Build(Verde, Mercado))
        {
            Assert.InRange(point.Latitude, 4.45, 4.85);
            Assert.InRange(point.Longitude, -74.25, -73.98);
        }
    }

    [Fact]
    public void Unknown_pair_falls_back_to_the_local_grid()
    {
        var far = new RoutePoint(6.25, -75.56);

        Assert.Equal(new LocalRouteProvider().Build(Verde, far), _provider.Build(Verde, far));
    }
}
