using System.Globalization;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>서버가 현재 당사자에게 허용한 완료 건수만 표시하며 방문 횟수를 추정하지 않습니다.</summary>
public static class DeliveryExperiencePresentation
{
    public static IReadOnlyList<string> DriverLines(IEnumerable<DeliveryExperienceSummaryDto>? values, bool isDelivery)
        => isDelivery
            ? Known(values, "DeliveryCompleted", "Road", "ApartmentComplex").Select(value =>
                $"{value.PlaceDisplayName} 배달 완료 {Count(value)}건 · {Basis(value)}").Take(2).ToArray()
            : Known(values, "PickupCompleted", "Restaurant").Select(value =>
                $"이 매장 픽업 완료 {Count(value)}건 · {Basis(value)}").Take(1).ToArray();

    public static IReadOnlyList<string> RestaurantLines(IEnumerable<DeliveryExperienceSummaryDto>? values)
        => Known(values, "PickupCompleted", "Restaurant").Select(value =>
            $"이 매장에서 {Count(value)}건을 픽업한 기사님입니다. · {Basis(value)}").Take(1).ToArray();

    public static IReadOnlyList<string> OrdererLines(IEnumerable<DeliveryExperienceSummaryDto>? values)
        => Known(values, "DeliveryCompleted", "Road", "ApartmentComplex").Select(value =>
            $"이 {(value.PlaceKind == "Road" ? "도로" : "단지")}에서 {Count(value)}건을 배달한 기사님이 전달 중입니다. · {Basis(value)}").Take(2).ToArray();

    private static IEnumerable<DeliveryExperienceSummaryDto> Known(IEnumerable<DeliveryExperienceSummaryDto>? values,
        string metric, params string[] places)
        => (values ?? []).Where(value => value is not null && value.MetricCode == metric
            && places.Contains(value.PlaceKind, StringComparer.Ordinal)
            && (value.EvidenceStatusCode == "Confirmed" && value.CompletedCount is >= 0
                || value.EvidenceStatusCode == "Partial" && value.CompletedCount is > 0)
            && value.AsOfUtc is { } time && time != default
            && value.ExecutionModeCode is "Operational" or "Simulation"
            && !string.IsNullOrWhiteSpace(value.PlaceDisplayName))
            .DistinctBy(value => value.PlaceKind)
            .OrderBy(value => Array.IndexOf(places, value.PlaceKind));

    private static string Count(DeliveryExperienceSummaryDto value)
        => value.CompletedCount!.Value.ToString("N0", CultureInfo.GetCultureInfo("ko-KR"));
    private static string Basis(DeliveryExperienceSummaryDto value)
        => (value.EvidenceStatusCode == "Partial" ? "확인된 " : string.Empty)
            + (value.ExecutionModeCode == "Simulation" ? "모의 기록" : "앱 기록");
}
