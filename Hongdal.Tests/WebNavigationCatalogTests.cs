using Hongdal.WebApp.Services;

namespace Hongdal.Tests;

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
        Assert.InRange(items.Count, 2, 6);
    }

    [Fact]
    public void GetBusinessItems_ForGuest_ReturnsMinimalPublicMenu()
    {
        var items = WebNavigationCatalog.GetBusinessItems("guest");

        Assert.Equal(3, items.Count);
        Assert.Contains(items, item => item.Href == "/login");
        Assert.DoesNotContain(items, item => item.Href == WebNavigationCatalog.DiagramRoute);
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

    [Theory]
    [InlineData("community")]
    [InlineData("community/group-purchase")]
    [InlineData("community/global-trade/101?tab=ledger")]
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
