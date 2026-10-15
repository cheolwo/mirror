using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.AI;

public sealed partial class FoodDispatchTimingService
{
    // 이 값은 원천 재조회 비교용 사본이며 주소·메뉴·금액을 AI 입력이나 로그에 전달하지 않습니다.
    private sealed record OrderInformationSource(string OrderId, long Revision, string State, string DispatchState, long? QueueId,
        int PreparationRound, DateTimeOffset? CookingStartedAtUtc, DateTimeOffset? ReadyAtUtc,
        DateTimeOffset? RecookingRequestedAtUtc, DateTimeOffset? EstimatedReadyAtUtc, int? EstimatedMinutes,
        int DeclaredRound, DateTimeOffset? DeclaredCookingAtUtc, DateTimeOffset? DeclaredReadyAtUtc,
        DateTimeOffset? DeclaredRecookingAtUtc, DateTimeOffset? LegacyCookingAtUtc, DateTimeOffset? LegacyReadyAtUtc,
        bool PreparationValid)
    {
        // 재조리 전의 미래 예정이 남아 있을 수 있어 첫 차수의 정상 예정만 선택 상한으로 사용합니다.
        public DateTimeOffset? LatestDispatchAtUtc => PreparationValid && PreparationRound == 1 ? EstimatedReadyAtUtc : null;
    }

    private OrderInformationSource? ReadOrderInformation(운송원장 queue, TimingInformation information)
    {
        var source = ReadOrderSource(queue, out var orderReason, out var preparationValid);
        var observedAt = clock.GetUtcNow();
        if (source is null)
        {
            information.Missing(FoodDispatchInformationKeys.Order, orderReason);
            information.Missing(FoodDispatchInformationKeys.Preparation, orderReason);
            return null;
        }
        information.Current(FoodDispatchInformationKeys.Order, observedAt, source.Revision);
        if (preparationValid)
            information.Current(FoodDispatchInformationKeys.Preparation, observedAt, source.Revision);
        else
            information.Missing(FoodDispatchInformationKeys.Preparation, "InvalidPreparation", observedAt, source.Revision);
        return source;
    }

    private void RecheckOrderInformation(운송원장 queue, OrderInformationSource? captured, TimingInformation information)
    {
        if (captured is null) return; // 처음 부족했던 자료를 같은 평가 중 새 관측으로 승격하지 않습니다.
        var current = ReadOrderSource(queue, out _, out _, captured.OrderId);
        if (current == captured) return;
        // 처음 관측한 시각/판본을 유지해야 모델 대기 시간으로 유효기간을 다시 시작하지 않습니다.
        information.Stale(FoodDispatchInformationKeys.Order, "OrderChanged");
        information.Stale(FoodDispatchInformationKeys.Preparation, "PreparationChanged");
    }

    private OrderInformationSource? ReadOrderSource(운송원장 queue, out string reason, out bool preparationValid,
        string? capturedOrderId = null)
    {
        preparationValid = false;
        reason = "OrderSourceUnavailable";
        if (foodOrderStore is null) return null;
        var queueOrderId = string.IsNullOrWhiteSpace(queue.원본의뢰Id) ? queue.의뢰Id : queue.원본의뢰Id;
        var orderId = capturedOrderId ?? queueOrderId;
        var order = foodOrderStore.GetOrder(orderId);
        reason = "OrderNotFound";
        if (order is null) return null;
        reason = "OrderLinkMismatch";
        if (string.IsNullOrWhiteSpace(orderId) || !string.Equals(queueOrderId, orderId, StringComparison.Ordinal)
            || !string.Equals(order.주문번호, orderId, StringComparison.Ordinal)
            || order.배차대기Id != queue.Id || queue.Id <= 0 || order.Revision < 1) return null;
        reason = "OrderNotActive";
        if (order.상태 is not (음식주문상태코드.주문대기 or 음식주문상태코드.주문확인 or 음식주문상태코드.기사배정
            or 음식주문상태코드.조리중 or 음식주문상태코드.픽업대기)) return null;
        if (order.상태이력 is null || order.상태이력.Any(x => x is null || x.사유 is null))
        { reason = "InvalidPreparation"; return null; }

        // UTC·로컬·구형 unspecified 시각을 같은 축으로 정규화한 뒤 기존 현재 조리 정책을 재사용합니다.
        var preparationOrder = new 음식주문응답
        {
            조리시작시각Utc = NormalizeUtc(order.조리시작시각Utc)?.UtcDateTime,
            픽업준비시각Utc = NormalizeUtc(order.픽업준비시각Utc)?.UtcDateTime,
            상태이력 = order.상태이력.Select(x => new 음식주문상태전이기록Dto
            {
                다음상태 = x.다음상태, 사유 = x.사유,
                전이시각Utc = NormalizeUtc(x.전이시각Utc)!.Value.UtcDateTime
            }).ToArray()
        };
        var preparation = 음식주문현재조리Policy.계산(preparationOrder);
        var started = NormalizeUtc(preparation.CookingStartedAtUtc);
        var ready = NormalizeUtc(preparation.ReadyAtUtc);
        var recooking = NormalizeUtc(preparation.RecookingRequestedAtUtc);
        var now = clock.GetUtcNow();
        preparationValid = order.조리예상분 is null or >= 0
            && order.CurrentPreparationRound == preparation.Round
            && (!order.CurrentCookingStartedAtUtc.HasValue || NormalizeUtc(order.CurrentCookingStartedAtUtc) == started)
            && (!order.CurrentPickupReadyAtUtc.HasValue || NormalizeUtc(order.CurrentPickupReadyAtUtc) == ready)
            && (!order.RecookingRequestedAtUtc.HasValue || NormalizeUtc(order.RecookingRequestedAtUtc) == recooking)
            && !(started > now || ready > now || recooking > now)
            && !(started.HasValue && ready.HasValue && ready < started)
            && (order.상태 != 음식주문상태코드.조리중 || started.HasValue)
            && (order.상태 != 음식주문상태코드.픽업대기 || ready.HasValue);
        return new(orderId, order.Revision, order.상태, order.배차상태, order.배차대기Id,
            preparation.Round, started, ready, recooking, NormalizeUtc(order.조리예상완료시각Utc), order.조리예상분,
            order.CurrentPreparationRound, NormalizeUtc(order.CurrentCookingStartedAtUtc),
            NormalizeUtc(order.CurrentPickupReadyAtUtc), NormalizeUtc(order.RecookingRequestedAtUtc),
            NormalizeUtc(order.조리시작시각Utc), NormalizeUtc(order.픽업준비시각Utc), preparationValid);
    }

