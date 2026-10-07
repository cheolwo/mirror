using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Application.Food;

public static class 음식배달가능행동Projector
{
    public static 음식주문응답 주문자용(음식주문응답 order)
    {
        order.Revision = ResolveRevision(order);
        order.AvailableActions = 주문자용(order.상태, order.Revision);
        return order;
    }

    public static 음식주문응답 음식점용(음식주문응답 order)
    {
        order.Revision = ResolveRevision(order);
        order.AvailableActions = 음식점용(order.상태, order.Revision,
            order.조리시작가능,
            order.CurrentCookingStartedAtUtc.HasValue || (order.CurrentPreparationRound == 1 && order.조리시작시각Utc.HasValue),
            order.CurrentPickupReadyAtUtc.HasValue || (order.CurrentPreparationRound == 1 && order.픽업준비시각Utc.HasValue));
        return order;
    }

    public static IReadOnlyList<업무가능행동Dto> 주문자용(string? status, long revision)
        => 음식주문상태코드.Normalize(status) switch
        {
            음식주문상태코드.주문대기 => [OrderAction(음식배달가능행동Ids.주문취소, revision)],
            음식주문상태코드.전달완료 => [Action(음식배달가능행동Ids.주문수령확인)],
            _ => []
        };

    public static IReadOnlyList<업무가능행동Dto> 음식점용(
        string? status, long revision, bool canStart = false, bool started = false, bool ready = false)
        => 음식주문상태코드.Normalize(status) switch
        {
            음식주문상태코드.주문대기 =>
            [
                Action(음식배달가능행동Ids.음식점주문수락),
                OrderAction(음식배달가능행동Ids.음식점주문거절, revision)
            ],
            음식주문상태코드.주문확인 => [OrderAction(음식배달가능행동Ids.음식점조리시간변경, revision)],
            음식주문상태코드.기사배정 when canStart =>
            [
                OrderAction(음식배달가능행동Ids.음식점조리시작, revision),
                OrderAction(음식배달가능행동Ids.음식점조리시간변경, revision)
            ],
            음식주문상태코드.조리중 or 음식주문상태코드.기사배정 when !ready && (started || status == 음식주문상태코드.조리중) =>
            [
                OrderAction(음식배달가능행동Ids.음식점조리시간변경, revision),
                OrderAction(음식배달가능행동Ids.음식점픽업준비완료, revision)
            ],
            _ => []
        };

    public static IReadOnlyList<업무가능행동Dto> 기사제안용(DateTime? expiresAtUtc)
        =>
        [
            Action(음식배달가능행동Ids.기사제안수락, expiresAtUtc),
            Action(음식배달가능행동Ids.기사제안거절, expiresAtUtc)
        ];

    public static IReadOnlyList<업무가능행동Dto> 기사배달용(
        string? workStatus,
        long attemptRevision,
        DateTime? restaurantArrivedAtUtc,
        bool pickupReady = true)
    {
        IReadOnlyList<업무가능행동Dto> actions = workStatus switch
        {
            DriverWorkOfferStatus.Accepted => BuildArrivalActions(
                attemptRevision,
                restaurantArrivedAtUtc),
            DriverWorkOfferStatus.MovingToPickup => BuildPickupActions(attemptRevision, restaurantArrivedAtUtc, pickupReady),
            DriverWorkOfferStatus.MovingToDropoff => new 업무가능행동Dto[] { Action(음식배달가능행동Ids.기사전달완료) },
            _ => Array.Empty<업무가능행동Dto>()
        };
        return attemptRevision > 0 && workStatus is (DriverWorkOfferStatus.Accepted
            or DriverWorkOfferStatus.MovingToPickup or DriverWorkOfferStatus.MovingToDropoff)
            ? actions.Append(DeliveryAttemptAction(음식배달가능행동Ids.기사배달중단, attemptRevision)).ToArray()
            : actions;
    }

    private static IReadOnlyList<업무가능행동Dto> BuildArrivalActions(
        long attemptRevision,
        DateTime? restaurantArrivedAtUtc)
        => restaurantArrivedAtUtc.HasValue
            ? []
            : [DeliveryAttemptAction(음식배달가능행동Ids.기사가게도착, attemptRevision)];

    private static IReadOnlyList<업무가능행동Dto> BuildPickupActions(
        long attemptRevision,
        DateTime? restaurantArrivedAtUtc,
        bool pickupReady)
    {
        var actions = new List<업무가능행동Dto>();
        if (!restaurantArrivedAtUtc.HasValue)
        {
            actions.Add(DeliveryAttemptAction(음식배달가능행동Ids.기사가게도착, attemptRevision));
        }

        if (pickupReady)
        {
            actions.Add(Action(음식배달가능행동Ids.기사픽업확인));
        }
        return actions;
    }

    private static 업무가능행동Dto OrderAction(string actionId, long revision)
        => new()
        {
            ActionId = actionId,
            RevisionKindCode = 업무Revision종류Codes.음식주문,
            ExpectedRevision = revision
        };

    private static 업무가능행동Dto DeliveryAttemptAction(string actionId, long revision)
        => new()
        {
            ActionId = actionId,
            RevisionKindCode = 업무Revision종류Codes.음식배달시도,
            ExpectedRevision = revision
        };

    private static 업무가능행동Dto Action(string actionId, DateTime? expiresAtUtc = null)
        => new()
        {
            ActionId = actionId,
            ExpiresAtUtc = expiresAtUtc
        };

    private static long ResolveRevision(음식주문응답 order)
        => order.Revision > 0 ? order.Revision : order.상태이력.Count;
}
