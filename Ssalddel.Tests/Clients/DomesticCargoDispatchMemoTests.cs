using System.Net;
using System.Net.Http.Json;
using System.Reflection;
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

/// <summary>실제 메모 바인딩·페이지·서비스와 메모리 HTTP handler의 회귀입니다. 호스트·브라우저·운영 저장을 실행하지 않습니다.</summary>
public sealed class DomesticCargoDispatchMemoTests
{
    [Theory]
    [InlineData("전송한 메모 A", "새로 작성한 메모 B")]
    [InlineData("같은 글자", " 같은 글자 ")]
    [InlineData("  ", " ")]
    [InlineData(" A ", "A")]
    public async Task 저장대기중실제메모바인딩이바뀌면전송본문은보존하고성공뒤새입력도남긴다(string submitted, string latest)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.NoteAsync(submitted); Assert.Equal(submitted, await fixture.InputAsync());
        var pending = fixture.Http.DeferPost(); var saving = fixture.SaveAsync();
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var payload = Assert.Single(fixture.Http.Decisions);
        Assert.Equal(submitted, payload.AdminNote); AssertOriginalWork(payload);

        await fixture.TypeAsync(latest); Assert.Equal(latest, await fixture.InputAsync());
        Assert.Equal(latest, await fixture.BoundNoteAsync());
        pending.Complete(HttpStatusCode.OK); await saving;

