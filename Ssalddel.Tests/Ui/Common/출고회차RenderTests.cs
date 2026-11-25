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

public sealed class 출고회차RenderTests
{
    [Fact]
    public async Task 실제부모의회차보기는_기존JSON등록으로열리고_같은출고상세와조회결과로돌아온다()
    {
        await using var fixture = new Fixture();
        Assert.Null(fixture.Services.GetService<IWarehouseWorkspaceService>());
        await fixture.StartAsync();
        await fixture.ShowLoadedPeriodAsync();
        var model = Assert.Single(fixture.PeriodModels);
        var snapshot = model.Snapshot;
        Assert.Contains("회차 상품 상자", await fixture.TextAsync());
        Assert.Contains("공정별 작업 연결을 확인하지 못했습니다", await fixture.TextAsync());
        Assert.DoesNotContain("이 공정에서 조회할 처리 대상이 없습니다", await fixture.TextAsync());

        await fixture.ClickAsync("회차 상품 상자 출고 검토");

        Assert.Equal(new long?[] { 17 }, fixture.Selected);
        Assert.Equal(new long[] { 17 }, fixture.Api.DetailIds);
        var detail = await fixture.TextAsync();
        Assert.Contains("같은 원장의 상세 상품", detail);
        Assert.DoesNotContain("일반 검토 목록 상품", detail);
        Assert.DoesNotContain("운송 전 검토 대상", detail);

        await fixture.ClickAsync("회차별 현황");

        Assert.Single(fixture.PeriodModels);
        Assert.Same(snapshot, model.Snapshot);
        Assert.Equal(1, model.WarehouseId);
        Assert.Contains("회차 상품 상자", await fixture.TextAsync());
        Assert.Single(fixture.Api.PeriodReads);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("session")]
    [InlineData("server")]
    public async Task 회차에서상세조회중_문맥변경은_이전상세와선택콜백을표시하지않는다(string change)
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync();
        await fixture.ShowLoadedPeriodAsync();
        fixture.Api.DetailGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var selecting = fixture.ClickAsync("회차 상품 상자 출고 검토");
        await fixture.Api.DetailStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.Renderer.Dispatcher.InvokeAsync(() => fixture.ChangeContext(change));
        fixture.Api.DetailGate.SetResult(Api.Detail());
        await selecting;
        await fixture.DrainAsync();

        Assert.Empty(fixture.Selected);
        var text = await fixture.TextAsync();
        Assert.DoesNotContain("같은 원장의 상세 상품", text);
        Assert.Contains("로그인이나 서버가 바뀌었습니다", text);

