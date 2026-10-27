using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Tests.Contracts.Common.Community;

public sealed class 공간공동운영업무RoutesTests
{
    [Theory]
    [InlineData("production")]
    [InlineData("inbound")]
    [InlineData("storage")]
    [InlineData("order")]
    [InlineData("outbound")]
    [InlineData("delivery")]
    [InlineData("settlement")]
    public void 일곱업무단계는_원장과선택회차를_로그인뒤에도_같이보존한다(string stage)
    {
        var route = 공간공동운영업무Routes.Detail("hub:동네&one", stage, "cycle:회차&two");
        var expected = "/community/space-cooperations/hub%3A%EB%8F%99%EB%84%A4%26one/work/" + stage
            + "?cycle=cycle%3A%ED%9A%8C%EC%B0%A8%26two";
        Assert.Equal(expected, route);
        Assert.True(공간공동운영업무Routes.TryCanonical(route, out var canonical));
        Assert.Equal(route, canonical);
        Assert.Equal(route, NeighborhoodExchangeAuthenticationRoutes.ReturnRoute(route));
        Assert.Equal("/community/login?returnUrl=" + Uri.EscapeDataString(route),
            NeighborhoodExchangeAuthenticationRoutes.LoginHref(route));
    }

    [Fact]
    public void 원장요약의_회차query와_기존회차없는_상세경로는_함께유지한다()
    {
        Assert.Equal("/community/space-cooperations/hub", 공간공동운영업무Routes.Overview("hub"));
        var selected = 공간공동운영업무Routes.Overview("hub", "cycle-2");
        Assert.Equal("/community/space-cooperations/hub?cycle=cycle-2", selected);
        Assert.Equal(selected, NeighborhoodExchangeAuthenticationRoutes.ReturnRoute(selected));
        Assert.True(공간공동운영업무Routes.TryCanonical("/community/space-cooperations/hub:동네?cycle=회차:2", out var canonical));
        Assert.Equal("/community/space-cooperations/hub%3A%EB%8F%99%EB%84%A4?cycle=%ED%9A%8C%EC%B0%A8%3A2", canonical);
        Assert.Equal("/community/space-cooperations", NeighborhoodExchangeAuthenticationRoutes.ReturnRoute("/community/space-cooperations"));
        Assert.Equal("/community/space-cooperations/new", NeighborhoodExchangeAuthenticationRoutes.ReturnRoute("/community/space-cooperations/new"));
    }

    [Theory]
    [InlineData("/community/space-cooperations/hub/work/inbound")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=")]
    [InlineData("/community/space-cooperations/hub/work/unknown?cycle=c1")]
    [InlineData("/community/space-cooperations/hub/work/Inbound?cycle=c1")]
    [InlineData("/community/space-cooperations/hub/work/inbound/other?cycle=c1")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=c1&cycle=c2")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=c1&")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=c1&action=accept")]
    [InlineData("/community/space-cooperations/hub/work/inbound?returnUrl=//outside.example&cycle=c1")]
    [InlineData("/community/space-cooperations/hub/work/inbound?%63ycle=c1")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=c1?other=value")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=c1#other")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=%2foutside.example")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=%5coutside.example")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=%252foutside.example")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=%2e%2e")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=%0ac1")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=+c1")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=c1%23other")]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=c1%3fother")]
    [InlineData("/community/space-cooperations/hub%2ftwo/work/inbound?cycle=c1")]
    [InlineData("/community/space-cooperations/%252foutside/work/inbound?cycle=c1")]
    [InlineData("/community/space-cooperations/%2e%2e/work/inbound?cycle=c1")]
    [InlineData("/community/space-cooperations/hub/work/%252finbound?cycle=c1")]
    [InlineData("/community/space-cooperations/hub/../work/inbound?cycle=c1")]
    [InlineData("/community/space-cooperations/new?cycle=c1")]
    [InlineData("/community/space-cooperations?cycle=c1")]
    [InlineData("/community/space-cooperations/hub?cycle=c1&other=value")]
    [InlineData("/community/space-cooperations/hub?cycle=c1&cycle=c2")]
    [InlineData("/community/space-cooperations/hub?cycle=c1#other")]
    [InlineData("https://outside.example/community/space-cooperations/hub/work/inbound?cycle=c1")]
    [InlineData("//outside.example/community/space-cooperations/hub/work/inbound?cycle=c1")]
    public void 모호한회차와_추가명령query와_경로탈출은_로그인복귀로_허용하지않는다(string route)
    {
        Assert.False(공간공동운영업무Routes.TryCanonical(route, out _));
        Assert.Equal(NeighborhoodExchange.Home, NeighborhoodExchangeAuthenticationRoutes.ReturnRoute(route));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" new")]
    [InlineData("new")]
    [InlineData("login")]
    [InlineData("CREATE")]
    [InlineData("../other")]
    [InlineData("one/two")]
    [InlineData("one\\two")]
    [InlineData("one%2Ftwo")]
    [InlineData("one?other")]
    [InlineData("one#other")]
    [InlineData("one\none")]
    public void 원장식별자는_다른경로나_작성인증의도로_해석되지_않는다(string id)
    {
        Assert.Throws<ArgumentException>(() => 공간공동운영업무Routes.Detail(id, "inbound", "c1"));
        Assert.Throws<ArgumentException>(() => 공간공동운영업무Routes.Overview(id, "c1"));
    }

    [Fact]
    public void 지원하지않는단계와_빈회차를_기본회차로_자동치환하지않는다()
    {
        Assert.Equal(7, 공간공동운영업무Routes.StageKeys.Count);
        Assert.False(공간공동운영업무Routes.IsKnownStage(null));
        Assert.Throws<ArgumentException>(() => 공간공동운영업무Routes.Detail("hub", "dispatch", "c1"));
        Assert.Throws<ArgumentException>(() => 공간공동운영업무Routes.Detail("hub", "inbound", ""));
        Assert.Throws<ArgumentException>(() => 공간공동운영업무Routes.Detail("hub", "inbound", null!));
        Assert.Throws<ArgumentException>(() => 공간공동운영업무Routes.Overview("hub", ""));
    }

    [Fact]
    public void query에서_복구한_공백과플러스도_호스트바인딩과_같은_한회차ID로_정규화한다()
    {
        Assert.True(공간공동운영업무Routes.TryCanonical("/community/space-cooperations/hub/work/inbound?cycle=회차+2", out var spaces));
        Assert.Equal(공간공동운영업무Routes.Detail("hub", "inbound", "회차 2"), spaces);
        Assert.True(공간공동운영업무Routes.TryCanonical("/community/space-cooperations/hub/work/inbound?cycle=c%2B1", out var plus));
        Assert.Equal(공간공동운영업무Routes.Detail("hub", "inbound", "c+1"), plus);
    }
}
