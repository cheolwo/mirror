namespace Ssalddel.Tests.Architecture;

public sealed class DriverWorkflowReliabilityCompositionTests
{
    [Fact]
    public void 운행시작화면은_존재하는Route로_다음행동을안내한다()
    {
        var source = Read("DriverApp/Components/Pages/Driver/01_Work/운행시작Page.razor");

        Assert.DoesNotContain("/driver/work/status", source);
        Assert.Contains("DriverRoutes.HomeSummary", source);
        Assert.Contains("DriverRoutes.Recommendations", source);
        Assert.Contains("DriverRoutes.CommunityInquiries", source);
    }

    [Fact]
    public void 새배차Push는_서버자료를_강제로다시조회한다()
    {
        var contract = Read("DriverApp/Services/IDriverSampleDataService.cs");
        var serverBacked = Read("DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs");
        var fcm = Read("DriverApp/Platforms/Android/SsalddelFirebaseMessagingService.cs");
        var recovery = Read("DriverApp/Services/DriverRecommendationPushRecovery.cs");

        Assert.Contains("bool force = false", contract);
        Assert.Contains("if (_loaded && !force)", serverBacked);
        Assert.Contains("GetRequiredService<DriverRecommendationPushRecovery>()", fcm);
        Assert.Contains("recovery.CaptureAsync(data, openRequested: false, cancellation.Token)", fcm);
        Assert.Contains("recovery.RecoverAsync(cancellation.Token)", fcm);
        Assert.Contains("samples.RefreshAsync(cancellationToken, force: true)", recovery);
        Assert.Contains("samples.추천의뢰조회(target.Hint.RequestId)", recovery);
        Assert.True(recovery.IndexOf("samples.RefreshAsync(cancellationToken, force: true)", StringComparison.Ordinal)
            < recovery.IndexOf("samples.추천의뢰조회(target.Hint.RequestId)", StringComparison.Ordinal));
        Assert.DoesNotContain("?? samples.추천의뢰목록.FirstOrDefault()", recovery);
    }

    [Fact]
    public void 기사자료Cache는_인증경계와_동시갱신을보호한다()
    {
        var source = Read("DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs");

        Assert.Contains("SemaphoreSlim _refreshGate", source);
        Assert.Contains("ApplyDisconnectedState();", source);
        Assert.Contains("_loadedAccessToken = null;", source);
        Assert.True(
            source.IndexOf("string.IsNullOrWhiteSpace(accessToken)", StringComparison.Ordinal)
            < source.IndexOf("if (_loaded && !force)", StringComparison.Ordinal));
    }

    [Fact]
    public void 운송원장SignalR수신은_기사Cache강제재조회와30초보완조회에연결된다()
    {
        var service = Read("DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs");
        var realtimeClient = Read("Ssalddel.Client.Infrastructure/Transport/TransportRequestLedgerRealtimeClient.cs");
        var refreshPolicy = Read("Ssalddel.Client.Infrastructure/Transport/TransportRequestLedgerObserver.cs");

        Assert.Contains("_realtimeClient.StartAsync", service);
        Assert.Contains("_ledgerObserver.RefreshRequested += OnLedgerRefreshRequested", service);
        Assert.Contains("RefreshAsync(force: true)", service);
        Assert.Contains("TransportRequestLedgerRealtime.ChangedMethod", realtimeClient);
        Assert.Contains("_observer.RequestRefresh", realtimeClient);
        Assert.Contains("TimeSpan.FromSeconds(30)", refreshPolicy);
    }

    [Fact]
    public void 배차결정은_서버Command뒤_원장을강제재조회하고_기존추천객체를성공상태로꾸미지않는다()
    {
        var source = Read("DriverApp/Services/DriverRecommendationDecisionService.cs");
        var acceptMethod = source[
            source.IndexOf("public async Task<RecommendationDecisionState> AcceptAsync", StringComparison.Ordinal)
            ..source.IndexOf("public RecommendationDecisionState Hold", StringComparison.Ordinal)];

        Assert.True(
            acceptMethod.IndexOf("_dispatchActionApi.수락Async", StringComparison.Ordinal)
            < acceptMethod.IndexOf("RefreshServerLedgerAsync", StringComparison.Ordinal));
        Assert.True(
            acceptMethod.IndexOf("RefreshServerLedgerAsync", StringComparison.Ordinal)
            < acceptMethod.IndexOf("SaveAccepted", StringComparison.Ordinal));
        Assert.Contains("_driverData.RefreshAsync(cancellationToken, force: true)", source);
        Assert.Contains("updateRequest: false", acceptMethod);
        Assert.Contains("observeRequest: false", acceptMethod);
    }

    [Fact]
    public void 운행종료는_서버확정뒤_단말위치송신을중지한다()
    {
        var source = Read("DriverApp/ViewModels/Driver/Work/기사운행시작PageViewModel.cs");
        var stopMethod = source[source.IndexOf("private async Task 운행종료Async", StringComparison.Ordinal)..];

        Assert.True(
            stopMethod.IndexOf("근무기능.운행종료.실행Async", StringComparison.Ordinal)
            < stopMethod.IndexOf("_위치송신.StopAsync", StringComparison.Ordinal));
        Assert.Contains("서버 운행은 종료됐지만 단말의 위치 송신 중지 확인에 실패했습니다.", stopMethod);
    }

    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
