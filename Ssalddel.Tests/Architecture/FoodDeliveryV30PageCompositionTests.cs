using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Architecture;

public sealed class FoodDeliveryV30PageCompositionTests
{
    [Fact]
    public void 음식점기본탐색은_주문진입과세탭_입력중이탈보호를갖는다()
    {
        Assert.Contains("@page \"/\"", Read("RestaurantDeskApp", "Components/Pages/OrderInbox.razor"));
        Assert.Contains("@page \"/store\"", Read("RestaurantDeskApp", "Components/Pages/Home.razor"));
        Assert.Contains("\"/store\"", Read("RestaurantDeskApp", "Components/Routes.razor"));
        var navigation = Read("RestaurantDeskApp", "Components/Layout/음식점하단탐색.razor");
        foreach (var route in new[] { "/orders", "/menus", "/store" }) Assert.Contains(route, navigation);
        var menus = Read("RestaurantDeskApp", "Components/Pages/Menus.razor");
        Assert.Contains("@if (!editing)", menus);
        Assert.Contains("context.PreventNavigation()", menus);
        Assert.Contains("editing = false;", menus);
    }
    [Fact]
    public void 음식점메뉴화면은_등록수정과서버재조회를연결한다()
    {
        var page = Read("RestaurantDeskApp", "Components/Pages/Menus.razor");
        var routes = Read("RestaurantDeskApp", "Components/Routes.razor");
        var client = Read("RestaurantDeskApp", "Services/Ssalddel음식주문Client.cs");
        Assert.Contains("\"/menus\"", routes);
        Assert.Contains("pendingCreate ??=", page);
        Assert.Contains("MenuClient.등록Async(pendingCreate)", page);
        Assert.Contains("예상Revision = revision", page);
        Assert.Contains("menus = await MenuClient.목록Async()", page);
        Assert.Contains("catch (메뉴저장거절Exception)", page);
        Assert.Contains("사진 파일 업로드는 아직 지원하지 않습니다", page);
        Assert.Contains("api/v1/restaurant/menus", client);
    }

    [Fact]
    public void 음식점수신함은_조회실패를_정상빈목록으로표시하지않는다()
    {
        var inbox = Read("RestaurantDeskApp", "Components/Pages/OrderInbox.razor");
        Assert.Contains("orders.Count == 0 && hasLoaded && !reloadFailed", inbox);
        Assert.Contains("isRefreshing && !hasLoaded", inbox);
        Assert.Contains("if (isRefreshing || workLocked || disposed) return Task.FromResult(false)", inbox);
        Assert.Contains("lastSuccessfulRefresh = DateTimeOffset.UtcNow", inbox);
        Assert.Contains("ReloadManuallyAsync", inbox);
        Assert.Contains("if (reloadFailed) message = null", inbox);
    }

    [Fact]
    public void 음식점실패복구는_연결실패와_인증권한실패를분리하고_명령후재조회를요구한다()
    {
        var route = Read("RestaurantDeskApp", "Components/RestaurantRouteView.razor");
        var inbox = Read("RestaurantDeskApp", "Components/Pages/OrderInbox.razor");
        var detail = Read("RestaurantDeskApp", "Components/Pages/OrderDetail.razor");
        var realtime = Read("RestaurantDeskApp", "Services/음식점주문SignalRClientService.cs");
        Assert.Contains("if (result.RequiresLogin)", route);
        Assert.Contains("if (auth.RequiresLogin)", realtime);
        Assert.Contains("Publish상태Async(음식점실시간연결상태.인증필요", realtime);
        Assert.Contains("Publish상태Async(음식점실시간연결상태.연결끊김", realtime);
        Assert.Contains("OnClick=\"CheckAccessAsync\"", route);
        Assert.Contains("catch (SsalddelApiException ex) when (ex.StatusCode == 401)", inbox);
        Assert.Contains("catch (SsalddelApiException ex) when (ex.StatusCode == 403)", inbox);
        Assert.Contains("WorkDisabled => isBusy || isLoading || readFailed || workLocked", detail);
        Assert.Contains("HandleRequestFailure(ex, \"주문 확인 결과", detail);
        Assert.Contains("HandleRequestFailure(ex, \"주문 진행 결과", detail);
        Assert.Contains("SsalddelApiException { StatusCode: 401 }", detail);
        Assert.Contains("SsalddelApiException { StatusCode: 403 }", detail);
        Assert.Contains("workLocked = false;", detail);
        Assert.Contains("readFailed = false;", detail);
    }

