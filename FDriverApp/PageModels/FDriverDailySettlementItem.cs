using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace FDriverApp.PageModels;

public sealed partial class FDriverDailySettlementItem : ObservableObject
{
    public string SettlementId { get; init; } = string.Empty;
    public string DriverId { get; init; } = string.Empty;
    public string DeliveryAttemptId { get; init; } = string.Empty;
    public required FoodDeliverySettlementDisplay Display { get; init; }
    public string DeductionSummaryText { get; init; } = "공제 미확정";
    public string NetSummaryText { get; init; } = "수령액 미확정";
    public string CompletedText { get; init; } = string.Empty;
    public string PricingDetails { get; init; } = string.Empty;
    public string DistanceText { get; init; } = "요금 기준 거리 확인 전";
    public IAsyncRelayCommand? OpenDetailsCommand { get; private set; }
    [ObservableProperty] private bool _isPricingExpanded;
    [RelayCommand] private void TogglePricing() => IsPricingExpanded = !IsPricingExpanded;

    public static FDriverDailySettlementItem From(FoodDeliveryOrderSettlementDto row,
        Func<FDriverDailySettlementItem, Task>? openDetails = null)
    {
        var pricing = row.PricingBreakdown;
        var lines = new List<string>();
        if (pricing.EvidenceStatusCode is "MissingEvidence" or "InvalidEvidence")
            lines.Add("수락 당시 요금 구성을 확인하지 못했습니다.");
        else
        {
            lines.Add(pricing.EvidenceStatusCode == "MissingComponents"
                ? "수락 당시 요금 · 세부 구성 확인 전" : "수락 당시 저장된 요금 구성");
            if (pricing.BaseSplitCode == "LegacyUnsplit")
                lines.Add($"기본비 합계 {Money(pricing.BaseAmount)} · 픽업·전달 구분 미확인");
            else
                lines.Add($"픽업 {Money(pricing.PickupAmount)} · 전달 {Money(pricing.DropoffAmount)}");
            lines.Add($"거리비 {Money(pricing.DistanceAmount)} · 최소요금 조정 {Money(pricing.MinimumAdjustmentAmount)}");
            lines.Add($"시간 할증 {Money(pricing.TimeSurchargeAmount)} · 날씨 할증 {Money(pricing.WeatherSurchargeAmount)} · 수요 할증 {Money(pricing.DemandSurchargeAmount)}");
            lines.Add(pricing.DistanceKm.HasValue
                ? $"요금 기준 거리 {pricing.DistanceKm.Value:0.###}km"
                : "요금 기준 거리 확인 전");
        }
        var item = new FDriverDailySettlementItem
        {
            SettlementId = row.SettlementId,
            DriverId = row.DriverId,
            DeliveryAttemptId = row.DeliveryAttemptId,
            Display = FoodDeliverySettlementDisplay.From(row),
            DeductionSummaryText = row.DeductionAmount.HasValue ? $"공제 {Money(row.DeductionAmount)}" : "공제 미확정",
            NetSummaryText = row.NetAmount.HasValue ? $"수령액 {Money(row.NetAmount)}" : "수령액 미확정",
            CompletedText = row.CompletedAtUtc == default ? "완료 시각 확인 전"
                : row.CompletedAtUtc.AddHours(9).ToString("M/d HH:mm", CultureInfo.InvariantCulture) + " 전달 완료",
            PricingDetails = string.Join(Environment.NewLine, lines),
            DistanceText = pricing.DistanceKm.HasValue
                ? $"요금 기준 거리 {pricing.DistanceKm.Value:0.###}km" : "요금 기준 거리 확인 전"
        };
        if (openDetails is not null) item.OpenDetailsCommand = new AsyncRelayCommand(() => openDetails(item));
        return item;
    }

    private static string Money(decimal? amount) => amount.HasValue ? $"{amount.Value:#,0.##}원" : "확인 전";
}