        await fixture.ClickAsync("운송 전 검토");
        var currentReview = await fixture.TextAsync();
        Assert.DoesNotContain("같은 원장의 상세 상품", currentReview);
        Assert.DoesNotContain("일반 검토 목록 상품", currentReview);
        Assert.Contains("새 문맥의 검토 목록 상품", currentReview);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 상세조회중회차로복귀하면_늦은상세가_복귀화면이나선택주소를바꾸지않는다()
    {
        await using var fixture = new Fixture();
        await fixture.StartAsync();
        await fixture.ShowLoadedPeriodAsync();
        var model = Assert.Single(fixture.PeriodModels);
        var snapshot = model.Snapshot;
        fixture.Api.DetailGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var selecting = fixture.ClickAsync("회차 상품 상자 출고 검토");
        await fixture.Api.DetailStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.ClickAsync("회차별 현황");
        fixture.Api.DetailGate.SetResult(Api.Detail());
        await selecting;
        await fixture.DrainAsync();

        Assert.Empty(fixture.Selected);
        Assert.Same(snapshot, model.Snapshot);
        Assert.Single(fixture.PeriodModels);
        var text = await fixture.TextAsync();
        Assert.Contains("회차 상품 상자", text);
        Assert.DoesNotContain("같은 원장의 상세 상품", text);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 공정과묶음의구성페이지에서_원목록밖같은출고상세를열고_선택한위치로돌아온다()
    {
        await using var fixture = new Fixture();
        fixture.Api.IncludeWorkflow = true;
        await fixture.StartAsync();
        await fixture.ShowLoadedPeriodAsync(2);
        await fixture.ClickAsync("포장");
        await fixture.ClickAsync("다음 묶음");
        await fixture.ClickAsync("실제 묶음 A 구성 내역 보기");
        var model = Assert.Single(fixture.PeriodModels);
        Assert.DoesNotContain(model.Snapshot!.Items, item => item.OutboundPlanId == 71);
        Assert.True(model.CanOpenPlan(71));
        Assert.False(model.CanOpenPlan(72));
        await fixture.ClickAsync("취소 구성 상품 출고 검토");
        Assert.Empty(fixture.Api.DetailIds);
        Assert.Empty(fixture.Selected);
        await fixture.ClickAsync("다음 내역");
        Assert.False(model.CanOpenPlan(71));
        Assert.True(model.CanOpenPlan(83));
        var snapshot = model.Snapshot;
        var reads = fixture.Api.PeriodReads.Count;

        await fixture.ClickAsync("구성 상품 83 출고 검토");

        Assert.Equal(new long?[] { 83 }, fixture.Selected);
        Assert.Equal(new long[] { 83 }, fixture.Api.DetailIds);
        Assert.Contains("같은 원장의 상세 상품", await fixture.TextAsync());
        Assert.DoesNotContain("일반 검토 목록 상품", await fixture.TextAsync());
        await fixture.ClickAsync("회차별 현황");
        Assert.Same(snapshot, model.Snapshot);
        Assert.Equal(OutboundWorkflowStageCodes.Packing, model.StageCode);
        Assert.Equal(1, model.BatchPage);
        Assert.Equal("batch-a", model.BatchKey);
        Assert.Equal(1, model.MemberPage);
        Assert.Contains("구성 상품 83", await fixture.TextAsync());
        Assert.Equal(reads, fixture.Api.PeriodReads.Count);
        await fixture.ClickAsync("묶음 목록으로");
        Assert.Null(model.BatchKey);
        Assert.Equal(1, model.BatchPage);
        Assert.Equal(2, fixture.Api.PeriodReads[^1].Page);
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 선택묶음404뒤에도_목록복귀버튼으로_같은공정과원페이지를다시읽는다()
    {
        await using var fixture = new Fixture();
        fixture.Api.IncludeWorkflow = true;
        await fixture.StartAsync();
        await fixture.ShowLoadedPeriodAsync(1);
        await fixture.ClickAsync("실제 묶음 A 구성 내역 보기");
        fixture.Api.NextPeriodFailure = new SsalddelApiException("missing", 404, "read", "", null);

        await fixture.ClickAsync("다음 내역");

        var model = Assert.Single(fixture.PeriodModels);
        Assert.Null(model.Snapshot);
        Assert.Contains("선택한 작업 묶음을 확인하지 못했습니다", await fixture.TextAsync());
        Assert.Contains("묶음 목록으로", await fixture.TextAsync());
        await fixture.ClickAsync("묶음 목록으로");
        Assert.NotNull(model.Snapshot);
        Assert.Null(model.BatchKey);
        Assert.Equal(0, model.MemberPage);
        Assert.Equal(1, fixture.Api.PeriodReads[^1].Page);
        Assert.Contains("실제 묶음 A", await fixture.TextAsync());
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Fact]
    public async Task 구성원상세조회중공정이바뀌면_늦은상세와주소선택을노출하지않는다()
    {
        await using var fixture = new Fixture();
        fixture.Api.IncludeWorkflow = true;
        await fixture.StartAsync();
        await fixture.ShowLoadedPeriodAsync();
        await fixture.ClickAsync("실제 묶음 A 구성 내역 보기");
        fixture.Api.DetailGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var selecting = fixture.ClickAsync("구성 상품 71 출고 검토");
        await fixture.Api.DetailStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var model = Assert.Single(fixture.PeriodModels);

        await fixture.Renderer.Dispatcher.InvokeAsync(() => model.SelectStageAsync(OutboundWorkflowStageCodes.Handoff));
        fixture.Api.DetailGate.SetResult(Api.Detail(71));
        await selecting;
        await fixture.DrainAsync();

        Assert.Empty(fixture.Selected);
        Assert.DoesNotContain("같은 원장의 상세 상품", await fixture.TextAsync());
        await fixture.ClickAsync("회차별 현황");
        Assert.Equal(OutboundWorkflowStageCodes.Handoff, model.StageCode);
        Assert.Null(model.BatchKey);
        Assert.Contains("완료 확인은 창고 인계 기록 기준입니다", await fixture.TextAsync());
        Assert.Equal(0, fixture.Api.Writes);
    }

    [Theory]
    [InlineData("recorded-batch")]
    [InlineData("unconfirmed")]
    public async Task 부분완료와잘못된연결은_구성누락사유를보존하고_전체완료로표시하지않는다(string kind)
    {
        await using var fixture = new Fixture();
        fixture.Api.IncludeWorkflow = true;
        fixture.Api.GroupKind = kind;
        fixture.Api.GroupNeedsReview = true;
        await fixture.StartAsync();
        await fixture.ShowLoadedPeriodAsync();
        await fixture.ClickAsync("실제 묶음 A 구성 내역 보기");

        var text = await fixture.TextAsync();
        Assert.Contains("일부 구성원의 창고·공정 확인 필요", text);
        Assert.Contains("조회 범위 밖 구성원이 있습니다", text);
        Assert.Contains("처리 기한은 미확인", text);
        Assert.Contains("출고 검토 불가", text);
        Assert.DoesNotContain("전체 완료", text);
        if (kind == "unconfirmed") Assert.DoesNotContain("999개", text);
        else Assert.Contains("4개", text);
        Assert.Equal(0, fixture.Api.Writes);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public readonly Api Api = new();
        public readonly User User = new();
        public readonly Scope Recovery;
        public readonly List<출고회차ViewModel> PeriodModels = [];
        public readonly List<long?> Selected = [];
        public ServiceProvider Services { get; }
        public PeriodRenderer Renderer { get; }

        public Fixture()
        {
            Recovery = new(User);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton<IJSRuntime, NoopJs>();
            services.AddSingleton<NavigationManager, Navigation>();
            services.AddSingleton<ISsalddelJsonApiClient>(Api);
            services.AddSingleton<I출고예정검토페이지Service>(Api);
            services.AddSingleton<ISsalddel현재사용자Context>(User);
            services.AddSingleton<IWorkflowRecoveryContext>(Recovery);
            services.AddSsalddelUiCommonAppServices();
            services.AddTransient(provider =>
            {
                var model = ActivatorUtilities.CreateInstance<출고회차ViewModel>(provider);
                PeriodModels.Add(model);
                return model;
            });
            Services = services.BuildServiceProvider();
            Renderer = new(Services, Services.GetRequiredService<ILoggerFactory>());
        }

        public async Task StartAsync()
        {
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Start(EventCallback.Factory.Create<long?>(this, id => Selected.Add(id))));
            await DrainAsync();
        }

        public async Task ShowLoadedPeriodAsync(int sourcePage = 0)
        {
            await ClickAsync("회차별 현황");
            var model = Assert.Single(PeriodModels);
            await Renderer.Dispatcher.InvokeAsync(async () =>
            {
                model.WarehouseId = 1;
                model.StartLocal = new DateTime(2026, 11, 7, 0, 0, 0);
                model.EndLocal = new DateTime(2026, 11, 7, 12, 0, 0);
                await model.LoadAsync(sourcePage);
            });
            await DrainAsync();
        }

        public async Task ClickAsync(string text)
        {
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Click(text));
            await DrainAsync();
        }

        public Task<string> TextAsync() => Renderer.Dispatcher.InvokeAsync(Renderer.VisibleText);

        public void ChangeContext(string change)
        {
            Api.ListProductName = "새 문맥의 검토 목록 상품";
            if (change == "owner") User.Switch("other");
            else if (change == "session") { Recovery.Revision++; User.Switch("worker"); }
            else Recovery.Endpoint = new('b', 32);
        }

        public async Task DrainAsync()
        {
            for (var index = 0; index < 4; index++)
                await Renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
            Assert.Empty(Renderer.Errors);
        }

        public async ValueTask DisposeAsync()
        {
            await Renderer.DisposeAsync();
            await Services.DisposeAsync();
        }
    }

