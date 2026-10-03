using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.Models;

/// <summary>기사와 운영자가 같은 주문 대금의 확정 범위를 읽도록 하는 표시 모델입니다.</summary>
public sealed record FoodDeliverySettlementDisplay(
    string OrderNo,
    string RestaurantName,
    string GrossText,
    string DeductionText,
    string NetText,
    string StatusText,
    string PaymentText,
    string NoticeText)
{
    public string CompletedText { get; init; } = string.Empty;
    public string ProgressText => string.Join(" · ", new[] { StatusText, PaymentText }
        .Where(text => !string.IsNullOrWhiteSpace(text)));

    public static FoodDeliverySettlementDisplay From(FoodDeliveryOrderSettlementDto value)
        => new(
            value.OrderNo,
            value.RestaurantName,
            Amount(value.GrossAmount, "배달료 확인 필요"),
            Amount(value.DeductionAmount, "공제 미확정"),
            Amount(value.NetAmount, "수령액 미확정"),
            value.SettlementStatusCode switch
            {
                "AwaitingReceipt" => "수령 확인 대기",
                "AwaitingDeductions" => "공제 확인 중",
                "ReadyForSimulation" when value.PayoutStatusCode is "SimulationSucceeded" or "SimulationFailed" => string.Empty,
                "ReadyForSimulation" => "정산 준비 완료",
                "BlockedMissingQuote" => "배달료 확인 필요",
                _ => "정산 상태 확인 필요"
            },
            value.PayoutStatusCode switch
            {
                "SimulationSucceeded" => "지급 테스트 완료",
                "SimulationFailed" => "지급 테스트 실패 · 재시도 필요",
                _ => string.Empty
            },
            value.DeductionEvidenceScopeCode == "SimulationFixture"
                ? "테스트 정산 · 실제 입금 아님"
                : "공제·입금 확인 전")
        {
            CompletedText = value.CompletedAtUtc == default
                ? string.Empty : value.CompletedAtUtc.ToLocalTime().ToString("M/d HH:mm")
        };

    private static string Amount(decimal? value, string unknown)
        => value.HasValue ? value.Value.ToString("#,0.##") + "원" : unknown;
}
