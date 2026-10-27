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

    [Theory]
    [InlineData("/community/space-cooperations")]
    [InlineData("/community/space-cooperations/new")]
    [InlineData("/community/space-cooperations/space-cooperation-123")]
    [InlineData("/community/space-cooperations/hub%3A123")]
    [InlineData("/community/space-cooperations/%EB%8F%99%EB%84%A4")]
    public void 공간공동운영은_로그인뒤_목록_제안또는_선택한단일원장으로_복귀한다(string route)
    {
        Assert.Equal(route, NeighborhoodExchangeAuthenticationRoutes.ReturnRoute(route));
        Assert.Equal("/community/login?returnUrl=" + Uri.EscapeDataString(route),
            NeighborhoodExchangeAuthenticationRoutes.LoginHref(route));
    }

    [Fact]
    public void 공간공동운영원장ID는_경로하나로_다시이스케이프한다()
    {
        const string canonical = "/community/space-cooperations/hub%3A%EB%8F%99%EB%84%A4%26one";
        Assert.Equal(canonical, NeighborhoodExchangeAuthenticationRoutes.ReturnRoute("/community/space-cooperations/hub:동네&one"));
    }

    [Theory]
    [InlineData("https://outside.example/community/space-cooperations/one")]
    [InlineData("//outside.example/community/space-cooperations/one")]
    [InlineData("/community/space-cooperations-evil")]
    [InlineData("/community/space-cooperations/")]
    [InlineData("/community/space-cooperations/one/two")]
    [InlineData("/community/space-cooperations/one%2ftwo")]
    [InlineData("/community/space-cooperations/%252foutside.example")]
    [InlineData("/community/space-cooperations/%5coutside.example")]
    [InlineData("/community/space-cooperations/../login")]
    [InlineData("/community/space-cooperations/%2e%2e")]
    [InlineData("/community/space-cooperations/%0aone")]
    [InlineData("/community/space-cooperations/%20one")]
    [InlineData("/community/space-cooperations/login")]
    [InlineData("/community/space-cooperations/create")]
    [InlineData("/community/space-cooperations/%6Eew")]
    [InlineData("/community/space-cooperations/NEW")]
    [InlineData("/community/space-cooperations/new/one")]
    [InlineData("/community/space-cooperations/one?returnUrl=//outside.example")]
    [InlineData("/community/space-cooperations/new?create=true")]
    [InlineData("/community/space-cooperations/one#new")]
    public void 공간공동운영의_외부주소_인코딩탈출과_모호한인증작성경로는_거부한다(string route)
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
