using System.Text.Json.Serialization;

namespace Ssalddel.Contracts.Common.Drivers;

public sealed record DriverMapMarkerItem(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("pickupLatitude")] double PickupLatitude,
    [property: JsonPropertyName("pickupLongitude")] double PickupLongitude,
    [property: JsonPropertyName("dropoffLatitude")] double DropoffLatitude,
    [property: JsonPropertyName("dropoffLongitude")] double DropoffLongitude,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("pickupAddress")] string PickupAddress,
    [property: JsonPropertyName("dropoffAddress")] string DropoffAddress = "",
    [property: JsonPropertyName("pickupLabel")] string PickupLabel = "추천 상차지",
    [property: JsonPropertyName("dropoffLabel")] string DropoffLabel = "추천 하차지")
{
    /// <summary>서버가 확인한 남은 정차의 표시 순번입니다. null은 기존 추천 핀입니다.</summary>
    [JsonPropertyName("stopOrder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StopOrder { get; init; }

    /// <summary>현재 확인한 첫 미완료 정차만 강조합니다. 운송 상태를 변경하지 않습니다.</summary>
    [JsonPropertyName("isNextStop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsNextStop { get; init; }

    [JsonIgnore]
    public string TicketId => RequestId;
}

public sealed record DriverMapRoutePoint(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("label")] string Label);

public sealed record DriverMapRouteOverlay(
    [property: JsonPropertyName("routeId")] string RouteId,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("points")] IReadOnlyList<DriverMapRoutePoint> Points,
    [property: JsonPropertyName("strokeColor")] string StrokeColor = "#2563eb",
    [property: JsonPropertyName("outlineColor")] string OutlineColor = "#ffffff",
    [property: JsonPropertyName("width")] int Width = 9);
