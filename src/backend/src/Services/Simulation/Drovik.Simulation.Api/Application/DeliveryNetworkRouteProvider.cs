using System.Globalization;
using System.Text.Json;

namespace Drovik.Simulation.Api.Application;

/// <summary>
/// Street routes between the network points, generated once with OSRM from OpenStreetMap data (ODbL) and shipped
/// with the service, so routing needs no external server. Any other pair falls back to the local grid.
/// </summary>
public sealed class DeliveryNetworkRouteProvider(LocalRouteProvider fallback) : IRouteProvider
{
    private const string ResourceName = "Drovik.Simulation.Api.Application.delivery-routes.json";
    private static readonly Lazy<Dictionary<string, double[][]>> Routes = new(Load);

    public static IReadOnlyCollection<string> KnownRoutes => Routes.Value.Keys;

    public IReadOnlyList<RoutePoint> Build(RoutePoint origin, RoutePoint destination)
    {
        if (!Routes.Value.TryGetValue($"{Key(origin)}|{Key(destination)}", out var street)) return fallback.Build(origin, destination);
        // The stored path starts on the nearest road; the exact points join it to the building at each end.
        return [origin, .. street.Select(point => new RoutePoint(point[0], point[1])), destination];
    }

    private static string Key(RoutePoint point) => string.Create(CultureInfo.InvariantCulture, $"{point.Latitude:F4},{point.Longitude:F4}");

    private static Dictionary<string, double[][]> Load()
    {
        using var stream = typeof(DeliveryNetworkRouteProvider).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Falta el recurso {ResourceName}.");
        return JsonSerializer.Deserialize<Dictionary<string, double[][]>>(stream) ?? [];
    }
}
