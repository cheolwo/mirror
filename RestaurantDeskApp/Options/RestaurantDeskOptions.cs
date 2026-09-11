using Ssalddel.Ui.Common.Areas.App.Services;

namespace RestaurantDeskApp.Options;

public sealed class RestaurantDeskOptions
{
    public const string SectionName = "RestaurantDesk";

    public long RestaurantId { get; set; } = 101;

    public string RestaurantName { get; set; } = "살뜰 식당";

    public string RestaurantAddress { get; set; } = string.Empty;

    public string RestaurantDetailAddress { get; set; } = string.Empty;

    public decimal? RestaurantLatitude { get; set; }

    public decimal? RestaurantLongitude { get; set; }

    public int DefaultPreparationMinutes { get; set; } = 20;

    public Dictionary<string, int> 상품별기본조리분 { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ServerBaseUrl { get; set; } =
        SsalddelServerEndpoint.LocalDevelopmentBaseAddress;

    public Uri GetServerBaseAddress()
        => SsalddelServerEndpoint.ResolveBaseAddress(
            ServerBaseUrl,
            new Uri(SsalddelServerEndpoint.LocalDevelopmentBaseAddress));
}
