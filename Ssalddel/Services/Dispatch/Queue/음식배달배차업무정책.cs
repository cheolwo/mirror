using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Pricing;
using 살뜰.Services.Dispatch.AI;

namespace 살뜰.Services.Dispatch.Queue;

public sealed class 음식배달배차업무정책 : I배차업무정책
{
    private readonly I국내화물운송기사상태Store _기사상태Store;
    private readonly IDriverRejectedRequestStore _거절Store;
    private readonly I배차추천경로Service _경로Service;
    private readonly I음식배달권실행공간Store _음식배달권실행공간Store;
    private readonly ISsalddelFoodOrderStore _음식주문Store;
    private readonly 배차큐정책Options _options;
    private readonly I운영배차공통UseCase _수신의사;
    private readonly IAgreedRateTableUseCase? _agreedRates;
    private readonly I기사권역복귀Service? _권역복귀;
    private readonly I음식권역복귀일정평가Service? _음식일정;
    private readonly I음식배달수요참여Service? _수요참여;
    private readonly IFoodDispatchCandidateAI? _candidateAI;
    private readonly ISharedDriverWorkAdmissionService? _sharedAdmission;

    public 음식배달배차업무정책(
        I국내화물운송기사상태Store 기사상태Store,
        IDriverRejectedRequestStore 거절Store,
        I배차추천경로Service 경로Service,
        I음식배달권실행공간Store 음식배달권실행공간Store,
        ISsalddelFoodOrderStore 음식주문Store,
        IOptions<배차큐정책Options> options,
        I운영배차공통UseCase 수신의사,
        IAgreedRateTableUseCase? agreedRates = null,
        I기사권역복귀Service? 권역복귀 = null,
        I음식권역복귀일정평가Service? 음식일정 = null,
        I음식배달수요참여Service? 수요참여 = null,
        IFoodDispatchCandidateAI? candidateAI = null,
        ISharedDriverWorkAdmissionService? sharedAdmission = null)
    {
        _기사상태Store = 기사상태Store;
        _거절Store = 거절Store;
        _경로Service = 경로Service;
        _음식배달권실행공간Store = 음식배달권실행공간Store;
        _음식주문Store = 음식주문Store;
        _options = options.Value;
        _수신의사 = 수신의사;
        _agreedRates = agreedRates;
        _권역복귀 = 권역복귀;
        _음식일정 = 음식일정;
        _수요참여 = 수요참여;
        _candidateAI = candidateAI;
        _sharedAdmission = sharedAdmission;
    }

    public int 배차업무유형 => 상태값.배차업무유형.음식배달;

