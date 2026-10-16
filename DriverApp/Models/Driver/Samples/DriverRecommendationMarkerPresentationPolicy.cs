using Ssalddel.Contracts.Common.Drivers;

namespace DriverApp.Models.Driver.Samples;

public sealed record DriverRecommendationMarkerPresentation(
    DriverMapMarkerItem Source,
    double Latitude,
    double Longitude,
    string Caption,
    string SubCaption,
    bool IsPickup,
    bool IsMuted,
    string TintColor,
    int ZIndex);

/// <summary>지도 핀의 좌표·색·겹침 순서만 소유합니다. 현재 위치와 카드 순번은 변경하지 않습니다.</summary>
public static class DriverRecommendationMarkerPresentationPolicy
{
    public const string PickupTintColor = "#F57C00";
    public const string DropoffTintColor = "#2563EB";
    public const string InactiveTintColor = "#9CA3AF";

    public static IReadOnlyList<DriverRecommendationMarkerPresentation> Build(
        IReadOnlyList<DriverMapMarkerItem> markers,
        bool highlightSelectedRecommendation = false,
        string? selectedRecommendationRequestId = null)
    {
        var result = new List<DriverRecommendationMarkerPresentation>();
        foreach (var item in markers)
        {
            var selected = highlightSelectedRecommendation && !string.IsNullOrWhiteSpace(selectedRecommendationRequestId)
                && string.Equals(item.RequestId, selectedRecommendationRequestId, StringComparison.Ordinal);
            var muted = highlightSelectedRecommendation && !selected;
            AddPoint(item.PickupLatitude, item.PickupLongitude, item.PickupLabel, item.PickupAddress, isPickup: true);
            AddPoint(item.DropoffLatitude, item.DropoffLongitude, item.DropoffLabel, item.DropoffAddress, isPickup: false);

            void AddPoint(double latitude, double longitude, string caption, string subCaption, bool isPickup)
            {
                if (!CoordinatesValid(latitude, longitude)) return;
                result.Add(new(item, latitude, longitude, caption, subCaption, isPickup, muted,
                    muted ? InactiveTintColor : isPickup ? PickupTintColor : DropoffTintColor,
                    selected ? 1 : 0));
            }
        }

        // 선택 핀을 나중에 추가하고 높은 ZIndex로 그려 같은 픽업 좌표의 회색 핀 뒤에 숨지 않게 합니다.
        return highlightSelectedRecommendation ? result.OrderBy(x => x.ZIndex).ToArray() : result;
    }

    private static bool CoordinatesValid(double latitude, double longitude)
        => double.IsFinite(latitude) && double.IsFinite(longitude)
            && latitude is >= -90d and <= 90d and not 0d
            && longitude is >= -180d and <= 180d and not 0d;
}
