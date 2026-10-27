using Microsoft.AspNetCore.Components;
using Ssalddel.WebApp.Models;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.WebApp;

public sealed class IntegratedBetaCatalogTests
{
    [Theory]
    [InlineData("/community/space-cooperations", WebInteractionBoundary.ReadOnly, false)]
    [InlineData("/community/space-cooperations/new", WebInteractionBoundary.PlatformPersistence, true)]
    [InlineData("/community/space-cooperations/hub", WebInteractionBoundary.PlatformPersistence, false)]
    [InlineData("/community/space-cooperations/hub/work/inbound?cycle=c1", WebInteractionBoundary.ReadOnly, true)]
    [InlineData("/community/space-cooperations/{LedgerId}/work/{StageKey}", WebInteractionBoundary.ReadOnly, true)]
    public void 공동운영은_공개소개와_인증후명령과_읽기전용업무상세를_구분한다(
        string href, WebInteractionBoundary expectedBoundary, bool expectedAuthentication)
    {
        var state = IntegratedBetaCatalog.Resolve(href);

        Assert.True(state.IsCataloged);
        Assert.Equal(IntegratedBetaStage.Beta, state.Stage);
        Assert.Equal(expectedBoundary, state.Boundary);
        Assert.Equal(expectedAuthentication, state.RequiresAuthentication);
    }

    [Theory]
    [InlineData("/community")]
    [InlineData("/ko/community")]
    [InlineData("/en/community")]
    [InlineData("/community/write")]
    [InlineData("/community/posts/42")]
    public void 익명_커뮤니티_핵심_경로는_운영_상태이며_로그인이_선택이다(string href)
    {
        var state = IntegratedBetaCatalog.Resolve(href);

        Assert.True(state.IsCataloged);
        Assert.Equal(IntegratedBetaStage.Live, state.Stage);
        Assert.False(state.RequiresAuthentication);
        Assert.Equal(WebInteractionBoundary.PlatformPersistence, state.Boundary);
    }

    [Theory]
    [InlineData("/roles/01/")]
    [InlineData("/roles/02/orderer")]
    [InlineData("/roles/03/shipper")]
    [InlineData("/roles/04/driver")]
    [InlineData("/roles/05/warehouse")]
    public void 역할별_독립_WebApp_경로는_운영_중인_읽기_전용_진입점이다(string href)
    {
        var state = IntegratedBetaCatalog.Resolve(href);

        Assert.True(state.IsCataloged);
        Assert.Equal(IntegratedBetaStage.Live, state.Stage);
        Assert.False(state.RequiresAuthentication);
        Assert.Equal(WebInteractionBoundary.ReadOnly, state.Boundary);
    }

    [Theory]
    [InlineData("/information/produce-price-comparison")]
    [InlineData("/information/apple-price-comparison")]
    public void 지역별_농산물가격비교경로는_운영중인_읽기전용정보다(string href)
    {
        var state = IntegratedBetaCatalog.Resolve(href);

        Assert.True(state.IsCataloged);
        Assert.Equal(IntegratedBetaStage.Live, state.Stage);
        Assert.False(state.RequiresAuthentication);
        Assert.Equal(WebInteractionBoundary.ReadOnly, state.Boundary);
    }