    public sealed class PeriodHarness : ComponentBase
    {
        [Parameter] public EventCallback<long?> OnSelected { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<SsalddelOutboundPlanReviewWorkspace>(1);
            builder.AddAttribute(2, nameof(SsalddelOutboundPlanReviewWorkspace.OnPlanSelected), OnSelected);
            builder.CloseComponent();
        }
    }

    private sealed class User : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        public 현재사용자Snapshot 현재사용자 { get; private set; } = new("worker", "가상 창고 담당", []);
        public event Action? Changed;
        public void Switch(string? id)
        {
            현재사용자 = new(id, "가상 창고 담당", []);
            Changed?.Invoke();
        }
    }

    private sealed class Scope(User user) : IWorkflowRecoveryContext
    {
        public long Revision = 1;
        public string Endpoint = new('a', 32);
        public WorkflowRecoverySnapshot Capture()
            => new(user.현재사용자.UserId ?? throw new InvalidOperationException(), Revision, Endpoint);
        public bool IsCurrent(WorkflowRecoverySnapshot captured)
            => user.현재사용자.인증됨 && captured == Capture();
    }

    private sealed class Api : I출고예정검토페이지Service, ISsalddelJsonApiClient
    {
        public readonly List<출고예정검토목록조회요청> PeriodReads = [];
        public readonly List<long> DetailIds = [];
        public string ListProductName = "일반 검토 목록 상품";
        public bool IncludeWorkflow;
        public string GroupKind = "recorded-batch";
        public bool GroupNeedsReview;
        public Exception? NextPeriodFailure;
        public TaskCompletionSource<출고예정검토상세응답?>? DetailGate;
        public readonly TaskCompletionSource DetailStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Writes;

        public Task<출고예정검토목록페이지응답> 목록조회Async(출고예정검토목록조회요청 request, CancellationToken cancellationToken = default)
        {
            if (request.PeriodStartUtc is null)
                return Task.FromResult(new 출고예정검토목록페이지응답
                {
                    Items = [new() { OutboundPlanId = 17, ProductName = ListProductName }], TotalCount = 1
                });
            PeriodReads.Add(request);
            if (NextPeriodFailure is { } exception)
            {
                NextPeriodFailure = null;
                return Task.FromException<출고예정검토목록페이지응답>(exception);
            }
            return Task.FromResult(new 출고예정검토목록페이지응답
            {
                Period = new()
                {
                    WarehouseId = request.WarehouseId!.Value,
                    WarehouseName = "가상 창고",
                    PeriodStartUtc = request.PeriodStartUtc.Value,
                    PeriodEndUtc = request.PeriodEndUtc!.Value,
                    ObservedAtUtc = new(2026, 11, 7, 2, 0, 0, DateTimeKind.Utc),
                    Current = new() { PlanCount = 1, ReceivedQuantity = 12 },
                    Items = [new() { OutboundPlanId = 17, ProductName = "회차 상품 상자", Quantity = 12 }],
                    TotalCount = 36, Page = request.Page, PageSize = 12,
                    Workflow = IncludeWorkflow ? Flow(request) : null
                }
            });
        }

        private OutboundPeriodWorkflow Flow(출고예정검토목록조회요청 request) => new()
        {
            SelectedStage = request.PeriodStage!, Page = request.BatchPage, PageSize = request.BatchPageSize,
            TotalGroups = 13, Groups = [Group("batch-a", 0)],
            Stages = [new() { Code = request.PeriodStage!, Label = "선택 공정", SourcePlanCount = 2,
                TaskCount = 2, WorkQuantity = 12, CompletedQuantity = 4 }],
            SelectedGroupKey = request.BatchKey,
            SelectedGroup = request.BatchKey is null ? null : Group(request.BatchKey, request.MemberPage)
        };

        private OutboundPeriodGroup Group(string key, int memberPage) => new()
        {
            Key = key, Label = "실제 묶음 A", Kind = GroupKind, SourcePlanCount = 2, TaskCount = 2,
            Quantity = GroupKind == "unconfirmed" ? 999 : 12, CompletedQuantity = GroupKind == "unconfirmed" ? 999 : 4,
            NeedsReview = GroupNeedsReview, Reason = GroupNeedsReview ? "일부 구성원의 창고·공정 확인 필요" : "일부 작업 완료 확인",
            MembershipComplete = !GroupNeedsReview, HasOutsideScopeMembers = GroupNeedsReview,
            MemberCount = 24, MemberPage = memberPage, MemberPageSize = 12,
            Members = [new() { OutboundPlanId = 71 + memberPage * 12, ProductName = "구성 상품 " + (71 + memberPage * 12),
                    TaskKey = "task-a", Quantity = 8, CanOpenReview = true, Status = "진행중" },
                new() { OutboundPlanId = 72 + memberPage * 12, ProductName = "취소 구성 상품", Quantity = 4, CanOpenReview = false, Status = "취소" }]
        };

        public Task<출고예정검토상세응답?> 상세조회Async(long outboundPlanId, CancellationToken cancellationToken = default)
        {
            DetailIds.Add(outboundPlanId);
            DetailStarted.TrySetResult();
            return DetailGate?.Task ?? Task.FromResult<출고예정검토상세응답?>(Detail(outboundPlanId));
        }

        public static 출고예정검토상세응답 Detail(long id = 17) => new()
        {
            OutboundPlanId = id, ProductName = "같은 원장의 상세 상품", WarehouseId = 1,
            WarehouseName = "가상 창고", Quantity = 12, OutboundStatus = "준비중", ReviewStatus = "운송 연결됨"
        };

        public Task<출고운송인계완료응답> 인계완료Async(long outboundPlanId, 출고운송인계완료요청 request, CancellationToken cancellationToken = default)
            => Write<출고운송인계완료응답>();

        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("api/v1/warehouse-operations/warehouses", path);
            Assert.Equal(typeof(창고목록응답), typeof(TResponse));
            Assert.False(allowNotFound);
            object value = new 창고목록응답 { Items = [new() { Id = 1, 창고명 = "가상 창고", IsActive = true }] };
            return Task.FromResult((TResponse?)value);
        }

        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName,
            bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request,
            string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => Write<object>();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName,
            CancellationToken cancellationToken = default) => Write<object>();
        private Task<T> Write<T>() { Writes++; throw new InvalidOperationException("회차 확인에서 업무 명령을 전송하면 안 됩니다."); }
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/warehouse/general/outbound-plan-review");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

