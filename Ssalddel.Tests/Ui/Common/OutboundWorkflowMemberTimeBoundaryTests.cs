using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Components.WarehouseOperations;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.기사;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OutboundWorkflowMemberTimeBoundaryTests
{
    private static readonly DateTime Start = new(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastConvertible = DateTime.SpecifyKind(DateTime.MaxValue.AddHours(-9), DateTimeKind.Utc);
    private const string Product = "분할 상자";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 미래작업의원시최대시각도_실제UseCase와부모를지나_같은원출고와구성으로돌아온다(bool stageEntry)
    {
        var rawTime = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
        await using var fixture = await Fixture.CreateAsync(rawTime);
        Assert.Equal("unconfirmed", fixture.Selected.Period!.Workflow!.SelectedGroup!.Kind);
        Assert.True(fixture.Selected.Period.Workflow.SelectedGroup.NeedsReview);
        Assert.Equal(0, fixture.Selected.Period.Workflow.SelectedGroup.Quantity);
        Assert.Contains("근거 확인이 필요", fixture.Selected.Period.Workflow.SelectedGroup.Reason);
        var sourceMember = Assert.Single(fixture.Selected.Period.Workflow.SelectedGroup.Members);
        Assert.Equal(rawTime, sourceMember.CreatedAtUtc);
        Assert.Equal(1102, sourceMember.OutboundPlanId);
        Assert.Equal(6, sourceMember.Quantity);
        Assert.True(sourceMember.CanOpenReview);

        // Actual UseCase DTO -> service adapter -> VM -> actual parent -> selected panel.
        // The RED assertion is an unhandled render exception, not a copied date helper.
        await fixture.StartAsync(stageEntry);
        Assert.Equal("미확인", await fixture.MemberFieldAsync("기록 시각"));
        Assert.Equal("미확인", await fixture.MemberFieldAsync("완료 확인"));
        Assert.Equal("6개", await fixture.MemberFieldAsync("기록 수량"));
        Assert.Null(fixture.Model.Error);
        Assert.Equal(fixture.GroupKey, fixture.Model.BatchKey);
        var snapshot = fixture.Model.Snapshot;
        var member = Assert.Single(snapshot!.Workflow!.SelectedGroup!.Members);
        Assert.Equal(rawTime, member.CreatedAtUtc); // UI formatting does not correct the ledger or DTO.
        Assert.Contains(Product, await fixture.TextAsync());

        await fixture.ClickAsync(Product + " 출고 검토");
        Assert.Equal(new long[] { 1102 }, fixture.Api.DetailIds);
        Assert.Equal(1102, fixture.Api.LastDetail!.OutboundPlanId);
        Assert.Equal(10, fixture.Api.LastDetail.Quantity);
        Assert.Contains("10개", await fixture.TextAsync());
        Assert.Equal(1102, fixture.Renderer.Harness.OutboundPlanId);

        await fixture.ClickAsync(stageEntry ? "묶음 구성 내역으로" : "회차별 현황");
        Assert.Same(snapshot, fixture.Model.Snapshot);
        Assert.Equal(fixture.GroupKey, fixture.Model.BatchKey);
        Assert.Equal(OutboundWorkflowStageCodes.Picking, fixture.Model.StageCode);
        Assert.Equal(0, fixture.Model.MemberPage);
        Assert.Equal("6개", await fixture.MemberFieldAsync("기록 수량"));
        Assert.Equal("미확인", await fixture.MemberFieldAsync("기록 시각"));
        await fixture.AssertNoWritesAsync();
    }

    public static IEnumerable<object[]> RecordedTimeCases()
    {
        yield return [LastConvertible, true, "12-31 23:59"];
        yield return [LastConvertible.AddTicks(1), true, "미확인"];
        yield return [Start.AddMinutes(10), false, "11-07 09:10"];
        yield return [DateTime.SpecifyKind(default, DateTimeKind.Utc), false, "미확인"];
    }

    [Theory]
    [MemberData(nameof(RecordedTimeCases))]
    public async Task 실제부모에서_한국시간상한직후와정상기본값_및완료null을구분한다(DateTime rawTime, bool stageEntry, string expected)
    {
        await using var fixture = await Fixture.CreateAsync(rawTime);
        await fixture.StartAsync(stageEntry);
        var member = Assert.Single(fixture.Model.Snapshot!.Workflow!.SelectedGroup!.Members);
        Assert.Equal(rawTime, member.CreatedAtUtc);
        Assert.Null(member.CompletedAtUtc);
        Assert.Null(member.StartedAtUtc);
        Assert.Equal(expected, await fixture.MemberFieldAsync("기록 시각"));
        Assert.Equal("미확인", await fixture.MemberFieldAsync("완료 확인"));
        Assert.Equal("6개", await fixture.MemberFieldAsync("기록 수량"));
        Assert.Contains(Product, await fixture.TextAsync());
        Assert.Empty(fixture.Api.DetailIds);
        await fixture.AssertNoWritesAsync();
    }

    [Fact]
    public async Task 실제UseCase의정상최대상한직후응답을_가상소비fixture로내보낸다()
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, rawTime) in new[]
        {
            ("normal", Start.AddMinutes(10)),
            ("max", DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)),
            ("lastConvertible", LastConvertible),
            ("firstOverflow", LastConvertible.AddTicks(1))
        })
        {
            await using var fixture = await Fixture.CreateAsync(rawTime);
            var member = Assert.Single(fixture.Selected.Period!.Workflow!.SelectedGroup!.Members);
            Assert.Equal(rawTime, member.CreatedAtUtc);
            Assert.Equal(1102, member.OutboundPlanId);
            Assert.Equal(6, member.Quantity);
            Assert.Equal(10, fixture.Detail.Quantity);
            payload.Add(name, new
            {
                listing = fixture.Listing,
                selected = fixture.Selected,
                detail = fixture.Detail,
                batchKey = fixture.GroupKey,
                warehouseId = 101
            });
            await fixture.AssertNoWritesAsync();
        }

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var json = JsonSerializer.Serialize(payload, options);
        using (var document = JsonDocument.Parse(json))
        {
            Assert.Equal("9999-12-31T23:59:59.9999999Z", document.RootElement.GetProperty("max")
                .GetProperty("selected").GetProperty("period").GetProperty("workflow").GetProperty("selectedGroup")
                .GetProperty("members")[0].GetProperty("createdAtUtc").GetString());
        }
        var evidence = Environment.GetEnvironmentVariable("W3_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(evidence)) return;
        var directory = Path.GetFullPath(evidence);
        Directory.CreateDirectory(directory);
        // The run owner explicitly supplies its artifact directory; prior fixture output is never overwritten.
        await using var output = new FileStream(Path.Combine(directory, "usecase-fixtures.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await output.WriteAsync(Encoding.UTF8.GetBytes(json));
    }

    private static T Value<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static 출고예정검토목록조회요청 Request(string? key = null) => new()
    {
        WarehouseId = 101, PeriodStartUtc = Start, PeriodEndUtc = Start.AddHours(3),
        Status = 출고예정검토조회상태코드.전체, Page = 0, PageSize = 12,
        PeriodStage = OutboundWorkflowStageCodes.Picking, BatchPage = 0, BatchPageSize = 6,
        BatchKey = key, MemberPage = 0, MemberPageSize = 12
    };

    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly SsalddelContext Db;
        internal readonly Api Api;
        internal readonly List<출고회차ViewModel> Models = [];
        internal readonly ServiceProvider Services;
        internal readonly BatchRenderer Renderer;
        internal readonly DateTime RawTime;
        internal readonly 출고예정검토목록페이지응답 Listing;
        internal readonly 출고예정검토목록페이지응답 Selected;
        internal readonly 출고예정검토상세응답 Detail;
        internal readonly string GroupKey;
        internal 출고회차ViewModel Model => Assert.Single(Models);

        private Fixture(SsalddelContext db, 출고예정검토UseCase useCase, DateTime rawTime,
            출고예정검토목록페이지응답 listing, 출고예정검토목록페이지응답 selected,
            출고예정검토상세응답 detail, string groupKey)
        {
            Db = db;
            RawTime = rawTime;
            Listing = listing;
            Selected = selected;
            Detail = detail;
            GroupKey = groupKey;
            Api = new(useCase);
            var user = new User();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton<IJSRuntime, NoopJs>();
            services.AddSingleton<NavigationManager>(new Navigation());
            services.AddSingleton<ISsalddelJsonApiClient>(Api);
            services.AddSingleton<I출고예정검토페이지Service>(Api);
            services.AddSingleton<ISsalddel현재사용자Context>(user);
            services.AddSingleton<IWorkflowRecoveryContext>(new Scope(user));
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

        internal static async Task<Fixture> CreateAsync(DateTime rawTime)
        {
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
            try
            {
                db.창고.Add(new 창고 { Id = 101, 소유자UserId = "worker", 창고명 = "동부 가상 창고", 주소 = "가상 주소", IsActive = true });
                db.출고예정.Add(new 출고예정
                {
                    Id = 1102, 출고창고Id = 101, 수량 = 10, 상품명 = Product, SKU = "W3-SPLIT-BOX",
                    주문참조번호 = "W3-OUTBOUND-1102", 상태 = 출고상태.준비중, CreatedAt = Start, UpdatedAt = Start
                });
                db.피킹포장작업.Add(new 피킹포장작업
                {
                    Id = 903, 출고예정Id = 1102, 창고Id = 101, 상품명 = Product, SKU = "W3-SPLIT-BOX",
                    주문참조번호 = "W3-OUTBOUND-1102", 작업Key = "picking:903", 작업유형 = 피킹포장작업유형.피킹,
                    수량 = 6, 상태 = 피킹포장작업상태.대기, 작업자UserId = "private-worker", 작업자표시명 = "가상 담당자",
                    CreatedAt = rawTime, UpdatedAt = Start
                });
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                var useCase = new 출고예정검토UseCase(db, new CurrentUser());
                var listing = Value(await useCase.목록Async(Request(), default));
                var key = Assert.Single(listing.Period!.Workflow!.Groups).Key;
                var selected = Value(await useCase.목록Async(Request(key), default));
                var detail = Value(await useCase.상세Async(1102, default));
                Assert.Equal(key, selected.Period!.Workflow!.SelectedGroupKey);
                Assert.Equal(rawTime, Assert.Single(selected.Period.Workflow.SelectedGroup!.Members).CreatedAtUtc);
                return new(db, useCase, rawTime, listing, selected, detail, key);
            }
            catch
            {
                await db.DisposeAsync();
                throw;
            }
        }

        internal async Task StartAsync(bool stageEntry)
        {
            var state = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Picking, 101, Start, Start.AddHours(3), 0, GroupKey, 0, 0);
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Start(state, stageEntry));
            await DrainAsync();
            if (!stageEntry)
            {
                // Existing real parent owns its PeriodWorkspace and its detail/return transitions.
                await ClickAsync("회차별 현황");
                await Renderer.Dispatcher.InvokeAsync(() => Model.LoadNavigationAsync(state));
                await DrainAsync();
            }
        }
        internal async Task ClickAsync(string label)
        {
            await Renderer.Dispatcher.InvokeAsync(() => Renderer.Click(label));
            await DrainAsync();
        }
        internal Task<string> TextAsync() => Renderer.Dispatcher.InvokeAsync(Renderer.VisibleText);
        internal Task<string> MemberFieldAsync(string label) => Renderer.Dispatcher.InvokeAsync(() => Renderer.MemberField(1102, label));
        private async Task DrainAsync()
        {
            for (var index = 0; index < 4; index++) await Renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
            Assert.Empty(Renderer.Errors);
        }
        internal async Task AssertNoWritesAsync()
        {
            Assert.Equal(0, Api.Writes);
            Assert.Empty(Db.ChangeTracker.Entries());
            var plan = await Db.출고예정.AsNoTracking().SingleAsync();
            Assert.Equal(1102, plan.Id);
            Assert.Equal(10, plan.수량);
            Assert.Equal(Start, plan.CreatedAt);
            Assert.Equal(출고상태.준비중, plan.상태);
            var task = await Db.피킹포장작업.AsNoTracking().SingleAsync();
            Assert.Equal(903, task.Id);
            Assert.Equal(1102, task.출고예정Id);
            Assert.Equal(6, task.수량);
            Assert.Equal(RawTime, task.CreatedAt);
            Assert.Equal(피킹포장작업상태.대기, task.상태);
            Assert.Equal(0, await Db.SaveChangesAsync());
        }
        public async ValueTask DisposeAsync()
        {
            await Renderer.DisposeAsync();
            await Services.DisposeAsync();
            await Db.DisposeAsync();
        }
    }

    public sealed class HostHarness : ComponentBase
    {
        [Parameter] public OutboundBatchNavigationState State { get; set; } = new("picking");
        [Parameter] public long? OutboundPlanId { get; set; }
        [Parameter] public bool StageEntry { get; set; } = true;
        internal List<OutboundBatchNavigationState> Published { get; } = [];
        internal List<long?> Selected { get; } = [];
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            if (StageEntry)
            {
                builder.OpenComponent<SsalddelOutboundBatchWorkspace>(1);
                builder.AddAttribute(2, nameof(SsalddelOutboundBatchWorkspace.State), State);
                builder.AddAttribute(3, nameof(SsalddelOutboundBatchWorkspace.OutboundPlanId), OutboundPlanId);
                builder.AddAttribute(4, nameof(SsalddelOutboundBatchWorkspace.OnNavigationChanged), EventCallback.Factory.Create<OutboundBatchNavigationState>(this, Navigate));
                builder.AddAttribute(5, nameof(SsalddelOutboundBatchWorkspace.OnPlanSelected), EventCallback.Factory.Create<long?>(this, Select));
                builder.CloseComponent();
            }
            else
            {
                builder.OpenComponent<SsalddelOutboundPlanReviewWorkspace>(1);
                builder.AddAttribute(2, nameof(SsalddelOutboundPlanReviewWorkspace.OutboundPlanId), OutboundPlanId);
                builder.AddAttribute(3, nameof(SsalddelOutboundPlanReviewWorkspace.OnPlanSelected), EventCallback.Factory.Create<long?>(this, Select));
                builder.CloseComponent();
            }
        }
        private void Navigate(OutboundBatchNavigationState state) { State = state; OutboundPlanId = null; Published.Add(state); }
        private void Select(long? id) { OutboundPlanId = id; Selected.Add(id); }
    }

    private sealed class CurrentUser : ICurrentUserAccessor
    {
        public string? UserId => "worker";
        public string? Role => 역할명.창고관리자;
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class User : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot 현재사용자 { get; } = new("worker", "가상 출고 담당", []);
    }
    private sealed class Scope(User user) : IWorkflowRecoveryContext
    {
        public WorkflowRecoverySnapshot Capture() => new(user.현재사용자.UserId!, 1, new string('a', 32));
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => user.현재사용자.인증됨 && snapshot == Capture();
    }
    private sealed class Api(출고예정검토UseCase useCase) : I출고예정검토페이지Service, ISsalddelJsonApiClient
    {
        internal List<출고예정검토목록조회요청> Reads { get; } = [];
        internal List<long> DetailIds { get; } = [];
        internal 출고예정검토상세응답? LastDetail;
        internal int Writes;
        public async Task<출고예정검토목록페이지응답> 목록조회Async(출고예정검토목록조회요청 request, CancellationToken cancellationToken = default)
        {
            Reads.Add(request);
            return Value(await useCase.목록Async(request, cancellationToken));
        }
        public async Task<출고예정검토상세응답?> 상세조회Async(long outboundPlanId, CancellationToken cancellationToken = default)
        {
            DetailIds.Add(outboundPlanId);
            return LastDetail = Value(await useCase.상세Async(outboundPlanId, cancellationToken));
        }
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        {
            Assert.Equal("api/v1/warehouse-operations/warehouses", path);
            Assert.Equal(typeof(창고목록응답), typeof(TResponse));
            Assert.False(allowNotFound);
            object response = new 창고목록응답 { Items = [new() { Id = 101, 창고명 = "동부 가상 창고", IsActive = true }] };
            return Task.FromResult((TResponse?)response);
        }
        public Task<출고운송인계완료응답> 인계완료Async(long id, 출고운송인계완료요청 request, CancellationToken cancellationToken = default) => Write<출고운송인계완료응답>();
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => Write<TResponse?>();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => Write<object>();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default) => Write<object>();
        private Task<T> Write<T>() { Writes++; throw new InvalidOperationException("시각 표시와 동일 출고 복귀는 업무 쓰기를 실행하지 않습니다."); }
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
        internal Task Start(OutboundBatchNavigationState state, bool stageEntry)
        {
            Harness = (HostHarness)InstantiateComponent(typeof(HostHarness));
            root = AssignRootComponentId(Harness);
            return RenderRootComponentAsync(root, ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(HostHarness.State)] = state,
                [nameof(HostHarness.StageEntry)] = stageEntry
            }));
        }
        internal string VisibleText() { var result = new StringBuilder(); Append(root, result); return result.ToString(); }
        internal string MemberField(long planId, string label)
            => FindMemberField(root, planId, label) ?? throw new InvalidOperationException("실제 구성 행의 필드가 없습니다. " + label);
        private string? FindMemberField(int id, long planId, string label)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component)
                {
                    var nested = FindMemberField(frame.ComponentId, planId, label);
                    if (nested is not null) return nested;
                }
                if (frame.FrameType != RenderTreeFrameType.Element || frame.ElementName != "article") continue;
                var contents = frames.Array.Skip(index + 1).Take(frame.ElementSubtreeLength - 1).ToArray();
                var attributes = contents.TakeWhile(item => item.FrameType == RenderTreeFrameType.Attribute).ToArray();
                if (!attributes.Any(item => item.AttributeName == "class" && item.AttributeValue as string == "flow-member")
                    || !attributes.Any(item => item.AttributeName == "data-plan-id" && Convert.ToString(item.AttributeValue, System.Globalization.CultureInfo.InvariantCulture) == planId.ToString(System.Globalization.CultureInfo.InvariantCulture))) continue;
                for (var position = 0; position < contents.Length; position++)
                {
                    var titleFrame = contents[position];
                    var isElementTitle = titleFrame.FrameType == RenderTreeFrameType.Element && titleFrame.ElementName == "dt";
                    var isMarkupTitle = titleFrame.FrameType == RenderTreeFrameType.Markup
                        && System.Text.RegularExpressions.Regex.IsMatch(titleFrame.MarkupContent,
                            @"<dt\b[^>]*>\s*" + System.Text.RegularExpressions.Regex.Escape(label) + @"\s*</dt>");
                    if (!isElementTitle && !isMarkupTitle) continue;
                    if (isElementTitle && RangeText(contents, position + 1, titleFrame.ElementSubtreeLength - 1) != label) continue;
                    for (var value = position + (isElementTitle ? titleFrame.ElementSubtreeLength : 1); value < contents.Length; value++)
                    {
                        if (contents[value].FrameType == RenderTreeFrameType.Element && contents[value].ElementName == "dd")
                            return RangeText(contents, value + 1, contents[value].ElementSubtreeLength - 1);
                        if (contents[value].FrameType == RenderTreeFrameType.Element && contents[value].ElementName == "dt") break;
                    }
                }
            }
            return null;
        }
        private string RangeText(RenderTreeFrame[] frames, int start, int length)
        {
            var result = new StringBuilder();
            foreach (var frame in frames.Skip(start).Take(length))
            {
                if (frame.FrameType == RenderTreeFrameType.Component) Append(frame.ComponentId, result);
                else if (frame.FrameType == RenderTreeFrameType.Text) result.Append(frame.TextContent);
                else if (frame.FrameType == RenderTreeFrameType.Markup) result.Append(frame.MarkupContent);
            }
            return result.ToString();
        }
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
