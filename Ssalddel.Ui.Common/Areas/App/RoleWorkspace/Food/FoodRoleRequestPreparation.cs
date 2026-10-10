using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>입력과 조회 근거의 값 사본으로 요청 본문만 준비합니다. 인증·전송·재조회는 수행하지 않습니다.</summary>
internal static class FoodRoleRequestPreparation
{
    public static FoodRolePreparedRequest Prepare(FoodRoleRequestSnapshot input, Guid requestId, DateTime utcNow)
    {
        if (input.RoleKey != "operator" && (!input.HasAllowedAction || input.ActionExpiresAtUtc is { } expires && expires <= utcNow))
            throw new InvalidOperationException("현재 가능 행동을 다시 조회해 주세요.");
        var cleanReason = input.Reason.Trim();
        if (cleanReason.Length > 1000) throw new ArgumentException("사유는 1,000자 이내로 입력해 주세요.");
        var id = Uri.EscapeDataString(input.ItemId);
        return input.RoleKey switch
        {
            "orderer" => PrepareCancellation(input, requestId, id, cleanReason),
            "restaurant" when input.ActionKey == 음식배달가능행동Ids.음식점주문수락 => PrepareAcceptance(input, requestId, id, cleanReason),
            "restaurant" => PrepareRestaurantProgress(input, requestId, id, cleanReason),
            "food-driver" => PrepareInterruption(input, requestId, id, cleanReason),
            _ => PrepareInterruptionReview(input, requestId, cleanReason)
        };
    }

    private static FoodRolePreparedRequest PrepareCancellation(FoodRoleRequestSnapshot input, Guid requestId, string id, string reason)
    {
        if (input.ReasonCode is not (운영배차주문자취소사유Code.단순변심 or 운영배차주문자취소사유Code.중복주문 or 운영배차주문자취소사유Code.주소문제 or 운영배차주문자취소사유Code.기타)
            || input.ReasonCode == 운영배차주문자취소사유Code.기타 && reason.Length == 0)
            throw new ArgumentException("취소 사유를 선택하고 필요한 설명을 입력해 주세요.");
        return new(requestId, $"api/v1/food-orders/{id}/cancellation", new 주문자음식주문취소요청
            { 클라이언트요청Id = requestId, 예상Revision = input.ExpectedRevision, 사유Code = input.ReasonCode, 사유 = reason },
            BeforeRevision: input.ExpectedRevision ?? input.OrdererHistoryCount);
    }

    private static FoodRolePreparedRequest PrepareAcceptance(FoodRoleRequestSnapshot input, Guid requestId, string id, string reason)
    {
        if (!input.ImmediatePickup && input.PreparationMinutes is not (> 0 and <= 240))
            throw new ArgumentException("조리 예상 시간을 1~240분으로 입력해 주세요.");
        if (string.IsNullOrWhiteSpace(input.RestaurantName) || string.IsNullOrWhiteSpace(input.RestaurantAddress))
            throw new ArgumentException("음식점 이름과 픽업 주소를 확인해 주세요.");
        var order = input.Order!;
        var originalAddress = input.RestaurantAddress.Trim() == order.Address.Trim() && input.RestaurantDetailAddress.Trim() == order.DetailAddress.Trim();
        return new(requestId, $"api/v1/food-orders/{id}/restaurant-acceptance", new 음식점주문수락요청
        {
            클라이언트요청Id = requestId, 음식점명 = input.RestaurantName.Trim(), 음식점주소 = input.RestaurantAddress.Trim(), 음식점상세주소 = input.RestaurantDetailAddress.Trim(),
            음식점위도 = originalAddress ? order.Latitude : null, 음식점경도 = originalAddress ? order.Longitude : null,
            조리예상분 = input.ImmediatePickup ? 0 : input.PreparationMinutes, 즉시픽업가능여부 = input.ImmediatePickup, 수락메모 = reason
        }, BeforeRevision: order.Revision, OriginalDetailAddress: order.DetailAddress);
    }

