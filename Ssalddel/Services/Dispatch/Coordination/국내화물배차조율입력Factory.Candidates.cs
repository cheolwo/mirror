using 살뜰.도메인.배차;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;
using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Dispatch.Coordination;

public sealed partial class 국내화물배차조율입력Factory
{
    private static 운송원장[] FilterCandidateQueues(
        IEnumerable<운송원장> queues,
        IReadOnlyDictionary<string, 화주운송의뢰> requestMap,
        DateTime now)
    {
        return queues
            .Where(queue =>
            {
                requestMap.TryGetValue(queue.의뢰Id, out var transportRequest);
                return 국내화물배차후보금지정책.운송의뢰후보금지사유(queue, transportRequest, now).Count == 0;
            })
            .ToArray();
    }

    private static 국내화물운송기사상태Snapshot[] FilterCandidateDriverStates(
        IEnumerable<국내화물운송기사상태Snapshot> driverStates,
        IReadOnlyDictionary<string, 용달기사> driverMap)
    {
        return driverStates
            .Where(state =>
            {
                driverMap.TryGetValue(state.DriverId, out var driver);
                return 국내화물배차후보금지정책.기사후보금지사유(
                    state,
                    driver).Count == 0;
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<운송의뢰기사조합평가>> BuildEvaluationsAsync(
        IReadOnlyList<운송원장> candidateQueues,
        IReadOnlyDictionary<string, 화주운송의뢰> requestMap,
        IReadOnlyDictionary<string, 화물요구조건> cargoMap,
        IReadOnlyList<국내화물운송기사상태Snapshot> candidateDriverStates,
        IReadOnlyDictionary<string, 용달기사> driverMap,
        IReadOnlyDictionary<string, 기사근무> currentShiftMap,
        IReadOnlyDictionary<string, 차량제원> vehicleSpecMap,
        IReadOnlyDictionary<string, int> acceptedTransportCounts,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var evaluations = new List<운송의뢰기사조합평가>();
        foreach (var queue in candidateQueues)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!requestMap.TryGetValue(queue.의뢰Id, out var transportRequest))
            {
                continue;
            }

            cargoMap.TryGetValue(queue.의뢰Id, out var cargoRequirement);
            var rejectedDriverIds = await _거절Store.GetRejectedDriverIdsAsync(queue.의뢰Id, cancellationToken);
            foreach (var state in candidateDriverStates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!driverMap.TryGetValue(state.DriverId, out var driver))
                {
                    continue;
                }

                vehicleSpecMap.TryGetValue(driver.기사Id, out var vehicleSpec);
                evaluations.Add(await EvaluateAsync(
                    queue,
                    transportRequest,
                    cargoRequirement,
                    state,
                    driver,
                    currentShiftMap.GetValueOrDefault(driver.기사Id),
                    vehicleSpec,
                    rejectedDriverIds,
                    GetAcceptedTransportCount(acceptedTransportCounts, state.DriverId),
                    now,
                    cancellationToken));
            }
        }

        return evaluations;
    }
}
