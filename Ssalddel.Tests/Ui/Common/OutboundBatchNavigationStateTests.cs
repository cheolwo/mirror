using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OutboundBatchNavigationStateTests
{
    private static readonly DateTime Start = new(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 공정입력이없으면기존검토진입을그대로허용한다(string? stage)
    {
        Assert.True(OutboundBatchNavigationState.TryCreate(stage, null, null, null, null, null, null, null, out var state, out var error));
        Assert.Null(state); Assert.Null(error);
    }

    [Theory]
    [InlineData(OutboundWorkflowStageCodes.Review)]
    [InlineData(OutboundWorkflowStageCodes.Picking)]
    [InlineData(OutboundWorkflowStageCodes.Packing)]
    [InlineData(OutboundWorkflowStageCodes.Handoff)]
    public void 단계만선택한진입은창고와작업을임의로선택하지않는다(string stage)
    {
        Assert.True(Parse(stage, out var state, out var error));
        Assert.NotNull(state); Assert.Null(error);
        Assert.Equal(stage, state.Stage); Assert.Equal(0, state.WarehouseId); Assert.Null(state.StartUtc); Assert.Null(state.BatchKey);
        Assert.Equal("/warehouse/general/outbound-plan-review?stage=" + stage, state.Href());
    }

    [Fact]
    public void 묶음키의한글과구분문자를주소로이스케이프하고같은문맥으로복원한다()
    {
        const string key = "packing:묶음/ A?x=1&next=2#조각+%\"";
        var expected = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Packing, 34, Start, Start.AddHours(1), 2, key, 1, 3);
        var href = expected.Href(81);
        var uri = new Uri("https://navigation.invalid" + href);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("/warehouse/general/outbound-plan-review", uri.AbsolutePath); Assert.Empty(uri.Fragment);
        Assert.Equal(key, query["batchKey"].ToString()); Assert.False(query.ContainsKey("next"));
        Assert.Equal("81", query["outboundPlanId"].ToString()); Assert.Contains("packing%3A", href, StringComparison.Ordinal);
        Assert.True(OutboundBatchNavigationState.TryCreate(query["stage"], long.Parse(query["warehouseId"].ToString(), CultureInfo.InvariantCulture),
            query["periodStartUtc"], query["periodEndUtc"], int.Parse(query["batchPage"].ToString(), CultureInfo.InvariantCulture), query["batchKey"],
            int.Parse(query["memberPage"].ToString(), CultureInfo.InvariantCulture), int.Parse(query["planPage"].ToString(), CultureInfo.InvariantCulture),
            out var restored, out var error));
        Assert.Null(error); Assert.Equal(expected, restored); Assert.Equal(DateTimeKind.Utc, restored!.StartUtc!.Value.Kind);
        Assert.Equal(href, restored.Href(81));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public void 양수가아닌출고번호는상세연결에포함하지않는다(long id)
    {
        var state = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Review);
        Assert.DoesNotContain("outboundPlanId", state.Href(id), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("stage-space")]
    [InlineData("warehouse")]
    [InlineData("batch-page")]
    [InlineData("member-page")]
    [InlineData("plan-page")]
    [InlineData("start-only")]
    [InlineData("end-only")]
    [InlineData("date-format")]
    [InlineData("unspecified")]
    [InlineData("offset")]
    [InlineData("zero-offset")]
    [InlineData("equal")]
    [InlineData("reverse")]
    [InlineData("too-long")]
    [InlineData("minimum")]
    [InlineData("maximum")]
    [InlineData("key-without-warehouse")]
    [InlineData("key-without-period")]
    [InlineData("member-without-key")]
    [InlineData("key-too-long")]
    [InlineData("key-control")]
    [InlineData("key-blank")]
    public void 잘못된문맥을기본공정이나첫페이지로대체하지않는다(string invalid)
    {
        var stage = OutboundWorkflowStageCodes.Picking; long? warehouse = 34;
        string? start = O(Start), end = O(Start.AddHours(1));
        int? batchPage = 0, memberPage = 0, planPage = 0; string? key = null;
        switch (invalid)
        {
            case "stage": stage = "unsupported"; break;
            case "stage-space": stage = " "; break;
            case "warehouse": warehouse = -1; break;
            case "batch-page": batchPage = -1; break;
            case "member-page": memberPage = -1; break;
            case "plan-page": planPage = -1; break;
            case "start-only": end = null; break;
            case "end-only": start = null; break;
            case "date-format": start = "2026-11-07T00:00:00Z"; break;
            case "unspecified": start = O(DateTime.SpecifyKind(Start, DateTimeKind.Unspecified)); break;
            case "offset": start = O(Start).Replace("Z", "+09:00", StringComparison.Ordinal); break;
            case "zero-offset": start = O(Start).Replace("Z", "+00:00", StringComparison.Ordinal); break;
            case "equal": end = start; break;
            case "reverse": end = O(Start.AddTicks(-1)); break;
            case "too-long": end = O(Start.AddDays(1).AddTicks(1)); break;
            case "minimum": start = O(DateTime.SpecifyKind(DateTime.MinValue.AddDays(9).AddHours(-9).AddTicks(-1), DateTimeKind.Utc)); end = O(DateTime.SpecifyKind(DateTime.MinValue.AddDays(9).AddHours(-8), DateTimeKind.Utc)); break;
            case "maximum": start = O(DateTime.SpecifyKind(DateTime.MaxValue.AddDays(-1).AddHours(-10), DateTimeKind.Utc)); end = O(DateTime.SpecifyKind(DateTime.MaxValue.AddDays(-1).AddHours(-9).AddTicks(1), DateTimeKind.Utc)); break;
            case "key-without-warehouse": key = "picking:task:1"; warehouse = 0; break;
            case "key-without-period": key = "picking:task:1"; start = end = null; break;
            case "member-without-key": memberPage = 1; break;
            case "key-too-long": key = new string('x', 513); break;
            case "key-control": key = "picking:task:\n1"; break;
            case "key-blank": key = " "; break;
        }
        Assert.False(OutboundBatchNavigationState.TryCreate(stage, warehouse, start, end, batchPage, key, memberPage, planPage, out var state, out var error));
        Assert.Null(state); Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void 정확히하루인구간과512자키와큰페이지값을보존한다()
    {
        var key = new string('x', 512);
        Assert.True(OutboundBatchNavigationState.TryCreate(OutboundWorkflowStageCodes.Handoff, long.MaxValue,
            O(Start), O(Start.AddDays(1)), int.MaxValue, key, int.MaxValue, int.MaxValue, out var state, out var error));
        Assert.Null(error); Assert.Equal(key, state!.BatchKey); Assert.Equal(int.MaxValue, state.MemberPage);
        Assert.Equal(long.MaxValue, state.WarehouseId); Assert.Equal(TimeSpan.FromHours(24), state.EndUtc!.Value - state.StartUtc!.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 한국시간입력에서허용하는날짜경계를그대로사용한다(bool minimum)
    {
        var start = minimum ? DateTime.MinValue.AddDays(9).AddHours(-9) : DateTime.MaxValue.AddDays(-1).AddHours(-10);
        var end = minimum ? start.AddHours(1) : DateTime.MaxValue.AddDays(-1).AddHours(-9);
        Assert.True(OutboundBatchNavigationState.TryCreate(OutboundWorkflowStageCodes.Review, 0,
            O(DateTime.SpecifyKind(start, DateTimeKind.Utc)), O(DateTime.SpecifyKind(end, DateTimeKind.Utc)), null, null, null, null,
            out var state, out var error));
        Assert.NotNull(state); Assert.Null(error);
    }

    [Fact]
    public void 직접만든잘못된상태도주소생성시에명시적으로거절한다()
    {
        Assert.Throws<ArgumentException>(() => new OutboundBatchNavigationState("unsupported").Href());
        Assert.Throws<ArgumentException>(() => new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Review, WarehouseId: -1).Href());
    }

    [Fact]
    public async Task 현재모델의공정과한국시간입력및각페이지를추가조회없이캡처한다()
    {
        var fixture = new ModelFixture(); using var model = new 출고회차ViewModel(fixture, fixture, fixture, fixture);
        await model.InitializeAsync(); model.WarehouseId = 34;
        model.StartLocal = new DateTime(2026, 11, 7, 9, 0, 0); model.EndLocal = model.StartLocal.AddHours(1);
        await model.LoadAsync(3); await model.SelectStageAsync(OutboundWorkflowStageCodes.Packing);
        await model.ChangeBatchPageAsync(2); await model.SelectBatchAsync("packing:task:33"); await model.ChangeMemberPageAsync(1);
        var reads = fixture.Reads;
        var captured = OutboundBatchNavigationState.FromModel(model);
        Assert.Equal(new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Packing, 34, Start, Start.AddHours(1), 2, "packing:task:33", 1, 3), captured);
        Assert.Equal(reads, fixture.Reads); Assert.DoesNotContain("private-account", captured.Href(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 주소의선택묶음을직접열어공정구간과각페이지를그대로복원한다()
    {
        var fixture = new ModelFixture(); using var model = new 출고회차ViewModel(fixture, fixture, fixture, fixture);
        await model.InitializeAsync();
        var state = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Packing, 34, Start, Start.AddHours(1), 2, "packing:task:33", 1, 3);

        await model.LoadNavigationAsync(state);

        var read = Assert.Single(fixture.Calls).Request;
        Assert.Equal(state.Stage, read.PeriodStage); Assert.Equal(state.WarehouseId, read.WarehouseId);
        Assert.Equal(state.StartUtc, read.PeriodStartUtc); Assert.Equal(state.EndUtc, read.PeriodEndUtc);
        Assert.Equal(2, read.BatchPage); Assert.Equal("packing:task:33", read.BatchKey); Assert.Equal(1, read.MemberPage); Assert.Equal(3, read.Page);
        Assert.Equal(new DateTime(2026, 11, 7, 9, 0, 0), model.StartLocal); Assert.Equal(DateTimeKind.Unspecified, model.StartLocal.Kind);
        Assert.Equal(state, OutboundBatchNavigationState.FromModel(model)); Assert.Null(model.Error);
        Assert.Equal("packing:task:33", model.Snapshot!.Workflow!.SelectedGroup!.Key);
        Assert.True(model.CanOpenPlan(81)); Assert.False(model.CanOpenPlan(82)); Assert.False(model.CanOpenPlan(999));
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("date")]
    [InlineData("page")]
    [InlineData("key-context")]
    public async Task 잘못된직접진입은이전현황을지우고추가조회를실행하지않는다(string invalid)
    {
        var fixture = new ModelFixture(); using var model = new 출고회차ViewModel(fixture, fixture, fixture, fixture);
        await model.InitializeAsync();
        var original = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Packing, 34, Start, Start.AddHours(1));
        await model.LoadNavigationAsync(original); Assert.NotNull(model.Snapshot);
        var state = invalid switch
        {
            "stage" => original with { Stage = "unsupported" },
            "date" => original with { EndUtc = Start },
            "page" => original with { PlanPage = -1 },
            _ => original with { WarehouseId = 0, BatchKey = "packing:task:33" }
        };
        var reads = fixture.Reads;

        await model.LoadNavigationAsync(state);

        Assert.Equal(reads, fixture.Reads); Assert.Null(model.Snapshot); Assert.False(string.IsNullOrWhiteSpace(model.Error));
        Assert.False(model.IsLoading); Assert.False(model.CanOpenPlan(81));
    }

    [Fact]
    public async Task 접근목록에없는창고로직접진입해도다른창고를자동선택하지않는다()
    {
        var fixture = new ModelFixture(); using var model = new 출고회차ViewModel(fixture, fixture, fixture, fixture);
        await model.InitializeAsync();

        await model.LoadNavigationAsync(new(OutboundWorkflowStageCodes.Review, 999, Start, Start.AddHours(1)));

        Assert.Empty(fixture.Calls); Assert.Equal(999, model.WarehouseId); Assert.Null(model.Snapshot); Assert.NotNull(model.Error);
    }

    [Fact]
    public async Task 원출고페이지조회가실패해도주소복귀문맥에는요청했던페이지를남긴다()
    {
        var fixture = new ModelFixture(); using var model = new 출고회차ViewModel(fixture, fixture, fixture, fixture);
        await model.InitializeAsync(); fixture.FailNextRead(new HttpRequestException("가상 연결 실패"));
        var state = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Handoff, 34, Start, Start.AddHours(1), PlanPage: 5);

        await model.LoadNavigationAsync(state);

        Assert.Null(model.Snapshot); Assert.NotNull(model.Error); Assert.Equal(5, model.PlanPage);
        Assert.Equal(state, OutboundBatchNavigationState.FromModel(model));
        await model.ShowBatchListAsync();
        Assert.Equal(5, model.Snapshot!.Page); Assert.Equal(5, fixture.Calls[^1].Request.Page);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    public async Task 선택묶음의권한거절과미발견을다른묶음으로대체하지않는다(int status)
    {
        var fixture = new ModelFixture(); using var model = new 출고회차ViewModel(fixture, fixture, fixture, fixture);
        await model.InitializeAsync(); fixture.FailNextRead(new SsalddelApiException("가상 접근 거절", status, "read", "", null));
        var state = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Packing, 34, Start, Start.AddHours(1), 2, "packing:task:33", 1, 3);

        await model.LoadNavigationAsync(state);

        Assert.Single(fixture.Calls); Assert.Null(model.Snapshot); Assert.NotNull(model.Error);
        Assert.Equal(state.BatchKey, model.BatchKey); Assert.Equal(3, model.PlanPage); Assert.False(model.CanOpenPlan(81));
    }

    [Fact]
    public async Task 조회취소뒤같은주소로돌아오면같은묶음과페이지를다시읽는다()
    {
        var fixture = new ModelFixture(); using var model = new 출고회차ViewModel(fixture, fixture, fixture, fixture);
        await model.InitializeAsync();
        var state = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Packing, 34, Start, Start.AddHours(1), 2, "packing:task:33", 1, 3);
        var deferred = fixture.DeferNextRead(); var loading = model.LoadNavigationAsync(state);
        var cancelled = Assert.Single(fixture.Calls); model.Suspend(); Assert.True(cancelled.Token.IsCancellationRequested);
        Assert.Equal(3, OutboundBatchNavigationState.FromModel(model).PlanPage);

        await model.LoadNavigationAsync(state);
        var restored = model.Snapshot; deferred.SetResult(ModelFixture.Reply(cancelled.Request)); await loading;

        Assert.NotNull(restored); Assert.Same(restored, model.Snapshot); Assert.Equal(state, OutboundBatchNavigationState.FromModel(model));
        Assert.Equal(2, fixture.Reads); Assert.Null(model.Error); Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task 이전주소의늦은응답이새공정과새조회구간을덮어쓰지않는다()
    {
        var fixture = new ModelFixture(); using var model = new 출고회차ViewModel(fixture, fixture, fixture, fixture);
        await model.InitializeAsync();
        var deferred = fixture.DeferNextRead();
        var old = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Picking, 34, Start, Start.AddHours(1), PlanPage: 2);
        var loading = model.LoadNavigationAsync(old); var stale = Assert.Single(fixture.Calls);
        var current = new OutboundBatchNavigationState(OutboundWorkflowStageCodes.Packing, 34, Start.AddHours(1), Start.AddHours(2), 2, "packing:task:33", 1, 4);

        await model.LoadNavigationAsync(current);
        var confirmed = model.Snapshot; deferred.SetResult(ModelFixture.Reply(stale.Request)); await loading;

        Assert.NotNull(confirmed); Assert.True(stale.Token.IsCancellationRequested); Assert.Same(confirmed, model.Snapshot);
        Assert.Equal(current, OutboundBatchNavigationState.FromModel(model)); Assert.Null(model.Error); Assert.False(model.IsLoading);
    }

    private static string O(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static bool Parse(string stage, out OutboundBatchNavigationState? state, out string? error)
        => OutboundBatchNavigationState.TryCreate(stage, null, null, null, null, null, null, null, out state, out error);

    private sealed class ModelFixture : I출고예정검토페이지Service, ISsalddelJsonApiClient, ISsalddel현재사용자Context, IWorkflowRecoveryContext
    {
        public int Reads { get; private set; }
        public List<(출고예정검토목록조회요청 Request, CancellationToken Token)> Calls { get; } = [];
        private readonly Queue<Func<출고예정검토목록조회요청, Task<출고예정검토목록페이지응답>>> handlers = [];
        public 현재사용자Snapshot 현재사용자 { get; } = new("private-account", "가상 검토자", []);
        public WorkflowRecoverySnapshot Capture() => new("private-account", 1, new string('a', 32));
        public bool IsCurrent(WorkflowRecoverySnapshot captured) => captured == Capture();
        public void FailNextRead(Exception exception) => handlers.Enqueue(_ => Task.FromException<출고예정검토목록페이지응답>(exception));
        public TaskCompletionSource<출고예정검토목록페이지응답> DeferNextRead()
        {
            var deferred = new TaskCompletionSource<출고예정검토목록페이지응답>(TaskCreationOptions.RunContinuationsAsynchronously);
            handlers.Enqueue(_ => deferred.Task); return deferred;
        }
        public Task<출고예정검토목록페이지응답> 목록조회Async(출고예정검토목록조회요청 request, CancellationToken cancellationToken = default)
        {
            Reads++; Calls.Add((request, cancellationToken));
            return handlers.Count > 0 ? handlers.Dequeue()(request) : Task.FromResult(Reply(request));
        }
        public static 출고예정검토목록페이지응답 Reply(출고예정검토목록조회요청 request)
            => new()
            {
                Period = new()
                {
                    WarehouseId = request.WarehouseId!.Value, PeriodStartUtc = request.PeriodStartUtc!.Value, PeriodEndUtc = request.PeriodEndUtc!.Value,
                    ObservedAtUtc = Start, Page = request.Page, PageSize = request.PageSize, TotalCount = 50,
                    Workflow = new()
                    {
                        SelectedStage = request.PeriodStage!, Page = request.BatchPage, PageSize = 6, TotalGroups = 30,
                        Groups = [new() { Key = "packing:task:33" }], SelectedGroupKey = request.BatchKey,
                        SelectedGroup = request.BatchKey is null ? null : new()
                        {
                            Key = request.BatchKey, MemberPage = request.MemberPage, MemberPageSize = 12, MemberCount = 30,
                            Members = [new() { OutboundPlanId = 81, CanOpenReview = true }, new() { OutboundPlanId = 82, CanOpenReview = false, Status = "출고취소" }]
                        }
                    }
                }
            };
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        {
            Assert.Equal("api/v1/warehouse-operations/warehouses", path);
            return Task.FromResult((TResponse?)(object)new 창고목록응답 { Items = [new() { Id = 34, 창고명 = "가상 창고" }] });
        }
        public Task<출고예정검토상세응답?> 상세조회Async(long outboundPlanId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<출고운송인계완료응답> 인계완료Async(long outboundPlanId, 출고운송인계완료요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest payload, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest payload, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