    private static FoodRolePreparedRequest PrepareRestaurantProgress(FoodRoleRequestSnapshot input, Guid requestId, string id, string reason)
    {
        if (input.ActionKey == 음식배달가능행동Ids.음식점조리시간변경 && input.PreparationMinutes is not (> 0 and <= 240))
            throw new ArgumentException("조리 예상 시간을 1~240분으로 입력해 주세요.");
        if (input.ActionKey == 음식배달가능행동Ids.음식점주문거절 && reason.Length == 0) throw new ArgumentException("거절 사유를 입력해 주세요.");
        var revision = input.ExpectedRevision ?? input.Order!.Revision;
        return new(requestId, $"api/v1/food-orders/{id}/restaurant-progress", new 음식점주문진행변경요청
        {
            클라이언트요청Id = requestId, 예상Revision = revision,
            작업 = input.ActionKey == 음식배달가능행동Ids.음식점주문거절 ? 음식점주문진행작업코드.거절 : 음식점주문진행작업코드.조리시간변경,
            조리예상분 = input.ActionKey == 음식배달가능행동Ids.음식점조리시간변경 ? input.PreparationMinutes : null, 사유 = reason
        }, BeforeRevision: revision);
    }

    private static FoodRolePreparedRequest PrepareInterruption(FoodRoleRequestSnapshot input, Guid requestId, string id, string reason)
    {
        var delivery = input.Delivery!;
        if (string.IsNullOrWhiteSpace(delivery.AttemptId)) throw new ArgumentException("현재 배달 시도를 다시 확인해 주세요.");
        var revision = input.ExpectedRevision ?? delivery.Revision;
        return new(requestId, $"api/v1/driver/food-deliveries/offers/{id}/interruption", new 음식배달중단요청
        {
            클라이언트요청Id = requestId, 예상시도Revision = revision,
            사유Code = 음식배달중단사유Code.정규화(input.ReasonCode), 메모 = reason
        }, BeforeRevision: revision, AttemptId: delivery.AttemptId);
    }

    private static FoodRolePreparedRequest PrepareInterruptionReview(FoodRoleRequestSnapshot input, Guid requestId, string reason)
    {
        var attempt = input.Review ?? throw new ArgumentException("검토할 중단 배달을 선택해 주세요.");
        if (input.DecisionCode is not (음식배달중단검토판정Code.보호 or 음식배달중단검토판정Code.기사책임 or 음식배달중단검토판정Code.음식점책임 or 음식배달중단검토판정Code.플랫폼책임)
            || reason.Length == 0) throw new ArgumentException("판정과 근거를 입력해 주세요.");
        return new(requestId, $"api/v1/admin/food-orders/delivery-attempts/{Uri.EscapeDataString(input.AttemptId)}/interruption-review",
            new 음식배달중단검토요청 { 클라이언트요청Id = requestId, 예상Revision = attempt.Revision, 판정Code = input.DecisionCode, 판정사유 = reason, 악용확정여부 = input.AbuseConfirmed },
            true, attempt.Revision, AttemptId: attempt.AttemptId);
    }
}

/// <summary>준비 시점의 값만 담습니다. ViewModel과 수정 가능한 조회 DTO를 보관하지 않습니다.</summary>
internal sealed record FoodRoleRequestSnapshot(string RoleKey, string ItemId, string ActionKey)
{
    public bool HasAllowedAction { get; init; }
    public long? ExpectedRevision { get; init; }
    public DateTime? ActionExpiresAtUtc { get; init; }
    public int OrdererHistoryCount { get; init; }
    public FoodRoleOrderPreparationSource? Order { get; init; }
    public FoodRoleDeliveryPreparationSource? Delivery { get; init; }
    public FoodRoleReviewPreparationSource? Review { get; init; }
    public int? PreparationMinutes { get; init; }
    public bool ImmediatePickup { get; init; }
    public string RestaurantName { get; init; } = string.Empty;
    public string RestaurantAddress { get; init; } = string.Empty;
    public string RestaurantDetailAddress { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string AttemptId { get; init; } = string.Empty;
    public string DecisionCode { get; init; } = string.Empty;
    public bool AbuseConfirmed { get; init; }
}

internal sealed record FoodRoleOrderPreparationSource(long Revision, string Address, string DetailAddress, decimal? Latitude, decimal? Longitude);
internal sealed record FoodRoleDeliveryPreparationSource(string? AttemptId, long Revision);
internal sealed record FoodRoleReviewPreparationSource(string AttemptId, long Revision);
internal sealed record FoodRolePreparedRequest(Guid RequestId, string Path, object Body, bool UsePut = false,
    long BeforeRevision = 0, string? AttemptId = null, string? OriginalDetailAddress = null);
