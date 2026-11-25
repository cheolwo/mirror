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

public sealed class OutboundMemberPageEmptyRecoveryTests
{
    private const string EmptyPage = "이 페이지에 표시할 구성 내역이 없습니다.";
    private const string RecoveryHelp = "선택한 묶음·창고·조회 구간·공정은 유지하고 첫 페이지를 다시 확인해 주세요.";
    private const string RecoveryAction = "첫 구성 페이지로";
    private const string WholeEmpty = "이 묶음에서 조회할 구성 내역이 없습니다.";

    [Fact]
    public async Task 현재구성페이지만비면_첫구성페이지를읽고_같은묶음창고구간공정과다른페이지를보존한다()
    {
        await using var fixture = new Fixture();
        var initial = State();
        await fixture.StartAsync(initial);

        var text = await fixture.TextAsync();
        Assert.Contains(EmptyPage, text);
        Assert.Contains(RecoveryHelp, text);
        Assert.DoesNotContain(WholeEmpty, text);
        var selected = fixture.Model.Snapshot!.Workflow!.SelectedGroup!;
        Assert.Equal(3, selected.MemberCount);
        Assert.Equal(1, selected.MemberPage);
        Assert.Empty(selected.Members);
        Assert.Single(fixture.Api.Reads);

        await fixture.ClickAsync(RecoveryAction);

        Assert.Equal(new[] { 1, 0 }, fixture.Api.Reads.Select(read => read.MemberPage));
        Assert.All(fixture.Api.Reads, read => AssertQuery(read, initial));
        Assert.Equal(initial with { MemberPage = 0 }, fixture.Renderer.Harness.State);
        Assert.Single(fixture.Renderer.Harness.Published);
        Assert.Equal(2, fixture.Model.BatchPage);
        Assert.Equal(3, fixture.Model.PlanPage);
        Assert.Equal(initial.BatchKey, fixture.Model.BatchKey);
        Assert.Equal(3, fixture.Model.Snapshot!.Workflow!.SelectedGroup!.Members.Count);
        Assert.Contains("분할 상자 대기", await fixture.TextAsync());
        Assert.DoesNotContain(EmptyPage, await fixture.TextAsync());
        Assert.Empty(fixture.Api.DetailIds);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 최대정수구성페이지도_음수로넘치지않고_같은묶음의첫구성페이지로돌아간다()
    {
        await using var fixture = new Fixture();
        var initial = State() with { MemberPage = int.MaxValue };
        await fixture.StartAsync(initial);

        var text = await fixture.TextAsync();
        Assert.DoesNotContain("-2147483648페이지", text);
        Assert.Contains("2147483648페이지", text);
        Assert.Contains(EmptyPage, text);
        Assert.Equal(int.MaxValue, Assert.Single(fixture.Api.Reads).MemberPage);

        await fixture.ClickAsync(RecoveryAction);
        Assert.Equal(new[] { int.MaxValue, 0 }, fixture.Api.Reads.Select(read => read.MemberPage));
        Assert.All(fixture.Api.Reads, read => AssertQuery(read, initial));
        Assert.Equal(initial with { MemberPage = 0 }, fixture.Renderer.Harness.State);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 정상구성은_분할4완료와6대기를_원래3작업과수량으로읽는다()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State() with { MemberPage = 0 });

        var text = await fixture.TextAsync();
        Assert.Contains("냉장 세트 완료", text);
        Assert.Contains("분할 상자 완료", text);
        Assert.Contains("분할 상자 대기", text);
        var members = fixture.Model.Snapshot!.Workflow!.SelectedGroup!.Members;
        Assert.Equal(new long[] { 30, 4, 6 }, members.Select(member => member.Quantity));
        Assert.Equal(new long[] { 1101, 1102, 1102 }, members.Select(member => member.OutboundPlanId));
        Assert.Contains("40개", text);
        Assert.Contains("34개", text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.DoesNotContain(WholeEmpty, text);
        Assert.Single(fixture.Api.Reads);
        Assert.Empty(fixture.Api.DetailIds);
    }

    [Fact]
    public async Task 구성전체0건은_기존전체빈안내를사용하고_페이지복구와구별한다()
    {
        await using var fixture = new Fixture();
        fixture.Api.MemberCount = 0;
        await fixture.StartAsync(State());

        var text = await fixture.TextAsync();
        Assert.Contains(WholeEmpty, text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.Single(fixture.Api.Reads);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 첫구성조회중에는_정상빈페이지안내를미리표시하지않는다()
    {
        await using var fixture = new Fixture();
        var pending = fixture.Api.Pending = new();
        var loading = fixture.StartAsync(State());
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
            await loading;
        }
        Assert.Contains(EmptyPage, await fixture.TextAsync());
    }

    [Fact]
    public async Task 같은구성을재조회중이면_이전결과가남아있어도_새빈안내를숨기고페이지행동을잠근다()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State());
        var previous = fixture.Model.Snapshot;
        var pending = fixture.Api.Pending = new();
        var loading = fixture.ReloadSameQueryAsync();
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var text = await fixture.TextAsync();
            Assert.Same(previous, fixture.Model.Snapshot);
            Assert.True(fixture.Model.IsLoading);
            Assert.DoesNotContain(EmptyPage, text);
            Assert.DoesNotContain(RecoveryAction, text);
            Assert.DoesNotContain(WholeEmpty, text);
            Assert.True(await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ButtonDisabled("이전 내역")));
            Assert.True(await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ButtonDisabled("다음 내역")));
            Assert.Equal(2, fixture.Api.Reads.Count);
        }
        finally
        {
            pending.Release();
            await loading;
        }
        Assert.Contains(EmptyPage, await fixture.TextAsync());
    }

    [Fact]
    public async Task 같은구성503은_이전결과를남겨도_정상빈페이지복구로표시하지않는다()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State());
        var previous = fixture.Model.Snapshot;
        fixture.Api.FailStatus = 503;

        await fixture.ReloadSameQueryAsync();

        Assert.Same(previous, fixture.Model.Snapshot);
        Assert.NotNull(fixture.Model.Error);
        Assert.NotNull(fixture.Model.Warning);
        var text = await fixture.TextAsync();
        Assert.Contains(fixture.Model.Error, text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.DoesNotContain(WholeEmpty, text);
        Assert.Empty(fixture.Renderer.Harness.Published);
        Assert.Equal(2, fixture.Api.Reads.Count);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 인증권한실패는_이전구성결과와정상빈페이지복구를표시하지않는다(int status)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync(State());
        fixture.Api.FailStatus = status;

        await fixture.ReloadSameQueryAsync();

        Assert.Null(fixture.Model.Snapshot);
        Assert.NotNull(fixture.Model.Error);
        var text = await fixture.TextAsync();
        Assert.Contains(fixture.Model.Error, text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.DoesNotContain(WholeEmpty, text);
        Assert.Empty(fixture.Renderer.Harness.Published);
        Assert.Equal(2, fixture.Api.Reads.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 회차카드표시모드는_정상구성과현재빈구성에_새웹단계안내를추가하지않는다(bool empty)
    {
        await using var fixture = new Fixture();
        await fixture.StartPanelAsync(State() with { MemberPage = empty ? 1 : 0 }, stageEntry: false);

        var text = await fixture.TextAsync();
        Assert.Contains("동부 출고묶음 · 피킹", text);
        Assert.DoesNotContain(EmptyPage, text);
        Assert.DoesNotContain(RecoveryHelp, text);
        Assert.DoesNotContain(RecoveryAction, text);
        Assert.DoesNotContain(WholeEmpty, text);
        if (empty) Assert.Contains("2페이지 · 구성 3개", text);
        else
        {
            Assert.Contains("분할 상자 대기", text);
            Assert.Contains("40개", text);
            Assert.Contains("같은 출고 검토", text);
        }
        Assert.Single(fixture.Api.Reads);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 기존이전내역도_한페이지씩이동하며_같은묶음과다른페이지를보존한다()
    {
        await using var fixture = new Fixture();
        var initial = State() with { MemberPage = 2 };
        await fixture.StartAsync(initial);
        Assert.False(await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ButtonDisabled("이전 내역")));
        Assert.True(await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.Renderer.ButtonDisabled("다음 내역")));

        await fixture.ClickAsync("이전 내역");
        await fixture.ClickAsync("이전 내역");

        Assert.Equal(new[] { 2, 1, 0 }, fixture.Api.Reads.Select(read => read.MemberPage));
        Assert.All(fixture.Api.Reads, read => AssertQuery(read, initial));
        Assert.Equal(initial with { MemberPage = 0 }, fixture.Renderer.Harness.State);
        Assert.Equal(2, fixture.Renderer.Harness.Published.Count);
        Assert.Contains("분할 상자 대기", await fixture.TextAsync());
        Assert.Empty(fixture.Api.DetailIds);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 복구된현재구성에서만_같은원출고1102를선택하고_수량10상세와원구성으로돌아온다()
    {
        await using var fixture = new Fixture();
        var initial = State();
        await fixture.StartAsync(initial);
        await fixture.ClickAsync(RecoveryAction);
        var report = fixture.Model.Snapshot;
        Assert.DoesNotContain("일반 출고 페이지 전용 상품", await fixture.TextAsync());

        await fixture.ClickAsync("분할 상자 대기 출고 검토");

        Assert.Equal(new long[] { 1102 }, fixture.Api.DetailIds);
        Assert.Equal(new long?[] { 1102 }, fixture.Renderer.Harness.Selected);
        var detail = await fixture.TextAsync();
        Assert.Contains("원출고 상자", detail);
        Assert.Contains("10개", detail);
        Assert.DoesNotContain("일반 출고 페이지 전용 상품", detail);

        await fixture.ClickAsync("묶음 구성 내역으로");

        Assert.Equal(new long?[] { 1102, null }, fixture.Renderer.Harness.Selected);
        Assert.Equal(initial with { MemberPage = 0 }, fixture.Renderer.Harness.State);
        Assert.Same(report, fixture.Model.Snapshot);
        Assert.Equal(2, fixture.Api.Reads.Count);
        Assert.Contains("분할 상자 완료", await fixture.TextAsync());
        Assert.Contains("분할 상자 대기", await fixture.TextAsync());
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 첫구성복구의늦은이전응답은_새묶음페이지문맥과주소콜백을덮지않는다()
    {
        await using var fixture = new Fixture();
        var initial = State();
        await fixture.StartAsync(initial);
        var pending = fixture.Api.Pending = new() { IgnoreCancellation = true };
        var recovering = fixture.ClickAsync(RecoveryAction);
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var next = initial with { BatchPage = 1, MemberPage = 0 };
        try
        {
            await fixture.ReplayAsync(next);
            var current = fixture.Model.Snapshot;
            Assert.True(pending.Token.IsCancellationRequested);
            Assert.False(recovering.IsCompleted);
            Assert.Contains("분할 상자 대기", await fixture.TextAsync());

            pending.Release();
            await recovering;

            Assert.Same(current, fixture.Model.Snapshot);
            Assert.Equal(next, fixture.Renderer.Harness.State);
            Assert.Empty(fixture.Renderer.Harness.Published);
            Assert.Equal(new[] { 2, 2, 1 }, fixture.Api.Reads.Select(read => read.BatchPage));
            Assert.Equal(new[] { 1, 0, 0 }, fixture.Api.Reads.Select(read => read.MemberPage));
            Assert.Empty(fixture.Api.DetailIds);
            Assert.Equal(0, fixture.Api.Writes);
        }
        finally
        {
            pending.Release();
            await recovering;
        }
    }

    private static OutboundBatchNavigationState State(string stage = OutboundWorkflowStageCodes.Picking)
        => new(stage, 101, new(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), new(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc), 2, "picking:batch:501", 1, 3);

    private static void AssertQuery(출고예정검토목록조회요청 read, OutboundBatchNavigationState state)
    {
        Assert.Equal(state.WarehouseId, read.WarehouseId);
        Assert.Equal(state.StartUtc, read.PeriodStartUtc);
        Assert.Equal(state.EndUtc, read.PeriodEndUtc);
        Assert.Equal(state.Stage, read.PeriodStage);
        Assert.Equal(state.PlanPage, read.Page);
        Assert.Equal(출고예정검토조회상태코드.전체, read.Status);
        Assert.Equal(state.BatchKey, read.BatchKey);
        Assert.Equal(state.BatchPage, read.BatchPage);
        Assert.Equal(6, read.BatchPageSize);
        Assert.Equal(12, read.MemberPageSize);
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
            services.AddTransient<출고예정검토목록ViewModel>();
            services.AddTransient<출고예정검토상세ViewModel>();
            services.AddTransient<출고예정검토PageViewModel>();
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
        internal async Task ReplayAsync(OutboundBatchNavigationState state)
        {
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Replay(state));
            await DrainAsync();
        }
        internal async Task ReloadSameQueryAsync()
        {
            // 선택 구성 화면에는 조회 form이 없다. 실제 VM 재조회와 부모의 상태 렌더만 확인한다.
            await Renderer.Dispatcher.InvokeAsync(() => Model.LoadAsync(Model.PlanPage));
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
        [Parameter] public long? OutboundPlanId { get; set; }
        [Parameter] public 출고회차ViewModel? PanelModel { get; set; }
        [Parameter] public bool PanelStageEntry { get; set; }
        internal List<OutboundBatchNavigationState> Published { get; } = [];
        internal List<long?> Selected { get; } = [];
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
            builder.AddAttribute(3, nameof(SsalddelOutboundBatchWorkspace.OutboundPlanId), OutboundPlanId);
            builder.AddAttribute(4, nameof(SsalddelOutboundBatchWorkspace.OnNavigationChanged),
                EventCallback.Factory.Create<OutboundBatchNavigationState>(this, Navigate));
            builder.AddAttribute(5, nameof(SsalddelOutboundBatchWorkspace.OnPlanSelected),
                EventCallback.Factory.Create<long?>(this, Select));
            builder.CloseComponent();
        }
        private void Navigate(OutboundBatchNavigationState state)
        {
            State = state;
            OutboundPlanId = null;
            Published.Add(state);
        }
        private void Select(long? id)
        {
            OutboundPlanId = id;
            Selected.Add(id);
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
        internal int MemberCount = 3;
        internal bool EmptyFirstPage;
        internal int? FailStatus;
        internal PendingRead? Pending;

        public Task<출고예정검토목록페이지응답> 목록조회Async(출고예정검토목록조회요청 request, CancellationToken cancellationToken = default)
        {
            Reads.Add(request);
            if (FailStatus is { } status)
                return Task.FromException<출고예정검토목록페이지응답>(new SsalddelApiException("isolated member read failure", status, "read", "", null));
            var response = new 출고예정검토목록페이지응답
            {
                Period = new()
                {
                    WarehouseId = request.WarehouseId!.Value, WarehouseName = "동부 가상 창고",
                    PeriodStartUtc = request.PeriodStartUtc!.Value, PeriodEndUtc = request.PeriodEndUtc!.Value,
                    ObservedAtUtc = new(2026, 11, 7, 3, 10, 0, DateTimeKind.Utc),
                    Page = request.Page, PageSize = request.PageSize, TotalCount = 2,
                    Items = [new() { OutboundPlanId = 999, ProductName = "일반 출고 페이지 전용 상품", CanOpenReview = true }],
                    Workflow = new()
                    {
                        SelectedStage = request.PeriodStage!, Page = request.BatchPage, PageSize = request.BatchPageSize,
                        TotalGroups = 2, Groups = request.BatchPage == 0 ? [Group(0)] : [], SelectedGroupKey = request.BatchKey,
                        SelectedGroup = Group(request.MemberPage),
                        Stages = [new() { Code = request.PeriodStage!, Label = "선택 공정", SourcePlanCount = 2,
                            TaskCount = 3, WorkQuantity = 40, CompletedQuantity = 34, RecordedBatchCount = 2 }]
                    }
                }
            };
            if (Pending is not { } pending) return Task.FromResult(response);
            Pending = null;
            pending.Response = response;
            pending.Token = cancellationToken;
            pending.Started.TrySetResult();
            return pending.IgnoreCancellation ? pending.Completion.Task : pending.Completion.Task.WaitAsync(cancellationToken);
        }
        private OutboundPeriodGroup Group(int memberPage) => new()
        {
            Key = "picking:batch:501", Label = "동부 출고묶음 · 피킹", Kind = "recorded-batch",
            SourcePlanCount = 2, TaskCount = 3, Quantity = 40, CompletedQuantity = 34,
            MembershipComplete = true, MemberCount = MemberCount, MemberPage = memberPage, MemberPageSize = 12,
            Members = MemberCount == 0 || memberPage != 0 || EmptyFirstPage ? [] :
            [
                new() { OutboundPlanId = 1101, ProductName = "냉장 세트 완료", TaskKey = "picking:901", Quantity = 30, Status = "완료", CanOpenReview = true },
                new() { OutboundPlanId = 1102, ProductName = "분할 상자 완료", TaskKey = "picking:902", Quantity = 4, Status = "완료", CanOpenReview = true },
                new() { OutboundPlanId = 1102, ProductName = "분할 상자 대기", TaskKey = "picking:903", Quantity = 6, Status = "대기", CanOpenReview = true }
            ]
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
            return Task.FromResult<출고예정검토상세응답?>(new()
            {
                OutboundPlanId = outboundPlanId, ProductName = "원출고 상자", WarehouseId = 101,
                WarehouseName = "동부 가상 창고", Quantity = outboundPlanId == 1102 ? 10 : 30,
                OutboundStatus = "준비중", ReviewStatus = "운송 초안 검토"
            });
        }
        public Task<출고운송인계완료응답> 인계완료Async(long id, 출고운송인계완료요청 request, CancellationToken cancellationToken = default) => Write<출고운송인계완료응답>();
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => Write<object>();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default) => Write<object>();
        private Task<T> Write<T>() { Writes++; throw new InvalidOperationException("구성 페이지 복구에서 업무 쓰기를 실행하면 안 됩니다."); }
    }
    private sealed class PendingRead
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<출고예정검토목록페이지응답> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal 출고예정검토목록페이지응답? Response;
        internal CancellationToken Token;
        internal bool IgnoreCancellation;
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
        internal Task Replay(OutboundBatchNavigationState state)
            => RenderRootComponentAsync(root, ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(HostHarness.State)] = state,
                [nameof(HostHarness.OutboundPlanId)] = null
            }));
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
