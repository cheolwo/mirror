using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.Components.PrivacySupport;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OverviewSupportNavigationR12Tests
{
    [Fact]
    public void 기본지원주소와로그인복귀는_기존주소를유지한다()
    {
        var context = PrivacySupportNavigationContext.Resolve();
        Assert.Equal("/privacy-support", context.ListPath());
        Assert.Equal("/privacy-support/case-one", context.DetailPath("case-one"));
        Assert.Equal("/login?returnUrl=%2Fprivacy-support%2Fcase-one", context.LoginPath("case-one"));
    }

    [Theory]
    [InlineData(-3, 1, "https://external.invalid/private")]
    [InlineData(int.MaxValue, 10000, "//external.invalid/private")]
    [InlineData(0, 1, "/%2fexternal.invalid/private")]
    public void 잘못된읽기조건과외부복귀는_지원권한이나외부이동으로해석하지않는다(int page, int expectedPage, string origin)
    {
        var context = PrivacySupportNavigationContext.Resolve("approve-admin", page, origin);
        Assert.Equal(보호지원종류Codes.분쟁, context.Kind);
        Assert.Equal(expectedPage, context.Page);
        Assert.Null(context.ReturnPath);
        Assert.DoesNotContain("external", context.CurrentPath("case-one"));
        Assert.DoesNotContain("approve-admin", context.CurrentPath("case-one"));
    }

    [Fact]
    public async Task 지원목록은선택한종류와페이지를조회하고_상세에같은복귀조건을전달한다()
    {
        var client = new Client();
        var html = await RenderAsync(client, Parameters());

        Assert.Equal((보호지원종류Codes.권리, 3), Assert.Single(client.ListReads));
        Assert.Contains("href=\"/privacy-support/case-one?kind=privacy-rights&page=3&from=%2Fdashboard%3Fstatus%3Dreview\"", html);
        Assert.Contains("이전 목록", html);
        Assert.Contains("다음 목록", html);
        Assert.Empty(client.DetailReads);
        Assert.Equal(0, client.EvidenceReads);
        Assert.Equal(0, client.Commands);
    }

    [Fact]
    public async Task 상세에서목록으로돌아갈때_종류페이지와원래요약경로가유지되고_감사열람은자동실행되지않는다()
    {
        var client = new Client();
        var parameters = Parameters("case-one");
        var html = await RenderAsync(client, parameters);

        Assert.Contains("href=\"/privacy-support?kind=privacy-rights&page=3&from=%2Fdashboard%3Fstatus%3Dreview\"", html);
        Assert.Contains("비공개 기록 확인", html);
        Assert.Equal("case-one", Assert.Single(client.DetailReads));
        Assert.Empty(client.ListReads);
        Assert.Equal(0, client.EvidenceReads);
        Assert.Equal(0, client.Commands);
        var login = PrivacySupportNavigationContext.Resolve(보호지원종류Codes.권리, 3, "/dashboard?status=review").LoginPath("case-one");
        Assert.Equal("/privacy-support/case-one?kind=privacy-rights&page=3&from=%2Fdashboard%3Fstatus%3Dreview",
            Uri.UnescapeDataString(login.Split("returnUrl=", 2)[1]));
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(0)]
    public async Task 상세조회실패도_필터목록으로복귀하며_원문오류나명령을노출하지않는다(int status)
    {
        var client = new Client
        {
            DetailError = status == 0 ? new HttpRequestException("private-server-trace")
                : new SsalddelApiException("private-server-trace", status, "support", "", null)
        };
        var html = await RenderAsync(client, Parameters("missing-case"));

        Assert.Contains("요청 목록으로", html);
        Assert.Contains("href=\"/privacy-support?kind=privacy-rights&page=3&from=%2Fdashboard%3Fstatus%3Dreview\"", html);
        Assert.DoesNotContain("private-server-trace", html);
        Assert.DoesNotContain("<form", html);
        Assert.Equal(0, client.EvidenceReads);
        Assert.Equal(0, client.Commands);
    }

    [Fact]
    public async Task 익명으로상세주소를열어도_목록상세감사열람과명령은호출하지않는다()
    {
        var client = new Client();
        var parameters = Parameters("case-one");
        parameters[nameof(보호지원관리Page.AdminOwnerProvider)] = (Func<string?>)(() => null);
        var html = await RenderAsync(client, parameters);

        Assert.Contains("운영자 로그인이 필요해요", html);
        Assert.Empty(client.ListReads);
        Assert.Empty(client.DetailReads);
        Assert.Equal(0, client.EvidenceReads);
        Assert.Equal(0, client.Commands);
    }

    [Fact]
    public async Task 실제종류변경과다음페이지버튼은_URL조건을갱신하고_새종류는첫페이지로시작한다()
    {
        var client = new Client(); var navigation = new TestNavigation();
        var services = Services(client); services.AddSingleton<NavigationManager>(navigation);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new SupportRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderAsync(Parameters()));
        await renderer.Dispatcher.InvokeAsync(() => renderer.ClickAsync("다음 목록"));
        Assert.Equal("/privacy-support?kind=privacy-rights&page=4&from=%2Fdashboard%3Fstatus%3Dreview", navigation.LastDestination);
        Assert.Single(client.ListReads); // Navigation requests a route; it never submits a support command.

        await renderer.Dispatcher.InvokeAsync(() => renderer.ChangeKindAsync(보호지원종류Codes.사고));
        Assert.Equal("/privacy-support?kind=privacy-incident&from=%2Fdashboard%3Fstatus%3Dreview", navigation.LastDestination);
        var next = Parameters(); next[nameof(보호지원관리Page.Kind)] = 보호지원종류Codes.사고; next[nameof(보호지원관리Page.Page)] = 1;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderAsync(next));
        Assert.Equal((보호지원종류Codes.사고, 1), client.ListReads.Last());
        Assert.Equal(0, client.EvidenceReads);
        Assert.Equal(0, client.Commands);
    }

    private static Dictionary<string, object?> Parameters(string? caseId = null) => new()
    {
        [nameof(보호지원관리Page.CaseId)] = caseId,
        [nameof(보호지원관리Page.Kind)] = 보호지원종류Codes.권리,
        [nameof(보호지원관리Page.Page)] = 3,
        [nameof(보호지원관리Page.ReturnPath)] = "/dashboard?status=review",
        [nameof(보호지원관리Page.AdminOwnerProvider)] = (Func<string?>)(() => "existing-admin")
    };

    private static ServiceCollection Services(Client client)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<I보호지원관리Client>(client); return services;
    }

    private static async Task<string> RenderAsync(Client client, Dictionary<string, object?> parameters)
    {
        await using var provider = Services(client).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<보호지원관리Page>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(component.ToHtmlString());
        });
    }

    private sealed class Client : I보호지원관리Client
    {
        public List<(string Kind, int Page)> ListReads { get; } = [];
        public List<string> DetailReads { get; } = [];
        public Exception? DetailError { get; init; }
        public int EvidenceReads { get; private set; }
        public int Commands { get; private set; }
        public Task<보호지원ListResponse> 목록Async(string kind, int page, CancellationToken ct)
        {
            ListReads.Add((kind, page));
            return Task.FromResult(new 보호지원ListResponse { Page = page, HasMore = page < 4, Items = [Row("case-one", kind)] });
        }
        public Task<보호지원CaseResponse> 상세Async(string caseId, CancellationToken ct)
        {
            DetailReads.Add(caseId);
            return DetailError is null ? Task.FromResult(Row(caseId, 보호지원종류Codes.권리)) : Task.FromException<보호지원CaseResponse>(DetailError);
        }
        public Task<보호지원CaseResponse> 변경Async(string caseId, 보호지원CommandRequest request, CancellationToken ct)
        { ++Commands; throw new InvalidOperationException("Navigation must not submit a command."); }
        public Task<보호지원증거Response> 비공개확인Async(string caseId, CancellationToken ct)
        { ++EvidenceReads; throw new InvalidOperationException("Evidence requires a separate explicit audited action."); }
        public Task<NeighborhoodMemberRecoveryContextResponse> 배송복구조회Async(string requestId, NeighborhoodMemberRecoveryRequest request, CancellationToken ct)
            => throw new InvalidOperationException("Navigation must not start recovery.");
        public Task<NeighborhoodMemberRecoveryResponse> 배송복구Async(string requestId, NeighborhoodMemberRecoveryRequest request, CancellationToken ct)
            => throw new InvalidOperationException("Navigation must not start recovery.");
        private static 보호지원CaseResponse Row(string id, string kind) => new()
        {
            CaseId = id, Kind = kind, StatusCode = "reviewing", Revision = 1,
            CreatedAtUtc = new DateTime(2026, 10, 22, 1, 0, 0, DateTimeKind.Utc), AllowedActions = ["add-evidence"]
        };
    }

    private sealed class TestNavigation : NavigationManager
    {
        public string? LastDestination { get; private set; }
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/privacy-support?kind=privacy-rights&page=3");
        protected override void NavigateToCore(string uri, bool forceLoad) => LastDestination = uri;
    }

