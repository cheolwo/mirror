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

public sealed class OutboundBatchPageEmptyRecoveryTests
{
    private const string EmptyPage = "이 페이지에 표시할 처리 묶음이 없습니다.";
    private const string RecoveryHelp = "선택한 창고·조회 구간·공정은 유지하고 첫 페이지를 다시 확인해 주세요.";
    private const string RecoveryAction = "첫 묶음 페이지로";
    private const string WholeEmpty = "이 공정에서 조회할 처리 대상이 없습니다.";

    [Fact]
    public async Task 현재묶음페이지만비면_명시적으로첫묶음페이지를읽고_창고구간공정원출고페이지를보존한다()
    {
        await using var fixture = new Fixture();
        var initial = State() with { BatchPage = 2, PlanPage = 3 };
        await fixture.StartAsync(initial);

        var text = await fixture.TextAsync();
        Assert.Contains(EmptyPage, text);
        Assert.Contains(RecoveryHelp, text);
        Assert.DoesNotContain(WholeEmpty, text);
        Assert.Equal(2, fixture.Model.Snapshot!.Workflow!.TotalGroups);
        Assert.Empty(fixture.Model.Snapshot.Workflow.Groups);
        Assert.Single(fixture.Api.Reads);

        await fixture.ClickAsync(RecoveryAction);

        Assert.Equal(new[] { 2, 0 }, fixture.Api.Reads.Select(read => read.BatchPage));
        Assert.All(fixture.Api.Reads, read => AssertQuery(read, initial));
        Assert.Equal(initial with { BatchPage = 0 }, fixture.Renderer.Harness.State);
        Assert.Single(fixture.Renderer.Harness.Published);
        Assert.Equal(0, fixture.Model.BatchPage);
        Assert.Equal(3, fixture.Model.PlanPage);
        Assert.Contains("동부 출고묶음 · 피킹", await fixture.TextAsync());
        Assert.DoesNotContain(EmptyPage, await fixture.TextAsync());
        Assert.Empty(fixture.Api.DetailIds);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 최대정수묶음페이지도_음수로넘치지않고_첫페이지복구를제공한다()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State() with { BatchPage = int.MaxValue });

        var text = await fixture.TextAsync();
        Assert.Contains("2147483648페이지", text);
        Assert.DoesNotContain("-2147483648페이지", text);
        Assert.Contains(EmptyPage, text);
        Assert.Equal(int.MaxValue, Assert.Single(fixture.Api.Reads).BatchPage);

