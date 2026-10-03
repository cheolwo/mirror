using SsalddelApp.Models.Shipper;
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperQueryRecoveryTests
{
    [Fact]
    public async Task 겹친목록조회는_직렬화하고_최신요청만적용한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var lifetime = new ShipperQueryLifetime(fixture.Auth);
        var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var last = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lastStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var applied = new List<string>();
        var finished = 0;
        CancellationToken firstToken = default;
        var firstRun = lifetime.RunAsync(token => { firstToken = token; return first.Task; }, applied.Add,
            ex => throw ex, () => finished++);
        var skippedCalls = 0;
        var skippedRun = lifetime.RunAsync(_ => { skippedCalls++; return Task.FromResult("skipped"); }, applied.Add,
            ex => throw ex, () => finished++);
        var lastRun = lifetime.RunAsync(_ => { lastStarted.SetResult(); return last.Task; }, applied.Add,
            ex => throw ex, () => finished++);

        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(lastStarted.Task.IsCompleted);
        first.SetResult("old");
        await lastStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        last.SetResult("latest");
        await Task.WhenAll(firstRun, skippedRun, lastRun).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "latest" }, applied);
        Assert.Equal(0, skippedCalls);
        Assert.Equal(1, finished);
    }

    [Fact]
    public async Task 이탈후_늦은결과와오류알림을적용하지않는다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var lifetime = new ShipperQueryLifetime(fixture.Auth);
        var response = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = 0;
        var run = lifetime.RunAsync(_ => response.Task, _ => callbacks++, _ => callbacks++, () => callbacks++);
        lifetime.Dispose();
        response.SetException(new IOException("late failure"));
        await run;
        await lifetime.RunAsync(_ => throw new InvalidOperationException("must not query"),
            (int _) => callbacks++, _ => callbacks++, () => callbacks++);
        Assert.Equal(0, callbacks);
    }

    [Fact]
    public async Task 다른의뢰선택은_조회중에도최신Id를보존한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var firstRun = fixture.Model.LoadAsync("A");
        var first = await fixture.Operations.NextAsync();
        var lastRun = fixture.Model.LoadAsync(" B ");
        Assert.Equal("B", fixture.Model.State.LookupRequestId);
        Assert.Null(fixture.Model.State.Request);
        Assert.True(first.Token.IsCancellationRequested);
        first.Completion.SetResult(Request("A", 1100));
        var last = await fixture.Operations.NextAsync();
        Assert.Equal("B", last.Id);
        Assert.True(fixture.Model.State.IsBusy);
        Assert.Null(fixture.Model.State.Request);
        last.Completion.SetResult(Request("B", 2200));
        await Task.WhenAll(firstRun, lastRun);
        Assert.Equal("B", fixture.Model.State.Request?.RequestId);
        Assert.Equal(2200, fixture.Model.PaymentAmount);
        Assert.False(fixture.Model.State.IsBusy);
    }

    [Fact]
    public async Task 이전선택의늦은실패는_현재상세의오류로표시하지않는다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var firstRun = fixture.Model.LoadAsync("A");
        var first = await fixture.Operations.NextAsync();
        var lastRun = fixture.Model.LoadAsync("B");
        first.Completion.SetException(new IOException("A 실패"));
        var last = await fixture.Operations.NextAsync();
        last.Completion.SetResult(Request("B", 2200));
        await Task.WhenAll(firstRun, lastRun);
        Assert.Equal("B", fixture.Model.State.Request?.RequestId);
        Assert.DoesNotContain("A 실패", fixture.Model.State.StatusMessage);
    }

    [Fact]
    public async Task 조회중로그아웃은_개인상태를즉시비우고_늦은응답을차단한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var run = fixture.Model.LoadAsync("A");
        var pending = await fixture.Operations.NextAsync();
        await fixture.Auth.ClearAsync();
        Assert.True(fixture.Model.State.RequiresLogin);
        Assert.False(fixture.Model.State.IsBusy);
        Assert.Null(fixture.Model.State.Request);
        Assert.True(pending.Token.IsCancellationRequested);
        pending.Completion.SetResult(Request("A", 1100));
        await run;
        Assert.Null(fixture.Model.State.Request);
        Assert.Equal(0, fixture.Model.PaymentAmount);
        Assert.Null(fixture.Model.Receipt);
        fixture.Model.OpenPaymentWindow();
        Assert.False(fixture.Model.PaymentWindowOpen);
    }

    [Fact]
    public async Task 동일사용자재로그인도_이전로그인의응답을재사용하지않는다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var oldRevision = fixture.Auth.SessionRevision;
        var run = fixture.Model.LoadAsync("A");
        var pending = await fixture.Operations.NextAsync();
        await fixture.Auth.ClearAsync();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot(token: "new-login"));
        pending.Completion.SetResult(Request("A", 1100));
        await run;
        Assert.True(fixture.Auth.SessionRevision > oldRevision);
        Assert.Null(fixture.Model.State.Request);
        Assert.Equal(0, fixture.Model.PaymentAmount);
    }

    [Fact]
    public async Task 같은사용자토큰갱신은_진행중조회문맥을유지한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var revision = fixture.Auth.SessionRevision;
        var run = fixture.Model.LoadAsync("A");
        var pending = await fixture.Operations.NextAsync();
        Assert.True(await fixture.Auth.TryRefreshAsync(ShipperRecoveryFixture.Snapshot(token: "refreshed"), revision, fixture.Auth.RefreshToken));
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.False(pending.Token.IsCancellationRequested);
        pending.Completion.SetResult(Request("A", 1100));
        await run;
        Assert.Equal("A", fixture.Model.State.Request?.RequestId);
    }

    [Fact]
    public async Task 완료상세로그아웃은_결제창과메모까지지운다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var run = fixture.Model.LoadAsync("A");
        (await fixture.Operations.NextAsync()).Completion.SetResult(Request("A", 1100));
        await run;
        fixture.Model.OpenPaymentWindow();
        fixture.Model.SetPaymentMemo("private");
        Assert.True(fixture.Model.PaymentWindowOpen);
        await fixture.Auth.ClearAsync();
        Assert.Null(fixture.Model.State.Request);
        Assert.False(fixture.Model.PaymentWindowOpen);
        Assert.Null(fixture.Model.PaymentMemo);
        Assert.Equal(0, fixture.Model.PaymentAmount);
    }

    [Fact]
    public async Task 상세이탈은_늦은응답과추가원장알림을차단한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var run = fixture.Model.InitializeAsync("A");
        var pending = await fixture.Operations.NextAsync();
        var notifications = 0;
        fixture.Model.StateChanged += () => notifications++;
        fixture.Model.Dispose();
        pending.Completion.SetResult(Request("A", 1100));
        await run;
        fixture.Observer.RequestRefresh("A", "after dispose");
        await fixture.Model.LoadAsync("B");
        Assert.Equal(1, fixture.Operations.Calls);
        Assert.Equal(0, notifications);
        Assert.Null(fixture.Model.State.Request);
    }

    [Fact]
    public async Task 빈Id로복귀하면_이전상세의늦은결과를적용하지않는다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var run = fixture.Model.LoadAsync("A");
        var pending = await fixture.Operations.NextAsync();
        await fixture.Model.LoadAsync(" ");
        pending.Completion.SetResult(Request("A", 1100));
        await run;
        Assert.Empty(fixture.Model.State.LookupRequestId);
        Assert.Null(fixture.Model.State.Request);
        Assert.False(fixture.Model.State.IsBusy);
    }

    [Fact]
    public async Task 외부취소는_실패안내없이조회중상태를해제한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var cancellation = new CancellationTokenSource();
        var run = fixture.Model.LoadAsync("A", cancellationToken: cancellation.Token);
        var pending = await fixture.Operations.NextAsync();
        cancellation.Cancel();
        pending.Completion.SetResult(Request("A", 1100));
        await run;
        Assert.False(fixture.Model.State.IsBusy);
        Assert.Null(fixture.Model.State.Request);
        Assert.DoesNotContain("조회 실패", fixture.Model.State.StatusMessage);
    }

    private static ShipperRequestItem Request(string id, int amount)
        => new() { 의뢰Id = id, 결제예정금액 = amount, 배차상태 = "운송중", 결제상태 = "결제대기" };

    [Fact]
    public async Task 세션복원중다른의뢰로이동하면_최신선택만조회한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        var restored = new TaskCompletionSource<Ssalddel.Client.Infrastructure.Security.ClientAuthTokenSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.Load = () => restored.Task;
        var firstRun = fixture.Model.LoadAsync("A");
        var latestRun = fixture.Model.LoadAsync("B");
        restored.SetResult(ShipperRecoveryFixture.Snapshot());
        var pending = await fixture.Operations.NextAsync();
        Assert.Equal("B", pending.Id);
        pending.Completion.SetResult(Request("B", 2200));
        await Task.WhenAll(firstRun, latestRun);
        Assert.Equal(1, fixture.Operations.Calls);
        Assert.Equal("B", fixture.Model.State.Request?.RequestId);
    }

    [Fact]
    public async Task 세션복원실패는_조회오류로표시하고_다시조회할수있다()
    {
        using var fixture = new ShipperRecoveryFixture();
        fixture.Store.Load = () => Task.FromException<Ssalddel.Client.Infrastructure.Security.ClientAuthTokenSnapshot?>(new IOException("일시 저장소 실패"));
        await fixture.Model.LoadAsync("A");
        Assert.Contains("로그인 세션 조회 실패", fixture.Model.State.StatusMessage);
        Assert.Equal(0, fixture.Operations.Calls);
        fixture.Store.Load = () => Task.FromResult<Ssalddel.Client.Infrastructure.Security.ClientAuthTokenSnapshot?>(ShipperRecoveryFixture.Snapshot());
        var run = fixture.Model.LoadAsync("A");
        (await fixture.Operations.NextAsync()).Completion.SetResult(Request("A", 1100));
        await run;
        Assert.Equal("A", fixture.Model.State.Request?.RequestId);
        Assert.False(fixture.Model.State.RequiresLogin);
    }
}
