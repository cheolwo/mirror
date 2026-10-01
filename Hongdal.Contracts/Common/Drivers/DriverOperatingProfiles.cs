using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Contracts.Common.Drivers;

public static class DriverRoutingProviderCodes
{
    public const string NaverDirections = "NaverDirections";
    public const string GoogleRoutes = "GoogleRoutes";
}

public static class DriverPlaceProviderCodes
{
    public const string KoreaRoadNameAddress = "KoreaRoadNameAddress";
    public const string GooglePlaces = "GooglePlaces";
}

public static class DriverNavigationProviderCodes
{
    public const string ExternalNavigation = "ExternalNavigation";
    public const string GoogleNavigation = "GoogleNavigation";
}

/// <summary>
/// Driver 앱의 화면 언어와 독립적으로 선택하는 운행 시장 프로필입니다.
/// 현재 한국과 미국을 지원하며, 이후 국가별 프로필을 같은 방식으로 확장할 수 있습니다.
/// </summary>
public sealed record DriverOperatingProfile(
    string MarketCode,
    string DisplayName,
    string Description,
    string MapProviderCode,
    string RoutingProviderCode,
    string PlaceProviderCode,
    string AddressProviderCode,
    string NavigationProviderCode,
    double DefaultCenterLatitude,
    double DefaultCenterLongitude,
    double DefaultZoom)
{
    public OperatingMarketProfile Market => OperatingMarketProfileCatalog.Get(MarketCode);
    public bool IsKorea => MarketCode == OperatingMarketCodes.Korea;
    public bool IsUnitedStates => MarketCode == OperatingMarketCodes.UnitedStates;
}

public static class DriverOperatingProfileCatalog
{
    public static DriverOperatingProfile Korea { get; } = CreateKorea();
    public static DriverOperatingProfile UnitedStates { get; } = CreateUnitedStates();
    public static IReadOnlyList<DriverOperatingProfile> All { get; } = [Korea, UnitedStates];

    public static DriverOperatingProfile Get(string? marketCode)
        => OperatingMarketCodes.Normalize(marketCode) == OperatingMarketCodes.UnitedStates
            ? UnitedStates
            : Korea;

    private static DriverOperatingProfile CreateKorea()
    {
        var market = OperatingMarketProfileCatalog.Get(OperatingMarketCodes.Korea);
        return new(
            market.MarketCode,
            "한국 기사용",
            "대한민국 배차와 운송에 최적화된 프로필",
            market.MapProviderCode,
            DriverRoutingProviderCodes.NaverDirections,
            DriverPlaceProviderCodes.KoreaRoadNameAddress,
            market.AddressProviderCode,
            DriverNavigationProviderCodes.ExternalNavigation,
            37.5665d,
            126.9780d,
            11d);
    }

    private static DriverOperatingProfile CreateUnitedStates()
    {
        var market = OperatingMarketProfileCatalog.Get(OperatingMarketCodes.UnitedStates);
        return new(
            market.MarketCode,
            "미국 기사용",
            "미국 배차와 운송을 위한 운영 프로필",
            market.MapProviderCode,
            DriverRoutingProviderCodes.GoogleRoutes,
            DriverPlaceProviderCodes.GooglePlaces,
            market.AddressProviderCode,
            DriverNavigationProviderCodes.GoogleNavigation,
            39.8283d,
            -98.5795d,
            4d);
    }
}
