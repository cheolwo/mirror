using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Architecture;

public sealed class FoodDeliveryV30PageCompositionTests
{
    [Fact]
    public void 음식점운영홈은_주문수락구현을포함하지않는다()
    {
        var source = Read("RestaurantDeskApp", "Components/Pages/Home.razor");

        Assert.Contains("@page \"/\"", source);
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
        Assert.Contains("상세 보기", inbox);
        Assert.DoesNotContain("주문수락후전표준비Async", inbox);
        Assert.Contains("@page \"/orders/{OrderNo}\"", detail);
        Assert.Contains("주문수락후전표준비Async", detail);
        Assert.Contains("이 주문의 조리 대기시간", detail);
        Assert.Contains("preparationMinutes", detail);
        Assert.Contains("주문거절Async", detail);
        Assert.Contains("조리시간변경Async", detail);
        Assert.Contains("픽업준비완료Async", detail);
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
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), project, relativePath));

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
