using Ssalddel.Ui.Common.Areas.App.Services;

namespace RestaurantDeskApp.Options;

public sealed class RestaurantDeskOptions
{
    public const string SectionName = "RestaurantDesk";

    public long RestaurantId { get; set; } = 1;

    public string RestaurantName { get; set; } = "관찰 검증 음식점";

    public string RestaurantAddress { get; set; } = "검증 표본 음식점";

    public string RestaurantDetailAddress { get; set; } = string.Empty;

    public decimal? RestaurantLatitude { get; set; } = 37.588m;

    public decimal? RestaurantLongitude { get; set; } = 127.085m;

    public int DefaultPreparationMinutes { get; set; } = 20;

    public Dictionary<string, int> 상품별기본조리분 { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ServerBaseUrl { get; set; } =
        SsalddelServerEndpoint.LocalDevelopmentBaseAddress;

    public Uri GetServerBaseAddress()
        => SsalddelServerEndpoint.ResolveBaseAddress(
            ServerBaseUrl,
            new Uri(SsalddelServerEndpoint.LocalDevelopmentBaseAddress));
}
