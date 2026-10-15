using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Contracts.Common;

public sealed class PageCapabilityCatalogTests
{
    [Theory]
    [InlineData("/activity-settlements")]
    [InlineData("/activity-settlements/agreement-1")]
    public void 활동정산_목록과상세는_Web과통합앱에서_인증필수3점5BetaSimulation으로분류한다(string route)
    {
        foreach (var app in new[] { SsalddelPageAppCodes.IntegratedWeb, SsalddelPageAppCodes.Shipper })
        {
            Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, route, out var capability));
            var prefix = app == SsalddelPageAppCodes.IntegratedWeb ? "web" : "shipper";
            Assert.Equal(prefix + "-activity-settlements", capability.PageKey);
            Assert.Equal(app, capability.AppCode);
            Assert.Equal("/activity-settlements", capability.RoutePattern);
            Assert.Equal(PageCapabilityMatchKind.Prefix, capability.MatchKind);
            Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
            Assert.Equal(PageInteractionBoundary.Simulation, capability.Boundary);
            Assert.True(capability.RequiresAuthentication);
            Assert.False(capability.HasExternalEffects);
            Assert.Equal("3.5", capability.IntroducedVersion);
            Assert.Contains("기본 비활성 정산 연습", capability.Notice);
            Assert.Equal(["WarehouseFulfillment", "FoodDelivery", "DomesticTransport"], capability.WorkflowCodes);
            Assert.Single(SsalddelPageCapabilityCatalog.GetAll(), item => item.AppCode == app
                && item.RoutePattern == "/activity-settlements" && item.MatchKind == PageCapabilityMatchKind.Prefix);
        }
    }

    [Fact]
    public void 제품로드맵은_개별주문뒤에_같이주문과무역준비를배치한다()
    {
        Assert.Equal(
            ["0.0", "0.5", "1.0", "1.5", "2.0", "2.5", "3.0", "3.5"],
            SsalddelProductRoadmapCatalog.All.Select(stage => stage.Version));
        Assert.Equal("0.0", SsalddelProductRoadmapCatalog.CurrentVersion);
        Assert.Equal("0.0", SsalddelProductRoadmapCatalog.CurrentDeliveryVersion);
        Assert.Equal("3.5", SsalddelProductRoadmapCatalog.DeploymentTargetVersion);
        Assert.Equal(
            SsalddelProductRoadmapCatalog.FoundationVersion,
            Assert.Single(SsalddelProductRoadmapCatalog.All, stage => stage.IsCurrent).Version);
        Assert.Equal("개별주문·개별 원장", SsalddelProductRoadmapCatalog.Find("0.5").DisplayName);
        Assert.Equal("같이 주문·주문자 집단화", SsalddelProductRoadmapCatalog.Find("1.0").DisplayName);
        Assert.Equal(
            ["0.0", "0.5", "1.0", "1.5"],
            SsalddelProductRoadmapCatalog.All
                .Where(stage => stage.IsCultureTransport)
                .Select(stage => stage.Version));
        Assert.All(
            SsalddelProductRoadmapCatalog.All.Take(4),
            stage => Assert.Equal("문화교통", stage.ProductName));
        Assert.Equal(
            "문화교통 1.5 · 공급·가격·무역 준비",
            SsalddelProductRoadmapCatalog.Find("1.5").FullDisplayName);
        Assert.False(SsalddelProductRoadmapCatalog.IsCultureTransportVersion("2.0"));
        Assert.Equal("0.0", SsalddelProductRoadmapCatalog.Find("0.5").PrerequisiteVersion);
        Assert.Equal("0.5", SsalddelProductRoadmapCatalog.Find("1.0").PrerequisiteVersion);
        Assert.Equal("1.0", SsalddelProductRoadmapCatalog.Find("1.5").PrerequisiteVersion);
        Assert.Equal("1.5", SsalddelProductRoadmapCatalog.Find("2.0").PrerequisiteVersion);
    }

    [Theory]
    [InlineData("community-group-purchase-public", "1.0")]
    [InlineData("us-korean-food-group-buy", "1.0")]
    [InlineData("global-home", "1.5")]
    [InlineData("shipper-request", "2.0")]
    [InlineData("shipper-warehouse", "2.5")]
    public void 대표화면은_공동구매우선로드맵버전을사용한다(
        string pageKey,
        string expectedVersion)
    {
        var capability = Assert.Single(
            SsalddelPageCapabilityCatalog.GetAll(),
            item => item.PageKey == pageKey);

        Assert.Equal(expectedVersion, capability.IntroducedVersion);
    }

    [Fact]
    public void 페이지_키와_앱별_라우트_규칙은_중복되지_않는다()
    {
        var capabilities = SsalddelPageCapabilityCatalog.GetAll();

        Assert.NotEmpty(capabilities);
        Assert.DoesNotContain(
            capabilities.GroupBy(item => item.PageKey, StringComparer.Ordinal),
            group => group.Count() > 1);
        Assert.DoesNotContain(
            capabilities.GroupBy(
                item => $"{item.AppCode}|{item.MatchKind}|{item.RoutePattern}",
                StringComparer.OrdinalIgnoreCase),
            group => group.Count() > 1);
    }

    [Fact]
    public void 핵심_업무_흐름은_각각_대표_page_capability를_가진다()
    {
        var workflowCodes = SsalddelPageCapabilityCatalog.GetAll()
            .SelectMany(item => item.WorkflowCodes)
            .ToHashSet(StringComparer.Ordinal);
        var expected = new[]
        {
            "DomesticTransport",
            "WarehouseFulfillment",
            "CustomsAndTradeData",
            "GroupPurchaseDemand",
            "GroupPurchaseImport",
            "SalesChannelFulfillment",
            "CommunityTrust",
            "HrParticipation",
            "FoodDelivery",
            "SsalddelMart"
        };

        Assert.All(expected, workflow => Assert.Contains(workflow, workflowCodes));
    }

    [Fact]
    public void 외부_효과가_있는_페이지는_운영_단계로_분류하지_않는다()
    {
        var unsafeLivePages = SsalddelPageCapabilityCatalog.GetAll()
            .Where(item => item.HasExternalEffects && item.Stage == PageCapabilityStage.Live)
            .Select(item => item.PageKey)
            .ToArray();

        Assert.Empty(unsafeLivePages);
    }

    [Fact]
    public void 공식재료페이지는_비로그인읽기전용으로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/information/food-ingredients?searchText=양파",
            out var capability);

        Assert.True(found);
        Assert.Equal("official-food-ingredients", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Live, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
    }

    [Fact]
    public void KAMIS국내가격비교페이지는_0점0공개읽기전용으로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/information/kamis-domestic-price-comparison",
            out var capability);

        Assert.True(found);
        Assert.Equal("kamis-domestic-price-comparison", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Live, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.Equal(SsalddelProductRoadmapCatalog.FoundationVersion, capability.IntroducedVersion);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
    }

    [Fact]
    public void USDA미국국내가격비교페이지는_0점0공개읽기전용으로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/information/usda-us-price-comparison",
            out var capability);

        Assert.True(found);
        Assert.Equal("usda-us-price-comparison", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Live, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.Equal(SsalddelProductRoadmapCatalog.FoundationVersion, capability.IntroducedVersion);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
    }

    [Fact]
    public void 지역문화특산물페이지는_0점0커뮤니티의_공개읽기전용으로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/community/regions",
            out var capability);

        Assert.True(found);
        Assert.Equal("regional-culture-specialties", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Live, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.Equal(SsalddelProductRoadmapCatalog.FoundationVersion, capability.IntroducedVersion);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
    }

    [Fact]
    public void 창고입고예정페이지는_인증된서버조회Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Warehouse,
            "/warehouse/inbounds/expected",
            out var capability);

        Assert.True(found);
        Assert.Equal("warehouse-app-expected-inbounds", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("WarehouseFulfillmentWorkflow", capability.FeatureKeys);
    }

    [Fact]
    public void 창고작업보드는_인증된같은Id서버조회Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Warehouse,
            "/work-board?inboundId=29",
            out var capability);

        Assert.True(found);
        Assert.Equal("warehouse-app-work-board", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("WarehouseFulfillmentWorkflow", capability.FeatureKeys);
    }

    [Theory]
    [InlineData("WarehouseManagerApp", "/work/inbound/products?inboundId=88", "warehouse-app-inbound-products")]
    [InlineData("Ssalddel.WebApp", "/warehouse/work/inbound/products?inboundId=88", "web-warehouse-inbound-products")]
    [InlineData("Ssalddel.WebApp", "/work/inbound/products?inboundId=88", "web-warehouse-inbound-products-alias")]
    public void 입고상품수령페이지는_명시적수령을영속하고외부효과가없는Beta로분류한다(
        string appCode,
        string route,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("WarehouseFulfillmentWorkflow", capability.FeatureKeys);
        Assert.Contains("WarehouseFulfillment", capability.WorkflowCodes);
        Assert.Contains("도착·수령", capability.Notice);
        Assert.Contains("자동 실행하지 않습니다", capability.Notice);
    }

    [Theory]
    [InlineData("WarehouseManagerApp", "/work/inbound/inspection?inboundItemId=88", "warehouse-app-inbound-inspection")]
    [InlineData("Ssalddel.WebApp", "/warehouse/work/inbound/inspection?inboundItemId=88", "web-warehouse-inbound-inspection")]
    [InlineData("Ssalddel.WebApp", "/work/inbound/inspection?inboundItemId=88", "web-warehouse-inbound-inspection-alias")]
    [InlineData("WarehouseManagerApp", "/work/inbound/inspection/88", "warehouse-app-inbound-inspection-routes")]
    [InlineData("WarehouseManagerApp", "/work/inbound/inspection/88/record", "warehouse-app-inbound-inspection-routes")]
    [InlineData("Ssalddel.WebApp", "/warehouse/work/inbound/inspection/88", "web-warehouse-inbound-inspection-routes")]
    [InlineData("Ssalddel.WebApp", "/work/inbound/inspection/88/record", "web-warehouse-inbound-inspection-alias-routes")]
    public void 입고검수페이지는_운영창고원장을변경하는Beta로분류한다(
        string appCode,
        string route,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("WarehouseFulfillmentWorkflow", capability.FeatureKeys);
        Assert.Contains("WarehouseFulfillment", capability.WorkflowCodes);
        Assert.Contains("적재", capability.Notice);
    }

    [Theory]
    [InlineData("WarehouseManagerApp", "/work/picking-batch?taskKey=PICK-88", "warehouse-app-picking-task")]
    [InlineData("Ssalddel.WebApp", "/warehouse/work/picking-batch?taskKey=PICK-88", "web-warehouse-picking-task")]
    [InlineData("Ssalddel.WebApp", "/work/picking-batch?taskKey=PICK-88", "web-warehouse-picking-task-alias")]
    [InlineData("WarehouseManagerApp", "/work/picking-batch/PICK-88", "warehouse-app-picking-task-routes")]
    [InlineData("WarehouseManagerApp", "/work/picking-batch/PICK-88/execute", "warehouse-app-picking-task-routes")]
    [InlineData("Ssalddel.WebApp", "/warehouse/work/picking-batch/PICK-88", "web-warehouse-picking-task-routes")]
    [InlineData("Ssalddel.WebApp", "/work/picking-batch/PICK-88/execute", "web-warehouse-picking-task-alias-routes")]
    public void 피킹작업페이지는_피킹상태만변경하는BetaSimulation으로분류한다(
        string appCode,
        string route,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.Simulation, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("WarehouseFulfillmentWorkflow", capability.FeatureKeys);
        Assert.Contains("WarehouseFulfillment", capability.WorkflowCodes);
        Assert.Contains("재고", capability.Notice);
        Assert.Contains("정산", capability.Notice);
    }

    [Theory]
    [InlineData("WarehouseManagerApp", "/warehouse/general/inventory?inboundItemId=88", "warehouse-app-inventory-overview")]
    [InlineData("Ssalddel.WebApp", "/warehouse/general/inventory?inboundItemId=88", "web-warehouse-inventory-overview")]
    [InlineData("Ssalddel.WebApp", "/warehouse/inventory?inboundItemId=88", "web-warehouse-inventory-overview-alias")]
    public void 재고현황페이지는_창고범위의Beta읽기전용으로분류한다(
        string appCode,
        string route,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("WarehouseFulfillmentWorkflow", capability.FeatureKeys);
        Assert.Contains("WarehouseFulfillment", capability.WorkflowCodes);
        Assert.Contains("계약", capability.Notice);
    }

    [Theory]
    [InlineData("WarehouseManagerApp", "/work/inbound/put-away?inboundItemId=8", "warehouse-app-put-away-task", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("Ssalddel.WebApp", "/warehouse/work/inbound/put-away?inboundItemId=8", "web-warehouse-put-away-task", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("Ssalddel.WebApp", "/work/inbound/put-away?inboundItemId=8", "web-warehouse-put-away-task-alias", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("WarehouseManagerApp", "/work/outbound/packing?inboundItemId=8", "warehouse-app-packing-task", PageInteractionBoundary.Simulation)]
    [InlineData("Ssalddel.WebApp", "/warehouse/work/outbound/packing?inboundItemId=8", "web-warehouse-packing-task", PageInteractionBoundary.Simulation)]
    [InlineData("Ssalddel.WebApp", "/work/outbound/packing?inboundItemId=8", "web-warehouse-packing-task-alias", PageInteractionBoundary.Simulation)]
    [InlineData("WarehouseManagerApp", "/warehouse/general/transport-handoff?inboundItemId=8", "warehouse-app-outbound-handoff", PageInteractionBoundary.Simulation)]
    [InlineData("Ssalddel.WebApp", "/warehouse/general/transport-handoff?inboundItemId=8", "web-warehouse-outbound-handoff", PageInteractionBoundary.Simulation)]
    [InlineData("Ssalddel.WebApp", "/work/outbound/handoff?inboundItemId=8", "web-warehouse-outbound-handoff-alias", PageInteractionBoundary.Simulation)]
    public void 적재와후속작업은_실제저장경계대로분류한다(string appCode,string route,string pageKey,PageInteractionBoundary expectedBoundary)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(appCode,route,out var capability));
        Assert.Equal(pageKey,capability.PageKey); Assert.Equal(PageCapabilityStage.Beta,capability.Stage);
        Assert.Equal(expectedBoundary,capability.Boundary); Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects); Assert.Contains("WarehouseFulfillmentWorkflow",capability.FeatureKeys);
    }

    [Theory]
    [InlineData("WarehouseManagerApp", "/warehouse/general/outbound-plan-review?outboundPlanId=11", "warehouse-app-outbound-plan-review")]
    [InlineData("Ssalddel.WebApp", "/warehouse/general/outbound-plan-review?outboundPlanId=11", "web-warehouse-outbound-plan-review")]
    [InlineData("Ssalddel.WebApp", "/work/outbound/plans?outboundPlanId=11", "web-warehouse-outbound-plan-review-alias")]
    public void 출고예정검토페이지는_BetaReadOnly로분류한다(string appCode, string route, string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("WarehouseFulfillmentWorkflow", capability.FeatureKeys);
        Assert.Contains("WarehouseFulfillment", capability.WorkflowCodes);
    }

    [Theory]
    [InlineData("WarehouseManagerApp", "/warehouse/general/transport-request-draft?outboundPlanId=11", "warehouse-app-transport-request-draft")]
    [InlineData("Ssalddel.WebApp", "/warehouse/general/transport-request-draft?outboundPlanId=11", "web-warehouse-transport-request-draft")]
    [InlineData("Ssalddel.WebApp", "/work/outbound/transport-request-draft?outboundPlanId=11", "web-warehouse-transport-request-draft-alias")]
    public void 운송의뢰초안페이지는_BetaSimulation무효력으로분류한다(string appCode, string route, string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.Simulation, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("WarehouseFulfillmentWorkflow", capability.FeatureKeys);
        Assert.Contains("WarehouseFulfillment", capability.WorkflowCodes);
    }

    [Fact]
    public void 마트피킹페이지는_인증된영속작업조회Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Warehouse,
            "/mart/picking/orders/73",
            out var capability);

        Assert.True(found);
        Assert.Equal("warehouse-app-mart-picking-orders", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("3.5", capability.IntroducedVersion);
        Assert.Contains("SsalddelMartWorkflow", capability.FeatureKeys);
        Assert.Contains("SsalddelMart", capability.WorkflowCodes);
    }

    [Theory]
    [InlineData("/warehouse/mart/picking", "web-warehouse-mart-picking")]
    [InlineData("/warehouse/mart/picking/orders/73", "web-warehouse-mart-picking-orders")]
    [InlineData("/mart/picking", "web-mart-picking-alias")]
    [InlineData("/mart/picking/orders/73", "web-mart-picking-orders-alias")]
    public void 통합웹마트피킹경로도_같은읽기전용경계를사용한다(string route, string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("SsalddelMartWorkflow", capability.FeatureKeys);
    }

    [Theory]
    [InlineData("WarehouseManagerApp", "/mart", "warehouse-app-mart-home", true)]
    [InlineData("WarehouseManagerApp", "/mart/work-board", "warehouse-app-mart-work-board", true)]
    [InlineData("Ssalddel.WebApp", "/warehouse/mart", "web-warehouse-mart-home", false)]
    [InlineData("Ssalddel.WebApp", "/warehouse/mart/work-board", "web-warehouse-mart-work-board", false)]
    [InlineData("Ssalddel.WebApp", "/mart", "web-mart-home-alias", false)]
    public void 마트허브는_샘플상태없는3점5읽기전용진입점이다(
        string appCode,
        string route,
        string pageKey,
        bool requiresAuthentication)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability));
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal("3.5", capability.IntroducedVersion);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.Equal(requiresAuthentication, capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("SsalddelMartWorkflow", capability.FeatureKeys);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "diagram")]
    [InlineData(SsalddelPageAppCodes.Shipper, "shipper-diagram")]
    public void 다이어그램은_Web과모바일모두_무효력Simulation으로분류한다(
        string appCode,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            appCode,
            "/diagram?ledgerTemplate=group-purchase&node=%EC%88%98%EC%9A%94%20%EB%AA%A8%EC%A7%91&zoom=120",
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Experience, capability.Stage);
        Assert.Equal(PageInteractionBoundary.Simulation, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("0.0", capability.IntroducedVersion);
        Assert.Contains("CommunityTrustWorkflow", capability.FeatureKeys);
    }

    [Theory]
    [InlineData("/community", "shipper-community-home", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("/community/boards?q=창고&filter=추천글", "shipper-community-boards", PageInteractionBoundary.ReadOnly)]
    [InlineData("/community/write?board=자유", "shipper-community-write", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("/community/posts/42?from=%2Fcommunity%2Fboards", "shipper-community-posts", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("/community/workspace", "shipper-community-workspace", PageInteractionBoundary.ReadOnly)]
    [InlineData("/community/ledgers/new", "shipper-community-ledger-draft", PageInteractionBoundary.PlatformPersistence)]
    public void 모바일커뮤니티route는_Web공용Screen의실행경계를명시한다(
        string route,
        string pageKey,
        PageInteractionBoundary boundary)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Shipper,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Live, capability.Stage);
        Assert.Equal(boundary, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("0.0", capability.IntroducedVersion);
        Assert.Contains("CommunityTrustWorkflow", capability.FeatureKeys);
    }

    [Fact]
    public void 통합웹_게시판모음은_독립된읽기전용route로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            CommunityPageRoutes.BoardDirectory,
            out var capability);

        Assert.True(found);
        Assert.Equal("community-board-directory", capability.PageKey);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
    }

    [Fact]
    public void 공동구매목록은_익명공개읽기Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/community/group-purchase?campaignId=ad1bcf9c-7e02-4dc4-a17a-96f9d4818f15",
            out var capability);

        Assert.True(found);
        Assert.Equal("community-group-purchase-public", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("1.0", capability.IntroducedVersion);
        Assert.Contains("CommunityTrustWorkflow", capability.FeatureKeys);
        Assert.Contains("CommunityTrust", capability.WorkflowCodes);
    }

    [Fact]
    public void 공동구매개설은_인증된플랫폼저장으로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/community/group-purchase/new",
            out var capability);

        Assert.True(found);
        Assert.Equal("community-group-purchase-create", capability.PageKey);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.True(capability.HasExternalEffects);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "community-group-purchase-demand")]
    [InlineData(SsalddelPageAppCodes.Shipper, "shipper-community-group-purchase-demand")]
    public void 재료수요등록은_Web과모바일에서_공개탐색과로그인저장경계를공유한다(
        string appCode,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            appCode,
            "/community/group-purchase/demand?ingredientKey=ingredient%3Aonion",
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("0.5", capability.IntroducedVersion);
        Assert.Contains("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
        Assert.Contains("로그인한 사용자", capability.Notice);
    }

    [Theory]
    [InlineData(
        "/community/orders/new?materialBundle=sample",
        "shipper-community-individual-order-start",
        false)]
    [InlineData(
        "/community/orders",
        "shipper-community-individual-orders",
        true)]
    public void 모바일커뮤니티의_개별주문route는_05원장경계를명시한다(
        string route,
        string pageKey,
        bool requiresAuthentication)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Shipper,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
        Assert.Equal(requiresAuthentication, capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("0.5", capability.IntroducedVersion);
        Assert.Contains("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/group-purchase/practice", "community-group-purchase-practice")]
    [InlineData(SsalddelPageAppCodes.Orderer, "/group-purchase/practice", "orderer-group-purchase-practice")]
    public void 체험공동구매는_익명무저장Simulation으로_실수요와분리된다(
        string appCode,
        string route,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal("1.0", capability.IntroducedVersion);
        Assert.Equal(PageCapabilityStage.Experience, capability.Stage);
        Assert.Equal(PageInteractionBoundary.Simulation, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("GroupPurchasePracticeWorkflow", capability.FeatureKeys);
        Assert.DoesNotContain("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
        Assert.Contains("가상 이웃", capability.Notice);
    }

    [Fact]
    public void 공동구매상세단계는_익명조회와Command인증경계를함께안내한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/community/group-purchase/ad1bcf9c-7e02-4dc4-a17a-96f9d4818f15/participation",
            out var capability);

        Assert.True(found);
        Assert.Equal("community-group-purchase", capability.PageKey);
        Assert.Equal(PageInteractionBoundary.Simulation, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.True(capability.HasExternalEffects);
        Assert.Contains("별도로 인증", capability.Notice);
    }

    [Fact]
    public void 주문자_같이수입검토는_1점5_읽기전용_무역준비경계다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            "/group-purchase/import-review/ingredient-onion",
            out var capability);

        Assert.True(found);
        Assert.Equal("orderer-group-purchase-import-review", capability.PageKey);
        Assert.Equal("1.5", capability.IntroducedVersion);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
        Assert.Contains("CustomsAndTradeDataWorkflow", capability.FeatureKeys);
        Assert.Contains("GroupPurchaseImport", capability.WorkflowCodes);
        Assert.Contains("계약·결제·신고·운송은 실행하지 않습니다", capability.Notice);
    }

    [Theory]
    [InlineData("/group-purchase/wishes", "orderer-group-purchase-wishes", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/group-purchase/wishes/new", "orderer-group-purchase-wish-create", PageInteractionBoundary.PlatformPersistence, true)]
    [InlineData("/group-purchase/wishes/wish-ledger-17", "orderer-group-purchase-wish-workspace", PageInteractionBoundary.PlatformPersistence, true)]
    [InlineData("/group-purchase/wishes/wish-ledger-17/edit", "orderer-group-purchase-wish-workspace", PageInteractionBoundary.PlatformPersistence, true)]
    public void 주문자_내원함은_목록조회와_본인원장Command경계를_분리한다(
        string route,
        string pageKey,
        PageInteractionBoundary boundary,
        bool hasExternalEffects)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal("0.5", capability.IntroducedVersion);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(boundary, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.Equal(hasExternalEffects, capability.HasExternalEffects);
        Assert.Contains("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
        Assert.Contains("GroupPurchaseDemand", capability.WorkflowCodes);
    }

    [Theory]
    [InlineData("/group-purchase/groups", "orderer-group-purchase-groups")]
    [InlineData("/group-purchase/groups/auto-group-onion", "orderer-group-purchase-group-detail")]
    public void 주문자_내집단은_개별원함에서_연결된_참여집단만_읽는다(
        string route,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal("1.0", capability.IntroducedVersion);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
    }

    [Theory]
    [InlineData("/group-purchase/delivery-scopes", "orderer-delivery-scope-finder")]
    [InlineData("/group-purchase/delivery-scopes/kr%3A11%3A11470%3A1147051000", "orderer-delivery-scope-together-orders")]
    [InlineData("/group-purchase/together-orders/auto-group-onion", "orderer-together-order-public-detail")]
    public void 배송권과_같이주문공개탐색은_인증없이_개인정보를제외한집계만읽는다(
        string route,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal("1.0", capability.IntroducedVersion);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
        Assert.Contains("GroupPurchaseDemand", capability.WorkflowCodes);
    }

    [Theory]
    [InlineData("/group-purchase/imports/group-import-17")]
    [InlineData("/group-purchase/imports/group-import-17/suppliers")]
    [InlineData("/group-purchase/imports/group-import-17/costs")]
    [InlineData("/group-purchase/imports/group-import-17/classification")]
    [InlineData("/group-purchase/imports/group-import-17/logistics-review")]
    [InlineData("/group-purchase/imports/group-import-17/handoff")]
    [InlineData("/group-purchase/imports/group-import-17/consent?autoGroupId=auto-group-onion")]
    public void 주문자_같이수입준비현황은_참여자인증된_1점5_읽기전용화면이다(string route)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal("orderer-group-import-readiness", capability.PageKey);
        Assert.Equal("1.5", capability.IntroducedVersion);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
        Assert.Contains("CustomsAndTradeDataWorkflow", capability.FeatureKeys);
        Assert.Contains("GroupPurchaseImport", capability.WorkflowCodes);
        Assert.Contains("계약·결제·신고·운송은 실행하지 않습니다", capability.Notice);
    }

    [Fact]
    public void 관리자_같이수입작업대는_1점5_인증저장경계다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Admin,
            "/trade-readiness",
            out var capability);

        Assert.True(found);
        Assert.Equal("admin-group-import-trade-readiness", capability.PageKey);
        Assert.Equal("1.5", capability.IntroducedVersion);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.True(capability.HasExternalEffects);
        Assert.Contains("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
        Assert.Contains("CustomsAndTradeDataWorkflow", capability.FeatureKeys);
        Assert.Contains("계약·결제·신고·운송 실행은 열지 않습니다", capability.Notice);
    }

    [Fact]
    public void 주문자_선적조회는_1점5가아닌_2점0_읽기전용준비자산이다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            "/group-purchase/shipments",
            out var capability);

        Assert.True(found);
        Assert.Equal("orderer-group-purchase-shipments", capability.PageKey);
        Assert.Equal("2.0", capability.IntroducedVersion);
        Assert.Equal(PageCapabilityStage.Experience, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("DomesticTransportWorkflow", capability.FeatureKeys);
    }

    [Fact]
    public void 화주HS코드검토페이지는_인증된읽기전용Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/shipper/customs/hs-reviews?reviewId=42",
            out var capability);

        Assert.True(found);
        Assert.Equal("shipper-customs-hs-reviews", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("1.5", capability.IntroducedVersion);
        Assert.Contains("CustomsAndTradeDataWorkflow", capability.FeatureKeys);
        Assert.Contains("CustomsAndTradeData", capability.WorkflowCodes);
    }

    [Fact]
    public void 판매채널연결페이지는_사용자소유원장을저장하는Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/shipper/sales/channels?accountId=17",
            out var capability);

        Assert.True(found);
        Assert.Equal("shipper-sales-channels", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("2.5", capability.IntroducedVersion);
        Assert.Contains("SalesChannelFulfillmentWorkflow", capability.FeatureKeys);
        Assert.Contains("SalesChannelFulfillment", capability.WorkflowCodes);
    }

    [Theory]
    [InlineData("/shipper/inbound/dashboard", "shipper-inbound-dashboard", PageInteractionBoundary.ReadOnly)]
    [InlineData("/shipper/inbound/requests", "shipper-inbound-list", PageInteractionBoundary.ReadOnly)]
    [InlineData("/shipper/inbound/requests/new", "shipper-inbound-create", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("/shipper/warehouses/new", "shipper-warehouse-create", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("/shipper/warehouse/workspace", "shipper-warehouse-workspace", PageInteractionBoundary.ReadOnly)]
    [InlineData("/shipper/warehouse/inventory", "shipper-warehouse-inventory", PageInteractionBoundary.ReadOnly)]
    [InlineData("/warehouse", "web-warehouse-home", PageInteractionBoundary.ReadOnly)]
    [InlineData("/warehouse/work-board", "web-warehouse-work-board", PageInteractionBoundary.ReadOnly)]
    [InlineData("/work-board", "warehouse-work-board-alias", PageInteractionBoundary.ReadOnly)]
    public void 창고이행route는_2점5의_조회와저장책임을분리한다(
        string route,
        string pageKey,
        PageInteractionBoundary boundary)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal("2.5", capability.IntroducedVersion);
        Assert.Equal(boundary, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("WarehouseFulfillmentWorkflow", capability.FeatureKeys);
    }

    [Theory]
    [InlineData("/shipper/sales/products", "shipper-sales-products", PageInteractionBoundary.ReadOnly)]
    [InlineData("/shipper/sales/products/new?inventoryItemId=5001", "shipper-sales-product-create", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("/shipper/sales/listings", "shipper-sales-listings", PageInteractionBoundary.ReadOnly)]
    [InlineData("/shipper/sales/listings/new?productId=10", "shipper-sales-listing-create", PageInteractionBoundary.PlatformPersistence)]
    public void 판매상품과채널출품은_2점5의_목표별route로분리한다(
        string route,
        string pageKey,
        PageInteractionBoundary boundary)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal("2.5", capability.IntroducedVersion);
        Assert.Equal(boundary, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("SalesChannelFulfillmentWorkflow", capability.FeatureKeys);
    }

    [Fact]
    public void 판매채널주문페이지는_영속출고후보를읽는Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/shipper/sales/orders?orderId=73",
            out var capability);

        Assert.True(found);
        Assert.Equal("shipper-sales-orders", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("2.5", capability.IntroducedVersion);
        Assert.Contains("SalesChannelFulfillmentWorkflow", capability.FeatureKeys);
        Assert.Contains("SalesChannelFulfillment", capability.WorkflowCodes);
    }

    [Fact]
    public void 판매채널주문상세는_Web과_모바일에서_같은ReadOnly경계를사용한다()
    {
        foreach (var appCode in new[] { SsalddelPageAppCodes.IntegratedWeb, SsalddelPageAppCodes.Shipper })
        {
            var found = SsalddelPageCapabilityCatalog.TryResolve(
                appCode,
                "/shipper/sales/orders/73?from=%2Fshipper%2Fsales%2Forders%3Fpage%3D2",
                out var capability);

            Assert.True(found);
            Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
            Assert.True(capability.RequiresAuthentication);
            Assert.False(capability.HasExternalEffects);
            Assert.Contains("SalesChannelFulfillmentWorkflow", capability.FeatureKeys);
        }
    }

    [Fact]
    public void 모바일_판매주문이행root는_상태를바꾸지않는목표허브다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Shipper,
            "/shipper/sales/fulfillment",
            out var capability);

        Assert.True(found);
        Assert.Equal("shipper-app-sales-fulfillment", capability.PageKey);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.HasExternalEffects);
    }

    [Theory]
    [InlineData("/shipper/sales/fulfillment/orders/order-key", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/shipper/sales/fulfillment/inventory", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/shipper/sales/fulfillment/picking", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/shipper/sales/fulfillment/packing", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/shipper/sales/fulfillment/samples", PageInteractionBoundary.Simulation, true)]
    [InlineData("/shipper/sales/fulfillment/picking/17", PageInteractionBoundary.Simulation, true)]
    [InlineData("/shipper/sales/fulfillment/packing/23", PageInteractionBoundary.Simulation, true)]
    [InlineData("/shipper/sales/fulfillment/restock-policy", PageInteractionBoundary.Simulation, true)]
    public void 모바일_판매주문이행하위route는_조회와Command경계를분리한다(
        string route,
        PageInteractionBoundary boundary,
        bool hasExternalEffects)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Shipper,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(boundary, capability.Boundary);
        Assert.Equal(hasExternalEffects, capability.HasExternalEffects);
        Assert.True(capability.RequiresAuthentication);
    }

    [Fact]
    public void 음식점메뉴페이지는_익명공개조회Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            "/food/restaurants?restaurantId=31",
            out var capability);

        Assert.True(found);
        Assert.Equal("orderer-food-restaurants", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("3.0", capability.IntroducedVersion);
        Assert.Contains("FoodDeliveryWorkflow", capability.FeatureKeys);
        Assert.Contains("FoodDelivery", capability.WorkflowCodes);
    }

    [Fact]
    public void 음식주문내역페이지는_인증된소유원장읽기Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            "/orders/food?orderNo=FOOD-20260720-01",
            out var capability);

        Assert.True(found);
        Assert.Equal("orderer-food-orders", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("3.0", capability.IntroducedVersion);
        Assert.Contains("FoodDeliveryWorkflow", capability.FeatureKeys);
        Assert.Contains("FoodDelivery", capability.WorkflowCodes);
    }

    [Theory]
    [InlineData("/orders", "orderer-orders")]
    [InlineData("/orders/individual-order-ledger-17?view=orderer", "orderer-order-detail")]
    public void 전체주문원장은_인증된주문자의_개별주문원장만_읽는다(
        string route,
        string pageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal("0.5", capability.IntroducedVersion);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("GroupPurchaseDemandWorkflow", capability.FeatureKeys);
        Assert.Contains("GroupPurchaseDemand", capability.WorkflowCodes);
    }

    [Fact]
    public void 마트상품페이지는_익명공개투영조회Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Orderer,
            "/food/mart?productId=41",
            out var capability);

        Assert.True(found);
        Assert.Equal("orderer-mart", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("3.5", capability.IntroducedVersion);
        Assert.Contains("SsalddelMartWorkflow", capability.FeatureKeys);
        Assert.Contains("SsalddelMart", capability.WorkflowCodes);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.Orderer, "/food/mart/order?productId=41", "orderer-mart-order-request")]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/orderer/mart/order?requestId=116e0c45-8acd-4d24-b468-f522a127bbac", "web-orderer-mart-order-request")]
    public void 마트주문요청페이지는_인증된비구속플랫폼저장Beta로분류한다(
        string appCode,
        string route,
        string expectedPageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(expectedPageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("3.5", capability.IntroducedVersion);
        Assert.Contains("SsalddelMartWorkflow", capability.FeatureKeys);
        Assert.Contains("SsalddelMart", capability.WorkflowCodes);
    }

    [Fact]
    public void 통합웹마트상품페이지도_익명공개조회Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/orderer/mart?productId=41",
            out var capability);

        Assert.True(found);
        Assert.Equal("web-orderer-mart", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("SsalddelMartWorkflow", capability.FeatureKeys);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/food/mart", "web-orderer-mart-canonical", PageInteractionBoundary.ReadOnly, false)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/food/mart/products/41", "web-orderer-mart-detail", PageInteractionBoundary.ReadOnly, false)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/food/mart/reviews/41", "web-orderer-mart-review", PageInteractionBoundary.PlatformPersistence, true)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/food/mart/order/41", "web-orderer-mart-order-request-canonical", PageInteractionBoundary.PlatformPersistence, true)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/orderer/mart/products/41", "web-orderer-mart-detail-legacy", PageInteractionBoundary.ReadOnly, false)]
    [InlineData(SsalddelPageAppCodes.Orderer, "/food/mart/products/41", "orderer-mart-detail", PageInteractionBoundary.ReadOnly, false)]
    [InlineData(SsalddelPageAppCodes.Orderer, "/food/mart/reviews/41", "orderer-mart-review", PageInteractionBoundary.PlatformPersistence, true)]
    [InlineData(SsalddelPageAppCodes.Orderer, "/food/mart/order/41", "orderer-mart-order-request", PageInteractionBoundary.PlatformPersistence, true)]
    public void 마트상품책임별Route는_조회와명시적저장경계를분리한다(
        string appCode,
        string route,
        string expectedPageKey,
        PageInteractionBoundary expectedBoundary,
        bool expectedAuthentication)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(expectedPageKey, capability.PageKey);
        Assert.Equal(expectedBoundary, capability.Boundary);
        Assert.Equal(expectedAuthentication, capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Contains("SsalddelMartWorkflow", capability.FeatureKeys);
        Assert.Contains("SsalddelMart", capability.WorkflowCodes);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/decorations", "community-decoration-store", PageInteractionBoundary.ReadOnly, false, false)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/decorations/products/theme-pack", "community-decoration-products", PageInteractionBoundary.PlatformPersistence, false, false)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/decorations/checkout/theme-pack", "community-decoration-checkout", PageInteractionBoundary.Simulation, true, true)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/decorations/theme-pack", "community-decoration-legacy", PageInteractionBoundary.Simulation, true, true)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/decorations", "shipper-app-community-decoration-store", PageInteractionBoundary.ReadOnly, false, false)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/decorations/products/theme-pack", "shipper-app-community-decoration-products", PageInteractionBoundary.PlatformPersistence, false, false)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/decorations/checkout/theme-pack", "shipper-app-community-decoration-checkout", PageInteractionBoundary.Simulation, true, true)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/decorations/theme-pack/checkout", "shipper-app-community-decoration-legacy", PageInteractionBoundary.Simulation, true, true)]
    public void 꾸미기Route는_Web과모바일에서_조회와FakePG경계를같게분리한다(
        string appCode,
        string route,
        string expectedPageKey,
        PageInteractionBoundary expectedBoundary,
        bool expectedAuthentication,
        bool expectedExternalEffects)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(expectedPageKey, capability.PageKey);
        Assert.Equal(expectedBoundary, capability.Boundary);
        Assert.Equal(expectedAuthentication, capability.RequiresAuthentication);
        Assert.Equal(expectedExternalEffects, capability.HasExternalEffects);
        Assert.Contains("CommunityTrustWorkflow", capability.FeatureKeys);
        Assert.Contains("CommunityTrust", capability.WorkflowCodes);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "shipper-home")]
    [InlineData(SsalddelPageAppCodes.Shipper, "shipper-app-home")]
    public void 화주허브는_Web과모바일에서_공개읽기전용0점0화면이다(
        string appCode,
        string expectedPageKey)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            appCode,
            "/shipper?from=%2Fcommunity",
            out var capability);

        Assert.True(found);
        Assert.Equal(expectedPageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Live, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("0.0", capability.IntroducedVersion);
        Assert.Contains("CommunityTrustWorkflow", capability.FeatureKeys);
    }

    [Fact]
    public void 별도분류가없는Web화주하위도구는_보수적인Simulationfallback을유지한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/shipper/future-tool",
            out var capability);

        Assert.True(found);
        Assert.Equal("shipper-fallback", capability.PageKey);
        Assert.Equal(PageInteractionBoundary.Simulation, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.True(capability.HasExternalEffects);
    }

    [Theory]
    [InlineData("/shipper/request", "shipper-request", PageInteractionBoundary.PlatformPersistence, false)]
    [InlineData("/shipper/request/cargo", "shipper-request-cargo", PageInteractionBoundary.PlatformPersistence, false)]
    [InlineData("/shipper/request/REQ-20", "shipper-request-detail", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/shipper/request/REQ-20/timeline", "shipper-request-detail", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/shipper/request/REQ-20/payment", "shipper-request-detail", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/shipper/request/payment-status", "shipper-payment-status", PageInteractionBoundary.ReadOnly, false)]
    public void 화주_2점0_운송화면은_작성과_책임별상세경계를_분리한다(
        string route,
        string expectedPageKey,
        PageInteractionBoundary expectedBoundary,
        bool expectedExternalEffects)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(expectedPageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(expectedBoundary, capability.Boundary);
        Assert.Equal(expectedExternalEffects, capability.HasExternalEffects);
        Assert.Equal("2.0", capability.IntroducedVersion);
        Assert.Contains("DomesticTransportWorkflow", capability.FeatureKeys);
    }

    [Theory]
    [InlineData("/driver/recommendations", "driver-recommendation-list", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/driver/recommendations/REQ-20", "driver-recommendation-detail", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/driver/dispatch-decisions/REQ-20", "driver-dispatch-decisions", PageInteractionBoundary.Simulation, true)]
    [InlineData("/driver/transports/current", "driver-current-transport", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/driver/transports/history", "driver-transport-history", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/driver/transport/proof?transportId=20", "driver-proof-stage-selector", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/driver/transports/20/pickup", "driver-transports", PageInteractionBoundary.Simulation, true)]
    [InlineData("/driver/transports/20/dropoff", "driver-transports", PageInteractionBoundary.Simulation, true)]
    [InlineData("/driver/settlements/current-month", "driver-settlements", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/driver/notifications", "driver-notification-inbox", PageInteractionBoundary.ReadOnly, false)]
    [InlineData("/driver/notifications/settings", "driver-notifications", PageInteractionBoundary.PlatformPersistence, false)]
    public void 기사_2점0_화면은_조회와_상태변경을_별도_route로_분리한다(
        string route,
        string expectedPageKey,
        PageInteractionBoundary expectedBoundary,
        bool expectedExternalEffects)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            route,
            out var capability);

        Assert.True(found);
        Assert.Equal(expectedPageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(expectedBoundary, capability.Boundary);
        Assert.Equal(expectedExternalEffects, capability.HasExternalEffects);
        Assert.True(capability.RequiresAuthentication);
        Assert.Equal("2.0", capability.IntroducedVersion);
        Assert.Contains("DomesticTransportWorkflow", capability.FeatureKeys);
    }

    [Fact]
    public void 인사역할검토홈은_인증된영속배정원장조회Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.HumanResources,
            "/?reviewId=2f4050b7-4784-44f7-a704-d895c4f2cc13",
            out var capability);

        Assert.True(found);
        Assert.Equal("human-resources-role-reviews", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("2.5", capability.IntroducedVersion);
        Assert.Contains("HrParticipationWorkflow", capability.FeatureKeys);
        Assert.Contains("HrParticipation", capability.WorkflowCodes);
    }

    [Fact]
    public void 커뮤니티역할지원은_인증된철회가능플랫폼저장Beta로분류한다()
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.IntegratedWeb,
            "/community/roles/apply",
            out var capability);

        Assert.True(found);
        Assert.Equal("community-role-application", capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
        Assert.True(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal("2.5", capability.IntroducedVersion);
        Assert.Contains("HrParticipationWorkflow", capability.FeatureKeys);
        Assert.Contains("HrParticipation", capability.WorkflowCodes);
    }
}