    [Fact]
    public void 음식점화면이탈과주문변경은_기존요청을취소하고_늦은상태반영을차단한다()
    {
        var inbox = Read("RestaurantDeskApp", "Components/Pages/OrderInbox.razor");
        var detail = Read("RestaurantDeskApp", "Components/Pages/OrderDetail.razor");
        Assert.Contains("주문목록조회Async(복구출처, cancellationToken)", inbox);
        Assert.Contains("cancellationToken.ThrowIfCancellationRequested();", inbox);
        Assert.Contains("await currentReloadTask;", inbox);
        Assert.Contains("catch (Exception) when (cancellationToken.IsCancellationRequested)", inbox);
        Assert.Contains("@implements IDisposable", detail);
        Assert.Contains("selectionCancellation.Cancel();", detail);
        Assert.Contains("generation == selectionGeneration", detail);
        Assert.Contains("!IsCurrentSelection(requestedOrderNo, generation)", detail);
        Assert.Contains("주문조회Async(requestedOrderNo, cancellationToken: cancellationToken)", detail);
        Assert.Contains("await action(cancellationToken)", detail);
        Assert.Contains("selectionGeneration++;", detail);
    }

    [Fact]
    public void 조리시간화면은_빠른선택과_기기저장경계를표시한다()
    {
        var page = Read("RestaurantDeskApp", "Components/Pages/PreparationTimeSettings.razor");
        Assert.Contains("new[] { 5, 10, 15, 20 }", page);
        Assert.Contains("판매 메뉴 등록이나 다른 기기와의 동기화는 아닙니다", page);
        Assert.Contains("상품별 시간 기준 추가", page);
        Assert.Contains("if (isSaving) return", page);
        Assert.Contains("restaurant-order-card-row", page);
    }

    [Fact]
    public void 음식점운영홈은_주문수락구현을포함하지않는다()
    {
        var source = Read("RestaurantDeskApp", "Components/Pages/Home.razor");

        Assert.Contains("@page \"/store\"", source);
        Assert.Contains("/orders", source);
        Assert.DoesNotContain("주문수락후전표준비Async", source);
        Assert.DoesNotContain("SimulateOrderAlertAsync", source);
    }

    [Fact]
    public void 음식점주문은_수신함과정확한주문번호상세로분리한다()
    {
        var inbox = Read("RestaurantDeskApp", "Components/Pages/OrderInbox.razor");
        var detail = Read("RestaurantDeskApp", "Components/Pages/OrderDetail.razor");

        Assert.Contains("@page \"/orders\"", inbox);
        Assert.Contains("주문 보기", inbox);
        Assert.DoesNotContain("주문수락후전표준비Async", inbox);
        Assert.Contains("@page \"/orders/{OrderNo}\"", detail);
        Assert.Contains("주문수락후전표준비Async", detail);
        Assert.Contains("조리 예상시간", detail);
        Assert.Contains("preparationMinutes", detail);
        Assert.Contains("주문거절Async", detail);
        Assert.Contains("조리시간변경Async", detail);
        Assert.Contains("픽업준비완료Async", detail);
    }

    [Fact]
    public void 음식점주문수락은_전표출력실패와_서버상태전이실패를_구분한다()
    {
        var detail = Read("RestaurantDeskApp", "Components/Pages/OrderDetail.razor");
        var staticAsset = Read(
            "Ssalddel.Ui.Common",
            "wwwroot/Areas/App/js/ssalddel-document-output.js");

        Assert.Contains("catch (JSException)", detail);
        Assert.Contains("주문을 확인해 배차를 요청했습니다", detail);
        Assert.Contains("기사 배정 후 조리를 시작해 주세요", detail);
        Assert.DoesNotContain("주문은 수락되어 조리를 시작했습니다", detail);
        Assert.Contains("window.ssalddelDocumentOutput", staticAsset);
        Assert.Contains("printHtml", staticAsset);
    }