#pragma warning disable BL0006 // Test-only renderer exercises the actual Razor navigation callbacks without a browser or server.
    private sealed class SupportRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
    {
        private int? root;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override void HandleException(Exception exception) => throw exception;
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        public Task RenderAsync(Dictionary<string, object?> parameters)
        {
            root ??= AssignRootComponentId(InstantiateComponent(typeof(보호지원관리Page)));
            return RenderRootComponentAsync(root.Value, ParameterView.FromDictionary(parameters));
        }
        public Task ChangeKindAsync(string kind)
        {
            var frames = Frames();
            for (var i = 0; i < frames.Length; ++i)
            {
                if (frames[i].FrameType != RenderTreeFrameType.Element || frames[i].ElementName != "select") continue;
                var attributes = frames.Skip(i + 1).TakeWhile(frame => frame.FrameType == RenderTreeFrameType.Attribute).ToArray();
                if (!attributes.Any(frame => frame.AttributeName == "id" && Equals(frame.AttributeValue, "support-kind"))) continue;
                var change = Assert.Single(attributes.Where(frame => frame.AttributeName == "onchange"));
                return DispatchEventAsync(change.AttributeEventHandlerId, null, new ChangeEventArgs { Value = kind });
            }
            throw new InvalidOperationException("Expected the real support kind selector.");
        }
        public Task ClickAsync(string label)
        {
            var frames = Frames();
            for (var i = 0; i < frames.Length; ++i)
            {
                if (frames[i].FrameType != RenderTreeFrameType.Element || frames[i].ElementName != "button") continue;
                var content = frames.Skip(i + 1).Take(frames[i].ElementSubtreeLength - 1).ToArray();
                var text = string.Concat(content.Where(frame => frame.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup)
                    .Select(frame => frame.FrameType == RenderTreeFrameType.Text ? frame.TextContent : frame.MarkupContent));
                if (WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", "")).Trim() != label) continue;
                var click = Assert.Single(content.Where(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick"));
                return DispatchEventAsync(click.AttributeEventHandlerId, null, new MouseEventArgs());
            }
            throw new InvalidOperationException("Expected the real support list paging control.");
        }
        private RenderTreeFrame[] Frames()
        {
            var frames = GetCurrentRenderTreeFrames(root!.Value);
            return frames.Array.Take(frames.Count).ToArray();
        }
    }
#pragma warning restore BL0006
}
