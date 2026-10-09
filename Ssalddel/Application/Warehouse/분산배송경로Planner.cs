using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Services.Dispatch.Recommendation;

namespace Ssalddel.Application.Warehouse;

public sealed record 분산경로적재량(decimal WeightKg, decimal VolumeCbm, decimal Pallets)
{
    public static 분산경로적재량 operator +(분산경로적재량 a, 분산경로적재량 b) => new(a.WeightKg + b.WeightKg, a.VolumeCbm + b.VolumeCbm, a.Pallets + b.Pallets);
    public static 분산경로적재량 operator -(분산경로적재량 a, 분산경로적재량 b) => new(a.WeightKg - b.WeightKg, a.VolumeCbm - b.VolumeCbm, a.Pallets - b.Pallets);
}
public sealed record 분산경로Stop(string StopId, string Kind, string? SiteId, IReadOnlyList<string> OrderIds,
    IReadOnlyList<string> ReservationIds, string Address, 배차경로좌표 Point, DateTime StartUtc, DateTime EndUtc,
    decimal ServiceMinutes, IReadOnlyDictionary<string, 분산경로적재량> Loads);
public sealed record 분산경로계획(IReadOnlyList<DistributedStopDto> Stops, decimal ElapsedMinutes, decimal DistanceKm,
    string EvidenceJson);
