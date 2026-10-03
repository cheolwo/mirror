namespace Ssalddel.Tests.Architecture;

public sealed class FDriverNotificationAuthRecipientCompositionTests
{
    [Fact]
    public void 음식배달기사앱은_새추천을_지도배너로알리고_바로가기초점을_실제영역으로이동한다()
    {
        var model = Read("FDriverApp", "PageModels/MainPageModel.cs");
        var notifications = Read("FDriverApp", "PageModels/MainPageModel.FoodNotifications.cs");
        var state = Read("FDriverApp", "PageModels/FDriverFoodNotificationState.cs");
        var page = Read("FDriverApp", "Pages/MainPage.xaml");
        var codeBehind = Read("FDriverApp", "Pages/MainPage.xaml.cs");

        Assert.Contains("HasNewRecommendations", model);
        Assert.Contains("NewRecommendationNotice", notifications);
        Assert.Contains("OpenNewRecommendations", model);
        Assert.Contains("ApplyFoodRecommendations(workspace)", model);
        Assert.Contains("_foodNotificationState.BindAccount", notifications);
        Assert.Contains("_service.ReadReceipt()", state);
        Assert.Contains("RestoreFoodNotificationTargetAsync", model);
        Assert.Contains("TakeFoodNotificationFocus", codeBehind);
        Assert.Contains("WorkspaceRefreshInterval = TimeSpan.FromSeconds(10)", model);
        Assert.Contains("await ReloadAsync(updateLocation: IsOnDuty)", model);
        Assert.Contains("다음 자동 갱신 10초 이내", model);
        Assert.Contains("OpenNewRecommendationsCommand", page);
        Assert.Contains("눌러서 음식점·전달지와 예상 경로를 확인하세요.", page);
        Assert.Contains("x:Name=\"WorkspaceScroll\"", page);
        Assert.Contains("x:Name=\"ActiveDeliverySection\"", page);
        Assert.Contains("x:Name=\"RecommendationSection\"", page);
        Assert.Contains("ScrollToEntryFocusAsync", codeBehind);
        Assert.Contains("\"dispatch\" or \"bundle\" => RecommendationSection", codeBehind);
        Assert.Contains("\"delivery\" => ActiveDeliverySection", codeBehind);
    }

    [Fact]
    public void 음식배달기사앱은_FCM을_갱신힌트로두고_10초서버조회를_복구경로로유지한다()
    {
        var push = Read("Ssalddel", "Services/Notifications/FcmDriverRecommendationPushService.cs");
        var controller = Read("Ssalddel", "Controllers/Driver/07_Notification/기사알림Controller.cs");
        var project = Read("FDriverApp", "FDriverApp.csproj");
        var registrations = Read("FDriverApp", "MauiProgram.cs");
        var model = Read("FDriverApp", "PageModels/MainPageModel.cs");

        Assert.Contains("_fcmPushService.SendToTokenAsync", push);
        Assert.Contains("[Route(\"api/v1/driver/notifications\")]", controller);
        Assert.Contains("[HttpPut(\"push-token\")]", controller);
        Assert.DoesNotContain("Microsoft.AspNetCore.SignalR.Client", project);
        Assert.DoesNotContain("IFDriverDispatchRealtimeService", registrations);
        Assert.DoesNotContain("IFDriverDispatchRealtimeService", model);
        Assert.Contains("WorkspaceRefreshInterval = TimeSpan.FromSeconds(10)", model);
        Assert.Contains("await ReloadAsync(updateLocation: IsOnDuty);", model);
        Assert.False(File.Exists(Path.Combine(
            FindRepositoryRoot(),
            "FDriverApp",
            "Services",
            "FDriverDispatchRealtimeService.cs")));
    }

    [Fact]
    public void 음식배달기사앱은_저장된갱신토큰과_401응답으로_액세스토큰을자동갱신한다()
    {
        var session = Read("FDriverApp", "Services/FDriverAuthSession.cs");
        var authApi = Read("FDriverApp", "Services/FDriverAuthApiService.cs");
        var foodApi = Read("FDriverApp", "Services/FoodDeliveryDriverApiService.cs");

        Assert.Contains("ClientAuthSessionRestoreState.RefreshRequired", session);
        Assert.Contains("RefreshTokenExpiresAtUtc", session);
        Assert.Contains("api/v1/auth/refresh", authApi);
        Assert.Contains("EnsureAccessTokenAsync", authApi);
        Assert.Contains("SemaphoreSlim _refreshGate", authApi);
        Assert.Contains("response.StatusCode == HttpStatusCode.Unauthorized", foodApi);
        Assert.Contains("forceRefresh: true", foodApi);
        Assert.Contains("SendOnceAsync(method, path, body", foodApi);
    }