#pragma warning disable BL0006 // 실제 컴포넌트의 이벤트와 수명주기를 서버 없이 확인한다.
    private sealed class PeriodRenderer : Renderer
    {
        private int root;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public readonly List<Exception> Errors = [];

        public PeriodRenderer(IServiceProvider services, ILoggerFactory logging) : base(services, logging)
        {
            ElementReferenceContext = new WebElementReferenceContext(services.GetRequiredService<IJSRuntime>());
        }

        protected override void HandleException(Exception exception) => Errors.Add(exception);
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;

        public Task Start(EventCallback<long?> selected)
        {
            root = AssignRootComponentId(InstantiateComponent(typeof(PeriodHarness)));
            return RenderRootComponentAsync(root, ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(PeriodHarness.OnSelected)] = selected
            }));
        }

        public string VisibleText()
        {
            var text = new StringBuilder();
            AppendText(root, text);
            return text.ToString();
        }

        private void AppendText(int componentId, StringBuilder text)
        {
            var frames = GetCurrentRenderTreeFrames(componentId);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component) AppendText(frame.ComponentId, text);
                else if (frame.FrameType == RenderTreeFrameType.Text) text.Append(frame.TextContent);
                else if (frame.FrameType == RenderTreeFrameType.Markup) text.Append(frame.MarkupContent);
            }
        }

        public Task Click(string name)
        {
            var eventId = FindButton(root, name);
            if (eventId == 0)
            {
                var text = VisibleText();
                throw new InvalidOperationException("실제 버튼을 찾지 못했습니다. " + name + " 표시 내용 " + text[..Math.Min(text.Length, 1200)]);
            }
            return DispatchEventAsync(eventId, null, new MouseEventArgs());
        }

        private ulong FindButton(int componentId, string name)
        {
            var frames = GetCurrentRenderTreeFrames(componentId);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component)
                {
                    var nested = FindButton(frame.ComponentId, name);
                    if (nested != 0) return nested;
                }
                if (frame.FrameType != RenderTreeFrameType.Element || frame.ElementName != "button") continue;
                var attributes = frames.Array.Skip(index + 1).Take(frame.ElementSubtreeLength - 1)
                    .TakeWhile(attribute => attribute.FrameType == RenderTreeFrameType.Attribute).ToArray();
                var label = attributes.FirstOrDefault(attribute => attribute.AttributeName == "aria-label").AttributeValue as string;
                var text = new StringBuilder();
                for (var child = index + 1; child < index + frame.ElementSubtreeLength; child++)
                {
                    var content = frames.Array[child];
                    if (content.FrameType == RenderTreeFrameType.Component) AppendText(content.ComponentId, text);
                    else if (content.FrameType == RenderTreeFrameType.Text) text.Append(content.TextContent);
                    else if (content.FrameType == RenderTreeFrameType.Markup) text.Append(content.MarkupContent);
                }
                if (label != name && !text.ToString().Contains(name, StringComparison.Ordinal)) continue;
                return attributes.FirstOrDefault(attribute => attribute.AttributeName == "onclick").AttributeEventHandlerId;
            }
            return 0;
        }
    }
#pragma warning restore BL0006
}