    private static DateTimeOffset? NormalizeUtc(DateTime? value) => value.HasValue
        ? new DateTimeOffset(value.Value.Kind == DateTimeKind.Local
            ? value.Value.ToUniversalTime() : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;

    private static void ObserveDemandInformation(TimingInformation information, 음식배달수요참여결과? demand)
    {
        var demandValid = demand is { 데이터상태Code: 음식배달수요자료상태Code.최신, 예상업무량건수: >= 0m,
            보호활동기사수: >= 0, 추가참여인원: >= 0 }
            && demand.정책판본 == 음식배달수요참여Policy.현재정책판본;
        var capacityValid = demandValid && demand!.기존기사가용처리건수 is >= 0m
            && demand.기사판정목록 is not null
            && demand.기사판정목록.All(x => x is not null && !string.IsNullOrWhiteSpace(x.기사Id) && x.우선순위 > 0)
            && demand.기사판정목록.Select(x => x.기사Id).Distinct(StringComparer.Ordinal).Count() == demand.기사판정목록.Count
            && demand.기사판정목록.Select(x => x.우선순위).Distinct().Count() == demand.기사판정목록.Count;
        // 미래/오래된 원래 관측시각은 순수 준비 정책이 Stale로 판정합니다. 현재 조회시각으로 바꾸지 않습니다.
        if (demandValid) information.Current(FoodDispatchInformationKeys.Demand, demand!.판정시각Utc);
        else information.Missing(FoodDispatchInformationKeys.Demand, "DemandUnknown", demand?.판정시각Utc);
        if (capacityValid) information.Current(FoodDispatchInformationKeys.DriverCapacity, demand!.판정시각Utc);
        else information.Missing(FoodDispatchInformationKeys.DriverCapacity, "DriverCapacityUnknown", demand?.판정시각Utc);
    }

    private sealed class TimingInformation
    {
        private readonly bool applicable;
        private readonly Dictionary<string, FoodDispatchInformationEvidence> evidence = new(StringComparer.Ordinal);

        public TimingInformation(bool applicable, 운송원장 queue, DateTimeOffset now)
        {
            this.applicable = applicable;
            Missing(FoodDispatchInformationKeys.Order, "OrderSourceUnavailable");
            Missing(FoodDispatchInformationKeys.Preparation, "NotCapturedAtTimingStage");
            if (Coordinate(queue.픽업_위도, queue.픽업_경도) && Coordinate(queue.하차_위도, queue.하차_경도))
                Current(FoodDispatchInformationKeys.Locations, now);
            else Missing(FoodDispatchInformationKeys.Locations, "LocationsMissing");
            Missing(FoodDispatchInformationKeys.OrderPool, "NotCapturedAtTimingStage");
            Missing(FoodDispatchInformationKeys.Demand, "DemandUnknown");
            Missing(FoodDispatchInformationKeys.DriverCapacity, "DriverCapacityUnknown");
            Missing(FoodDispatchInformationKeys.CustomerPromise, "CustomerPromiseNotCaptured");
            Missing(FoodDispatchInformationKeys.CostConsent, "CostConsentNotCaptured");
            Missing(FoodDispatchInformationKeys.DriverScheduleAndLoad, "DriverScheduleAndLoadNotEvaluated");
            Missing(FoodDispatchInformationKeys.DriverQuote, "DriverQuoteNotCalculated");
            Missing(FoodDispatchInformationKeys.PlatformFunds, "PlatformFundsNotRecorded");
        }

        public void Current(string key, DateTimeOffset observedAt, long? revision = null)
            => evidence[key] = new(key, FoodDispatchInformationStateCodes.Current, "InformationObserved", observedAt, revision);
        public void Missing(string key, string reason, DateTimeOffset? observedAt = null, long? revision = null)
            => evidence[key] = new(key, FoodDispatchInformationStateCodes.Missing, reason, observedAt, revision);
        public void Stale(string key, string reason)
        {
            var previous = evidence[key];
            evidence[key] = previous with { StateCode = FoodDispatchInformationStateCodes.Stale, ReasonCode = reason };
        }
        public FoodDispatchInformationReadiness Evaluate(DateTimeOffset now, int maxAgeSeconds)
            => new FoodDispatchInformationReadinessPolicy().Evaluate(applicable, now, maxAgeSeconds, evidence.Values.ToArray());
    }
}