    [Theory]
    [InlineData("/community/group-purchase", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/community/group-import", IntegratedBetaStage.Experience, WebInteractionBoundary.Simulation)]
    [InlineData("/shipper/request", IntegratedBetaStage.Beta, WebInteractionBoundary.PlatformPersistence)]
    [InlineData("/shipper/request/REQ-20", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/driver/recommendations", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/driver/recommendations/REQ-20", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/driver/dispatch-decisions/REQ-20", IntegratedBetaStage.Beta, WebInteractionBoundary.Simulation)]
    [InlineData("/driver/transports/current", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/driver/transports/history", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/driver/transport/proof?transportId=20", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/driver/transports/20/pickup", IntegratedBetaStage.Beta, WebInteractionBoundary.Simulation)]
    [InlineData("/driver/notifications", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/driver/notifications/settings", IntegratedBetaStage.Beta, WebInteractionBoundary.PlatformPersistence)]
    [InlineData("/warehouse/work-board", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/orderer", IntegratedBetaStage.Beta, WebInteractionBoundary.ReadOnly)]
    [InlineData("/orderer/ledgers/individual-import", IntegratedBetaStage.Beta, WebInteractionBoundary.PlatformPersistence)]
    [InlineData("/orderer/ledgers/individual-export", IntegratedBetaStage.Beta, WebInteractionBoundary.PlatformPersistence)]
    [InlineData("/orderer/ledgers/group-export", IntegratedBetaStage.Beta, WebInteractionBoundary.PlatformPersistence)]
    public void 대표_업무_경로는_공용Capability의_명시적_실행_경계를_유지한다(
        string href,
        IntegratedBetaStage expectedStage,
        WebInteractionBoundary expectedBoundary)
    {
        var state = IntegratedBetaCatalog.Resolve(href);

        Assert.True(state.IsCataloged);
        Assert.Equal(expectedStage, state.Stage);
        Assert.Equal(expectedBoundary, state.Boundary);
        Assert.NotEqual(IntegratedBetaStage.Live, state.Stage);
    }

    [Fact]
    public void 역할별_통합_홈의_모든_대표_화면은_카탈로그에_등록되어_있다()
    {
        var screens = WebExperienceCatalog.Roles.SelectMany(role => role.Screens);

        Assert.All(screens, screen =>
        {
            var isCataloged = IntegratedBetaCatalog.TryResolve(screen.Href, out var state);

            Assert.True(isCataloged, $"통합 상태가 없는 대표 화면: {screen.Href}");
            Assert.True(state.IsCataloged);
        });
    }

    [Fact]
    public void 역할별_내비게이션의_모든_경로는_카탈로그에_등록되어_있다()
    {
        var themeCodes = new[] { "guest", "driver", "shipper", "warehouse", "orderer", "customs", "member" };
        var items = themeCodes
            .SelectMany(WebNavigationCatalog.GetBusinessItems)
            .Concat(WebNavigationCatalog.CommunityItems)
            .Concat(WebNavigationCatalog.IntegratedItems)
            .DistinctBy(item => item.Href);

        Assert.All(items, item =>
            Assert.True(
                IntegratedBetaCatalog.TryResolve(item.Href, out _),
                $"통합 상태가 없는 내비게이션: {item.Href}"));
    }

    [Fact]
    public void 통합_WebApp의_모든_컴파일된_라우트는_capability_규칙으로_분류된다()
    {
        var routes = typeof(Ssalddel.WebApp.App).Assembly
            .GetTypes()
            .SelectMany(type => type
                .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
                .Cast<RouteAttribute>()
                .Select(attribute => new { Component = type.FullName, attribute.Template }))
            .OrderBy(item => item.Template, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(routes);
        Assert.All(routes, route =>
            Assert.True(
                IntegratedBetaCatalog.TryResolve(route.Template, out _),
                $"capability 규칙이 없는 WebApp 라우트: {route.Template} ({route.Component})"));
    }

    [Fact]
    public void 알_수_없는_경로는_안전하게_준비_중으로_분류한다()
    {
        var state = IntegratedBetaCatalog.Resolve("/future/external-operation?preview=true");

        Assert.False(state.IsCataloged);
        Assert.Equal(IntegratedBetaStage.Preparing, state.Stage);
        Assert.Equal(WebInteractionBoundary.Simulation, state.Boundary);
    }

    [Fact]
    public void 쿼리와_후행_슬래시는_같은_경로_상태로_해석한다()
    {
        var state = IntegratedBetaCatalog.Resolve("/community/?board=free#latest");

        Assert.Equal(IntegratedBetaStage.Live, state.Stage);
        Assert.False(state.RequiresAuthentication);
    }
}
