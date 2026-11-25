using Ssalddel.Contracts.Common.Sales;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 판매채널주문상세조회경합Tests
{
    [Fact]
    public async Task 이전주문조회가진행중이어도최신주문을실제로조회하고늦은이전성공을버린다()
    {
        var service = new ControlledService();
        var previous = service.Prepare(22); var latest = service.Prepare(33);
        var model = new 판매채널주문상세PageViewModel(service);
        var oldRead = model.조회Async(22);
        Assert.Equal(new long[] { 22 }, service.Calls.Select(call => call.OrderId));
        var currentRead = model.조회Async(33);
        try
        {
            Assert.Equal(new long[] { 22, 33 }, service.Calls.Select(call => call.OrderId));
            Assert.Equal(33, model.요청OrderId); Assert.True(model.처리중); Assert.Null(model.상세);
            latest.Complete(Detail(33, "현재 주문 C"));
            Assert.True(await currentRead);
            var current = model.상세; var completed = model.마지막완료시각;
            Assert.Equal(33, current!.주문.OrderId); Assert.Equal("현재 주문 C", current.주문.대표상품명);

            previous.Complete(Detail(22, "이전 주문 B"));
            await oldRead;

            Assert.Equal(33, model.요청OrderId); Assert.Same(current, model.상세);
            Assert.True(model.성공함); Assert.False(model.처리중); Assert.False(model.찾을수없음);
            Assert.Null(model.오류); Assert.Null(model.오류메시지); Assert.Equal(completed, model.마지막완료시각);
            Assert.Equal(0, service.ListCalls);
        }
        finally
        {
            previous.Complete(Detail(22, "정리용 이전 응답")); latest.Complete(Detail(33, "정리용 최신 응답"));
            await Task.WhenAll(oldRead, currentRead);
        }
    }

    [Fact]
    public async Task 선택해제뒤취소를무시한이전응답이도착해도상세와작업상태를되살리지않는다()
    {
        var service = new ControlledService(); var previous = service.Prepare(22);
        var model = new 판매채널주문상세PageViewModel(service);
        var reading = model.조회Async(22);
        Assert.Equal(22, Assert.Single(service.Calls).OrderId);
        try
        {
            model.선택해제();
            Assert.Null(model.요청OrderId); Assert.Null(model.상세);

            previous.Complete(Detail(22, "이탈한 주문 B"));
            await reading;

            Assert.Null(model.요청OrderId); Assert.Null(model.상세); Assert.False(model.찾을수없음);
            Assert.False(model.처리중); Assert.False(model.성공함); Assert.False(model.오류발생);
            Assert.Null(model.오류); Assert.Null(model.오류메시지); Assert.Null(model.성공메시지); Assert.Null(model.마지막완료시각);
            Assert.Equal(0, service.ListCalls);
        }
        finally
        {
            previous.Complete(Detail(22, "정리용 이전 응답"));
            await reading;
        }
    }

    [Fact]
    public async Task 최신주문실패뒤이전주문성공이도착해도최신오류와빈상세를보존한다()
    {
        await using var fixture = new Sequence();
        var previous = fixture.Start(22); var latest = fixture.Start(33);
        Assert.Equal(new long[] { 22, 33 }, fixture.Service.Calls.Select(call => call.OrderId));
        latest.Pending.Fail(new HttpRequestException("CURRENT_FAILURE"));
        Assert.False(await latest.Task);
        var error = fixture.Model.오류; var message = fixture.Model.오류메시지; var completed = fixture.Model.마지막완료시각;
        Assert.True(fixture.Model.오류발생); Assert.Contains("CURRENT_FAILURE", message);

        previous.Pending.Complete(Detail(22, "늦은 이전 상품")); await previous.Task;

        Assert.Equal(33, fixture.Model.요청OrderId); Assert.Null(fixture.Model.상세); Assert.False(fixture.Model.찾을수없음);
        Assert.True(fixture.Model.오류발생); Assert.False(fixture.Model.처리중); Assert.False(fixture.Model.성공함);
        Assert.Same(error, fixture.Model.오류); Assert.Equal(message, fixture.Model.오류메시지); Assert.Equal(completed, fixture.Model.마지막완료시각);
    }

    [Fact]
    public async Task 이전주문의늦은실패가최신주문의성공상세와완료시각을덮지않는다()
    {
        await using var fixture = new Sequence();
        var previous = fixture.Start(22); var latest = fixture.Start(33);
        Assert.Equal(new long[] { 22, 33 }, fixture.Service.Calls.Select(call => call.OrderId));
        latest.Pending.Complete(Detail(33, "현재 상품")); Assert.True(await latest.Task);
        var detail = fixture.Model.상세; var completed = fixture.Model.마지막완료시각;

        previous.Pending.Fail(new HttpRequestException("OLD_FAILURE")); await previous.Task;

        Assert.Equal(33, fixture.Model.요청OrderId); Assert.Same(detail, fixture.Model.상세);
        Assert.True(fixture.Model.성공함); Assert.False(fixture.Model.처리중); Assert.Null(fixture.Model.오류메시지);
        Assert.Equal(completed, fixture.Model.마지막완료시각);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 이전작업의실패나취소종료가진행중인최신조회를종료하지않는다(bool cancelled)
    {
        await using var fixture = new Sequence();
        var previous = fixture.Start(22); var latest = fixture.Start(33);
        Assert.Equal(new long[] { 22, 33 }, fixture.Service.Calls.Select(call => call.OrderId));
        if (cancelled) previous.Pending.Fail(new OperationCanceledException(fixture.Service.Calls[0].Token));
        else previous.Pending.Fail(new HttpRequestException("OLD_FAILURE"));
        await previous.Task;

        Assert.Equal(33, fixture.Model.요청OrderId); Assert.True(fixture.Model.처리중); Assert.Null(fixture.Model.상세);
        Assert.False(fixture.Model.오류발생); Assert.False(fixture.Model.취소됨); Assert.Null(fixture.Model.마지막완료시각);
        latest.Pending.Complete(Detail(33, "최신 상품")); Assert.True(await latest.Task);
        Assert.Equal(33, fixture.Model.상세!.주문.OrderId); Assert.False(fixture.Model.처리중);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 같은ID재조회도독립조회로실행하여이전세대의성공이나실패를버린다(bool oldFails)
    {
        await using var fixture = new Sequence();
        var previous = fixture.Start(33); var latest = fixture.Start(33);
        Assert.Equal(new long[] { 33, 33 }, fixture.Service.Calls.Select(call => call.OrderId));
        latest.Pending.Complete(Detail(33, "같은 ID의 최신 판본")); Assert.True(await latest.Task);
        var detail = fixture.Model.상세; var completed = fixture.Model.마지막완료시각;

        if (oldFails) previous.Pending.Fail(new HttpRequestException("OLD_SAME_ID_FAILURE"));
        else previous.Pending.Complete(Detail(33, "같은 ID의 이전 판본"));
        await previous.Task;

        Assert.Same(detail, fixture.Model.상세); Assert.Equal("같은 ID의 최신 판본", fixture.Model.상세!.주문.대표상품명);
        Assert.True(fixture.Model.성공함); Assert.False(fixture.Model.처리중); Assert.Null(fixture.Model.오류메시지);
        Assert.Equal(completed, fixture.Model.마지막완료시각);
    }

    [Fact]
    public async Task 선택해제뒤이전조회가실패해도해제된화면에오류와완료시각을되살리지않는다()
    {
        await using var fixture = new Sequence(); var previous = fixture.Start(22);
        Assert.Equal(22, Assert.Single(fixture.Service.Calls).OrderId);
        fixture.Model.선택해제();

        previous.Pending.Fail(new HttpRequestException("CLEARED_ORDER_FAILURE")); await previous.Task;

        Assert.Null(fixture.Model.요청OrderId); Assert.Null(fixture.Model.상세); Assert.False(fixture.Model.찾을수없음);
        Assert.False(fixture.Model.처리중); Assert.False(fixture.Model.오류발생); Assert.False(fixture.Model.성공함);
        Assert.Null(fixture.Model.오류메시지); Assert.Null(fixture.Model.성공메시지); Assert.Null(fixture.Model.마지막완료시각);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task 잘못된최신ID는서비스를호출하지않고진행중이던이전상세도되살리지않는다(long invalidId)
    {
        await using var fixture = new Sequence(); var previous = fixture.Start(22);
        Assert.False(await fixture.Model.조회Async(invalidId));
        Assert.Equal(22, Assert.Single(fixture.Service.Calls).OrderId);
        var error = fixture.Model.오류메시지; var completed = fixture.Model.마지막완료시각;
        Assert.True(fixture.Model.오류발생); Assert.Null(fixture.Model.상세);

        previous.Pending.Complete(Detail(22, "잘못된 주소 이전의 상품")); await previous.Task;

        Assert.Null(fixture.Model.상세); Assert.False(fixture.Model.성공함); Assert.False(fixture.Model.처리중);
        Assert.True(fixture.Model.오류발생); Assert.Equal(error, fixture.Model.오류메시지); Assert.Equal(completed, fixture.Model.마지막완료시각);
    }

    [Fact]
    public async Task 작업취소는실제조회토큰을취소하고취소를무시한성공을정상상세로표시하지않는다()
    {
        await using var fixture = new Sequence(); var reading = fixture.Start(22);
        var call = Assert.Single(fixture.Service.Calls);
        fixture.Model.작업취소(); Assert.True(call.Token.IsCancellationRequested);

        reading.Pending.Complete(Detail(22, "취소 이후 도착한 상품"));
        Assert.False(await reading.Task);

        Assert.Null(fixture.Model.상세); Assert.False(fixture.Model.찾을수없음); Assert.True(fixture.Model.취소됨);
        Assert.False(fixture.Model.처리중); Assert.False(fixture.Model.성공함); Assert.Null(fixture.Model.오류메시지);
        var retry = fixture.Start(22); retry.Pending.Complete(Detail(22, "취소 후 다시 확인한 상품"));
        Assert.True(await retry.Task); Assert.Equal(new long[] { 22, 22 }, fixture.Service.Calls.Select(item => item.OrderId));
        Assert.Equal("취소 후 다시 확인한 상품", fixture.Model.상세!.주문.대표상품명);
    }

    [Fact]
    public async Task 이전호출자의취소와늦은응답이새주문의실제조회를취소하지않는다()
    {
        await using var fixture = new Sequence(); using var caller = new CancellationTokenSource();
        var previous = fixture.Start(22, caller.Token); caller.Cancel(); var latest = fixture.Start(33);
        Assert.Equal(new long[] { 22, 33 }, fixture.Service.Calls.Select(call => call.OrderId));
        Assert.True(fixture.Service.Calls[0].Token.IsCancellationRequested); Assert.False(fixture.Service.Calls[1].Token.IsCancellationRequested);

        previous.Pending.Complete(Detail(22, "취소된 이전 요청")); await previous.Task;
        Assert.True(fixture.Model.처리중); Assert.False(fixture.Model.취소됨); Assert.Null(fixture.Model.상세);
        latest.Pending.Complete(Detail(33, "새 요청")); Assert.True(await latest.Task);
        Assert.Equal(33, fixture.Model.상세!.주문.OrderId); Assert.True(fixture.Model.성공함);
    }

    [Fact]
    public async Task 최신요청ID와다른상세응답은성공이나없음으로대체하지않고거절한다()
    {
        await using var fixture = new Sequence(); var reading = fixture.Start(33);
        reading.Pending.Complete(Detail(22, "다른 주문의 비공개 상품"));

        Assert.False(await reading.Task);

        Assert.Equal(33, Assert.Single(fixture.Service.Calls).OrderId); Assert.Equal(33, fixture.Model.요청OrderId);
        Assert.Null(fixture.Model.상세); Assert.False(fixture.Model.찾을수없음); Assert.False(fixture.Model.성공함);
        Assert.True(fixture.Model.오류발생); Assert.False(string.IsNullOrWhiteSpace(fixture.Model.오류메시지));
    }

    [Fact]
    public async Task 기존상세Client의404허용null계약은정확한ID조회뒤없음으로유지한다()
    {
        var api = new NotFoundApi(); var model = new 판매채널주문상세PageViewModel(new 판매채널주문Client(api));

        Assert.True(await model.조회Async(77));

        var call = Assert.Single(api.Calls); Assert.Equal("api/v1/sales-channels/orders/77", call.Path); Assert.True(call.AllowNotFound);
        Assert.Equal(77, model.요청OrderId); Assert.True(model.찾을수없음); Assert.Null(model.상세);
        Assert.True(model.성공함); Assert.False(model.오류발생);
    }

    [Fact]
    public async Task 최신조회의없음결과는이전주문의늦은성공으로채워지지않는다()
    {
        await using var fixture = new Sequence(); var previous = fixture.Start(22); var latest = fixture.Start(33);
        Assert.Equal(new long[] { 22, 33 }, fixture.Service.Calls.Select(call => call.OrderId));
        latest.Pending.Complete(null); Assert.True(await latest.Task);
        var completed = fixture.Model.마지막완료시각;

        previous.Pending.Complete(Detail(22, "이전 주문")); await previous.Task;

        Assert.Equal(33, fixture.Model.요청OrderId); Assert.True(fixture.Model.찾을수없음); Assert.Null(fixture.Model.상세);
        Assert.True(fixture.Model.성공함); Assert.Equal(completed, fixture.Model.마지막완료시각);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("server")]
    public async Task 조회도중세션이나서버가바뀌면이전문맥내용을노출하지않고새문맥으로다시조회한다(string change)
    {
        var recovery = new Recovery(); await using var fixture = new Sequence(recovery);
        var previous = fixture.Start(22); Assert.Equal(22, Assert.Single(fixture.Service.Calls).OrderId);
        if (change == "session") recovery.Revision++; else recovery.Endpoint = new('b', 32);

        previous.Pending.Complete(Detail(22, "이전 문맥의 상품")); Assert.False(await previous.Task);

        Assert.Null(fixture.Model.상세); Assert.False(fixture.Model.성공함); Assert.False(fixture.Model.처리중);
        var latest = fixture.Start(33); latest.Pending.Complete(Detail(33, "새 문맥의 상품")); Assert.True(await latest.Task);
        Assert.Equal(new long[] { 22, 33 }, fixture.Service.Calls.Select(call => call.OrderId));
        Assert.Equal("새 문맥의 상품", fixture.Model.상세!.주문.대표상품명); Assert.True(recovery.CurrentChecks > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 주입된문맥확인이실패하면이전상세를숨기고서비스조회없이거절하며복구뒤다시읽는다(bool hadPrevious)
    {
        var recovery = new Recovery(); await using var fixture = new Sequence(recovery);
        if (hadPrevious)
        {
            var previous = fixture.Start(22); previous.Pending.Complete(Detail(22, "기존 문맥의 상품")); Assert.True(await previous.Task);
        }
        var reads = fixture.Service.Calls.Count; recovery.CaptureFails = true;

        Assert.False(await fixture.Model.조회Async(33));

        Assert.Equal(reads, fixture.Service.Calls.Count); Assert.Null(fixture.Model.상세); Assert.False(fixture.Model.성공함);
        Assert.False(fixture.Model.처리중); Assert.False(string.IsNullOrWhiteSpace(fixture.Model.오류메시지));
        recovery.CaptureFails = false;
        var retry = fixture.Start(33); retry.Pending.Complete(Detail(33, "복구된 문맥의 상품")); Assert.True(await retry.Task);
        Assert.Equal(reads + 1, fixture.Service.Calls.Count); Assert.Equal(33, fixture.Service.Calls[^1].OrderId);
        Assert.Equal("복구된 문맥의 상품", fixture.Model.상세!.주문.대표상품명);
    }

    private static 판매채널주문상세응답 Detail(long id, string product)
        => new() { 주문 = new() { OrderId = id, 대표상품명 = product } };

    private sealed record Call(long OrderId, CancellationToken Token);
    private sealed class PendingRead(long orderId)
    {
        public long OrderId { get; } = orderId;
        public readonly TaskCompletionSource<판매채널주문상세응답?> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete(판매채널주문상세응답? result) => Completion.TrySetResult(result);
        public void Fail(Exception error) => Completion.TrySetException(error);
    }

    private sealed record Attempt(PendingRead Pending, Task<bool> Task);
    private sealed class Sequence : IAsyncDisposable
    {
        public ControlledService Service { get; } = new();
        public 판매채널주문상세PageViewModel Model { get; }
        private readonly List<Attempt> attempts = [];
        public Sequence(IWorkflowRecoveryContext? recovery = null) => Model = new(Service, recovery);
        public Attempt Start(long id, CancellationToken token = default)
        {
            var pending = Service.Prepare(id); var attempt = new Attempt(pending, Model.조회Async(id, token));
            attempts.Add(attempt); return attempt;
        }
        public async ValueTask DisposeAsync()
        {
            foreach (var attempt in attempts) attempt.Pending.Complete(null);
            await Task.WhenAll(attempts.Select(attempt => attempt.Task));
            Assert.Equal(0, Service.ListCalls);
        }
    }

    private sealed class Recovery : IWorkflowRecoveryContext
    {
        public long Revision = 1;
        public string Endpoint = new('a', 32);
        public bool CaptureFails;
        public int CurrentChecks;
        public WorkflowRecoverySnapshot Capture()
            => CaptureFails ? throw new InvalidOperationException("가상 문맥 확인 실패") : new("seller-a", Revision, Endpoint);
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot)
        {
            CurrentChecks++; return snapshot == new WorkflowRecoverySnapshot("seller-a", Revision, Endpoint);
        }
    }

    private sealed class NotFoundApi : ISsalddelJsonApiClient
    {
        public readonly List<(string Path, bool AllowNotFound)> Calls = [];
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        {
            Assert.Equal(typeof(판매채널주문상세응답), typeof(TResponse)); Calls.Add((path, allowNotFound));
            return Task.FromResult<TResponse?>(default);
        }
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest payload, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest payload, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // 취소된 전송도 나중에 응답할 수 있는 경합을 검증하기 위해 token을 의도적으로 무시합니다.
    private sealed class ControlledService : I판매채널주문읽기Service
    {
        private readonly Queue<PendingRead> pending = [];
        public readonly List<Call> Calls = [];
        public int ListCalls { get; private set; }
        public PendingRead Prepare(long id)
        {
            var read = new PendingRead(id); pending.Enqueue(read); return read;
        }
        public Task<판매채널주문상세응답?> 상세조회Async(long orderId, CancellationToken cancellationToken = default)
        {
            Calls.Add(new(orderId, cancellationToken));
            var read = pending.Dequeue();
            Assert.Equal(read.OrderId, orderId);
            return read.Completion.Task;
        }
        public Task<판매채널주문목록응답> 목록조회Async(판매채널주문목록조회요청 request, CancellationToken cancellationToken = default)
        {
            ListCalls++; throw new InvalidOperationException("상세 경합 시험에서 목록을 조회하면 안 됩니다.");
        }
    }
}
