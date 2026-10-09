using System.Net;
using System.Text.RegularExpressions;
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
        foreach (var (label, route) in new[] { ("운송 내역", "timeline"), ("결제·정산 확인", "payment"), ("인수증·증빙", "proofs") })
            Assert.Equal($"http://localhost/shipper/request/work-a/{route}?returnUrl=%2Fworkspace%2Fshipper%3Fselected%3Dwork-a",
                ControlAttribute(ControlWithText(tools, "a", label), "href"));
        Assert.Equal(0, adapter.PerformCalls);
    }

    [Fact]
    public async Task 비강조_현재절차와_문제신고는_상세를_접어도_바로_보이고_서버금지를_유지한다()
    {
        var adapter = new Adapter("cargo-driver", [new("pickup", "상차 완료", "/pickup", Enabled: false, DisabledReason: "필수 증빙을 확인해 주세요."),
            new("issue", "문제 신고", "/issue", IsPrimary: false)]);
        var html = await RenderAsync(adapter);
        var beforeTools = html[..html.IndexOf("role-map-workspace__tools", StringComparison.Ordinal)];
        var pickup = ControlWithText(beforeTools, "button", "상차 완료");
        Assert.NotNull(ControlAttribute(pickup, "disabled"));
        Assert.Null(ControlAttribute(pickup, "href"));
        Assert.Empty(Controls(html, "a").Where(control => ControlText(control) == "상차 완료"));
        Assert.Contains("필수 증빙을 확인해 주세요.", beforeTools);
        Assert.Equal("http://localhost/issue?returnUrl=%2Fworkspace%2Fcargo-driver%3Fselected%3Dwork-a",
            ControlAttribute(ControlWithText(beforeTools, "a", "문제 신고"), "href"));
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
        var directActions = NavigationWithLabel(html, "역할 업무 설정");
        Assert.Equal($"http://localhost{route}?returnUrl=%2Fworkspace%2F{role}",
            ControlAttribute(ControlWithText(directActions, "a", label), "href"));
        Assert.Equal("is-primary", ControlAttribute(ControlWithText(directActions, "a", label), "class"));
        var tools = NavigationWithLabel(html, "업무 도구");
        Assert.Empty(Controls(tools, "a").Where(control => ControlText(control) == label));
        Assert.DoesNotMatch("role-map-workspace__tools[^>]*\\bopen(?:=|[ >])", html);
        if (role == "shipper")
        {
            Assert.Equal(new[] { "합의 요금표", "분산 보관·배송 업무" }, Controls(tools, "a").Select(ControlText).ToArray());
            Assert.Equal("http://localhost/workspace/shipper/agreed-rates?returnUrl=%2Fworkspace%2Fshipper",
                ControlAttribute(ControlWithText(tools, "a", "합의 요금표"), "href"));
        }
        else Assert.Equal(new[] { "분산 보관·배송 업무" }, Controls(tools, "a").Select(ControlText).ToArray());
        var distributedScreen = role == "shipper" ? "inventory" : "orders";
        Assert.Equal($"http://localhost/workspace/{role}/distributed/{distributedScreen}?returnUrl=%2Fworkspace%2F{role}",
            ControlAttribute(ControlWithText(tools, "a", "분산 보관·배송 업무"), "href"));
        Assert.Equal(0, adapter.PerformCalls);
    }

    [Fact]
    public async Task 종료된_선택내역만_있어도_새작업_진입은_바로_보인다()
    {
        var adapter = new Adapter("orderer", [], emptyActions: [new("food.new-order", "음식 주문", "/workspace/orderer/new")], current: false);
        var html = await RenderAsync(adapter);
        Assert.Contains("시작할 작업", html);
        var directActions = NavigationWithLabel(html, "역할 업무 설정");
        Assert.Equal("http://localhost/workspace/orderer/new?returnUrl=%2Fworkspace%2Forderer%3Fselected%3Dwork-a",
            ControlAttribute(ControlWithText(directActions, "a", "음식 주문"), "href"));
        var tools = NavigationWithLabel(html, "업무 도구");
        Assert.Equal(new[] { "분산 보관·배송 업무" }, Controls(tools, "a").Select(ControlText).ToArray());
        Assert.Empty(Controls(tools, "a").Where(control => ControlText(control) == "음식 주문"));
        Assert.DoesNotMatch("role-map-workspace__tools[^>]*\\bopen(?:=|[ >])", html);
        Assert.Equal("http://localhost/workspace/orderer/distributed/orders?returnUrl=%2Fworkspace%2Forderer%3Fselected%3Dwork-a",
            ControlAttribute(ControlWithText(tools, "a", "분산 보관·배송 업무"), "href"));
        Assert.Equal(0, adapter.PerformCalls);
    }

    [Fact]
    public async Task 미분류_추가행동은_기존그룹과_강조를_보존한다()
    {
        var adapter = new Adapter("restaurant", [], emptyActions: [new("future-action", "기존 추가 작업", "/existing/tool", IsPrimary: true)]);
        var html = await RenderAsync(adapter);
        Assert.Contains("추가 작업</summary>", html);
        Assert.Contains("class=\"is-primary\"", html);
        var additional = ControlWithText(NavigationWithLabel(html, "추가 작업"), "a", "기존 추가 작업");
        Assert.Equal("http://localhost/existing/tool?returnUrl=%2Fworkspace%2Frestaurant%3Fselected%3Dwork-a",
            ControlAttribute(additional, "href"));
        Assert.Equal("is-primary", ControlAttribute(additional, "class"));
        var tools = NavigationWithLabel(html, "업무 도구");
        Assert.Equal(new[] { "합의 요금표", "이 업무의 합의 요금표" }, Controls(tools, "a").Select(ControlText).ToArray());
        Assert.Equal("http://localhost/workspace/restaurant/agreed-rates?returnUrl=%2Fworkspace%2Frestaurant%3Fselected%3Dwork-a",
            ControlAttribute(ControlWithText(tools, "a", "합의 요금표"), "href"));
        Assert.Equal("http://localhost/workspace/restaurant/agreed-rates?workId=work-a&returnUrl=%2Fworkspace%2Frestaurant%3Fselected%3Dwork-a",
            ControlAttribute(ControlWithText(tools, "a", "이 업무의 합의 요금표"), "href"));
        Assert.Equal(0, adapter.PerformCalls);
    }

    [Fact]
    public async Task 선택조회의_외부경로는_메뉴에서도_실행링크가_되지_않는다()
    {
        var adapter = new Adapter("shipper", [new("timeline", "운송 내역", "https://example.invalid/untrusted")]);
        var html = await RenderAsync(adapter);
        Assert.DoesNotContain("example.invalid", html);
        var tools = html[html.IndexOf("role-map-workspace__tools", StringComparison.Ordinal)..];
        var timeline = ControlWithText(tools, "button", "운송 내역");
        Assert.NotNull(ControlAttribute(timeline, "disabled"));
        Assert.Null(ControlAttribute(timeline, "href"));
        Assert.Empty(Controls(html, "a").Where(control => ControlText(control) == "운송 내역"));
        Assert.Equal(0, adapter.PerformCalls);
    }

    private static string NavigationWithLabel(string html, string label)
        => Assert.Single(Regex.Matches(html,
                $"<nav\\b[^>]*\\baria-label=\"{Regex.Escape(label)}\"[^>]*>(?<content>.*?)</nav>", RegexOptions.Singleline)
            .Cast<Match>()).Groups["content"].Value;

    private static IEnumerable<Match> Controls(string html, string tag)
        => Regex.Matches(html, $"<{tag}\\b(?<attributes>[^>]*)>(?<content>.*?)</{tag}>", RegexOptions.Singleline)
            .Cast<Match>();

    private static Match ControlWithText(string html, string tag, string label)
        => Assert.Single(Controls(html, tag).Where(control => ControlText(control) == label));

    private static string ControlText(Match control)
    {
        var content = Regex.Replace(control.Groups["content"].Value, "<svg\\b[^>]*>.*?</svg>", "", RegexOptions.Singleline);
        var text = WebUtility.HtmlDecode(Regex.Replace(content, "<[^>]*>", ""));
        return Regex.Replace(text, "\\s+", " ").Trim();
    }

    private static string? ControlAttribute(Match control, string name)
    {
        var attribute = Regex.Matches(control.Groups["attributes"].Value,
                @"(?<name>[^\s=/>]+)(?:\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'|(?<unquoted>[^\s>]+)))?")
            .Cast<Match>().SingleOrDefault(match => match.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (attribute is null) return null;
        if (attribute.Groups["double"].Success) return WebUtility.HtmlDecode(attribute.Groups["double"].Value);
        if (attribute.Groups["single"].Success) return WebUtility.HtmlDecode(attribute.Groups["single"].Value);
        return attribute.Groups["unquoted"].Success ? WebUtility.HtmlDecode(attribute.Groups["unquoted"].Value) : string.Empty;
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
