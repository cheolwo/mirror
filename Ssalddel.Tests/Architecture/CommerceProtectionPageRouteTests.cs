using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Architecture;

public sealed class CommerceProtectionPageRouteTests
{
    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb)]
    [InlineData(SsalddelPageAppCodes.Shipper)]
    [InlineData(SsalddelPageAppCodes.Orderer)]
    [InlineData(SsalddelPageAppCodes.RestaurantDesk)]
    [InlineData(SsalddelPageAppCodes.FoodDeliveryDriver)]
    public void 보호페이지는_공개안내와_본인처리경계로_분류되고_역할기능에_가려지지않는다(string app)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, "/commerce/notices", out var notice));
        Assert.False(notice.RequiresAuthentication);
        Assert.Equal(PageInteractionBoundary.ReadOnly, notice.Boundary);
        Assert.Empty(notice.FeatureKeys);
        Assert.False(notice.HasExternalEffects);

        foreach (var route in new[]
        {
            "/commerce/seller", "/commerce/privacy", "/commerce/privacy/{CaseId}",
            "/commerce/disputes", "/commerce/disputes/{CaseId}"
        })
        {
            Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, route, out var capability), route);
            Assert.True(capability.RequiresAuthentication);
            Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
            Assert.Empty(capability.FeatureKeys);
            Assert.False(capability.HasExternalEffects);
        }
    }

    [Fact]
    public void 역할보호페이지는_독립레이아웃으로열고_본인처리페이지는_인증을_유지한다()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Ssalddel.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var route = File.ReadAllText(Path.Combine(root!.FullName, "RestaurantDeskApp", "Components", "RestaurantRouteView.razor"));
        var publicStart = route.IndexOf("if (RouteData.PageType == typeof(Pages.Login)", StringComparison.Ordinal);
        var authStart = route.IndexOf("await AuthService.EnsureAccessTokenAsync()", StringComparison.Ordinal);
        Assert.True(publicStart >= 0 && authStart > publicStart);
        var publicBranch = route[publicStart..authStart];
        Assert.Contains("RouteData.PageType == typeof(Pages.CommerceNoticesPage)", publicBranch);
        foreach (var privatePage in new[] { "CommerceSellerPage", "CommercePrivacyPage", "CommerceDisputesPage" }) Assert.DoesNotContain(privatePage, publicBranch);
        Assert.Contains("CommerceAuthenticationRoutes.TryReturnRoute", route);
        Assert.Contains("NavigationManager.NavigateTo(loginHref, replace: true)", route);
        var notice = File.ReadAllText(Path.Combine(root.FullName, "RestaurantDeskApp", "Components", "Pages", "CommerceNoticesPage.razor"));
        Assert.Contains("@page \"/commerce/notices\"", notice);
        Assert.Contains("@layout AuthLayout", notice);

        var driverRoute = File.ReadAllText(Path.Combine(root.FullName, "FDriverApp", "Components", "FDriverSupportRoutes.razor"));
        Assert.Contains("DefaultLayout=\"typeof(ProtectionSupportLayout)\"", driverRoute);
        Assert.Contains("Layout=\"typeof(ProtectionSupportLayout)\"", driverRoute);
        Assert.DoesNotContain("typeof(MainLayout)", driverRoute);
        var nativeSupport = File.ReadAllText(Path.Combine(root.FullName, "FDriverApp", "Pages", "ProtectionSupportPage.cs"));
        Assert.Contains("ComponentType = typeof(FDriverSupportRoutes)", nativeSupport);
        foreach (var pageName in new[] { "CommerceDisputesPage", "CommercePrivacyPage", "CommerceSellerPage", "CommerceNoticesPage", "SupportLoginPage" })
        {
            var driverPage = File.ReadAllText(Path.Combine(root.FullName, "FDriverApp", "Components", "Pages", pageName + ".razor"));
            Assert.DoesNotContain("@layout", driverPage);
        }
    }

    [Fact]
    public void 주문자_로그인은_인증전용레이아웃과_독립복귀화면을_사용한다()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Ssalddel.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var page = File.ReadAllText(Path.Combine(root!.FullName, "OrdererApp", "Components", "Pages", "Login.razor"));
        Assert.Contains("@page \"/login\"", page);
        Assert.Contains("@layout AuthenticationLayout", page);
        Assert.Contains("OrdererCommerceAuthenticationPage ReturnUrl=", page);
        Assert.DoesNotContain("OrdererFoodOrderWorkspace", page);
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(SsalddelPageAppCodes.Orderer, "/login", out var capability));
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, capability.Boundary);
    }
}
