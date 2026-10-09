using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleWorkspaceActionIconRenderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("future-unregistered-action")]
    [InlineData("foodorder.confirmreceipt")]
    public async Task UnknownKeyShowsOnlyExistingLabel_WithoutInferringFromItsWords(string? key)
    {
        var html = await RenderAsync<RoleWorkspaceActionContent>(new()
        {
            [nameof(RoleWorkspaceActionContent.ActionKey)] = key,
            [nameof(RoleWorkspaceActionContent.Label)] = "음식 수령 확인"
        });

        Assert.Contains("음식 수령 확인", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("<svg", html);
    }

    [Fact]
    public async Task KnownKeyAddsDecorativeInheritedColorIcon_AndKeepsEncodedRecheckLabel()
    {
        const string label = "같은 요청 다시 확인 <strong>중단</strong> & 확인";
        var html = await RenderAsync<RoleWorkspaceActionContent>(new()
        {
            [nameof(RoleWorkspaceActionContent.ActionKey)] = 음식배달가능행동Ids.기사배달중단,
            [nameof(RoleWorkspaceActionContent.Label)] = label
        });

        Assert.Contains("<svg", html);
        Assert.Contains("aria-hidden=\"true\"", html);
        Assert.Contains("color:currentColor", html);
        Assert.Contains(label, WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("<strong>", html);
        Assert.Contains("&lt;strong&gt;", html);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DetailIconKeepsRouteOrDisabledButton_OriginalLabelAndReason(bool enabled, bool busy)
    {
        var action = new RoleWorkspaceAction(음식배달가능행동Ids.음식점주문수락,
            "주문 수락", "/existing-input", IsPrimary: true, Enabled: enabled,
            DisabledReason: "현재 상태를 먼저 확인해 주세요.", RequiresConfirmation: true);
        var original = action with { };
        var requested = 0;
        var html = await RenderAsync<FoodRoleDetailPanel>(new()
        {
            [nameof(FoodRoleDetailPanel.RoleKey)] = "restaurant",
            [nameof(FoodRoleDetailPanel.Item)] = new RoleWorkspaceItem("order-a", "선택 주문", "수락 전", Actions: [action]),
            [nameof(FoodRoleDetailPanel.IsBusy)] = busy,
            [nameof(FoodRoleDetailPanel.ActionRequested)] = EventCallback.Factory.Create<RoleWorkspaceAction>(this,
                (RoleWorkspaceAction _) => requested++)
        });

        Assert.Contains("주문 수락", WebUtility.HtmlDecode(html));
        Assert.Contains("<svg", html);
        Assert.Contains("aria-hidden=\"true\"", html);
        Assert.Equal(original, action);
        Assert.Equal(0, requested);
        if (enabled && !busy)
        {
            Assert.Contains("href=\"/existing-input\"", html);
            Assert.DoesNotContain("<button", html);
        }
        else
        {
            Assert.DoesNotContain("href=\"/existing-input\"", html);
            Assert.Matches("<button\\b[^>]*\\bdisabled\\b", html);
        }
        if (!enabled)
            Assert.Contains(action.DisabledReason!, WebUtility.HtmlDecode(html));
        else
            Assert.DoesNotContain(action.DisabledReason!, WebUtility.HtmlDecode(html));
    }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoBrowserJs>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }

    private sealed class NoBrowserJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new InvalidOperationException("행동 내용의 정적 렌더는 브라우저를 실행하지 않습니다.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
