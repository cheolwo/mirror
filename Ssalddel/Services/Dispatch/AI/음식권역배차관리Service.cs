using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.AI;

public interface I음식권역배차관리Service
{
    Task<음식권역배차관리결과> 검토Async(운송원장 queue, 멀티배차조합요청 request,
        CancellationToken cancellationToken = default);
    Task<음식권역배차관리결과> 검토자료Async(음식배달수요참여결과? demand, 멀티배차조합요청 request,
        CancellationToken cancellationToken = default);
}

public interface I음식권역배차AI
{
    bool Enabled { get; }
    Task<음식권역배차AI선택> 선택Async(음식권역배차AI검토 review,
        CancellationToken cancellationToken = default);
}

public sealed record 음식권역배차AI검토(bool 수요자료최신, string 수요ActionCode,
    decimal? 예상업무량건수, decimal? 기존기사가용처리건수, int 보호활동기사수,
    int 추가참여인원, int 제안허용기사수, IReadOnlyList<멀티배차조합후보> 유효조합목록);

public sealed record 음식권역배차AI선택(string? 조합키, string StatusCode);

/// <summary>참여 권고·조합 비교 결과입니다. 배차 확정·용량/모든 약속 검증 결과가 아닙니다.</summary>
public sealed record 음식권역배차관리결과(string 수요ActionCode, 음식배달수요참여결과? 수요판정,
    IReadOnlyList<멀티배차조합후보> 생성조합목록, IReadOnlyList<멀티배차조합후보> 유효조합목록,
    멀티배차조합후보? 기준조합, 멀티배차조합후보? 선택조합,
    IReadOnlyList<string> 제안허용기사Ids, string AIStatusCode)
{
    public bool OperationalApplied => false;
    public bool RequiresCurrentGates => true;
    public bool 실제적재검증됨 => false;
    public bool 전체약속시간검증됨 => false;
}

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Application,
    "현재 수요 참여 판정과 기존 음식 단건·2건 조합을 읽기 전용으로 결합한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.NetworkCall, FlowOrder = 46,
    StepKey = "application.food-regional-dispatch-review",
    DependsOnStepKeys = ["application.food-demand-participation"],
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.None,
    Boundary = "수요 정책의 참여 허용과 우선순위를 보존한다. AI는 유효 조합 중 제안만 하며 원장·수신 의사·확정·수락·알림·지급을 변경하거나 실제 적재/모든 약속 시간 관문 통과를 인증하지 않는다.")]
