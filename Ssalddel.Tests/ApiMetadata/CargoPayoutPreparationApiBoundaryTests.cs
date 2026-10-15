using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Ssalddel.ApiMetadata;
using Ssalddel.Controllers.Driver.Settlement06;
using Ssalddel.Filters;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.ApiMetadata;

public sealed class CargoPayoutPreparationApiBoundaryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 지급준비API는국내운송기능이켜진경우에만실제실행필터를통과한다(bool cargo, bool food)
    {
        var descriptor = Descriptor();
        var context = new ActionExecutingContext(new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor),
            [], new Dictionary<string, object?>(), new object());
        context.HttpContext.Request.Path = "/api/v1/driver/payout-preparations";
        var flags = new VersionFeatureFlagService(new Monitor(new VersionFeatureFlagsOptions
            { CommunityTrustWorkflow = true, DomesticTransportWorkflow = cargo, FoodDeliveryWorkflow = food }));
        var nextCalled = false;
        await new SsalddelApiFeatureBoundaryFilter(flags).OnActionExecutionAsync(context, () =>
        { nextCalled = true; return Task.FromResult(new ActionExecutedContext(context, [], new object())); });

        Assert.Equal(cargo, nextCalled);
        Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow, SsalddelApiFeatureBoundaryFilter.ResolveFeatureKey(descriptor));
        if (cargo) Assert.Null(context.Result);
        else
        {
            var response = Assert.IsType<ObjectResult>(context.Result); Assert.Equal(404, response.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(response.Value);
            Assert.Equal("FeatureDisabled", problem.Extensions["errorCode"]);
            Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow, problem.Extensions["featureKey"]);
        }
    }

    [Fact]
    public void 기존GET경로와기사인증판본은기능경계추가후에도유지한다()
    {
        var type = typeof(기사지급준비Controller); var action = Descriptor().MethodInfo;
        Assert.Equal("api/v1/driver/payout-preparations", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Null(action.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("기사", type.GetCustomAttribute<AuthorizeAttribute>(inherit: true)!.Roles);
        Assert.Contains(type.GetCustomAttributes<SsalddelApiAudienceAttribute>(inherit: true), x => x.Actor == SsalddelActor.Driver);
        Assert.Equal(SsalddelProductVersion.V2_0, type.GetCustomAttribute<SsalddelApiVersionAttribute>()!.Version);
        Assert.All(action.GetParameters().Where(x => x.Name is "year" or "month"), parameter =>
            Assert.NotNull(parameter.GetCustomAttribute<FromQueryAttribute>()));
    }

    private static ControllerActionDescriptor Descriptor()
    {
        var type = typeof(기사지급준비Controller); var action = type.GetMethod(nameof(기사지급준비Controller.월별조회))!;
        return new() { ControllerName = type.Name, ActionName = action.Name, ControllerTypeInfo = type.GetTypeInfo(), MethodInfo = action };
    }
    private sealed class Monitor(VersionFeatureFlagsOptions value) : IOptionsMonitor<VersionFeatureFlagsOptions>
    { public VersionFeatureFlagsOptions CurrentValue => value; public VersionFeatureFlagsOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<VersionFeatureFlagsOptions, string?> listener) => null; }
}
