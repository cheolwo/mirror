using Ssalddel.Contracts.Driver.Settings;

namespace Ssalddel.Contracts.Common.Notifications;

public sealed record DriverRecommendationAlertVisual(string Label, string Title, string Body,
    string IconPath, string AndroidIconName, string AccentColor);

/// <summary>알림의 공개 표시만 담당합니다. 알려지지 않은 업무를 음식 또는 화물로 추정하지 않습니다.</summary>
public static class DriverRecommendationAlertPresentation
{
    public const string FoodIconPath = "M4 3 V8 C4 11 8 11 8 8 V3 M6 3 V21 M20 3 V21 M20 3 C14 3 14 13 20 13";
    public const string CargoIconPath = "M2 5 H14 V17 H2 Z M14 9 H18 L22 13 V17 H14 M16 9 V13 H22 M5 17 A2 2 0 1 0 9 17 A2 2 0 1 0 5 17 M16 17 A2 2 0 1 0 20 17 A2 2 0 1 0 16 17";

    public static DriverRecommendationAlertVisual? Resolve(string? workKind) => workKind switch
    {
        DriverWorkKinds.FoodDelivery => new("음식 배달", "음식 배달 · 새 추천", "앱에서 최신 음식 배달 추천과 배달료를 확인하세요.",
            FoodIconPath, "ic_driver_food", "#2563EB"),
        DriverWorkKinds.CargoTransport => new("화물 운송", "화물 운송 · 새 추천", "앱에서 최신 화물 운송 추천과 운송료를 확인하세요.",
            CargoIconPath, "ic_driver_cargo", "#DC2626"),
        DriverWorkKinds.QuickDelivery => new("소형 물품·퀵", "소형 물품·퀵 · 새 추천", "앱에서 최신 소형 물품·퀵 추천과 배송료를 확인하세요.",
            CargoIconPath, "ic_driver_cargo", "#DC2626"),
        _ => null
    };
}
