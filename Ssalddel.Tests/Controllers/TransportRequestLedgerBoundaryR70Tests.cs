using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Ssalddel.ApiMetadata;
using Ssalddel.Controllers.Admin.Evidence04;
using Ssalddel.Controllers.Admin.Inflow02;
using Ssalddel.Controllers.Admin.Settlement05;
using Ssalddel.Controllers.Common;
using Ssalddel.Controllers.Shipper.Payment02;
using Ssalddel.Filters;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Controllers;

public sealed class TransportRequestLedgerBoundaryR70Tests
{
    private static readonly string[] PrepaymentR16Actions =
        [nameof(화주결제Controller.선결제상태조회), nameof(화주결제Controller.페이크결제승인)];
    private static readonly (Type Type, string Method, string Route, string? GetRoute, string Policy)[] Reads =
    [
        (typeof(운송원장Controller), nameof(운송원장Controller.이벤트조회), "api/v1/transport-request-ledgers", "{requestId}/events", "물류운영사용자전용"),
        (typeof(배차대기Controller), nameof(배차대기Controller.목록조회), "api/v1/dispatch/wait", null, "서버관리자전용"),
        (typeof(파일POD관리Controller), nameof(파일POD관리Controller.목록조회), "api/v1/admin/files/pod", null, "서버관리자전용"),
        (typeof(기사월정산관리Controller), nameof(기사월정산관리Controller.목록조회), "api/v1/admin/driver-settlements", null, "서버관리자전용"),
        (typeof(화주결제Controller), nameof(화주결제Controller.결제목록조회), "api/v1/payments", null, "서버관리자전용")
    ];

    public static IEnumerable<object[]> ReadCases()
    {
        foreach (var (type, method, _, _, _) in Reads)
            foreach (var cargo in new[] { false, true })
                foreach (var food in new[] { false, true })
                    yield return [type, method, cargo, food];
    }

    [Theory]
    [MemberData(nameof(ReadCases))]
    public async Task 다섯조회액션은_실제버전실행필터에서_국내운송설정으로만_허용된다(Type type, string method, bool cargo, bool food)
    {
        var (context, executed) = await RunFilter(type, method, cargo, food);
        Assert.Equal(cargo, executed);
        Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow,
            SsalddelApiFeatureBoundaryFilter.ResolveFeatureKey(context.ActionDescriptor));
        if (cargo) Assert.Null(context.Result);
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
    public void 조회전용기능경계는_기존정책과경로를유지하고_컨트롤러전체에전파하지않는다()
    {
        foreach (var (type, method, route, getRoute, policy) in Reads)
        {
            var action = type.GetMethod(method)!;
            var authorization = action.GetCustomAttribute<AuthorizeAttribute>(inherit: true)
                ?? type.GetCustomAttribute<AuthorizeAttribute>(inherit: true);
            Assert.Equal(policy, authorization!.Policy);
            Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
            Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
            Assert.Equal(route, type.GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.Equal(getRoute, action.GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow,
                action.GetCustomAttribute<SsalddelApiFeatureAttribute>()!.FeatureKey);
            Assert.Null(type.GetCustomAttribute<SsalddelApiFeatureAttribute>());
            var version = type.GetCustomAttribute<SsalddelApiVersionAttribute>()!;
            Assert.Equal(SsalddelProductVersion.V2_0, version.Version);
            Assert.Null(version.FeatureKey); Assert.Null(version.WorkflowKey);
        }
    }

    [Fact]
    public async Task 조회만분류한뒤에도_기존미분류쓰기와다른GET은_켜진국내운송설정으로열리지않는다()
    {
        var untouched = Reads.Select(item => item.Type).Distinct()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any()
                    && !Reads.Any(item => item.Type == type && item.Method == method.Name)
                    && !(type == typeof(화주결제Controller) && PrepaymentR16Actions.Contains(method.Name)))
                .Select(method => (Type: type, Method: method.Name))).ToArray();
        Assert.NotEmpty(untouched);
        Assert.Contains(untouched, item => item.Type == typeof(화주결제Controller) && item.Method == nameof(화주결제Controller.공통결제준비));
        Assert.Contains(untouched, item => item.Type == typeof(파일POD관리Controller) && item.Method == nameof(파일POD관리Controller.업로드));
        Assert.Contains(untouched, item => item.Type == typeof(배차대기Controller) && item.Method == nameof(배차대기Controller.생성));
        foreach (var (type, method) in untouched)
        {
            var (context, executed) = await RunFilter(type, method, cargo: true, food: true);
            Assert.False(executed);
            Assert.Null(SsalddelApiFeatureBoundaryFilter.ResolveFeatureKey(context.ActionDescriptor));
            var response = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
            Assert.Equal("FeatureBoundaryUnclassified", Assert.IsType<ProblemDetails>(response.Value).Extensions["errorCode"]);
        }
    }

    [Theory]
    [InlineData(nameof(화주결제Controller.선결제상태조회), false, false)]
    [InlineData(nameof(화주결제Controller.선결제상태조회), false, true)]
    [InlineData(nameof(화주결제Controller.선결제상태조회), true, false)]
    [InlineData(nameof(화주결제Controller.선결제상태조회), true, true)]
    [InlineData(nameof(화주결제Controller.페이크결제승인), false, false)]
    [InlineData(nameof(화주결제Controller.페이크결제승인), false, true)]
    [InlineData(nameof(화주결제Controller.페이크결제승인), true, false)]
    [InlineData(nameof(화주결제Controller.페이크결제승인), true, true)]
    public async Task 선결제조회와모의승인만_명시한운송기능과기존역할권한에결속한다(string method, bool cargo, bool food)
    {
        var action = typeof(화주결제Controller).GetMethod(method)!;
        var auth = action.GetCustomAttribute<AuthorizeAttribute>()!;
        Assert.Contains("화주", auth.Roles); Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
        var (context, executed) = await RunFilter(typeof(화주결제Controller), method, cargo, food);
        Assert.Equal(cargo, executed);
        Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow,
            SsalddelApiFeatureBoundaryFilter.ResolveFeatureKey(context.ActionDescriptor));
        if (!cargo) Assert.Equal("FeatureDisabled", Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(context.Result).Value).Extensions["errorCode"]);
    }

    private static async Task<(ActionExecutingContext Context, bool Executed)> RunFilter(Type type, string method, bool cargo, bool food)
    {
        var action = type.GetMethod(method)!;
        var descriptor = new ControllerActionDescriptor
        { ControllerName = type.Name, ActionName = action.Name, ControllerTypeInfo = type.GetTypeInfo(), MethodInfo = action };
        var context = new ActionExecutingContext(new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor),
            [], new Dictionary<string, object?>(), new object());
        var flags = new VersionFeatureFlagService(new Monitor(new VersionFeatureFlagsOptions
        { DomesticTransportWorkflow = cargo, FoodDeliveryWorkflow = food, CommunityTrustWorkflow = true }));
        var executed = false;
        await new SsalddelApiVersionFeatureFilter(flags).OnActionExecutionAsync(context, () =>
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
