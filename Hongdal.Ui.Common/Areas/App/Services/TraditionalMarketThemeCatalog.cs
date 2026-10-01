namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed record TraditionalMarketThemePalette(
    string PreviewBackground,
    string MarketHeader,
    string BoardMarker,
    string MarketDayBanner,
    string ProductMarker,
    string PickupSign,
    string StoryCover);

public sealed record TraditionalMarketThemeDefinition(
    string ProductKey,
    string PackKey,
    string Title,
    string CreatorName,
    string Summary,
    string MarketScopeKey,
    string MarketName,
    string LicenseLabel,
    string DesignerCompensationPolicyLabel,
    TraditionalMarketThemePalette Palette);

public static class TraditionalMarketThemeCatalog
{
    public static IReadOnlyList<TraditionalMarketThemeDefinition> Definitions { get; } =
    [
        new(
            "store-market-theme-seongnam-harvest-v1",
            "market-theme-seongnam-harvest-v1",
            "성남 함께 여는 장날",
            "시장빛 디자인 협업 예시",
            "성남 생활권 장날의 시장 이야기, 게시판 표식, 상품·수령 안내를 한 묶음으로 연결한 전통시장 팩입니다.",
            "traditional-market:sample-seongnam",
            "성남 생활권 전통시장 예시",
            "상인회 시범운영 범위 내 사용",
            "디자이너 보상은 상인회와 별도 계약",
            new(
                "#F5F7F4",
                "#146B55",
                "#B8443C",
                "#C18416",
                "#35566B",
                "#694F3D",
                "#6D5A88"))
    ];

    public static IReadOnlyList<CommunityDecorationProduct> CreateProducts()
        => Definitions.Select(CreateProduct).ToArray();

    private static CommunityDecorationProduct CreateProduct(TraditionalMarketThemeDefinition definition)
    {
        var palette = definition.Palette;
        return new(
            definition.ProductKey,
            definition.PackKey,
            definition.Title,
            definition.CreatorName,
            definition.Summary,
            CommunityDecorationTarget.TraditionalMarketTheme,
            0,
            "KRW",
            [],
            TraditionalMarketTheme: new(
                "1.0.0",
                "traditional-market-surfaces-v1",
                "승인",
                "승인",
                definition.LicenseLabel,
                definition.MarketScopeKey,
                definition.MarketName,
                palette.PreviewBackground,
                new("market-header", "시장 머리말", palette.MarketHeader, AltText: "짙은 초록색 시장 머리말"),
                new("board-marker", "게시판 표식", palette.BoardMarker, AltText: "붉은 벽돌색 게시판 표식"),
                new("market-day-banner", "장날 배너", palette.MarketDayBanner, AltText: "황금빛 장날 배너"),
                new("product-marker", "상품 표식", palette.ProductMarker, AltText: "푸른 회색 상품 표식"),
                new("pickup-sign", "수령 표식", palette.PickupSign, AltText: "나무빛 수령 표식"),
                new("story-cover", "성사 이야기", palette.StoryCover, AltText: "보랏빛 성사 이야기 표지"),
                true,
                definition.DesignerCompensationPolicyLabel));
    }
}