public sealed class 음식권역배차관리Service(I음식배달수요참여Service demandService,
    I음식멀티배차조합Service bundleService, I음식권역배차AI ai, TimeProvider clock)
    : I음식권역배차관리Service
{
    public async Task<음식권역배차관리결과> 검토Async(운송원장 queue, 멀티배차조합요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (queue.배차업무유형 != 상태값.배차업무유형.음식배달)
            return Empty(null, "NotFood", "NotFood");
        var demand = await demandService.평가Async(queue, cancellationToken);
        return await 검토자료Async(demand, request, cancellationToken);
    }

    public async Task<음식권역배차관리결과> 검토자료Async(음식배달수요참여결과? demand,
        멀티배차조합요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.배차업무유형 != 상태값.배차업무유형.음식배달)
            return Empty(demand, "NotFood", "NotFood");
        if (request.작업목록 is null || request.작업목록.Count > 12
            || request.작업목록.Any(x => x is null || string.IsNullOrWhiteSpace(x.의뢰Id))
            || request.작업목록.Select(x => x.의뢰Id).Distinct(StringComparer.Ordinal).Count() != request.작업목록.Count)
            return Empty(demand, "InvalidBundleRequest", "InvalidBundleRequest");

        // 기존 생성기는 최대 두 건입니다. 호출자가 큰 값을 넣어도 기존 한도를 넓히지 않습니다.
        var bounded = request with { 최대묶음크기 = Math.Min(2, request.최대묶음크기),
            최대조합수 = request.최대조합수 <= 0 ? 50 : Math.Min(50, request.최대조합수) };
        var generated = bundleService.조합생성(bounded);
        var sources = request.작업목록.ToDictionary(x => x.의뢰Id, StringComparer.Ordinal);
        var valid = generated.Where(x => Valid(x, sources))
            .GroupBy(x => x.조합키, StringComparer.Ordinal).Where(x => x.Count() == 1).Select(x => x.Single()).ToArray();
        var baseline = valid.FirstOrDefault();
        var current = DemandCurrent(demand, clock.GetUtcNow());
        var action = !current ? "DemandUnknown"
            : demand!.추가참여인원 > 0 ? "AdditionalParticipationNeeded" : "ExistingCapacitySufficient";
        var allowed = current ? demand!.기사판정목록.Where(x => x.신규제안허용)
            .OrderBy(x => x.우선순위).Select(x => x.기사Id).ToArray() : [];
        음식권역배차관리결과 Result(멀티배차조합후보? selected, string status)
            => new(action, demand, generated, valid, baseline, selected, allowed, status);
        if (!current) return Result(baseline, "DemandUnknown");
        if (allowed.Length == 0) return Result(null, "NoParticipatingDriver");
        if (baseline is null) return Result(null, "NoPlans");
        if (!ai.Enabled) return Result(baseline, "Disabled");
        if (valid.Length == 1) return Result(baseline, "SinglePlan");

        var selection = await ai.선택Async(new(true, action, demand!.예상업무량건수,
            demand.기존기사가용처리건수, demand.보호활동기사수, demand.추가참여인원, allowed.Length, valid), cancellationToken);
        if (selection.StatusCode != "Applied") return Result(baseline, selection.StatusCode);
        var selected = valid.FirstOrDefault(x => x.조합키 == selection.조합키);
        return selected is null ? Result(baseline, "InvalidSelection") : Result(selected, "Applied");
    }

    private static bool Valid(멀티배차조합후보 x, IReadOnlyDictionary<string, 픽업하차경로작업> sources)
        => x is not null && x.조합가능여부 && !string.IsNullOrWhiteSpace(x.조합키)
            && x.의뢰Ids is not null && x.작업목록 is not null && x.의뢰Ids.Count is 1 or 2
            && x.의뢰Ids.Distinct(StringComparer.Ordinal).Count() == x.의뢰Ids.Count
            && x.작업목록.Count == x.의뢰Ids.Count && x.제외사유?.Count is null or 0
            && x.작업목록.All(job => job is not null && x.의뢰Ids.Contains(job.의뢰Id, StringComparer.Ordinal)
                && sources.TryGetValue(job.의뢰Id, out var source) && source == job
                && Coordinate(job.픽업좌표) && Coordinate(job.하차좌표));

    private static bool Coordinate(배차경로좌표? p) => p is { Latitude: >= -90m and <= 90m, Longitude: >= -180m and <= 180m };

    private static bool DemandCurrent(음식배달수요참여결과? demand, DateTimeOffset now)
        => demand is { 데이터상태Code: 음식배달수요자료상태Code.최신, 예상업무량건수: >= 0m,
            기존기사가용처리건수: >= 0m, 보호활동기사수: >= 0, 추가참여인원: >= 0 }
            && demand.정책판본 == 음식배달수요참여Policy.현재정책판본
            && demand.판정시각Utc <= now && now - demand.판정시각Utc <= TimeSpan.FromMinutes(5)
            && demand.기사판정목록 is not null
            && demand.기사판정목록.All(x => x is not null && !string.IsNullOrWhiteSpace(x.기사Id) && x.우선순위 > 0)
            && demand.기사판정목록.Select(x => x.기사Id).Distinct(StringComparer.Ordinal).Count() == demand.기사판정목록.Count
            && demand.기사판정목록.Select(x => x.우선순위).Distinct().Count() == demand.기사판정목록.Count;

    private static 음식권역배차관리결과 Empty(음식배달수요참여결과? demand, string action, string status)
        => new(action, demand, [], [], null, null, [], status);
}
