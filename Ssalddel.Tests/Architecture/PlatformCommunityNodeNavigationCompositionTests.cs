namespace Ssalddel.Tests.Architecture;

public sealed class PlatformCommunityNodeNavigationCompositionTests
{
    [Fact]
    public void 공용Node상세는_플랫폼URL과임시업무ID를소유하지않는다()
    {
        var details = Read(
            "Ssalddel.Ui.Common",
            "Areas/App/Components/Community/PlatformCommunityHome.LedgerNodeDetails.razor.cs");
        var forms = Read(
            "Ssalddel.Ui.Common",
            "Areas/App/Components/Community/PlatformCommunityHome.FormNodes.razor.cs");
        var interactions = Read(
            "Ssalddel.Ui.Common",
            "Areas/App/Components/Community/PlatformCommunityHome.DiagramNodeInteractions.razor.cs");
        var panel = Read(
            "Ssalddel.Ui.Common",
            "Areas/App/Components/Community/PlatformCommunityDiagramNodeDetailPanel.razor");

        Assert.Contains("NodeNavigationResolver.Resolve", details);
        Assert.Contains("PageNavigationQueryNames.ReturnPath", details);
        Assert.DoesNotContain("HD-WEB-001", details);
        Assert.DoesNotContain("\"/", details);
        Assert.DoesNotContain("ResolveDiagramFormDetailPath", forms);
        Assert.DoesNotContain("\"/", forms);
        Assert.Contains("!action.CanNavigate || action.Url is null", interactions);
        Assert.Contains("Disabled=\"@(!Presentation.Action.CanNavigate)\"", panel);
        Assert.Contains("연결된 화면 없음", panel);
    }

    [Theory]
    [InlineData("Ssalddel.WebApp", "Program.cs", "WebPlatformCommunityNodeNavigationResolver")]
    [InlineData("SsalddelApp", "MauiProgram.cs", "SsalddelAppPlatformCommunityNodeNavigationResolver")]
    [InlineData("WarehouseManagerApp", "MauiProgram.cs", "WarehousePlatformCommunityNodeNavigationResolver")]
    [InlineData("FDriverApp", "MauiProgram.cs", "FDriverPlatformCommunityNodeNavigationResolver")]
    [InlineData("OrdererApp", "MauiProgram.cs", "OrdererPlatformCommunityNodeNavigationResolver")]
    public void 목적지화면을가진Host는_자기Resolver를공용UI보다먼저등록한다(
        string project,
        string startupPath,
        string resolverName)
    {
        var source = Read(project, startupPath);
        var resolverRegistration = source.IndexOf(
            $"IPlatformCommunityNodeNavigationResolver, {resolverName}",
            StringComparison.Ordinal);
        var sharedRegistration = source.IndexOf(
            "AddSsalddelUiCommonAppServices",
            StringComparison.Ordinal);

        Assert.True(resolverRegistration >= 0, $"{project} resolver 등록이 없습니다.");
        Assert.True(sharedRegistration > resolverRegistration, $"{project} resolver는 공용 fallback보다 먼저 등록해야 합니다.");
    }

    [Fact]
    public void 모바일전문AppResolver는_각App이실제로제공하는RouteCatalog만참조한다()
    {
        var app = Read("SsalddelApp", "Services/SsalddelAppPlatformCommunityNodeNavigationResolver.cs");
        var warehouse = Read("WarehouseManagerApp", "Services/WarehousePlatformCommunityNodeNavigationResolver.cs");
        var driver = Read("FDriverApp", "Services/FDriverPlatformCommunityNodeNavigationResolver.cs");
        var orderer = Read("OrdererApp", "Services/OrdererPlatformCommunityNodeNavigationResolver.cs");

        Assert.Contains("ShipperRoutes.Request", app);
        Assert.Contains("ShipperRoutes.WarehouseWorkspace", app);
        Assert.DoesNotContain("/driver/", app);
        Assert.DoesNotContain("/warehouse/work", app);

        Assert.Contains("WarehouseManagerRoutes.InboundInspection", warehouse);
        Assert.Contains("WarehouseManagerRoutes.PickingBatch", warehouse);
        Assert.DoesNotContain("/shipper/", warehouse);
        Assert.DoesNotContain("/driver/", warehouse);

        Assert.Contains("/food-delivery/open/{Focus}", Read(
            "FDriverApp",
            "Components/Pages/FDriverWorkspaceLaunchPage.razor"));
        Assert.DoesNotContain("/driver/", driver);

        Assert.Contains("OrdererRoutes.Food", orderer);
        Assert.Contains("OrdererRoutes.Mart", orderer);
        Assert.Contains("OrdererRoutes.Cargo", orderer);
        Assert.DoesNotContain("CommunityLedgerTemplateKeys.GroupPurchase", orderer);
        Assert.DoesNotContain("CommunityLedgerTemplateKeys.GroupImport", orderer);
        Assert.Contains("@page \"/food\"", Read("OrdererApp", "Components/Pages/FoodOrderHome.razor"));
        Assert.Contains("@page \"/food/mart\"", Read("OrdererApp", "Components/Pages/MartOrder.razor"));
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
