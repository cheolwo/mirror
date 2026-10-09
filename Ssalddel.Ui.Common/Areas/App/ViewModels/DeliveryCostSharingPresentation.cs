using Ssalddel.Contracts.Common.Fulfillment;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>부담 정책 설명만 소유합니다. 금액 계산은 공통 계산기의 확인 결과를 표시합니다.</summary>
public static class DeliveryCostSharingPresentation
{
    public const string HeldReason = "음식점의 부담 조건을 충족하지 못해 접수가 보류됩니다. 전액 부담 한도의 초과분을 주문자에게 자동으로 넘기지 않습니다. 원 주문의 비용 조건을 다시 확인해 주세요.";
    public static bool FoodQuoteUnconfirmed(AutoFulfillmentSnapshot value) => value.SourceKind == FulfillmentSourceKinds.FoodOrder
        && !value.ExecutionPaused && value.AttemptId is null && value.TotalCostKrw == 0
        && (value.CostSharingAllowed == false || value.StateCode is AutoFulfillmentStates.AwaitingSource or AutoFulfillmentStates.AwaitingRouting or AutoFulfillmentStates.NeedsReview);
    public static string Total(AutoFulfillmentSnapshot value) => FoodQuoteUnconfirmed(value)
        ? value.CostSharingAllowed == false ? "견적 미확정" : "견적 대기"
        : AutoFulfillmentPresentation.Money(value.TotalCostKrw);
    public static string Merchant(AutoFulfillmentSnapshot value) => FoodQuoteUnconfirmed(value) && value.CostSharingAllowed != false
        ? "견적 대기" : AutoFulfillmentPresentation.Money(!value.ExecutionPaused && !FoodQuoteUnconfirmed(value)
            && value.CostSharingAllowed != false && value.TotalCostKrw is not null && value.UserChargeKrw is not null
            ? value.RestaurantContributionKrw : null);
    public static string Customer(AutoFulfillmentSnapshot value) => FoodQuoteUnconfirmed(value) && value.CostSharingAllowed != false
        ? "견적 대기" : AutoFulfillmentPresentation.Money(value.CostSharingAllowed == false ? null : value.UserChargeKrw);

    public static string Name(string? mode) => mode switch
    {
        "CustomerPays" => "주문자 부담",
        "FixedContribution" => "정액 지원",
        "PercentageShare" => "비율 분담",
        "RestaurantPays" => "음식점 전액",
        _ => "부담 정책 확인 필요"
    };
    public static string Summary(DeliveryCostSharingPolicy? policy, decimal legacyContribution = 0)
    {
        var value = DeliveryCostSharingCalculator.Effective(policy, legacyContribution);
        if (DeliveryCostSharingCalculator.ValidationError(value) is not null) return "배송비 부담 정책을 다시 확인해 주세요.";
        var limit = value.MaximumRestaurantChargeKrw is { } cap ? $"음식점 한도 {cap:N0}원" : "음식점 한도 없음";
        return value.Mode switch
        {
            "CustomerPays" => "주문자 부담 · 음식점 지원 없음",
            "FixedContribution" => $"음식점 정액 {value.FixedAmountKrw:N0}원 지원",
            "PercentageShare" => $"음식점 {value.RestaurantSharePercent:0.##}% 부담 · {limit}",
            "RestaurantPays" => $"음식점 전액 부담 · {limit}",
            _ => "배송비 부담 정책을 다시 확인해 주세요."
        };
    }
}
