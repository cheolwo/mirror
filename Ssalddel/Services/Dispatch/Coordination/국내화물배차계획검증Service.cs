using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Coordination;

/// <summary>기존 조율 결과를 검증한다. 새 배차·수익 엔진이나 운영 원장을 만들지 않는다.</summary>
public sealed class 국내화물배차계획검증Service(
    SsalddelContext db,
    I기사운송일정구성Service schedules,
    I운송일정삽입평가Service insertions,
    I기사실차적재근거조회Service vehicles,
    화물구간적재검증Service capacity,
    I화물배차상태SnapshotService? source = null) : I국내화물배차계획검증Service
{
    public async Task<국내화물배차조율결과> 검증Async(국내화물배차조율입력 input,
        국내화물배차조율결과 result, CancellationToken ct = default)
    {
        var kept = new List<국내화물배차제안>();
        var holds = result.보류목록.ToList();
        var plans = new List<국내화물검증수행계획>();
        var knownIds = input.운송의뢰목록.Select(x => x.의뢰Id).ToHashSet(StringComparer.Ordinal);
        var duplicates = result.추천배정목록.GroupBy(x => x.의뢰Id, StringComparer.Ordinal)
            .Where(x => x.Count() != 1).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var group in result.추천배정목록.GroupBy(x => x.기사Id, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var proposals = group.OrderBy(x => x.기사별추천순번).ThenBy(x => x.순번).ToArray();
            var drivers = input.기사후보목록.Where(x => x.기사Id == group.Key).ToArray();
            var driver = drivers.Length == 1 ? drivers[0] : null;
            var vehicle = await vehicles.조회Async(group.Key, ct);
            if (driver is null || driver.현재좌표 is null || input.판정근거?.입력지문 is null
                || result.기준시각Utc != input.기준시각Utc || !vehicle.실차확인됨 || vehicle.유효제원 is null)
            {
                Reject(proposals, "CargoPlan.ActualVehicleOrSnapshotUnverified");
                continue;
            }

            var active = await db.운송원장.AsNoTracking()
                .Where(x => (x.기사_운송자 == group.Key || x.확정기사Id == group.Key)
                    && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료)
                .ToArrayAsync(ct);
            if (active.Any(x => x.배차업무유형 != 상태값.배차업무유형.용달운송))
            {
                Reject(proposals, "CargoPlan.OtherWorkObligationUnverified");
                continue;
            }
            var ids = active.Select(x => x.의뢰Id).Concat(proposals.Select(x => x.의뢰Id))
                .Distinct(StringComparer.Ordinal).ToArray();
            var requests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
            var handling = 국내화물배차조율입력Factory.작업시간입력결합(requests.Values, input.상하차작업시간, out var timeConflicts);
            if (timeConflicts.Length > 0)
            {
                Reject(proposals, "CargoPlan.HandlingInputConflict");
                continue;
            }
            var cargo = await db.화물요구조건.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
            var fareIds = requests.Values.Select(x => x.운임구성Id).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
            var fares = await db.운임구성.AsNoTracking().Where(x => Enumerable.Contains(fareIds, x.Id))
                .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
            var original = await schedules.구성Async(group.Key, driver.현재좌표, ct);
            var current = original with
            {
                기준시각 = input.기준시각Utc,
                항목목록 = original.항목목록.Select(WithService).ToArray(),
                상하차작업시간 = handling
            };
            var jobs = active.Where(x => requests.ContainsKey(x.의뢰Id))
                .Select(x => new 화물구간적재작업(requests[x.의뢰Id], cargo.GetValueOrDefault(x.의뢰Id),
                    x.상태 is "상차완료" or "하차지도착" or "운송중")).ToList();
            if (jobs.Count != active.Length || current.항목목록.Any(x => !KnownService(x.의뢰Id) || !x.시간창종료일시.HasValue)
                || active.Any(x => !current.항목목록.Any(n => n.의뢰Id == x.의뢰Id)))
            {
                Reject(proposals, "CargoPlan.ActiveObligationOrHandlingTimeMissing");
                continue;
            }

            var accepted = new List<국내화물배차제안>();
            운송삽입시도결과? last = null;
            화물구간적재검증결과? lastCapacity = null;
            foreach (var proposal in proposals)
            {
                if (proposal.판정근거?.미확인사유.Contains("DispatchEvidence.ParticipationGuardDisabled", StringComparer.Ordinal) == true)
                {
                    Reject([proposal], "CargoPlan.ParticipationEvidenceMissing");
                    continue;
                }
                if (!knownIds.Contains(proposal.의뢰Id) || duplicates.Contains(proposal.의뢰Id)
                    || accepted.Count >= input.기사당최대추천건수 || jobs.Any(x => x.의뢰.의뢰Id == proposal.의뢰Id)
                    || !requests.TryGetValue(proposal.의뢰Id, out var request)
                    || !KnownService(proposal.의뢰Id) || request.픽업_시간창_종료일시 == default
                    || !request.하차_시간창_종료일시.HasValue)
                {
                    Reject([proposal], "CargoPlan.RequestOrHandlingTimeMissing");
                    continue;
                }
                if (proposal.판정근거?.입력지문 is null || !fares.TryGetValue(proposal.의뢰Id, out var fare)
                    || fare.기사지급예정운임 is not > 0m
                    || decimal.Truncate(fare.기사지급예정운임.Value) != fare.기사지급예정운임.Value
                    || (proposal.예상총비용 is { } cost && cost < 0m))
                {
                    Reject([proposal], "CargoPlan.PayoutOrDecisionEvidenceMissing");
                    continue;
                }
                var newJobs = jobs.Append(new 화물구간적재작업(request, cargo.GetValueOrDefault(request.의뢰Id))).ToArray();
                // 구형 추천의 수량 1·회전/적층 true 가정은 전체 계획의 명시 확인이 아니다.
                if (newJobs.Any(job =>
                    {
                        var raw = 차량화물적합성Service.NormalizeLoadingInput(job.의뢰, job.요구조건).입력;
                        return raw.수량 is null || raw.바닥회전가능여부 is null || raw.적층가능여부 is null;
                    }))
                {
                    Reject([proposal], "CargoPlan.LoadingAssumptionsUnverified");
                    continue;
                }
                if (화물구간적재검증Service.혼적차단사유(newJobs).Count > 0)
                {
                    Reject([proposal], "CargoPlan.MixedHandlingNotVerified");
                    continue;
                }
                if (source is not null && !await source.현재근거확인Async(proposal.판정근거, ct))
                {
                    Reject([proposal], "CargoPlan.SourceChanged");
                    continue;
                }
                var evaluated = await insertions.평가Async(current, request, ct);
                var candidateNodes = CandidateNodes(request);
                var allNodes = current.항목목록.Concat(candidateNodes).ToDictionary(x => (x.의뢰Id, x.단계유형));
                var found = false;
                foreach (var attempt in evaluated.시도목록.Where(x => x.전체완수가능여부 && x.전체실제경로확인됨)
                    .OrderBy(x => x.총추가지연분 ?? decimal.MaxValue).ThenBy(x => x.총소요시간분 ?? decimal.MaxValue))
                {
                    var rows = attempt.도착예상목록.OrderBy(x => x.순서).ToArray();
                    if (rows.Length != allNodes.Count || rows.Any(x => !allNodes.ContainsKey((x.의뢰Id, x.단계유형)))
                        || rows.Select(x => (x.의뢰Id, x.단계유형)).Distinct().Count() != allNodes.Count
                        || rows.Any(x => x.예상도착시각 is null || x.시간위반여부)) continue;
                    var ordered = rows.Select((x, i) => allNodes[(x.의뢰Id, x.단계유형)] with { 순서 = i }).ToArray();
                    if (!PreservesExistingOrder(current.항목목록, ordered)) continue;
                    var load = capacity.평가(vehicle.유효제원, newJobs, ordered);
                    if (!load.확인됨 || !load.적합여부) continue;
                    current = current with { 항목목록 = ordered };
                    jobs = newJobs.ToList();
                    last = attempt;
                    lastCapacity = load;
                    accepted.Add(proposal);
                    found = true;
                    break;
                }
                if (!found) Reject([proposal], "CargoPlan.NoVerifiedJointSchedule");
            }

            if (accepted.Count > 0 && last is not null && lastCapacity is not null)
            {
                var stable = true;
                if (source is not null)
                    foreach (var p in accepted)
                        stable &= await source.현재근거확인Async(p.판정근거!, ct);
                if (!stable)
                {
                    Reject(accepted, "CargoPlan.SourceChanged");
                    continue;
                }
                var stops = last.도착예상목록.OrderBy(x => x.순서).Select(x =>
                {
                    var duration = handling[x.의뢰Id];
                    var minutes = x.단계유형 == "pickup" ? duration.상차분!.Value : duration.하차분!.Value;
                    return new 국내화물계획경유점(x.의뢰Id, x.단계유형, x.순서, x.예상도착시각,
                        x.예상도착시각?.AddMinutes((double)minutes));
                }).ToArray();
                var requestIds = accepted.Select(x => x.의뢰Id).ToArray();
                var fingerprint = Hash(new { revision = "cargo-plan-r76-v1", input.판정근거.입력지문,
                    vehicle.근거지문, vehicle.판본, driver.기사Id, requestIds, stops, loads = lastCapacity.구간목록,
                    payouts = requestIds.Select(x => new { id = x, amount = fares[x].기사지급예정운임 }).ToArray() });
                var plan = new 국내화물검증수행계획
                {
                    계획Id = "cargo-plan-" + fingerprint, 기사Id = group.Key, 의뢰Ids = requestIds,
                    확인됨 = true, 가능여부 = true, 사유Code = "CargoPlan.Verified", 입력지문 = input.판정근거.입력지문,
                    경로순서 = stops, 구간검증결과 = lastCapacity, 총소요시간분 = last.총소요시간분, 총거리Km = last.총거리Km,
                    상하차작업시간 = jobs.ToDictionary(x => x.의뢰.의뢰Id, x => handling[x.의뢰.의뢰Id], StringComparer.Ordinal),
                    합의기사보수합계 = requestIds.Sum(x => fares[x].기사지급예정운임!.Value)
                };
                plans.Add(plan);
                kept.AddRange(accepted.Select(x => x with { 수행계획Id = plan.계획Id }));
            }

            bool KnownService(string id) => handling.TryGetValue(id, out var t) && t is { 확인됨: true };
            기사운송일정항목 WithService(기사운송일정항목 node) => node with
            {
                작업시간분 = handling.GetValueOrDefault(node.의뢰Id) is { } t
                    ? node.단계유형 == "pickup" ? t.상차분 : t.하차분 : null
            };
            기사운송일정항목[] CandidateNodes(화주운송의뢰 r) =>
            [
                WithService(new(r.의뢰Id, "pickup", "", Point(r.픽업_위도, r.픽업_경도), r.픽업_시간창_시작일시, r.픽업_시간창_종료일시, 0, null, false, true)),
                WithService(new(r.의뢰Id, "dropoff", "", Point(r.하차_위도, r.하차_경도), r.하차_시간창_시작일시, r.하차_시간창_종료일시, 1, null, false, true))
            ];
        }

        var acceptedIds = kept.Select(x => x.의뢰Id).ToHashSet(StringComparer.Ordinal);
        return result with
        {
            추천배정목록 = kept.OrderBy(x => x.순번).ToArray(), 검증계획목록 = plans,
            보류목록 = holds.Where(x => !acceptedIds.Contains(x.의뢰Id)).DistinctBy(x => x.의뢰Id).ToArray(),
            전체예상비용 = CompleteSum(kept.Select(x => x.예상총비용)),
            전체예상운임 = CompleteSum(kept.Select(x => x.예상운임)),
            전체예상순이익 = CompleteSum(kept.Select(x => x.예상순이익))
        };

        void Reject(IEnumerable<국내화물배차제안> items, string code)
        {
            var batch = items.ToArray();
            foreach (var p in batch) holds.Add(new(p.의뢰Id, code));
            if (batch.Length > 0) plans.Add(new()
            {
                기사Id = batch[0].기사Id, 의뢰Ids = batch.Select(x => x.의뢰Id).ToArray(),
                확인됨 = false, 가능여부 = false, 사유Code = code, 입력지문 = input.판정근거?.입력지문
            });
        }
    }

    private static bool PreservesExistingOrder(IReadOnlyList<기사운송일정항목> before, IReadOnlyList<기사운송일정항목> after)
        => before.Select(x => (x.의뢰Id, x.단계유형)).SequenceEqual(after
            .Where(x => before.Any(b => b.의뢰Id == x.의뢰Id && b.단계유형 == x.단계유형))
            .Select(x => (x.의뢰Id, x.단계유형)));
    private static 배차경로좌표? Point(decimal? lat, decimal? lon) => lat.HasValue && lon.HasValue ? new(lat.Value, lon.Value) : null;
    private static decimal? CompleteSum(IEnumerable<decimal?> amounts)
    {
        var values = amounts.ToArray();
        return values.Length == 0 || values.Any(x => !x.HasValue) ? null : values.Sum(x => x!.Value);
    }
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
