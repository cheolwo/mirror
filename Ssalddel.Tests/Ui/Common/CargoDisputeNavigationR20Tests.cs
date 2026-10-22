using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Commerce;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoDisputeNavigationR20Tests
{
    private const string RequestId = "cargo:r20&part=1";
    private static string From => CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.Shipper, RequestId);

    [Fact]
    public void 화주는_현재의뢰와_선택목록복귀를_신고경로로만전달한다()
    {
        var mapped = ShipperWorkspaceAdapter.Map(new 화주운송의뢰응답 { 의뢰Id = RequestId, 의뢰상태 = "생성됨", 배차상태 = "미시작" });
        var action = Assert.Single(mapped.Actions!, x => x.Key == "issue");

        Assert.Equal("문제 신고", action.Label);
        Assert.False(action.IsPrimary);
        Assert.False(action.RequiresConfirmation);
        Assert.Equal(CommerceAuthenticationRoutes.DisputeRoute(null, "cargo-request", RequestId, From), action.Route);
        var query = Query(action.Route!);
        Assert.Equal("cargo-request", query["sourceKind"]);
        Assert.Equal(RequestId, query["sourceId"]);
        Assert.Equal(From, query["from"]);
        Assert.Contains(mapped.Actions!, x => x.Key == "timeline");
        Assert.Contains(mapped.Actions!, x => x.Key == "payment");
        Assert.Contains(mapped.Actions!, x => x.Key == "proofs");
    }

    [Theory]
    [InlineData(null, "/login")]
    [InlineData("case-r20", "/login")]
    [InlineData(null, "/community/login")]
    [InlineData("case-r20", "/community/login")]
    public void 로그인복귀는_신고대상과_안전한원래업무를_보존한다(string? caseId, string login)
    {
        var route = CommerceAuthenticationRoutes.DisputeRoute(caseId, "cargo-request", RequestId, From);

        Assert.True(CommerceAuthenticationRoutes.TryReturnRoute(route, out var canonical));
        Assert.Equal(route, canonical);
        Assert.Equal(From, Query(canonical)["from"]);
        Assert.Equal(route, Query(CommerceAuthenticationRoutes.LoginHref(login, route))["returnUrl"]);
        Assert.Equal(route, NeighborhoodExchangeAuthenticationRoutes.ReturnRoute(route));
        if (caseId is null) Assert.Equal(RequestId, Query(canonical)["sourceId"]);
        else Assert.StartsWith("/commerce/disputes/case-r20?from=", canonical);
    }

    [Theory]
    [InlineData("https://outside.example/work")]
    [InlineData("//outside.example/work")]
    [InlineData("/\\outside.example/work")]
    [InlineData("/%2foutside.example/work")]
    [InlineData("/%5coutside.example/work")]
    [InlineData("/workspace/ship\nper")]
    public void 외부주소와잘못된복귀값은_인증허용문맥으로받지않는다(string from)
    {
        var direct = "/commerce/disputes?sourceKind=cargo-request&sourceId=r20&from=" + Uri.EscapeDataString(from);
        Assert.False(CommerceAuthenticationRoutes.TryReturnRoute(direct, out _));
        Assert.False(CommerceAuthenticationRoutes.TryReturnRoute("/commerce/disputes/case-r20?from=" + Uri.EscapeDataString(from), out _));
        var safe = CommerceAuthenticationRoutes.DisputeRoute(null, "cargo-request", "r20", from);
        Assert.DoesNotContain("from=", safe);
        Assert.Equal("/commerce/disputes?sourceKind=cargo-request&sourceId=r20", safe);
    }

    [Theory]
    [InlineData("token=test-only")]
    [InlineData("password=test-only")]
    [InlineData("returnUrl=%2Fworkspace%2Fshipper")]
    [InlineData("sourceId=another")]
    [InlineData("from=%2Fworkspace%2Fshipper&from=%2F")]
    public void 허용하지않은쿼리와_중복값은_새복귀지원으로통과하지않는다(string extra)
    {
        Assert.False(CommerceAuthenticationRoutes.TryReturnRoute(
            "/commerce/disputes?sourceKind=cargo-request&sourceId=r20&" + extra, out _));
        Assert.False(CommerceAuthenticationRoutes.TryReturnRoute("/commerce/privacy/case-r20?from=%2Fworkspace%2Fshipper", out _));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/mobile/")]
    public async Task 신고목록의_원래의뢰와_사건링크는_앱기준경로와_복귀문맥을유지한다(string basePath)
    {
        var result = await RenderAsync(true, From, null, basePath);
        Assert.Contains("href=\"http://localhost" + basePath + From.TrimStart('/') + "\"", result.Html);
        var detail = CommerceAuthenticationRoutes.DisputeRoute("case-r20", returnPath: From);
        Assert.Contains("href=\"http://localhost" + basePath + detail.TrimStart('/') + "\"", result.Html);
        Assert.Contains("의뢰로 돌아가기", result.Html);
        Assert.Equal(0, result.Client.DisputeCalls);
        Assert.Equal(0, result.Client.Commands);
    }

    [Fact]
    public async Task 익명신고화면은_원래의뢰복귀와_로그인경로만보여주고_자동접수하지않는다()
    {
        var result = await RenderAsync(false, From);
        var login = CommerceAuthenticationRoutes.LoginHref("/community/login",
            CommerceAuthenticationRoutes.DisputeRoute(null, "cargo-request", RequestId, From));
        Assert.Contains("href=\"" + login + "\"", result.Html);
        Assert.Contains("의뢰로 돌아가기", result.Html);
        Assert.DoesNotContain("<textarea", result.Html);
        Assert.Equal(0, result.Client.PrivateReads);
        Assert.Equal(0, result.Client.DisputeCalls);
        Assert.Equal(0, result.Client.Commands);
    }

    [Fact]
    public async Task 사건상세의_목록복귀와_원래의뢰복귀는_독립된링크다()
    {
        var result = await RenderAsync(true, From, "case-r20");
        var list = CommerceAuthenticationRoutes.DisputeRoute(null, "cargo-request", RequestId, From);
        Assert.Contains("href=\"http://localhost" + list + "\"", result.Html);
        Assert.Contains("href=\"http://localhost" + From + "\"", result.Html);
        Assert.Contains("내 요청 목록으로", result.Html);
        Assert.Contains("의뢰로 돌아가기", result.Html);
        Assert.Equal(0, result.Client.DisputeCalls);
        Assert.Equal(0, result.Client.Commands);
    }

    [Fact]
    public async Task 외부복귀주소는_공통신고화면의_링크가되지않는다()
    {
        var result = await RenderAsync(true, "//outside.example/work");
        Assert.DoesNotContain("outside.example", result.Html);
        Assert.DoesNotContain("aria-label=\"이전 업무\"", result.Html);
        Assert.Equal(0, result.Client.DisputeCalls);
    }

    private static Dictionary<string, string> Query(string path) => path.Split('?', 2)[1].Split('&')
        .Select(pair => pair.Split('=', 2)).ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));

    private static async Task<(string Html, CommerceUiFixture Client)> RenderAsync(bool authenticated, string? from,
        string? caseId = null, string basePath = "/")
    {
        var client = new CommerceUiFixture
        {
            List = () => Task.FromResult(new 보호지원ListResponse { Items = [new() { CaseId = "case-r20", StatusCode = "received" }] }),
            Detail = id => Task.FromResult<보호지원CaseResponse?>(new() { CaseId = id, SourceKind = "cargo-request", StatusCode = "received" })
        };
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<I통신판매보호Client>(client); services.AddCommerceProtectionUi();
        services.AddSingleton<ISsalddel현재사용자Context>(new User(authenticated));
        services.AddSingleton<IJSRuntime, NoopJs>(); services.AddSingleton<NavigationManager>(new Navigation(basePath));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var page = await renderer.RenderComponentAsync<보호지원Page>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(보호지원Page.SourceKind)] = "cargo-request", [nameof(보호지원Page.SourceId)] = RequestId,
                [nameof(보호지원Page.CaseId)] = caseId, [nameof(보호지원Page.ReturnPath)] = from,
                [nameof(보호지원Page.LoginHref)] = CommerceAuthenticationRoutes.LoginHref("/community/login",
                    CommerceAuthenticationRoutes.DisputeRoute(caseId, "cargo-request", RequestId, from))
            }));
            return WebUtility.HtmlDecode(page.ToHtmlString());
        });
        return (html, client);
    }

    private sealed class User(bool authenticated) : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot 현재사용자 => authenticated ? new("r20-owner", "시험 사용자", []) : 현재사용자Snapshot.익명;
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation(string basePath) => Initialize("http://localhost" + basePath, "http://localhost" + basePath + "commerce/disputes");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
