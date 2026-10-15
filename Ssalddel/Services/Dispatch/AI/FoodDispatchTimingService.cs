using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.AI;

public interface IFoodDispatchTimingService
{
    bool Enabled { get; }
    Task<FoodDispatchTimingEvaluation> EvaluateAsync(운송원장 queue, CancellationToken cancellationToken = default);
}

public sealed record FoodDispatchTimingEvaluation(FoodDispatchTimingInput Input,
    FoodDispatchTimingDecision Decision, string AIStatusCode);

public interface IFoodDispatchTimingAI
{
    bool Enabled { get; }
    Task<FoodDispatchTimingAISelection> SelectAsync(FoodDispatchTimingInput input,
        FoodDispatchTimingDecision baseline, CancellationToken cancellationToken = default);
}

public sealed record FoodDispatchTimingAISelection(string? ActionCode, string StatusCode);

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Application,
    "음식 권역의 미배정 주문·최근 유입·기사 참여 여력을 대조해 짧은 수집 또는 기존 즉시 처리를 판단한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall, FlowOrder = 46,
    StepKey = "application.food-dispatch-timing",
    DependsOnStepKeys = ["application.food-demand-participation", "domain.food-dispatch-timing-policy", "domain.food-dispatch-information-policy"],
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "최초 접수 시각의 수집 상한을 재시도·AI로 연장하지 않는다. 좌표 근사 조합은 적재/모든 약속 검증이나 실제 묶음 수락이 아니다. 기존 수요 서비스의 판정 투영 복구는 가능하나 요금·추천·수락·알림 원장은 변경하지 않는다.")]
public sealed partial class FoodDispatchTimingService(SsalddelContext db, I음식배달수요참여Service demandService,
    IFoodDispatchTimingAI ai, IOptions<FoodDispatchTimingOptions> options, FoodDispatchTimingPolicy policy,
    TimeProvider clock, ILogger<FoodDispatchTimingService> logger,
    ISsalddelFoodOrderStore? foodOrderStore = null) : IFoodDispatchTimingService
{
    private const decimal CellSize = 0.025m;
    public bool Enabled => options.Value.Enabled;

    public async Task<FoodDispatchTimingEvaluation> EvaluateAsync(운송원장 queue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        var timingSettings = settings.Policy;
        var now = clock.GetUtcNow();
        var applicable = Enabled && queue.배차업무유형 == 상태값.배차업무유형.음식배달
            && queue.상태 == 상태값.배차대기상태.대기 && !Market(queue)
            && string.IsNullOrWhiteSpace(queue.확정기사Id) && string.IsNullOrWhiteSpace(queue.기사_운송자)
            && queue.배차큐단계 is 상태값.배차큐단계.계획배차 or 상태값.배차큐단계.배차추천;
        var input = new FoodDispatchTimingInput
        {
            NowUtc = now, QueuedAtUtc = Utc(queue.CreatedAt), Applicable = applicable,
            HasActiveOffer = !string.IsNullOrWhiteSpace(queue.현재추천대상기사Id)
                || 배차실행인덱스재구성정책.유효한추천중잠금인가(queue, now.UtcDateTime),
            IsRetry = queue.추천라운드 > 0 || queue.계획배차시도횟수 > 1
                || queue.배차노출상태 == 상태값.배차노출상태.추천후보없음
        };
        var information = new TimingInformation(applicable && !input.HasActiveOffer && !input.IsRetry, queue, now);
        // 설정 오류도 기존 복귀 결과로 반환하므로 준비 상태 계산에 잘못된 유효시간을 넘기지 않습니다.
        var informationMaxAge = timingSettings?.MaxSnapshotAgeSeconds is >= 1 and <= 120
            ? timingSettings.MaxSnapshotAgeSeconds : 15;
        void RefreshInformation() => input = input with
        {
            InformationReadiness = information.Evaluate(input.NowUtc, informationMaxAge)
        };
        FoodDispatchTimingEvaluation Result(string status, FoodDispatchTimingDecision? decision = null, bool defaultPolicy = false)
        {
            input = input with { NowUtc = clock.GetUtcNow() };
            RefreshInformation();
            var currentDecision = policy.Evaluate(input, defaultPolicy ? null : timingSettings);
            if (decision?.ActionCode == "Collect" && currentDecision.ActionCode != "Collect")
                return new(input, currentDecision, status == "Applied" ? "ServerRequiredAfterAI" : "ServerRequired");
            return new(input, decision ?? currentDecision, status);
        }
        if (!applicable) return Result(Enabled ? "NotApplicable" : "Disabled", defaultPolicy: true);
        if (!settings.Validate() || timingSettings is null) return Result("InvalidConfiguration", defaultPolicy: true);
        // 재배차·기존 제안·최대 대기를 먼저 판정하여 불필요한 조회나 모델 호출을 하지 않습니다.
        if (input.HasActiveOffer || input.IsRetry || input.QueuedAtUtc == default || input.QueuedAtUtc > now
            || (now - input.QueuedAtUtc).TotalSeconds >= timingSettings.MaxWaitSeconds)
            return Result("ServerRequired");
        if (!Coordinate(queue.픽업_위도, queue.픽업_경도)
            || !Coordinate(queue.하차_위도, queue.하차_경도)) return Result("RegionUnknown");

        try
        {
            var source = ReadOrderInformation(queue, information);
            // 현재 차수의 조리 예정만 보수적 상한으로 사용합니다. 실제 픽업·도착 시각이 아닙니다.
            input = input with { LatestDispatchAtUtc = source?.LatestDispatchAtUtc };
            cancellationToken.ThrowIfCancellationRequested();
            var region = 음식배달권정책.판정(new(queue.픽업_위도!.Value, queue.픽업_경도!.Value), null).배달권키;
            var keys = 음식배달권정책.인접배달권키조회(region).Append(region).ToHashSet(StringComparer.Ordinal);
            var minLatitude = Math.Floor(queue.픽업_위도.Value / CellSize) * CellSize - CellSize;
            var minLongitude = Math.Floor(queue.픽업_경도!.Value / CellSize) * CellSize - CellSize;
            var from = now.UtcDateTime.AddSeconds(-settings.ObservationSeconds);
            var rows = await db.운송원장.AsNoTracking()
                .Where(x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                    && x.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트주문
                    && x.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트음식주문
                    && x.상태 != 상태값.배차상태.취소
                    && x.픽업_위도 >= minLatitude && x.픽업_위도 < minLatitude + 3m * CellSize
                    && x.픽업_경도 >= minLongitude && x.픽업_경도 < minLongitude + 3m * CellSize
                    && (x.CreatedAt >= from || x.상태 == 상태값.배차대기상태.대기))
                .OrderBy(x => x.Id).Take(settings.MaximumQueueRows + 1).ToArrayAsync(cancellationToken);
            if (rows.Length > settings.MaximumQueueRows)
            {
                information.Missing(FoodDispatchInformationKeys.OrderPool, "QueuePoolTruncated");
                return Result("SnapshotTruncated");
            }
            var regional = rows.Where(x => Coordinate(x.픽업_위도, x.픽업_경도)
                    && keys.Contains(음식배달권정책.판정(new(x.픽업_위도!.Value, x.픽업_경도!.Value), null).배달권키))
                .GroupBy(x => string.IsNullOrWhiteSpace(x.원본의뢰Id) ? x.의뢰Id : x.원본의뢰Id, StringComparer.Ordinal)
                .Select(x => x.OrderByDescending(y => y.Id).First()).ToArray();
            var pending = regional.Where(x => x.상태 == 상태값.배차대기상태.대기
                && x.배차큐단계 is 상태값.배차큐단계.계획배차 or 상태값.배차큐단계.배차추천
                && string.IsNullOrWhiteSpace(x.현재추천대상기사Id)
                && string.IsNullOrWhiteSpace(x.확정기사Id) && string.IsNullOrWhiteSpace(x.기사_운송자)
                && !배차실행인덱스재구성정책.유효한추천중잠금인가(x, now.UtcDateTime)).ToArray();
            if (!pending.Any(x => x.의뢰Id == queue.의뢰Id && x.Id == queue.Id))
            {
                information.Missing(FoodDispatchInformationKeys.OrderPool, "CurrentQueueNotObserved");
                return Result("CurrentQueueNotObserved");
            }
            information.Current(FoodDispatchInformationKeys.OrderPool, clock.GetUtcNow());
            var demand = await demandService.평가Async(queue, cancellationToken);
            ObserveDemandInformation(information, demand);
            RecheckOrderInformation(queue, source, information);
            var observedAt = clock.GetUtcNow();
            var current = Current(demand, observedAt);
            var compatible = pending.Count(x => Compatible(queue, x));
            var arrivals = regional.Count(x => x.의뢰Id != queue.의뢰Id && x.CreatedAt >= from
                && x.CreatedAt <= now.UtcDateTime);
            input = input with
            {
                NowUtc = observedAt, SnapshotAtUtc = current && demand!.판정시각Utc < now ? demand.판정시각Utc : current ? now : null,
                DemandKnown = current,
                DriverCapacityKnown = current && demand!.기존기사가용처리건수.HasValue,
                AvailableDrivers = current ? demand!.기사판정목록.Count(x => x.신규제안허용) : 0,
                WaitingOrders = pending.Length, CompatibleOrders = compatible,
                PromisingPlanAvailable = compatible >= timingSettings.MinimumCompatibleOrders,
                ExpectedNewOrders = arrivals * (decimal)timingSettings.CollectionSeconds / settings.ObservationSeconds,
                DelayRisk = pending.Any(x => x.CreatedAt < now.UtcDateTime.AddMinutes(-10))
            };
            RefreshInformation();
            var baseline = policy.Evaluate(input, timingSettings);
            if (!baseline.AllowedActions.Contains("Collect", StringComparer.Ordinal)) return Result("ServerRequired", baseline);
            if (!ai.Enabled) return Result("Disabled", baseline);
            var selection = await ai.SelectAsync(input, baseline, cancellationToken);
            RecheckOrderInformation(queue, source, information);
            // 모델 대기 중 경과한 시간도 원래 접수 시각에 합산합니다.
            input = input with { NowUtc = clock.GetUtcNow() };
            RefreshInformation();
            var currentDecision = policy.Evaluate(input, timingSettings);
            if (selection.StatusCode == "Applied" && selection.ActionCode == "Collect"
                && currentDecision.AllowedActions.Contains("Collect", StringComparer.Ordinal))
                return Result("Applied", currentDecision);
            if (selection.StatusCode == "Applied" && selection.ActionCode == "Collect")
                return Result("ServerRequiredAfterAI", currentDecision);
            if (selection.StatusCode == "Applied" && selection.ActionCode == "DispatchNow")
                return Result("Applied", currentDecision with { ActionCode = "DispatchNow", ReasonCode = "AITimingDispatchNow", RecheckAtUtc = null,
                    AllowedActions = ["DispatchNow"] });
            // 통신 실패 또는 허용 밖 선택으로 주문을 더 기다리게 만들지 않습니다.
            return Result(selection.StatusCode == "Applied" ? "InvalidSelection" : selection.StatusCode,
                currentDecision with { ActionCode = "DispatchNow", ReasonCode = "AITimingFallback", RecheckAtUtc = null,
                    AllowedActions = ["DispatchNow"] });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is System.Data.Common.DbException or TimeoutException or InvalidOperationException
            or StackExchange.Redis.RedisException or OperationCanceledException)
        {
            logger.LogWarning("Food dispatch timing snapshot unavailable. FailureType={FailureType}", ex.GetType().Name);
            information.Missing(FoodDispatchInformationKeys.Order, "SnapshotReadFailed");
            information.Missing(FoodDispatchInformationKeys.Preparation, "SnapshotReadFailed");
            information.Missing(FoodDispatchInformationKeys.OrderPool, "SnapshotReadFailed");
            information.Missing(FoodDispatchInformationKeys.Demand, "SnapshotReadFailed");
            information.Missing(FoodDispatchInformationKeys.DriverCapacity, "SnapshotReadFailed");
            input = input with { NowUtc = clock.GetUtcNow(), DemandKnown = false, DriverCapacityKnown = false, SnapshotAtUtc = null };
            return Result("SnapshotReadFailed");
        }
    }

    private static bool Current(음식배달수요참여결과? value, DateTimeOffset now)
        => value is { 데이터상태Code: 음식배달수요자료상태Code.최신, 예상업무량건수: >= 0m,
            기존기사가용처리건수: >= 0m, 보호활동기사수: >= 0, 추가참여인원: >= 0 }
            && value.정책판본 == 음식배달수요참여Policy.현재정책판본 && value.판정시각Utc <= now
            && now - value.판정시각Utc <= TimeSpan.FromMinutes(5) && value.기사판정목록 is not null
            && value.기사판정목록.All(x => x is not null && !string.IsNullOrWhiteSpace(x.기사Id) && x.우선순위 > 0)
            && value.기사판정목록.Select(x => x.기사Id).Distinct(StringComparer.Ordinal).Count() == value.기사판정목록.Count
            && value.기사판정목록.Select(x => x.우선순위).Distinct().Count() == value.기사판정목록.Count;

    private static bool Market(운송원장 value) => value.원본의뢰유형 is 운송의뢰배차원천유형.살뜰마트주문 or 운송의뢰배차원천유형.살뜰마트음식주문;
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static bool Coordinate(decimal? latitude, decimal? longitude)
        => latitude is >= -90m and <= 90m && longitude is >= -180m and <= 180m;

    // 동일·인접 셀의 가까운 두 지점은 조합 검토 기회일 뿐 시간·적재 약속 통과 근거가 아닙니다.
    private static bool Compatible(운송원장 source, 운송원장 candidate)
        => Coordinate(source.하차_위도, source.하차_경도) && Coordinate(candidate.하차_위도, candidate.하차_경도)
            && Distance(source.픽업_위도!.Value, source.픽업_경도!.Value, candidate.픽업_위도!.Value, candidate.픽업_경도!.Value) <= 1m
            && Distance(source.하차_위도!.Value, source.하차_경도!.Value, candidate.하차_위도!.Value, candidate.하차_경도!.Value) <= 2m;

    private static decimal Distance(decimal latitudeA, decimal longitudeA, decimal latitudeB, decimal longitudeB)
    {
        const double radians = Math.PI / 180d;
        var latitudeDelta = (double)(latitudeB - latitudeA) * radians;
        var longitudeDelta = (double)(longitudeB - longitudeA) * radians;
        var a = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d) + Math.Cos((double)latitudeA * radians)
            * Math.Cos((double)latitudeB * radians) * Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        return (decimal)(6371d * 2d * Math.Asin(Math.Sqrt(Math.Clamp(a, 0d, 1d))));
    }
}
