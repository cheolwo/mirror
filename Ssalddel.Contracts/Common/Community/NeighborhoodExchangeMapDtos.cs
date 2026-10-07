namespace Ssalddel.Contracts.Common.Community;

public static class NeighborhoodExchangeMapRoutes
{
    public const string Api = "api/v1/community/exchange/map";
    public const string RegionsApi = Api + "/regions";
    public const string PostsApi = Api + "/posts";
}

/// <summary>공식 지역 경계에서 정한 동네 대표점입니다. 물품·주거·배송 위치가 아닙니다.</summary>
public sealed class NeighborhoodPublicRegionDto
{
    public string RegionKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ParentRegionKey { get; init; } = string.Empty;
    public string CountryCode { get; init; } = "KR";
    public string PrecisionCode { get; init; } = "neighborhood";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string AnchorSourceName { get; init; } = string.Empty;
    public string AnchorSourceUrl { get; init; } = string.Empty;
    public string AnchorSourceVintage { get; init; } = string.Empty;
    public string AnchorSourceSha256 { get; init; } = string.Empty;
    public string AnchorLicenseCode { get; init; } = string.Empty;
    public string LocationBoundary { get; init; } = "동네 기준 위치이며 실제 물품·주거·픽업·전달 위치가 아닙니다.";
}

public sealed class NeighborhoodExchangeRegionListResponse
{
    public IReadOnlyList<NeighborhoodPublicRegionDto> Items { get; init; } = [];
    public string Notice { get; init; } = "검증된 지역 대표점이 있는 동네만 선택할 수 있습니다. 동네 선택은 선택 사항입니다.";
}

public sealed class NeighborhoodExchangeMapQuery
{
    public string? PublicNeighborhoodRegionKey { get; init; }
    public string? Intent { get; init; }
}

public sealed class NeighborhoodExchangeMapMarkerDto
{
    public NeighborhoodPublicRegionDto Region { get; init; } = new();
    public int OfferCount { get; init; }
    public int NeedCount { get; init; }
    public int PostCount => OfferCount + NeedCount;
}

public sealed class NeighborhoodExchangeMapResponse
{
    public IReadOnlyList<NeighborhoodExchangeMapMarkerDto> Items { get; init; } = [];
    public int UnlocatedPostCount { get; init; }
    public int UnsupportedRegionPostCount { get; init; }
    public string LocationBoundary { get; init; } = "핀은 동네별 공개 글을 모아 보여주는 지역 대표점입니다. 실제 물품 위치나 배송 경로가 아닙니다.";
}
