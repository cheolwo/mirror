using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Food;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Controllers.Admin.Food;
using Ssalddel.Filters;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Controllers;

public sealed class 음식주문운영추적ControllerTests
{
    [Fact]
    public void 관리자전용V3음식주문상관관계조회경로를유지한다()
    {
        var type = typeof(음식주문운영추적Controller);
        var method = type.GetMethod(nameof(음식주문운영추적Controller.조회));

        Assert.Equal("서버관리자전용", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("api/v1/admin/food-orders", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal(SsalddelProductVersion.V3_0, type.GetCustomAttribute<SsalddelApiVersionAttribute>()?.Version);
        Assert.Equal(
            VersionFeatureFlagKeys.FoodDeliveryWorkflow,
            type.GetCustomAttribute<SsalddelApiVersionAttribute>()?.FeatureKey);
        Assert.Contains(
            VersionFeatureFlagKeys.FoodDeliveryWorkflow,
            type.GetCustomAttribute<RequireVersionFeatureAttribute>()?.Arguments ?? []);
        Assert.Equal(
            "{orderNo}/operations-trace",
            method?.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Fact]
    public async Task 존재하지않는주문은404를반환한다()
    {
        var controller = new 음식주문운영추적Controller(new StubUseCase(null));

        var result = await controller.조회("FOOD-MISSING", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task 주문번호공백은400문제로반환한다()
    {
        var controller = new 음식주문운영추적Controller(new ThrowingUseCase());

        var result = await controller.조회(" ", CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    private sealed record StubUseCase(음식주문운영추적응답? Response)
        : I음식주문운영추적UseCase
    {
        public Task<음식주문운영추적응답?> 조회Async(
            string 주문번호,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Response);
    }

    private sealed class ThrowingUseCase : I음식주문운영추적UseCase
    {
        public Task<음식주문운영추적응답?> 조회Async(
            string 주문번호,
            CancellationToken cancellationToken = default)
            => throw new ArgumentException("주문번호가 필요합니다.", nameof(주문번호));
    }
}
