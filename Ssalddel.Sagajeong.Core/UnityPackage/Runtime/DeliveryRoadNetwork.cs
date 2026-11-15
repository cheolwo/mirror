using System;
using System.Collections.Generic;
using System.Linq;
namespace Ssalddel.BatchMonitor.Windows
{

internal sealed record DeliveryPoint(double Latitude, double Longitude)
{
    public bool IsValid => double.IsFinite(Latitude) && double.IsFinite(Longitude)
        && Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180;

    public double DistanceTo(DeliveryPoint other)
    {
        var north = (other.Latitude - Latitude) * 111.2;
        var east = (other.Longitude - Longitude) * 111.2 * Math.Cos((Latitude + other.Latitude) * Math.PI / 360);
        return Math.Sqrt(north * north + east * east);
    }
}

internal sealed record DeliveryRoadSample(DeliveryPoint[] Nodes, int[][] Edges);

internal sealed class DeliveryRoadNetwork
{
    private readonly DeliveryPoint[] nodes;
    private readonly List<(int Node, double Km)>[] adjacent;
    private readonly Dictionary<int, (double[] Distances, int[] Previous)> trees = new();

    public DeliveryRoadNetwork(DeliveryRoadSample sample)
    {
        if (sample.Nodes is not { Length: >= 2 } || sample.Nodes.Any(node => node is null || !node.IsValid) ||
            sample.Edges is not { Length: > 0 })
        {
            throw new ArgumentException("Invalid road sample.");
        }

        nodes = sample.Nodes;
        adjacent = nodes.Select(_ => new List<(int, double)>()).ToArray();
        foreach (var edge in sample.Edges)
        {
            if (edge is null || edge.Length != 2 || (uint)edge[0] >= (uint)nodes.Length || (uint)edge[1] >= (uint)nodes.Length)
            {
                throw new ArgumentException("Invalid road edge.");
            }

            var a = edge[0];
            var b = edge[1];
            var km = nodes[a].DistanceTo(nodes[b]);
            if (km > 0)
            {
                adjacent[a].Add((b, km));
                adjacent[b].Add((a, km));
            }
        }
    }

    public DeliveryPoint this[int node] => nodes[node];
    public DeliveryPoint[] Coverage =>
    new DeliveryPoint[] {
        new(nodes.Min(node => node.Latitude), nodes.Min(node => node.Longitude)),
        new(nodes.Max(node => node.Latitude), nodes.Max(node => node.Longitude))
    };

    public int Closest(DeliveryPoint point) =>
        Enumerable.Range(0, nodes.Length).OrderBy(node => point.DistanceTo(nodes[node])).First();

    public int[] NearbyDestinations(int origin)
    {
        var (distances, _) = Tree(origin);
        return Enumerable.Range(0, nodes.Length)
            .Where(node => distances[node] is >= 0.26 and <= 0.95)
            .OrderBy(node => distances[node]).ToArray();
    }

    public DeliveryPath? FindPath(int from, int to)
    {
        var (distances, previous) = Tree(from);
        if (!double.IsFinite(distances[to]))
        {
            return null;
        }

        var path = new List<DeliveryPoint> { nodes[to] };
        for (var node = to; node != from;)
        {
            node = previous[node];
            path.Add(nodes[node]);
        }

        path.Reverse();
        return new(path.ToArray());
    }

    private (double[] Distances, int[] Previous) Tree(int from)
    {
        if (trees.TryGetValue(from, out var cached))
        {
            return cached;
        }

        var distances = Enumerable.Repeat(double.PositiveInfinity, nodes.Length).ToArray();
        var previous = Enumerable.Repeat(-1, nodes.Length).ToArray();
        var queue = new RoadNodePriorityQueue();
        distances[from] = 0;
        queue.Enqueue(from, 0);
        while (queue.TryDequeue(out var node, out var distance))
        {
            if (distance > distances[node])
            {
                continue;
            }

            foreach (var (next, km) in adjacent[node])
            {
                var proposed = distance + km;
                if (proposed < distances[next])
                {
                    distances[next] = proposed;
                    previous[next] = node;
                    queue.Enqueue(next, proposed);
                }
            }
        }

        return trees[from] = (distances, previous);
    }
}

internal sealed class DeliveryPath
{
    private readonly double[] ends;
    public DeliveryPoint[] Points { get; }
    public DeliveryPath(DeliveryPoint[] points) { Points = points; ends = CumulativeDistances(points); }
    public double Kilometers => ends[^1];

    public DeliveryPoint Position(double fraction)
    {
        var km = Math.Clamp(fraction, 0, 1) * Kilometers;
        var end = Array.BinarySearch(ends, km);
        if (end >= 0)
        {
            return Points[end];
        }

        end = ~end;
        var weight = (km - ends[end - 1]) / (ends[end] - ends[end - 1]);
        var a = Points[end - 1];
        var b = Points[end];
        return new(a.Latitude + (b.Latitude - a.Latitude) * weight, a.Longitude + (b.Longitude - a.Longitude) * weight);
    }

    private static double[] CumulativeDistances(DeliveryPoint[] points)
    {
        var result = new double[points.Length];
        for (var index = 1; index < points.Length; index++)
        {
            result[index] = result[index - 1] + points[index - 1].DistanceTo(points[index]);
        }

        return result;
    }
}

}
