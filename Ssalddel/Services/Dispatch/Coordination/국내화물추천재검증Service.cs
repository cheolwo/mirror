using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Coordination;

public sealed record 국내화물추천재검증결과(
    IReadOnlyList<string> 유효의뢰Ids, IReadOnlyList<string> 갱신의뢰Ids, IReadOnlyList<string> 보류의뢰Ids);

public interface I국내화물추천재검증Service
{
    Task<국내화물추천재검증결과> 갱신Async(string driverId, CancellationToken ct = default);
}

/// <summary>이미 본인에게 전달된 추천만 재검증한다. 새 배정·가격·알림·기사 동의를 만들지 않는다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Application,
    "위치와 수행이 달라진 본인 추천을 정본 작업시간·일정·구간 적재로 다시 확인한다.",
    StepKey = "application.cargo-owned-recommendation-refresh", FlowOrder = 48,
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "본인 유효 추천의 근거·판본만 갱신/보류. 신규 추천·푸시·대기순위·운영 활성화·수락 확정 없음.")]
public sealed class 국내화물추천재검증Service(
    SsalddelContext db, I화물배차상태SnapshotService sources,
    I국내화물운송기사상태Store states, I국내화물배차계획검증Service plans,
    ISharedDriverWorkAdmissionService participation, TimeProvider? clock = null) : I국내화물추천재검증Service
{
    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public async Task<국내화물추천재검증결과> 갱신Async(string driverId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(driverId)) return new([], [], []);
        using var lease = await participation.EnterAsync(driverId, ct);
        if (db.Database.CurrentTransaction is not null)
            return await 갱신CoreAsync(driverId, ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(token => 갱신CoreAsync(driverId, token), ct);
    }

    private async Task<국내화물추천재검증결과> 갱신CoreAsync(string driverId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        await participation.LockAsync(driverId, ct);
        var now = Now;
        var rows = await db.운송원장.Where(x => x.배차업무유형 == 상태값.배차업무유형.용달운송
            && x.상태 == 상태값.배차대기상태.대기 && x.배차큐단계 == 상태값.배차큐단계.배차추천
            && x.배차노출상태 == 상태값.배차노출상태.추천중 && x.현재추천대상기사Id == driverId
            && x.추천만료시각.HasValue && x.추천만료시각 > now)
            .OrderBy(x => x.의뢰Id).ToArrayAsync(ct);
        foreach (var row in rows) await db.Entry(row).ReloadAsync(ct);
        rows = rows.Where(x => x.상태 == 상태값.배차대기상태.대기 && x.배차큐단계 == 상태값.배차큐단계.배차추천
            && x.배차노출상태 == 상태값.배차노출상태.추천중 && x.현재추천대상기사Id == driverId
            && x.추천만료시각 > Now).ToArray();
        var generations = rows.ToDictionary(x => x.의뢰Id,
            x => (x.추천라운드, x.추천만료시각, x.기사지급예정액, x.추천판정근거Json), StringComparer.Ordinal);
        var valid = new List<string>();
        var refreshed = new List<string>();
        var held = new List<string>();
        var observed = new Dictionary<string, 국내화물배차판정근거>(StringComparer.Ordinal);
        var saved = new Dictionary<string, 국내화물추천저장근거>(StringComparer.Ordinal);
        var needsRefresh = false;
        foreach (var row in rows)
        {
            var original = Read(row.추천판정근거Json);
            // 구형 일반 큐/직접 추천은 기존 조회·수락 관문에 맡긴다. 근거가 없는 행을 승격하거나 새 갱신으로 회수하지 않는다.
            if (original?.판정근거?.지문판본 != "cargo-dispatch-state-r77-v1") continue;
            var current = await sources.조회Async(row.의뢰Id, driverId, now, ct);
            if (!await SameGeneration(row)) { held.Add(row.의뢰Id); continue; }
            if (!EnvelopeMatches(original, row, driverId) || !MayRefresh(original!.판정근거!, current, row.의뢰Id))
            { Hold(row, original, "CargoRefresh.ProtectedConditionsChanged"); continue; }
            saved[row.의뢰Id] = original!;
            observed[row.의뢰Id] = current;
            needsRefresh |= current.입력지문 != original!.판정근거!.입력지문;
        }

        if (!needsRefresh)
        {
            foreach (var row in rows.Where(x => observed.ContainsKey(x.의뢰Id)))
            {
                var stable = await sources.현재근거확인Async(observed[row.의뢰Id], ct);
                if (!await SameGeneration(row)) held.Add(row.의뢰Id);
                else if (stable) valid.Add(row.의뢰Id);
                else Hold(row, saved[row.의뢰Id], "CargoRefresh.SourceChangedDuringCheck");
            }
        }
        else
        {
            var candidates = rows.Where(x => observed.ContainsKey(x.의뢰Id)).ToArray();
            var ids = candidates.Select(x => x.의뢰Id).ToArray();
            var requests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
            var activeIds = await db.운송원장.AsNoTracking().Where(x => (x.확정기사Id == driverId || x.기사_운송자 == driverId)
                && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료)
                .Select(x => x.의뢰Id).ToArrayAsync(ct);
            var allIds = ids.Concat(activeIds).Distinct(StringComparer.Ordinal).ToArray();
            var handling = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(allIds, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, x => 화주운송의뢰매퍼.ReadHandlingTime(x), StringComparer.Ordinal, ct);
            var state = await states.GetAsync(driverId, ct);
            var driver = await db.용달기사.AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == driverId, ct);
            var admitted = candidates.Length > 0 && (await participation.CheckAsync(driverId, ids, ct)).IsSuccess;
            if (!admitted || state?.Latitude is null || state.Longitude is null || driver is null || requests.Count != candidates.Length)
                foreach (var row in candidates) Hold(row, saved[row.의뢰Id], "CargoRefresh.InputMissing");
            else
            {
                var batch = new 국내화물배차판정근거
                {
                    기준시각Utc = now, 조회시작시각Utc = now, 조회완료시각Utc = Now, 원자적조회여부 = false,
                    조회범위 = "owned active recommendations + current obligations; sequential refresh",
                    입력지문 = 화물배차상태SnapshotService.지문(observed.OrderBy(x => x.Key, StringComparer.Ordinal)
                        .Select(x => new { Id = x.Key, x.Value.입력지문, x.Value.순위관측지문 }).ToArray())
                };
                var input = new 국내화물배차조율입력(now, candidates.Length,
                    candidates.Select(x => RequestInput(x, requests[x.의뢰Id])).ToArray(),
                    [new(driverId, driver.차량, state.운행상태, activeIds.Length, "", "",
                        new(state.Latitude.Value, state.Longitude.Value), state.Aging점수, state.Aging기준시각Utc,
                        state.상차접근허용반경Km, state.위치수신시각Utc)], [])
                    { 판정근거 = batch, 상하차작업시간 = handling };
                var proposed = new 국내화물배차조율결과(now,
                    candidates.Select((row, i) => new 국내화물배차제안(i + 1, row.의뢰Id, driverId, i + 1,
                        0m, null, row.기사지급예정액, null, "본인 추천의 최신 수행계획 재검증", [])
                        { 판정근거 = observed[row.의뢰Id] }).ToArray(), [], [], null, null, null)
                    { 판정근거 = batch };
                // 기존 실제 경로·순서·구간 적재 검사를 실행한다. 과거 계획에 새 지문만 붙이지 않는다.
                var checkedPlan = await plans.검증Async(input, proposed, ct);
                var allStable = true;
                foreach (var evidence in observed.Values)
                    allStable &= await sources.현재근거확인Async(evidence, ct);
                foreach (var row in candidates)
                {
                    if (!await SameGeneration(row))
                    {
                        // 다른 수락/새 추천을 보류 처리로 덮지 않는다. 최신 정본의 소유·라운드가 권위다.
                        held.Add(row.의뢰Id);
                        continue;
                    }
                    var proposal = checkedPlan.추천배정목록.SingleOrDefault(x => x.의뢰Id == row.의뢰Id && x.기사Id == driverId);
                    var matches = checkedPlan.검증계획목록.Where(x => x.계획Id == proposal?.수행계획Id).Take(2).ToArray();
                    if (!allStable || proposal is null || matches.Length != 1
                        || matches[0] is not { 확인됨: true, 가능여부: true } plan
                        || plan.입력지문 != batch.입력지문 || plan.기사Id != driverId
                        || !plan.의뢰Ids.Contains(row.의뢰Id, StringComparer.Ordinal)
                        || row.추천만료시각 <= Now)
                    { Hold(row, saved[row.의뢰Id], "CargoRefresh.NoCurrentVerifiedPlan"); continue; }
                    row.추천판정근거Json = JsonSerializer.Serialize(new 국내화물추천저장근거
                        { 판정근거 = observed[row.의뢰Id], 수행계획 = plan, 조율입력지문 = batch.입력지문,
                            재검증상태Code = "Recomputed", 재검증시각Utc = Now });
                    row.추천라운드++;
                    row.UpdatedAt = Now;
                    valid.Add(row.의뢰Id);
                    refreshed.Add(row.의뢰Id);
                }
            }
        }
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new(valid, refreshed, held);

        async Task<bool> SameGeneration(운송원장 row)
        {
            var latest = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == row.Id, ct);
            var original = generations[row.의뢰Id];
            var same = latest is not null && latest.상태 == 상태값.배차대기상태.대기
                && latest.배차큐단계 == 상태값.배차큐단계.배차추천 && latest.배차노출상태 == 상태값.배차노출상태.추천중
                && latest.현재추천대상기사Id == driverId && latest.추천라운드 == original.추천라운드
                && latest.추천만료시각 == original.추천만료시각 && latest.추천만료시각 > Now
                && latest.기사지급예정액 == original.기사지급예정액 && latest.추천판정근거Json == original.추천판정근거Json;
            if (!same) await db.Entry(row).ReloadAsync(ct);
            return same;
        }

        void Hold(운송원장 row, 국내화물추천저장근거? original, string code)
        {
            row.추천라운드++;
            row.현재추천대상기사Id = null;
            row.추천시작시각 = null;
            row.추천만료시각 = null;
            row.배차노출상태 = 상태값.배차노출상태.추천후보없음;
            row.UpdatedAt = Now;
            // 과거 근거를 보존하면서 보류를 기록한다. 미확인 원장에 새 검증 근거를 만들어 넣지 않는다.
            if (original is not null)
                row.추천판정근거Json = JsonSerializer.Serialize(original with { 재검증상태Code = code, 재검증시각Utc = Now });
            held.Add(row.의뢰Id);
        }
    }

    private static bool MayRefresh(국내화물배차판정근거 old, 국내화물배차판정근거 current, string requestId)
        => old.지문판본 == "cargo-dispatch-state-r77-v1" && current.지문판본 == old.지문판본
            && !string.IsNullOrWhiteSpace(old.기사고정조건지문) && old.기사고정조건지문 == current.기사고정조건지문
            && old.차단사유.Count == 0 && current.차단사유.Count == 0
            && !old.미확인사유.Contains("DispatchEvidence.ParticipationGuardDisabled", StringComparer.Ordinal)
            && !current.미확인사유.Contains("DispatchEvidence.ParticipationGuardDisabled", StringComparer.Ordinal)
            && old.의뢰근거.TryGetValue(requestId, out var oldRequest)
            && current.의뢰근거.TryGetValue(requestId, out var currentRequest) && oldRequest == currentRequest;

    private static bool EnvelopeMatches(국내화물추천저장근거? value, 운송원장 row, string driverId)
        => value?.판정근거 is { } evidence && evidence.의뢰근거.Count == 1 && evidence.기사근거.Count == 1
            && evidence.기사근거.ContainsKey(driverId) && evidence.의뢰근거.ContainsKey(row.의뢰Id)
            && value.수행계획 is { 가능여부: true, 확인됨: true } plan && plan.기사Id == driverId
            && plan.의뢰Ids.Contains(row.의뢰Id, StringComparer.Ordinal)
            && !string.IsNullOrWhiteSpace(value.조율입력지문) && plan.입력지문 == value.조율입력지문;

    private static 국내화물추천저장근거? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<국내화물추천저장근거>(json); }
        catch (JsonException) { return null; }
    }

    private static 운송의뢰조율입력 RequestInput(운송원장 q, 화주운송의뢰 r)
        => new(q.Id, q.의뢰Id, q.원본의뢰유형, r.화물종류, r.화물온도조건, r.화물중량Kg, r.최종운임, "", "",
            r.픽업_위도.HasValue && r.픽업_경도.HasValue ? new(r.픽업_위도.Value, r.픽업_경도.Value) : null,
            r.하차_위도.HasValue && r.하차_경도.HasValue ? new(r.하차_위도.Value, r.하차_경도.Value) : null,
            r.픽업_시간창_시작일시, r.픽업_시간창_종료일시, r.하차_시간창_시작일시, r.하차_시간창_종료일시,
            q.추천라운드, q.CreatedAt);
}
