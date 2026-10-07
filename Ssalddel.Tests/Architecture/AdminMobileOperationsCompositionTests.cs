namespace Ssalddel.Tests.Architecture;

public sealed class AdminMobileOperationsCompositionTests
{
    [Fact]
    public void 모바일관리홈은_음식과화물의독립진입만제공한다()
    {
        var home = Read("SsalddelAdminApp", "Components/Pages/Home.razor");
        var cargo = Read("SsalddelAdminApp", "Components/Pages/CargoOverview.razor");
        var formerHome = Read("SsalddelAdminApp", "Components/Pages/CommunityInformationReview.razor");
        Assert.Contains("@page \"/\"", home);
        Assert.Contains("@page \"/overview\"", home);
        Assert.Contains("Href=\"/food-operations\"", home);
        Assert.Contains("Href=\"/overview/cargo\"", home);
        Assert.DoesNotContain("AdminDashboardService", home);
        Assert.DoesNotContain("AdminOperationsService", home);
        Assert.DoesNotContain("GetAsync", home);
        Assert.Contains("@page \"/overview/cargo\"", cargo);
        Assert.DoesNotContain("@page \"/overview\"", cargo);
        Assert.Contains("@inject AdminDashboardService DashboardService", cargo);
        Assert.Contains("관리자확인필요수", cargo);
        Assert.Contains("운송예외수", cargo);
        Assert.Contains("배차대기수", cargo);
        Assert.Contains("RefreshInterval = TimeSpan.FromSeconds(30)", cargo);
        Assert.Contains("Href=\"/operations\"", cargo);
        Assert.Contains("Href=\"/operations/follow-up-recovery\"", cargo);
        Assert.Contains("Href=\"/operations/finance\"", cargo);
        Assert.DoesNotContain("@page \"/\"", formerHome);
    }

    [Fact]
    public void 음식모바일목록추적검토는_화물조회없이_출시라우트와연결된다()
    {
        var service = Read("SsalddelAdminApp", "Services/AdminFoodOperationsService.cs");
        var cargo = Read("SsalddelAdminApp", "Services/AdminOperationsService.cs");
        var routes = Read("SsalddelAdminApp", "Components/Routes.razor");
        var detail = Read("SsalddelAdminApp", "Components/Pages/FoodOrderDetail.razor");
        var review = Read("SsalddelAdminApp", "Components/Pages/FoodInterruptionReview.razor");
        Assert.Contains("api/v1/admin/food-orders/operations?", service);
        Assert.Contains("/operations-trace", service);
        Assert.Contains("/interruption-review", service);
        Assert.DoesNotContain("api/v1/admin/transports", service);
        Assert.DoesNotContain("api/v1/admin/drivers/operating", service);
        Assert.DoesNotContain("food-delivery-pricing-policy", cargo);
        Assert.Contains("\"/food-operations\"", routes);
        Assert.Contains("\"/food-operations/orders/{OrderNo}\"", routes);
        Assert.Contains("\"/food-operations/reviews/{OrderNo}\"", routes);
        Assert.Contains("FoodDeliverySettlementDisplay.From", detail);
        Assert.DoesNotContain("simulate-driver-payout", detail);
        Assert.Contains("FoodDeliveryInterruptionReviewState", review);
        Assert.Contains("Session.Changed", review);
        Assert.Contains("state.SubmitAsync()", review);
    }

    [Fact]
    public void 관리자로그인은_소셜계정선택없이_간단한계정로그인만제공한다()
    {
        var login = Read("SsalddelAdminApp", "Components/Pages/Login.razor");

        Assert.Contains("소셜로그인표시=\"false\"", login);
        Assert.Contains("OnPasswordLogin=\"HandleLoginAsync\"", login);
        Assert.DoesNotContain("OnSocialLogin", login);
        Assert.DoesNotContain("카카오", login);
        Assert.DoesNotContain("구글", login);
        Assert.DoesNotContain("네이버", login);
    }

    [Fact]
    public void 모바일운영화면은_실제운송원장과_운행기사목록을_함께조회한다()
    {
        var page = Read("SsalddelAdminApp", "Components/Pages/Operations.razor");
        var service = Read("SsalddelAdminApp", "Services/AdminOperationsService.cs");
        var layout = Read("SsalddelAdminApp", "Components/Layout/MainLayout.razor");
        var startup = Read("SsalddelAdminApp", "MauiProgram.cs");

        Assert.Contains("@page \"/operations\"", page);
        Assert.Contains("@inject AdminAuthService AuthService", page);
        Assert.Contains("@inject AdminOperationsService OperationsService", page);
        Assert.Contains("RefreshInterval = TimeSpan.FromSeconds(30)", page);
        Assert.Contains("@inject TransportRequestLedgerRealtimeClient RealtimeClient", page);
        Assert.Contains("LedgerObserver.RefreshRequested += OnLedgerRefreshRequested", page);
        Assert.Contains("RealtimeClient.StartAsync", page);
        Assert.Contains("RetryAuthenticationAsync", page);
        Assert.Contains("transport.관리자확인필요 || transport.예외신고됨", page);
        Assert.Contains("snapshot.OperatingDrivers", page);
        Assert.Contains("\"api/v1/admin/transports\"", service);
        Assert.Contains("\"api/v1/admin/drivers/operating\"", service);
        Assert.Contains("Task.WhenAll", service);
        Assert.Contains("Href=\"/operations\"", layout);
        Assert.Contains("AddScoped<AdminOperationsService>()", startup);
        Assert.Contains("AddScoped<AdminDashboardService>()", startup);
        Assert.Contains("AddSingleton<ITransportRequestLedgerObserver, TransportRequestLedgerObserver>()", startup);
        Assert.Contains("new TransportRequestLedgerRealtimeClient", startup);
    }

    [Fact]
    public void 모바일관리인증은_갱신토큰을보존하고_401에서_한번만재시도한다()
    {
        var session = Read("SsalddelAdminApp", "Services/AdminAuthSession.cs");
        var auth = Read("SsalddelAdminApp", "Services/AdminAuthService.cs");
        var apiClient = Read(
            "SsalddelAdminApp",
            "Services/AdminAuthenticatedApiClient.cs");
        var startup = Read("SsalddelAdminApp", "MauiProgram.cs");

        Assert.Contains("RefreshTokenExpiresAtUtc", session);
        Assert.Contains("ClientAuthSessionRestoreState.RefreshRequired", session);
        Assert.Contains("IClientSessionGuard", session);
        Assert.Contains("\"api/v1/auth/refresh\"", auth);
        Assert.Contains("EnsureAccessTokenAsync", auth);
        Assert.Contains("SemaphoreSlim refreshGate", auth);
        Assert.Contains("response.StatusCode == HttpStatusCode.Unauthorized", apiClient);
        Assert.Contains("forceRefresh: true", apiClient);
        Assert.Contains("SendOnceAsync(method, path", apiClient);
        Assert.Contains("AddSingleton<IClientSessionGuard, ClientSessionGuard>()", startup);
        Assert.Contains("AddScoped<AdminAuthenticatedApiClient>()", startup);
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
