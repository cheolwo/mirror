using Ssalddel.Contracts.Food;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Application.Food;

public static class 음식배달수명주기SnapshotFactory
{
    public static 음식배달수명주기Snapshot FromOperationalOrder(
        음식주문응답 order,
        string sourceRevision,
        string? driverStableId = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new 음식배달수명주기Snapshot
        {
            SourceCode = 음식배달상태원천코드.OperationalServer,
            SourceRevision = sourceRevision ?? string.Empty,
            OrderStableId = order.주문번호,
            OrderRevision = order.Revision,
            OrderStateCode = order.상태,
            DispatchStateCode = order.배차상태,
            RestaurantStableId = $"restaurant:{order.음식점Id}",
            OrdererStableId = order.주문자UserId,
            DriverStableId = driverStableId?.Trim() ?? string.Empty,
            AcceptedAtUtc = order.음식점수락시각Utc,
            ReadyForPickupAtUtc = order.CurrentPreparationRound > 1
                ? order.CurrentPickupReadyAtUtc
                : order.CurrentPickupReadyAtUtc ?? order.픽업준비시각Utc,
            DispatchRequestedAtUtc = order.배차요청시각Utc,
            PickedUpAtUtc = TransitionAt(order, 음식주문상태코드.픽업완료),
            DeliveredAtUtc = TransitionAt(order, 음식주문상태코드.전달완료),
            ReceiptConfirmedAtUtc = TransitionAt(order, 음식주문상태코드.수령확인),
            SourceRefs = ["api/v1/food-orders/" + order.주문번호]
        };
    }

    private static DateTime? TransitionAt(음식주문응답 order, string state)
    {
        var history = order.상태이력.OrderBy(x => x.전이시각Utc).ToArray();
        var boundary = Array.FindLastIndex(history, x =>
            x.다음상태 == 음식주문상태코드.조리중
            && x.사유 == "픽업 후 배달 중단 · 재조리·재배차");
        // 같은 시각으로 저장된 사건도 이력 순서로 새 음식의 픽업을 구분합니다.
        return history.Skip(boundary + 1)
            .Where(x => x.다음상태 == state)
            .Where(x => boundary >= 0 || !order.RecookingRequestedAtUtc.HasValue
                        || x.전이시각Utc > order.RecookingRequestedAtUtc.Value)
            .Select(x => (DateTime?)x.전이시각Utc).FirstOrDefault();
    }
}
