using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Pricing;

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
        I음식권역복귀일정평가Service? 음식일정 = null)
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
    }

    public int 배차업무유형 => 상태값.배차업무유형.음식배달;

    public async Task<배차추천후보?> 다음후보선정Async(
        운송원장 queue,
        string? 제외기사Id = null,
        CancellationToken cancellationToken = default)
    {
        if (!queue.픽업_위도.HasValue || !queue.픽업_경도.HasValue)
        {
            return null;
        }

        string? selectedDriver;
        try
        {
            selectedDriver = _agreedRates is null ? null : await _agreedRates.SelectedDriverAsync(
                AgreedRateBusinessKinds.FoodDelivery, queue.원본의뢰Id, cancellationToken);
        }
        catch (AgreedRateTableException) { return null; }
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

        if (!string.Equals(주배달권.배달권키, "unknown", StringComparison.Ordinal))
        {
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
                cancellationToken));

            var 인접탐색필요 = 평가목록.Count < Math.Max(1, _options.음식배달동일권최소후보기사수);
            if (인접탐색필요 || (_권역복귀 is not null && _음식일정 is not null))
            {
                var 인접기사Ids = new List<string>();
                foreach (var 인접배달권키 in 음식배달권정책.인접배달권키조회(주배달권.배달권키))
                {
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
                    cancellationToken);
                // 복귀 가점이 확인된 경우에만 충분한 동일권 후보와 인접 후보를 함께 비교한다.
                // OFF/미확인은 기존 권역 선택을 바꾸지 않는다. 접근 반경 관문은 평가에서 유지된다.
                if (인접탐색필요 || 평가목록.Concat(인접평가).Any(x => x.복귀평가 is { 확인됨: true, 가점: > 0m }))
                    평가목록.AddRange(인접평가);
            }
        }

        if (평가목록.Count == 0)
        {
            var 확장기사Ids = new List<string>();
            foreach (var 확장공간키 in 음식배달권정책.거리확장배달권키조회(
                         주배달권.배달권키,
                         _options.음식배달후보검색반경Km))
            {
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
                cancellationToken));
        }

        var best = 평가목록.Any(x => x.복귀평가 is { 확인됨: true, 가점: > 0m })
            ? 평가목록.OrderBy(x => x.평가.단계).ThenByDescending(x => x.추천점수)
                .ThenBy(x => x.평가.차이분).ThenBy(x => x.공간단계).ThenBy(x => x.거리Km)
                .ThenBy(x => x.기사.DriverId, StringComparer.Ordinal).FirstOrDefault()
            : Ssalddel.WorkflowRules.음식배달픽업평가Policy.정렬(
                평가목록, x => x.평가, x => x.공간단계, x => x.거리Km, x => x.기사.DriverId).FirstOrDefault();
        return best is null
            ? null
            : new 배차추천후보(
                best.기사.DriverId,
                best.추천점수,
                $"{best.공간설명} · 음식점까지 {best.거리Km:0.0}km · {best.픽업시각설명} · F드라이버 대기 보정 {best.기사.Aging점수:0}"
                + (best.복귀평가 is null ? string.Empty : $" · {best.복귀평가.사유}"));
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
        CancellationToken cancellationToken)
    {
        var uniqueIds = driverIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && seenDriverIds.Add(id))
            .Take(Math.Max(1, _options.음식배달후보최대조회수))
            .ToArray();
        if (uniqueIds.Length == 0)
        {
            return [];
        }

        var states = await Task.WhenAll(
            uniqueIds.Select(id => _기사상태Store.GetAsync(id, cancellationToken)));
        var receiving = new List<국내화물운송기사상태Snapshot>();
        // 공통 원장은 같은 scoped DbContext를 사용하므로 병렬 조회하지 않는다.
        foreach (var state in states.Where(state => state is not null))
        {
            var availability = await _수신의사.수신상태조회Async(state!.DriverId, cancellationToken);
            if (살뜰.도메인.배차.운영배차수신상태Policy.신규배차수신허용(availability))
                receiving.Add(state);
        }
        var evaluated = 평가(
            receiving,
            null,
            pickup,
            rejectedDriverIds,
            제외기사Id,
            조리예상완료시각Utc,
            now,
            공간단계,
            공간설명);
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
        return result;
    }

    private IReadOnlyList<음식배달기사항목> 평가(
        IReadOnlyList<국내화물운송기사상태Snapshot> candidates,
        HashSet<string>? seenDriverIds,
        배차경로좌표 pickup,
        IReadOnlySet<string> rejectedDriverIds,
        string? 제외기사Id,
        DateTime? 조리예상완료시각Utc,
        DateTime now,
        int 공간단계,
        string 공간설명)
    {
        var result = new List<음식배달기사항목>();
        foreach (var candidate in candidates)
        {
            if (seenDriverIds is not null && !seenDriverIds.Add(candidate.DriverId))
            {
                continue;
            }

            if (!string.Equals(candidate.AppKey, 기사앱식별자.FoodDeliveryDriverApp, StringComparison.Ordinal)
                || !string.Equals(candidate.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase)
                || !candidate.Latitude.HasValue
                || !candidate.Longitude.HasValue
                || !기사위치신선도정책.유효한가(
                    candidate.위치수신시각Utc,
                    now,
                    _options.기사위치유효시간분)
                || rejectedDriverIds.Contains(candidate.DriverId)
                || (!string.IsNullOrWhiteSpace(제외기사Id)
                    && string.Equals(candidate.DriverId, 제외기사Id, StringComparison.Ordinal)))
            {
                continue;
            }

            var distance = _경로Service.CalculateDistanceKm(
                new 배차경로좌표(candidate.Latitude.Value, candidate.Longitude.Value),
                pickup);
            if (!distance.HasValue)
            {
                continue;
            }

            var allowedRadiusKm = Math.Min(
                Math.Max(1m, _options.음식배달후보검색반경Km),
                Math.Max(1m, candidate.상차접근허용반경Km ?? _options.음식배달후보검색반경Km));
            if (distance.Value > allowedRadiusKm)
            {
                continue;
            }

            var 픽업시각 = Ssalddel.WorkflowRules.음식배달픽업평가Policy.판정(
                distance.Value, candidate.Aging점수, 조리예상완료시각Utc, now,
                _options.음식배달평균이동속도KmH,
                _options.음식배달픽업조기도착허용분, _options.음식배달픽업지연허용분);
            result.Add(new 음식배달기사항목(
                candidate,
                distance.Value,
                공간단계,
                공간설명,
                픽업시각));
        }

        return result;
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