    public async Task<배차추천후보?> 다음후보선정Async(
        운송원장 queue,
        string? 제외기사Id = null,
        CancellationToken cancellationToken = default)
    {
        var review = await 후보검토Async(queue, 제외기사Id, cancellationToken);
        if (_candidateAI?.Enabled != true || review.선정후보 is null)
            return review.선정후보;

        FoodDispatchAISelection decision;
        try
        {
            decision = await _candidateAI.SelectAsync(review, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            decision = new(null, "Unavailable");
        }
        cancellationToken.ThrowIfCancellationRequested();

        // 모델 응답을 기다린 동안 수신·위치·거절·수요가 바뀔 수 있다. 모델을 다시 부르지 않고 관문만 재실행한다.
        // 이 재조회 자체가 실패하면 기존 저장소 오류를 전파하며 오래된 후보로 숨기지 않는다.
        var current = await 후보검토Async(queue, 제외기사Id, cancellationToken);
        if (current.선정후보 is null) return null;
        var initiallyAllowed = FoodDispatchCandidateConstraints.Eligible(review);
        var currentlyAllowed = FoodDispatchCandidateConstraints.Eligible(current);
        var selected = decision.StatusCode == "Applied"
            && initiallyAllowed.Any(x => string.Equals(x.기사Id, decision.DriverId, StringComparison.Ordinal))
            ? currentlyAllowed.FirstOrDefault(x => string.Equals(x.기사Id, decision.DriverId, StringComparison.Ordinal))
            : null;
        if (selected is null)
        {
            var status = decision.StatusCode switch
            {
                "Disabled" or "InvalidConfiguration" or "NoCandidates" or "SingleCandidate" or "HttpFailure"
                    or "ResponseTooLarge" or "InvalidResponse" or "Timeout" or "NetworkFailure" or "Unavailable"
                    => decision.StatusCode,
                _ => "CandidateChanged"
            };
            return current.선정후보 with { 추천사유 = $"{current.선정후보.추천사유} · FoodDispatchAI.{status}" };
        }

        if (string.Equals(current.선정후보.DriverId, selected.기사Id, StringComparison.Ordinal))
            return current.선정후보 with { 추천사유 = $"{current.선정후보.추천사유} · FoodDispatchAI.Applied" };
        return new(selected.기사Id, selected.추천점수!.Value,
            $"{selected.공간설명} · 음식점까지 {selected.거리Km:0.0}km · 픽업 단계 {selected.픽업단계}"
            + $" · 도착 차이 {selected.도착차이분:0.0}분 · F드라이버 대기 보정 {selected.대기점수:0} · FoodDispatchAI.Applied");
    }

    /// <summary>기존 후보 관문과 정렬을 실행하고 실제 확인 범위만 반환한다. 추천 확정은 수행하지 않는다.</summary>
    public async Task<음식배달후보검토결과> 후보검토Async(
        운송원장 queue,
        string? 제외기사Id = null,
        CancellationToken cancellationToken = default)
    {
        var 조회공간 = new List<string>();
        var 검토항목 = new List<음식배달후보검토항목>();
        if (!queue.픽업_위도.HasValue || !queue.픽업_경도.HasValue)
        {
            return new(null, "unknown", 조회공간.ToArray(), 검토항목.ToArray(), [], false, false, false,
                "PickupCoordinatesMissing");
        }

        string? selectedDriver;
        try
        {
            selectedDriver = _agreedRates is null ? null : await _agreedRates.SelectedDriverAsync(
                AgreedRateBusinessKinds.FoodDelivery, queue.원본의뢰Id, cancellationToken);
        }
        catch (AgreedRateTableException)
        {
            return new(null, "unknown", 조회공간.ToArray(), 검토항목.ToArray(), [], false, false, false,
                "AgreedRateUnavailable");
        }
        IReadOnlyList<string> EligibleIds(IEnumerable<string> ids)
            => selectedDriver is null ? ids.ToArray() : ids.Where(id => string.Equals(id, selectedDriver, StringComparison.Ordinal)).ToArray();

        var pickup = new 배차경로좌표(queue.픽업_위도.Value, queue.픽업_경도.Value);
        var rejectedDriverIds = await _거절Store.GetRejectedDriverIdsAsync(
            queue.의뢰Id,
            cancellationToken);
        var now = DateTime.UtcNow;
        var 조리예상완료시각Utc = Resolve조리예상완료시각Utc(queue);
        var seenDriverIds = new HashSet<string>(StringComparer.Ordinal);
        var 평가목록 = new List<음식배달기사항목>();
        var 주배달권 = 음식배달권정책.판정(pickup, queue.픽업_도로명주소);
        var participation = _수요참여 is null ? null : await _수요참여.평가Async(queue, cancellationToken);
        var decisions = participation?.데이터상태Code == 음식배달수요자료상태Code.최신
            ? participation.기사판정목록.ToDictionary(x => x.기사Id, StringComparer.Ordinal) : null;

        if (!string.Equals(주배달권.배달권키, "unknown", StringComparison.Ordinal))
        {
            조회공간.Add(주배달권.배달권키);
            var 주배달권공간 = await _음식배달권실행공간Store.GetAsync(
                주배달권.배달권키,
                cancellationToken);
            평가목록.AddRange(await 평가Async(
                EligibleIds(주배달권공간?.운행중기사Ids ?? []),
                queue,
                seenDriverIds,
                pickup,
                rejectedDriverIds,
                제외기사Id,
                조리예상완료시각Utc,
                now,
                공간단계: 0,
                공간설명: "같은 음식배달권",
                decisions,
                검토항목,
                cancellationToken));

            var 인접탐색필요 = 평가목록.Count < Math.Max(1, _options.음식배달동일권최소후보기사수);
            if (decisions is not null || 인접탐색필요 || (_권역복귀 is not null && _음식일정 is not null))
            {
                var 인접기사Ids = new List<string>();
                foreach (var 인접배달권키 in 음식배달권정책.인접배달권키조회(주배달권.배달권키))
                {
                    조회공간.Add(인접배달권키);
                    var 인접공간 = await _음식배달권실행공간Store.GetAsync(
                        인접배달권키,
                        cancellationToken);
                    if (인접공간 is not null)
                    {
                        인접기사Ids.AddRange(인접공간.운행중기사Ids);
                    }
                }

                var 인접평가 = await 평가Async(
                    EligibleIds(인접기사Ids),
                    queue,
                    seenDriverIds,
                    pickup,
                    rejectedDriverIds,
                    제외기사Id,
                    조리예상완료시각Utc,
                    now,
                    공간단계: 1,
                    공간설명: "인접 음식배달권",
                    decisions,
                    검토항목,
                    cancellationToken);
                // 복귀 가점이 확인된 경우에만 충분한 동일권 후보와 인접 후보를 함께 비교한다.
                // OFF/미확인은 기존 권역 선택을 바꾸지 않는다. 접근 반경 관문은 평가에서 유지된다.
                if (decisions is not null || 인접탐색필요 || 평가목록.Concat(인접평가).Any(x => x.복귀평가 is { 확인됨: true, 가점: > 0m }))
                    평가목록.AddRange(인접평가);
                else
                    foreach (var item in 인접평가) 판정갱신(검토항목, item.기사.DriverId, "AdjacentScopeNotIncluded");
            }
        }

        if (평가목록.Count == 0)
        {
            var 확장기사Ids = new List<string>();
            foreach (var 확장공간키 in 음식배달권정책.거리확장배달권키조회(
                         주배달권.배달권키,
                         _options.음식배달후보검색반경Km))
            {
                조회공간.Add(확장공간키);
                var 확장공간 = await _음식배달권실행공간Store.GetAsync(
                    확장공간키,
                    cancellationToken);
                if (확장공간 is not null)
                {
                    확장기사Ids.AddRange(확장공간.운행중기사Ids);
                }
            }

            평가목록.AddRange(await 평가Async(
                EligibleIds(확장기사Ids),
                queue,
                seenDriverIds,
                pickup,
                rejectedDriverIds,
                제외기사Id,
                조리예상완료시각Utc,
                now,
                공간단계: 2,
                공간설명: "제한 음식배달공간 확장",
                decisions,
                검토항목,
                cancellationToken));
        }

        var demandFallback = false;
        var pickupTimelinessFallback = false;
        if (decisions is not null)
        {
            foreach (var item in 평가목록.Where(x => decisions.TryGetValue(x.기사.DriverId, out var decision)
                         && decision.상태Code == 음식배달수요참여상태Code.적격조건미충족))
                판정갱신(검토항목, item.기사.DriverId, "DemandIneligible");
            평가목록 = 평가목록.Where(x => !decisions.TryGetValue(x.기사.DriverId, out var decision)
                || decision.상태Code != 음식배달수요참여상태Code.적격조건미충족).ToList();
            var anyAdmitted = 평가목록.Any(x => decisions.TryGetValue(x.기사.DriverId, out var decision)
                && decision.신규제안허용);
            // 수요 우선권 때문에 제시간에 픽업할 후보를 버리고 지연 후보를 선택하지 않는다.
            if (평가목록.Count > 0)
            {
                var bestPickupStage = 평가목록.Min(x => x.평가.단계);
                foreach (var item in 평가목록.Where(x => x.평가.단계 != bestPickupStage))
                    판정갱신(검토항목, item.기사.DriverId, "PickupTimelinessExcluded");
                평가목록 = 평가목록.Where(x => x.평가.단계 == bestPickupStage).ToList();
            }
            var admitted = 평가목록.Where(x => decisions.TryGetValue(x.기사.DriverId, out var decision)
                && decision.신규제안허용).ToList();
            // 참여 권고는 주문을 방치하는 하드 정원이 아니다. 기존 물리·수신 관문을 통과한 후보만 재탐색한다.
            if (admitted.Count > 0)
            {
                foreach (var item in 평가목록.Except(admitted))
                    판정갱신(검토항목, item.기사.DriverId, "DemandParticipationExcluded");
                평가목록 = admitted;
            }
            else
            {
                demandFallback = 평가목록.Count > 0;
                pickupTimelinessFallback = demandFallback && anyAdmitted;
            }
        }
        var best = decisions is not null
            ? 평가목록.OrderBy(x => x.평가.단계)
                .ThenBy(x => decisions.TryGetValue(x.기사.DriverId, out var decision) ? decision.우선순위 : int.MaxValue)
                .ThenByDescending(x => x.추천점수).ThenBy(x => x.평가.차이분)
                .ThenBy(x => x.공간단계).ThenBy(x => x.거리Km)
                .ThenBy(x => x.기사.DriverId, StringComparer.Ordinal).FirstOrDefault()
            : 평가목록.Any(x => x.복귀평가 is { 확인됨: true, 가점: > 0m })
            ? 평가목록.OrderBy(x => x.평가.단계).ThenByDescending(x => x.추천점수)
                .ThenBy(x => x.평가.차이분).ThenBy(x => x.공간단계).ThenBy(x => x.거리Km)
                .ThenBy(x => x.기사.DriverId, StringComparer.Ordinal).FirstOrDefault()
            : Ssalddel.WorkflowRules.음식배달픽업평가Policy.정렬(
                평가목록, x => x.평가, x => x.공간단계, x => x.거리Km, x => x.기사.DriverId).FirstOrDefault();
        foreach (var item in 평가목록) 판정갱신(검토항목, item.기사.DriverId, "CandidateAllowed");
        var selected = best is null
            ? null
            : new 배차추천후보(
                best.기사.DriverId,
                best.추천점수,
                $"{best.공간설명} · 음식점까지 {best.거리Km:0.0}km · {best.픽업시각설명} · F드라이버 대기 보정 {best.기사.Aging점수:0}"
                + (best.복귀평가 is null ? string.Empty : $" · {best.복귀평가.사유}")
                + (decisions is null ? string.Empty : demandFallback
                    ? pickupTimelinessFallback
                        ? " · 수요 참여 예외: 픽업 적시성 우선"
                        : " · 수요 참여 예외: 적격 활동 기사 없음"
                    : $" · 수요 참여: {decisions[best.기사.DriverId].사유Code}"));
        return new(selected, 주배달권.배달권키, 조회공간.ToArray(), 검토항목.ToArray(),
            평가목록.Select(x => x.기사.DriverId).ToArray(), decisions is not null, demandFallback,
            pickupTimelinessFallback, best is null ? "NoEligibleCandidate" : null);
    }

    private async Task<IReadOnlyList<음식배달기사항목>> 평가Async(
        IReadOnlyList<string> driverIds,
        운송원장 queue,
        HashSet<string> seenDriverIds,
        배차경로좌표 pickup,
        IReadOnlySet<string> rejectedDriverIds,
        string? 제외기사Id,
        DateTime? 조리예상완료시각Utc,
        DateTime now,
        int 공간단계,
        string 공간설명,
        IReadOnlyDictionary<string, 음식배달수요기사판정>? decisions,
        List<음식배달후보검토항목> 검토항목,
        CancellationToken cancellationToken)
    {
        IEnumerable<string> orderedIds = driverIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && !seenDriverIds.Contains(id)
                && 수요적격관문(id))
            .Distinct(StringComparer.Ordinal);
        bool 수요적격관문(string id)
        {
            if (decisions is null || !decisions.TryGetValue(id, out var decision)
                || decision.상태Code != 음식배달수요참여상태Code.적격조건미충족) return true;
            검토기록(검토항목, id, 공간단계, 공간설명, "DemandIneligible", null, null, decisions);
            return false;
        }
        if (decisions is not null)
            orderedIds = orderedIds.OrderBy(id => decisions.TryGetValue(id, out var decision) && decision.신규제안허용 ? 0 : 1)
                .ThenBy(id => decisions.TryGetValue(id, out var decision) ? decision.우선순위 : int.MaxValue)
                .ThenBy(id => id, StringComparer.Ordinal);
        var uniqueIds = orderedIds
            .Take(Math.Max(1, _options.음식배달후보최대조회수))
            .ToArray();
        seenDriverIds.UnionWith(uniqueIds);
        if (uniqueIds.Length == 0)
        {
            return [];
        }

        var states = await Task.WhenAll(
            uniqueIds.Select(id => _기사상태Store.GetAsync(id, cancellationToken)));
        for (var i = 0; i < states.Length; i++)
            if (states[i] is null)
                검토기록(검토항목, uniqueIds[i], 공간단계, 공간설명, "DriverStateMissing", null, null, decisions);
        var receiving = new List<국내화물운송기사상태Snapshot>();
        // 공통 원장은 같은 scoped DbContext를 사용하므로 병렬 조회하지 않는다.
        foreach (var state in states.Where(state => state is not null))
        {
            var availability = await _수신의사.수신상태조회Async(state!.DriverId, cancellationToken);
            if (살뜰.도메인.배차.운영배차수신상태Policy.신규배차수신허용(availability))
                receiving.Add(state);
            else
                검토기록(검토항목, state.DriverId, 공간단계, 공간설명, "DriverReceivingOff", state, null, decisions);
        }
        var evaluated = await 평가Async(
            receiving,
            null,
            pickup,
            rejectedDriverIds,
            제외기사Id,
            조리예상완료시각Utc,
            now,
            공간단계,
            공간설명,
            검토항목,
            decisions,
            queue,
            cancellationToken);
        if (_권역복귀 is null || _음식일정 is null) return evaluated;
        var result = new List<음식배달기사항목>();
        foreach (var item in evaluated)
        {
            var point = new 배차경로좌표(item.기사.Latitude!.Value, item.기사.Longitude!.Value);
            var state = await _권역복귀.상태조회Async(item.기사.DriverId, point,
                item.기사.위치기록시각Utc, item.기사.AccuracyM, cancellationToken);
            if (!state.정책선택됨) { result.Add(item); continue; }
            // 픽업 지연 후보를 복귀 가점으로 살리지 않는다.
            if (!state.활성여부 || item.평가.단계 == 2)
            {
                result.Add(item with { 복귀평가 = 기사권역복귀평가.보류(state.활성여부 ? "픽업 지연 후보에는 복귀 가점을 적용하지 않습니다." : state.사유) });
                continue;
            }
            var plan = await _음식일정.평가Async(item.기사.DriverId, queue, point, now, cancellationToken);
            var returnEvaluation = plan.적격여부
                ? await _권역복귀.평가Async(state, plan.기존마지막좌표, plan.후보마지막좌표,
                    plan.전체실경로확인됨, plan.적격여부, plan.RouteOption, cancellationToken)
                : 기사권역복귀평가.보류(plan.사유);
            result.Add(item with { 복귀평가 = returnEvaluation });
        }
        foreach (var item in result)
            점수갱신(검토항목, item.기사.DriverId, item.추천점수);
        return result;
    }

