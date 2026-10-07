using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Contracts.Common;

public sealed class NeighborhoodExchangeAuthenticationRoutesTests
{
    [Theory]
    [InlineData("/community/exchange")]
    [InlineData("/workspace/community?panel=post&target=42")]
    [InlineData("/community/exchange/write")]
    [InlineData("/community/exchange/deliveries/new?sourcePostId=42")]
    [InlineData("/community/exchange/deliveries/NEIGHBOR-42")]
    public void 인증뒤에_선택한생활교류업무로만복귀한다(string route)
    {
        Assert.Equal(route, NeighborhoodExchangeAuthenticationRoutes.ReturnRoute(route));
        Assert.Equal("/community/login?returnUrl=" + Uri.EscapeDataString(route),
            NeighborhoodExchangeAuthenticationRoutes.LoginHref(route));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://outside.example/community/exchange")]
    [InlineData("//outside.example/community/exchange")]
    [InlineData("/\\outside.example/community/exchange")]
    [InlineData("/community/exchange-evil")]
    [InlineData("/community/exchange/%2foutside.example")]
    [InlineData("/community/exchange/%5coutside.example")]
    [InlineData("/community/exchange/%252foutside.example")]
    [InlineData("/community/exchange/../login")]
    [InlineData("/community/exchange/%2e%2e/login")]
    [InlineData("/community/exchange/%0anew")]
    [InlineData("/shipper")]
    public void 외부주소와다른업무경계는_공개생활교류로복귀시킨다(string? route)
        => Assert.Equal(NeighborhoodExchange.Home, NeighborhoodExchangeAuthenticationRoutes.ReturnRoute(route));

    [Fact]
    public void 커뮤니티로그인은_역할업무를실행하지않는_공개인증화면이다()
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(
            SsalddelPageAppCodes.Shipper, NeighborhoodExchangeAuthenticationRoutes.Login, out var rule));
        Assert.Equal("shipper-community-login", rule.PageKey);
        Assert.False(rule.RequiresAuthentication);
        Assert.False(rule.HasExternalEffects);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, rule.Boundary);
        Assert.Equal(["CommunityTrustWorkflow"], rule.FeatureKeys);
    }
}