    [Fact]
    public void 음식점주문은_상품별기본시간을추천하고_주문별선택시간으로수락한다()
    {
        var settings = Read("RestaurantDeskApp", "appsettings.json");
        var settingsPage = Read("RestaurantDeskApp", "Components/Pages/PreparationTimeSettings.razor");
        var startup = Read("RestaurantDeskApp", "MauiProgram.cs");
        var desk = Read("RestaurantDeskApp", "Services/음식점주문DeskService.cs");
        var notification = Read("Ssalddel.Contracts", "Food/음식점주문알림Dtos.cs");

        Assert.Contains("\"상품별기본조리분\"", settings);
        Assert.Contains("AddJsonFile", startup);
        Assert.Contains("@page \"/settings/preparation-times\"", settingsPage);
        Assert.Contains("음식점 기본시간", settingsPage);
        Assert.Contains("상품별 기본시간", settingsPage);
        Assert.Contains("PreparationSettingsService.저장Async", settingsPage);
        Assert.Contains("음식점조리시간정책.주문추천분", desk);
        Assert.Contains("조리예상분 = 선택조리예상분", desk);
        Assert.Contains("상품목록", notification);
    }

    [Fact]
    public void 음식점데스크의_내부시험기본값은_배차인계좌표를포함한다()
    {
        var options = Read("RestaurantDeskApp", "Options/RestaurantDeskOptions.cs");
        var settings = Read("RestaurantDeskApp", "appsettings.json");

        Assert.Contains("검증 표본 음식점", options);
        Assert.Contains("RestaurantLatitude", options);
        Assert.Contains("37.588m", options);
        Assert.Contains("127.085m", options);
        Assert.Contains("검증 표본 음식점", settings);
    }

    [Fact]
    public void 음식점Api실패는_샘플주문으로대체하지않는다()
    {
        var client = Read("RestaurantDeskApp", "Services/Ssalddel음식주문Client.cs");
        var desk = Read("RestaurantDeskApp", "Services/음식점주문DeskService.cs");

        Assert.DoesNotContain("RestaurantDeskSampleService", client);
        Assert.DoesNotContain("sampleService", desk);
        Assert.Contains("/restaurant-acceptance", client);
        Assert.Contains("/restaurant-progress", client);
        Assert.Contains("SendAsync", client);
    }

    [Fact]
    public void 음식점데스크는_로그인토큰과서버원장복구를사용한다()
    {
        var startup = Read("RestaurantDeskApp", "MauiProgram.cs");
        var auth = Read("RestaurantDeskApp", "Services/RestaurantAuthService.cs");
        var client = Read("RestaurantDeskApp", "Services/Ssalddel음식주문Client.cs");
        var realtime = Read("RestaurantDeskApp", "Services/음식점주문SignalRClientService.cs");
        var desk = Read("RestaurantDeskApp", "Services/음식점주문DeskService.cs");

        Assert.Contains("RestaurantMauiSecureTokenStore", startup);
        Assert.Contains("ClientAuthSession", startup);
        Assert.Contains("AddHttpClient<RestaurantAuthService>", startup);
        Assert.DoesNotContain("AddScoped<RestaurantAuthService>", startup);
        Assert.Contains("api/v1/auth/refresh", auth);
        Assert.Contains("AuthenticationHeaderValue", client);
        Assert.Contains("/restaurant/inbox", client);
        Assert.Contains("forceRefresh: true", client);
        Assert.Contains("response.StatusCode != HttpStatusCode.Unauthorized", client);
        Assert.Contains("AccessTokenProvider", realtime);
        Assert.Contains("JoinRestaurantOrders\", cancellationToken", realtime);
        Assert.Contains("_foodOrderClient.주문목록조회Async", desk);
        Assert.Contains("_foodOrderClient.주문상세조회Async", desk);
        Assert.DoesNotContain("payload.음식점Id != _options.RestaurantId", desk);
        Assert.Contains("_serverInboxGate.WaitAsync", desk);
    }

    [Fact]
    public void 음식점진행변경은_같은멱등요청을한번재시도하고_판본충돌시정본을재조회한다()
    {
        var client = Read("RestaurantDeskApp", "Services/Ssalddel음식주문Client.cs");
        var desk = Read("RestaurantDeskApp", "Services/음식점주문DeskService.cs");

        Assert.Contains("SsalddelApiProblemParser.Parse", client);
        Assert.Contains("var request = new 음식점주문진행변경요청", desk);
        Assert.Contains("업무멱등재시도실행기.한번Async", desk);
        Assert.Contains("RetryIdempotent", Read("Ssalddel.Contracts", "Common/Workflow/업무실패복구Dtos.cs"));
        Assert.Contains("TryRefreshCanonicalOrderAsync", desk);
        Assert.Contains("_foodOrderClient.주문상세조회Async(orderNo", desk);
        Assert.Contains("UpsertServerOrder(canonical", desk);
    }