        await fixture.ClickAsync(RecoveryAction);
        Assert.Equal(new[] { int.MaxValue, 0 }, fixture.Api.Reads.Select(read => read.BatchPage));
        Assert.Equal(0, fixture.Renderer.Harness.State.BatchPage);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 정상묶음표는_기존식별수량과구성진입을표시하고_페이지복구를추가하지않는다()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State());

        var text = await fixture.TextAsync();
        Assert.Contains("동부 출고묶음 · 피킹", text);
        Assert.Contains("40개", text);
        Assert.Contains("34개", text);
        Assert.Contains("구성 내역 보기", text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.DoesNotContain(WholeEmpty, text);
        Assert.Equal(1, await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ElementCount("table")));
        Assert.Single(fixture.Api.Reads);
        Assert.Empty(fixture.Api.DetailIds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task 전체묶음0건은_기존공정빈상태를유지하고_첫페이지복구로설명하지않는다(int page)
    {
        await using var fixture = new Fixture();
        fixture.Api.TotalGroups = 0;
        await fixture.StartAsync(State() with { BatchPage = page });

        var text = await fixture.TextAsync();
        Assert.Contains(WholeEmpty, text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.Single(fixture.Api.Reads);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 첫페이지도항목만비었으면_자동추가조회없이_명시한번만다시읽는다()
    {
        await using var fixture = new Fixture();
        fixture.Api.EmptyFirstPage = true;
        await fixture.StartAsync(State());

        Assert.Contains(EmptyPage, await fixture.TextAsync());
        Assert.Single(fixture.Api.Reads);
        await fixture.ClickAsync(RecoveryAction);

        Assert.Equal(new[] { 0, 0 }, fixture.Api.Reads.Select(read => read.BatchPage));
        Assert.Contains(EmptyPage, await fixture.TextAsync());
        Assert.DoesNotContain(WholeEmpty, await fixture.TextAsync());
        Assert.Single(fixture.Renderer.Harness.Published);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 첫조회중에는_정상빈페이지안내나복구행동을먼저표시하지않는다()
    {
        await using var fixture = new Fixture();
        var pending = fixture.Api.Pending = new();
        var starting = fixture.StartAsync(State() with { BatchPage = 2 });
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var text = await fixture.TextAsync();
            Assert.Contains("선택한 단계의 처리 묶음을 확인하고 있습니다.", text);
            Assert.DoesNotContain(EmptyPage, text);
            Assert.DoesNotContain(RecoveryAction, text);
            Assert.DoesNotContain(WholeEmpty, text);
            Assert.Null(fixture.Model.Snapshot);
            Assert.Single(fixture.Api.Reads);
        }
        finally
        {
            pending.Release();
            await starting;
        }
        Assert.Contains(EmptyPage, await fixture.TextAsync());
    }

    [Fact]
    public async Task 이전빈결과를유지하며재조회중이면_새빈안내를숨기고_기존페이지행동을잠근다()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State() with { BatchPage = 2 });
        var previous = fixture.Model.Snapshot;
        var pending = fixture.Api.Pending = new();
        var reloading = fixture.SubmitQueryAsync();
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var text = await fixture.TextAsync();
            Assert.Same(previous, fixture.Model.Snapshot);
            Assert.True(fixture.Model.IsLoading);
            Assert.DoesNotContain(EmptyPage, text);
            Assert.DoesNotContain(RecoveryAction, text);
            Assert.DoesNotContain(WholeEmpty, text);
            Assert.True(await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ButtonDisabled("이전 묶음")));
            Assert.True(await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ButtonDisabled("다음 묶음")));
            Assert.Equal(2, fixture.Api.Reads.Count);
        }
        finally
        {
            pending.Release();
            await reloading;
        }
        Assert.Contains(EmptyPage, await fixture.TextAsync());
    }

    [Fact]
    public async Task 같은조회503은_이전빈결과를남겨도_정상빈페이지복구를오류위에표시하지않는다()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State() with { BatchPage = 2 });
        var previous = fixture.Model.Snapshot;
        fixture.Api.FailStatus = 503;

        await fixture.SubmitQueryAsync();

        var text = await fixture.TextAsync();
        Assert.Same(previous, fixture.Model.Snapshot);
        Assert.NotNull(fixture.Model.Error);
        Assert.NotNull(fixture.Model.Warning);
        Assert.Contains(fixture.Model.Error, text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.DoesNotContain(WholeEmpty, text);
        Assert.Equal(2, fixture.Api.Reads.Count);
        Assert.Empty(fixture.Renderer.Harness.Published);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 인증권한실패는_이전빈결과를제거하고_정상빈페이지복구를표시하지않는다(int status)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State() with { BatchPage = 2 });
        fixture.Api.FailStatus = status;

        await fixture.SubmitQueryAsync();

        Assert.Null(fixture.Model.Snapshot);
        Assert.NotNull(fixture.Model.Error);
        var text = await fixture.TextAsync();
        Assert.Contains(fixture.Model.Error, text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.DoesNotContain(WholeEmpty, text);
        Assert.Equal(2, fixture.Api.Reads.Count);
        Assert.Empty(fixture.Renderer.Harness.Published);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 회차카드표시모드는_정상묶음과현재빈페이지에_새웹단계안내를추가하지않는다(bool empty)
    {
        await using var fixture = new Fixture();
        await fixture.StartPanelAsync(State() with { BatchPage = empty ? 2 : 0 }, stageEntry: false);

        var text = await fixture.TextAsync();
        Assert.Contains("공정별 작업 흐름", text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryHelp, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.DoesNotContain(WholeEmpty, text);
        if (empty) Assert.Contains("3페이지 · 전체 2개", text);
        else
        {
            Assert.Contains("동부 출고묶음 · 피킹", text);
            Assert.Contains("구성 내역 보기", text);
            Assert.Contains("40개", text);
        }
        Assert.Equal(0, await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ElementCount("table")));
        Assert.Single(fixture.Api.Reads);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 기존이전묶음행동도_한페이지씩이동하며_원조회문맥을보존한다()
    {
        await using var fixture = new Fixture();
        var initial = State() with { BatchPage = 2, PlanPage = 3 };
        await fixture.StartAsync(initial);
        Assert.False(await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ButtonDisabled("이전 묶음")));
        Assert.True(await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ButtonDisabled("다음 묶음")));

        await fixture.ClickAsync("이전 묶음");
        await fixture.ClickAsync("이전 묶음");

        Assert.Equal(new[] { 2, 1, 0 }, fixture.Api.Reads.Select(read => read.BatchPage));
        Assert.All(fixture.Api.Reads, read => AssertQuery(read, initial));
        Assert.Equal(initial with { BatchPage = 0 }, fixture.Renderer.Harness.State);
        Assert.Equal(2, fixture.Renderer.Harness.Published.Count);
        Assert.Contains("동부 출고묶음 · 피킹", await fixture.TextAsync());
        Assert.Empty(fixture.Api.DetailIds);
        Assert.Equal(0, fixture.Api.Writes);
    }

    private static OutboundBatchNavigationState State(string stage = OutboundWorkflowStageCodes.Picking)
        => new(stage, 101, new(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), new(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc));

    private static void AssertQuery(출고예정검토목록조회요청 read, OutboundBatchNavigationState state)
    {
        Assert.Equal(state.WarehouseId, read.WarehouseId);
        Assert.Equal(state.StartUtc, read.PeriodStartUtc);
        Assert.Equal(state.EndUtc, read.PeriodEndUtc);
        Assert.Equal(state.Stage, read.PeriodStage);
        Assert.Equal(state.PlanPage, read.Page);
        Assert.Equal(출고예정검토조회상태코드.전체, read.Status);
        Assert.Null(read.BatchKey);
        Assert.Equal(0, read.MemberPage);
        Assert.Equal(6, read.BatchPageSize);
        Assert.Equal(12, read.PageSize);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly Api Api = new();
        internal readonly User User = new();
        internal readonly Scope Recovery;
        internal readonly List<출고회차ViewModel> Models = [];
        internal readonly ServiceProvider Services;
        internal readonly BatchRenderer Renderer;
        internal 출고회차ViewModel Model => Assert.Single(Models);

        internal Fixture()
        {
            Recovery = new(User);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton<IJSRuntime, NoopJs>();
            services.AddSingleton<NavigationManager>(new Navigation());
            services.AddSingleton<ISsalddelJsonApiClient>(Api);
            services.AddSingleton<I출고예정검토페이지Service>(Api);
            services.AddSingleton<ISsalddel현재사용자Context>(User);
            services.AddSingleton<IWorkflowRecoveryContext>(Recovery);
            services.AddTransient(provider =>
            {
                var model = ActivatorUtilities.CreateInstance<출고회차ViewModel>(provider);
                Models.Add(model);
                return model;
            });
            Services = services.BuildServiceProvider();
            Renderer = new(Services, Services.GetRequiredService<ILoggerFactory>());
        }

        internal async Task StartAsync(OutboundBatchNavigationState state)
        {
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Start(state));
            await DrainAsync();
        }
        internal async Task ClickAsync(string label)
        {
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Click(label));
            await DrainAsync();
        }
        internal async Task StartPanelAsync(OutboundBatchNavigationState state, bool stageEntry)
        {
            await Renderer.Dispatcher.InvokeAsync(async () =>
            {
                var model = Services.GetRequiredService<출고회차ViewModel>();
                await model.InitializeAsync();
                await model.LoadNavigationAsync(state);
                await Renderer.StartPanel(model, stageEntry);
            });
            await DrainAsync();
        }
        internal async Task SubmitQueryAsync()
        {
            await Renderer.Dispatcher.InvokeAsync(Renderer.SubmitQuery);
            await DrainAsync();
        }
        internal Task<string> TextAsync() => Renderer.Dispatcher.InvokeAsync(Renderer.VisibleText);
        internal async Task DrainAsync()
        {
            for (var index = 0; index < 4; index++) await Renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
            Assert.Empty(Renderer.Errors);
        }
        public async ValueTask DisposeAsync()
        {
            await Renderer.DisposeAsync();
            await Services.DisposeAsync();
        }
    }

    public sealed class HostHarness : ComponentBase
    {
        [Parameter] public OutboundBatchNavigationState State { get; set; } = new("picking");
        [Parameter] public 출고회차ViewModel? PanelModel { get; set; }
        [Parameter] public bool PanelStageEntry { get; set; }
        internal List<OutboundBatchNavigationState> Published { get; } = [];
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            if (PanelModel is not null)
            {
                builder.OpenComponent<SsalddelOutboundWorkflowPanel>(1);
                builder.AddAttribute(2, nameof(SsalddelOutboundWorkflowPanel.Model), PanelModel);
                builder.AddAttribute(3, nameof(SsalddelOutboundWorkflowPanel.StageEntry), PanelStageEntry);
                builder.CloseComponent();
                return;
            }
            builder.OpenComponent<SsalddelOutboundBatchWorkspace>(1);
            builder.AddAttribute(2, nameof(SsalddelOutboundBatchWorkspace.State), State);
            builder.AddAttribute(3, nameof(SsalddelOutboundBatchWorkspace.OnNavigationChanged),
                EventCallback.Factory.Create<OutboundBatchNavigationState>(this, Navigate));
            builder.CloseComponent();
        }
        private void Navigate(OutboundBatchNavigationState state)
        {
            State = state;
            Published.Add(state);
        }
    }

    private sealed class User : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        public 현재사용자Snapshot 현재사용자 { get; private set; } = new("warehouse-worker", "가상 출고 담당", []);
        public event Action? Changed;
        internal void Switch(string? id)
        {
            현재사용자 = id is null ? 현재사용자Snapshot.익명 : new(id, "다른 가상 담당", []);
            Changed?.Invoke();
        }
    }

    private sealed class Scope(User user) : IWorkflowRecoveryContext
    {
        public WorkflowRecoverySnapshot Capture() => new(user.현재사용자.UserId!, 1, new string('a', 32));
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => snapshot == Capture();
    }

    private sealed class Api : I출고예정검토페이지Service, ISsalddelJsonApiClient
    {
        internal List<출고예정검토목록조회요청> Reads { get; } = [];
        internal List<long> DetailIds { get; } = [];
        internal int Writes;
        internal int TotalGroups = 2;
        internal bool EmptyFirstPage;
        internal int? FailStatus;
        internal PendingRead? Pending;

        public Task<출고예정검토목록페이지응답> 목록조회Async(출고예정검토목록조회요청 request, CancellationToken cancellationToken = default)
        {
            Reads.Add(request);
            if (FailStatus is { } status)
                return Task.FromException<출고예정검토목록페이지응답>(new SsalddelApiException("isolated read failure", status, "read", "", null));
            OutboundPeriodGroup[] groups = TotalGroups == 0 || request.BatchPage != 0 || EmptyFirstPage
                ? Array.Empty<OutboundPeriodGroup>() : [Group()];
            var response = new 출고예정검토목록페이지응답
            {
                Period = new()
                {
                    WarehouseId = request.WarehouseId!.Value,
                    WarehouseName = "동부 가상 창고",
                    PeriodStartUtc = request.PeriodStartUtc!.Value,
                    PeriodEndUtc = request.PeriodEndUtc!.Value,
                    ObservedAtUtc = new(2026, 11, 7, 3, 10, 0, DateTimeKind.Utc),
                    Page = request.Page, PageSize = request.PageSize, TotalCount = TotalGroups == 0 ? 0 : 2,
                    Workflow = new()
                    {
                        SelectedStage = request.PeriodStage!,
                        Page = request.BatchPage, PageSize = request.BatchPageSize, TotalGroups = TotalGroups,
                        Groups = groups,
                        Stages = [new()
                        {
                            Code = request.PeriodStage!, Label = "선택 공정", SourcePlanCount = TotalGroups == 0 ? 0 : 2,
                            TaskCount = TotalGroups == 0 ? 0 : 3, WorkQuantity = TotalGroups == 0 ? 0 : 40,
                            CompletedQuantity = TotalGroups == 0 ? 0 : 34, RecordedBatchCount = TotalGroups
                        }]
                    }
                }
            };
            if (Pending is not { } pending) return Task.FromResult(response);
            Pending = null;
            pending.Response = response;
            pending.Token = cancellationToken;
            pending.Started.TrySetResult();
            return pending.Completion.Task.WaitAsync(cancellationToken);
        }
        private static OutboundPeriodGroup Group() => new()
        {
            Key = "picking:batch:501", Label = "동부 출고묶음 · 피킹", Kind = "recorded-batch",
            SourcePlanCount = 2, TaskCount = 3, Quantity = 40, CompletedQuantity = 34,
            MembershipComplete = true, MemberCount = 3, MemberPageSize = 12
        };
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        {
            Assert.Equal("api/v1/warehouse-operations/warehouses", path);
            Assert.False(allowNotFound);
            return Task.FromResult((TResponse?)(object)new 창고목록응답
            {
                Items = [new() { Id = 101, 창고명 = "동부 가상 창고", IsActive = true }]
            });
        }
        public Task<출고예정검토상세응답?> 상세조회Async(long outboundPlanId, CancellationToken cancellationToken = default)
        {
            DetailIds.Add(outboundPlanId);
            throw new InvalidOperationException("묶음 페이지 복구는 상세를 자동 선택하지 않습니다.");
        }
        public Task<출고운송인계완료응답> 인계완료Async(long id, 출고운송인계완료요청 request, CancellationToken cancellationToken = default) => Write<출고운송인계완료응답>();
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => Write<object>();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default) => Write<object>();
        private Task<T> Write<T>() { Writes++; throw new InvalidOperationException("읽기 복구에서 업무 쓰기를 실행하면 안 됩니다."); }
    }

    private sealed class PendingRead
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<출고예정검토목록페이지응답> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal 출고예정검토목록페이지응답? Response;
        internal CancellationToken Token;
        internal void Release() => Completion.TrySetResult(Response ?? throw new InvalidOperationException("읽기 시작 전에는 응답하지 않습니다."));
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/roles/05/", "http://localhost/roles/05/warehouse/general/outbound-plan-review?stage=picking");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }

#pragma warning disable BL0006 // 실제 컴포넌트의 등록 이벤트와 수명주기를 검사하며 DOM 조작과 구별한다.
    private sealed class BatchRenderer : Renderer
    {
        private int root;
        internal HostHarness Harness { get; private set; } = default!;
        internal List<Exception> Errors { get; } = [];
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        internal BatchRenderer(IServiceProvider services, ILoggerFactory logger) : base(services, logger)
            => ElementReferenceContext = new WebElementReferenceContext(services.GetRequiredService<IJSRuntime>());
        protected override void HandleException(Exception exception) => Errors.Add(exception);
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        internal Task Start(OutboundBatchNavigationState state)
        {
            Harness = (HostHarness)InstantiateComponent(typeof(HostHarness));
            root = AssignRootComponentId(Harness);
            return RenderRootComponentAsync(root, ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(HostHarness.State)] = state }));
        }
        internal Task StartPanel(출고회차ViewModel model, bool stageEntry)
        {
            Harness = (HostHarness)InstantiateComponent(typeof(HostHarness));
            root = AssignRootComponentId(Harness);
            return RenderRootComponentAsync(root, ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(HostHarness.PanelModel)] = model,
                [nameof(HostHarness.PanelStageEntry)] = stageEntry
            }));
        }
        internal string VisibleText() { var result = new StringBuilder(); Append(root, result); return result.ToString(); }
        private void Append(int id, StringBuilder text)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component) Append(frame.ComponentId, text);
                else if (frame.FrameType == RenderTreeFrameType.Text) text.Append(frame.TextContent);
                else if (frame.FrameType == RenderTreeFrameType.Markup) text.Append(frame.MarkupContent);
            }
        }
        internal Task Click(string label)
        {
            var action = Find(root, label);
            if (action.Event == 0) throw new InvalidOperationException("실제 렌더의 클릭 행동이 없습니다. " + label);
            return action.Disabled ? Task.CompletedTask : DispatchEventAsync(action.Event, null, new MouseEventArgs());
        }
        internal bool ButtonDisabled(string label)
        {
            var action = Find(root, label);
            if (action.Event == 0) throw new InvalidOperationException("실제 렌더의 버튼이 없습니다. " + label);
            return action.Disabled;
        }
        internal Task SubmitQuery()
        {
            var action = FindForm(root);
            if (action == 0) throw new InvalidOperationException("실제 처리 묶음 조회 양식이 없습니다.");
            return DispatchEventAsync(action, null, EventArgs.Empty);
        }
        private ulong FindForm(int id)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component)
                {
                    var nested = FindForm(frame.ComponentId);
                    if (nested != 0) return nested;
                }
                if (frame.FrameType != RenderTreeFrameType.Element || frame.ElementName != "form") continue;
                var attributes = frames.Array.Skip(index + 1).Take(frame.ElementSubtreeLength - 1)
                    .TakeWhile(item => item.FrameType == RenderTreeFrameType.Attribute).ToArray();
                if (attributes.Any(attribute => attribute.AttributeName == "class" && attribute.AttributeValue as string == "batch-query"))
                    return attributes.FirstOrDefault(attribute => attribute.AttributeName == "onsubmit").AttributeEventHandlerId;
            }
            return 0;
        }
        internal int ElementCount(string name) => Count(root, name);
        private int Count(int id, string name)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            var count = 0;
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component) count += Count(frame.ComponentId, name);
                else if (frame.FrameType == RenderTreeFrameType.Element && frame.ElementName == name) count++;
            }
            return count;
        }
        private (ulong Event, bool Disabled) Find(int id, string label)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component)
                {
                    var nested = Find(frame.ComponentId, label);
                    if (nested.Event != 0) return nested;
                }
                if (frame.FrameType != RenderTreeFrameType.Element || frame.ElementName is not ("button" or "a")) continue;
                var contents = frames.Array.Skip(index + 1).Take(frame.ElementSubtreeLength - 1).ToArray();
                var attributes = contents.TakeWhile(item => item.FrameType == RenderTreeFrameType.Attribute).ToArray();
                var text = new StringBuilder();
                foreach (var content in contents)
                {
                    if (content.FrameType == RenderTreeFrameType.Component) Append(content.ComponentId, text);
                    else if (content.FrameType == RenderTreeFrameType.Text) text.Append(content.TextContent);
                    else if (content.FrameType == RenderTreeFrameType.Markup) text.Append(content.MarkupContent);
                }
                var aria = attributes.FirstOrDefault(attribute => attribute.AttributeName == "aria-label").AttributeValue as string;
                if (aria != label && !string.Equals(text.ToString().Trim(), label, StringComparison.Ordinal)) continue;
                return (attributes.FirstOrDefault(attribute => attribute.AttributeName == "onclick").AttributeEventHandlerId,
                    attributes.Any(attribute => attribute.AttributeName == "disabled" && attribute.AttributeValue is true));
            }
            return default;
        }
    }
#pragma warning restore BL0006
}
