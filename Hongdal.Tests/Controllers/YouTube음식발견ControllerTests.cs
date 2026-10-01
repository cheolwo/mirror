using System.Reflection;
using Hongdal.Controllers.Admin.Content07;
using Hongdal.Controllers.Orderer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Tests.Controllers;

public sealed class YouTube음식발견ControllerTests
{
    [Fact]
    public void 관리자상품후보API는_서버관리자전용이다()
    {
        var authorize = typeof(YouTube음식상품관리Controller).GetCustomAttribute<AuthorizeAttribute>();
        var route = typeof(YouTube음식상품관리Controller).GetCustomAttribute<RouteAttribute>();

        Assert.Equal("서버관리자전용", authorize?.Policy);
        Assert.Equal("api/v1/admin/content/youtube-food", route?.Template);
        Assert.Equal(
            "product-candidates/{candidateId:long}/review",
            typeof(YouTube음식상품관리Controller)
                .GetMethod(nameof(YouTube음식상품관리Controller.상품후보검수))
                ?.GetCustomAttribute<HttpPutAttribute>()
                ?.Template);
        Assert.Equal(
            "videos/{videoId}/ingredient-recognition",
            typeof(YouTube음식상품관리Controller)
                .GetMethod(nameof(YouTube음식상품관리Controller.영상재료자동인지))
                ?.GetCustomAttribute<HttpPostAttribute>()
                ?.Template);
        Assert.Equal(
            "videos/{videoId}/transcript",
            typeof(YouTube음식상품관리Controller)
                .GetMethod(nameof(YouTube음식상품관리Controller.영상자막조회))
                ?.GetCustomAttribute<HttpPostAttribute>()
                ?.Template);
    }

    [Fact]
    public void 주문자음식발견API는_인증과구매의향경로를요구한다()
    {
        var authorize = typeof(YouTube음식발견Controller).GetCustomAttribute<AuthorizeAttribute>();
        var route = typeof(YouTube음식발견Controller).GetCustomAttribute<RouteAttribute>();
        var intentAction = typeof(YouTube음식발견Controller)
            .GetMethod(nameof(YouTube음식발견Controller.구매의향등록));

        Assert.NotNull(authorize);
        Assert.Equal("api/v1/orderer/youtube-food-discovery", route?.Template);
        Assert.Equal(
            "products/{candidateId:long}/intents",
            intentAction?.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Null(intentAction?.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void 주문자음식발견API는_국가집계경로를제공한다()
    {
        var countriesAction = typeof(YouTube음식발견Controller)
            .GetMethod(nameof(YouTube음식발견Controller.음식채널국가집계));

        Assert.Equal(
            "countries",
            countriesAction?.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Fact]
    public void 승인된음식영상목록만_익명공개한다()
    {
        var productsAction = typeof(YouTube음식발견Controller)
            .GetMethod(nameof(YouTube음식발견Controller.공개상품후보목록));
        var channelsAction = typeof(YouTube음식발견Controller)
            .GetMethod(nameof(YouTube음식발견Controller.음식채널목록));

        Assert.NotNull(productsAction?.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(channelsAction?.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void 음식채널검색과프로필설정은_기존관리자API에포함된다()
    {
        var searchAction = typeof(YouTube채널감시Controller)
            .GetMethod(nameof(YouTube채널감시Controller.음식채널검색));
        var profileAction = typeof(YouTube채널감시Controller)
            .GetMethod(nameof(YouTube채널감시Controller.음식채널프로필설정));

        Assert.Equal("channels/search", searchAction?.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal(
            "channels/{channelId}/food-profile",
            profileAction?.GetCustomAttribute<HttpPutAttribute>()?.Template);
    }
}
