using Microsoft.Data.Sqlite;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Infrastructure.Storage.Memory;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Common;

public sealed class 운영배차미응답Tests
{
    [Fact]
    public async Task 표시확인한_서로다른_제안의_세번째_만료만_신규수신을중지한다()
    {
        await using var f = await Fixture.Create();
        for (var round = 1; round <= 3; round++)
        {
            await f.ExpireOffer("offer-" + round);
            var state = await f.UseCase.수신상태조회Async("driver");
            Assert.Equal(round, state.연속미응답횟수);
            Assert.Equal(round == 3, state.미응답자동중지여부);
            Assert.Equal(round < 3, 운영배차수신상태Policy.신규배차수신허용(state));
            Assert.Equal(운영배차수신의사Code.On, state.수신의사Code);
        }
        var events = await f.Db.운영배차활동사건.ToArrayAsync();
        Assert.Equal(3, events.Count(x => x.사건유형Code == 운영배차사건유형Code.무응답));
        Assert.Single(events.Where(x => x.사건유형Code == 운영배차사건유형Code.수신의사변경));
        var rebuilt = await f.NewUseCase(new InMemory운영배차판정ProjectionStore()).수신상태조회Async("driver");
        Assert.True(rebuilt.미응답자동중지여부);
        Assert.NotNull(rebuilt.미응답자동중지시각Utc);
    }

    [Fact]
    public async Task 표시확인없는_발송실패나_발송성공만으로는_미응답을세지않는다()
    {
        await using var f = await Fixture.Create();
        await f.ExpireOffer("not-displayed", receipt: false);
        var state = await f.UseCase.수신상태조회Async("driver");
        Assert.Equal(0, state.연속미응답횟수);
        Assert.Empty(await f.Db.운영배차활동사건.Where(x => x.사건유형Code == 운영배차사건유형Code.무응답).ToArrayAsync());
    }

    [Fact]
    public async Task 같은_표시확인과_만료를_다시처리해도_한번만센다()
    {
        await using var f = await Fixture.Create();
        await f.AddOffer("offer");
        Assert.True(await f.Receipt.표시확인Async("driver", "offer", 1));
        Assert.True(await f.Receipt.표시확인Async("driver", "offer", 1));
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await f.Expiry.추천만료처리Async("offer");
        await f.Expiry.추천만료처리Async("offer");
        Assert.Equal(1, (await f.UseCase.수신상태조회Async("driver")).연속미응답횟수);
        Assert.Single(await f.Db.운영배차활동사건.Where(x => x.사건유형Code == 운영배차사건유형Code.제안표시확인).ToArrayAsync());
        Assert.Single(await f.Db.운영배차활동사건.Where(x => x.사건유형Code == 운영배차사건유형Code.무응답).ToArrayAsync());
    }

    [Theory]
    [InlineData("other-driver")]
    [InlineData("old-round")]
    [InlineData("expired")]
    [InlineData("not-started")]
    [InlineData("accepted")]
    [InlineData("off")]
    [InlineData("server-paused")]
    public async Task 표시확인은_본인_현재라운드_활성시간_신규수신조건을검사한다(string change)
    {
        await using var f = await Fixture.Create();
        var queue = await f.AddOffer("offer");
        var driver = "driver";
        var round = 1;
        switch (change)
        {
            case "other-driver": driver = "other"; break;
            case "old-round": round = 2; break;
            case "expired": f.Clock.Now = f.Clock.Now.AddMinutes(1); break;
            case "not-started": queue.추천시작시각 = f.Clock.Now.AddMinutes(1); break;
            case "accepted": queue.상태 = 상태값.배차대기상태.확정; break;
            case "off": await f.UseCase.기사의사변경Async("driver", new() { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.Off }); break;
            case "server-paused": await f.UseCase.서버실효상태변경Async("driver", Guid.NewGuid(), 운영배차실효상태Code.서버일시정지, "OtherRestriction"); break;
        }
        await f.Db.SaveChangesAsync();
        Assert.False(await f.Receipt.표시확인Async(driver, "offer", round));
        Assert.Empty(await f.Db.운영배차활동사건.Where(x => x.사건유형Code == 운영배차사건유형Code.제안표시확인).ToArrayAsync());
    }

