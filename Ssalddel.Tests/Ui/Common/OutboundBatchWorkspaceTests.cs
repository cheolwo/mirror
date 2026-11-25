using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Components.WarehouseOperations;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OutboundBatchWorkspaceTests
{
    [Theory]
    [InlineData("review", "출고 검토", "http://localhost/")]
    [InlineData("picking", "피킹", "http://localhost/")]
    [InlineData("packing", "포장", "http://localhost/")]
    [InlineData("handoff", "운송 인계", "http://localhost/")]
    [InlineData("review", "출고 검토", "http://localhost/roles/05/")]
    [InlineData("picking", "피킹", "http://localhost/roles/05/")]
    [InlineData("packing", "포장", "http://localhost/roles/05/")]
    [InlineData("handoff", "운송 인계", "http://localhost/roles/05/")]
    public async Task 단계주소는_같은창고구간의묶음표로직접진입하고_역할기준경로를유지한다(
        string stage, string label, string baseUri)
    {
        await using var fixture = new Fixture(baseUri);
        var state = State(stage) with { BatchPage = 1, PlanPage = 2 };

        await fixture.StartAsync(state);

        var read = Assert.Single(fixture.Api.PeriodReads);
        Assert.Equal(stage, read.PeriodStage);
        Assert.Equal(1, read.WarehouseId);
        Assert.Equal(state.StartUtc, read.PeriodStartUtc);
        Assert.Equal(state.EndUtc, read.PeriodEndUtc);
        Assert.Equal(1, read.BatchPage);
        Assert.Equal(2, read.Page);
        Assert.Contains(label + " 처리 묶음", await fixture.TextAsync());
        Assert.Equal(1, await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ElementCount("table", "class", "flow-table")));
        var href = await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.Href("BATCH-A 구성 내역 보기"));
        Assert.StartsWith(new Uri(baseUri).AbsolutePath, new Uri(new Uri(baseUri), href).AbsolutePath);
        Assert.Contains("batchKey=batch-a", href);
        Assert.Contains("stage=" + stage, href);
        Assert.Empty(fixture.Api.DetailIds);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("http://localhost/roles/05/")]
    public async Task 실제묶음선택과구성페이지와같은출고상세는_주소문맥을보존하며_원묶음표로돌아온다(string baseUri)
    {
        await using var fixture = new Fixture(baseUri);
        await fixture.StartAsync(State("packing") with { PlanPage = 2 });
        await fixture.ClickAsync("다음 묶음");
        Assert.Equal(1, fixture.Renderer.Harness.State.BatchPage);
        await fixture.ClickAsync("BATCH-A 구성 내역 보기");
        Assert.Equal("batch-a", fixture.Renderer.Harness.State.BatchKey);
        Assert.Contains("구성 상품 71", await fixture.TextAsync());
        await fixture.ClickAsync("취소 구성 상품 출고 검토");
        Assert.Empty(fixture.Api.DetailIds);
        Assert.Empty(fixture.Renderer.Harness.Selected);
        await fixture.ClickAsync("다음 내역");
        var selectedState = fixture.Renderer.Harness.State;
        Assert.Equal(1, selectedState.MemberPage);
        Assert.Contains("구성 상품 83", await fixture.TextAsync());
        Assert.DoesNotContain(fixture.Model.Snapshot!.Items, item => item.OutboundPlanId == 83);
        var periodReads = fixture.Api.PeriodReads.Count;

        await fixture.ClickAsync("구성 상품 83 출고 검토");

        Assert.Equal(new long[] { 83 }, fixture.Api.DetailIds);
        Assert.Equal(new long?[] { 83 }, fixture.Renderer.Harness.Selected);
        Assert.Equal(83, fixture.Renderer.Harness.OutboundPlanId);
        Assert.Contains("검토 상세 83", await fixture.TextAsync());
        Assert.DoesNotContain("일반 목록 전용 상품", await fixture.TextAsync());
        Assert.Contains("outboundPlanId=83", fixture.Navigation.Visited[^1]);
        Assert.StartsWith(baseUri, fixture.Navigation.Visited[^1]);

        await fixture.ClickAsync("묶음 구성 내역으로");

        Assert.Null(fixture.Renderer.Harness.OutboundPlanId);
        Assert.Equal(selectedState, fixture.Renderer.Harness.State);
        Assert.Equal(periodReads, fixture.Api.PeriodReads.Count);
        Assert.Contains("구성 상품 83", await fixture.TextAsync());
        await fixture.ClickAsync("묶음 목록으로");
        Assert.Null(fixture.Renderer.Harness.State.BatchKey);
        Assert.Equal(1, fixture.Renderer.Harness.State.BatchPage);
        Assert.Equal(0, fixture.Renderer.Harness.State.MemberPage);
        Assert.Equal(2, fixture.Renderer.Harness.State.PlanPage);
        Assert.Equal(1, await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ElementCount("table", "class", "flow-table")));
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 뒤로가기와같은매개변수재적용은_이전공정과묶음구성페이지를복원한다()
    {
        await using var fixture = new Fixture("http://localhost/roles/05/");
        var previous = State("picking") with { BatchKey = "batch-a", BatchPage = 1, MemberPage = 1, PlanPage = 2 };
        await fixture.StartAsync(previous);
        Assert.Contains("구성 상품 83", await fixture.TextAsync());
        await fixture.ReplayAsync(State("handoff"));
        Assert.Contains("운송 인계 처리 묶음", await fixture.TextAsync());

        await fixture.ReplayAsync(previous);

        Assert.Equal(previous, fixture.Renderer.Harness.State);
        Assert.Equal("picking", fixture.Api.PeriodReads[^1].PeriodStage);
        Assert.Equal("batch-a", fixture.Api.PeriodReads[^1].BatchKey);
        Assert.Equal(1, fixture.Api.PeriodReads[^1].MemberPage);
        Assert.Equal(2, fixture.Api.PeriodReads[^1].Page);
        Assert.Contains("구성 상품 83", await fixture.TextAsync());
        Assert.DoesNotContain("검토 상세", await fixture.TextAsync());
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Theory]
    [InlineData("missing-group", 71)]
    [InlineData("unlisted-plan", 11)]
    [InlineData("cancelled-plan", 72)]
    [InlineData("detail-mismatch", 71)]
    public async Task 잘못된묶음과구성원또는다른상세응답은_개별출고내용을표시하지않는다(string scenario, long planId)
    {
        await using var fixture = new Fixture();
        if (scenario == "detail-mismatch") fixture.Api.DetailIdOverride = 999;
        var state = State("picking") with { BatchKey = scenario == "missing-group" ? "missing" : "batch-a" };

        await fixture.StartAsync(state, planId);

        var text = await fixture.TextAsync();
        Assert.DoesNotContain("검토 상세 71", text);
        Assert.DoesNotContain("다른 출고의 비공개 상세", text);
        Assert.Contains("구성 내역으로 돌아가 다시 선택", text);
        if (scenario == "detail-mismatch") Assert.Equal(new long[] { 71 }, fixture.Api.DetailIds);
        else Assert.Empty(fixture.Api.DetailIds);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("session")]
    [InlineData("server")]
    public async Task 구성원상세조회중문맥변경은_늦은내용과선택주소를노출하지않는다(string change)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State("picking") with { BatchKey = "batch-a" });
        fixture.Api.DetailGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var selecting = fixture.ClickAsync("구성 상품 71 출고 검토");
        await fixture.Api.DetailStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.ChangeContext(change));
        fixture.Api.DetailGate.SetResult(Api.Detail(71));
        await selecting;
        await fixture.DrainAsync();

        Assert.Empty(fixture.Renderer.Harness.Selected);
        Assert.DoesNotContain("검토 상세 71", await fixture.TextAsync());
        Assert.Null(fixture.Model.Snapshot);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 느린개별상세뒤단계주소가바뀌면_새묶음표와주소를이전선택으로덮지않는다()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State("picking") with { BatchKey = "batch-a" });
        fixture.Api.DetailGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var selecting = fixture.ClickAsync("구성 상품 71 출고 검토");
        await fixture.Api.DetailStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.ReplayAsync(State("packing"));
        fixture.Api.DetailGate.SetResult(Api.Detail(71));
        await selecting;
        await fixture.DrainAsync();

        Assert.Empty(fixture.Renderer.Harness.Selected);
        Assert.Equal("packing", fixture.Renderer.Harness.State.Stage);
        Assert.Null(fixture.Renderer.Harness.State.BatchKey);
        Assert.Contains("포장 처리 묶음", await fixture.TextAsync());
        Assert.DoesNotContain("검토 상세 71", await fixture.TextAsync());
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Theory]
    [InlineData("group-selection")]
    [InlineData("member-page")]
    [InlineData("group-list")]
    [InlineData("group-page")]
    [InlineData("query")]
    public async Task 이전묶음조회이벤트의늦은완료는_새단계에서연상세주소를목록으로되돌리지않는다(string action)
    {
        await using var fixture = new Fixture("http://localhost/roles/05/");
        var initial = State("picking") with
        {
            BatchKey = action is "member-page" or "group-list" ? "batch-a" : null,
            PlanPage = 2
        };
        await fixture.StartAsync(initial);
        var pending = fixture.Api.DeferNextPeriod();
        var oldEvent = action switch
        {
            "group-selection" => fixture.ClickAsync("BATCH-A 구성 내역 보기"),
            "member-page" => fixture.ClickAsync("다음 내역"),
            "group-list" => fixture.ClickAsync("묶음 목록으로"),
            "group-page" => fixture.ClickAsync("다음 묶음"),
            _ => fixture.SubmitQueryAsync()
        };
        try
        {
            await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var next = State("packing") with { BatchKey = "batch-a", BatchPage = 1, MemberPage = 1, PlanPage = 2 };
            await fixture.ReplayAsync(next).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(pending.Token.IsCancellationRequested);
            await fixture.ClickAsync("구성 상품 83 출고 검토").WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains("검토 상세 83", await fixture.TextAsync());
            var snapshot = fixture.Model.Snapshot;
            var navigationCount = fixture.Navigation.Visited.Count;
            var detailUrl = fixture.Navigation.Visited[^1];

            pending.Release();
            await oldEvent.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.DrainAsync();

            Assert.Same(snapshot, fixture.Model.Snapshot);
            Assert.Equal(next, fixture.Renderer.Harness.State);
            Assert.Equal(83, fixture.Renderer.Harness.OutboundPlanId);
            Assert.Equal(new long?[] { 83 }, fixture.Renderer.Harness.Selected);
            Assert.Equal(navigationCount, fixture.Navigation.Visited.Count);
            Assert.Equal(detailUrl, fixture.Navigation.Visited[^1]);
            Assert.Contains("outboundPlanId=83", detailUrl);
            Assert.Contains("검토 상세 83", await fixture.TextAsync());
            Assert.Equal(0, fixture.Api.Writes);
        }
        finally
        {
            pending.Release();
            await oldEvent.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static OutboundBatchNavigationState State(string stage) => new(stage, 1,
        new(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc), new(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc));

    private sealed class Fixture : IAsyncDisposable
    {
        public readonly Api Api = new();
        public readonly User User = new();
        public readonly Scope Recovery;
        public readonly List<출고회차ViewModel> Models = [];
        public ServiceProvider Services { get; }
        public BatchRenderer Renderer { get; }
        public Navigation Navigation { get; }
        public 출고회차ViewModel Model => Assert.Single(Models);

        public Fixture(string baseUri = "http://localhost/")
        {
            Recovery = new(User);
            Navigation = new(baseUri);
            var services = new ServiceCollection();
            services.AddLogging(); services.AddMudServices();
            services.AddSingleton<IJSRuntime, NoopJs>();
            services.AddSingleton<NavigationManager>(Navigation);
            services.AddSingleton<ISsalddelJsonApiClient>(Api);
            services.AddSingleton<I출고예정검토페이지Service>(Api);
            services.AddSingleton<ISsalddel현재사용자Context>(User);
            services.AddSingleton<IWorkflowRecoveryContext>(Recovery);
            services.AddSsalddelUiCommonAppServices();
            services.AddTransient(provider =>
            {
                var model = ActivatorUtilities.CreateInstance<출고회차ViewModel>(provider);
                Models.Add(model);
                return model;
            });
            Services = services.BuildServiceProvider();
            Renderer = new(Services, Services.GetRequiredService<ILoggerFactory>());
        }

        public async Task StartAsync(OutboundBatchNavigationState state, long? planId = null)
        {
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Start(state, planId));
            await DrainAsync();
        }
        public async Task ReplayAsync(OutboundBatchNavigationState state, long? planId = null)
        {
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Replay(state, planId));
            await DrainAsync();
        }
        public async Task ClickAsync(string text)
        {
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Click(text));
            await DrainAsync();
        }
        public async Task SubmitQueryAsync()
        {
            await Renderer.Dispatcher.InvokeAsync(Renderer.SubmitQuery);
            await DrainAsync();
        }
        public Task<string> TextAsync() => Renderer.Dispatcher.InvokeAsync(Renderer.VisibleText);
        public void ChangeContext(string change)
        {
            if (change == "owner") User.Switch("other");
            else if (change == "session") { Recovery.Revision++; User.Switch("worker"); }
            else Recovery.Endpoint = new('b', 32);
        }
        public async Task DrainAsync()
        {
            for (var index = 0; index < 4; index++) await Renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
            Assert.Empty(Renderer.Errors);
        }
        public async ValueTask DisposeAsync() { await Renderer.DisposeAsync(); await Services.DisposeAsync(); }
    }

    public sealed class HostHarness : ComponentBase
    {
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Parameter] public OutboundBatchNavigationState State { get; set; } = new("picking");
        [Parameter] public long? OutboundPlanId { get; set; }
        public readonly List<long?> Selected = [];
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudPopoverProvider>(0); builder.CloseComponent();
            builder.OpenComponent<SsalddelOutboundBatchWorkspace>(1);
            builder.AddAttribute(2, nameof(SsalddelOutboundBatchWorkspace.State), State);
            builder.AddAttribute(3, nameof(SsalddelOutboundBatchWorkspace.OutboundPlanId), OutboundPlanId);
            builder.AddAttribute(4, nameof(SsalddelOutboundBatchWorkspace.OnNavigationChanged), EventCallback.Factory.Create<OutboundBatchNavigationState>(this, Navigate));
            builder.AddAttribute(5, nameof(SsalddelOutboundBatchWorkspace.OnPlanSelected), EventCallback.Factory.Create<long?>(this, Select));
            builder.CloseComponent();
        }
        private void Navigate(OutboundBatchNavigationState state)
        {
            State = state; OutboundPlanId = null;
            Navigation.NavigateTo(state.Href().TrimStart('/'));
        }
        private void Select(long? id)
        {
            Selected.Add(id); OutboundPlanId = id;
            Navigation.NavigateTo(State.Href(id).TrimStart('/'));
        }
    }

    private sealed class User : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        public 현재사용자Snapshot 현재사용자 { get; private set; } = new("worker", "가상 담당", []);
        public event Action? Changed;
        public void Switch(string id) { 현재사용자 = new(id, "가상 담당", []); Changed?.Invoke(); }
    }
    private sealed class Scope(User user) : IWorkflowRecoveryContext
    {
        public long Revision = 1;
        public string Endpoint = new('a', 32);
        public WorkflowRecoverySnapshot Capture() => new(user.현재사용자.UserId!, Revision, Endpoint);
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => snapshot == Capture();
    }
    private sealed class Api : I출고예정검토페이지Service, ISsalddelJsonApiClient
    {
        public readonly List<출고예정검토목록조회요청> PeriodReads = [];
        public readonly List<long> DetailIds = [];
        public long? DetailIdOverride;
        public TaskCompletionSource<출고예정검토상세응답?>? DetailGate;
        public readonly TaskCompletionSource DetailStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Queue<PendingPeriod> pendingPeriods = [];
        public int Writes;
        public PendingPeriod DeferNextPeriod()
        {
            var pending = new PendingPeriod();
            pendingPeriods.Enqueue(pending);
            return pending;
        }
        public Task<출고예정검토목록페이지응답> 목록조회Async(출고예정검토목록조회요청 request, CancellationToken cancellationToken = default)
        {
            if (request.PeriodStartUtc is null) return Task.FromResult(new 출고예정검토목록페이지응답());
            PeriodReads.Add(request);
            if (request.BatchKey == "missing") return Task.FromException<출고예정검토목록페이지응답>(new SsalddelApiException("missing", 404, "read", "", null));
            var reply = new 출고예정검토목록페이지응답
            {
                Period = new()
                {
                    WarehouseId = request.WarehouseId!.Value, WarehouseName = "가상 창고",
                    PeriodStartUtc = request.PeriodStartUtc.Value, PeriodEndUtc = request.PeriodEndUtc!.Value,
                    ObservedAtUtc = new(2026, 11, 7, 2, 0, 0, DateTimeKind.Utc),
                    Page = request.Page, PageSize = request.PageSize, TotalCount = 36,
                    Items = [new() { OutboundPlanId = 11, ProductName = "일반 목록 전용 상품", CanOpenReview = true }],
                    Workflow = new()
                    {
                        SelectedStage = request.PeriodStage!, Page = request.BatchPage, PageSize = request.BatchPageSize,
                        TotalGroups = 13, Groups = [Group(0)], SelectedGroupKey = request.BatchKey,
                        SelectedGroup = request.BatchKey is null ? null : Group(request.MemberPage),
                        Stages = [new() { Code = request.PeriodStage!, Label = "공정 현황", SourcePlanCount = 2, TaskCount = 2, WorkQuantity = 12, CompletedQuantity = 4 }]
                    }
                }
            };
            if (pendingPeriods.Count == 0) return Task.FromResult(reply);
            var pending = pendingPeriods.Dequeue();
            pending.Reply = reply;
            pending.Token = cancellationToken;
            pending.Started.TrySetResult();
            return pending.Completion.Task;
        }
        private static OutboundPeriodGroup Group(int page) => new()
        {
            Key = "batch-a", Label = "BATCH-A", Kind = "recorded-batch", SourcePlanCount = 2,
            TaskCount = 2, Quantity = 12, CompletedQuantity = 4, MembershipComplete = true,
            MemberCount = 24, MemberPage = page, MemberPageSize = 12,
            Members = [new() { OutboundPlanId = 71 + page * 12, ProductName = "구성 상품 " + (71 + page * 12), Quantity = 8, Status = "진행중", CanOpenReview = true },
                new() { OutboundPlanId = 72 + page * 12, ProductName = "취소 구성 상품", Quantity = 4, Status = "취소", CanOpenReview = false }]
        };
        public Task<출고예정검토상세응답?> 상세조회Async(long outboundPlanId, CancellationToken cancellationToken = default)
        {
            DetailIds.Add(outboundPlanId); DetailStarted.TrySetResult();
            return DetailGate?.Task ?? Task.FromResult<출고예정검토상세응답?>(Detail(DetailIdOverride ?? outboundPlanId));
        }
        public static 출고예정검토상세응답 Detail(long id) => new()
        {
            OutboundPlanId = id, ProductName = id == 999 ? "다른 출고의 비공개 상세" : "검토 상세 " + id,
            WarehouseId = 1, WarehouseName = "가상 창고", Quantity = 8, OutboundStatus = "준비중", ReviewStatus = "운송 연결됨"
        };
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        {
            Assert.Equal("api/v1/warehouse-operations/warehouses", path);
            Assert.False(allowNotFound);
            return Task.FromResult((TResponse?)(object)new 창고목록응답 { Items = [new() { Id = 1, 창고명 = "가상 창고", IsActive = true }] });
        }
        public Task<출고운송인계완료응답> 인계완료Async(long id, 출고운송인계완료요청 request, CancellationToken cancellationToken = default) => Write<출고운송인계완료응답>();
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => Write<object>();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default) => Write<object>();
        private Task<T> Write<T>() { Writes++; throw new InvalidOperationException("처리 묶음 조회에서 업무 명령을 실행하면 안 됩니다."); }
    }
    private sealed class PendingPeriod
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<출고예정검토목록페이지응답> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public 출고예정검토목록페이지응답? Reply;
        public CancellationToken Token;
        public void Release()
        {
            if (Reply is { } reply) Completion.TrySetResult(reply);
            else Completion.TrySetCanceled();
        }
    }
    private sealed class Navigation : NavigationManager
    {
        public readonly List<string> Visited = [];
        public Navigation(string baseUri) => Initialize(baseUri, baseUri + "warehouse/general/outbound-plan-review?stage=picking");
        protected override void NavigateToCore(string uri, bool forceLoad) => Visited.Add(ToAbsoluteUri(uri).AbsoluteUri);
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }

#pragma warning disable BL0006 // 실제 컴포넌트의 이벤트와 매개변수 수명을 서버 없이 확인한다.
    private sealed class BatchRenderer : Renderer
    {
        private int root;
        public HostHarness Harness { get; private set; } = default!;
        public readonly List<Exception> Errors = [];
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public BatchRenderer(IServiceProvider services, ILoggerFactory logger) : base(services, logger)
            => ElementReferenceContext = new WebElementReferenceContext(services.GetRequiredService<IJSRuntime>());
        protected override void HandleException(Exception exception) => Errors.Add(exception);
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        public Task Start(OutboundBatchNavigationState state, long? plan)
        {
            Harness = (HostHarness)InstantiateComponent(typeof(HostHarness));
            root = AssignRootComponentId(Harness);
            return Replay(state, plan);
        }
        public Task Replay(OutboundBatchNavigationState state, long? plan) => RenderRootComponentAsync(root,
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(HostHarness.State)] = state, [nameof(HostHarness.OutboundPlanId)] = plan }));
        public string VisibleText() { var text = new StringBuilder(); Append(root, text); return text.ToString(); }
        private void Append(int id, StringBuilder text)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames.Array[i];
                if (frame.FrameType == RenderTreeFrameType.Component) Append(frame.ComponentId, text);
                else if (frame.FrameType == RenderTreeFrameType.Text) text.Append(frame.TextContent);
                else if (frame.FrameType == RenderTreeFrameType.Markup) text.Append(frame.MarkupContent);
            }
        }
        public Task Click(string label)
        {
            var action = FindAction(root, label);
            if (action.Event == 0) throw new InvalidOperationException("실제 클릭 행동을 찾지 못했습니다. " + label);
            return DispatchEventAsync(action.Event, null, new MouseEventArgs());
        }
        public Task SubmitQuery()
        {
            var eventId = FindQueryForm(root);
            if (eventId == 0) throw new InvalidOperationException("실제 처리 묶음 조회 양식을 찾지 못했습니다.");
            return DispatchEventAsync(eventId, null, EventArgs.Empty);
        }
        private ulong FindQueryForm(int id)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component)
                {
                    var nested = FindQueryForm(frame.ComponentId);
                    if (nested != 0) return nested;
                }
                if (frame.FrameType != RenderTreeFrameType.Element || frame.ElementName != "form") continue;
                var attributes = frames.Array.Skip(index + 1).Take(frame.ElementSubtreeLength - 1)
                    .TakeWhile(item => item.FrameType == RenderTreeFrameType.Attribute).ToArray();
                if (!attributes.Any(item => item.AttributeName == "class" && item.AttributeValue as string == "batch-query")) continue;
                return attributes.FirstOrDefault(item => item.AttributeName == "onsubmit").AttributeEventHandlerId;
            }
            return 0;
        }
        public string Href(string label) => FindAction(root, label).Href ?? throw new InvalidOperationException("실제 링크가 없습니다. " + label);
        private (ulong Event, string? Href) FindAction(int id, string label)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component)
                {
                    var nested = FindAction(frame.ComponentId, label);
                    if (nested.Event != 0 || nested.Href is not null) return nested;
                }
                if (frame.FrameType != RenderTreeFrameType.Element || frame.ElementName is not ("button" or "a")) continue;
                var contents = frames.Array.Skip(index + 1).Take(frame.ElementSubtreeLength - 1).ToArray();
                var attributes = contents.TakeWhile(item => item.FrameType == RenderTreeFrameType.Attribute).ToArray();
                var aria = attributes.FirstOrDefault(item => item.AttributeName == "aria-label").AttributeValue as string;
                var text = new StringBuilder();
                foreach (var item in contents)
                {
                    if (item.FrameType == RenderTreeFrameType.Component) Append(item.ComponentId, text);
                    else if (item.FrameType == RenderTreeFrameType.Text) text.Append(item.TextContent);
                    else if (item.FrameType == RenderTreeFrameType.Markup) text.Append(item.MarkupContent);
                }
                if (aria != label && !text.ToString().Contains(label, StringComparison.Ordinal)) continue;
                return (attributes.FirstOrDefault(item => item.AttributeName == "onclick").AttributeEventHandlerId,
                    attributes.FirstOrDefault(item => item.AttributeName == "href").AttributeValue as string);
            }
            return default;
        }
        public int ElementCount(string name, string attribute, string value) => Count(root, name, attribute, value);
        private int Count(int id, string name, string attribute, string value)
        {
            var result = 0;
            var frames = GetCurrentRenderTreeFrames(id);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component) result += Count(frame.ComponentId, name, attribute, value);
                if (frame.FrameType != RenderTreeFrameType.Element || frame.ElementName != name) continue;
                if (frames.Array.Skip(index + 1).Take(frame.ElementSubtreeLength - 1)
                    .TakeWhile(item => item.FrameType == RenderTreeFrameType.Attribute)
                    .Any(item => item.AttributeName == attribute && item.AttributeValue as string == value)) result++;
            }
            return result;
        }
    }
#pragma warning restore BL0006
}