    [Fact]
    public void 음식배달기사운행은_화물Api가아닌_음식배달Feature경계를사용한다()
    {
        var foodApi = Read("FDriverApp", "Services/FoodDeliveryDriverApiService.cs");
        var foodController = Read("Ssalddel", "Controllers/Driver/Food/음식배달기사업무Controller.cs");
        var cargoController = Read("Ssalddel", "Controllers/Driver/01_Work/기사운행Controller.cs");
        var workspaceUseCase = Read("Ssalddel", "Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs");
        var startCommand = Read("Ssalddel", "Application/Driver/Work/Commands/운행시작Command.cs");
        var startHandler = Read("Ssalddel", "Application/Driver/Work/Handlers/운행시작CommandHandler.cs");

        Assert.DoesNotContain("\"api/v1/driver/work/", foodApi);
        Assert.Contains("api/v1/driver/food-deliveries/work/status", foodApi);
        Assert.Contains("api/v1/driver/food-deliveries/work/start", foodApi);
        Assert.Contains("api/v1/driver/food-deliveries/work/stop", foodApi);
        Assert.Contains("api/v1/driver/food-deliveries/work/location", foodApi);
        Assert.Contains("시작모드 = \"바로시작\"", foodApi);
        Assert.DoesNotContain("시작모드 = \"immediate\"", foodApi);
        Assert.Contains("[HttpPost(\"work/start\")]", foodController);
        Assert.Contains("[HttpPost(\"work/location\")]", foodController);
        Assert.Contains("VersionFeatureFlagKeys.FoodDeliveryWorkflow", foodController);
        Assert.Contains("운송실행유형코드.음식배달", foodController);
        Assert.Contains("커뮤니티운행공개: false", foodController);
        Assert.Contains("VersionFeatureFlagKeys.DomesticTransportWorkflow", cargoController);
        Assert.Contains("VersionFeatureFlagKeys.FoodDeliveryWorkflow", workspaceUseCase);
        Assert.Contains("운송실행유형코드.화물운송", startCommand);
        Assert.Contains("request.운송실행유형", startHandler);
        Assert.Contains("_dispatchRecommendationService.SendToDriverAsync", startHandler);
        Assert.DoesNotContain(
            "\"국내 운송 기능이 비활성화되어 자동 기사 추천이 실행되지 않습니다.\"",
            workspaceUseCase);
    }

    [Fact]
    public void 실제수령자정보는_배차확정뒤_진행업무에만연결된다()
    {
        var workContract = Read("Ssalddel.Contracts", "Common/Drivers/DriverWorkDtos.cs");
        var workspaceContract = Read("Ssalddel.Contracts", "Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs");
        var workService = Read("Ssalddel", "Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs");
        var workspaceUseCase = Read("Ssalddel", "Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs");
        var model = Read("FDriverApp", "PageModels/MainPageModel.cs");
        var page = Read("FDriverApp", "Pages/MainPage.xaml");

        Assert.Contains("DriverWorkRecipientDto", workContract);
        Assert.Contains("FoodDeliveryDriverRecipientDto Recipient", workspaceContract);
        Assert.Contains("isRecommended", workService);
        Assert.Contains("? null", workService);
        Assert.Contains("order.수령인명", workService);
        Assert.Contains("order.수령인연락처", workService);
        Assert.Contains("order.수령요청사항", workService);
        Assert.Contains("Recipient = ToRecipient(offer.Recipient)", workspaceUseCase);
        Assert.Contains("public bool HasActiveRecipient => ActiveDelivery?.HasRecipient == true;", model);
        Assert.Contains("IsVisible=\"{Binding HasActiveRecipient}\"", page);
        Assert.Contains("ActiveDelivery.RecipientContactText", page);
    }

    [Fact]
    public void 음식배달기사앱은_운영Api만사용하고_활성업무상한은_서버값을따른다()
    {
        var registrations = Read("FDriverApp", "MauiProgram.cs");
        var model = Read("FDriverApp", "PageModels/MainPageModel.cs");
        var contract = Read("Ssalddel.Contracts", "Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs");
        var workspace = Read("Ssalddel", "Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs");

        Assert.Contains("IFoodDeliveryDriverApiService, FoodDeliveryDriverApiService", registrations);
        Assert.DoesNotContain("BusinessWorkflowRuntime", registrations);
        Assert.DoesNotContain("LocalSimulationRuntime", registrations);
        Assert.Contains("MaxActiveDeliveries", contract);
        Assert.Contains("MaxActiveDeliveries = workspace.MaxActiveDeliveries", model);
        Assert.Contains("ActiveDeliveryItems.Count < MaxActiveDeliveries", model);
        Assert.DoesNotContain("ActiveDeliveryItems.Count < 3", model);
        Assert.Contains("음식배달기사활성업무Policy.MaxActiveDeliveries", workspace);
    }

    private static string Read(string project, string relativePath)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), project, relativePath));

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
