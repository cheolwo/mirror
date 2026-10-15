using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Ssalddel.ApiMetadata;
using Ssalddel.Controllers.Driver.Profile00;
using Ssalddel.Controllers.Driver.Profile01;
using Ssalddel.Controllers.Driver.Notification07;
using Ssalddel.Controllers.Driver.Recommendation02;
using Ssalddel.Filters;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.ApiMetadata;

public sealed class DriverHomeAndParticipationApiBoundaryR74Tests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 화물기사홈은국내운송기능에만열리고음식기능만으로열리지않는다(bool cargo, bool food)
        => await AssertBoundaryAsync(typeof(기사홈Controller), nameof(기사홈Controller.조회),
            cargo, food, cargo, VersionFeatureFlagKeys.DomesticTransportWorkflow);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 공통희망업무설정의조회저장은음식또는화물기능에열린다(bool cargo, bool food)
    {
        foreach (var action in new[] { nameof(DriverWorkParticipationController.조회), nameof(DriverWorkParticipationController.저장) })
            await AssertBoundaryAsync(typeof(DriverWorkParticipationController), action, cargo, food,
                cargo || food, VersionFeatureFlagKeys.OperationalDispatchCore);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 공통기사알림의조회와설정은음식또는화물기능에열린다(bool cargo, bool food)
    {
        foreach (var action in new[] { nameof(기사알림Controller.조회), nameof(기사알림Controller.등록),
                     nameof(기사알림Controller.삭제), nameof(기사알림Controller.설정조회),
                     nameof(기사알림Controller.설정수정), nameof(기사알림Controller.알림함), nameof(기사알림Controller.읽음처리) })
            await AssertBoundaryAsync(typeof(기사알림Controller), action, cargo, food,
                cargo || food, VersionFeatureFlagKeys.OperationalDispatchCore);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 화물추천의모든조회는음식기능만으로열리지않는다(bool cargo, bool food)
    {
        foreach (var action in new[] { nameof(기사배차추천Controller.조회), nameof(기사배차추천Controller.비운행중조회),
                     nameof(기사배차추천Controller.운행중조회), nameof(기사배차추천Controller.검색), nameof(기사배차추천Controller.전국콜조회) })
            await AssertBoundaryAsync(typeof(기사배차추천Controller), action, cargo, food,
                cargo, VersionFeatureFlagKeys.DomesticTransportWorkflow);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 화물추천상세와공개콜및요약도같은국내운송경계를소비한다(bool cargo, bool food)
    {
        await AssertBoundaryAsync(typeof(기사운송의뢰Controller), nameof(기사운송의뢰Controller.상세조회),
            cargo, food, cargo, VersionFeatureFlagKeys.DomesticTransportWorkflow);
        await AssertBoundaryAsync(typeof(기사공개배차Controller), nameof(기사공개배차Controller.조회),
            cargo, food, cargo, VersionFeatureFlagKeys.DomesticTransportWorkflow);
        await AssertBoundaryAsync(typeof(기사배차추천요약Controller), nameof(기사배차추천요약Controller.요약),
            cargo, food, cargo, VersionFeatureFlagKeys.DomesticTransportWorkflow);
    }

    [Fact]
    public void 기존경로_HTTP동사_기사권한_제품판본을그대로보존한다()
    {
        AssertContract(typeof(기사홈Controller), "api/v1/driver/home");
        AssertContract(typeof(DriverWorkParticipationController), "api/v1/driver/work-participation");
        AssertContract(typeof(기사알림Controller), "api/v1/driver/notifications");
        AssertContract(typeof(기사배차추천Controller), "api/v1/driver/recommendations");
        AssertContract(typeof(기사운송의뢰Controller), "api/v1/driver/requests");
        AssertContract(typeof(기사공개배차Controller), "api/v1/driver/public-dispatches");
        AssertContract(typeof(기사배차추천요약Controller), "api/v1/driver/recommendations");
        Assert.Null(typeof(기사홈Controller).GetMethod(nameof(기사홈Controller.조회))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Null(typeof(DriverWorkParticipationController).GetMethod(nameof(DriverWorkParticipationController.조회))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Null(typeof(DriverWorkParticipationController).GetMethod(nameof(DriverWorkParticipationController.저장))!.GetCustomAttribute<HttpPutAttribute>()!.Template);
        Assert.Equal("settings", typeof(기사알림Controller).GetMethod(nameof(기사알림Controller.설정조회))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("settings", typeof(기사알림Controller).GetMethod(nameof(기사알림Controller.설정수정))!.GetCustomAttribute<HttpPutAttribute>()!.Template);
        Assert.Null(typeof(기사배차추천Controller).GetMethod(nameof(기사배차추천Controller.조회))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("{requestId}", typeof(기사운송의뢰Controller).GetMethod(nameof(기사운송의뢰Controller.상세조회))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Null(typeof(기사공개배차Controller).GetMethod(nameof(기사공개배차Controller.조회))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("summary", typeof(기사배차추천요약Controller).GetMethod(nameof(기사배차추천요약Controller.요약))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    private static void AssertContract(Type type, string route)
    {
        Assert.Equal(route, type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("기사", type.GetCustomAttribute<AuthorizeAttribute>(inherit: true)!.Roles);
        Assert.Equal(SsalddelProductVersion.V2_0, type.GetCustomAttribute<SsalddelApiVersionAttribute>()!.Version);
    }

    private static async Task AssertBoundaryAsync(Type type, string method, bool cargo, bool food, bool expected, string key)
    {
        var action = type.GetMethod(method)!;
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = type.Name, ActionName = method, ControllerTypeInfo = type.GetTypeInfo(), MethodInfo = action
        };
        var context = new ActionExecutingContext(new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor),
            [], new Dictionary<string, object?>(), new object());
        var service = new VersionFeatureFlagService(new Monitor(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = cargo, DomesticTransportWorkflow = cargo, FoodDeliveryWorkflow = food
        }));
        var nextCalled = false;
        await new SsalddelApiFeatureBoundaryFilter(service).OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.Equal(expected, nextCalled);
        Assert.Equal(key, SsalddelApiFeatureBoundaryFilter.ResolveFeatureKey(descriptor));
        if (expected) Assert.Null(context.Result);
        else
        {
            var response = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(response.Value);
            Assert.Equal("FeatureDisabled", problem.Extensions["errorCode"]);
            Assert.Equal(key, problem.Extensions["featureKey"]);
        }
    }

    private sealed class Monitor(VersionFeatureFlagsOptions value) : IOptionsMonitor<VersionFeatureFlagsOptions>
    {
        public VersionFeatureFlagsOptions CurrentValue => value;
        public VersionFeatureFlagsOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<VersionFeatureFlagsOptions, string?> listener) => null;
    }
}
