namespace Ssalddel.Tests.Architecture;

public sealed class DriverLoginAndWorkspaceStateCompositionTests
{
    [Fact]
    public void 기사로그인은_요청한_내부업무화면으로만_복귀한다()
    {
        var routes = Read("DriverApp", "Services/DriverRoutes.cs");
        var login = Read("DriverApp", "Components/Pages/Login.razor");
        var layout = Read("DriverApp", "Components/Layout/MainLayout.razor");

        Assert.Contains("LoginFor(string returnRoute)", routes);
        Assert.Contains("Uri.EscapeDataString(returnRoute)", routes);
        Assert.Contains("SupplyParameterFromQuery(Name = \"returnUrl\")", login);
        Assert.Contains("returnUrl.StartsWith(\"/driver/\"", login);
        Assert.Contains("!returnUrl.Contains(\"://\"", login);
        Assert.Contains("안전한복귀경로 ?? DriverRoutes.HomeSummary", login);
        Assert.Contains("로그인 및 계정", layout);
        Assert.Contains("href=\"@DriverRoutes.Login\"", layout);
    }

    [Theory]
    [InlineData("02_Recommendation/추천목록Page.razor", "DriverRoutes.Recommendations")]
    [InlineData("02_Recommendation/추천상세Page.razor", "DriverRoutes.RecommendationDetail(의뢰Id)")]
    [InlineData("02_Recommendation/배차처리Page.razor", "DriverRoutes.RecommendationDecision(의뢰Id)")]
    [InlineData("03_Progress/진행중운송Page.razor", "DriverRoutes.CurrentTransport")]
    public void 기사업무화면은_직접진입해도_인증_조회_재시도_상태를_구분한다(
        string relativePath,
        string expectedReturnRoute)
    {
        var source = Read(
            "DriverApp",
            $"Components/Pages/Driver/{relativePath.Replace('/', Path.DirectorySeparatorChar)}");

        Assert.Contains("@inject IAuthSession AuthSession", source);
        var currentTransport = relativePath.EndsWith("진행중운송Page.razor", StringComparison.Ordinal);
        Assert.Contains(currentTransport ? "await AuthSession.RestoreAsync(_갱신토큰)" : "await AuthSession.RestoreAsync()", source);
        Assert.Contains(currentTransport ? "await Samples.RefreshAsync(_갱신토큰, force: force)" : "await Samples.RefreshAsync(force: force)", source);
        Assert.Contains($"DriverRoutes.LoginFor({expectedReturnRoute})", source);
        Assert.Contains("다시 시도", source);
        Assert.Contains("_데이터로딩중", source);
        Assert.Contains("_데이터오류", source);
    }

    [Fact]
    public void 기사내역화면은_전용수명모델에_인증_조회_재시도와_계정정리를_위임한다()
    {
        var page = Read("DriverApp", "Components/Pages/Driver/03_Progress/배달내역Page.razor");
        var model = Read("DriverApp", "ViewModels/Driver/Transport/기사운송내역PageViewModel.cs");
        var services = Read("DriverApp", "Services/DriverServiceCollectionExtensions.cs");
        var component = Read("Ssalddel.Ui.Common", "Areas/App/Components/MvvmComponentBase.cs");

        Assert.Contains("@inherits DriverApp.Components.MvvmComponentBase<기사운송내역PageViewModel>", page);
        Assert.Contains("=> ViewModel.InitializeAsync()", page);
        Assert.Contains("=> ViewModel.RefreshAsync()", page);
        Assert.Contains("ViewModel.불러오는중", page);
        Assert.Contains("ViewModel.로그인필요", page);
        Assert.Contains("ViewModel.오류메시지", page);
        Assert.Contains("ViewModel.운송목록.Count == 0", page);
        Assert.Contains("DriverRoutes.LoginFor(DriverRoutes.DeliveryHistory)", page);
        Assert.Contains("OnClick=\"재조회Async\">다시 시도", page);

        // 페이지마다 별도 모델을 만들고, 공통 컴포넌트가 페이지 이탈 시 모델을 정리합니다.
        // 늦은 응답·계정 전환·조회 경합의 실제 상태는 CargoTransportHistoryLifetimeTests에서 검증합니다.
        Assert.Contains("services.AddTransient<기사운송내역PageViewModel>()", services);
        Assert.Contains("await _auth.RestoreAsync(_pageToken)", model);
        Assert.Contains("await _api.목록조회Async(query.Token)", model);
        Assert.Contains("_auth.Changed += OnAuthenticationChanged", model);
        Assert.Contains("_auth.Changed -= OnAuthenticationChanged", model);
        Assert.Contains("_pageCancellation.Cancel()", model);
        Assert.Contains("if (_viewModel is IDisposable disposable)", component);
        Assert.Contains("disposable.Dispose()", component);
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
