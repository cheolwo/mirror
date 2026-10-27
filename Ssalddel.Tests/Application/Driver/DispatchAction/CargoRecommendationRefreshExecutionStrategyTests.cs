using System.Text.Json;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed class CargoRecommendationRefreshExecutionStrategyTests
{
    // 개인정보 converter의 protector/model cache를 다른 fixture와 공유하지 않습니다.
    private static readonly ServiceProvider OrdinaryProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().BuildServiceProvider(validateScopes: true);
    private static readonly ServiceProvider RetryingProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().AddScoped<IExecutionStrategyFactory, StrategyFactory>()
        .BuildServiceProvider(validateScopes: true);
    private const string DriverId = "refresh-driver";
    private const string RequestId = "refresh-request";
    private const int OriginalRound = 3;

    [Fact]
    public async Task 재시도전략이켜져도추천검사전체를소유트랜잭션안에서수행한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = new Clock();
        await Seed(connection, clock);
        await using var db = Db(connection, retry: true);
        var source = new Snapshot();
        var admission = new Admission();

        var result = await Service(db, source, admission, clock).갱신Async(DriverId);

        AssertHeldOnce(result);
        Assert.Equal(1, source.ReadCalls);
        Assert.Equal(1, admission.EnterCalls);
        Assert.Equal(1, admission.LockCalls);
        Assert.Equal(1, admission.DisposeCalls);
        Assert.Null(db.Database.CurrentTransaction);
        await using var canonical = Db(connection);
        var row = await canonical.운송원장.SingleAsync();
        AssertHeld(row);
    }

    [Fact]
    public async Task 저장직전일시실패는전체검사를재시도하고결과와라운드를중복누적하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = new Clock();
        await Seed(connection, clock);
        var failure = new FirstSaveFailure();
        await using var db = Db(connection, retry: true, failure);
        var source = new Snapshot();
        var admission = new Admission();

        var result = await Service(db, source, admission, clock).갱신Async(DriverId);

        AssertHeldOnce(result);
        Assert.Equal(2, failure.SaveCalls);
        Assert.Equal(2, source.ReadCalls);
        Assert.Equal(2, admission.LockCalls);
        // 같은 기사에 대한 프로세스 lease는 재시도 밖에서 한 번만 잡습니다.
        Assert.Equal(1, admission.EnterCalls);
        Assert.Equal(1, admission.DisposeCalls);
        await using var canonical = Db(connection);
        AssertHeld(await canonical.운송원장.SingleAsync());
    }

    [Fact]
    public async Task 재시도대기중취소하면대기를끝내고기사잠금을해제한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = new Clock();
        await Seed(connection, clock);
        var failure = new DelayedSaveFailure();
        await using var db = Db(connection, retry: true, failure);
        var admission = new Admission();
        using var cancellation = new CancellationTokenSource();

        var operation = Service(db, new Snapshot(), admission, clock).갱신Async(DriverId, cancellation.Token);
        await failure.BackoffStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => operation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, failure.SaveCalls);
        Assert.Equal(1, admission.EnterCalls);
        Assert.Equal(1, admission.DisposeCalls);
        Assert.Null(db.Database.CurrentTransaction);
        await using var canonical = Db(connection);
        var row = await canonical.운송원장.SingleAsync();
        Assert.Equal(OriginalRound, row.추천라운드);
        Assert.Equal(DriverId, row.현재추천대상기사Id);
    }

    [Fact]
    public async Task 호출자가시작한트랜잭션은소유권과롤백가능성을유지한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = new Clock();
        await Seed(connection, clock);
        await using var db = Db(connection, retry: true);
        var admission = new Admission();
        var source = new Snapshot();

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var callerTransaction = await db.Database.BeginTransactionAsync();
            var result = await Service(db, source, admission, clock).갱신Async(DriverId);
            AssertHeldOnce(result);
            Assert.Same(callerTransaction, db.Database.CurrentTransaction);
            AssertHeld(await db.운송원장.AsNoTracking().SingleAsync());
            await callerTransaction.RollbackAsync();
        });

        await using var canonical = Db(connection);
        var row = await canonical.운송원장.SingleAsync();
        Assert.Equal(OriginalRound, row.추천라운드);
        Assert.Equal(DriverId, row.현재추천대상기사Id);
        Assert.Equal(상태값.배차노출상태.추천중, row.배차노출상태);
        Assert.NotNull(row.추천만료시각);
        Assert.Equal(1, source.ReadCalls);
        Assert.Equal(1, admission.EnterCalls);
        Assert.Equal(1, admission.DisposeCalls);
    }

    private static 국내화물추천재검증Service Service(SsalddelContext db, Snapshot source, Admission admission, Clock clock)
        // 불충분한 기존 근거를 보류하는 경로입니다. 재계산용 store/plan은 호출되면 이 검사가 실패합니다.
        => new(db, source, null!, null!, admission, clock);

    private static async Task Seed(SqliteConnection connection, Clock clock)
    {
        await using var db = Db(connection);
        await db.Database.EnsureCreatedAsync();
        db.운송원장.Add(new 운송원장
        {
            의뢰Id = RequestId,
            배차업무유형 = 상태값.배차업무유형.용달운송,
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = DriverId,
            추천라운드 = OriginalRound,
            추천시작시각 = clock.GetUtcNow().UtcDateTime.AddSeconds(-1),
            추천만료시각 = clock.GetUtcNow().UtcDateTime.AddMinutes(3),
            기사지급예정액 = 5000m,
            추천판정근거Json = JsonSerializer.Serialize(new 국내화물추천저장근거
            {
                판정근거 = new 국내화물배차판정근거 { 지문판본 = "cargo-dispatch-state-r77-v1" }
            })
        });
        await db.SaveChangesAsync();
    }

    private static void AssertHeldOnce(국내화물추천재검증결과 result)
    {
        Assert.Equal(RequestId, Assert.Single(result.보류의뢰Ids));
        Assert.Empty(result.유효의뢰Ids);
        Assert.Empty(result.갱신의뢰Ids);
    }

    private static void AssertHeld(운송원장 row)
    {
        Assert.Equal(OriginalRound + 1, row.추천라운드);
        Assert.Null(row.현재추천대상기사Id);
        Assert.Null(row.추천만료시각);
        Assert.Equal(상태값.배차노출상태.추천후보없음, row.배차노출상태);
        Assert.Equal("CargoRefresh.ProtectedConditionsChanged",
            JsonSerializer.Deserialize<국내화물추천저장근거>(row.추천판정근거Json!)!.재검증상태Code);
    }

    private static SsalddelContext Db(SqliteConnection connection, bool retry = false, SaveChangesInterceptor? failure = null)
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection)
            .UseInternalServiceProvider(retry ? RetryingProvider : OrdinaryProvider);
        if (failure is not null) options.AddInterceptors(failure);
        return new(options.Options, new Encryption());
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Clock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero); }
    private sealed class Snapshot : I화물배차상태SnapshotService
    {
        public int ReadCalls { get; private set; }
        public Task<국내화물배차판정근거> 조회Async(string requestId, string driverId, DateTime 기준시각Utc, CancellationToken ct = default)
        { ReadCalls++; return Task.FromResult(new 국내화물배차판정근거()); }
        public Task<bool> 현재근거확인Async(국내화물배차판정근거 expected, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected stable-evidence path.");
    }
    private sealed class Admission : ISharedDriverWorkAdmissionService
    {
        public bool Enabled => true;
        public int EnterCalls { get; private set; }
        public int LockCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public Task<IDisposable> EnterAsync(string driverId, CancellationToken ct = default)
        { EnterCalls++; return Task.FromResult<IDisposable>(new Lease(this)); }
        public Task LockAsync(string driverId, CancellationToken ct = default)
        { LockCalls++; return Task.CompletedTask; }
        public Task<Result> CheckAsync(string driverId, IReadOnlyList<string> requestIds, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected recomputation path.");
        public Task<Result> CheckRecommendationAsync(string driverId, 운송원장 queue, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected recommendation admission path.");
        public Task<Result> CheckParticipationAsync(string driverId, string workKind, bool requiresDirectPicking, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected participation path.");
        private sealed class Lease(Admission owner) : IDisposable
        { public void Dispose() => owner.DisposeCalls++; }
    }
    private class TransientFailure : Exception { }
    private sealed class DelayedTransientFailure(TaskCompletionSource backoffStarted) : TransientFailure
    { public TaskCompletionSource BackoffStarted { get; } = backoffStarted; }
    private sealed class StrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    { public IExecutionStrategy Create() => new Strategy(dependencies); }
    private sealed class Strategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TransientFailure;
        protected override TimeSpan? GetNextDelay(Exception lastException)
        {
            var delay = base.GetNextDelay(lastException);
            if (delay.HasValue && lastException is DelayedTransientFailure delayed)
            {
                delayed.BackoffStarted.TrySetResult();
                return TimeSpan.FromSeconds(30);
            }
            return delay;
        }
    }
    private sealed class DelayedSaveFailure : SaveChangesInterceptor
    {
        public int SaveCalls { get; private set; }
        public TaskCompletionSource BackoffStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            SaveCalls++;
            throw new DelayedTransientFailure(BackoffStarted);
        }
    }
    private sealed class FirstSaveFailure : SaveChangesInterceptor
    {
        public int SaveCalls { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (++SaveCalls == 1) throw new TransientFailure();
            return ValueTask.FromResult(result);
        }
    }
}
