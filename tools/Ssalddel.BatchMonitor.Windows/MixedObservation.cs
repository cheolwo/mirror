namespace Ssalddel.BatchMonitor.Windows;

internal sealed class MixedObservation(DeliverySimulation delivery, FreightReplay? freight)
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(250);
    private const int MaxStepsPerUpdate = 40;
    private TimeSpan nextStep;

    public DeliverySimulation Delivery { get; } = delivery;
    public FreightReplay? Freight { get; } = freight;

    public static MixedObservation? Load(string directory)
    {
        var food = DeliverySimulation.Load(directory);
        return food is null ? null : new(food, FreightReplay.TryCreate(food.RoadNetwork, food.DistrictStops));
    }

    public void Tick(TimeSpan elapsed)
    {
        // Use the same discrete clock for both lanes. Catch up in bounded slices
        // after the UI is suspended so the next render can still run.
        for (var steps = 0; steps < MaxStepsPerUpdate && nextStep <= elapsed; steps++)
        {
            Delivery.Tick(nextStep);
            Freight?.Tick(nextStep);
            nextStep += Step;
        }
    }

    public DeliveryMapSnapshot MapSnapshot() => Delivery.MapSnapshot() with { Freight = Freight?.MapSnapshot() };

    public IEnumerable<ObservationEvent> LatestEvents(int count) => Delivery.Events
        .Concat(Freight?.Events ?? [])
        .OrderByDescending(entry => entry.At)
        .ThenBy(entry => entry.Kind, StringComparer.Ordinal)
        .Take(count);
}

