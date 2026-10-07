namespace Ssalddel.Ui.Common.Areas.App.Models;

/// <summary>공개 동네 대표점과 권한이 확인된 선택 배송만 지도 표시 어댑터에 전달합니다.</summary>
public sealed record NeighborhoodMapPoint(double Latitude, double Longitude);
public sealed record NeighborhoodMapViewport(double Latitude, double Longitude, double Zoom);
public sealed record NeighborhoodMapMarker(string Id, string Label, string Kind, double Latitude, double Longitude, int Count = 1,
    DateTimeOffset? MeasuredAt = null, DateTimeOffset? ReceivedAt = null, DateTimeOffset? ExpiresAt = null);
public sealed record NeighborhoodMapRoute(string Id, IReadOnlyList<NeighborhoodMapPoint> Points, string Color, string? Notice = null, string? SourceCode = null,
    DateTimeOffset? ExpiresAt = null);
public sealed record NeighborhoodMapRenderState(
    long Revision,
    IReadOnlyList<NeighborhoodMapMarker> Markers,
    IReadOnlyList<NeighborhoodMapRoute> Routes,
    string? SelectedMarkerId = null,
    NeighborhoodMapViewport? Viewport = null,
    bool PreserveViewport = true);

public static class NeighborhoodMapMarkerKinds
{
    public const string Region = "region";
    public const string Pickup = "pickup";
    public const string Dropoff = "dropoff";
    public const string Driver = "driver";
    public const string Inactive = "inactive";
    public const string PublicData = "public-data";
}
public sealed record NeighborhoodMapHostStatus(string Code, string? Message = null)
{
    public bool IsReady => Code == "ready";
    public static readonly NeighborhoodMapHostStatus Loading = new("loading", "지도를 불러오는 중입니다.");
    public static readonly NeighborhoodMapHostStatus Unavailable = new("unavailable", "지도를 사용할 수 없습니다. 아래 목록으로 확인해 주세요.");
}
