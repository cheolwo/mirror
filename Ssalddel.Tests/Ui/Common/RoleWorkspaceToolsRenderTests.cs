using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleWorkspaceToolsRenderTests
{
    [Fact]
    public async Task 강조된_운송조회도_기본접힌_도구에_모이고_선택복귀를_보존한다()
    {
        var adapter = new Adapter("shipper", [new("timeline", "운송 내역", "/shipper/request/work-a/timeline", IsPrimary: true),
            new("payment", "결제·정산 확인", "/shipper/request/work-a/payment"),
            new("proofs", "인수증·증빙", "/shipper/request/work-a/proofs")]);
        var html = await RenderAsync(adapter);
        var tools = html[html.IndexOf("role-map-workspace__tools", StringComparison.Ordinal)..];
        Assert.DoesNotContain("운송 내역", html[..html.IndexOf("role-map-workspace__tools", StringComparison.Ordinal)]);
        Assert.Contains("<summary", tools);
        Assert.Contains(">업무 도구</summary>", tools);
        Assert.DoesNotMatch("role-map-workspace__tools[^>]*\\bopen(?:=|[ >])", html);
        Assert.Contains("인수증·증빙", tools);
        Assert.Contains("returnUrl=%2Fworkspace%2Fshipper%3Fselected%3Dwork-a", tools);
        Assert.Equal(0, adapter.PerformCalls);
    }

    [Fact]
    public async Task 비강조_현재절차와_문제신고는_상세를_접어도_바로_보이고_서버금지를_유지한다()
    {
        var adapter = new Adapter("cargo-driver", [new("pickup", "상차 완료", "/pickup", Enabled: false, DisabledReason: "필수 증빙을 확인해 주세요."),
            new("issue", "문제 신고", "/issue", IsPrimary: false)]);
        var html = await RenderAsync(adapter);
        var beforeTools = html[..html.IndexOf("role-map-workspace__tools", StringComparison.Ordinal)];
        Assert.Contains("상차 완료</button>", beforeTools);
        Assert.Matches("<button[^>]*disabled[^>]*>상차 완료</button>", beforeTools);
        Assert.Contains("필수 증빙을 확인해 주세요.", beforeTools);
        Assert.Contains("문제 신고</a>", beforeTools);
        Assert.DoesNotContain("href=\"http://localhost/pickup", html);
        Assert.Equal(0, adapter.PerformCalls);
    }

    [Theory]
    [InlineData("orderer", "food.new-order", "음식 주문", "/workspace/orderer/new")]
    [InlineData("shipper", "create", "운송 의뢰 등록", "/shipper/request")]
    public async Task 현재업무가_없으면_새작업을_메뉴에_숨기지_않는다(string role, string key, string label, string route)
    {
        var adapter = new Adapter(role, [], empty: true, [new(key, label, route, IsPrimary: true)]);
        var html = await RenderAsync(adapter);
        Assert.Contains("시작할 작업", html);
        Assert.Contains(label + "</a>", html);
        Assert.DoesNotContain("role-map-workspace__tools", html);
        Assert.Equal(0, adapter.PerformCalls);
    }

    [Fact]
    public async Task 종료된_선택내역만_있어도_새작업_진입은_바로_보인다()
    {
        var adapter = new Adapter("orderer", [], emptyActions: [new("food.new-order", "음식 주문", "/workspace/orderer/new")], current: false);
        var html = await RenderAsync(adapter);
        Assert.Contains("시작할 작업", html);
        Assert.Contains("음식 주문</a>", html);
        Assert.DoesNotContain("role-map-workspace__tools", html);
        Assert.Equal(0, adapter.PerformCalls);
    }

    [Fact]
    public async Task 미분류_추가행동은_기존그룹과_강조를_보존한다()
    {
        var adapter = new Adapter("restaurant", [], emptyActions: [new("future-action", "기존 추가 작업", "/existing/tool", IsPrimary: true)]);
        var html = await RenderAsync(adapter);
        Assert.Contains("추가 작업</summary>", html);
        Assert.Contains("class=\"is-primary\"", html);
        Assert.Contains("기존 추가 작업</a>", html);
        Assert.DoesNotContain("role-map-workspace__tools", html);
        Assert.Equal(0, adapter.PerformCalls);
    }

    [Fact]
    public async Task 선택조회의_외부경로는_메뉴에서도_실행링크가_되지_않는다()
    {
        var adapter = new Adapter("shipper", [new("timeline", "운송 내역", "https://example.invalid/untrusted")]);
        var html = await RenderAsync(adapter);
        Assert.DoesNotContain("example.invalid", html);
        Assert.Matches("<button[^>]*disabled[^>]*>운송 내역</button>", html);
        Assert.Equal(0, adapter.PerformCalls);
    }

    private static async Task<string> RenderAsync(Adapter adapter)
    {
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new RoleWorkspaceState());
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices(); services.AddSingleton(model);
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton<INeighborhoodMapHost, MapHost>();
        services.AddSingleton<IJSRuntime, StaticRenderJs>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<RoleMapWorkspace>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(RoleMapWorkspace.RoleKey)] = adapter.RoleKey }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class Adapter(string role, IReadOnlyList<RoleWorkspaceAction> actions, bool empty = false,
        IReadOnlyList<RoleWorkspaceAction>? emptyActions = null, bool current = true) : IRoleWorkspaceAdapter
    {
        public string RoleKey => role;
        public int PerformCalls { get; private set; }
        public Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
            => Task.FromResult(new RoleWorkspaceSnapshot(role,
                empty ? [] : [new("work-a", "현재 업무", "진행 중", Actions: actions, IsCurrent: current)],
                SelectedId: empty ? null : "work-a", EmptyActions: emptyActions));
        public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
        { PerformCalls++; throw new InvalidOperationException("조회 렌더는 업무를 실행하지 않습니다."); }
        public void Clear() { }
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("owner-a", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class StaticRenderJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class MapHost : INeighborhoodMapHost
    {
        public Task<NeighborhoodMapHostStatus> RenderAsync(string id, NeighborhoodMapRenderState state, Func<string, Task> selected, CancellationToken cancellationToken = default)
            => Task.FromResult(new NeighborhoodMapHostStatus("ready"));
        public Task HideAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/workspace/shipper");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
