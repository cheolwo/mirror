using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Tests.Architecture;

public sealed class CommunityWorldMapHomeCompositionTests
{
    [Fact]
    public void 세계지도는_로그인역할에맞는Layer를기본활성화하고_다른공개Layer선택도유지한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var styleSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");

        Assert.Contains("@inject WebAuthSessionService AuthSession", pageSource);
        Assert.Contains("커뮤니티세계지도RoleLayerProfileCatalog.Resolve(AuthSession.PrimaryRole)", pageSource);
        Assert.Contains("ActiveRoleProfile.RecommendedLayerCodes", pageSource);
        Assert.Contains("역할 기본 레이어 복원", pageSource);
        Assert.Contains("추가 공개 자료", pageSource);
        Assert.Contains("world-community-home__role-profile", pageSource);
        Assert.Contains(".world-community-home__role-profile", styleSource);
        Assert.Contains(".is-role-optional", styleSource);
    }

    [Fact]
    public void 세계지도는_원장구조에서고른공개인계관점을_기존공식Marker에중복없이연결한다()
    {
        var contractSource = ReadRepositoryFile(
            "Ssalddel.Contracts",
            "Common",
            "Community",
            "커뮤니티세계지도Dtos.cs");
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var styleSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");

        Assert.Contains("ProcurementHandoff = \"procurement-handoff\"", contractSource);
        Assert.Contains("ImportReadiness = \"import-readiness\"", contractSource);
        Assert.Contains("TransportHandoff = \"transport-handoff\"", contractSource);
        Assert.Contains("WarehouseInboundHandoff = \"warehouse-inbound-handoff\"", contractSource);
        Assert.Contains("ObservationSourceLayerCodes", contractSource);
        Assert.Contains("CommunityLedgerTemplateKeys.MeatImportReadiness", contractSource);
        Assert.Contains("CommunityLedgerTemplateKeys.CargoTransport", contractSource);
        Assert.Contains("CommunityLedgerTemplateKeys.WarehouseInbound", contractSource);
        Assert.Contains("IsObservationSourceLayerVisible", pageSource);
        Assert.Contains("LayerHasVisibleSource", pageSource);
        Assert.Contains("layer.LedgerTemplateKey ?? (layer.Code switch", pageSource);
        Assert.Contains("원장 관점 · 공개 레이어", pageSource);
        Assert.Contains(".world-community-home__layer-shape--route", styleSource);
        Assert.Contains(".world-community-home__layer-shape--warehouse", styleSource);
    }

    [Fact]
    public void 세계지도는_해외제조업소를_행정권역집계Layer와공장형Marker로표시한다()
    {
        var contractSource = ReadRepositoryFile(
            "Ssalddel.Contracts",
            "Common",
            "Community",
            "커뮤니티세계지도Dtos.cs");
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var styleSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");
        var scriptSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "js",
            "community-world-google-map.js");
        Assert.Contains("OverseasManufacturer = \"overseas-manufacturer\"", contractSource);
        Assert.Contains("행정권역 대표점", pageSource);
        Assert.Contains("재료 관계 근거", pageSource);
        Assert.Contains("CommunityLedgerTemplateKeys.GroupImport", pageSource);
        Assert.Contains(".world-community-home__layer-shape--factory", styleSource);
        Assert.Contains("case \"overseas-manufacturer\"", scriptSource);
    }

    [Fact]
    public void 지역마커선택Panel은_지역설명과검토상태가있는생성이미지를함께표시한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var styleSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");

        Assert.Contains("@inject 지역문화이미지Client RegionalImageClient", pageSource);
        Assert.Contains("regional-culture-one-each-v1", pageSource);
        Assert.Contains("SelectedRegionImage", pageSource);
        Assert.Contains("AI 생성 이미지", pageSource);
        Assert.Contains("공식 기록·원산지·거래 근거가 아닙니다", pageSource);
        Assert.Contains("지역 이미지를 불러오지 못했습니다", pageSource);
        Assert.Contains("다시 시도", pageSource);
        Assert.Contains(".world-community-home__region-visual", styleSource);
        Assert.Contains("aspect-ratio: 16 / 9", styleSource);
        Assert.Contains("width: min(440px", styleSource);
    }

    [Fact]
    public void 커뮤니티Web시작화면은_공통메뉴없이_전체화면지도와지도조작패널만표시한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var pageStyleSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");
        var layoutSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Layout",
            "MapOnlyLayout.razor");

        Assert.Contains("@layout MapOnlyLayout", pageSource);
        Assert.Contains("world-community-home__map-fallback", pageSource);
        Assert.Contains("preserveAspectRatio=\"xMidYMid meet\"", pageSource);
        Assert.Contains("@Body", layoutSource);
        Assert.DoesNotContain("NavMenu", layoutSource);
        Assert.DoesNotContain("MudAppBar", layoutSource);
        Assert.Contains("height: 100dvh", pageStyleSource);
        Assert.Contains("world-community-home__map-controls", pageSource);
        Assert.Contains("지도 데이터와 레이어 조작", pageSource);
        Assert.Contains("HasSelectedCountry ? \"is-open\"", pageSource);
        Assert.Contains("선택한 지역 상세 닫기", pageSource);
        Assert.Contains(".world-community-home__results", pageStyleSource);
        Assert.Contains(".world-community-home__results.is-open", pageStyleSource);
        Assert.Contains(".world-community-home__boundary", pageStyleSource);
        Assert.Contains("display: none", pageStyleSource);
        Assert.Contains("border-radius: 0", pageStyleSource);
    }

    [Fact]
    public void 커뮤니티Web시작화면은_세계지도에서_지역자료를선택한다()
    {
        var source = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");

        Assert.Contains("@page \"/community/home\"", source);
        Assert.Contains("<svg viewBox=\"0 0 1000 500\"", source);
        Assert.Contains("aria-controls=\"world-map-results\"", source);
        Assert.Contains("Google 지도 마커 키보드 선택", source);
        Assert.Contains("@onchange=\"SelectMapFeatureFromKeyboard\"", source);
        Assert.Contains("@onkeydown=\"HandleResultsPanelKeyDown\"", source);
        Assert.Contains("_pendingFocusElementId = \"world-map-results\"", source);
        Assert.Contains("focusElement", ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "js",
            "community-world-google-map.js"));
        Assert.Contains("RegionalCultureSpecialtyCatalog.ForCountry", source);
        Assert.Contains("지도는 자료를 찾기 위한 개략도", source);
    }

    [Fact]
    public void 세계지도선택은_국가_레이어_마커_관측StableId를Url로복원한다()
    {
        var source = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var deepLink = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Services",
            "CommunityWorldMapDeepLink.cs");

        Assert.Contains("CommunityWorldMapDeepLink.CountryQueryKey", source);
        Assert.Contains("CommunityWorldMapDeepLink.LayersQueryKey", source);
        Assert.Contains("CommunityWorldMapDeepLink.MarkerQueryKey", source);
        Assert.Contains("CommunityWorldMapDeepLink.ObservationQueryKey", source);
        Assert.Contains("CommunityWorldMapDeepLink.SnapshotRevisionQueryKey", source);
        Assert.Contains("CommunityWorldMapDeepLink.SourceVersionQueryKey", source);
        Assert.Contains("ApplyDeepLinkParameters", source);
        Assert.Contains("SyncDeepLinkToUrl", source);
        Assert.Contains("Navigation.GetUriWithQueryParameters", source);
        Assert.Contains("SyncDeepLinkToUrl(replace: true)", source);
        Assert.Contains("public const string NoLayersValue = \"none\"", deepLink);
        Assert.Contains("NormalizeStableId", deepLink);
        Assert.Contains("EvidenceVersionNotice", source);
        Assert.Contains("게시 이후 공개 근거 갱신", source);
    }

    [Fact]
    public void 지도마커상세는_공개출처와신선도를즉시표시하고_가격진입을가격마커로제한한다()
    {
        var source = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");

        Assert.Contains("world-community-home__selection-summary", source);
        Assert.Contains("ObservationStatusLabel(selectedObservation)", source);
        Assert.Contains("EvidenceFreshnessLabel(selectedObservation.EvidenceAsOfUtc)", source);
        Assert.Contains("원장·참여·주문·계약·배차를 만들지 않습니다", source);
        Assert.Contains("SelectedObservation?.LayerCode ?? SelectedMapMarker?.LayerCode", source);
        Assert.Contains("SelectedCountry is not null && IsPriceMarketSelection", source);
        Assert.Contains("@if (IsPriceMarketSelection)", source);
        Assert.Contains("가격·시장 출처", source);
        Assert.Contains("KAMIS와 USDA AMS 출처별 보기", source);
        Assert.Contains("USDA Agricultural Marketing Service (AMS)", source);
        Assert.Contains("USD · 원 포장단위", source);
        Assert.Contains("_isPriceMarketCatalogOpen || IsPriceMarketSelection", source);
        Assert.Contains("서로 다른 국가·시장 단계의 관측을 합산하지 않습니다", source);
        Assert.Contains("SelectedMapMarkerSummary", source);
        Assert.Contains("공개 시장 대표 위치와 시장 단계", source);
        Assert.Contains("market.SourceName", source);
    }

    [Fact]
    public void 세계지도는_Google실제지도조작과_지도형Fallback지형표현을제공한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var styleSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");
        var scriptSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "js",
            "community-world-google-map.js");

        Assert.Contains("world-community-home__ocean", pageSource);
        Assert.Contains("world-community-home__country-boundaries", pageSource);
        Assert.Contains("world-community-home__water-labels", pageSource);
        Assert.Contains("world-community-home__coordinate-labels", pageSource);
        Assert.Contains("world-community-home__compass", pageSource);
        Assert.Contains("world-community-home__scale", pageSource);
        Assert.Contains("지도형 개략도 표시", pageSource);
        Assert.Contains("url(#world-map-land)", styleSource);
        Assert.Contains(".world-community-home--night .world-community-home__ocean", styleSource);

        Assert.Contains("mapTypeId: \"roadmap\"", scriptSource);
        Assert.Contains("mapTypeControl: true", scriptSource);
        Assert.Contains("[\"roadmap\", \"terrain\", \"satellite\"]", scriptSource);
        Assert.Contains("scaleControl: true", scriptSource);
        Assert.Contains("streetViewControl: true", scriptSource);
        Assert.Contains("zoomControl: true", scriptSource);
        Assert.Contains("gestureHandling: \"greedy\"", scriptSource);
        Assert.Contains("mapViewportPadding", scriptSource);
        Assert.Contains("selectedScale", scriptSource);
        Assert.DoesNotContain("case \"public-price\":\n            return { color: \"#ef8f3c\", path: \"M 0,-10 10,0 0,10 -10,0 z\" };", scriptSource);
    }

    [Fact]
    public void 세계지도는_문화자료와_국가별가격근거를함께연결한다()
    {
        var source = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");

        Assert.Contains("RegionalCultureSpecialtyRoutes.DetailFor", source);
        Assert.Contains("커뮤니티세계지도Routes.KoreaPriceDetail", source);
        Assert.Contains("커뮤니티세계지도Routes.UnitedStatesPriceDetail", source);
        Assert.Contains("한국 가격 화면 보기", source);
        Assert.Contains("미국 가격 화면 보기", source);
        Assert.Contains("information/produce-price-comparison", source);
        Assert.Contains("AppRelative", source);
        Assert.Contains("출처·기준 시각·통화·거래 단위", source);
        Assert.Contains("자동 가입·상대 추천·주문·수입·배차를 만들지 않습니다", source);
    }

    [Fact]
    public void 커뮤니티역할WebApp은_지도에서연결한가격화면을포함한다()
    {
        var source = ReadRepositoryFile(
            "eng",
            "web-role-app",
            "Ssalddel.RoleWebApp.props");

        Assert.Contains("PublicDataInformationPage.razor", source);
        Assert.Contains("KamisDomesticPriceComparisonPage.razor", source);
        Assert.Contains("ProduceRegionalPriceComparisonPage.razor", source);
        Assert.Contains("UsdaUnitedStatesPriceComparisonPage.razor", source);
        Assert.Contains("Layout\\MapOnlyLayout.razor", source);
        Assert.Contains("Layout\\MapOnlyLayout.razor.css", source);
    }

    [Fact]
    public void 통합Web지도는_생활업무공개정보만노출한다()
    {
        var source = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");

        Assert.Contains("생활·업무 통합 지도", source);
        Assert.Contains("생활·업무 공개정보", source);
        Assert.Contains("private bool IsNightLearning => false", source);
        Assert.Contains("var requestedMode = MapDatasetMode.DayWork", source);
        Assert.DoesNotContain("class=\"world-community-home__dataset-switch\"", source);
        Assert.DoesNotContain("@onclick=\"ToggleDatasetAsync\"", source);
    }

    [Fact]
    public void 세계지도경로계약은_밤배움데이터셋을_공유가능한질의로유지한다()
    {
        var source = ReadRepositoryFile(
            "Ssalddel.Contracts",
            "Common",
            "Community",
            "CommunityPageRoutes.cs");

        Assert.Contains("WorldMap = \"/community/home\"", source);
        Assert.Contains("WorldMapDayWorkDataset = \"day-work\"", source);
        Assert.Contains("WorldMapNightLearningDataset = \"night-learning\"", source);
        Assert.Contains("WorldMapFor", source);
    }

    [Fact]
    public void 모바일에서는_세계지도가_페이지가아닌지도영역안에서만가로이동한다()
    {
        var source = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");

        Assert.Contains(".world-community-home > *", source);
        Assert.Contains("min-width: 0", source);
        Assert.Contains(".world-community-home__map-scroll", source);
        Assert.Contains("overflow-x: auto", source);
    }

    [Fact]
    public void 세계지도는_Google지도한개에서_생활업무데이터레이어를교체한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var scriptSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "js",
            "community-world-google-map.js");
        var sdkSource = ReadRepositoryFile("Ssalddel.WebApp", "Services", "커뮤니티지도GoogleSdkAdapter.cs");

        Assert.Equal(1, CountOccurrences(pageSource, "id=\"community-world-google-map\""));
        Assert.Contains("./js/community-world-google-map.js", sdkSource);
        Assert.Contains("CurrentMapMarkers", pageSource);
        Assert.Contains("GoogleSdk.UpdateDatasetAsync", pageSource);
        Assert.Contains("\"updateDataset\"", sdkSource);
        Assert.Contains("new GoogleMap(element", scriptSource);
        Assert.Contains("instance.map.data", scriptSource);
        Assert.Contains("auth_referrer_policy", scriptSource);
        Assert.Contains("ssalddelRuntimeConfig", scriptSource);
        Assert.Contains("suppliedRuntimeConfig", scriptSource);
        Assert.Contains("runtime.TryGetAsync", sdkSource);
        Assert.DoesNotContain("AIza", scriptSource);
    }

    [Fact]
    public void 개발ReadOnlySimulation은_지도요청감사쓰기와_HostedService를비활성화한다()
    {
        var programSource = ReadRepositoryFile("Ssalddel", "Program.cs");
        var middlewareSource = ReadRepositoryFile(
            "Ssalddel",
            "Middleware",
            "사용자행위로그Middleware.cs");

        Assert.Contains("executionOptions.DevelopmentReadOnly", programSource);
        Assert.Contains("builder.Services.RemoveAll<IHostedService>()", programSource);
        Assert.Contains("ShouldSkipDevelopmentMapReadAudit", middlewareSource);
        Assert.Contains("/api/v1/platform/runtime/google-maps", middlewareSource);
        Assert.Contains("/api/v1/community/world-map/observations", middlewareSource);
    }

    [Fact]
    public void Google지도BrowserKey는_배포산출물에만주입하고_허용Origin에서만소비한다()
    {
        var indexSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "index.html");
        var runtimeConfigSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "runtime-config.js");
        var mapScriptSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "js",
            "community-world-google-map.js");
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var injectionScriptSource = ReadRepositoryFile(
            "eng",
            "inject-web-runtime-config.ps1");

        var runtimeConfigIndex = indexSource.IndexOf("runtime-config.js", StringComparison.Ordinal);
        var blazorBootIndex = indexSource.IndexOf("_framework/blazor.webassembly", StringComparison.Ordinal);

        Assert.True(runtimeConfigIndex >= 0);
        Assert.True(blazorBootIndex > runtimeConfigIndex);
        Assert.Contains("strict-origin-when-cross-origin", indexSource);
        Assert.Contains("ssalddelRuntimeConfig ??= {}", runtimeConfigSource);
        Assert.DoesNotContain("googleMapsBrowserApiKey", runtimeConfigSource);
        Assert.DoesNotContain("AIza", runtimeConfigSource);

        Assert.Contains("consumeRuntimeValue", mapScriptSource);
        Assert.Contains("googleMapsAllowedOrigins", mapScriptSource);
        Assert.Contains("blocked-origin", mapScriptSource);
        Assert.True(
            mapScriptSource.IndexOf("isRuntimeOriginAllowed(runtimeConfig)", StringComparison.Ordinal)
            < mapScriptSource.IndexOf("consumeRuntimeValue(runtimeConfig", StringComparison.Ordinal));
        Assert.Contains("delete runtimeConfig[configName]", mapScriptSource);
        Assert.DoesNotContain("ssalddel-google-maps-browser-key", mapScriptSource);
        Assert.DoesNotContain("document.querySelector(`meta", mapScriptSource);
        Assert.Contains("strict-origin-when-cross-origin", mapScriptSource);
        Assert.Contains("script.remove()", mapScriptSource);
        Assert.Contains("GoogleMapState.BlockedOrigin", pageSource);

        Assert.Contains("SSALDDEL_GOOGLE_MAPS_BROWSER_API_KEY", injectionScriptSource);
        Assert.Contains("GoogleMaps:BrowserApiKey", injectionScriptSource);
        Assert.DoesNotContain("GoogleMaps:UnifiedApiKey", injectionScriptSource);
        Assert.Contains("Refusing to inject", injectionScriptSource);
        Assert.Contains("AllowLoopback", injectionScriptSource);
        Assert.Contains("Do not mix loopback and remote origins", injectionScriptSource);
        Assert.Contains("must not contain user information", injectionScriptSource);
        Assert.Contains("UriSchemeHttps", injectionScriptSource);
        Assert.Contains("uri.IsLoopback", injectionScriptSource);
        Assert.Contains("ConvertTo-Json -Compress", injectionScriptSource);
        Assert.Contains("runtime-config.js?v=", injectionScriptSource);
    }

    [Fact]
    public void 통합Web지도는_맥락없는낮밤Toggle을노출하지않는다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        Assert.DoesNotContain("class=\"world-community-home__dataset-switch\"", pageSource);
        Assert.DoesNotContain("aria-label=\"@DatasetToggleAriaLabel\"", pageSource);
        Assert.Contains("IsNightLearning => false", pageSource);
    }

    [Fact]
    public void 세계지도는_분야별Layer와_화면이동없는자동자료갱신을제공한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var scriptSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "js",
            "community-world-google-map.js");
        var snapshotSource = ReadRepositoryFile("Ssalddel.WebApp", "Services", "커뮤니티지도SnapshotSession.cs");

        Assert.Contains("지도에 표시할 분야", pageSource);
        Assert.Contains("SnapshotSession.StartPolling", pageSource);
        Assert.Contains("PollMapSnapshotsAsync", snapshotSource);
        Assert.Contains("TimeSpan.FromSeconds(30)", snapshotSource);
        Assert.Contains("preserveViewport: true", snapshotSource);
        Assert.DoesNotContain("_pendingSnapshot", pageSource);
        Assert.DoesNotContain("_pendingSnapshot", snapshotSource);
        Assert.Contains("preserveViewport = false", scriptSource);
        Assert.Contains("!preserveViewport", scriptSource);
        Assert.Contains("markerStyleFor", scriptSource);
        Assert.Contains("regional-culture", scriptSource);
        Assert.Contains("wholesale-market", scriptSource);
        Assert.Contains("traditional-market-hub", scriptSource);
        Assert.Contains("tourism-public-evidence", scriptSource);
        Assert.Contains("online-price-public-evidence", scriptSource);
        Assert.Contains("kosis-statistical-context", scriptSource);
        Assert.Contains("scripture-classics", scriptSource);
    }

    [Fact]
    public void 세계지도하단은_업무영역별원장Template내부절차를_접고펼친다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var styleSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");

        Assert.Contains("id=\"world-map-layer-diagrams\"", pageSource);
        Assert.Contains("aria-controls=\"world-map-layer-diagram-content\"", pageSource);
        Assert.Contains("_isDiagramPanelExpanded", pageSource);
        Assert.Contains("ActiveLayerDiagrams", pageSource);
        Assert.Contains("원장 업무흐름 다이어그램", pageSource);
        Assert.Contains("레이어 알아차림 다이어그램", pageSource);
        Assert.Contains("현재 켜진 레이어가 없습니다", pageSource);
        Assert.Contains("커뮤니티세계지도LayerCodes.RegionalCulture", pageSource);
        Assert.Contains("커뮤니티세계지도LayerCodes.PublicPrice", pageSource);
        Assert.Contains("커뮤니티세계지도LayerCodes.WholesaleMarket", pageSource);
        Assert.Contains("커뮤니티세계지도LayerCodes.TraditionalMarketHub", pageSource);
        Assert.Contains("커뮤니티세계지도LayerCodes.TourismPublicEvidence", pageSource);
        Assert.Contains("커뮤니티세계지도LayerCodes.OnlinePricePublicEvidence", pageSource);
        Assert.Contains("커뮤니티세계지도LayerCodes.KosisStatisticalContext", pageSource);
        Assert.Contains("커뮤니티세계지도LayerCodes.LearningChannel", pageSource);
        Assert.Contains("커뮤니티세계지도LayerCodes.ScriptureAndClassics", pageSource);
        Assert.Contains("CommunityLedgerTemplateKeys.LocalSale", pageSource);
        Assert.Contains("CommunityLedgerTemplateKeys.GroupPurchase", pageSource);
        Assert.Contains("CommunityLedgerTemplateKeys.GroupImport", pageSource);
        Assert.Contains("CommunityLedgerTemplateCatalog.Find", pageSource);
        Assert.Contains("template.LedgerBlocks", pageSource);
        Assert.Contains("template.BlockRelations", pageSource);
        Assert.Contains("template.CompositionRules", pageSource);
        Assert.Contains("업무 영역별 기준 원장 투영", pageSource);
        Assert.DoesNotContain("new(\"04\", \"가원장\"", pageSource);
        Assert.Contains("공개 채널", pageSource);
        Assert.Contains("공개 목록", pageSource);

        foreach (var templateKey in new[]
                 {
                     CommunityLedgerTemplateKeys.LocalSale,
                     CommunityLedgerTemplateKeys.GroupPurchase,
                     CommunityLedgerTemplateKeys.GroupImport
                 })
        {
            var template = CommunityLedgerTemplateCatalog.Find(templateKey);
            Assert.NotEmpty(template.LedgerBlocks);
            Assert.NotEmpty(template.BlockRelations);
        }

        Assert.Contains(".world-community-home__diagram-panel", styleSource);
        Assert.Contains(".world-community-home__diagram-panel.is-expanded", styleSource);
        Assert.Contains(".world-community-home__diagram-track", styleSource);
        Assert.Contains(".world-community-home__diagram-panel.has-results", styleSource);
        Assert.Contains("prefers-reduced-motion: reduce", styleSource);
    }

    [Fact]
    public void 세계지도좌우Panel은_업무영역과선택지역을_같은원장Template에연결한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var styleSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");

        Assert.Contains("업무 영역과 원장 연결", pageSource);
        Assert.Contains("world-community-home__layer-option-copy", pageSource);
        Assert.Contains("world-community-home__layer-ledger-summary", pageSource);
        Assert.Contains("{layerDiagram.DisplayName} 연결", pageSource);
        Assert.Contains("SelectedDayLedgerDiagrams", pageSource);
        Assert.Contains("선택 지역의 업무 영역별 원장 연결", pageSource);
        Assert.Contains("업무 영역별 원장", pageSource);
        Assert.Contains("원장을 만들기 전에 살펴보는 판단 자료", pageSource);
        Assert.Contains("ledger.Nodes.Take(3)", pageSource);
        Assert.Contains("하단에서 원장 전체 절차 보기", pageSource);
        Assert.Contains("OpenDiagramPanel", pageSource);
        Assert.Contains(".Select(BuildLedgerWorkflowDiagram)", pageSource);
        Assert.Contains("LayerHasVisibleSource(layer, availableLayerCodes)", pageSource);

        Assert.Contains(".world-community-home__layer-option-copy", styleSource);
        Assert.Contains(".world-community-home__layer-ledger-summary", styleSource);
        Assert.Contains(".world-community-home__selected-ledgers", styleSource);
        Assert.Contains(".world-community-home__selected-ledger-list", styleSource);
    }

    [Fact]
    public void 원장연결Marker를선택하면_하단업무흐름Diagram을자동으로연다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");

        Assert.Contains("OpenLedgerDiagramForMapFeature(observationId)", pageSource);
        Assert.Contains("private void OpenLedgerDiagramForMapFeature", pageSource);
        Assert.Contains("marker.LayerCode", pageSource);
        Assert.Contains("LayerHasVisibleSource(layer, availableSourceLayerCodes)", pageSource);
        Assert.Contains("if (hasSelectedLedgerLayer)", pageSource);
        Assert.Contains("_isDiagramPanelExpanded = true", pageSource);
    }

    [Fact]
    public void 세계지도는_한국미국개별도매시장을_좌표정밀도와공식출처경계로표시한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var styleSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor.css");
        var scriptSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "js",
            "community-world-google-map.js");

        Assert.Equal(18, 커뮤니티도매시장MapCatalog.All.Count);
        Assert.Equal(6, 커뮤니티도매시장MapCatalog.ForCountry("KR").Count);
        Assert.Equal(12, 커뮤니티도매시장MapCatalog.ForCountry("US").Count);
        Assert.All(커뮤니티도매시장MapCatalog.ForCountry("KR"), market =>
            Assert.Equal(커뮤니티도매시장위치정밀도Codes.시장대표점, market.LocationPrecisionCode));
        Assert.All(커뮤니티도매시장MapCatalog.ForCountry("US"), market =>
            Assert.Equal(커뮤니티도매시장위치정밀도Codes.도시중심점, market.LocationPrecisionCode));
        Assert.StartsWith("https://", 커뮤니티도매시장MapCatalog.KoreaSourceHref, StringComparison.Ordinal);
        Assert.StartsWith("https://", 커뮤니티도매시장MapCatalog.UnitedStatesSourceHref, StringComparison.Ordinal);

        Assert.Contains("GoogleMapMarker.ForMarket", pageSource);
        Assert.Contains("MapHotspotStyle", pageSource);
        Assert.Contains("aria-label=\"@($\"{country.Name} · {country.DataLabel}\")\"", pageSource);
        Assert.Contains("world-community-home__market-metadata", pageSource);
        Assert.Contains("공식 출처", pageSource);
        Assert.Contains("ConsolidateLedgerDiagrams", pageSource);
        Assert.Contains(".world-community-home__hotspot--market", styleSource);
        Assert.Contains(".world-community-home__layer-shape--market", styleSource);
        Assert.Contains("case \"wholesale-market\"", scriptSource);
        Assert.Contains("case \"traditional-market-hub\"", scriptSource);
        Assert.Contains("? first.Id", pageSource);
        Assert.Contains("SelectMapFeatureFromGoogleMap", scriptSource);
        Assert.Contains("selectedMarkerId", scriptSource);
        Assert.Contains("focusSelection(instance)", scriptSource);
    }

    [Fact]
    public void 지도마커오른쪽클릭은_세신청화면으로_후보문맥만전달한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var scriptSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "js",
            "community-world-google-map.js");
        var routeSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Services",
            "CommunityMapApplicationRoutes.cs");
        var chooserSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityMapApplicationChooserPage.razor");
        var inboundSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "ShipperInboundRequestCreatePage.razor");
        var transportSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "ShipperRequestPage.razor");
        var orderSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "OrdererMartOrderRequestPage.razor");

        Assert.Contains("@oncontextmenu:preventDefault", pageSource);
        Assert.Contains("OpenMapApplicationChooser", pageSource);
        Assert.Contains("지도 신청 업무 선택", chooserSource);
        Assert.Contains("CommunityMapApplicationRoutes.ForMarker", chooserSource);
        Assert.Contains("물류대행 신청", routeSource);
        Assert.Contains("운송대행 신청", routeSource);
        Assert.Contains("개별 주문 신청", routeSource);
        Assert.Contains("addListener(\"contextmenu\"", scriptSource);
        Assert.DoesNotContain("addListener(\"rightclick\"", scriptSource);
        Assert.Contains("OpenMapApplicationsFromGoogleMap", scriptSource);
        Assert.Contains("CommunityMapApplicationRoutes.SourceCode", inboundSource);
        Assert.Contains("CommunityMapApplicationRoutes.SourceCode", transportSource);
        Assert.Contains("CommunityMapApplicationRoutes.SourceCode", orderSource);
        Assert.Contains("ReturnHref=\"@Context.From\"", inboundSource);
        Assert.Contains("ReturnHref=\"@RouteContext.ReturnPath\"", transportSource);
        Assert.Contains("ReturnHref=\"@Context.From\"", orderSource);
        Assert.Contains("비구속 주문 의향", orderSource);
    }

    [Fact]
    public void 세계지도관측Api는_개인위치나업무실행없이_안정Revision을제공한다()
    {
        var contractSource = ReadRepositoryFile(
            "Ssalddel.Contracts",
            "Common",
            "Community",
            "커뮤니티세계지도Dtos.cs");
        var useCaseSource = ReadRepositoryFile(
            "Ssalddel",
            "Services",
            "Community",
            "커뮤니티세계지도조회UseCase.cs");

        Assert.Contains("StableId", contractSource);
        Assert.Contains("Revision", contractSource);
        Assert.Contains("SourceName", contractSource);
        Assert.Contains("EvidenceAsOfUtc", contractSource);
        Assert.Contains("SHA256.HashData", useCaseSource);
        Assert.Contains("결제·주문·계약·배차를 실행하지 않습니다", useCaseSource);
    }

    [Fact]
    public void 뉴스출처마커는_언론사Feed상태와_별도공식Rss후보를구분한다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var clientSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Services",
            "커뮤니티세계지도Client.cs");
        var controllerSource = ReadRepositoryFile(
            "Ssalddel",
            "Controllers",
            "Common",
            "커뮤니티세계지도뉴스후보Controller.cs");

        Assert.Contains("RSS REVIEW · 자동 게시 없음", pageSource);
        Assert.Contains("이 뉴스 출처의 기사 수집 상태", pageSource);
        Assert.Contains("같은 국가의 별도 공식뉴스 RSS 선택", pageSource);
        Assert.Contains("외부 실패를 빈 기사 목록으로 숨기지 않습니다", pageSource);
        Assert.Contains("뉴스후보조회Async", clientSource);
        Assert.Contains("sourceKey", clientSource);
        Assert.Contains("news-candidates", controllerSource);
        Assert.Contains("SsalddelCodeEffect.PersistentRead", controllerSource);
        Assert.Contains("운영 검토 원장에서 승인된 snapshot만 조회", controllerSource);
    }

    [Fact]
    public void 지도마커신청은_독립선택화면과사용자전용원장재조회로연결된다()
    {
        var pageSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityRoleHomePage.razor");
        var chooserSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Pages",
            "CommunityMapApplicationChooserPage.razor");
        var scriptSource = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "wwwroot",
            "js",
            "community-world-google-map.js");

        Assert.Contains("OpenMapApplicationChooser", pageSource);
        Assert.Contains("BuildChooserPath", pageSource);
        Assert.DoesNotContain("world-community-home__application-menu\"", pageSource);
        Assert.Contains("@inject CommunityMapApplicationLedgerClient MapApplicationLedgerClient", pageSource);
        Assert.Contains("RefreshMyMapLedgersAsync", pageSource);
        Assert.Contains("지도 마커의 내 신청 원장", pageSource);
        Assert.Contains("@page \"/community/map-application-chooser\"", chooserSource);
        Assert.Contains("OpenSelectedMapApplicationsFromKeyboard", scriptSource);
    }

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;

    private static string ReadRepositoryFile(params string[] relativePath)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. relativePath]));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }
}
