using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.운송;
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Pricing;
using static 살뜰.Services.Dispatch.Queue.음식배달후보검토기록;

namespace 살뜰.Services.Dispatch.Queue;

/// <summary>같은 조회 기준시각으로 공간을 순차 탐색하고 현재 기사 상태와 수신 의사를 준비한다.</summary>
internal sealed class 음식배달후보준비Service
{
    private readonly I국내화물운송기사상태Store _기사상태Store;
    private readonly IDriverRejectedRequestStore _거절Store;
    private readonly I음식배달권실행공간Store _음식배달권실행공간Store;
    private readonly ISsalddelFoodOrderStore _음식주문Store;
    private readonly 배차큐정책Options _options;
    private readonly I운영배차공통UseCase _수신의사;
    private readonly IAgreedRateTableUseCase? _agreedRates;
    private readonly I기사권역복귀Service? _권역복귀;
    private readonly I음식권역복귀일정평가Service? _음식일정;
    private readonly I음식배달수요참여Service? _수요참여;
    private readonly 음식배달후보평가Service _평가Service;

    internal 음식배달후보준비Service(I국내화물운송기사상태Store 기사상태Store,
        IDriverRejectedRequestStore 거절Store, I음식배달권실행공간Store 공간Store,
        ISsalddelFoodOrderStore 음식주문Store, 배차큐정책Options options,
        I운영배차공통UseCase 수신의사, IAgreedRateTableUseCase? agreedRates,
        I기사권역복귀Service? 권역복귀, I음식권역복귀일정평가Service? 음식일정,
        I음식배달수요참여Service? 수요참여, 음식배달후보평가Service 평가Service)
    {
        _기사상태Store = 기사상태Store;
        _거절Store = 거절Store;
        _음식배달권실행공간Store = 공간Store;
        _음식주문Store = 음식주문Store;
        _options = options;
        _수신의사 = 수신의사;
        _agreedRates = agreedRates;
        _권역복귀 = 권역복귀;
        _음식일정 = 음식일정;
        _수요참여 = 수요참여;
        _평가Service = 평가Service;
    }

    /// <summary>기존 후보 관문과 정렬을 실행하고 실제 확인 범위만 반환한다. 추천 확정은 수행하지 않는다.</summary>
    internal async Task<음식배달후보검토결과> 준비Async(
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
        var best = 음식배달후보순위Policy.선택(평가목록, decisions);
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
        var evaluated = await _평가Service.평가Async(
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
        return await _평가Service.복귀평가Async(evaluated, queue, now, 검토항목, cancellationToken);
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

}
