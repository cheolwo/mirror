using Ssalddel.Contracts.Common.Community;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests;

public sealed class WebNavigationCatalogTests
{
    [Theory]
    [InlineData("driver", "/driver/home")]
    [InlineData("shipper", "/shipper")]
    [InlineData("warehouse", "/warehouse")]
    [InlineData("orderer", "/orderer")]
    [InlineData("customs", "/global/import-requests")]
    public void GetBusinessHome_ReturnsRoleHome(string themeCode, string expected)
    {
        Assert.Equal(expected, WebNavigationCatalog.GetBusinessHome(themeCode));
    }

    [Theory]
    [InlineData("driver", "/driver/home")]
    [InlineData("shipper", "/shipper")]
    [InlineData("warehouse", "/warehouse")]
    public void GetBusinessItems_ContainsOnlyCurrentRoleAreaAndDiagram(string themeCode, string rolePrefix)
    {
        var items = WebNavigationCatalog.GetBusinessItems(themeCode);

        Assert.Contains(items, item => item.Href == WebNavigationCatalog.DiagramRoute);
        Assert.Contains(items, item => item.Href.StartsWith(rolePrefix, StringComparison.Ordinal));
        Assert.DoesNotContain(items, item => item.Href == "/login");
        Assert.InRange(items.Count, 2, themeCode == "warehouse" ? 16 : 12);
        if (themeCode == "warehouse")
        {
            Assert.Contains(items, item => item.Href == WarehouseManagerRoutes.OutboundPlanReview);
            Assert.Contains(items, item => item.Href == WarehouseManagerRoutes.TransportRequestDraft);
        }
    }

    [Fact]
    public void GetBusinessItems_ForGuest_ReturnsMinimalPublicMenu()
    {
        var items = WebNavigationCatalog.GetBusinessItems("guest");

        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Href == "/login");
        Assert.DoesNotContain(items, item => item.Href == WebNavigationCatalog.DiagramRoute);
    }

    [Fact]
    public void GetBusinessItems_ForOrderer_ExposesV15TradeLedgerChecks()
    {
        var routes = WebNavigationCatalog.GetBusinessItems("orderer")
            .Select(item => item.Href)
            .ToArray();

        Assert.Contains(WebOrdererRoutes.GroupPurchase, routes);
        Assert.Contains(WebOrdererRoutes.GroupPurchaseDemand, routes);
        Assert.Contains(WebOrdererRoutes.IndividualImportLedger, routes);
        Assert.Contains(WebOrdererRoutes.IndividualExportLedger, routes);
        Assert.Contains(WebOrdererRoutes.GroupExportLedger, routes);
    }

    [Fact]
    public void GetBusinessItems_ForDriver_UsesV20ResponsibilityRoutes()
    {
        var routes = WebNavigationCatalog.GetBusinessItems("driver")
            .Select(item => item.Href)
            .ToArray();

        Assert.Contains(DriverRoutes.WorkStart, routes);
        Assert.Contains(DriverRoutes.Recommendations, routes);
        Assert.Contains(DriverRoutes.CurrentTransport, routes);
        Assert.DoesNotContain(DriverRoutes.DispatchDecisions, routes);
    }

    [Fact]
    public void NavigationItems_DoNotExposeIntegratedHomeMenu()
    {
        var themeCodes = new[] { "guest", "driver", "shipper", "warehouse", "orderer", "customs", "member" };
        var items = themeCodes
            .SelectMany(WebNavigationCatalog.GetBusinessItems)
            .Concat(WebNavigationCatalog.IntegratedItems);

        Assert.DoesNotContain(items, item =>
            item.Href == "/"
            || item.Title == "통합 홈");
    }

    [Fact]
    public void IntegratedItems_ExposeFigmaRolePagesWithoutUnitedStatesBuyerPage()
    {
        Assert.Equal(
        [
            CommunityPageRoutes.WorldMap,
            "/roles/01/",
            "/roles/02/",
            "/roles/03/",
            "/roles/04/",
            "/roles/05/"
        ],
            WebNavigationCatalog.IntegratedItems.Select(item => item.Href));
        Assert.DoesNotContain(
            WebNavigationCatalog.IntegratedItems,
            item => item.Href.StartsWith("/us/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CommunityItems_ExposePersonalExplorationInsteadOfBusinessFeatures()
    {
        var items = WebNavigationCatalog.CommunityItems;

        Assert.Equal(
        [
            "/community/me",
            "/community/me/posts",
            "/community/me/actions",
            "/community/roles/apply",
            "/community/me/ledgers",
            "/community/me/notifications",
            "/community/decorations",
            "/community/me/settings"
        ],
            items.Select(item => item.Href));
        Assert.DoesNotContain(items, item => item.Href is "/community/workspace"
            or "/community/actions"
            or "/community/group-purchase"
            or "/community/group-import");
    }

    [Fact]
    public void VisibleCommunityNavigationItems_ExposeOnlyPublicCommunityAndBasicPersonalViews()
    {
        var items = WebNavigationCatalog.VisibleCommunityNavigationItems;

        Assert.Equal(
        [
            ("통합 지도", CommunityPageRoutes.WorldMap),
            ("공개 커뮤니티", "/community"),
            ("지역 문화·특산물", "/community/regions"),
            ("내 정보", "/community/me"),
            ("내 글", "/community/me/posts")
        ],
            items.Select(item => (item.Title, item.Href)));

        Assert.Contains(
            WebNavigationCatalog.CommunityItems,
            item => item.Href == "/community/me/settings");
        Assert.DoesNotContain(
            items,
            item => item.Href == "/community/me/settings");
    }

    [Theory]
    [InlineData("community")]
    [InlineData("community/group-purchase")]
    [InlineData("community/global-trade/101?tab=ledger")]
    [InlineData("ko/community")]
    [InlineData("en/community")]
    public void IsCommunityRoute_ReturnsTrueForCommunityArea(string relativePath)
    {
        Assert.True(WebNavigationCatalog.IsCommunityRoute(relativePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("driver/home")]
    [InlineData("diagram")]
    public void IsCommunityRoute_ReturnsFalseOutsideCommunityArea(string relativePath)
    {
        Assert.False(WebNavigationCatalog.IsCommunityRoute(relativePath));
    }
}