public interface I분산배송경로Planner
{
    Task<분산경로계획> PlanAsync(배차경로좌표 origin, DateTime startsUtc, 분산경로적재량 capacity,
        IReadOnlyList<분산경로Stop> consolidated, IReadOnlyList<분산경로Stop> separate, CancellationToken ct,
        분산경로적재량? initialLoad = null, IReadOnlyCollection<string>? initiallyPicked = null);
}
/// <summary>같은 거점 상차 통합과 분리 방문을 모두 비교한다. 대기와 작업시간을 포함한 시간, 거리 순으로 선택한다.</summary>
public sealed class 분산배송경로Planner(I배차추천경로Service routes, IOptions<DistributedFulfillmentOptions> options) : I분산배송경로Planner
{
    public async Task<분산경로계획> PlanAsync(배차경로좌표 origin, DateTime startsUtc, 분산경로적재량 capacity,
        IReadOnlyList<분산경로Stop> consolidated, IReadOnlyList<분산경로Stop> separate, CancellationToken ct,
        분산경로적재량? initialLoad = null, IReadOnlyCollection<string>? initiallyPicked = null)
    {
        분산물류UseCase.Require(options.Value.UseExistingRouteProvider, "DistributedRouteEvidenceProviderDisabled");
        분산물류UseCase.Require(capacity.WeightKg > 0 && capacity.VolumeCbm > 0 && capacity.Pallets >= 0, "DistributedVehicleCapacityMissing");
        분산물류UseCase.Require(initialLoad is null || initialLoad.WeightKg <= capacity.WeightKg && initialLoad.VolumeCbm <= capacity.VolumeCbm && initialLoad.Pallets <= capacity.Pallets, "DistributedVehicleCapacityExceeded");
        var variants = new[] { consolidated, separate }.Distinct().Where(x => x.Count > 0 && x.Count <= options.Value.MaximumRouteStops).ToArray();
        분산물류UseCase.Require(variants.Length > 0 && options.Value.MaximumRouteStops <= 10, "DistributedRouteTooLarge");
        분산경로계획? best = null; var attempts = new List<object>(); var attemptCount = 0;
        var cache = new Dictionary<(배차경로좌표, 배차경로좌표), 배차경로예상결과?>();
        foreach (var stops in variants)
        {
            분산물류UseCase.Require(stops.Select(x => x.StopId).Distinct().Count() == stops.Count
                && stops.All(x => x.Kind is "Pickup" or "Delivery" && x.StartUtc < x.EndUtc && x.ServiceMinutes >= 0
                    && x.ReservationIds.Count > 0 && x.ReservationIds.Distinct().Count() == x.ReservationIds.Count
                    && x.ReservationIds.All(id => x.Loads.TryGetValue(id, out var value) && value.WeightKg >= 0 && value.VolumeCbm >= 0 && value.Pallets >= 0)), "DistributedRouteSnapshotInvalid");
            var pickupIds = stops.Where(x => x.Kind == "Pickup").SelectMany(x => x.ReservationIds).ToArray();
            var deliveryIds = stops.Where(x => x.Kind == "Delivery").SelectMany(x => x.ReservationIds).ToArray();
            분산물류UseCase.Require(pickupIds.Distinct().Count() == pickupIds.Length && deliveryIds.Distinct().Count() == deliveryIds.Length
                && deliveryIds.ToHashSet().SetEquals(pickupIds.Concat(initiallyPicked ?? [])), "DistributedRouteSnapshotInvalid");
            var required = stops.Where(x => x.Kind == "Pickup").SelectMany(x => x.ReservationIds).ToHashSet(StringComparer.Ordinal);
            foreach (var sequence in Sequences(stops, [], new HashSet<string>(initiallyPicked ?? [], StringComparer.Ordinal)))
            {
                ct.ThrowIfCancellationRequested(); var point = origin; var time = startsUtc; var distance = 0m;
                var load = initialLoad ?? new 분산경로적재량(0, 0, 0); var result = new List<DistributedStopDto>(); var feasible = true;
                var picked = new HashSet<string>(initiallyPicked ?? [], StringComparer.Ordinal);
                foreach (var stop in sequence)
                {
                    var key = (point, stop.Point);
                    if (!cache.TryGetValue(key, out var leg))
                    {
                        try { leg = await routes.EstimateRouteAsync(point, stop.Point, ct); }
                        catch (OperationCanceledException) { throw; }
                        catch { throw new DistributedFulfillmentException("DistributedRouteProviderUnavailable"); }
                        cache[key] = leg;
                    }
                    if (leg?.Duration is null || leg.DistanceKm is null || leg.Duration.Value < TimeSpan.Zero || leg.DistanceKm < 0
                        || !leg.실제경로여부)
                    { feasible = false; break; }
                    var arrival = time.Add(leg.Duration.Value); if (arrival < stop.StartUtc) arrival = stop.StartUtc;
                    var done = arrival.AddMinutes((double)stop.ServiceMinutes);
                    if (done > stop.EndUtc) { feasible = false; break; }
                    foreach (var reservation in stop.ReservationIds)
                    {
                        if (stop.Kind == "Pickup") { load += stop.Loads[reservation]; picked.Add(reservation); }
                        else
                        {
                            if (!picked.Contains(reservation)) { feasible = false; break; }
                            load -= stop.Loads[reservation];
                        }
                    }
                    if (!feasible || load.WeightKg > capacity.WeightKg || load.VolumeCbm > capacity.VolumeCbm || load.Pallets > capacity.Pallets)
                    { feasible = false; break; }
                    result.Add(new(stop.StopId, result.Count + 1, stop.Kind, stop.SiteId, stop.OrderIds, arrival, stop.StartUtc, stop.EndUtc,
                        stop.ServiceMinutes, "Pending", stop.Address, stop.Point.Latitude, stop.Point.Longitude));
                    point = stop.Point; time = done; distance += leg.DistanceKm.Value;
                }
                if (!feasible || !required.IsSubsetOf(picked)) continue;
                var elapsed = (decimal)(time - startsUtc).TotalMinutes;
                attemptCount++;
                if (attempts.Count < 8) attempts.Add(new { ids = result.Select(x => x.StopId).ToArray(), elapsed, distance });
                if (best is null || elapsed < best.ElapsedMinutes || (elapsed == best.ElapsedMinutes && distance < best.DistanceKm))
                    best = new(result, elapsed, distance, "");
            }
        }
        분산물류UseCase.Require(best is not null, "DistributedRouteInfeasibleOrEvidenceMissing");
        return best! with { EvidenceJson = 분산물류UseCase.Serialize(new { objective = "elapsedIncludingWaitAndServiceThenDistance", startsUtc,
            source = "ExistingTypedRouteProvider", feasibleAttemptCount = attemptCount, sampleFeasibleAttempts = attempts, selected = best!.Stops.Select(x => x.StopId).ToArray() }) };
    }
    private static IEnumerable<IReadOnlyList<분산경로Stop>> Sequences(IReadOnlyList<분산경로Stop> remaining,
        IReadOnlyList<분산경로Stop> chosen, HashSet<string> picked)
    {
        if (remaining.Count == 0) { yield return chosen; yield break; }
        foreach (var stop in remaining)
        {
            if (stop.Kind == "Delivery" && stop.ReservationIds.Any(x => !picked.Contains(x))) continue;
            var nextPicked = new HashSet<string>(picked, StringComparer.Ordinal);
            if (stop.Kind == "Pickup") nextPicked.UnionWith(stop.ReservationIds);
            foreach (var sequence in Sequences(remaining.Where(x => x.StopId != stop.StopId).ToArray(), chosen.Append(stop).ToArray(), nextPicked)) yield return sequence;
        }
    }
}
