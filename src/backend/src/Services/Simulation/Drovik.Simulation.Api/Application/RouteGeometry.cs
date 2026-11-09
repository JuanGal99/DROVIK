using System.Globalization;

namespace Drovik.Simulation.Api.Application;

public readonly record struct RoutePoint(double Latitude, double Longitude);

public interface IRouteProvider
{
    IReadOnlyList<RoutePoint> Build(RoutePoint origin, RoutePoint destination);
}

/// <summary>
/// Offline street-grid path: alternating east-west and north-south legs, like driving through Bogota's blocks.
/// The same two points always give the same path, with no external routing service.
/// </summary>
public sealed class LocalRouteProvider : IRouteProvider
{
    private const int Legs = 8;

    public IReadOnlyList<RoutePoint> Build(RoutePoint origin, RoutePoint destination)
    {
        var random = new Random(StableSeed(origin, destination));
        var points = new List<RoutePoint> { origin };
        var current = origin;
        for (var leg = 1; leg < Legs; leg++)
        {
            var fraction = (leg + (random.NextDouble() - 0.5) * 0.5) / Legs;
            current = leg % 2 == 1
                ? current with { Longitude = origin.Longitude + (destination.Longitude - origin.Longitude) * fraction }
                : current with { Latitude = origin.Latitude + (destination.Latitude - origin.Latitude) * fraction };
            points.Add(current);
        }
        points.Add(current with { Longitude = destination.Longitude });
        points.Add(destination);
        return points;
    }

    // string.GetHashCode is randomized per process, which would change every route after a restart.
    private static int StableSeed(RoutePoint origin, RoutePoint destination)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{origin.Latitude:F5},{origin.Longitude:F5};{destination.Latitude:F5},{destination.Longitude:F5}");
        return text.Aggregate(17, (hash, character) => unchecked(hash * 31 + character));
    }
}

public static class RouteMath
{
    private const double EarthRadiusMeters = 6_371_000;

    public static double Length(IReadOnlyList<RoutePoint> route) =>
        route.Zip(route.Skip(1), Distance).Sum();

    public static RoutePoint PointAt(IReadOnlyList<RoutePoint> route, double meters)
    {
        var remaining = Math.Max(0, meters);
        foreach (var (from, to) in route.Zip(route.Skip(1)))
        {
            var leg = Distance(from, to);
            if (remaining <= leg && leg > 0)
            {
                var share = remaining / leg;
                return new RoutePoint(from.Latitude + (to.Latitude - from.Latitude) * share, from.Longitude + (to.Longitude - from.Longitude) * share);
            }
            remaining -= leg;
        }
        return route[^1];
    }

    private static double Distance(RoutePoint a, RoutePoint b)
    {
        var dLat = double.DegreesToRadians(b.Latitude - a.Latitude);
        var dLng = double.DegreesToRadians(b.Longitude - a.Longitude);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) +
            Math.Cos(double.DegreesToRadians(a.Latitude)) * Math.Cos(double.DegreesToRadians(b.Latitude)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }
}
