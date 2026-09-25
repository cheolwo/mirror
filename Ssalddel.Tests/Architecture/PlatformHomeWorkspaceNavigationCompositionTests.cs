namespace Ssalddel.Tests.Architecture;

public sealed class PlatformHomeWorkspaceNavigationCompositionTests
{
    [Fact]
    public void 공용WorkspaceCatalog는_HostURL을소유하지않는다()
    {
        var profile = Read(
            "Ssalddel.Ui.Common",
            "Areas/App/Models/PlatformHomeWorkspaceProfile.cs");
        var context = Read(
            "Ssalddel.Ui.Common",
            "Areas/App/Components/Community/PlatformCommunityHome.DiagramContext.razor.cs");
        var hub = Read(
            "Ssalddel.Ui.Common",
            "Areas/App/Components/Community/PlatformCommunityWorkspaceHub.razor");
        var panel = Read(
            "Ssalddel.Ui.Common",
            "Areas/App/Components/Community/PlatformCommunityWorkPanel.razor");

        Assert.DoesNotContain("\"/", profile);
        Assert.Contains("WorkspaceNavigationResolver.ResolveEntryHref", context);
        Assert.Contains("PageNavigationContext.NormalizeReturnPath", context);
        Assert.Contains("현재 앱 미지원", hub);
        Assert.Contains("현재 앱 미지원", panel);
    }

    [Theory]
    [InlineData("Ssalddel.WebApp", "Program.cs", "WebPlatformHomeWorkspaceNavigationResolver")]
    [InlineData("SsalddelApp", "MauiProgram.cs", "SsalddelAppPlatformHomeWorkspaceNavigationResolver")]
    [InlineData("WarehouseManagerApp", "MauiProgram.cs", "WarehousePlatformHomeWorkspaceNavigationResolver")]
    [InlineData("FDriverApp", "MauiProgram.cs", "FDriverPlatformHomeWorkspaceNavigationResolver")]
    [InlineData("OrdererApp", "MauiProgram.cs", "OrdererPlatformHomeWorkspaceNavigationResolver")]
    public void Host는_WorkspaceResolver를공용Fallback보다먼저등록한다(
        string project,
        string startupPath,
        string resolverName)
    {
        var source = Read(project, startupPath);
        var resolverRegistration = source.IndexOf(
            $"IPlatformHomeWorkspaceNavigationResolver, {resolverName}",
            StringComparison.Ordinal);
        var sharedRegistration = source.IndexOf(
            "AddSsalddelUiCommonAppServices",
            StringComparison.Ordinal);

        Assert.True(resolverRegistration >= 0, $"{project} workspace resolver 등록이 없습니다.");
        Assert.True(sharedRegistration > resolverRegistration, $"{project} resolver는 공용 fallback보다 먼저 등록해야 합니다.");
    }

    [Fact]
    public void 기사App홈은_공용Profile에URL을덮어쓰지않는다()
    {
        var home = Read("FDriverApp", "Components/Pages/FDriverHome.razor");

        Assert.DoesNotContain("with { EntryHref", home);
        Assert.DoesNotContain("/food-delivery/open/workspace", home);
    }

    [Fact]
    public void 모바일HostResolver는_각AppRouteCatalog만참조한다()
    {
        var app = Read(
            "SsalddelApp",
            "Services/SsalddelAppPlatformHomeWorkspaceNavigationResolver.cs");
        var warehouse = Read(
            "WarehouseManagerApp",
            "Services/WarehousePlatformHomeWorkspaceNavigationResolver.cs");
        var driver = Read(
            "FDriverApp",
            "Services/FDriverPlatformHomeWorkspaceNavigationResolver.cs");
        var orderer = Read(
            "OrdererApp",
            "Services/OrdererPlatformHomeWorkspaceNavigationResolver.cs");

        Assert.Contains("ShipperRoutes.Request", app);
        Assert.Contains("ShipperRoutes.WarehouseOutboundWorkStart", app);
        Assert.DoesNotContain("/food", app);
        Assert.DoesNotContain("/driver/", app);

        Assert.Contains("WarehouseManagerRoutes.TransportRequestDraft", warehouse);
        Assert.Contains("WarehouseManagerRoutes.InboundWorkStart", warehouse);
        Assert.DoesNotContain("CommunityPageRoutes.", warehouse);
        Assert.DoesNotContain("/shipper/", warehouse);

        Assert.Contains("CommunityLedgerTemplateKeys.FoodDelivery", driver);
        Assert.Contains("/food-delivery/open/workspace", driver);
        Assert.DoesNotContain("/warehouse", driver);

        Assert.Contains("OrdererRoutes.Food", orderer);
        Assert.Contains("OrdererRoutes.Mart", orderer);
        Assert.Contains("OrdererRoutes.Cargo", orderer);
        Assert.DoesNotContain("CommunityLedgerTemplateKeys.GroupPurchase", orderer);
        Assert.DoesNotContain("CommunityLedgerTemplateKeys.GroupImport", orderer);
        Assert.DoesNotContain("/warehouse", orderer);
        Assert.DoesNotContain("/driver/", orderer);
    }

    private static string Read(string project, string relativePath)
        => File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            project,
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
