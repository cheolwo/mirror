using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Ssalddel.Controllers.Shipper.Request01;
using Ssalddel.Controllers.Driver.Recommendation02;
using Ssalddel.Controllers.Driver.Action03;

namespace Ssalddel.Tests.ApiMetadata;

public sealed class CargoPublicBrowseAuthorizationTests
{
    [Fact]
    public void 화주공개요약만_익명이고_개인조회와_신청은_인증을유지한다()
    {
        var controller = typeof(화주운송의뢰Controller);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        var anonymous = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<AllowAnonymousAttribute>() is not null).ToArray();
        Assert.Equal(nameof(화주운송의뢰Controller.공개화물요약조회), Assert.Single(anonymous).Name);
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(typeof(기사배차추천Controller))]
    [InlineData(typeof(기사배차액션Controller))]
    public void 기사추천과_배차행동은_역할인증을_유지한다(Type controller)
    {
        var authorization = controller.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorization);
        Assert.False(string.IsNullOrWhiteSpace(authorization.Roles));
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.DoesNotContain(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
    }
}
