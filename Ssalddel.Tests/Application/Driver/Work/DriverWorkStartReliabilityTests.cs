using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Work;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using Ssalddel.Services.Outbox;
using Ssalddel.Migrations;
using Ssalddel.Hubs;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Application.Driver.Work;

public sealed class DriverWorkStartReliabilityTests
{
    [Fact]
    public void 기본_운행시작요청은_서버_시작모드_검증을_통과한다()
    {
        var input = new Ssalddel.Contracts.Driver.Work.기사운행시작요청 { 시작위치 = "현재 위치" };
        var command = new 운행시작Command("driver", input.시작모드, null, input.시작위치,
            null, null, null, null, false, null, null, false, false, ClientRequestId: Guid.NewGuid());
        Assert.True(new 운행시작CommandValidator().Validate(command).IsValid);
    }

    [Fact]
    public async Task 같은요청은_다른Context에서도_한근무를반환한다()
    {
        await using var f = await Fixture.CreateAsync();
        var id = Guid.NewGuid();
        await using var firstDb = f.Db();
        var first = await f.Start(firstDb).Handle(Request(id), default);
        await using var retryDb = f.Db();
        var retry = await f.Start(retryDb).Handle(Request(id), default);
        Assert.True(first.IsSuccess); Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value.ShiftId, retry.Value.ShiftId);
        await using var read = f.Db();
        Assert.Single(await read.기사근무.ToArrayAsync());
        Assert.Single(await read.Set<기사운행시작후속>().ToArrayAsync());
        Assert.Equal(OutboxProcessingStatuses.Succeeded, (await read.Set<기사운행시작후속>().SingleAsync()).Status);
        Assert.Equal(0, f.Recommendations.Sends);
    }

    [Fact]
    public async Task 같은요청식별자의_다른입력은_충돌이다()
    {
        await using var f = await Fixture.CreateAsync();
        await using var db = f.Db();
        var id = Guid.NewGuid();
        Assert.True((await f.Start(db).Handle(Request(id), default)).IsSuccess);
        var changed = await f.Start(db).Handle(Request(id) with { 시작위치 = "다른 위치" }, default);
        Assert.True(changed.IsFailed);
        Assert.Equal("DriverWork.RequestConflict", changed.Errors[0].Metadata["ErrorCode"]);
        Assert.Equal(409, changed.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 빈식별자는_저장하지않는다()
    {
        await using var f = await Fixture.CreateAsync(); await using var db = f.Db();
        Assert.True((await f.Start(db).Handle(Request(Guid.Empty), default)).IsFailed);
        Assert.Empty(await db.기사근무.ToArrayAsync());
    }

    [Fact]
    public async Task 식별자없는_구형호출도_중복근무를만들지않는다()
    {
        await using var f = await Fixture.CreateAsync(); await using var db = f.Db();
        var first = await f.Start(db).Handle(Request(null), default);
        var again = await f.Start(db).Handle(Request(null), default);
        Assert.True(first.IsSuccess); Assert.True(again.IsSuccess);
        Assert.Equal(first.Value.ShiftId, again.Value.ShiftId);
        Assert.Single(await db.기사근무.ToArrayAsync());
    }

    [Fact]
    public async Task 운행중_새식별자는_두번째근무를만들지않는다()
    {
        await using var f = await Fixture.CreateAsync(); await using var db = f.Db();
        Assert.True((await f.Start(db).Handle(Request(Guid.NewGuid()), default)).IsSuccess);
        var next = await f.Start(db).Handle(Request(Guid.NewGuid()), default);
        Assert.True(next.IsFailed);
        Assert.Equal("DriverWork.AlreadyStarted", next.Errors[0].Metadata["ErrorCode"]);
        Assert.Single(await db.기사근무.ToArrayAsync());
    }

    [Fact]
    public async Task 동시에_같은요청을시작해도_한근무만생긴다()
    {
        await using var f = await Fixture.CreateAsync();
        await using var a = f.Db(); await using var b = f.Db();
        var id = Guid.NewGuid();
        var results = await Task.WhenAll(f.Start(a).Handle(Request(id), default), f.Start(b).Handle(Request(id), default));
        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal(results[0].Value.ShiftId, results[1].Value.ShiftId);
        await using var read = f.Db(); Assert.Single(await read.기사근무.ToArrayAsync());
    }

    [Fact]
    public async Task 동시에_서로다른요청은_한개만시작한다()
    {
        await using var f = await Fixture.CreateAsync();
        await using var a = f.Db(); await using var b = f.Db();
        var results = await Task.WhenAll(f.Start(a).Handle(Request(Guid.NewGuid()), default), f.Start(b).Handle(Request(Guid.NewGuid()), default));
        Assert.Single(results.Where(x => x.IsSuccess)); Assert.Single(results.Where(x => x.IsFailed));
        await using var read = f.Db(); Assert.Single(await read.기사근무.ToArrayAsync());
    }

    [Fact]
    public async Task 인덱스실패도_근무확정은성공이고_같은예약이복구된다()
    {
        await using var f = await Fixture.CreateAsync(); f.Queue.Fail = true;
        await using var db = f.Db(); var result = await f.Start(db).Handle(Request(Guid.NewGuid()), default);
        Assert.True(result.IsSuccess); Assert.True(result.Value.FollowupPending);
        await using var read = f.Db();
        Assert.Equal(상태값.기사운행상태.운행중, (await read.용달기사.SingleAsync()).운행상태);
        Assert.Equal(OutboxProcessingStatuses.Pending, (await read.Set<기사운행시작후속>().SingleAsync()).Status);
        f.Queue.Fail = false; f.Clock.Now += TimeSpan.FromSeconds(31);
        await using var retry = f.Db(); Assert.True(await f.Followup(retry).처리Async(result.Value.ShiftId));
        Assert.Single(await f.Queue.SnapshotAsync());
    }

    [Fact]
    public async Task 후속예약저장실패는_근무와운행상태도_함께롤백한다()
    {
        await using var f = await Fixture.CreateAsync();
        await using var db = f.Db(new RejectFollowupSave());
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Start(db).Handle(Request(Guid.NewGuid()), default));
        await using var read = f.Db();
        Assert.Empty(await read.기사근무.ToArrayAsync());
        Assert.Empty(await read.Set<기사운행시작후속>().ToArrayAsync());
        Assert.Equal(상태값.기사운행상태.대기, (await read.용달기사.SingleAsync()).운행상태);
    }

    [Fact]
    public async Task 종료뒤_늦은후속과_옛요청재전송은_운행을되살리지않는다()
    {
        await using var f = await Fixture.CreateAsync(); f.Queue.Fail = true;
        var id = Guid.NewGuid(); await using var startDb = f.Db();
        var start = await f.Start(startDb).Handle(Request(id), default);
        await using var stopDb = f.Db(); await f.Stop(stopDb).Handle(new 운행종료Command("driver"), default);
        f.Queue.Fail = false; f.Clock.Now += TimeSpan.FromSeconds(31);
        await using var retryDb = f.Db();
        Assert.True(await f.Followup(retryDb).처리Async(start.Value.ShiftId));
        var replay = await f.Start(retryDb).Handle(Request(id), default);
        Assert.True(replay.IsSuccess); Assert.Equal(상태값.기사운행상태.대기, replay.Value.Status);
        Assert.Empty(await f.Queue.SnapshotAsync()); Assert.Null(await f.StateStore.GetAsync("driver"));
        await using var read = f.Db(); Assert.Single(await read.기사근무.ToArrayAsync());
    }

    [Fact]
    public async Task 새근무뒤_옛후속은_새인덱스를지우거나덮어쓰지않는다()
    {
        await using var f = await Fixture.CreateAsync(); f.Queue.Fail = true;
        await using var a = f.Db(); var old = await f.Start(a).Handle(Request(Guid.NewGuid()), default);
        await using var stop = f.Db(); await f.Stop(stop).Handle(new 운행종료Command("driver"), default);
        f.Queue.Fail = false;
        await using var b = f.Db(); var current = await f.Start(b).Handle(Request(Guid.NewGuid()), default);
        f.Clock.Now += TimeSpan.FromSeconds(31);
        await using var retry = f.Db(); Assert.True(await f.Followup(retry).처리Async(old.Value.ShiftId));
        Assert.Equal(current.Value.ShiftId, Assert.Single(await f.Queue.SnapshotAsync()).ShiftId);
        Assert.Equal(current.Value.ShiftId, (await f.StateStore.GetAsync("driver"))!.ShiftId);
    }

    [Fact]
    public async Task 기존_화물시작은_화물추천과앱식별자를유지한다()
    {
        await using var f = await Fixture.CreateAsync(); await using var db = f.Db();
        var result = await f.Start(db).Handle(Request(Guid.NewGuid(), 운송실행유형코드.화물운송), default);
        Assert.True(result.IsSuccess); Assert.Equal(1, f.Recommendations.Sends);
        Assert.Equal(기사앱식별자.CargoYongdalDriverApp, (await f.StateStore.GetAsync("driver"))!.AppKey);
        Assert.Empty(await db.Set<기사운행시작후속>().ToArrayAsync());
    }

    [Fact]
    public async Task 기존운행이있으면_다른앱시작으로_근무유형을바꾸지않는다()
    {
        await using var f = await Fixture.CreateAsync(); await using var db = f.Db();
        Assert.True((await f.Start(db).Handle(Request(null), default)).IsSuccess);
        Assert.True((await f.Start(db).Handle(Request(null, 운송실행유형코드.화물운송), default)).IsFailed);
        Assert.Equal(운송실행유형코드.음식배달, (await db.기사근무.SingleAsync()).운송실행유형);
    }

    [Fact]
    public async Task SQL고유제약은_활성기사와요청키의중복을막는다()
    {
        await using var f = await Fixture.CreateAsync(); await using var db = f.Db();
        Assert.True((await f.Start(db).Handle(Request(Guid.NewGuid()), default)).IsSuccess);
        db.기사근무.Add(new 기사근무 { 기사Id = "driver", ActiveDriverId = "driver", 시작모드 = "바로시작", 시작위치 = "서울" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var existing = await db.기사근무.AsNoTracking().SingleAsync();
        db.기사근무.Add(new 기사근무 { 기사Id = "driver", WorkStartRequestKey = existing.WorkStartRequestKey,
            시작모드 = "바로시작", 시작위치 = "서울" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task 같은근무의_이미수신한GPS는_후속재시도로_초기화하지않는다()
    {
        await using var f = await Fixture.CreateAsync(); f.Queue.Fail = true;
        await using var db = f.Db(); var started = await f.Start(db).Handle(Request(Guid.NewGuid()), default);
        var states = new 국내화물운송기사상태Service(f.StateStore);
        await states.운행시작Async("driver", started.Value.ShiftId, DateTime.UtcNow, "바로시작", "서울", null);
        await states.위치갱신Async(new DriverLocationSnapshot("driver", 37.6m, 127.1m, 5m, "운행중", DateTime.UtcNow, DateTime.UtcNow), started.Value.ShiftId);
        f.Queue.Fail = false; f.Clock.Now += TimeSpan.FromSeconds(31);
        await using var retry = f.Db(); Assert.True(await f.Followup(retry).처리Async(started.Value.ShiftId));
        Assert.Equal(37.6m, (await f.StateStore.GetAsync("driver"))!.Latitude);
    }

    [Fact]
    public async Task 상태투영후_공간정리가실패하면_같은근무도_남은공간정리를재시도한다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.FoodScopes.Upsert기사Async("food-scope:old", "driver", []);
        await f.CargoScopes.Upsert기사Async("cargo-scope:old", "driver", []);
        f.FoodScopes.FailRemove = true;
        await using var db = f.Db();
        var result = await f.Start(db).Handle(Request(Guid.NewGuid()), default);
        Assert.True(result.IsSuccess); Assert.True(result.Value.FollowupPending);
        Assert.Equal(result.Value.ShiftId, (await f.StateStore.GetAsync("driver"))!.ShiftId);
        Assert.Contains("driver", Assert.Single(await f.FoodScopes.SnapshotAsync()).운행중기사Ids);
        var states = new 국내화물운송기사상태Service(f.StateStore);
        await states.위치갱신Async(new DriverLocationSnapshot("driver", 37.6m, 127.1m, 5m, "운행중", DateTime.UtcNow, DateTime.UtcNow), result.Value.ShiftId);
        f.FoodScopes.FailRemove = false; f.Clock.Now += TimeSpan.FromSeconds(31);
        await using var retry = f.Db();
        Assert.True(await f.Followup(retry).처리Async(result.Value.ShiftId));
        Assert.Equal(2, f.FoodScopes.RemoveAttempts);
        Assert.All(await f.FoodScopes.SnapshotAsync(), x => Assert.DoesNotContain("driver", x.운행중기사Ids));
        Assert.All(await f.CargoScopes.SnapshotAsync(), x => Assert.DoesNotContain("driver", x.운행중기사Ids));
        Assert.Equal(37.6m, (await f.StateStore.GetAsync("driver"))!.Latitude);
        await using var read = f.Db();
        Assert.Equal(OutboxProcessingStatuses.Succeeded, (await read.Set<기사운행시작후속>().SingleAsync()).Status);
    }

    [Fact]
    public async Task 다섯번실패한예약은_무한재시도하지않는다()
    {
        await using var f = await Fixture.CreateAsync(); f.Queue.Fail = true;
        await using var db = f.Db(); var started = await f.Start(db).Handle(Request(Guid.NewGuid()), default);
        for (var attempt = 1; attempt < 5; attempt++)
        {
            f.Clock.Now += TimeSpan.FromSeconds(31);
            await using var retry = f.Db(); Assert.False(await f.Followup(retry).처리Async(started.Value.ShiftId));
        }
        await using var read = f.Db();
        var item = await read.Set<기사운행시작후속>().SingleAsync();
        Assert.Equal(5, item.AttemptCount); Assert.Equal(OutboxProcessingStatuses.Failed, item.Status);
        f.Queue.Fail = false; f.Clock.Now += TimeSpan.FromHours(1);
        await using var last = f.Db(); Assert.Equal(0, await f.Followup(last).대기처리Async());
    }

    [Fact]
    public void 마이그레이션은_기존행을바꾸지않고_고유제약과후속FK를추가한다()
    {
        var operations = new AddDriverWorkStartReliabilityR50().UpOperations;
        var columns = operations.OfType<AddColumnOperation>().ToArray();
        Assert.Equal(6, columns.Length); Assert.All(columns, x => Assert.True(x.IsNullable));
        Assert.Equal(2, operations.OfType<CreateIndexOperation>().Count(x => x.Table == "driver_shifts" && x.IsUnique));
        var table = Assert.Single(operations.OfType<CreateTableOperation>());
        var fk = Assert.Single(table.ForeignKeys);
        Assert.Equal("driver_shifts", fk.PrincipalTable); Assert.Equal(["id"], fk.PrincipalColumns);
        Assert.DoesNotContain(operations, x => x is SqlOperation or UpdateDataOperation or DeleteDataOperation);
    }

    [Fact]
    public async Task 상태조회는_오래된인덱스대신_SQL최신근무와요청을반환한다()
    {
        await using var f = await Fixture.CreateAsync();
        await using var firstDb = f.Db();
        var first = await f.Start(firstDb).Handle(Request(Guid.NewGuid()), default);
        var oldState = (await f.StateStore.GetAsync("driver"))!;
        await using var stopDb = f.Db();
        await f.Stop(stopDb).Handle(new 운행종료Command("driver"), default);
        var requestId = Guid.NewGuid();
        await using var nextDb = f.Db();
        var latest = await f.Start(nextDb).Handle(Request(requestId), default);
        await f.StateStore.UpsertAsync(oldState);
        await using var read = f.Db();

        var status = await new 운행상태조회QueryHandler(read, f.StateStore)
            .Handle(new 운행상태조회Query("driver"), default);

        Assert.NotEqual(first.Value.ShiftId, latest.Value.ShiftId);
        Assert.Equal(latest.Value.ShiftId, status.ShiftId);
        Assert.Equal(requestId, status.WorkStartClientRequestId);
        Assert.Equal(운송실행유형코드.음식배달, status.운송실행유형);
        Assert.Equal(상태값.기사운행상태.운행중, status.Status);
    }

    [Fact]
    public async Task 상태조회는_근무없는기사와_구형요청없는근무를_구분한다()
    {
        await using var f = await Fixture.CreateAsync(); await using var db = f.Db();
        var query = new 운행상태조회QueryHandler(db, f.StateStore);
        var idle = await query.Handle(new 운행상태조회Query("driver"), default);
        Assert.Null(idle.ShiftId); Assert.Null(idle.WorkStartClientRequestId);
        Assert.Null(idle.운송실행유형);
        var started = await f.Start(db).Handle(Request(null), default);
        var legacy = await query.Handle(new 운행상태조회Query("driver"), default);
        Assert.Equal(started.Value.ShiftId, legacy.ShiftId);
        Assert.Null(legacy.WorkStartClientRequestId);
        Assert.Equal(운송실행유형코드.음식배달, legacy.운송실행유형);
    }

    private static 운행시작Command Request(Guid? id, string type = 운송실행유형코드.음식배달)
        => new("driver", "바로시작", null, "서울특별시 중랑구", null, null, null, null,
            false, null, null, false, false, type, id);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string connectionString = $"Data Source=driver-work-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        private SqliteConnection anchor = null!;
        public Queue Queue { get; } = new();
        public InMemory국내화물운송기사상태Store StateStore { get; } = new();
        public FoodScopes FoodScopes { get; } = new();
        public InMemory국내화물배달권실행공간Store CargoScopes { get; } = new();
        public CommunityDriverAvailabilityService Community { get; } = new(new SystemCommunityDriverAvailabilityClock());
        public Recommendations Recommendations { get; } = new();
        public Clock Clock { get; } = new();
        public SsalddelContext Db(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connectionString);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new(options.Options, new Encryption());
        }
        public 기사운행시작후속Service Followup(SsalddelContext db) => new(db, Queue, new 국내화물운송기사상태Service(StateStore),
            FoodScopes, CargoScopes, NullLogger<기사운행시작후속Service>.Instance, Clock, StateStore);
        public 운행시작CommandHandler Start(SsalddelContext db) => new(db, Recommendations, Queue,
            new 국내화물운송기사상태Service(StateStore), FoodScopes, CargoScopes, new User(), new 참여자실행권한검사(),
            Community, NullLogger<운행시작CommandHandler>.Instance, Followup(db),
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);
        public 운행종료CommandHandler Stop(SsalddelContext db) => new(db, Queue,
            new 국내화물운송기사상태Service(StateStore), FoodScopes, CargoScopes, new User(), new 참여자실행권한검사(),
            Community, NullLogger<운행종료CommandHandler>.Instance);
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture(); f.anchor = new(f.connectionString); await f.anchor.OpenAsync();
            await using var db = f.Db(); await db.Database.EnsureCreatedAsync();
            db.용달기사.Add(new 용달기사 { 기사Id = "driver", 기사명 = "기사", 운행상태 = 상태값.기사운행상태.대기 });
            await db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() => await anchor.DisposeAsync();
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class FoodScopes : I음식배달권실행공간Store
    {
        private readonly InMemory음식배달권실행공간Store inner = new();
        public bool FailRemove { get; set; }
        public int RemoveAttempts { get; private set; }
        public Task Remove기사Async(string id, CancellationToken ct = default)
        {
            RemoveAttempts++;
            if (FailRemove) throw new IOException("scope offline");
            return inner.Remove기사Async(id, ct);
        }
        public Task Upsert기사Async(string key, string id, IReadOnlyList<string> neighbors, CancellationToken ct = default)
            => inner.Upsert기사Async(key, id, neighbors, ct);
        public Task Upsert운송의뢰Async(string key, string id, IReadOnlyList<string> neighbors, CancellationToken ct = default)
            => inner.Upsert운송의뢰Async(key, id, neighbors, ct);
        public Task Remove운송의뢰Async(string id, CancellationToken ct = default) => inner.Remove운송의뢰Async(id, ct);
        public Task<음식배달권실행공간Snapshot?> GetAsync(string key, CancellationToken ct = default) => inner.GetAsync(key, ct);
        public Task<IReadOnlyList<음식배달권실행공간Snapshot>> SnapshotAsync(CancellationToken ct = default) => inner.SnapshotAsync(ct);
    }
    private sealed class Queue : IDriverWorkQueueStore
    {
        private readonly Dictionary<string, DriverWorkQueueEntry> entries = new();
        public bool Fail { get; set; }
        public Task UpsertAsync(DriverWorkQueueEntry entry, CancellationToken ct = default)
        { if (Fail) throw new IOException("offline"); entries[entry.DriverId] = entry; return Task.CompletedTask; }
        public Task RemoveAsync(string driverId, CancellationToken ct = default) { entries.Remove(driverId); return Task.CompletedTask; }
        public Task<IReadOnlyList<DriverWorkQueueEntry>> SnapshotAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DriverWorkQueueEntry>>(entries.Values.ToArray());
    }
    private sealed class Recommendations : I배차추천Service
    {
        public int Sends { get; private set; }
        public Task SendToDriverAsync(string id) { Sends++; return Task.CompletedTask; }
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string id) => Task.FromResult<IReadOnlyList<DispatchRecommendationDto>>([]);
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string id, 배차추천검색조건? criteria) => GetRecommendationsAsync(id);
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetDrivingRecommendationsAsync(string id, 배차추천검색조건? criteria = null) => GetRecommendationsAsync(id);
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetIdleRecommendationsAsync(string id, 배차추천검색조건? criteria = null) => GetRecommendationsAsync(id);
    }
    private sealed class User : ICurrentUserAccessor { public string UserId => "driver"; public string Role => 역할명.기사; }
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class RejectFollowupSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<기사운행시작후속>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("test followup write failure");
            return ValueTask.FromResult(result);
        }
    }
}
