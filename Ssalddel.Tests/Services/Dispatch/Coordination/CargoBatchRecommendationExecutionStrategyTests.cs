using System.Data.Common;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Services.Dispatch.Compliance;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch.Coordination;

public sealed class CargoBatchRecommendationExecutionStrategyTests
{
    // 다른 fixture의 암호화 converter/model cache와 공유하지 않는 실제 SQLite transaction 시험입니다.
    private static readonly ServiceProvider OrdinaryProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().BuildServiceProvider(validateScopes: true);
    private static readonly ServiceProvider RetryingProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().AddScoped<IExecutionStrategyFactory, StrategyFactory>()
        .BuildServiceProvider(validateScopes: true);
    private const string DriverId = "batch-retry-driver";
    private const string RequestId = "batch-retry-request";
    private const int OriginalRound = 3;

    [Fact]
    public void 기본조율요청은_기사별한건제안이고_명시한다건설정은유지한다()
    {
        Assert.Equal(1, new 국내화물배차조율입력요청().기사당최대추천건수);
        Assert.Equal(1, new 배달권기반배차조율요청().기사당최대추천건수);
        Assert.Equal(2, new 국내화물배차조율입력요청 { 기사당최대추천건수 = 2 }.기사당최대추천건수);
        Assert.Equal(2, new 배달권기반배차조율요청 { 기사당최대추천건수 = 2 }.기사당최대추천건수);
    }

    [Fact]
    public async Task 기본추천은_현재한건의응답을기다리고_잠금해제후다음추천과Outbox를한번만만든다()
    {
        await using var f = await Fixture.Create();
        var nextId = RequestId + "-next";
        await f.AddRequest(nextId);
        Assert.Equal(1, (await f.Apply()).잠금건수);

        var waiting = await f.ApplyFor(nextId);
        Assert.Equal(0, waiting.잠금건수);
        Assert.Equal(1, waiting.실패건수);
        Assert.Single(await f.Db.배차추천알림Outbox.AsNoTracking().ToArrayAsync());
        var untouched = await f.Db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == nextId);
        Assert.Null(untouched.현재추천대상기사Id);
        Assert.Equal(OriginalRound, untouched.추천라운드);

        // 응답 명령 자체의 시험이 아니라 정본에서 이전 추천 잠금이 해제된 뒤의 적용 경계다.
        await using (var response = Db(f.Connection))
        {
            var answered = await response.운송원장.SingleAsync(x => x.의뢰Id == RequestId);
            answered.현재추천대상기사Id = null;
            answered.배차큐단계 = 상태값.배차큐단계.계획배차;
            answered.배차노출상태 = 상태값.배차노출상태.계획대기;
            await response.SaveChangesAsync();
        }

