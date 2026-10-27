using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Ssalddel.ApiMetadata;
using Ssalddel.Controllers.Driver.Profile01;
using Ssalddel.Filters;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.ApiMetadata;

public sealed class TransportProfileBoundaryR72Tests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 본인화물기사프로필은_국내운송기능에만열리고_음식기능만으로열리지않는다(bool cargo, bool food)
    {
        var (context, executed) = await RunFilter(nameof(용달기사Controller.내용달기사조회), cargo, food);

        Assert.Equal(cargo, executed);
        Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow,
            SsalddelApiFeatureBoundaryFilter.ResolveFeatureKey(context.ActionDescriptor));
        if (cargo)
        {
            Assert.Null(context.Result);
        }
        else
        {
            var response = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(response.Value);
            Assert.Equal("FeatureDisabled", problem.Extensions["errorCode"]);
            Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow, problem.Extensions["featureKey"]);
        }
    }

    [Fact]
    public void 본인조회_GET경로와기존로그인요건_제품판본을보존하고_클래스전체를열지않는다()
    {
        var type = typeof(용달기사Controller);
        var action = type.GetMethod(nameof(용달기사Controller.내용달기사조회))!;

        Assert.Equal("api/v1/drivers", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("me", action.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>(inherit: true));
        Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        var version = type.GetCustomAttribute<SsalddelApiVersionAttribute>()!;
        Assert.Equal(SsalddelProductVersion.V2_0, version.Version);
        Assert.Null(version.FeatureKey);
        Assert.Null(type.GetCustomAttribute<SsalddelApiFeatureAttribute>());
        Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow,
            action.GetCustomAttribute<SsalddelApiFeatureAttribute>()!.FeatureKey);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 조회분류를추가해도_기사등록POST는_기존미분류경계에남는다(bool cargo, bool food)
    {
        var action = typeof(용달기사Controller).GetMethod(nameof(용달기사Controller.용달기사등록))!;
        Assert.Equal("register", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Null(action.GetCustomAttribute<SsalddelApiFeatureAttribute>());

        var (context, executed) = await RunFilter(action.Name, cargo, food);

        Assert.False(executed);
        Assert.Null(SsalddelApiFeatureBoundaryFilter.ResolveFeatureKey(context.ActionDescriptor));
        var response = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("FeatureBoundaryUnclassified",
            Assert.IsType<ProblemDetails>(response.Value).Extensions["errorCode"]);
    }

    private static async Task<(ActionExecutingContext Context, bool Executed)> RunFilter(string method, bool cargo, bool food)
    {
        var type = typeof(용달기사Controller);
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = type.Name, ActionName = method,
            ControllerTypeInfo = type.GetTypeInfo(), MethodInfo = type.GetMethod(method)!
        };
        var context = new ActionExecutingContext(new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor),
            [], new Dictionary<string, object?>(), new object());
        var flags = new VersionFeatureFlagService(new Monitor(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = true, DomesticTransportWorkflow = cargo, FoodDeliveryWorkflow = food
        }));
        var executed = false;
        await new SsalddelApiFeatureBoundaryFilter(flags).OnActionExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });
        return (context, executed);
    }

    private sealed class Monitor(VersionFeatureFlagsOptions value) : IOptionsMonitor<VersionFeatureFlagsOptions>
    {
        public VersionFeatureFlagsOptions CurrentValue => value;
        public VersionFeatureFlagsOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<VersionFeatureFlagsOptions, string?> listener) => null;
    }
}