    [Fact]
    public void 음식점데스크는_모바일크기로시작하고_첫화면을메뉴로가리지않는다()
    {
        var app = Read("RestaurantDeskApp", "App.xaml.cs");
        var mainPage = Read("RestaurantDeskApp", "MainPage.xaml");
        var layout = Read("RestaurantDeskApp", "Components/Layout/MainLayout.razor");
        var routes = Read("RestaurantDeskApp", "Components/Routes.razor");
        var routeView = Read("RestaurantDeskApp", "Components/RestaurantRouteView.razor");
        var styles = Read("RestaurantDeskApp", "wwwroot/app.css");

        Assert.Contains("Title = \"살뜰 식당\"", app);
        Assert.Contains("window.Width = 430", app);
        Assert.Contains("window.Height = 860", app);
        Assert.Contains("StartPath=\"/\"", mainPage);
        Assert.Contains("private bool _drawerOpen;", layout);
        Assert.DoesNotContain("private bool _drawerOpen = true;", layout);
        Assert.Contains("<RestaurantRouteView", routes);
        Assert.Contains("RouteData.PageType == typeof(Pages.Login)", routeView);
        Assert.Contains("AuthService.EnsureAccessTokenAsync", routeView);
        Assert.Contains("NavigationManager.NavigateTo(\"/login\", replace: true)", routeView);
        Assert.Contains(".restaurant-navmenu", styles);
        Assert.Contains("width: 100%;", styles);
        Assert.DoesNotContain("width: 280px;", styles);
    }

    [Theory]
    [InlineData("OrdererApp")]
    [InlineData("RestaurantDeskApp")]
    [InlineData("SsalddelAdminApp")]
    public void Android_MAUI화면은_상태표시줄안전영역을_확보한다(string project)
    {
        var mainPage = Read(project, "MainPage.xaml");

        Assert.Contains("SafeAreaEdges=\"Container\"", mainPage);
    }

    [Fact]
    public void 내부시험배너는_각앱의본문흐름안에서렌더링한다()
    {
        var orderer = Read("OrdererApp", "Components/Layout/MainLayout.razor");
        var restaurant = Read("RestaurantDeskApp", "Components/Layout/MainLayout.razor");
        var admin = Read("SsalddelAdminApp", "Components/Layout/MainLayout.razor");

        AssertAppearsBetween(
            orderer,
            "</header>",
            "<MobileFieldTestBanner AppVersion=\"0.1.0\" />",
            "<section class=\"orderer-mobile-shell__content\">");
        AssertAppearsBetween(
            restaurant,
            "<MudMainContent>",
            "<MobileFieldTestBanner AppVersion=\"0.1.0\" />",
            "</MudMainContent>");
        AssertAppearsBetween(
            admin,
            "<MudMainContent>",
            "<MobileFieldTestBanner AppVersion=\"0.1.0\" />",
            "</MudMainContent>");
    }

    [Fact]
    public void 주문자모바일첫동선은_공동주문진입점을노출하지않는다()
    {
        var sources = new[]
        {
            Read("OrdererApp", "Components/Layout/MainLayout.razor"),
            Read("OrdererApp", "Components/Layout/NavMenu.razor"),
            Read("OrdererApp", "Components/Orderer/OrdererMobileHomeScreen.razor"),
            Read("OrdererApp", "Components/Pages/Home.razor")
        };

        Assert.All(sources, source =>
        {
            Assert.DoesNotContain("OrdererRoutes.GroupPurchaseGroups", source);
            Assert.DoesNotContain("OrdererRoutes.GroupPurchaseTogetherOrders", source);
            Assert.DoesNotContain("공동주문", source);
            Assert.DoesNotContain("같이 주문", source);
        });
    }

