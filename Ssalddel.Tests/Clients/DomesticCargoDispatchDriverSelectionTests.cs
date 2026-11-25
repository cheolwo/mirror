using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Ssalddel.Contracts.Admin.Dispatch;
using SsalddelAdmin.Components.Pages;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 페이지 이벤트·서비스의 격리 HTTP 검증입니다. POST는 메모리 handler에만 기록하며 서버·배차를 변경하지 않습니다.</summary>
public sealed class DomesticCargoDispatchDriverSelectionTests
{
    [Fact]
    public async Task 재조회에서기사목록이바뀌고추천기사가없어지면이전기사로판단을저장하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.Equal(1, fixture.Http.Reads); Assert.Contains("이전 가상 기사", await fixture.HtmlAsync());
        await fixture.EventAsync("SelectDriver", "D1");

        await fixture.EventAsync("ReloadAsync");

        var html = await fixture.HtmlAsync();
        Assert.Equal(2, fixture.Http.Reads); Assert.Contains("현재 가상 기사", html); Assert.DoesNotContain("이전 가상 기사", html);
        Assert.Contains("현재 가상 화물", html); Assert.Contains("구성 의뢰 확인", html);
        await fixture.EventAsync("SaveDecisionAsync");
        Assert.Empty(fixture.Http.Decisions);
        Assert.Contains("기사", await fixture.HtmlAsync());
        Assert.Equal(0, fixture.Http.UnexpectedRequests);
    }

    [Fact]
    public async Task 재조회뒤이전기사의선택이벤트도저장을막고현재기사를선택하면기존묶음판단만전달한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.EventAsync("SelectDriver", "D1"); await fixture.EventAsync("ReloadAsync");
        Assert.Equal(2, fixture.Http.Reads); Assert.Contains("현재 가상 기사", await fixture.HtmlAsync());

        // 이미 렌더됐던 D1 버튼의 늦은 콜백도 현재 목록을 대신하지 못합니다.
        await fixture.EventAsync("SelectDriver", "D1"); await fixture.EventAsync("SaveDecisionAsync");
        Assert.Empty(fixture.Http.Decisions);

        await fixture.EventAsync("SelectDriver", "D2"); await fixture.EventAsync("SaveDecisionAsync");

        var saved = Assert.Single(fixture.Http.Decisions);
        Assert.Equal("D2", saved.DriverId); Assert.Equal("BUNDLE-CURRENT", saved.BundleKey);
        Assert.Equal(new[] { "REQUEST-CURRENT" }, saved.RequestIds); Assert.Equal("운영자승인", saved.DecisionType);
        Assert.True(saved.Accepted); Assert.False(saved.ManualBundle);
        Assert.Contains("격리 판단 응답", await fixture.HtmlAsync());
        Assert.Equal(2, fixture.Http.Reads); Assert.Equal(0, fixture.Http.UnexpectedRequests);
    }

    [Fact]
    public async Task 정상추천은같은현재기사의버튼과지도선택및정확한저장본문으로연결된다()
    {
        var snapshot = Snapshot("D1", Driver("D1", "추천 기사"), Driver("D2", "다른 기사"));
        await using var fixture = await Fixture.CreateAsync(http => http.Reply(snapshot));
        var html = await fixture.HtmlAsync();
        AssertSelection(html, "추천 기사", true); AssertSelection(html, "다른 기사", false);
        await fixture.EventAsync("SaveDecisionAsync");
        Assert.Equal("D1", Assert.Single(fixture.Http.Decisions).DriverId);
        Assert.Equal(1, fixture.Http.Reads);
    }

    [Fact]
    public async Task 새조회에유효한새추천이있으면이전선택대신새기사의지도와버튼및본문을일치시킨다()
    {
        await using var fixture = await Fixture.CreateAsync(http =>
        {
            http.Reply(Snapshot("D1", Driver("D1", "첫 기사"), Driver("D2", "다음 기사")));
            http.Reply(Snapshot("D2", Driver("D1", "첫 기사"), Driver("D2", "다음 기사")));
        });
        await fixture.EventAsync("ReloadAsync");
        var html = await fixture.HtmlAsync(); AssertSelection(html, "첫 기사", false); AssertSelection(html, "다음 기사", true);
        await fixture.EventAsync("SaveDecisionAsync");
        Assert.Equal("D2", Assert.Single(fixture.Http.Decisions).DriverId); Assert.Equal(2, fixture.Http.Reads);
    }

    [Fact]
    public async Task 추천이없어져도현재목록에유일하게남은수동기사선택은보존한다()
    {
        await using var fixture = await Fixture.CreateAsync(http =>
        {
            http.Reply(Snapshot("D1", Driver("D1", "추천 기사"), Driver("D2", "수동 기사")));
            http.Reply(Snapshot(null, Driver("D1", "추천 기사"), Driver("D2", "수동 기사")));
        });
        await fixture.EventAsync("SelectDriver", "D2"); await fixture.EventAsync("ReloadAsync");
        var html = await fixture.HtmlAsync(); AssertSelection(html, "추천 기사", false); AssertSelection(html, "수동 기사", true);
        await fixture.EventAsync("SaveDecisionAsync");
        Assert.Equal("D2", Assert.Single(fixture.Http.Decisions).DriverId);
    }

    [Theory]
    [InlineData("OUTSIDE")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("d2")]
    [InlineData("D2 ")]
    public async Task 현재목록과정확히연결되지않은추천은선택을생성하거나저장하지않는다(string? suggestion)
    {
        await using var fixture = await Fixture.CreateAsync(http => http.Reply(Snapshot(suggestion, Driver("D2", "현재 기사"))));
        AssertSelection(await fixture.HtmlAsync(), "현재 기사", false);
        await fixture.EventAsync("SaveDecisionAsync");
        Assert.Empty(fixture.Http.Decisions); Assert.Contains("기사님을 선택", await fixture.HtmlAsync());
    }

    [Theory]
    [InlineData("blank")]
    [InlineData("duplicate")]
    [InlineData("none")]
    public async Task 빈번호와중복번호및없는현재기사는선택표시와저장근거가되지않는다(string kind)
    {
        var drivers = kind switch
        {
            "blank" => new[] { Driver(" ", "빈 번호 기사") },
            "duplicate" => new[] { Driver("D1", "중복 기사 A"), Driver("D1", "중복 기사 B") },
            _ => Array.Empty<DomesticCargoDispatchAIReviewDriverDto>()
        };
        await using var fixture = await Fixture.CreateAsync(http => http.Reply(Snapshot(kind == "blank" ? " " : "D1", drivers)));
        foreach (var driver in drivers) AssertSelection(await fixture.HtmlAsync(), driver.DriverName, false);
        await fixture.EventAsync("SelectDriver", kind == "blank" ? " " : "D1"); await fixture.EventAsync("SaveDecisionAsync");
        Assert.Empty(fixture.Http.Decisions); Assert.Equal(1, fixture.Http.Reads);
    }

    [Theory]
    [InlineData("d2")]
    [InlineData("D2 ")]
    [InlineData(" D2")]
    public async Task 직접선택도대소문자와공백을임의정규화해현재기사와연결하지않는다(string selected)
    {
        await using var fixture = await Fixture.CreateAsync(http => http.Reply(Snapshot(null, Driver("D2", "현재 기사"))));
        await fixture.EventAsync("SelectDriver", selected);
        AssertSelection(await fixture.HtmlAsync(), "현재 기사", false);
        await fixture.EventAsync("SaveDecisionAsync"); Assert.Empty(fixture.Http.Decisions);
        await fixture.EventAsync("SelectDriver", "D2"); AssertSelection(await fixture.HtmlAsync(), "현재 기사", true);
        await fixture.EventAsync("SaveDecisionAsync"); Assert.Equal("D2", Assert.Single(fixture.Http.Decisions).DriverId);
    }

    [Fact]
    public async Task 이전기사선택콜백이늦게와도이미명시선택한현재기사를바꾸지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.EventAsync("ReloadAsync"); await fixture.EventAsync("SelectDriver", "D2");
        await fixture.EventAsync("SelectDriver", "D1");
        AssertSelection(await fixture.HtmlAsync(), "현재 가상 기사", true);
        await fixture.EventAsync("SaveDecisionAsync"); Assert.Equal("D2", Assert.Single(fixture.Http.Decisions).DriverId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장직전현재목록에서선택기사가사라지거나중복되면POST를보내지않는다(bool duplicate)
    {
        await using var fixture = await Fixture.CreateAsync(http => http.Reply(Snapshot("D1", Driver("D1", "선택 기사"))));
        AssertSelection(await fixture.HtmlAsync(), "선택 기사", true);
        // 저장 경계 자체를 검증한다. 중간 조회 선택 초기화의 도움 없이 현재 목록만 변경한다.
        await fixture.SetCurrentDriversAsync(duplicate
            ? [Driver("D1", "중복 A"), Driver("D1", "중복 B")]
            : [Driver("D2", "새 현재 기사")]);
        await fixture.EventAsync("SaveDecisionAsync");
        Assert.Empty(fixture.Http.Decisions); Assert.Contains("기사님을 선택", await fixture.HtmlAsync());
        foreach (var driver in duplicate ? new[] { "중복 A", "중복 B" } : new[] { "새 현재 기사" })
            AssertSelection(await fixture.HtmlAsync(), driver, false);
    }

    [Theory]
    [InlineData(503)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(0)]
    public async Task 조회실패와인증권한오류및정상빈결과에서는이전선택을저장하지않는다(int status)
    {
        await using var fixture = await Fixture.CreateAsync(http =>
        {
            http.Reply(Snapshot("D1", Driver("D1", "이전 기사")));
            if (status == 0) http.Reply(new DomesticCargoDispatchAIReviewWorkspaceDto());
            else http.ReplyError((HttpStatusCode)status);
        });
        await fixture.EventAsync("ReloadAsync");
        var html = await fixture.HtmlAsync();
        Assert.DoesNotContain("이전 기사", html); Assert.DoesNotContain("RAG 판단 사례로 저장", html);
        await fixture.EventAsync("SelectDriver", "D1"); await fixture.EventAsync("SaveDecisionAsync");
        Assert.Empty(fixture.Http.Decisions); Assert.Equal(2, fixture.Http.Reads);
    }

    [Fact]
    public async Task 사용가능묶음이없는기존초기화는빈중복번호를건너뛰고첫유효기사선택을유지한다()
    {
        var snapshot = Snapshot(null, Driver(" ", "빈 번호"), Driver("DUP", "중복 A"), Driver("DUP", "중복 B"), Driver("D2", "첫 유효 기사"));
        snapshot.Bundles.Clear();
        await using var fixture = await Fixture.CreateAsync(http => http.Reply(snapshot));
        var html = await fixture.HtmlAsync();
        AssertSelection(html, "빈 번호", false); AssertSelection(html, "중복 A", false); AssertSelection(html, "중복 B", false); AssertSelection(html, "첫 유효 기사", true);
        await fixture.EventAsync("SaveDecisionAsync");
        var saved = Assert.Single(fixture.Http.Decisions); Assert.Equal("D2", saved.DriverId); Assert.Equal(string.Empty, saved.BundleKey);
        Assert.Equal(new[] { "REQUEST-CURRENT" }, saved.RequestIds);
    }

    [Fact]
    public async Task 같은업무의구성화물과제안크기는재조회전후보존하고명시선택한새기사만판단본문에반영한다()
    {
        var first = Snapshot("D1", Driver("D1", "이전 기사"));
        first.Requests.Add(new() { RequestId = "REQUEST-SECOND", CargoType = "둘째 가상 화물 7박스" });
        first.Bundles[0].RequestIds.Add("REQUEST-SECOND"); first.Bundles[0].BundleSize = 2;
        var next = JsonSerializer.Deserialize<DomesticCargoDispatchAIReviewWorkspaceDto>(JsonSerializer.Serialize(first))!;
        next.Drivers = [Driver("D2", "현재 기사")]; next.Bundles[0].SuggestedDriverId = null;
        await using var fixture = await Fixture.CreateAsync(http => { http.Reply(first); http.Reply(next); });
        var before = await fixture.HtmlAsync();
        Assert.Contains("현재 가상 화물", before); Assert.Contains("둘째 가상 화물 7박스", before);
        await fixture.EventAsync("ReloadAsync");
        var after = await fixture.HtmlAsync();
        Assert.Contains("현재 가상 화물", after); Assert.Contains("둘째 가상 화물 7박스", after);
        Assert.Contains("REQUEST-CURRENT", after); Assert.Contains("REQUEST-SECOND", after); Assert.Contains("2 건", after);
        AssertSelection(after, "현재 기사", false);
        await fixture.EventAsync("SelectDriver", "D2"); AssertSelection(await fixture.HtmlAsync(), "현재 기사", true);
        await fixture.EventAsync("SaveDecisionAsync");
        var saved = Assert.Single(fixture.Http.Decisions);
        Assert.Equal(first.Bundles[0].BundleKey, saved.BundleKey); Assert.Equal(first.Bundles[0].RequestIds, saved.RequestIds); Assert.Equal("D2", saved.DriverId);
        Assert.Equal(JsonSerializer.Serialize(first.Requests), JsonSerializer.Serialize(next.Requests));
        Assert.Equal(first.Bundles[0].BundleSize, next.Bundles[0].BundleSize); Assert.Equal(2, fixture.Http.Reads);
    }

    private static DomesticCargoDispatchAIReviewDriverDto Driver(string id, string name)
        => new() { DriverId = id, DriverName = name, VehicleType = "가상 차량", Latitude = 37.5m, Longitude = 127m };
    private static DomesticCargoDispatchAIReviewWorkspaceDto Snapshot(string? suggestion, params DomesticCargoDispatchAIReviewDriverDto[] drivers)
        => new()
        {
            GeneratedAt = new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero),
            Requests = [new() { RequestId = "REQUEST-CURRENT", CargoType = "현재 가상 화물", PickupAddress = "가상 상차 지역", DropoffAddress = "가상 하차 지역" }],
            Drivers = drivers.ToList(),
            Bundles = [new() { BundleKey = "BUNDLE-CURRENT", BundleType = "가상 제안", RequestIds = ["REQUEST-CURRENT"], BundleSize = 1,
                IsAISuggested = true, IsBundleAvailable = true, SuggestedDriverId = suggestion, Reason = "가상 추천 근거" }]
        };
    private static void AssertSelection(string html, string name, bool expected)
    {
        var button = Assert.Single(Regex.Matches(html, "<button\\b[^>]*>(?:(?!</button>).)*</button>", RegexOptions.Singleline)
            .Cast<Match>().Select(match => match.Value).Where(value => value.Contains(name + " · 가상 차량", StringComparison.Ordinal)));
        var marker = Assert.Single(Regex.Matches(html, "<div\\b[^>]*>").Cast<Match>().Select(match => match.Value)
            .Where(value => Attribute(value, "class").Split(' ').Contains("driver") && Attribute(value, "title") == name + " · 가상 차량"));
        Assert.Equal(expected, Attribute(button, "class").Split(' ').Contains("mud-button-filled"));
        Assert.Equal(expected, Attribute(marker, "class").Split(' ').Contains("selected"));
    }
    private static string Attribute(string html, string name)
        => Regex.Match(html, "\\b" + Regex.Escape(name) + "=\"([^\"]*)\"").Groups[1].Value;

    private sealed class Fixture : IAsyncDisposable
    {
        public IsolatedHttp Http { get; } = new();
        private readonly ServiceProvider services;
        private readonly StaticHtmlRenderer renderer;
        private readonly DomesticCargoDispatchAIReview page = new();
        private HtmlRootComponent rendered;

        private Fixture()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminData:UseMemory"] = "false", ["AdminAuth:DevelopmentBootstrap:Enabled"] = "true",
                ["AdminAuth:DevelopmentBootstrap:AccessToken"] = IsolatedHttp.Token,
                ["AdminAuth:DevelopmentBootstrap:UserId"] = "synthetic-operator",
                ["AdminAuth:DevelopmentBootstrap:Roles:0"] = "서버관리자"
            }).Build();
            var registrations = new ServiceCollection(); registrations.AddLogging(); registrations.AddMudServices();
            registrations.AddSingleton<IConfiguration>(config); registrations.AddSingleton<IWebHostEnvironment, EnvironmentStub>();
            registrations.AddSingleton<IJSRuntime, NoJavaScript>(); registrations.AddSingleton<NavigationManager, NavigationStub>();
            registrations.AddSingleton<관리자인증세션Service>();
            registrations.AddSingleton(new HttpClient(Http, false) { BaseAddress = new Uri("http://fixture.invalid/") });
            registrations.AddSingleton<DomesticCargoDispatchAIReviewAdminService>();
            services = registrations.BuildServiceProvider();
            renderer = new(services, services.GetRequiredService<ILoggerFactory>());
            foreach (var property in typeof(DomesticCargoDispatchAIReview).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null))
                property.SetValue(page, services.GetRequiredService(property.PropertyType));
        }

        public static async Task<Fixture> CreateAsync(Action<IsolatedHttp>? configure = null)
        {
            var fixture = new Fixture();
            configure?.Invoke(fixture.Http);
            try
            {
                await fixture.renderer.Dispatcher.InvokeAsync(async () =>
                {
                    var providers = fixture.renderer.BeginRenderingComponent(new Providers(), ParameterView.Empty);
                    await providers.QuiescenceTask;
                    // 실제 인스턴스를 사용하므로 제품 @rendermode를 바꾸지 않고 정적 이벤트 결과를 읽습니다.
                    fixture.rendered = fixture.renderer.BeginRenderingComponent(fixture.page, ParameterView.Empty);
                    await fixture.rendered.QuiescenceTask;
                });
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public Task EventAsync(string method, params object?[] args)
            => renderer.Dispatcher.InvokeAsync(() => ((IHandleEvent)page).HandleEventAsync(
                new EventCallbackWorkItem((Func<Task>)(async () =>
                {
                    var target = typeof(DomesticCargoDispatchAIReview).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingMethodException(method);
                    if (target.Invoke(page, args) is Task task) await task;
                })), null));
        public Task<string> HtmlAsync() => renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(rendered.ToHtmlString()));
        public Task SetCurrentDriversAsync(IReadOnlyList<DomesticCargoDispatchAIReviewDriverDto> drivers)
            => renderer.Dispatcher.InvokeAsync(() =>
            {
                var workspace = (DomesticCargoDispatchAIReviewWorkspaceDto)typeof(DomesticCargoDispatchAIReview)
                    .GetField("workspace", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
                workspace.Drivers = drivers.ToList();
            });
        public async ValueTask DisposeAsync() { await renderer.DisposeAsync(); await services.DisposeAsync(); Http.Dispose(); }
    }

    private sealed class IsolatedHttp : HttpMessageHandler
    {
        public const string Token = "operator-driver-selection-synthetic";
        private const string Endpoint = "/api/v1/admin/dispatch/domestic-cargo-ai-review";
        public int Reads { get; private set; }
        public int UnexpectedRequests { get; private set; }
        public List<DomesticCargoDispatchAIReviewDecisionRequest> Decisions { get; } = [];
        private readonly Queue<Func<HttpResponseMessage>> replies = [];
        public void Reply(DomesticCargoDispatchAIReviewWorkspaceDto snapshot) => replies.Enqueue(() => Json(snapshot));
        public void ReplyError(HttpStatusCode status) => replies.Enqueue(() => new(status));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri?.Host == "fixture.invalid" && request.Headers.Authorization?.Parameter == Token)
            {
                if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == Endpoint)
                {
                    Reads++;
                    return replies.Count > 0 ? replies.Dequeue()() : Json(Workspace(Reads == 1));
                }
                if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == Endpoint + "/decisions")
                {
                    Decisions.Add(await request.Content!.ReadFromJsonAsync<DomesticCargoDispatchAIReviewDecisionRequest>(token)
                        ?? throw new InvalidDataException("판단 요청 본문이 없습니다."));
                    return Json(new DomesticCargoDispatchAIReviewDecisionResponse { CaseId = "SYNTHETIC-CASE", Message = "격리 판단 응답" });
                }
            }
            UnexpectedRequests++; throw new InvalidOperationException("허용되지 않은 격리 HTTP 요청입니다.");
        }
        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        private static DomesticCargoDispatchAIReviewWorkspaceDto Workspace(bool first)
            => Snapshot(first ? "D1" : null, Driver(first ? "D1" : "D2", first ? "이전 가상 기사" : "현재 가상 기사"));
    }

    private sealed class NavigationStub : NavigationManager
    {
        public NavigationStub() => Initialize("http://fixture.invalid/", "http://fixture.invalid/dispatch/ai-review");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("이 시험은 탐색하지 않습니다.");
    }
    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SyntheticDriverSelectionTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Providers : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudThemeProvider>(0); builder.CloseComponent();
            builder.OpenComponent<MudPopoverProvider>(1); builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(2); builder.CloseComponent();
            builder.OpenComponent<MudSnackbarProvider>(3); builder.CloseComponent();
        }
    }
}