        Assert.Equal(1, (await f.ApplyFor(nextId)).잠금건수);
        Assert.Equal(0, (await f.ApplyFor(nextId)).잠금건수);
        await using var canonical = Db(f.Connection);
        var current = await canonical.운송원장.SingleAsync(x => x.의뢰Id == nextId);
        Assert.Equal(DriverId, current.현재추천대상기사Id);
        Assert.Equal(OriginalRound + 1, current.추천라운드);
        var notifications = await canonical.배차추천알림Outbox.ToArrayAsync();
        Assert.Equal(2, notifications.Length);
        Assert.Single(notifications.Where(x => x.의뢰Id == nextId));
        Assert.Equal(2, f.States.Calls);
        Assert.Equal(2, f.Space.Calls);
        Assert.Equal(f.Admission.EnterCalls, f.Admission.LockCalls);
        Assert.Equal(f.Admission.EnterCalls, f.Admission.DisposeCalls);
    }

    [Fact]
    public async Task 명시적두건추천은호환하고_기본단건적용은기존두추천을취소하거나추가하지않는다()
    {
        await using var f = await Fixture.Create();
        var secondId = RequestId + "-second";
        var thirdId = RequestId + "-third";
        await f.AddRequest(secondId);
        await f.AddRequest(thirdId);
        Assert.Equal(1, (await f.ApplyFor(RequestId, maximum: 2)).잠금건수);
        Assert.Equal(1, (await f.ApplyFor(secondId, maximum: 2)).잠금건수);
        var before = await f.Db.운송원장.AsNoTracking()
            .Where(x => x.의뢰Id != thirdId).OrderBy(x => x.의뢰Id)
            .Select(x => new { x.의뢰Id, x.배차큐단계, x.배차노출상태, x.현재추천대상기사Id,
                x.추천라운드, x.추천시작시각, x.추천만료시각, x.추천판정근거Json }).ToArrayAsync();

        Assert.Equal(0, (await f.ApplyFor(thirdId)).잠금건수);

        await using var canonical = Db(f.Connection);
        var after = await canonical.운송원장.AsNoTracking()
            .Where(x => x.의뢰Id != thirdId).OrderBy(x => x.의뢰Id)
            .Select(x => new { x.의뢰Id, x.배차큐단계, x.배차노출상태, x.현재추천대상기사Id,
                x.추천라운드, x.추천시작시각, x.추천만료시각, x.추천판정근거Json }).ToArrayAsync();
        Assert.Equal(before, after);
        Assert.Equal(2, await canonical.배차추천알림Outbox.CountAsync());
        Assert.Null((await canonical.운송원장.SingleAsync(x => x.의뢰Id == thirdId)).현재추천대상기사Id);
    }

    [Fact]
    public async Task 재시도전략이켜져도추천과Outbox를같이커밋한뒤별도저장소를한번갱신한다()
    {
        await using var f = await Fixture.Create();
        var result = await f.Apply();
        await f.AssertCommitted(result);
        Assert.Equal(["lease", "sql", "state", "space", "release"], f.Order);
        Assert.Equal(1, f.Admission.EnterCalls);
        Assert.Equal(1, f.Admission.LockCalls);
        Assert.Equal(1, f.Admission.DisposeCalls);
        Assert.Equal(2, f.Snapshot.CheckCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장전이나Outbox저장직후실패는정본을다시읽고라운드와알림을중복하지않는다(bool afterOutbox)
    {
        var failure = new SaveFailure(afterOutbox);
        await using var f = await Fixture.Create(failure);
        var result = await f.Apply();
        await f.AssertCommitted(result);
        Assert.Equal(1, failure.Failures);
        Assert.Equal(2, f.Admission.LockCalls);
        Assert.Equal(1, f.Admission.EnterCalls);
        Assert.Equal(4, f.Snapshot.CheckCalls);
        Assert.Equal(1, f.Db.ChangeTracker.Entries<살뜰.도메인.설정.배차추천알림Outbox>().Count());
    }

    [Fact]
    public async Task 커밋응답만유실되면저장된동일판본을확인하고재추천이나Outbox중복을만들지않는다()
    {
        var failure = new CommitReplyFailure();
        await using var f = await Fixture.Create(failure);
        var result = await f.Apply();
        await f.AssertCommitted(result);
        Assert.Equal(1, failure.Failures);
        Assert.Equal(2, f.Admission.LockCalls);
        Assert.Equal(2, f.Snapshot.CheckCalls); // 재시도는 이미 커밋된 같은 판본만 확인합니다.
    }

    [Fact]
    public async Task 커밋응답유실후성공확인시호출자의추가행과변경도커밋된추적상태에수렴한다()
    {
        await using var f = await Fixture.Create(new CommitReplyFailure());
        var added = new 운임구성 { 의뢰Id = "caller-added-before-lost-reply", 최종운임 = 7000m };
        f.Db.운임구성.Add(added);
        var changed = await f.Db.화주운송의뢰.SingleAsync();
        changed.화물설명 = "호출자가 먼저 수정한 메모";
        f.Db.ChangeTracker.DetectChanges();

        var result = await f.Apply();

        await f.AssertCommitted(result);
        Assert.Equal(EntityState.Unchanged, f.Db.Entry(added).State);
        Assert.True(added.Id > 0);
        Assert.False(f.Db.Entry(added).Property(x => x.Id).IsTemporary);
        Assert.Equal(EntityState.Unchanged, f.Db.Entry(changed).State);
        Assert.Equal(changed.화물설명, f.Db.Entry(changed).OriginalValues[nameof(화주운송의뢰.화물설명)]);
        // commit 확인이 끝난 같은 context를 다시 저장해도 이미 저장한 별도 행을 INSERT하지 않습니다.
        await f.Db.SaveChangesAsync();
        await using var canonical = Db(f.Connection);
        var saved = Assert.Single(await canonical.운임구성.Where(x => x.의뢰Id == added.의뢰Id).ToArrayAsync());
        Assert.Equal(added.Id, saved.Id);
        Assert.Equal(changed.화물설명, (await canonical.화주운송의뢰.SingleAsync()).화물설명);
        Assert.Equal(OriginalRound + 1, (await canonical.운송원장.SingleAsync()).추천라운드);
        Assert.Single(await canonical.배차추천알림Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 커밋확인에서도마지막저장이후의호출자미저장변경을임의로승인하지않는다()
    {
        await using var f = await Fixture.Create(new CommitReplyFailure());
        var added = new 운임구성 { 의뢰Id = "caller-pending-after-save", 최종운임 = 7000m };
        f.Db.운임구성.Add(added);
        f.AfterOutboxSave = () =>
        {
            added.최종운임 = 7100m;
            f.Db.ChangeTracker.DetectChanges();
        };

        await f.AssertCommitted(await f.Apply());

        Assert.Equal(EntityState.Modified, f.Db.Entry(added).State);
        Assert.Equal(7100m, added.최종운임);
        Assert.Equal(7000m, f.Db.Entry(added).OriginalValues[nameof(운임구성.최종운임)]);
        await using (var beforeSave = Db(f.Connection))
        {
            var committed = Assert.Single(await beforeSave.운임구성.Where(x => x.의뢰Id == added.의뢰Id).ToArrayAsync());
            Assert.Equal(added.Id, committed.Id);
            Assert.Equal(7000m, committed.최종운임);
        }
        await f.Db.SaveChangesAsync();
        await using var afterSave = Db(f.Connection);
        Assert.Equal(7100m, Assert.Single(await afterSave.운임구성.Where(x => x.의뢰Id == added.의뢰Id).ToArrayAsync()).최종운임);
    }

    [Fact]
    public async Task 재시도대기취소는롤백하고잠금을해제하며호출자가추적하던변경도보존한다()
    {
        var failure = new SaveFailure(false, delay: true);
        await using var f = await Fixture.Create(failure);
        var unrelated = await f.Db.화주운송의뢰.SingleAsync();
        unrelated.화물설명 = "호출자가 먼저 작성한 별도 메모";
        f.Db.ChangeTracker.DetectChanges();
        using var cancellation = new CancellationTokenSource();
        var operation = f.Apply(cancellation.Token);
        await failure.BackoffStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("호출자가 먼저 작성한 별도 메모", unrelated.화물설명);
        Assert.Equal(EntityState.Modified, f.Db.Entry(unrelated).State);
        Assert.True(f.Db.Entry(unrelated).Property(x => x.화물설명).IsModified);
        Assert.Equal(1, f.Admission.EnterCalls);
        Assert.Equal(1, f.Admission.DisposeCalls);
        Assert.Empty(f.Order.Where(x => x is "state" or "space"));
        await f.AssertUnchanged();
    }

    [Fact]
    public async Task 롤백후재시도도호출자가먼저추적하던추가행과변경을버리지않는다()
    {
        await using var f = await Fixture.Create(new SaveFailure(true));
        var unrelated = new 운임구성 { 의뢰Id = "separate-caller-row", 최종운임 = 7000m };
        f.Db.운임구성.Add(unrelated);
        var result = await f.Apply();
        await f.AssertCommitted(result);
        await using var canonical = Db(f.Connection);
        var saved = await canonical.운임구성.SingleAsync(x => x.의뢰Id == unrelated.의뢰Id);
        Assert.Equal(7000m, saved.최종운임);
        Assert.Equal(saved.Id, unrelated.Id);
        Assert.Equal(EntityState.Unchanged, f.Db.Entry(unrelated).State);
    }

    [Fact]
    public async Task 외부트랜잭션에는중첩없이SQL과Outbox만참여하고롤백전별도저장소를변경하지않는다()
    {
        await using var f = await Fixture.Create();
        await f.Db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var caller = await f.Db.Database.BeginTransactionAsync();
            var result = await f.Apply();
            Assert.Equal(1, result.잠금건수);
            Assert.Same(caller, f.Db.Database.CurrentTransaction);
            Assert.Single(await f.Db.배차추천알림Outbox.AsNoTracking().ToArrayAsync());
            Assert.Equal(OriginalRound + 1, (await f.Db.운송원장.AsNoTracking().SingleAsync()).추천라운드);
            Assert.Empty(f.Order.Where(x => x is "state" or "space"));
            await caller.RollbackAsync();
        });
        Assert.Equal(1, f.Admission.LockCalls);
        await f.AssertUnchanged();
    }

    [Fact]
    public async Task 커밋후별도저장소실패는SQL재시도를일으키지않고커밋한추천과Outbox를보존한다()
    {
        await using var f = await Fixture.Create();
        f.States.Throw = true;
        await Assert.ThrowsAsync<TransientFailure>(() => f.Apply());
        Assert.Equal(1, f.Admission.LockCalls);
        Assert.Equal(1, f.States.Calls);
        Assert.Equal(0, f.Space.Calls);
        Assert.Equal(1, f.Admission.DisposeCalls);
        await using var canonical = Db(f.Connection);
        Assert.Equal(OriginalRound + 1, (await canonical.운송원장.SingleAsync()).추천라운드);
        Assert.Single(await canonical.배차추천알림Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 재시도사이에현재근거가바뀌면기존제안을재적용하거나알리지않는다()
    {
        await using var f = await Fixture.Create(new SaveFailure(false));
        f.Snapshot.ValidChecks = 2;
        var result = await f.Apply();
        Assert.Equal(0, result.잠금건수);
        Assert.Equal(1, result.실패건수);
        Assert.Equal(3, f.Snapshot.CheckCalls);
        Assert.Equal(2, f.Admission.LockCalls);
        Assert.Empty(f.Order.Where(x => x is "state" or "space"));
        await f.AssertUnchanged();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public SsalddelContext Db { get; }
        public List<string> Order { get; } = [];
        public Admission Admission { get; }
        public Snapshot Snapshot { get; } = new();
        public DriverState States { get; }
        public Space Space { get; }
        public Action? AfterOutboxSave { get; set; }
        private readonly 국내화물배차조율적용Service service;
        private readonly Clock clock = new();
        private Fixture(SqliteConnection connection, SsalddelContext db)
        {
            Connection = connection; Db = db;
            Admission = new(db, Order); States = new(db, Order); Space = new(db, Order);
            var guard = new Qualification();
            // 실제 Outbox 생성 코드를 사용하며 토큰/FCM 발송 기능은 호출하지 않습니다.
            var notifications = new 배차추천알림Service(db, null!, null!, NullLogger<배차추천알림Service>.Instance, clock, guard);
            service = new(db, Options.Create(new 배차큐정책Options()), new NotificationAfterSave(notifications, () => AfterOutboxSave?.Invoke()), States, Space,
                NullLogger<국내화물배차조율적용Service>.Instance, guard, Snapshot, new Availability(), Admission, new Eligibility(), clock);
        }
        public static async Task<Fixture> Create(IInterceptor? failure = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using (var seed = CargoBatchRecommendationExecutionStrategyTests.Db(connection))
            {
                await seed.Database.EnsureCreatedAsync();
                seed.운송원장.Add(new 운송원장 { 의뢰Id = RequestId, 배차업무유형 = 상태값.배차업무유형.용달운송,
                    상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.계획배차,
                    배차노출상태 = 상태값.배차노출상태.계획대기, 추천라운드 = OriginalRound });
                var fare = new 운임구성 { 의뢰Id = RequestId, 기사지급예정운임 = 5000m, 최종운임 = 6000m };
                seed.운임구성.Add(fare);
                await seed.SaveChangesAsync();
                seed.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = RequestId, 운임구성Id = fare.Id,
                    화물종류 = "격리 시험 상자", 화물중량Kg = 1m, 화물길이Mm = 200, 화물폭Mm = 150, 화물높이Mm = 100,
                    픽업_도로명주소 = "가상 상차지", 하차_도로명주소 = "가상 하차지" });
                await seed.SaveChangesAsync();
            }
            return new(connection, CargoBatchRecommendationExecutionStrategyTests.Db(connection, true, failure));
        }
        public Task<국내화물배차조율적용결과> Apply(CancellationToken ct = default)
            => ApplyFor(RequestId, ct: ct);

        public async Task AddRequest(string requestId)
        {
            await using var seed = CargoBatchRecommendationExecutionStrategyTests.Db(Connection);
            seed.운송원장.Add(new 운송원장 { 의뢰Id = requestId, 배차업무유형 = 상태값.배차업무유형.용달운송,
                상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.계획배차,
                배차노출상태 = 상태값.배차노출상태.계획대기, 추천라운드 = OriginalRound });
            var fare = new 운임구성 { 의뢰Id = requestId, 기사지급예정운임 = 5000m, 최종운임 = 6000m };
            seed.운임구성.Add(fare);
            await seed.SaveChangesAsync();
            seed.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = requestId, 운임구성Id = fare.Id,
                화물종류 = "격리 시험 상자", 화물중량Kg = 1m, 화물길이Mm = 200, 화물폭Mm = 150, 화물높이Mm = 100,
                픽업_도로명주소 = "가상 상차지", 하차_도로명주소 = "가상 하차지" });
            await seed.SaveChangesAsync();
        }

        public Task<국내화물배차조율적용결과> ApplyFor(string requestId, int? maximum = null, CancellationToken ct = default)
        {
            var evidence = new 국내화물배차판정근거 { 입력지문 = "fixture-current-hash" };
            var plan = new 국내화물검증수행계획 { 계획Id = "fixture-plan-" + requestId, 기사Id = DriverId, 의뢰Ids = [requestId],
                확인됨 = true, 가능여부 = true, 입력지문 = evidence.입력지문,
                상하차작업시간 = new Dictionary<string, 화물상하차작업시간> { [requestId] = new(1m, 1m) } };
            var result = new 국내화물배차조율결과(clock.GetUtcNow().UtcDateTime,
                [new 국내화물배차제안(1, requestId, DriverId, 1, 100m, 1000m, 6000m, 5000m, "격리 시험", [])
                    { 판정근거 = evidence, 수행계획Id = plan.계획Id }], [], [], 1000m, 6000m, 5000m)
                { 판정근거 = evidence, 검증계획목록 = [plan] };
            return maximum.HasValue
                ? service.추천잠금적용Async(result, 기사동시추천잠금건수: maximum.Value, cancellationToken: ct)
                : service.추천잠금적용Async(result, cancellationToken: ct);
        }
        public async Task AssertCommitted(국내화물배차조율적용결과 result)
        {
            Assert.Equal(1, result.잠금건수); Assert.Equal(0, result.실패건수);
            Assert.Equal(1, States.Calls); Assert.Equal(1, Space.Calls);
            Assert.Null(Db.Database.CurrentTransaction);
            await using var canonical = CargoBatchRecommendationExecutionStrategyTests.Db(Connection);
            var queue = await canonical.운송원장.SingleAsync();
            Assert.Equal(DriverId, queue.현재추천대상기사Id);
            Assert.Equal(OriginalRound + 1, queue.추천라운드);
            Assert.Equal(5000m, queue.기사지급예정액);
            var outbox = Assert.Single(await canonical.배차추천알림Outbox.ToArrayAsync());
            Assert.Equal(queue.추천라운드, outbox.추천라운드);
            Assert.Equal(DriverId, outbox.기사Id);
            Assert.Equal("Pending", outbox.발송상태); Assert.Equal(0, outbox.시도횟수);
        }
        public async Task AssertUnchanged()
        {
            Assert.Null(Db.Database.CurrentTransaction);
            await using var canonical = CargoBatchRecommendationExecutionStrategyTests.Db(Connection);
            var queue = await canonical.운송원장.SingleAsync();
            Assert.Equal(OriginalRound, queue.추천라운드); Assert.Null(queue.현재추천대상기사Id);
            Assert.Null(queue.추천판정근거Json);
            Assert.Empty(await canonical.배차추천알림Outbox.ToArrayAsync());
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private static SsalddelContext Db(SqliteConnection connection, bool retry = false, IInterceptor? failure = null)
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection)
            .UseInternalServiceProvider(retry ? RetryingProvider : OrdinaryProvider);
        if (failure is not null) options.AddInterceptors(failure);
        return new(options.Options, new Encryption());
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class NotificationAfterSave(I배차추천알림Service actual, Action afterSave) : I배차추천알림Service
    {
        public async Task 추천알림요청생성Async(long id, string request, string driver, int round, CancellationToken ct = default)
        { await actual.추천알림요청생성Async(id, request, driver, round, ct); afterSave(); }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Clock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero); }
    private sealed class Qualification : I신규화물업무자격Guard
    { public Task<운송자격평가결과> 평가Async(string id, CancellationToken ct = default)
        => Task.FromResult(new 운송자격평가결과(true, "Qualification.Valid", "격리 시험", 운송자격Codes.Valid)); }
    private sealed class Snapshot : I화물배차상태SnapshotService
    {
        public int CheckCalls { get; private set; }
        public int ValidChecks { get; set; } = int.MaxValue;
        public Task<bool> 현재근거확인Async(국내화물배차판정근거 expected, CancellationToken ct = default)
            => Task.FromResult(++CheckCalls <= ValidChecks);
        public Task<국내화물배차판정근거> 조회Async(string request, string driver, DateTime now, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
    private sealed class Admission(SsalddelContext db, List<string> order) : ISharedDriverWorkAdmissionService
    {
        public bool Enabled => true;
        public int EnterCalls { get; private set; }
        public int LockCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public Task<IDisposable> EnterAsync(string id, CancellationToken ct = default)
        { EnterCalls++; order.Add("lease"); return Task.FromResult<IDisposable>(new Lease(this)); }
        public Task LockAsync(string id, CancellationToken ct = default)
        { Assert.NotNull(db.Database.CurrentTransaction); LockCalls++; order.Add("sql"); return Task.CompletedTask; }
        public Task<Result> CheckRecommendationAsync(string id, 운송원장 row, CancellationToken ct = default) => Task.FromResult(Result.Ok());
        public Task<Result> CheckAsync(string id, IReadOnlyList<string> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result> CheckParticipationAsync(string id, string kind, bool picking, CancellationToken ct = default) => throw new NotSupportedException();
        private void Release() { DisposeCalls++; order.Add("release"); }
        private sealed class Lease(Admission owner) : IDisposable
        { public void Dispose() => owner.Release(); }
    }
    private sealed class Eligibility : I화물배차수락적격성Service
    {
        public Task<화물배차수락적격성결과> 평가Async(string id, 화주운송의뢰 request, IReadOnlyCollection<string> warnings, CancellationToken ct = default)
            => throw new NotSupportedException("명시 작업시간을 사용해야 합니다.");
        public Task<화물배차수락적격성결과> 평가Async(string id, 화주운송의뢰 request, IReadOnlyCollection<string> warnings,
            IReadOnlyDictionary<string, 화물상하차작업시간> times, CancellationToken ct)
        { Assert.Empty(warnings); Assert.Equal(1m, times[request.의뢰Id].상차분); return Task.FromResult(new 화물배차수락적격성결과(true, null, null, [], [], [], 5m, 0m)); }
    }
    private sealed class Availability : I운영배차공통UseCase
    {
        public Task<운영배차수신상태Dto> 수신상태조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 운영배차수신상태Dto
        { 주체Id = id, 수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능,
            수신의사변경시각Utc = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero) });
        public Task<운영배차수신상태Dto> 기사의사변경Async(string id, 운영배차수신의사변경요청 request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운영배차수신상태Dto> 서버실효상태변경Async(string id, Guid request, string state, string reason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운영배차단기지표Dto> 단기지표조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 활동사건기록Async(운영배차활동사건Dto item, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class DriverState(SsalddelContext db, List<string> order) : I국내화물운송기사상태Service
    {
        public int Calls { get; private set; }
        public bool Throw { get; set; }
        public Task<국내화물운송기사상태Snapshot?> 추천기록Async(string id, DateTime now, CancellationToken ct = default)
        { Assert.Null(db.Database.CurrentTransaction); Calls++; order.Add("state"); if (Throw) throw new TransientFailure(); return Task.FromResult<국내화물운송기사상태Snapshot?>(null); }
        public Task<국내화물운송기사상태Snapshot> 운행시작Async(string id, long shift, DateTime now, string mode, string location,
            string? destination, string? preference = null, string? appKey = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<국내화물운송기사상태Snapshot> 위치갱신Async(DriverLocationSnapshot location, long? shift = null,
            decimal? radius = null, string? appKey = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<국내화물운송기사상태Snapshot?> 후보없음기록Async(string id, DateTime now, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 운행종료Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Space(SsalddelContext db, List<string> order) : I국내화물배달권실행공간Store
    {
        public int Calls { get; private set; }
        public Task Remove운송의뢰Async(string id, CancellationToken ct = default)
        { Assert.Null(db.Database.CurrentTransaction); Calls++; order.Add("space"); return Task.CompletedTask; }
        public Task Upsert기사Async(string key, string id, IReadOnlyList<string> adjacent, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Remove기사Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Upsert운송의뢰Async(string key, string id, IReadOnlyList<string> adjacent, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<국내화물배달권실행공간Snapshot?> GetAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<국내화물배달권실행공간Snapshot>> SnapshotAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
    private class TransientFailure : Exception { }
    private sealed class DelayedFailure(TaskCompletionSource started) : TransientFailure
    { public TaskCompletionSource Started { get; } = started; }
    private sealed class StrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    { public IExecutionStrategy Create() => new Strategy(dependencies); }
    private sealed class Strategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception ex) => ex is TransientFailure;
        protected override TimeSpan? GetNextDelay(Exception last)
        {
            var delay = base.GetNextDelay(last);
            if (delay.HasValue && last is DelayedFailure delayed)
            { delayed.Started.TrySetResult(); return TimeSpan.FromSeconds(30); }
            return delay;
        }
    }
    private sealed class SaveFailure(bool afterOutbox, bool delay = false) : SaveChangesInterceptor
    {
        public int Failures { get; private set; }
        public TaskCompletionSource BackoffStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!afterOutbox && Failures == 0) Fail();
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            if (afterOutbox && Failures == 0 && data.Context!.ChangeTracker.Entries<살뜰.도메인.설정.배차추천알림Outbox>().Any()) Fail();
            return ValueTask.FromResult(result);
        }
        private void Fail() { Failures++; if (delay) throw new DelayedFailure(BackoffStarted); throw new TransientFailure(); }
    }
    private sealed class CommitReplyFailure : DbTransactionInterceptor
    {
        public int Failures { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken ct = default)
        { if (Failures++ == 0) throw new TransientFailure(); return Task.CompletedTask; }
    }
}