        Assert.Equal(latest, await fixture.InputAsync()); Assert.Equal(latest, await fixture.BoundNoteAsync()); Assert.Equal(submitted, payload.AdminNote);
        Assert.Equal(1, fixture.Http.Reads); Assert.Equal(0, fixture.Http.UnexpectedRequests);
    }

    [Fact]
    public async Task 저장대기중바뀌지않은메모는성공응답뒤기존동작대로비운다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.NoteAsync("변경 없는 메모 A");
        var pending = fixture.Http.DeferPost(); var saving = fixture.SaveAsync();
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("변경 없는 메모 A", await fixture.InputAsync());
        pending.Complete(HttpStatusCode.OK); await saving;

        Assert.Equal(string.Empty, await fixture.InputAsync());
        Assert.Equal(string.Empty, await fixture.BoundNoteAsync());
        var payload = Assert.Single(fixture.Http.Decisions); Assert.Equal("변경 없는 메모 A", payload.AdminNote); AssertOriginalWork(payload);
    }

    [Fact]
    public async Task 이미부모바인딩까지전달된새메모도이전저장성공으로지우지않는다()
    {
        await using var fixture = await Fixture.CreateAsync(); await fixture.NoteAsync("전송 A");
        var pending = fixture.Http.DeferPost(); var saving = fixture.SaveAsync();
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // oninput 반영 시점과 별개로 성공 콜백의 무조건 초기화 문제를 독립 검증합니다.
        await fixture.NoteAsync("새 메모 B"); Assert.Equal("새 메모 B", await fixture.BoundNoteAsync());
        pending.Complete(HttpStatusCode.OK); await saving;
        Assert.Equal("새 메모 B", await fixture.BoundNoteAsync()); Assert.Equal("새 메모 B", await fixture.InputAsync());
        var payload = Assert.Single(fixture.Http.Decisions); Assert.Equal("전송 A", payload.AdminNote); AssertOriginalWork(payload);
    }

    [Theory]
    [InlineData(503)]
    [InlineData(401)]
    public async Task 저장실패나인증오류는작성중최신메모를보존하며다음명시저장은그원문을다시보낸다(int status)
    {
        await using var fixture = await Fixture.CreateAsync(); await fixture.NoteAsync("전송한 A");
        var pending = fixture.Http.DeferPost(); var saving = fixture.SaveAsync();
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.TypeAsync("  작성 중 B\n다음 줄  ");
        pending.Complete((HttpStatusCode)status); await saving;

        Assert.Equal("  작성 중 B\n다음 줄  ", await fixture.BoundNoteAsync());
        Assert.Equal("  작성 중 B\n다음 줄  ", await fixture.InputAsync()); Assert.Contains("판단 사례 저장 실패", await fixture.TextAsync());
        Assert.Equal("전송한 A", Assert.Single(fixture.Http.Decisions).AdminNote);

        await fixture.SaveAsync();

        Assert.Equal(2, fixture.Http.Decisions.Count); Assert.Equal("  작성 중 B\n다음 줄  ", fixture.Http.Decisions[1].AdminNote);
        Assert.All(fixture.Http.Decisions, AssertOriginalWork);
        Assert.Equal(string.Empty, await fixture.BoundNoteAsync()); Assert.Equal(string.Empty, await fixture.InputAsync());
        Assert.Equal(1, fixture.Http.Reads); Assert.Equal(0, fixture.Http.UnexpectedRequests);
    }

    [Fact]
    public async Task 저장대기중반복저장도같은POST한번만보내며입력과현재업무를중복처리하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync(); await fixture.NoteAsync("같은 요청 A");
        var pending = fixture.Http.DeferPost(); var first = fixture.SaveAsync();
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.SaveAsync();

        var payload = Assert.Single(fixture.Http.Decisions); Assert.Equal("같은 요청 A", payload.AdminNote); AssertOriginalWork(payload);
        Assert.Equal("같은 요청 A", await fixture.InputAsync());
        pending.Complete(HttpStatusCode.OK); await first;
        Assert.Single(fixture.Http.Decisions); Assert.Equal(string.Empty, await fixture.InputAsync());
        Assert.Equal(1, fixture.Http.Reads); Assert.Equal(0, fixture.Http.UnexpectedRequests);
    }

    [Fact]
    public async Task 이전저장성공뒤보존한B를다음에명시저장하면같은업무로B를보내고그입력만비운다()
    {
        await using var fixture = await Fixture.CreateAsync(); await fixture.NoteAsync("A");
        var pending = fixture.Http.DeferPost(); var saving = fixture.SaveAsync();
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.TypeAsync(" B "); pending.Complete(HttpStatusCode.OK); await saving;
        Assert.Equal(" B ", await fixture.BoundNoteAsync()); Assert.Equal(" B ", await fixture.InputAsync());

        await fixture.SaveAsync();

        Assert.Equal(new[] { "A", " B " }, fixture.Http.Decisions.Select(decision => decision.AdminNote));
        Assert.All(fixture.Http.Decisions, AssertOriginalWork);
        Assert.Equal(string.Empty, await fixture.BoundNoteAsync()); Assert.Equal(string.Empty, await fixture.InputAsync());
        Assert.Equal(1, fixture.Http.Reads); Assert.Equal(0, fixture.Http.UnexpectedRequests);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("  정확한 원문  ")]
    public async Task 변경하지않은공백이나앞뒤공백메모도원문그대로전송하고성공때만비운다(string note)
    {
        await using var fixture = await Fixture.CreateAsync(); await fixture.NoteAsync(note);
        Assert.Equal(note, await fixture.InputAsync()); await fixture.SaveAsync();
        var payload = Assert.Single(fixture.Http.Decisions); Assert.Equal(note, payload.AdminNote); AssertOriginalWork(payload);
        Assert.Equal(string.Empty, await fixture.BoundNoteAsync()); Assert.Equal(string.Empty, await fixture.InputAsync());
    }

    private static void AssertOriginalWork(DomesticCargoDispatchAIReviewDecisionRequest payload)
    {
        Assert.Equal("BUNDLE-MEMO", payload.BundleKey); Assert.Equal(new[] { "REQUEST-A", "REQUEST-B" }, payload.RequestIds);
        Assert.Equal("DRIVER-A", payload.DriverId); Assert.Equal("운영자승인", payload.DecisionType);
        Assert.True(payload.Accepted); Assert.False(payload.ManualBundle);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public IsolatedHttp Http { get; } = new();
        private readonly ServiceProvider services;
        private readonly MemoRenderer renderer;
        private readonly DomesticCargoDispatchAIReview page = new();
        private readonly List<Task> saving = [];
        private HtmlRootComponent rendered;
        private Fixture()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminData:UseMemory"] = "false", ["AdminAuth:DevelopmentBootstrap:Enabled"] = "true",
                ["AdminAuth:DevelopmentBootstrap:AccessToken"] = IsolatedHttp.Token,
                ["AdminAuth:DevelopmentBootstrap:UserId"] = "synthetic-memo-operator",
                ["AdminAuth:DevelopmentBootstrap:Roles:0"] = "서버관리자"
            }).Build();
            var registrations = new ServiceCollection(); registrations.AddLogging(); registrations.AddMudServices();
            registrations.AddSingleton<IConfiguration>(config); registrations.AddSingleton<IWebHostEnvironment, EnvironmentStub>();
            registrations.AddSingleton<IJSRuntime, NoJavaScript>(); registrations.AddSingleton<NavigationManager, NavigationStub>();
            registrations.AddSingleton<관리자인증세션Service>();
            registrations.AddSingleton(new HttpClient(Http, false) { BaseAddress = new Uri("http://fixture.invalid/") });
            registrations.AddSingleton<DomesticCargoDispatchAIReviewAdminService>();
            services = registrations.BuildServiceProvider(); renderer = new(services, services.GetRequiredService<ILoggerFactory>());
            foreach (var property in typeof(DomesticCargoDispatchAIReview).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null))
                property.SetValue(page, services.GetRequiredService(property.PropertyType));
        }
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            try
            {
                await fixture.renderer.Dispatcher.InvokeAsync(async () =>
                {
                    var providers = fixture.renderer.BeginRenderingComponent(new Providers(), ParameterView.Empty); await providers.QuiescenceTask;
                    fixture.rendered = fixture.renderer.BeginRenderingComponent(fixture.page, ParameterView.Empty); await fixture.rendered.QuiescenceTask;
                });
                Assert.Equal(1, fixture.Http.Reads); return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public Task NoteAsync(string value)
            => renderer.Dispatcher.InvokeAsync(() => renderer.Note.ValueChanged.InvokeAsync(value));
        public Task TypeAsync(string value) => renderer.Dispatcher.InvokeAsync(() => renderer.TypeNoteAsync(value));
        public Task<string> BoundNoteAsync() => renderer.Dispatcher.InvokeAsync(() => renderer.Note.Value ?? string.Empty);
        public Task<string> TextAsync() => renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(rendered.ToHtmlString()));
        public Task<string> InputAsync() => renderer.Dispatcher.InvokeAsync(() =>
        {
            var raw = rendered.ToHtmlString();
            var textarea = Assert.Single(Regex.Matches(raw, "<textarea\\b[^>]*>(.*?)</textarea>", RegexOptions.Singleline).Cast<Match>());
            var text = WebUtility.HtmlDecode(textarea.Groups[1].Value);
            return text;
        });
        public Task SaveAsync()
        {
            var task = renderer.Dispatcher.InvokeAsync(() => ((IHandleEvent)page).HandleEventAsync(new EventCallbackWorkItem((Func<Task>)(() =>
                (Task)(typeof(DomesticCargoDispatchAIReview).GetMethod("SaveDecisionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(page, null) ?? throw new InvalidOperationException("저장 이벤트 Task가 없습니다.")))), null));
            saving.Add(task); return task;
        }
        public async ValueTask DisposeAsync()
        {
            Http.ReleasePending(); await Task.WhenAll(saving);
            await renderer.DisposeAsync(); await services.DisposeAsync(); Http.Dispose();
        }
    }

    private sealed class MemoRenderer(IServiceProvider services, ILoggerFactory logger) : StaticHtmlRenderer(services, logger)
    {
        private readonly List<ComponentState> components = [];
        public MudTextField<string> Note => Assert.Single(components.Select(state => state.Component).OfType<MudTextField<string>>(), input => input.Label == "판정 메모");
        protected override ComponentState CreateComponentState(int componentId, IComponent component, ComponentState? parentComponentState)
        {
            var state = base.CreateComponentState(componentId, component, parentComponentState); components.Add(state); return state;
        }
        public Task TypeNoteAsync(string value)
        {
            var note = Note;
            var input = (MudInput<string>)Assert.Single(components,
                state => state.Component is MudInput<string> && IsDescendantOf(state, note)).Component;
            var handler = typeof(MudInput<string>).GetMethod("OnInput", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(MudInput<string>).FullName, "OnInput");
            // 정적 SSR에는 DOM 이벤트 ID가 없다. 실제 입력 컴포넌트의 C# OnInput 경로를
            // IHandleEvent로 호출하며 blur·DOM·IME 동작 검증으로 표현하지 않는다.
            return ((IHandleEvent)input).HandleEventAsync(new EventCallbackWorkItem((Func<Task>)(() =>
                (Task)(handler.Invoke(input, [value])
                    ?? throw new InvalidOperationException("실제 MudInput.OnInput Task가 없습니다.")))), null);
        }
        private static bool IsDescendantOf(ComponentState state, IComponent ancestor)
        {
            for (var parent = state.ParentComponentState; parent is not null; parent = parent.ParentComponentState)
                if (ReferenceEquals(parent.Component, ancestor)) return true;
            return false;
        }
    }

    private sealed class PendingPost
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<HttpResponseMessage> Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete(HttpStatusCode status) => Response.TrySetResult(new(status)
        { Content = JsonContent.Create(new DomesticCargoDispatchAIReviewDecisionResponse { CaseId = "MEMO-CASE", Message = "격리 메모 응답" }) });
    }
    private sealed class IsolatedHttp : HttpMessageHandler
    {
        public const string Token = "operator-memo-synthetic";
        private const string Endpoint = "/api/v1/admin/dispatch/domestic-cargo-ai-review";
        private readonly Queue<PendingPost> pending = [];
        private readonly List<PendingPost> allPending = [];
        public int Reads { get; private set; }
        public int UnexpectedRequests { get; private set; }
        public List<DomesticCargoDispatchAIReviewDecisionRequest> Decisions { get; } = [];
        public PendingPost DeferPost() { var post = new PendingPost(); pending.Enqueue(post); allPending.Add(post); return post; }
        public void ReleasePending() { foreach (var post in allPending) post.Complete(HttpStatusCode.OK); }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri?.Host == "fixture.invalid" && request.Headers.Authorization?.Parameter == Token)
            {
                if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == Endpoint)
                {
                    Reads++; return new(HttpStatusCode.OK) { Content = JsonContent.Create(Workspace()) };
                }
                if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == Endpoint + "/decisions")
                {
                    Decisions.Add(await request.Content!.ReadFromJsonAsync<DomesticCargoDispatchAIReviewDecisionRequest>(token)
                        ?? throw new InvalidDataException("메모 판단 요청 본문이 없습니다."));
                    if (pending.TryDequeue(out var post)) { post.Started.TrySetResult(); return await post.Response.Task; }
                    return new(HttpStatusCode.OK) { Content = JsonContent.Create(new DomesticCargoDispatchAIReviewDecisionResponse { Message = "격리 즉시 응답" }) };
                }
            }
            UnexpectedRequests++; throw new InvalidOperationException("허용되지 않은 메모 격리 요청입니다.");
        }
        private static DomesticCargoDispatchAIReviewWorkspaceDto Workspace() => new()
        {
            Requests = [new() { RequestId = "REQUEST-A", CargoType = "가상 화물 A" }, new() { RequestId = "REQUEST-B", CargoType = "가상 화물 B" }],
            Drivers = [new() { DriverId = "DRIVER-A", DriverName = "가상 기사", VehicleType = "가상 차량" }],
            Bundles = [new() { BundleKey = "BUNDLE-MEMO", RequestIds = ["REQUEST-A", "REQUEST-B"], BundleSize = 2,
                IsAISuggested = true, IsBundleAvailable = true, SuggestedDriverId = "DRIVER-A" }]
        };
    }
    private sealed class NavigationStub : NavigationManager
    {
        public NavigationStub() => Initialize("http://fixture.invalid/", "http://fixture.invalid/dispatch/ai-review");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("메모 시험은 이동하지 않습니다.");
    }
    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SyntheticMemoTests";
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
