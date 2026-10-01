using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Tests.Services.Community;

public sealed class PlatformCommunityDecorationStateServiceTests
{
    [Fact]
    public void 기본전환애니메이션_전체125개슬롯에_활성화된다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var defaultMotion = service.Products.Single(product =>
            string.Equals(
                product.PackKey,
                PlatformCommunityDecorationStateService.DefaultBaguaMotionPackKey,
                StringComparison.OrdinalIgnoreCase));

        var resolved = service.ResolveBaguaMotion(
            "bagua-motion:orderer:order-to-transport",
            "relay");

        Assert.True(defaultMotion.IsBaguaMotion);
        Assert.True(service.IsProductOwned(defaultMotion));
        Assert.True(service.IsProductActive(defaultMotion));
        Assert.Equal(125, resolved?.CoveredPerspectiveSlotCount);
        Assert.Equal("runner-v1", resolved?.RendererProfile);
    }

    [Fact]
    public void 주문운송애니메이션_구매후_해당전환슬롯에만_적용된다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var motion = service.Products.Single(product =>
            string.Equals(product.PackKey, "bagua-motion-courier-sprint-v1", StringComparison.OrdinalIgnoreCase));
        var originalNodeAssetKey = service.ActiveNodeAssetKey;

        Assert.False(service.ApplyProduct(motion));

        service.Purchase(motion);
        var applied = service.ApplyProduct(motion);

        var matching = service.ResolveBaguaMotion(
            "bagua-motion:seller:order-to-transport",
            "relay");
        var unrelated = service.ResolveBaguaMotion(
            "bagua-motion:seller:sales-to-warehouse",
            "relay");

        Assert.True(applied);
        Assert.True(service.IsProductActive(motion));
        Assert.Equal("courier-sprint-v1", matching?.RendererProfile);
        Assert.Equal("runner-v1", unrelated?.RendererProfile);
        Assert.Equal(originalNodeAssetKey, service.ActiveNodeAssetKey);
    }

    [Fact]
    public void 전체애니메이션팩을_나중에적용하면_부분슬롯오버레이를_정리한다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var targetedMotion = service.Products.Single(product =>
            string.Equals(product.PackKey, "bagua-motion-courier-sprint-v1", StringComparison.OrdinalIgnoreCase));
        var globalMotion = service.Products.Single(product =>
            string.Equals(product.PackKey, "bagua-motion-light-trail-v1", StringComparison.OrdinalIgnoreCase));

        service.Purchase(targetedMotion);
        service.ApplyProduct(targetedMotion);
        service.Purchase(globalMotion);
        service.ApplyProduct(globalMotion);

        var resolved = service.ResolveBaguaMotion(
            "bagua-motion:orderer:order-to-transport",
            "relay");

        Assert.Equal("light-trail-v1", resolved?.RendererProfile);
        Assert.False(service.IsProductActive(targetedMotion));
        Assert.True(service.IsProductActive(globalMotion));
    }

    [Fact]
    public void 임시보유권초기화시_유료애니메이션활성표시도_기본값으로돌아간다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var motion = service.Products.Single(product =>
            string.Equals(product.PackKey, "bagua-motion-courier-sprint-v1", StringComparison.OrdinalIgnoreCase));

        service.Purchase(motion);
        service.ApplyProduct(motion);
        service.ClearAccountOwnedPacks();

        Assert.False(service.IsProductOwned(motion));
        Assert.False(service.IsProductActive(motion));
        Assert.Equal(
            "runner-v1",
            service.ResolveBaguaMotion("bagua-motion:orderer:order-to-transport", "relay")?.RendererProfile);
    }

    [Fact]
    public void 전환애니메이션표시를끄면_내장장면으로폴백하도록_null을반환한다()
    {
        var service = new PlatformCommunityDecorationStateService();

        service.SetTargetEnabled(CommunityDecorationTarget.BaguaTransitionMotion, false);

        Assert.Null(service.ResolveBaguaMotion(
            "bagua-motion:orderer:order-to-transport",
            "relay"));
    }

    [Fact]
    public void 서버보유권동기화_유료상품보유상태를_교체한다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var paidProduct = service.Products.First(product =>
            !product.IsFree && product.Assets.Any(asset => asset.NodeSticker is not null));

        Assert.False(service.IsProductOwned(paidProduct));

        service.SynchronizeServerOwnedPacks([paidProduct.PackKey]);

        Assert.True(service.IsProductOwned(paidProduct));

        service.SynchronizeServerOwnedPacks([]);

        Assert.False(service.IsProductOwned(paidProduct));
    }

    [Fact]
    public void 서버보유권초기화_무료상품보유상태는_유지한다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var freeProduct = service.Products.First(product => product.IsFree);

        service.SynchronizeServerOwnedPacks(["temporary-paid-pack"]);
        service.ClearServerOwnedPacks();

        Assert.True(service.IsProductOwned(freeProduct));
    }

    [Fact]
    public void 경전고전연결팩은_상점의무료홈테마로_등록된다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var scriptureProducts = service.Products
            .Where(product => product.ScriptureSource is not null)
            .ToArray();

        Assert.Equal(ScriptureDecorationCatalog.Definitions.Count, scriptureProducts.Length);
        Assert.All(scriptureProducts, product =>
        {
            Assert.True(product.IsHomeTheme);
            Assert.True(service.IsProductOwned(product));
            Assert.Equal("Hongdal Lecture Link", product.CreatorName);
        });
    }

    [Fact]
    public void 승인된전통시장팩은_해당시장범위에서만_기본해석된다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var product = service.Products.Single(item =>
            string.Equals(item.PackKey, "market-theme-seongnam-harvest-v1", StringComparison.OrdinalIgnoreCase));

        var matching = service.ResolveTraditionalMarketThemeProduct("traditional-market:sample-seongnam");
        var unrelated = service.ResolveTraditionalMarketThemeProduct("traditional-market:another-market");

        Assert.Same(product, matching);
        Assert.Null(unrelated);
        Assert.True(product.TraditionalMarketTheme?.IsOfficiallyApplicable);
        Assert.True(service.IsProductActive(product));
    }

    [Fact]
    public void 전통시장팩카탈로그는_공식적용가능한시장범위상품을_생성한다()
    {
        var products = TraditionalMarketThemeCatalog.CreateProducts();

        Assert.Equal(TraditionalMarketThemeCatalog.Definitions.Count, products.Count);
        Assert.All(products, product =>
        {
            Assert.True(product.IsTraditionalMarketTheme);
            Assert.True(product.TraditionalMarketTheme?.IsOfficiallyApplicable);
            Assert.StartsWith(
                "traditional-market:",
                product.TraditionalMarketTheme!.MarketScopeKey,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void 디자이너의전통시장팩초안은_검수와상인회승인전에는_적용되지않는다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var sampleManifest = service.Products
            .Single(item => item.IsTraditionalMarketTheme)
            .TraditionalMarketTheme! with
        {
            MarketScopeKey = "traditional-market:designer-pilot",
            MarketName = "디자이너 참여 시장"
        };

        var created = service.TryCreateTraditionalMarketThemePackage(
            "우리 시장 이야기",
            "동네 디자이너",
            "시장과 함께 검토할 표식 묶음",
            0,
            sampleManifest,
            out var product,
            out var message);

        Assert.True(created);
        Assert.NotNull(product);
        Assert.Equal("초안", product.TraditionalMarketTheme?.ReviewStatus);
        Assert.Equal("검토 전", product.TraditionalMarketTheme?.AssociationApprovalStatus);
        Assert.False(product.TraditionalMarketTheme?.IsOfficiallyApplicable);
        Assert.False(service.ApplyProduct(product));
        Assert.Null(service.ResolveTraditionalMarketThemeProduct("traditional-market:designer-pilot"));
        Assert.Contains("상인회 승인", message);
    }

    [Fact]
    public void 전통시장팩은_다른시장범위에_적용할수없다()
    {
        var service = new PlatformCommunityDecorationStateService();

        var applied = service.ApplyTraditionalMarketThemePack(
            "traditional-market:another-market",
            "market-theme-seongnam-harvest-v1");

        Assert.False(applied);
        Assert.Empty(service.ActiveTraditionalMarketThemePackByScope);
    }

    [Fact]
    public void 전통시장팩초안은_시장식별자가없는범위키를_거부한다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var sampleManifest = service.Products
            .Single(item => item.IsTraditionalMarketTheme)
            .TraditionalMarketTheme! with
        {
            MarketScopeKey = "traditional-market:"
        };

        var created = service.TryCreateTraditionalMarketThemePackage(
            "식별자 없는 시장 팩",
            "동네 디자이너",
            "잘못된 범위를 검증하는 초안",
            0,
            sampleManifest,
            out var product,
            out var message);

        Assert.False(created);
        Assert.Null(product);
        Assert.Contains("traditional-market:", message);
    }
}
