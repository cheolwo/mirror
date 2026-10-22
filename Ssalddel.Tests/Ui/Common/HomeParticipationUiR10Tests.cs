using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Home;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class HomeParticipationUiR10Tests
{
    [Fact]
    public async Task 확정근거가없는금액과참여는_0으로꾸미지않고_각각미집계로보인다()
    {
        var html = await RenderSummaryAsync(null);

        Assert.Equal("미집계", Metric(html, "confirmed-platform-profit"));
        Assert.Equal("미집계", Metric(html, "confirmed-shared-payout"));
        Assert.Equal("미집계", Metric(html, "completed-freight-transports"));
        Assert.Equal("미집계", Metric(html, "freight-shipper-accounts"));
        Assert.Equal("미집계", Metric(html, "freight-driver-accounts"));
        Assert.DoesNotContain("0원", html);
        Assert.DoesNotContain("<time", html);
    }

    [Fact]
    public async Task 실제조회한0건은표시하되_미집계금액을0원으로바꾸지않는다()
    {
        var summary = Recorded();
        summary.CompletedFreightTransportCount = 0;
        summary.FreightShipperAccountCount = 0;
        summary.FreightDriverAccountCount = 0;

        var html = await RenderSummaryAsync(summary);

        Assert.Equal("0건", Metric(html, "completed-freight-transports"));
        Assert.Equal("0개", Metric(html, "freight-shipper-accounts"));
        Assert.Equal("0개", Metric(html, "freight-driver-accounts"));
        Assert.Equal("미집계", Metric(html, "confirmed-platform-profit"));
        Assert.Equal("미집계", Metric(html, "confirmed-shared-payout"));
    }

    [Fact]
    public async Task 확정금액의실제0원은_미집계와구별하고_화주기사계정을합산하지않는다()
    {
        var summary = Recorded();
        summary.ConfirmedPlatformProfitKrw = 0m;
        summary.ConfirmedSharedPayoutKrw = 12400m;

        var html = await RenderSummaryAsync(summary);

        Assert.Equal("0원", Metric(html, "confirmed-platform-profit"));
        Assert.Equal("12,400원", Metric(html, "confirmed-shared-payout"));
        Assert.Equal("17건", Metric(html, "completed-freight-transports"));
        Assert.Equal("3개", Metric(html, "freight-shipper-accounts"));
        Assert.Equal("5개", Metric(html, "freight-driver-accounts"));
        Assert.Contains("같은 계정의 역할별 중복 가능", html);
        Assert.DoesNotContain("8개", html);
        Assert.DoesNotContain("가입", html);
    }

    [Fact]
    public async Task 시험집계와일부참여누락_화물범위_누적기간과기준시각을함께보인다()
    {
        var summary = Recorded();
        summary.ExecutionMode = "Simulation";
        summary.HasIncompleteParticipantBindings = true;

        var html = await RenderSummaryAsync(summary);

        Assert.Contains("시험 집계", html);
        Assert.Contains("전체 누적", html);
        Assert.Contains("화물 완료 참여", html);
        Assert.Contains("일부 참여 계정은 집계되지 않았습니다.", html);
        Assert.Contains("수령 확인·정산과 별도", html);
        Assert.Contains("2026.10.22 12:00 (한국)", html);
        Assert.Contains("datetime=\"2026-10-22T03:00:00.0000000+00:00\"", html);
    }

    [Theory]
    [InlineData(true, false, "확인 중")]
    [InlineData(false, true, "확인 불가")]
    public async Task 로딩과오류에서는_과거숫자를정상현황처럼표시하지않는다(bool loading, bool error, string expected)
    {
        var summary = Recorded();
        summary.ConfirmedPlatformProfitKrw = 95000m;
        var html = await RenderSummaryAsync(summary, loading, error);

        Assert.Equal(expected, Metric(html, "confirmed-platform-profit"));
        Assert.Equal(expected, Metric(html, "completed-freight-transports"));
        Assert.Equal(expected, Metric(html, "freight-driver-accounts"));
        Assert.DoesNotContain("95,000원", html);
        Assert.DoesNotContain("17건", html);
        Assert.DoesNotContain("<time", html);
        if (loading) Assert.Contains("aria-busy=\"true\"", html);
    }

    [Fact]
    public async Task 조회가끝나기전에도_기존9개역할진입은즉시사용할수있다()
    {
        var pending = new TaskCompletionSource<HomeParticipationSummaryResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ScriptedClient((_, ct) => pending.Task.WaitAsync(ct));
        await using var fixture = new HomeFixture(client);
        Task render = Task.CompletedTask;
        await fixture.Renderer.Dispatcher.InvokeAsync(() => { render = fixture.Renderer.StartHome(); });
        try
        {
            await fixture.Renderer.Dispatcher.InvokeAsync(() =>
            {
                Assert.Equal(1, client.Calls);
                AssertAllRoleLinks(fixture.Renderer.RoleLinks());
                Assert.Contains("현황 확인 중", fixture.Renderer.VisibleText());
                Assert.DoesNotContain("0원", fixture.Renderer.VisibleText());
            });
        }
        finally
        {
            pending.TrySetResult(Recorded());
            await render;
        }
    }

    [Fact]
    public async Task 조회실패에도역할진입을유지하고_재시도버튼은현황만다시조회한다()
    {
        var client = new ScriptedClient((call, _) => call == 1
            ? Task.FromException<HomeParticipationSummaryResponse?>(new HttpRequestException("private-server-debug-trace"))
            : Task.FromResult<HomeParticipationSummaryResponse?>(Recorded()));
        await using var fixture = new HomeFixture(client);
        await fixture.Renderer.Dispatcher.InvokeAsync(fixture.Renderer.StartHome);

        await fixture.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            AssertAllRoleLinks(fixture.Renderer.RoleLinks());
            Assert.Contains("현황을 불러오지 못했습니다.", fixture.Renderer.VisibleText());
            Assert.Contains("확인 불가", fixture.Renderer.VisibleText());
            Assert.DoesNotContain("private-server-debug-trace", fixture.Renderer.VisibleText());
            Assert.DoesNotContain("0원", fixture.Renderer.VisibleText());
            Assert.Equal(1, client.Calls);

            await fixture.Renderer.ClickRetry();

            Assert.Equal(2, client.Calls);
            AssertAllRoleLinks(fixture.Renderer.RoleLinks());
            Assert.Contains("17건", fixture.Renderer.VisibleText());
            Assert.Contains("미집계", fixture.Renderer.VisibleText());
            Assert.DoesNotContain("현황을 불러오지 못했습니다.", fixture.Renderer.VisibleText());
            Assert.DoesNotContain("확인 불가", fixture.Renderer.VisibleText());
        });
        Assert.Empty(fixture.Navigation.Navigations);
    }

    [Fact]
    public async Task 현황client가없는기존호스트에서도_전체역할진입과하단거래안내를유지한다()
    {
        await using var fixture = new HomeFixture(null);
        await fixture.Renderer.Dispatcher.InvokeAsync(fixture.Renderer.StartHome);

        await fixture.Renderer.Dispatcher.InvokeAsync(() =>
        {
            AssertAllRoleLinks(fixture.Renderer.RoleLinks());
            Assert.Contains("현황을 불러오지 못했습니다.", fixture.Renderer.VisibleText());
            Assert.Contains("통신판매중개자", fixture.Renderer.VisibleText());
            Assert.Contains("거래·개인정보 안내", fixture.Renderer.VisibleText());
            Assert.DoesNotContain("0원", fixture.Renderer.VisibleText());
        });
    }

    [Fact]
    public async Task 현황화면수명종료는조회를취소하고_늦게온실패를홈의예외로퍼뜨리지않는다()
    {
        var pending = new TaskCompletionSource<HomeParticipationSummaryResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var client = new ScriptedClient((_, ct) => { observed = ct; return pending.Task; });
        await using var fixture = new HomeFixture(client);
        Task render = Task.CompletedTask;
        await fixture.Renderer.Dispatcher.InvokeAsync(() => { render = fixture.Renderer.StartHome(); });

        await fixture.Renderer.Dispatcher.InvokeAsync(() =>
        {
            fixture.Renderer.EndOverviewLifetime();
            Assert.True(observed.IsCancellationRequested);
            AssertAllRoleLinks(fixture.Renderer.RoleLinks());
        });
        pending.SetException(new HttpRequestException("late-private-response"));

        await render;
        await fixture.Renderer.Dispatcher.InvokeAsync(() =>
        {
            AssertAllRoleLinks(fixture.Renderer.RoleLinks());
            Assert.DoesNotContain("late-private-response", fixture.Renderer.VisibleText());
        });
        Assert.Equal(1, client.Calls);
    }

    private static HomeParticipationSummaryResponse Recorded() => new()
    {
        PeriodCode = "all-time", AsOfUtc = new(2026, 10, 22, 3, 0, 0, TimeSpan.Zero),
        ExecutionMode = "Operational", StatusCode = "recorded",
        CompletedFreightTransportCount = 17, FreightShipperAccountCount = 3, FreightDriverAccountCount = 5
    };

    private static string Metric(string html, string key)
    {
        var match = Regex.Match(html, "data-home-metric=\"" + Regex.Escape(key) + "\"[^>]*>.*?<dd\\b[^>]*>(.*?)</dd>", RegexOptions.Singleline);
        Assert.True(match.Success, "Expected a rendered metric: " + key);
        return Regex.Replace(match.Groups[1].Value, "<[^>]+>", "").Trim();
    }

    private static void AssertAllRoleLinks(IReadOnlyList<RoleLink> links)
    {
        Assert.Equal(9, links.Count);
        foreach (var entry in RoleWorkspaceHomeCatalog.Groups.SelectMany(group => group.Entries))
            Assert.Contains(links, link => link.Role == entry.RoleKey && link.Href == "http://localhost/preview" + entry.Href);
    }

    private static async Task<string> RenderSummaryAsync(HomeParticipationSummaryResponse? summary, bool loading = false, bool error = false)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<HomeParticipationSummary>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(HomeParticipationSummary.Summary)] = summary,
                [nameof(HomeParticipationSummary.IsLoading)] = loading,
                [nameof(HomeParticipationSummary.HasError)] = error
            }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class ScriptedClient(Func<int, CancellationToken, Task<HomeParticipationSummaryResponse?>> load) : IHomeParticipationSummaryClient
    {
        internal int Calls;
        public Task<HomeParticipationSummaryResponse?> LoadAsync(CancellationToken cancellationToken = default)
            => load(Interlocked.Increment(ref Calls), cancellationToken);
    }

    private sealed class HomeFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        internal readonly TestNavigation Navigation = new();
        internal readonly HomeRenderer Renderer;
        internal HomeFixture(IHomeParticipationSummaryClient? client)
        {
            var services = new ServiceCollection();
            services.AddLogging(); services.AddMudServices();
            services.AddSingleton<NavigationManager>(Navigation);
            if (client is not null) services.AddSingleton(client);
            _provider = services.BuildServiceProvider();
            Renderer = new(_provider, _provider.GetRequiredService<ILoggerFactory>());
        }
        public async ValueTask DisposeAsync() { await Renderer.DisposeAsync(); await _provider.DisposeAsync(); }
    }

    private sealed class TestNavigation : NavigationManager
    {
        internal readonly List<string> Navigations = [];
        internal TestNavigation() => Initialize("http://localhost/preview/", "http://localhost/preview/");
        protected override void NavigateToCore(string uri, bool forceLoad) => Navigations.Add(uri);
    }

    private sealed record RoleLink(string Role, string Href);