    private async Task<IReadOnlyList<음식배달기사항목>> 평가Async(
        IReadOnlyList<국내화물운송기사상태Snapshot> candidates,
        HashSet<string>? seenDriverIds,
        배차경로좌표 pickup,
        IReadOnlySet<string> rejectedDriverIds,
        string? 제외기사Id,
        DateTime? 조리예상완료시각Utc,
        DateTime now,
        int 공간단계,
        string 공간설명,
        List<음식배달후보검토항목> 검토항목,
        IReadOnlyDictionary<string, 음식배달수요기사판정>? decisions,
        운송원장 queue,
        CancellationToken cancellationToken)
    {
        var result = new List<음식배달기사항목>();
        foreach (var candidate in candidates)
        {
            if (seenDriverIds is not null && !seenDriverIds.Add(candidate.DriverId))
            {
                continue;
            }

            var blocked = !string.Equals(candidate.AppKey, 기사앱식별자.FoodDeliveryDriverApp, StringComparison.Ordinal)
                ? "DriverAppMismatch"
                : !string.Equals(candidate.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase)
                    ? "DriverNotDriving"
                : !candidate.Latitude.HasValue || !candidate.Longitude.HasValue ? "DriverLocationMissing"
                : !기사위치신선도정책.유효한가(candidate.위치수신시각Utc, now, _options.기사위치유효시간분)
                    ? "DriverLocationStale"
                : rejectedDriverIds.Contains(candidate.DriverId) ? "DriverRejected"
                : !string.IsNullOrWhiteSpace(제외기사Id)
                    && string.Equals(candidate.DriverId, 제외기사Id, StringComparison.Ordinal) ? "DriverExcluded"
                : null;
            if (blocked is not null)
            {
                검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, blocked, candidate, null, decisions);
                continue;
            }

            if (_sharedAdmission is not null)
            {
                var participation = await _sharedAdmission.CheckRecommendationAsync(candidate.DriverId, queue, cancellationToken);
                if (participation.IsFailed)
                {
                    var code = participation.Errors.FirstOrDefault()?.Metadata.GetValueOrDefault("ErrorCode")?.ToString()
                        ?? "DriverWorkNotSelected";
                    검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, code, candidate, null, decisions);
                    continue;
                }
            }

