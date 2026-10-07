using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodMapWorkspaceNavigationTests
{
    [Fact]
    public void 입력_복귀는_선택동네와_표시항목을_유지하고_새대상만_선택한다()
    {
        var original = NeighborhoodMapNavigation.Href(["offer", "storage"], "map", "11680580", "space", "space-old");
        var returned = NeighborhoodMapNavigation.ReturnToPanel(original, "work", "work-001");
        Assert.Contains("layers=offer%2Cstorage", returned);
        Assert.Contains("region=11680580", returned);
        Assert.Contains("panel=work&target=work-001", returned);
        Assert.DoesNotContain("space-old", returned);
        Assert.DoesNotContain("panel=space", returned);
    }

    [Fact]
    public void 기존_직접입력_라우트는_복귀선택이_없으면_기존_상세이동을_유지한다()
        => Assert.Null(NeighborhoodMapNavigation.ReturnToPanel(null, "work", "work-001"));

    [Fact]
    public void 보관공간_선택은_연결협업을_유지하고_다른대상은_그_문맥을_사용하지않는다()
    {
        var selected = NeighborhoodMapNavigation.RelatedHref("/community/map?layers=storage&view=map", "space-1", "work-1");
        Assert.Contains("panel=space&target=space-1&related=work-1", selected);
        Assert.Contains("related=work-1", NeighborhoodMapNavigation.ReturnToPanel(selected, "space", "space-1"));
        Assert.DoesNotContain("related=", NeighborhoodMapNavigation.PanelHref(selected, "work", "work-2"));
        Assert.DoesNotContain("related=", NeighborhoodMapNavigation.PanelHref(selected, "space", "space-2"));
    }

    [Fact]
    public void 입력_페이지의_기존출처_ID는_복귀주소와_함께_유지한다()
    {
        var url = NeighborhoodMapNavigation.WithReturn("/community/exchange/work/new?postId=42", "/community/map?layers=none&view=list");
        Assert.StartsWith("/community/exchange/work/new?postId=42&returnUrl=", url);
        Assert.Contains(Uri.EscapeDataString("/community/map?layers=none&view=list"), url);
    }

    [Theory]
    [InlineData("https://evil.invalid/community/map")]
    [InlineData("//evil.invalid/community/map")]
    [InlineData("/community/map/../login")]
    [InlineData("/community/map?target=a%2fb")]
    public void 외부또는_위험한_복귀경로는_생활지도_기본값으로_대체한다(string returnHref)
    {
        var url = NeighborhoodMapNavigation.WithReturn("/community/exchange/write", returnHref);
        Assert.EndsWith("returnUrl=%2Fcommunity%2Fmap", url);
    }

    [Theory]
    [InlineData("../../private")]
    [InlineData("주소=서울")]
    [InlineData("contact@example.com")]
    [InlineData("a/b")]
    public void 패널URL은_주소연락처_대신_안전한_식별자만_허용한다(string target)
    {
        Assert.Null(NeighborhoodMapNavigation.PanelTarget(target));
        Assert.DoesNotContain("target=", NeighborhoodMapNavigation.PanelHref("/community/map", "delivery", target));
    }

    [Fact]
    public void 패널전환은_미등록쿼리와_잘못된목적을_복제하지않는다()
    {
        var result = NeighborhoodMapNavigation.PanelHref("/community/map?token=secret&layers=none&view=list", "unregistered", "123");
        Assert.Equal("/community/map?layers=none&view=list", result);
        Assert.Equal("/community/map?layers=offer%2Cneed%2Cmine-active&view=map&panel=spaces",
            NeighborhoodMapNavigation.PanelHref("/community/map", "spaces"));
    }
}