    [Theory]
    [InlineData(운영배차사건유형Code.수락)]
    [InlineData(운영배차사건유형Code.거절)]
    public async Task 명시수락이나거절은_연속횟수를초기화하지만_이미중지된상태는해제하지않는다(string eventType)
    {
        await using var f = await Fixture.Create();
        await f.ExpireOffer("one");
        await f.ExpireOffer("two");
        f.Clock.Now = f.Clock.Now.AddSeconds(1);
        f.Db.운영배차활동사건.Add(eventType == 운영배차사건유형Code.수락
            ? 운영배차활동사건Factory.수락("driver", "reply", "order", 1, f.Clock.Now)
            : 운영배차활동사건Factory.거절("driver", "reply", 1, f.Clock.Now, null));
        await f.Db.SaveChangesAsync();
        Assert.Equal(0, (await f.UseCase.수신상태조회Async("driver")).연속미응답횟수);
        await f.ExpireOffer("three"); await f.ExpireOffer("four"); await f.ExpireOffer("five");
        f.Clock.Now = f.Clock.Now.AddSeconds(1);
        f.Db.운영배차활동사건.Add(eventType == 운영배차사건유형Code.수락
            ? 운영배차활동사건Factory.수락("driver", "late-reply", "order", 1, f.Clock.Now)
            : 운영배차활동사건Factory.거절("driver", "late-reply", 1, f.Clock.Now, null));
        await f.Db.SaveChangesAsync();
        var paused = await f.UseCase.수신상태조회Async("driver");
        Assert.True(paused.미응답자동중지여부);
        Assert.Equal(0, paused.연속미응답횟수);
    }

    [Fact]
    public async Task 운행시작자동_ON은재개하지않고_명시_ON해제만_다른제한을보존하며재개한다()
    {
        await using var f = await Fixture.Create();
        await f.ExpireOffer("one"); await f.ExpireOffer("two"); await f.ExpireOffer("three");
        await f.UseCase.서버실효상태변경Async("driver", Guid.NewGuid(), 운영배차실효상태Code.연결확인불가, "OtherRestriction");
        f.Clock.Now = f.Clock.Now.AddSeconds(1);
        var automatic = await f.UseCase.기사의사변경Async("driver", new() { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.On });
        Assert.True(automatic.미응답자동중지여부);
        Assert.Equal(3, automatic.연속미응답횟수);
        f.Clock.Now = f.Clock.Now.AddSeconds(1);
        var request = new 운영배차수신의사변경요청 { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.On, 미응답자동중지해제 = true };
        var resumed = await f.UseCase.기사의사변경Async("driver", request);
        Assert.False(resumed.미응답자동중지여부);
        Assert.Equal(0, resumed.연속미응답횟수);
        Assert.Equal(운영배차실효상태Code.연결확인불가, resumed.실효상태Code);
        Assert.Equal("OtherRestriction", resumed.실효사유Code);
        Assert.False(운영배차수신상태Policy.신규배차수신허용(resumed));
        Assert.False((await f.UseCase.기사의사변경Async("driver", request)).미응답자동중지여부);
        request.미응답자동중지해제 = false;
        await Assert.ThrowsAsync<운영배차수신요청충돌Exception>(() => f.UseCase.기사의사변경Async("driver", request));
    }

    [Fact]
    public async Task 자동중지해제는_OFF로요청할수없다()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => f.UseCase.기사의사변경Async("driver", new()
        { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.Off, 미응답자동중지해제 = true }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 재개전_표시한_이전제안의_지연만료는_새연속횟수에섞이지않는다(bool sameTime)
    {
        await using var f = await Fixture.Create();
        await f.AddOffer("old");
        Assert.True(await f.Receipt.표시확인Async("driver", "old", 1));
        var expiry = f.Clock.Now.AddSeconds(20);
        f.Clock.Now = sameTime ? expiry : f.Clock.Now.AddSeconds(5);
        await f.UseCase.기사의사변경Async("driver", new()
        { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.On, 미응답자동중지해제 = true });
        f.Clock.Now = expiry.AddSeconds(1);
        await f.Expiry.추천만료처리Async("old");
        Assert.Equal(0, (await f.UseCase.수신상태조회Async("driver")).연속미응답횟수);
    }

    [Fact]
    public async Task 이전허용캐시가남아도_정본의세번째미응답을먼저반영한다()
    {
        await using var f = await Fixture.Create();
        await f.ExpireOffer("one"); await f.ExpireOffer("two"); await f.ExpireOffer("three");
        var stale = new InMemory운영배차판정ProjectionStore();
        await stale.수신상태저장Async(new() { 주체Id = "driver", 수신의사Code = 운영배차수신의사Code.On,
            수신의사변경시각Utc = DateTimeOffset.UnixEpoch });
        Assert.True((await f.NewUseCase(stale).수신상태조회Async("driver")).미응답자동중지여부);
    }

    [Fact]
    public async Task 자동중지와재개는_이미수락한_전달업무를변경하지않는다()
    {
        await using var f = await Fixture.Create();
        var active = await f.AddOffer("active");
        active.상태 = 상태값.배차대기상태.확정;
        active.배차큐단계 = 상태값.배차큐단계.확정;
        active.배차노출상태 = 상태값.배차노출상태.확정;
        active.확정기사Id = "driver";
        active.기사_운송자 = "driver";
        active.현재추천대상기사Id = null;
        active.추천시작시각 = active.추천만료시각 = null;
        await f.Db.SaveChangesAsync();
        await f.ExpireOffer("one"); await f.ExpireOffer("two"); await f.ExpireOffer("three");
        Assert.True((await f.UseCase.수신상태조회Async("driver")).미응답자동중지여부);
        await f.UseCase.기사의사변경Async("driver", new()
        { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.On, 미응답자동중지해제 = true });
        var unchanged = await f.Db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == "active");
        Assert.Equal(상태값.배차대기상태.확정, unchanged.상태);
        Assert.Equal(상태값.배차큐단계.확정, unchanged.배차큐단계);
        Assert.Equal("driver", unchanged.확정기사Id);
        Assert.Equal("driver", unchanged.기사_운송자);
    }

