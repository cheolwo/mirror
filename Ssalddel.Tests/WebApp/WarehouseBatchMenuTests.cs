using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.WebApp;

public sealed class WarehouseBatchMenuTests
{
    private const string CanonicalPath = "/warehouse/general/outbound-plan-review";
    private static readonly string[] Labels = ["1 출고 검토 묶음", "2 피킹 묶음", "3 포장 묶음", "4 운송 인계 묶음"];

    [Fact]
    public void 웹창고메뉴는_출고공정내기존순서의_유효한네단계로직접진입한다()
    {
        var entries = WebNavigationCatalog.GetBusinessItems("warehouse")
            .Where(item => IsStageEntry(item.Href)).ToArray();

        Assert.Equal(Labels, entries.Select(item => item.Title));
        Assert.Equal(OutboundWorkflowStageCodes.All, entries.Select(item => Stage(item.Href)));
        Assert.Equal(4, entries.Select(item => item.Href).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void 공정묶음메뉴를추가해도_기존단건작업과검토및운송의뢰는보존한다()
    {
        var entries = WebNavigationCatalog.GetBusinessItems("warehouse");
        Assert.Contains(entries, item => item.Title == "입고 검수" && item.Href == WarehouseManagerRoutes.InboundInspection);
        Assert.Contains(entries, item => item.Title == "피킹 작업" && item.Href == WarehouseManagerRoutes.PickingBatch);
        Assert.Contains(entries, item => item.Title == "포장 작업" && item.Href == WarehouseManagerRoutes.PackingTask);
        Assert.Contains(entries, item => item.Title == "출고 인계 준비" && item.Href == WarehouseManagerRoutes.GeneralTransportHandoff);
        Assert.Contains(entries, item => item.Title == "출고예정 검토" && item.Href == CanonicalPath);
        Assert.Contains(entries, item => item.Title == "출고 운송의뢰" && item.Href == WarehouseManagerRoutes.TransportRequestDraft);
    }

    [Theory]
    [InlineData("guest", 2)]
    [InlineData("driver", 6)]
    [InlineData("shipper", 6)]
    [InlineData("orderer", 10)]
    [InlineData("customs", 5)]
    [InlineData("member", 6)]
    public void 다른웹역할의메뉴개수와출고공정비노출은유지한다(string role, int count)
    {
        var entries = WebNavigationCatalog.GetBusinessItems(role);
        Assert.Equal(count, entries.Count);
        Assert.DoesNotContain(entries, item => IsStageEntry(item.Href));
    }

    [Fact]
    public void 창고역할호스트도_같은네단계의이름과정본경로를사용한다()
    {
        var descriptor = RoleWebAppDescriptor.FromAssembly(RoleWebAppDescriptor.WarehouseAssemblyName);
        var entries = descriptor.NavigationItems.Where(item => IsStageEntry(item.Path)).ToArray();
        var integrated = WebNavigationCatalog.GetBusinessItems("warehouse").Where(item => IsStageEntry(item.Href)).ToArray();

        Assert.Equal("05", descriptor.Code);
        Assert.Equal(Labels, entries.Select(item => item.Label));
        Assert.Equal(OutboundWorkflowStageCodes.All, entries.Select(item => Stage(item.Path)));
        Assert.Equal(integrated.Select(item => (item.Title, item.Href)), entries.Select(item => (item.Label, item.Path)));
    }

    [Fact]
    public void 창고역할의단건피킹과기존운영메뉴는그대로남는다()
    {
        var descriptor = RoleWebAppDescriptor.FromAssembly(RoleWebAppDescriptor.WarehouseAssemblyName);
        Assert.Equal(14, descriptor.NavigationItems.Count);
        Assert.Contains(descriptor.NavigationItems, item => item.Label == "피킹" && item.Path == "/warehouse/work/picking-batch");
        Assert.Contains(descriptor.NavigationItems, item => item.Label == "입고 검수" && item.Path == "/warehouse/work/inbound/inspection");
        Assert.Contains(descriptor.NavigationItems, item => item.Label == "작업 보드" && item.Path == "/warehouse/work-board");
        Assert.Contains(descriptor.NavigationItems, item => item.Label == "예외 처리" && item.Path == "/warehouse/exceptions");
    }

    [Fact]
    public void 창고모바일출고는_피킹묶음으로직접진입한다()
    {
        var descriptor = RoleWebAppDescriptor.FromAssembly(RoleWebAppDescriptor.WarehouseAssemblyName);
        var outbound = Assert.Single(descriptor.MobileNavigationItems, item => item.Label == "출고");
        Assert.Equal(CanonicalPath + "?stage=picking", outbound.Path);
        Assert.Equal(4, descriptor.MobileNavigationItems.Count);
    }

    [Theory]
    [InlineData(RoleWebAppDescriptor.CommunityAssemblyName, "01", "/community/map", 6, 3)]
    [InlineData(RoleWebAppDescriptor.OrdererAssemblyName, "02", "/orderer", 6, 4)]
    [InlineData(RoleWebAppDescriptor.ShipperAssemblyName, "03", "/shipper", 6, 4)]
    [InlineData(RoleWebAppDescriptor.DriverAssemblyName, "04", "/driver", 8, 4)]
    public void 다른역할호스트의홈과메뉴구성은영향받지않는다(
        string assembly, string code, string home, int navigationCount, int mobileCount)
    {
        var descriptor = RoleWebAppDescriptor.FromAssembly(assembly);
        Assert.Equal(code, descriptor.Code);
        Assert.Equal(home, descriptor.HomePath);
        Assert.Equal(navigationCount, descriptor.NavigationItems.Count);
        Assert.Equal(mobileCount, descriptor.MobileNavigationItems.Count);
        Assert.DoesNotContain(descriptor.NavigationItems, item => IsStageEntry(item.Path));
        Assert.DoesNotContain(descriptor.MobileNavigationItems, item => IsStageEntry(item.Path));
    }

    private static bool IsStageEntry(string path) => path.StartsWith(CanonicalPath + "?stage=", StringComparison.Ordinal);

    private static string Stage(string path)
    {
        var uri = new Uri("http://localhost" + path);
        Assert.Equal(CanonicalPath, uri.AbsolutePath);
        var stage = Uri.UnescapeDataString(uri.Query["?stage=".Length..]);
        Assert.Contains(stage, OutboundWorkflowStageCodes.All);
        Assert.Equal("?stage=" + stage, uri.Query);
        return stage;
    }
}
