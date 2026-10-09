using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Community;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OrderLedgerTaskGuidanceRenderTests
{
    [Theory]
    [InlineData("FoodOrder", "/orders/food?orderNo=FOOD%20%2F%201")]
    [InlineData("DistributedOrder", "/workspace/orderer/distributed/orders?selected=FOOD%20%2F%201")]
    public async Task Verified_source_opens_only_existing_internal_work_route(string kind, string route)
    {
        var html = await Render(업무안내(kind), false);
        Assert.Contains(route, html);
        Assert.Contains("담당", html);
        Assert.Contains("data-order-current-task", html);
        Assert.Contains("data-order-condition-guidance", html);
        Assert.DoesNotContain("<details open", html);
        Assert.DoesNotContain("https://", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("UnknownKind")]
    [InlineData("https://example.invalid")]
    public async Task Missing_or_unsupported_source_cannot_create_a_navigation_action(string? kind)
    {
        var guide = kind is null ? null : 업무안내(kind);
        var html = await Render(guide, true);
        Assert.Contains("주문 정보 새로고침", html);
        Assert.Contains("disabled", html);
        Assert.DoesNotContain("href=", html);
    }

    private static 주문원장업무안내Dto 업무안내(string kind) => new()
    {
        현재단계 = "배송 확인", 담당역할 = "기사", 대기이유 = "전달 상태를 확인합니다.",
        업무종류 = kind, 원천Id = "FOOD / 1", 다음행동안내 = "상세 보기"
    };

    private static async Task<string> Render(주문원장업무안내Dto? guide, bool busy)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJs>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var result = await renderer.RenderComponentAsync<주문원장업무안내Panel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(주문원장업무안내Panel.Guide)] = guide,
                [nameof(주문원장업무안내Panel.OrderLedgerId)] = "order/root",
                [nameof(주문원장업무안내Panel.Busy)] = busy
            }));
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/orders/root");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(T)!);
    }
}