            var current = new 배차경로좌표(candidate.Latitude!.Value, candidate.Longitude!.Value);
            음식후속픽업기준결과? basis = null;
            decimal? travelMinutes = null;
            string? routeBasis = null;
            decimal? distance;
            if (_options.음식배달수행종료기준사용)
            {
                basis = _음식일정 is null ? 음식후속픽업기준결과.보류("ActiveScheduleServiceUnavailable")
                    : await _음식일정.후속픽업기준조회Async(candidate.DriverId, queue, current, now, cancellationToken);
                if (!basis.확인됨 || basis.기준좌표 is null || !basis.가용예상시각Utc.HasValue
                    || basis.가용예상시각Utc.Value < now)
                {
                    검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, basis.사유Code,
                        candidate, null, decisions, basis: basis);
                    continue;
                }
                if (basis.활성업무있음)
                {
                    if (!조리예상완료시각Utc.HasValue)
                    {
                        검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "ActiveScheduleReadyTimeMissing",
                            candidate, null, decisions, basis: basis);
                        continue;
                    }
                    배차경로예상결과? route = null;
                    if (basis.RouteOption is "trafast" or "tracomfort" or "traoptimal" or "traavoidtoll" or "traavoidcaronly")
                    {
                        try { route = await _경로Service.EstimateRouteAsync(basis.기준좌표, pickup, basis.RouteOption, cancellationToken); }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                        catch (Exception) { /* 경로 실패는 아래의 명시적 신규 후보 제외로 남긴다. */ }
                    }
                    if (route is not { 실제경로여부: true, DistanceKm: >= 0m, Duration: not null }
                        || route.Duration.Value < TimeSpan.Zero || route.RouteOption != basis.RouteOption)
                    {
                        검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "ActiveSchedulePickupRouteUnverified",
                            candidate, null, decisions, basis: basis);
                        continue;
                    }
                    distance = route.DistanceKm;
                    travelMinutes = (decimal)(basis.가용예상시각Utc.Value - now).TotalMinutes + (decimal)route.Duration.Value.TotalMinutes;
                    routeBasis = "VerifiedVehicleRoute";
                }
                else
                {
                    distance = _경로Service.CalculateDistanceKm(current, pickup);
                    routeBasis = "CurrentLocationEstimate";
                }
            }
            else distance = _경로Service.CalculateDistanceKm(current, pickup);
            if (!distance.HasValue)
            {
                검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "DistanceUnavailable", candidate, null, decisions, basis: basis);
                continue;
            }

            var allowedRadiusKm = Math.Min(
                Math.Max(1m, _options.음식배달후보검색반경Km),
                Math.Max(1m, candidate.상차접근허용반경Km ?? _options.음식배달후보검색반경Km));
            if (distance.Value > allowedRadiusKm)
            {
                검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "OutsideApproachRadius", candidate, null, decisions, distance, basis, travelMinutes, routeBasis);
                continue;
            }

            var 픽업시각 = travelMinutes.HasValue
                ? Ssalddel.WorkflowRules.음식배달픽업평가Policy.예상소요시간판정(
                    distance.Value, candidate.Aging점수, 조리예상완료시각Utc, now, travelMinutes.Value,
                    _options.음식배달픽업조기도착허용분, _options.음식배달픽업지연허용분)
                : Ssalddel.WorkflowRules.음식배달픽업평가Policy.판정(
                distance.Value, candidate.Aging점수, 조리예상완료시각Utc, now,
                _options.음식배달평균이동속도KmH,
                _options.음식배달픽업조기도착허용분, _options.음식배달픽업지연허용분);
            if (basis is { 활성업무있음: true } && 픽업시각.단계 == 2)
            {
                검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "ActiveSchedulePickupLate",
                    candidate, 픽업시각, decisions, distance, basis, travelMinutes, routeBasis);
                continue;
            }
            검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "Eligible", candidate, 픽업시각, decisions, distance, basis, travelMinutes, routeBasis);
            result.Add(new 음식배달기사항목(
                candidate,
                distance.Value,
                공간단계,
                공간설명,
                픽업시각));
        }

        return result;
    }

    private static void 검토기록(List<음식배달후보검토항목> records, string driverId, int spaceStage,
        string spaceDescription, string code, 국내화물운송기사상태Snapshot? state,
        Ssalddel.WorkflowRules.음식배달픽업평가? evaluation,
        IReadOnlyDictionary<string, 음식배달수요기사판정>? decisions, decimal? distance = null,
        음식후속픽업기준결과? basis = null, decimal? travelMinutes = null, string? routeBasis = null)
    {
        var decision = decisions is not null && decisions.TryGetValue(driverId, out var found) ? found : null;
        var item = new 음식배달후보검토항목(driverId, spaceStage, spaceDescription, code, distance,
            evaluation?.단계, evaluation?.차이분, evaluation?.추천점수, state?.Aging점수,
            decision?.우선순위, decision?.사유Code)
        {
            위치기준Code = basis is null ? null : basis.확인됨
                ? basis.활성업무있음 ? "ActiveScheduleEnd" : "CurrentLocation" : "UnverifiedActiveSchedule",
            기존수행완료예상시각Utc = basis is { 활성업무있음: true, 확인됨: true } ? basis.가용예상시각Utc : null,
            픽업예상소요분 = travelMinutes,
            경로근거Code = routeBasis
        };
        var index = records.FindIndex(x => string.Equals(x.기사Id, driverId, StringComparison.Ordinal));
        if (index < 0) records.Add(item);
        else records[index] = item;
    }

    private static void 판정갱신(List<음식배달후보검토항목> records, string driverId, string code)
    {
        var index = records.FindIndex(x => string.Equals(x.기사Id, driverId, StringComparison.Ordinal));
        if (index >= 0) records[index] = records[index] with { 판정Code = code };
    }

    private static void 점수갱신(List<음식배달후보검토항목> records, string driverId, decimal score)
    {
        var index = records.FindIndex(x => string.Equals(x.기사Id, driverId, StringComparison.Ordinal));
        if (index >= 0) records[index] = records[index] with { 추천점수 = score };
    }



    private DateTime? Resolve조리예상완료시각Utc(운송원장 queue)
    {
        var orderId = string.IsNullOrWhiteSpace(queue.원본의뢰Id)
            ? queue.의뢰Id
            : queue.원본의뢰Id;
        var value = _음식주문Store.GetOrder(orderId)?.조리예상완료시각Utc;
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private sealed record 음식배달기사항목(
        국내화물운송기사상태Snapshot 기사,
        decimal 거리Km,
        int 공간단계,
        string 공간설명,
        Ssalddel.WorkflowRules.음식배달픽업평가 평가,
        기사권역복귀평가? 복귀평가 = null)
    {
        public string 픽업시각설명 => 평가.설명;
        public decimal 추천점수 => 평가.추천점수 + (복귀평가 is { 확인됨: true } ? 복귀평가.가점 : 0m);
    }
}
