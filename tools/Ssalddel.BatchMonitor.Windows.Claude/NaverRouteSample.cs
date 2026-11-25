namespace Ssalddel.BatchMonitor.Windows;

internal static class NaverRouteSample
{
    // Road sample from OSM ways 218790209, 1112326984, 1112326970, 576294786 and 1256772587.
    // © OpenStreetMap contributors, ODbL. https://www.openstreetmap.org/copyright
    private static readonly MapPoint[] Road =
    [
        new(37.5806574, 127.0884624), new(37.5806203, 127.0887156), new(37.5806079, 127.0887824),
        new(37.5805536, 127.0890751), new(37.5804713, 127.0895293), new(37.5806281, 127.0895697),
        new(37.5811216, 127.0897142), new(37.5814033, 127.0897487), new(37.5814289, 127.0895023),
        new(37.5814288, 127.0894970), new(37.5814277, 127.0894932)
    ];
    private const int StopCount = 12;
    private static readonly double[] RoadDistances = Distances(Road);

    internal static Snapshot Read(BatchSimulation simulation) => new(
        simulation.Orders.Select(order =>
        {
            var point = AtDistance(Road, RoadDistances, StopDistance(order.Number));
            return new Order(order.Number, order.Phase.ToString(), point.Latitude, point.Longitude);
        }).ToArray(),
        simulation.Batches.Select(batch => BuildBatch(batch, simulation.Elapsed)).ToArray(),
        Road[0]);

    private static Batch BuildBatch(BatchMonitorBatch batch, TimeSpan elapsed)
    {
        var path = new List<MapPoint> { Road[0] };
        double lastStop = 0;
        foreach (var number in batch.OrderNumbers)
        {
            var stop = StopDistance(number);
            if (lastStop < stop)
            {
                for (var index = 1; index < Road.Length; index++)
                {
                    if (RoadDistances[index] > lastStop && RoadDistances[index] < stop)
                    {
                        path.Add(Road[index]);
                    }
                }
            }
            else
            {
                for (var index = Road.Length - 2; index > 0; index--)
                {
                    if (RoadDistances[index] < lastStop && RoadDistances[index] > stop)
                    {
                        path.Add(Road[index]);
                    }
                }
            }

            path.Add(AtDistance(Road, RoadDistances, stop));
            lastStop = stop;
        }

        MapPoint? vehicle = null;
        if (batch.Phase == BatchMonitorPhase.InTransit)
        {
            var distances = Distances(path);
            var fraction = Math.Clamp((elapsed - batch.CreatedAt - BatchSimulation.TransitAge) /
                (BatchSimulation.CompletionAge - BatchSimulation.TransitAge), 0, 1);
            vehicle = AtDistance(path, distances, distances[^1] * fraction);
        }

        return new(batch.Number, batch.Phase.ToString(), path, vehicle);
    }

    private static double StopDistance(long number) => RoadDistances[^1] * ((number - 1) % StopCount + 1) / StopCount;

    private static double[] Distances(IReadOnlyList<MapPoint> path)
    {
        var distances = new double[path.Count];
        for (var index = 1; index < path.Count; index++)
        {
            var lat = path[index].Latitude - path[index - 1].Latitude;
            var lon = (path[index].Longitude - path[index - 1].Longitude) * .792;
            distances[index] = distances[index - 1] + Math.Sqrt(lat * lat + lon * lon);
        }
        return distances;
    }

    private static MapPoint AtDistance(IReadOnlyList<MapPoint> points, double[] distances, double distance)
    {
        for (var index = 1; index < points.Count; index++)
        {
            if (distance <= distances[index])
            {
                var start = points[index - 1];
                var end = points[index];
                var length = distances[index] - distances[index - 1];
                var fraction = length == 0 ? 0 : (distance - distances[index - 1]) / length;
                return new(start.Latitude + fraction * (end.Latitude - start.Latitude),
                    start.Longitude + fraction * (end.Longitude - start.Longitude));
            }
        }
        return points[^1];
    }

    internal readonly record struct MapPoint(double Latitude, double Longitude);
    internal sealed record Order(long Number, string Phase, double Latitude, double Longitude);
    internal sealed record Batch(long Number, string Phase, IReadOnlyList<MapPoint> Points, MapPoint? Vehicle);
    internal sealed record Snapshot(IReadOnlyList<Order> Orders, IReadOnlyList<Batch> Batches, MapPoint Depot);
}
