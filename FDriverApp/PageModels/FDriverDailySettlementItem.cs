using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace FDriverApp.PageModels;

public sealed partial class FDriverDailySettlementItem : ObservableObject
{
    public required FoodDeliverySettlementDisplay Display { get; init; }
    public string CompletedText { get; init; } = string.Empty;
    public string PricingDetails { get; init; } = string.Empty;
    [ObservableProperty] private bool _isPricingExpanded;
    [RelayCommand] private void TogglePricing() => IsPricingExpanded = !IsPricingExpanded;

    public static FDriverDailySettlementItem From(FoodDeliveryOrderSettlementDto row)
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
        return new()
        {
            Display = FoodDeliverySettlementDisplay.From(row),
            CompletedText = row.CompletedAtUtc == default ? "완료 시각 확인 전"
                : row.CompletedAtUtc.AddHours(9).ToString("M/d HH:mm", CultureInfo.InvariantCulture) + " 전달 완료",
            PricingDetails = string.Join(Environment.NewLine, lines)
        };
    }

    private static string Money(decimal? amount) => amount.HasValue ? $"{amount.Value:#,0.##}원" : "확인 전";
}