    [Fact]
    public void 기사상태변경은_음식점실시간알림과30초서버재조회로수렴한다()
    {
        var hub = Read("Ssalddel", "Hubs/RestaurantOrderHub.cs");
        var serverNotification = Read("Ssalddel", "Services/Food/음식점주문SignalR알림Service.cs");
        var driverWork = Read(
            "Ssalddel",
            "Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs");
        var realtime = Read(
            "RestaurantDeskApp",
            "Services/음식점주문SignalRClientService.cs");
        var inbox = Read("RestaurantDeskApp", "Components/Pages/OrderInbox.razor");

        Assert.Contains("ReceiveRestaurantOrderStatusChanged", hub);
        Assert.Contains("주문상태변경알림발송Async", serverNotification);
        Assert.Contains("NotifyRestaurantAsync", driverWork);
        Assert.Contains("음식점주문상태변경알림", realtime);
        Assert.Contains("재연결후재조회요청", realtime);
        Assert.Contains("음식점실시간연결상태.재연결중", realtime);
        Assert.Contains("TimeSpan.FromSeconds(30)", inbox);
        Assert.Contains("음식점주문복구출처.재연결재조회", inbox);
        Assert.Contains("다음 30초 조회에서 다시 시도", inbox);
    }

    [Fact]
    public void 음식배달3_0미리보기는_Operational에서3_5를켜지않는다()
    {
        var compose = Read("deploy", "azure-vm/compose.food-delivery-v30.override.yaml");

        Assert.Contains("SsalddelExecution__Mode: Operational", compose);
        Assert.Contains("VersionFeatureFlags__FoodDeliveryWorkflow: \"true\"", compose);
        Assert.Contains("VersionFeatureFlags__SsalddelMartWorkflow: \"false\"", compose);
    }

    [Fact]
    public void 음식배달기사반복항목Command는_기사업무ViewModel을명시한다()
    {
        var page = Read("FDriverApp", "Pages/MainPage.xaml");

        Assert.DoesNotContain("BindingContext.AcceptBundleCommand", page);
        Assert.DoesNotContain("BindingContext.SelectTicketCommand", page);
        Assert.DoesNotContain("BindingContext.AcceptTicketCommand", page);
        Assert.Contains("x:DataType='pageModels:MainPageModel'", page);
        Assert.Contains("AncestorType={x:Type pageModels:MainPageModel}", page);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.Orderer, "/food", "orderer-food-home", PageInteractionBoundary.ReadOnly)]
    [InlineData(SsalddelPageAppCodes.RestaurantDesk, "/orders", "restaurant-order-inbox", PageInteractionBoundary.ReadOnly)]
    [InlineData(SsalddelPageAppCodes.RestaurantDesk, "/orders/FOOD-20260723-01", "restaurant-order-detail", PageInteractionBoundary.PlatformPersistence)]
    [InlineData(SsalddelPageAppCodes.FoodDeliveryDriver, "/food-delivery/open/dispatch", "food-driver-workspace-launch", PageInteractionBoundary.PlatformPersistence)]
    public void 음식배달3_0페이지는_책임별Capability를가진다(
        string appCode,
        string route,
        string expectedPageKey,
        PageInteractionBoundary expectedBoundary)
    {
        var found = SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability);

        Assert.True(found);
        Assert.Equal(expectedPageKey, capability.PageKey);
        Assert.Equal("3.0", capability.IntroducedVersion);
        Assert.Equal(expectedBoundary, capability.Boundary);
        Assert.Contains("FoodDeliveryWorkflow", capability.FeatureKeys);
        Assert.Contains("FoodDelivery", capability.WorkflowCodes);
    }

    private static string Read(string project, string relativePath)
    {
        var path = Path.Combine(FindRepositoryRoot(), project, relativePath);
        return File.ReadAllText(path)
            + (File.Exists(path + ".cs") ? File.ReadAllText(path + ".cs") : string.Empty);
    }

    private static void AssertAppearsBetween(
        string source,
        string openingMarker,
        string expected,
        string closingMarker)
    {
        var openingIndex = source.IndexOf(openingMarker, StringComparison.Ordinal);
        var expectedIndex = source.IndexOf(expected, StringComparison.Ordinal);
        var closingIndex = source.IndexOf(closingMarker, StringComparison.Ordinal);

        Assert.True(openingIndex >= 0, $"시작 표식을 찾지 못했습니다: {openingMarker}");
        Assert.True(expectedIndex > openingIndex, $"본문 앞에서 항목을 찾지 못했습니다: {expected}");
        Assert.True(closingIndex > expectedIndex, $"본문 끝보다 앞에서 항목을 찾지 못했습니다: {expected}");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Ssalddel.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("저장소 루트를 찾을 수 없습니다.");
    }
}
