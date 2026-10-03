using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Ui.Common.Areas.App.Components.Orderer;

namespace Ssalddel.Tests.Ui.Common;

public sealed class GroupPurchaseAuthenticationFrameTests
{
    [Fact]
    public async Task 인증표시는_업무탐색과경계설명을숨기고_로그인본문만렌더한다()
    {
        var html = await RenderAsync(authenticationOnly: true);

        Assert.Contains("수요 원장 로그인", html);
        Assert.Contains("합성 인증 본문", html);
        Assert.DoesNotContain("group-purchase-screen__actions", html);
        Assert.DoesNotContain("group-purchase-screen__boundary", html);
        Assert.DoesNotContain("이전 화면", html);
        Assert.DoesNotContain("공동구매 개요", html);
    }

    [Fact]
    public async Task 기본표시는_기존업무복귀와경계설명을보존한다()
    {
        var html = await RenderAsync(authenticationOnly: null);

        Assert.Contains("group-purchase-screen__actions", html);
        Assert.Contains("group-purchase-screen__boundary", html);
        Assert.Contains("이전 화면", html);
        Assert.Contains("공동구매 개요", html);
        Assert.Contains("비구속 수요와 공개 모집", html);
        Assert.Contains("합성 인증 본문", html);
    }

    private static async Task<string> RenderAsync(bool? authenticationOnly)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJsRuntime>();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object?>
            {
                [nameof(GroupPurchaseScreenFrame.CurrentScreen)] = GroupPurchaseScreenKind.DemandCreate,
                [nameof(GroupPurchaseScreenFrame.Eyebrow)] = "LOGIN",
                [nameof(GroupPurchaseScreenFrame.Title)] = "수요 원장 로그인",
                [nameof(GroupPurchaseScreenFrame.Description)] = "합성 인증 검증",
                [nameof(GroupPurchaseScreenFrame.BackHref)] = GroupPurchasePageRoutes.ProductsRoot,
                [nameof(GroupPurchaseScreenFrame.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, "합성 인증 본문"))
            };
            if (authenticationOnly.HasValue)
                parameters[nameof(GroupPurchaseScreenFrame.AuthenticationOnly)] = authenticationOnly.Value;
            var rendered = await renderer.RenderComponentAsync<GroupPurchaseScreenFrame>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(rendered.ToHtmlString());
        });
    }

    private sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/group-purchase/demands/new/synthetic");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
