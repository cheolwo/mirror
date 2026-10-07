using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Tests.Contracts.Common;

public sealed class CommerceAuthenticationRoutesTests
{
    [Theory]
    [InlineData("/commerce/seller")]
    [InlineData("/commerce/privacy")]
    [InlineData("/commerce/privacy/case-12")]
    [InlineData("/commerce/disputes/case-34")]
    [InlineData("/commerce/disputes?sourceKind=food-order&sourceId=FOOD-20261006")]
    [InlineData("/commerce/disputes?sourceKind=cargo-request&sourceId=request%3A42")]
    public void 인증복귀가_보호업무와_사건또는거래를_보존한다(string route)
    {
        Assert.True(CommerceAuthenticationRoutes.TryReturnRoute(route, out var result));
        Assert.Equal(route, result);
        Assert.Equal(route, NeighborhoodExchangeAuthenticationRoutes.ReturnRoute(route));
        Assert.Equal("/community/login?returnUrl=" + Uri.EscapeDataString(route),
            CommerceAuthenticationRoutes.LoginHref("/community/login", route));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://outside.example/commerce/privacy")]
    [InlineData("//outside.example/commerce/privacy")]
    [InlineData("/\\outside.example/commerce/privacy")]
    [InlineData("/commerce/privacy/%2foutside.example")]
    [InlineData("/commerce/privacy/%5coutside.example")]
    [InlineData("/commerce/privacy/%252foutside.example")]
    [InlineData("/commerce/privacy/../login")]
    [InlineData("/commerce/privacy/%2e%2e/login")]
    [InlineData("/commerce/privacy/%0acase")]
    [InlineData("/commerce/privacy/case?returnUrl=//outside.example")]
    [InlineData("/commerce/disputes?sourceKind=food-order&sourceId=id&sourceId=other")]
    [InlineData("/commerce/disputes?sourceKind=food-order&sourceId=id&password=secret")]
    [InlineData("/commerce/disputes?sourceKind=unsupported&sourceId=id")]
    [InlineData("/commerce/disputes?sourceKind=food-order&sourceId=id%0aother")]
    [InlineData("/commerce/disputes?sourceKind=food-order")]
    [InlineData("/commerce/privacy-case")]
    [InlineData("/orders/food")]
    public void 외부주소_경로탈출_중복쿼리와_다른업무는_공개안내로_제한한다(string? route)
    {
        Assert.False(CommerceAuthenticationRoutes.TryReturnRoute(route, out _));
        Assert.Equal(CommerceAuthenticationRoutes.Notices, CommerceAuthenticationRoutes.ReturnRoute(route));
    }

    [Fact]
    public void 사건선택과_원래거래_식별자는_쿼리값으로만_조립한다()
    {
        Assert.Equal("/commerce/privacy/case-1", CommerceAuthenticationRoutes.PrivacyRoute("case-1"));
        Assert.Equal("/commerce/disputes/case-2", CommerceAuthenticationRoutes.DisputeRoute("case-2", "food-order", "ignored"));
        Assert.Equal("/commerce/disputes?sourceKind=food-order&sourceId=order%26other%3Dvalue",
            CommerceAuthenticationRoutes.DisputeRoute(null, "food-order", "order&other=value"));
        Assert.Equal("/commerce/notices", CommerceAuthenticationRoutes.PrivacyRoute(new string('a', 161)));
        Assert.Throws<ArgumentException>(() => CommerceAuthenticationRoutes.LoginHref("//outside.example", "/commerce/privacy"));
    }
}
