namespace Ssalddel.Tests.Ui.Common;

public sealed class RolePurposeMobileNavigationTests
{
    [Fact]
    public void 커뮤니티01은_업무단계대신_정보둘러보기로표현한다()
    {
        var source = Read(
            "SsalddelApp",
            "Components",
            "Layout",
            "CommunityMobileLayout.razor");

        Assert.Contains("생활·지역·재료·시세 정보를 편하게 둘러보고", source);
        Assert.Contains("<span>둘러보기</span>", source);
        Assert.DoesNotContain("<RolePurposeJourney", source);
    }

    [Fact]
    public void 공용역할흐름은_네가지목적상태와실제Route를표현한다()
    {
        var component = Read(
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "RolePurposeJourney.razor");
        var model = Read(
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Models",
            "RolePurposeJourneyStep.cs");

        Assert.Contains("역할 업무 흐름", component);
        Assert.Contains("href=\"@step.Href\"", component);
        Assert.Contains("BoundaryNote", component);
        Assert.Contains("RolePurposeJourneyStep", model);
        Assert.Contains("IsPrimary", model);
    }

    [Theory]
    [InlineData(
        "OrdererApp/Components/Pages/Home.razor",
        "주문자",
        "시작할 일",
        "OrdererRoutes.Restaurants",
        "Orders")]
    [InlineData(
        "SsalddelApp/Components/Shared/ShipperHomeAppShell.razor",
        "화주·판매자",
        "시작할 일",
        "ShipperRoutes.Request",
        "ShipperRoutes.SalesOrders")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/Home/기사홈Page.razor",
        "운송 기사",
        "시작할 일",
        "DriverRoutes.Recommendations",
        "DriverRoutes.CurrentMonthSettlement")]
    [InlineData(
        "WarehouseManagerApp/Components/Pages/WarehouseWorkspace.razor",
        "창고 관리자",
        "시작할 일",
        "WarehouseManagerRoutes.ExpectedInbounds",
        "WarehouseManagerRoutes.WarehouseHistory")]
    public void 역할앱02부터05는_시작과완료근거를실제업무Route로연결한다(
        string relativePath,
        string roleName,
        string firstState,
        string startRoute,
        string evidenceRoute)
    {
        var source = Read(relativePath.Split('/'));

        Assert.Contains("<RolePurposeJourney", source);
        Assert.Contains($"RoleName=\"{roleName}\"", source);
        Assert.Contains($"new(\"{firstState}\"", source);
        Assert.Contains("\"진행 확인\"", source);
        Assert.Contains("\"확인 필요\"", source);
        Assert.Contains("\"완료 근거\"", source);
        Assert.Contains(startRoute, source);
        Assert.Contains(evidenceRoute, source);
    }

    [Theory]
    [InlineData("OrdererApp/Components/Layout/MainLayout.razor.css")]
    [InlineData("SsalddelApp/Components/Layout/ShipperMobileLayout.razor.css")]
    [InlineData("DriverApp/wwwroot/driver-mobile.css")]
    [InlineData("WarehouseManagerApp/wwwroot/warehouse-mobile.css")]
    public void 역할흐름은_각앱의기존강조색을상속한다(string relativePath)
    {
        var source = Read(relativePath.Split('/'));

        Assert.Contains("--role-purpose-accent:", source);
        Assert.Contains("--role-purpose-soft:", source);
        Assert.Contains("--role-purpose-border:", source);
    }

    [Fact]
    public void 통합앱역할선택은_공통홈에머물지않고_해당업무홈으로이동한다()
    {
        var source = Read(
            "SsalddelApp",
            "Components",
            "Pages",
            "RoleNeutralHome.razor");

        Assert.Contains("SsalddelClientRole.Shipper => ShipperRoutes.ShipperHome", source);
        Assert.Contains("SsalddelClientRole.WarehouseManager => ShipperRoutes.WarehouseWorkspace", source);
    }

    [Fact]
    public void 주문자앱은_목적형홈을_기본시작화면으로연다()
    {
        var source = Read("OrdererApp", "MainPage.xaml");

        Assert.Contains("StartPath=\"/\"", source);
    }

    [Fact]
    public void 기사홈은_공용목적컴포넌트를_실제Razor컴포넌트로해석한다()
    {
        var source = Read(
            "DriverApp",
            "Components",
            "Pages",
            "Driver",
            "Home",
            "기사홈Page.razor");

        Assert.Contains("@using Ssalddel.Ui.Common.Areas.App.Components", source);
        Assert.Contains("@inherits DriverApp.Components.MvvmComponentBase<기사홈PageViewModel>", source);
    }

    private static string Read(params string[] segments)
        => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(segments).ToArray()));

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