#pragma warning disable BL0006 // Test-only renderer dispatches the real Razor retry callback and observes role links during a pending read.
    private sealed class HomeRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
    {
        private int _root;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override void HandleException(Exception exception) => throw exception;
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        internal Task StartHome()
        {
            _root = AssignRootComponentId(InstantiateComponent(typeof(RoleSelectionHome)));
            return RenderRootComponentAsync(_root, ParameterView.Empty);
        }
        internal string VisibleText() => string.Concat(Trees().SelectMany(tree => tree)
            .Where(frame => frame.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup)
            .Select(frame => frame.FrameType == RenderTreeFrameType.Text ? frame.TextContent : frame.MarkupContent));
        internal IReadOnlyList<RoleLink> RoleLinks()
        {
            var links = new List<RoleLink>();
            foreach (var tree in Trees())
            for (var i = 0; i < tree.Length; i++)
            {
                if (tree[i].FrameType != RenderTreeFrameType.Element || tree[i].ElementName != "a") continue;
                var attributes = tree.Skip(i + 1).TakeWhile(frame => frame.FrameType == RenderTreeFrameType.Attribute)
                    .ToDictionary(frame => frame.AttributeName, frame => frame.AttributeValue?.ToString(), StringComparer.Ordinal);
                if (attributes.GetValueOrDefault("data-role") is { } role && attributes.GetValueOrDefault("href") is { } href)
                    links.Add(new(role, href));
            }
            return links;
        }
        internal Task ClickRetry()
        {
            foreach (var tree in Trees())
            for (var i = 0; i < tree.Length; i++)
            {
                if (tree[i].FrameType != RenderTreeFrameType.Element || tree[i].ElementName != "button") continue;
                var content = tree.Skip(i + 1).Take(tree[i].ElementSubtreeLength - 1).ToArray();
                var label = string.Concat(content.Where(frame => frame.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup)
                    .Select(frame => frame.FrameType == RenderTreeFrameType.Text ? frame.TextContent : frame.MarkupContent));
                if (!WebUtility.HtmlDecode(Regex.Replace(label, "<[^>]+>", "")).Contains("다시 확인", StringComparison.Ordinal)) continue;
                var click = Assert.Single(content.Where(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick"));
                return DispatchEventAsync(click.AttributeEventHandlerId, null, new MouseEventArgs());
            }
            throw new InvalidOperationException("Expected the rendered summary retry control.");
        }
        internal void EndOverviewLifetime()
            => Assert.Single(Trees().SelectMany(tree => tree)
                .Where(frame => frame.FrameType == RenderTreeFrameType.Component)
                .Select(frame => frame.Component).OfType<HomeParticipationOverview>()).Dispose();
        private IReadOnlyList<RenderTreeFrame[]> Trees()
        {
            var trees = new List<RenderTreeFrame[]>();
            var pending = new Queue<int>(); pending.Enqueue(_root);
            var seen = new HashSet<int>();
            while (pending.TryDequeue(out var component))
            {
                if (!seen.Add(component)) continue;
                var frames = GetCurrentRenderTreeFrames(component);
                var tree = frames.Array.Take(frames.Count).ToArray();
                trees.Add(tree);
                foreach (var child in tree.Where(frame => frame.FrameType == RenderTreeFrameType.Component)) pending.Enqueue(child.ComponentId);
            }
            return trees;
        }
    }
#pragma warning restore BL0006
}