    [Fact]
    public async Task 이미수락된_정본을_이전추적사본의만료로_되돌리지않는다()
    {
        await using var f = await Fixture.Create(sqlite: true);
        await f.AddOffer("accepted");
        Assert.True(await f.Receipt.표시확인Async("driver", "accepted", 1));
        await using (var other = f.NewContext())
        {
            var accepted = await other.운송원장.SingleAsync();
            accepted.상태 = 상태값.배차대기상태.확정;
            accepted.배차큐단계 = 상태값.배차큐단계.확정;
            accepted.확정기사Id = "driver";
            await other.SaveChangesAsync();
        }
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var result = await f.Expiry.추천만료처리Async("accepted");
        Assert.False(result.전환여부);
        Assert.Equal(상태값.배차대기상태.확정, (await f.Db.운송원장.SingleAsync()).상태);
        Assert.Equal("driver", (await f.Db.운송원장.SingleAsync()).확정기사Id);
        Assert.Empty(await f.Db.운영배차활동사건.Where(x => x.사건유형Code == 운영배차사건유형Code.무응답).ToArrayAsync());
    }

    [Fact]
    public async Task 미응답저장실패는_같은트랜잭션의추천만료도되돌린다()
    {
        await using var f = await Fixture.Create(sqlite: true, failNoResponse: true);
        await f.AddOffer("atomic");
        Assert.True(await f.Receipt.표시확인Async("driver", "atomic", 1));
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Expiry.추천만료처리Async("atomic"));
        await using var read = f.NewContext();
        var unchanged = await read.운송원장.SingleAsync();
        Assert.Equal("driver", unchanged.현재추천대상기사Id);
        Assert.Equal(상태값.배차노출상태.추천중, unchanged.배차노출상태);
        Assert.Empty(await read.운영배차활동사건.Where(x => x.사건유형Code == 운영배차사건유형Code.무응답).ToArrayAsync());
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Added)]
    [InlineData(EntityState.Deleted)]
    public async Task 동일원장의_미저장변경은_만료재조회로버리거나저장하지않는다(EntityState pendingState)
    {
        await using var f = await Fixture.Create();
        var queue = await f.AddOffer("pending");
        queue.메모 = "아직 저장하지 않은 변경";
        f.Db.Entry(queue).State = pendingState;
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var result = await f.Expiry.추천만료처리Async("pending");
        Assert.False(result.전환여부);
        Assert.Equal(pendingState, f.Db.Entry(queue).State);
        Assert.Equal("아직 저장하지 않은 변경", queue.메모);
        Assert.Equal("driver", queue.현재추천대상기사Id);
        Assert.Empty(await f.Db.운영배차활동사건.Where(x => x.사건유형Code == 운영배차사건유형Code.무응답).ToArrayAsync());
    }

    [Theory]
    [InlineData(운영배차사건유형Code.제안표시확인, false)]
    [InlineData(운영배차사건유형Code.무응답, false)]
    [InlineData(운영배차사건유형Code.제안표시확인, true)]
    [InlineData(운영배차사건유형Code.무응답, true)]
    public async Task 첫_저장이나커밋실패뒤_같은Context재시도는_새사건추적을정리하고회복한다(string eventType, bool commitFailure)
    {
        await using var f = await Fixture.Create(sqlite: true, firstFailureType: eventType, commitFailure: commitFailure);
        await f.AddOffer("recover");
        if (eventType == 운영배차사건유형Code.제안표시확인)
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => f.Receipt.표시확인Async("driver", "recover", 1));
            Assert.DoesNotContain(f.Db.ChangeTracker.Entries<운영배차활동사건>(), x => x.Entity.사건유형Code == eventType);
            Assert.True(await f.Receipt.표시확인Async("driver", "recover", 1));
        }
        else
        {
            Assert.True(await f.Receipt.표시확인Async("driver", "recover", 1));
            f.Clock.Now = f.Clock.Now.AddMinutes(1);
            await Assert.ThrowsAsync<DbUpdateException>(() => f.Expiry.추천만료처리Async("recover"));
            Assert.DoesNotContain(f.Db.ChangeTracker.Entries<운영배차활동사건>(), x => x.Entity.사건유형Code == eventType);
            await f.Expiry.추천만료처리Async("recover");
            Assert.Equal(1, (await f.UseCase.수신상태조회Async("driver")).연속미응답횟수);
        }
        Assert.Single(await f.Db.운영배차활동사건.Where(x => x.사건유형Code == eventType).ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 만료커밋뒤_후보조회실패는_기존대기흐름이나만료재진입으로회복한다(bool expiryReentry)
    {
        await using var f = await Fixture.Create(sqlite: true);
        var queue = await f.AddOffer("retry-followup");
        queue.마지막거절기사Id = "previous-explicit-rejector";
        await f.Db.SaveChangesAsync();
        Assert.True(await f.Receipt.표시확인Async("driver", queue.의뢰Id, 1));
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        f.Candidates.FailNext = true;
        await Assert.ThrowsAsync<TimeoutException>(() => f.Expiry.추천만료처리Async(queue.의뢰Id));
        var saved = await f.Db.운송원장.AsNoTracking().SingleAsync();
        Assert.Equal(상태값.배차노출상태.추천대기, saved.배차노출상태);
        Assert.Equal("previous-explicit-rejector", saved.마지막거절기사Id);
        if (expiryReentry) await f.Expiry.추천만료처리Async(queue.의뢰Id);
        else await f.Expiry.추천대기처리Async(queue.의뢰Id);
        Assert.Equal("driver", f.Candidates.LastExcludedDriverId);
        Assert.Single(await f.Db.운영배차활동사건.Where(x => x.사건유형Code == 운영배차사건유형Code.무응답).ToArrayAsync());
        Assert.Equal(상태값.배차노출상태.추천후보없음, (await f.Db.운송원장.SingleAsync()).배차노출상태);
    }

    [Fact]
    public async Task 만료커밋성공_응답유실후_같은Context는_정본대기에서후속만재개한다()
    {
        await using var f = await Fixture.Create(sqlite: true, responseLostType: 운영배차사건유형Code.무응답);
        await f.AddOffer("commit-unknown");
        Assert.True(await f.Receipt.표시확인Async("driver", "commit-unknown", 1));
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await Assert.ThrowsAsync<TimeoutException>(() => f.Expiry.추천만료처리Async("commit-unknown"));
        Assert.Empty(f.Db.ChangeTracker.Entries<운영배차활동사건>().Where(x => x.Entity.사건유형Code == 운영배차사건유형Code.무응답));
        await f.Expiry.추천만료처리Async("commit-unknown");
        Assert.Equal("driver", f.Candidates.LastExcludedDriverId);
        Assert.Single(await f.Db.운영배차활동사건.Where(x => x.사건유형Code == 운영배차사건유형Code.무응답).ToArrayAsync());
        Assert.Equal(1, (await f.UseCase.수신상태조회Async("driver")).연속미응답횟수);
        Assert.Equal(상태값.배차노출상태.추천후보없음, (await f.Db.운송원장.SingleAsync()).배차노출상태);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection? _connection;
        private readonly DbContextOptions<SsalddelContext> _options;
        public SsalddelContext Db { get; }
        public Clock Clock { get; } = new();
        public 운영배차공통UseCase UseCase => NewUseCase(new InMemory운영배차판정ProjectionStore());
        public 운영배차제안표시Service Receipt => new(Db, UseCase, Clock);
        public NoCandidates Candidates { get; } = new();
        public 배차대기원장전환Service Expiry => new(Db, Options.Create(new 배차큐정책Options()),
            Candidates, null!, null!, new 음식배달배차흐름Resolver(), timeProvider: Clock);
        private Fixture(SqliteConnection? connection, DbContextOptions<SsalddelContext> options)
        { _connection = connection; _options = options; Db = NewContext(); }
        public SsalddelContext NewContext() => new(_options, new Encryption());
        public 운영배차공통UseCase NewUseCase(I운영배차판정ProjectionStore projection) => new(new Ef운영배차활동원장Store(Db),
            projection, new 운영배차수신상태Policy(), new 운영배차단기지표Calculator(), Clock, NullLogger<운영배차공통UseCase>.Instance);
        public static async Task<Fixture> Create(bool sqlite = false, bool failNoResponse = false,
            string? firstFailureType = null, bool commitFailure = false, string? responseLostType = null)
        {
            SqliteConnection? connection = null;
            var builder = new DbContextOptionsBuilder<SsalddelContext>();
            if (sqlite)
            { connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); builder.UseSqlite(connection); }
            else builder.UseInMemoryDatabase("no-response-" + Guid.NewGuid());
            if (failNoResponse) builder.AddInterceptors(new NoResponseSaveFailure());
            if (responseLostType is not null) builder.AddInterceptors(new FirstCommitResponseLoss(responseLostType));
            if (firstFailureType is not null)
            {
                if (commitFailure) builder.AddInterceptors(new FirstCommitFailure(firstFailureType));
                else builder.AddInterceptors(new FirstActivitySaveFailure(firstFailureType));
            }
            var f = new Fixture(connection, builder.Options);
            await f.Db.Database.EnsureCreatedAsync();
            await f.UseCase.기사의사변경Async("driver", new() { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.On });
            f.Clock.Now = f.Clock.Now.AddSeconds(1);
            return f;
        }
        public async Task<운송원장> AddOffer(string id)
        {
            Clock.Now = Clock.Now.AddSeconds(1);
            var queue = new 운송원장 { 의뢰Id = id, 배차업무유형 = 상태값.배차업무유형.음식배달,
                원본의뢰유형 = 운송의뢰배차원천유형.음식점주문, 상태 = 상태값.배차대기상태.대기,
                배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
                현재추천대상기사Id = "driver", 추천라운드 = 1, 추천시작시각 = Clock.Now,
                추천만료시각 = Clock.Now.AddSeconds(20), CreatedAt = Clock.Now, UpdatedAt = Clock.Now };
            Db.운송원장.Add(queue);
            Db.운영배차활동사건.Add(운영배차활동사건Factory.유효제안("driver", id, 1, Clock.Now));
            await Db.SaveChangesAsync(); return queue;
        }
        public async Task ExpireOffer(string id, bool receipt = true)
        {
            await AddOffer(id);
            if (receipt) Assert.True(await Receipt.표시확인Async("driver", id, 1));
            Clock.Now = Clock.Now.AddSeconds(21);
            await Expiry.추천만료처리Async(id);
        }
        public async ValueTask DisposeAsync()
        { await Db.DisposeAsync(); if (_connection is not null) await _connection.DisposeAsync(); }
    }
    private sealed class NoCandidates : I배차추천후보선정Service
    {
        public bool FailNext { get; set; }
        public string? LastExcludedDriverId { get; private set; }
        public Task<배차추천후보선정결과> 다음후보선정Async(string requestId, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        {
            LastExcludedDriverId = 제외기사Id;
            if (FailNext) { FailNext = false; throw new TimeoutException("후보 조회 일시 실패"); }
            return Task.FromResult(배차추천후보선정결과.적격후보없음("후보 없음"));
        }
    }
    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 10, 16, 1, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class NoResponseSaveFailure : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<운영배차활동사건>().Any(x => x.State == EntityState.Added
                && x.Entity.사건유형Code == 운영배차사건유형Code.무응답)) throw new InvalidOperationException("NoResponseSaveFailed");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FirstActivitySaveFailure(string eventType) : SaveChangesInterceptor
    {
        private bool _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_failed && eventData.Context!.ChangeTracker.Entries<운영배차활동사건>()
                    .Any(x => x.State == EntityState.Added && x.Entity.사건유형Code == eventType))
            { _failed = true; throw new DbUpdateException("첫 저장 일시 실패"); }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FirstCommitFailure(string eventType) : DbTransactionInterceptor
    {
        private bool _failed;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!_failed && eventData.Context!.ChangeTracker.Entries<운영배차활동사건>()
                    .Any(x => x.Entity.사건유형Code == eventType))
            { _failed = true; throw new DbUpdateException("첫 커밋 일시 실패"); }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FirstCommitResponseLoss(string eventType) : DbTransactionInterceptor
    {
        private bool _failed;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (!_failed && eventData.Context!.ChangeTracker.Entries<운영배차활동사건>()
                    .Any(x => x.Entity.사건유형Code == eventType))
            { _failed = true; throw new TimeoutException("커밋 성공 응답 유실"); }
            return Task.CompletedTask;
        }
    }
}
