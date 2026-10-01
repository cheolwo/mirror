using System.Reflection;
using Hongdal.Controllers.Admin.Content07;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Tests.Controllers;

public sealed class YouTube관리ControllerTests
{
    [Fact]
    public void 홍익학당자료API는_서버관리자전용이다()
    {
        var authorize = typeof(YouTube채널감시Controller).GetCustomAttribute<AuthorizeAttribute>();
        var route = typeof(YouTube채널감시Controller).GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("서버관리자전용", authorize.Policy);
        Assert.Equal("api/v1/admin/content/youtube", route?.Template);
    }

    [Fact]
    public void 재생목록과영상조회도_관리자경로에포함된다()
    {
        var listAction = typeof(YouTube채널감시Controller)
            .GetMethod(nameof(YouTube채널감시Controller.재생목록조회), BindingFlags.Instance | BindingFlags.Public);
        var videosAction = typeof(YouTube채널감시Controller)
            .GetMethod(nameof(YouTube채널감시Controller.재생목록영상조회), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(listAction);
        Assert.Equal("playlists", listAction.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Null(listAction.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(videosAction);
        Assert.Equal("playlists/{playlistId}/videos", videosAction.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Null(videosAction.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void 영상승인상태변경은_관리자쓰기API다()
    {
        var action = typeof(YouTube채널감시Controller)
            .GetMethod(nameof(YouTube채널감시Controller.영상공개설정), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(action);
        Assert.Equal(
            "videos/{videoId}/publication",
            action.GetCustomAttribute<HttpPutAttribute>()?.Template);
    }

    [Fact]
    public void 카드반야게시승인은_별도관리자쓰기API다()
    {
        var authorize = typeof(HongikHakdangCardController).GetCustomAttribute<AuthorizeAttribute>();
        var action = typeof(HongikHakdangCardController)
            .GetMethod(
                nameof(HongikHakdangCardController.SetCardCommunityPublication),
                BindingFlags.Instance | BindingFlags.Public);

        Assert.Equal("서버관리자전용", authorize?.Policy);
        Assert.NotNull(action);
        Assert.Equal(
            "{cardId:long}/community-publication",
            action.GetCustomAttribute<HttpPutAttribute>()?.Template);
    }
}
