using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 출고회차ViewModelTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 12)]
    public async Task 초기회차는_한국시간의_현재반일이며_창고를자동선택하지않는다(int utcHour, int localStartHour)
    {
        var fixture = new Fixture(utcHour);
        using var model = fixture.Create();

        await model.InitializeAsync();

        Assert.Equal(new DateTime(2026, 11, 7).AddHours(localStartHour), model.StartLocal);
        Assert.Equal(model.StartLocal.AddHours(12), model.EndLocal);
        Assert.True(model.WarehousesLoaded);
        Assert.Equal(0, model.WarehouseId);
        Assert.Empty(fixture.Reads);
        Assert.Null(model.Snapshot);
    }

    [Fact]
    public async Task 한국시간조회구간은_UTC포함시작과제외종료로_같은창고에전달한다()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        model.StartLocal = new DateTime(2026, 11, 7, 0, 0, 0);
        model.EndLocal = new DateTime(2026, 11, 8, 0, 0, 0);

        await model.LoadAsync();

        var read = Assert.Single(fixture.Reads).Request;
        Assert.Equal(1, read.WarehouseId);
        Assert.Equal(new DateTime(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc), read.PeriodStartUtc);
        Assert.Equal(new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc), read.PeriodEndUtc);
        Assert.Equal(출고예정검토조회상태코드.전체, read.Status);
        Assert.Equal(12, read.PageSize);
        Assert.Equal(Fixture.ObservedAt, model.Snapshot!.ObservedAtUtc);
        Assert.Null(model.Error);
        Assert.False(model.IsLoading);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("warehouse")]
    public async Task 조회뒤입력변경은_이전기간의현황을지운다(string field)
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        Assert.NotNull(model.Snapshot);

        ChangeInput(model, field);

        Assert.Null(model.Snapshot);
        Assert.Null(model.Error);
        Assert.Null(model.Warning);
        Assert.False(model.IsLoading);
        Assert.Single(fixture.Reads);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("warehouse")]
    public async Task 조회중입력변경은_취소를무시한이전응답도버린다(string field)
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        var gate = fixture.DeferNextRead();
        var loading = model.LoadAsync();
        var read = Assert.Single(fixture.Reads);

        ChangeInput(model, field);
        Assert.True(read.Token.IsCancellationRequested);
        gate.SetResult(Fixture.Reply(read.Request));
        await loading;

        Assert.Null(model.Snapshot);
        Assert.Null(model.Error);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task 같은기간새로고침실패는_이전결과와서버확인시각을유지한다()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        var confirmed = model.Snapshot;
        fixture.FailNextRead(new HttpRequestException("offline"));

        await model.LoadAsync();

        Assert.Same(confirmed, model.Snapshot);
        Assert.Equal(Fixture.ObservedAt, model.Snapshot!.ObservedAtUtc);
        Assert.NotNull(model.Error);
        Assert.Contains("이전에 조회한 결과", model.Warning);
        Assert.False(model.IsLoading);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task 같은기간의권한거절은_보관한결과도지운다(int status)
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        fixture.FailNextRead(new SsalddelApiException("denied", status, "read", "", null));

        await model.LoadAsync();

        Assert.Null(model.Snapshot);
        Assert.Null(model.Warning);
        Assert.NotNull(model.Error);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task 새기간조회실패는_이전기간결과를남기지않는다()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        model.MovePeriod(1);
        fixture.FailNextRead(new HttpRequestException("offline"));

        await model.LoadAsync();

        Assert.Null(model.Snapshot);
        Assert.Null(model.Warning);
        Assert.NotNull(model.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 이전기간의늦은성공이나오류는_새기간을덮지않는다(bool oldFails)
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        var oldGate = fixture.DeferNextRead();
        var old = model.LoadAsync();
        model.MovePeriod(1);
        var nextGate = fixture.DeferNextRead();
        var next = model.LoadAsync();
        var nextResponse = Fixture.Reply(fixture.Reads[1].Request);
        nextGate.SetResult(nextResponse);
        await next;

        if (oldFails) oldGate.SetException(new HttpRequestException("old failure"));
        else oldGate.SetResult(Fixture.Reply(fixture.Reads[0].Request));
        await old;

        Assert.Same(nextResponse.Period, model.Snapshot);
        Assert.Null(model.Error);
        Assert.Null(model.Warning);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task 이전기간의finally는_새기간의로딩을종료하지않는다()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        var oldGate = fixture.DeferNextRead();
        var old = model.LoadAsync();
        model.MovePeriod(1);
        var nextGate = fixture.DeferNextRead();
        var next = model.LoadAsync();

        oldGate.SetException(new HttpRequestException("old failure"));
        await old;
        Assert.True(model.IsLoading);
        Assert.Null(model.Error);
        Assert.Null(model.Snapshot);

        var nextResponse = Fixture.Reply(fixture.Reads[1].Request);
        nextGate.SetResult(nextResponse);
        await next;
        Assert.Same(nextResponse.Period, model.Snapshot);
        Assert.False(model.IsLoading);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("logout")]
    [InlineData("session")]
    [InlineData("server")]
    public async Task 조회중계정세션서버변경은_늦은현황을버린다(string change)
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        var gate = fixture.DeferNextRead();
        var loading = model.LoadAsync();

        fixture.ChangeContext(change);
        gate.SetResult(Fixture.Reply(fixture.Reads[0].Request));
        await loading;

        Assert.Null(model.Snapshot);
        Assert.Empty(model.Warehouses);
        Assert.False(model.WarehousesLoaded);
        Assert.Equal(0, model.WarehouseId);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task 표시된결과도_알림없는서버변경뒤에는노출하지않는다()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        Assert.NotNull(model.Snapshot);

        fixture.Recovery.Endpoint = new string('b', 32);

        Assert.Null(model.Snapshot);
        await model.InitializeAsync();
        Assert.Equal(2, fixture.WarehouseReads);
        Assert.Equal(0, model.WarehouseId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 잘못된서버문맥은_조회요청이나화면예외대신_재확인을안내한다(bool invalidData)
    {
        var fixture = new Fixture();
        fixture.Recovery.CaptureFailure = invalidData
            ? new InvalidDataException("invalid endpoint")
            : new ArgumentException("invalid endpoint");
        using var model = fixture.Create();

        await model.InitializeAsync();
        Assert.NotNull(model.Error);
        Assert.False(model.WarehousesLoaded);
        Assert.Equal(0, fixture.WarehouseReads);

        await model.LoadAsync();
        Assert.Empty(fixture.Reads);
        Assert.Null(model.Snapshot);
        Assert.NotNull(model.Error);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task 보기중단과복귀는_같은문맥의확인결과를유지하고_중단한응답을버린다()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        var confirmed = model.Snapshot;
        var gate = fixture.DeferNextRead();
        var loading = model.LoadAsync();
        var read = fixture.Reads[1];

        model.Suspend();
        Assert.True(read.Token.IsCancellationRequested);
        Assert.False(model.IsLoading);
        Assert.Same(confirmed, model.Snapshot);
        await model.InitializeAsync();
        Assert.Equal(1, fixture.WarehouseReads);
        gate.SetResult(Fixture.Reply(read.Request));
        await loading;

        Assert.Same(confirmed, model.Snapshot);
        Assert.Null(model.Error);
        Assert.False(model.IsLoading);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("warehouse")]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("observed")]
    public async Task 기간이나창고가다른응답은_이전결과도성공으로유지하지않는다(string mismatch)
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        fixture.ReadHandlers.Enqueue(request =>
        {
            var reply = Fixture.Reply(request);
            if (mismatch == "missing") reply.Period = null;
            else if (mismatch == "warehouse") reply.Period!.WarehouseId = 2;
            else if (mismatch == "start") reply.Period!.PeriodStartUtc = reply.Period.PeriodStartUtc.AddHours(-1);
            else if (mismatch == "end") reply.Period!.PeriodEndUtc = reply.Period.PeriodEndUtc.AddHours(1);
            else reply.Period!.ObservedAtUtc = default;
            return Task.FromResult(reply);
        });

        await model.LoadAsync();

        Assert.Null(model.Snapshot);
        Assert.Null(model.Warning);
        Assert.NotNull(model.Error);
        Assert.False(model.IsLoading);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25)]
    public async Task 잘못된기간은_서버조회없이안내한다(int hours)
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        model.EndLocal = model.StartLocal.AddHours(hours);

        await model.LoadAsync();

        Assert.Empty(fixture.Reads);
        Assert.Null(model.Snapshot);
        Assert.Contains("24시간", model.Error);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task 창고목록실패와정상빈목록을구별하며_실패는다시조회한다()
    {
        var fixture = new Fixture();
        fixture.WarehouseHandler = () => Task.FromException<창고목록응답?>(new HttpRequestException("offline"));
        using var model = fixture.Create();

        await model.InitializeAsync();
        Assert.False(model.WarehousesLoaded);
        Assert.Empty(model.Warehouses);
        Assert.NotNull(model.Error);

        fixture.WarehouseHandler = () => Task.FromResult<창고목록응답?>(new());
        await model.InitializeAsync();
        Assert.True(model.WarehousesLoaded);
        Assert.Empty(model.Warehouses);
        Assert.Null(model.Error);
        Assert.Equal(2, fixture.WarehouseReads);

        await model.LoadAsync();
        Assert.Empty(fixture.Reads);
        Assert.Contains("창고를 선택", model.Error);
    }

    [Fact]
    public async Task 두번째페이지도_같은창고와기간을전달한다()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        await model.LoadAsync(1);

        var first = fixture.Reads[0].Request;
        var second = fixture.Reads[1].Request;
        Assert.Equal(first.WarehouseId, second.WarehouseId);
        Assert.Equal(first.PeriodStartUtc, second.PeriodStartUtc);
        Assert.Equal(first.PeriodEndUtc, second.PeriodEndUtc);
        Assert.Equal(1, second.Page);
        Assert.Equal(1, model.Snapshot!.Page);
        Assert.Equal(1, model.Snapshot.Items.Single().OutboundPlanId);
    }

    [Fact]
    public async Task 폐기는_요청과인증구독을종료하고_늦은응답이나재호출을무시한다()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        await SelectWarehouseAsync(model);
        var gate = fixture.DeferNextRead();
        var loading = model.LoadAsync();
        var read = Assert.Single(fixture.Reads);
        Assert.Equal(1, fixture.User.SubscriberCount);

        model.Dispose();
        Assert.Equal(0, fixture.User.SubscriberCount);
        Assert.True(read.Token.IsCancellationRequested);
        gate.SetResult(Fixture.Reply(read.Request));
        await loading;
        fixture.User.Switch("other");
        await model.InitializeAsync();
        await model.LoadAsync();

        Assert.Null(model.Snapshot);
        Assert.Empty(model.Warehouses);
        Assert.False(model.IsLoading);
        Assert.Single(fixture.Reads);
        Assert.Equal(1, fixture.WarehouseReads);
    }

    [Fact]
    public async Task 공정과묶음과구성페이지는_원출고페이지를유지하며_하위선택만초기화한다()
    {
        var fixture = new Fixture { IncludeWorkflow = true };
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync(2);
        await model.SelectStageAsync(OutboundWorkflowStageCodes.Packing);
        await model.ChangeBatchPageAsync(3);
        await model.SelectBatchAsync("batch-a");
        await model.ChangeMemberPageAsync(1);

        var request = fixture.Reads[^1].Request;
        Assert.Equal(2, request.Page);
        Assert.Equal(OutboundWorkflowStageCodes.Packing, request.PeriodStage);
        Assert.Equal(3, request.BatchPage);
        Assert.Equal(6, request.BatchPageSize);
        Assert.Equal("batch-a", request.BatchKey);
        Assert.Equal(1, request.MemberPage);
        Assert.Equal(12, request.MemberPageSize);
        Assert.All(fixture.Reads, read =>
        {
            Assert.Equal(1, read.Request.WarehouseId);
            Assert.Equal(fixture.Reads[0].Request.PeriodStartUtc, read.Request.PeriodStartUtc);
            Assert.Equal(fixture.Reads[0].Request.PeriodEndUtc, read.Request.PeriodEndUtc);
        });

        await model.ShowBatchListAsync();
        Assert.Equal(3, model.BatchPage);
        Assert.Null(model.BatchKey);
        Assert.Equal(0, model.MemberPage);
        Assert.Equal(2, fixture.Reads[^1].Request.Page);
        await model.SelectBatchAsync("batch-b");
        await model.SelectStageAsync(OutboundWorkflowStageCodes.Handoff);
        Assert.Equal(0, model.BatchPage);
        Assert.Null(model.BatchKey);
        Assert.Equal(0, model.MemberPage);
        Assert.Equal(2, fixture.Reads[^1].Request.Page);
    }

    [Theory]
    [InlineData("warehouse")]
    [InlineData("start")]
    [InlineData("end")]
    public async Task 창고나기간입력변경은_공정을유지하고_묶음과모든페이지를초기화한다(string field)
    {
        var fixture = new Fixture { IncludeWorkflow = true };
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync(2);
        await model.SelectStageAsync(OutboundWorkflowStageCodes.Packing);
        await model.ChangeBatchPageAsync(2);
        await model.SelectBatchAsync("batch-a");
        await model.ChangeMemberPageAsync(1);

        ChangeInput(model, field);

        Assert.Null(model.Snapshot);
        Assert.Equal(OutboundWorkflowStageCodes.Packing, model.StageCode);
        Assert.Equal(0, model.BatchPage);
        Assert.Null(model.BatchKey);
        Assert.Equal(0, model.MemberPage);
        await model.SelectStageAsync(OutboundWorkflowStageCodes.Handoff);
        Assert.Equal(0, fixture.Reads[^1].Request.Page);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 이전공정의늦은성공과오류와finally는_새공정조회중상태를바꾸지않는다(bool oldFails)
    {
        var fixture = new Fixture { IncludeWorkflow = true };
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync(2);
        var oldGate = fixture.DeferNextRead();
        var oldLoading = model.SelectStageAsync(OutboundWorkflowStageCodes.Packing);
        var oldRead = fixture.Reads[^1];
        var newGate = fixture.DeferNextRead();
        var newLoading = model.SelectStageAsync(OutboundWorkflowStageCodes.Handoff);
        var newRead = fixture.Reads[^1];
        Assert.True(oldRead.Token.IsCancellationRequested);
        Assert.Null(model.Snapshot);
        Assert.Equal(2, newRead.Request.Page);
        if (oldFails) oldGate.SetException(new HttpRequestException("old failure"));
        else oldGate.SetResult(Fixture.FlowReply(oldRead.Request));
        await oldLoading;

        Assert.True(model.IsLoading);
        Assert.Null(model.Snapshot);
        Assert.Null(model.Error);
        newGate.SetResult(Fixture.FlowReply(newRead.Request));
        await newLoading;
        Assert.False(model.IsLoading);
        Assert.Equal(OutboundWorkflowStageCodes.Handoff, model.Snapshot!.Workflow!.SelectedStage);
    }

    [Fact]
    public async Task 구성페이지변경은_이전묶음응답을버리고_새페이지구성원만연다()
    {
        var fixture = new Fixture { IncludeWorkflow = true };
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        await model.SelectBatchAsync("batch-a");
        Assert.True(model.CanOpenPlan(17));
        Assert.False(model.CanOpenPlan(18));
        Assert.False(model.CanOpenPlan(999));
        var gate = fixture.DeferNextRead();
        var oldLoading = model.ChangeMemberPageAsync(1);
        var oldRead = fixture.Reads[^1];
        Assert.False(model.CanOpenPlan(17));
        await model.ChangeMemberPageAsync(2);
        gate.SetResult(Fixture.FlowReply(oldRead.Request));
        await oldLoading;

        Assert.Equal(2, model.MemberPage);
        Assert.Equal(2, model.Snapshot!.Workflow!.SelectedGroup!.MemberPage);
        Assert.True(model.CanOpenPlan(41));
        Assert.False(model.CanOpenPlan(17));
        Assert.False(model.CanOpenPlan(42));
    }

    [Theory]
    [InlineData("account")]
    [InlineData("session")]
    [InlineData("server")]
    public async Task 선택묶음조회중문맥변경은_구성원과선택공정을지우고_늦은응답을버린다(string change)
    {
        var fixture = new Fixture { IncludeWorkflow = true };
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        await model.SelectStageAsync(OutboundWorkflowStageCodes.Packing);
        var gate = fixture.DeferNextRead();
        var loading = model.SelectBatchAsync("batch-a");
        var read = fixture.Reads[^1];
        fixture.ChangeContext(change);
        gate.SetResult(Fixture.FlowReply(read.Request));
        await loading;

        Assert.Null(model.Snapshot);
        Assert.Null(model.BatchKey);
        Assert.Equal(OutboundWorkflowStageCodes.Picking, model.StageCode);
        Assert.False(model.CanOpenPlan(17));
        Assert.Empty(model.Warehouses);
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("batch-page")]
    [InlineData("selected-key")]
    [InlineData("group-key")]
    [InlineData("member-page")]
    [InlineData("workflow-missing")]
    public async Task 요청과다른공정묶음구성응답은_정상결과로노출하지않는다(string mismatch)
    {
        var fixture = new Fixture { IncludeWorkflow = true };
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        fixture.ReadHandlers.Enqueue(request =>
        {
            var reply = Fixture.FlowReply(request);
            var flow = reply.Period!.Workflow!;
            if (mismatch == "stage") flow.SelectedStage = OutboundWorkflowStageCodes.Handoff;
            else if (mismatch == "batch-page") flow.Page++;
            else if (mismatch == "selected-key") flow.SelectedGroupKey = "batch-b";
            else if (mismatch == "group-key") flow.SelectedGroup!.Key = "batch-b";
            else if (mismatch == "member-page") flow.SelectedGroup!.MemberPage++;
            else reply.Period.Workflow = null;
            return Task.FromResult(reply);
        });

        await model.SelectBatchAsync("batch-a");

        Assert.Null(model.Snapshot);
        Assert.NotNull(model.Error);
        Assert.False(model.CanOpenPlan(17));
    }

    [Fact]
    public async Task 같은묶음새로고침실패만_이전확인시각을유지하며_404는목록복귀로회복한다()
    {
        var fixture = new Fixture { IncludeWorkflow = true };
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync(2);
        await model.SelectBatchAsync("batch-a");
        var confirmed = model.Snapshot;
        fixture.FailNextRead(new HttpRequestException("offline"));
        await model.LoadAsync(2);
        Assert.Same(confirmed, model.Snapshot);
        Assert.Equal(Fixture.ObservedAt, model.Snapshot!.ObservedAtUtc);
        Assert.NotNull(model.Warning);
        fixture.FailNextRead(new SsalddelApiException("missing", 404, "read", "", null));
        await model.ChangeMemberPageAsync(1);
        Assert.Null(model.Snapshot);
        Assert.Contains("선택한 작업 묶음", model.Error);
        Assert.Equal("batch-a", model.BatchKey);

        await model.ShowBatchListAsync();

        Assert.Null(model.BatchKey);
        Assert.Equal(0, model.MemberPage);
        Assert.NotNull(model.Snapshot);
        Assert.Null(model.Error);
        Assert.Equal(2, fixture.Reads[^1].Request.Page);
    }

    [Fact]
    public async Task 없는묶음선택과공정코드는_요청을만들지않고_구형응답의원목록은보존한다()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        await SelectWarehouseAsync(model);
        await model.LoadAsync();
        var confirmed = model.Snapshot;
        await model.SelectBatchAsync("not-listed");
        await model.SelectStageAsync("not-a-stage");
        Assert.Single(fixture.Reads);
        Assert.Same(confirmed, model.Snapshot);
        Assert.True(model.CanOpenPlan(1));
        Assert.Null(model.Snapshot!.Workflow);
        Assert.NotNull(model.Error);
    }

    [Fact]
    public async Task 실제조회서비스는_공정과독립된두페이지및묶음키를_안전하게전송한다()
    {
        var fixture = new Fixture();
        var service = new 출고예정검토페이지Service(fixture);
        const string key = "picking:묶음/A?x=1&y=2";
        await service.목록조회Async(new()
        {
            WarehouseId = 1, PeriodStartUtc = new(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc),
            PeriodStage = OutboundWorkflowStageCodes.Packing, Page = 2, PageSize = 12,
            BatchPage = 3, BatchPageSize = 6, BatchKey = key, MemberPage = 4, MemberPageSize = 12
        });
        var path = Assert.Single(fixture.JsonPaths);
        var query = new Uri("http://localhost/" + path).Query.TrimStart('?').Split('&')
            .Select(part => part.Split('=', 2)).ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]));
        Assert.Equal("packing", query["periodStage"]);
        Assert.Equal("2", query["page"]);
        Assert.Equal("3", query["batchPage"]);
        Assert.Equal("6", query["batchPageSize"]);
        Assert.Equal("4", query["memberPage"]);
        Assert.Equal("12", query["memberPageSize"]);
        Assert.Equal(key, query["batchKey"]);
        Assert.DoesNotContain(key, path);
        Assert.DoesNotContain("y", query.Keys);
    }

    private static async Task SelectWarehouseAsync(출고회차ViewModel model)
    {
        await model.InitializeAsync();
        model.WarehouseId = 1;
    }

    private static void ChangeInput(출고회차ViewModel model, string field)
    {
        if (field == "start") model.StartLocal = model.StartLocal.AddHours(1);
        else if (field == "end") model.EndLocal = model.EndLocal.AddHours(-1);
        else model.WarehouseId = 2;
    }

    private sealed class User : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        public 현재사용자Snapshot 현재사용자 { get; private set; } = new("worker", "창고 검토자", []);
        public event Action? Changed;
        public int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;
        public void Switch(string? id)
        {
            현재사용자 = new(id, "창고 검토자", []);
            Changed?.Invoke();
        }
    }

    private sealed class Scope(User user) : IWorkflowRecoveryContext
    {
        public long Revision = 1;
        public string Endpoint = new('a', 32);
        public Exception? CaptureFailure;
        public WorkflowRecoverySnapshot Capture()
        {
            if (CaptureFailure is not null) throw CaptureFailure;
            return new(user.현재사용자.UserId ?? throw new InvalidOperationException(), Revision, Endpoint);
        }
        public bool IsCurrent(WorkflowRecoverySnapshot captured)
            => user.현재사용자.인증됨 && captured == Capture();
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Read(출고예정검토목록조회요청 Request, CancellationToken Token);

    private sealed class Fixture : I출고예정검토페이지Service, ISsalddelJsonApiClient
    {
        public static readonly DateTime ObservedAt = new(2026, 11, 7, 1, 30, 0, DateTimeKind.Utc);
        public readonly User User = new();
        public readonly Scope Recovery;
        public readonly List<Read> Reads = [];
        public readonly Queue<Func<출고예정검토목록조회요청, Task<출고예정검토목록페이지응답>>> ReadHandlers = new();
        public int WarehouseReads;
        public bool IncludeWorkflow;
        public readonly List<string> JsonPaths = [];
        public Func<Task<창고목록응답?>>? WarehouseHandler;
        private readonly Clock clock;

        public Fixture(int utcHour = 2)
        {
            Recovery = new(User);
            clock = new(new DateTimeOffset(2026, 11, 7, utcHour, 30, 0, TimeSpan.Zero));
        }

        public 출고회차ViewModel Create() => new(this, this, User, Recovery, clock);

        public TaskCompletionSource<출고예정검토목록페이지응답> DeferNextRead()
        {
            var gate = new TaskCompletionSource<출고예정검토목록페이지응답>(TaskCreationOptions.RunContinuationsAsynchronously);
            ReadHandlers.Enqueue(_ => gate.Task);
            return gate;
        }

        public void FailNextRead(Exception exception)
            => ReadHandlers.Enqueue(_ => Task.FromException<출고예정검토목록페이지응답>(exception));

        public void ChangeContext(string change)
        {
            if (change == "account") User.Switch("other");
            else if (change == "logout") User.Switch(null);
            else if (change == "session") { Recovery.Revision++; User.Switch("worker"); }
            else Recovery.Endpoint = new('b', 32);
        }

        public static 출고예정검토목록페이지응답 Reply(출고예정검토목록조회요청 request)
            => new()
            {
                Period = new()
                {
                    WarehouseId = request.WarehouseId!.Value,
                    WarehouseName = "가상 창고",
                    PeriodStartUtc = request.PeriodStartUtc!.Value,
                    PeriodEndUtc = request.PeriodEndUtc!.Value,
                    ObservedAtUtc = ObservedAt,
                    Current = new() { PlanCount = 1, ReceivedQuantity = 12 },
                    Items = [new() { OutboundPlanId = 1, ProductName = "포장 상자", Quantity = 12 }],
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalCount = 24
                },
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = 24
            };

        public Task<출고예정검토목록페이지응답> 목록조회Async(출고예정검토목록조회요청 request, CancellationToken cancellationToken = default)
        {
            Reads.Add(new(request, cancellationToken));
            return ReadHandlers.Count > 0 ? ReadHandlers.Dequeue()(request) : Task.FromResult(IncludeWorkflow ? FlowReply(request) : Reply(request));
        }

        public static 출고예정검토목록페이지응답 FlowReply(출고예정검토목록조회요청 request)
        {
            var reply = Reply(request);
            reply.Period!.Workflow = new()
            {
                SelectedStage = request.PeriodStage!, Page = request.BatchPage, PageSize = request.BatchPageSize,
                TotalGroups = 30, Groups = [Group("batch-a", 0), Group("batch-b", 0)],
                SelectedGroupKey = request.BatchKey,
                SelectedGroup = request.BatchKey is null ? null : Group(request.BatchKey, request.MemberPage)
            };
            return reply;
        }

        private static OutboundPeriodGroup Group(string key, int page) => new()
        {
            Key = key, Kind = "recorded-batch", Label = "가상 " + key, SourcePlanCount = 2, TaskCount = 2,
            Quantity = 12, CompletedQuantity = 4, MemberCount = 30, MemberPage = page, MemberPageSize = 12,
            MembershipComplete = true,
            Members = [new() { OutboundPlanId = 17 + page * 12, ProductName = "구성 상품", Quantity = 8, CanOpenReview = true },
                new() { OutboundPlanId = 18 + page * 12, ProductName = "취소 상품", Quantity = 4, CanOpenReview = false }]
        };

        public Task<창고목록응답?> GetWarehousesAsync(CancellationToken cancellationToken = default)
        {
            WarehouseReads++;
            return WarehouseHandler is not null ? WarehouseHandler() : Task.FromResult<창고목록응답?>(new()
            {
                Items = [new() { Id = 1, 창고명 = "가상 창고", IsActive = true }, new() { Id = 2, 창고명 = "다른 가상 창고", IsActive = true }]
            });
        }

        public async Task<TResponse?> GetAsync<TResponse>(string path, string operationName,
            bool allowNotFound = true, CancellationToken cancellationToken = default)
        {
            if (typeof(TResponse) == typeof(출고예정검토목록페이지응답))
            {
                JsonPaths.Add(path);
                Assert.False(allowNotFound);
                return (TResponse?)(object)new 출고예정검토목록페이지응답();
            }
            Assert.Equal("api/v1/warehouse-operations/warehouses", path);
            Assert.False(allowNotFound);
            Assert.Equal(typeof(창고목록응답), typeof(TResponse));
            return (TResponse?)(object?)await GetWarehousesAsync(cancellationToken);
        }

        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName,
            bool allowNotFound = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request,
            string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<출고예정검토상세응답?> 상세조회Async(long outboundPlanId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<출고운송인계완료응답> 인계완료Async(long outboundPlanId, 출고운송인계완료요청 request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<창고요약응답?> CreateWarehouseAsync(창고저장요청 payload, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<입고요청목록응답?> GetInboundsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<입고요청항목응답?> CreateInboundAsync(입고요청저장요청 payload, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<입고상품목록응답?> CompleteInboundAsync(long inboundId, 입고완료요청 payload, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<재고목록응답?> GetInventoryAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
