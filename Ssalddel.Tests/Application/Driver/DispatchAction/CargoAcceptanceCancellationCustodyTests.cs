using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed class CargoAcceptanceCancellationCustodyTests
{
    // DB명은 case마다 고유하게 유지하고 EF singleton 서비스는 같은 root로 재사용한다.
    private static readonly InMemoryDatabaseRoot SharedDatabaseRoot = new();

    [Theory]
    [InlineData("확정", true)]
    [InlineData("배차확정", true)]
    [InlineData("이동중", true)]
    [InlineData("상차지도착", true)]
    [InlineData("상차완료", false)]
    [InlineData("운송중", false)]
    [InlineData("하차지도착", false)]
    [InlineData("인수완료", false)]
    [InlineData("취소", false)]
    [InlineData("알수없는상태", false)]
    public void 확정큐표식만남아있어도_상차이후와미확인상태는일반수락취소대상이아니다(string state, bool allowed)
    {
        var queue = Queue(); queue.상태 = state;
        Assert.Equal(allowed, 배차응답가능정책.수락취소가능(queue, "driver-1"));
    }

    [Theory]
    [InlineData("pickup-time")]
    [InlineData("arrival-time")]
    [InlineData("loaded")]
    [InlineData("driving")]
    [InlineData("delivered")]
    public async Task 인증기사의취소명령도_물품인수사실이있으면409이고_기사증빙정산을보존한다(string stage)
    {
        await using var f = new Fixture();
        if (stage == "pickup-time") f.Queue.출발_픽업 = DateTime.UtcNow;
        if (stage == "arrival-time") f.Queue.도착 = DateTime.UtcNow;
        if (stage == "loaded") f.Queue.상태 = 기사운송상태코드.상차완료;
        if (stage == "driving") f.Queue.상태 = 기사운송상태코드.운송중;
        if (stage == "delivered") f.Queue.상태 = 기사운송상태코드.인수완료;
        await f.Db.SaveChangesAsync(); var before = Snapshot(f.Queue);
        var publisher = new Publisher();
        var result = await f.Handler(publisher).Handle(new("driver-1", "quick-1"), default);
        Assert.True(result.IsFailed);
        Assert.Equal(409, Assert.Single(result.Errors).Metadata["StatusCode"]);
        Assert.Equal(배차응답가능정책.물품보유복구필요Code, result.Errors[0].Metadata["ErrorCode"]);
        Assert.Contains("문제 신고", result.Errors[0].Message); Assert.Equal(0, publisher.Count);
        await f.AssertPreserved(before);
    }

    [Fact]
    public async Task 직접서비스와사후이벤트경로도_추적된상차전사본으로최신픽업사실을지우지않는다()
    {
        await using var f = new Fixture();
        await using (var writer = f.AnotherContext())
        {
            var current = await writer.운송원장.SingleAsync();
            current.상태 = 기사운송상태코드.상차완료; current.출발_픽업 = DateTime.UtcNow;
            await writer.SaveChangesAsync();
        }
        Assert.Equal("확정", f.Queue.상태);
        var result = await f.Service.배차수락취소처리Async("quick-1", "driver-1");
        Assert.False(result.전환여부); Assert.Equal(배차응답가능정책.물품보유복구필요Code, result.결과코드);
        await using var read = f.AnotherContext(); var stored = await read.운송원장.SingleAsync();
        Assert.Equal(기사운송상태코드.상차완료, stored.상태); Assert.NotNull(stored.출발_픽업);
        Assert.Equal("driver-1", stored.확정기사Id); Assert.Equal("driver-1", stored.기사_운송자);
    }

    [Fact]
    public async Task 사전취소검사후픽업이먼저저장되면_실제전환서비스가다시검사하고취소이벤트를발행하지않는다()
    {
        await using var f = new Fixture(); var publisher = new Publisher();
        var service = new InterleavingService(f.Service, async () =>
        {
            await using var writer = f.AnotherContext(); var stored = await writer.운송원장.SingleAsync();
            stored.상태 = 기사운송상태코드.상차완료; stored.출발_픽업 = DateTime.UtcNow;
            await writer.SaveChangesAsync();
        });
        var handler = new 배차수락취소CommandHandler(f.Db, service, publisher, new User(), new 참여자실행권한검사(),
            NullLogger<배차수락취소CommandHandler>.Instance);
        var result = await handler.Handle(new("driver-1", "quick-1"), default);
        Assert.True(result.IsFailed); Assert.Equal(0, publisher.Count);
        Assert.Equal(배차응답가능정책.물품보유복구필요Code, result.Errors[0].Metadata["ErrorCode"]);
        await using var read = f.AnotherContext(); var current = await read.운송원장.SingleAsync();
        Assert.NotNull(current.출발_픽업); Assert.Equal("driver-1", current.기사_운송자);
        Assert.Equal("확정", (await read.화주운송의뢰.SingleAsync()).배차상태);
    }

    [Theory]
    [InlineData("different-driver")]
    [InlineData("different-performer")]
    [InlineData("member")]
    [InlineData("source-loaded")]
    public async Task 다른담당자나전용배송또는원의뢰상차사실을_일반취소로우회하지않는다(string mismatch)
    {
        await using var f = new Fixture();
        if (mismatch == "different-driver") { f.Queue.확정기사Id = "other"; f.Queue.현재추천대상기사Id = "driver-1"; }
        if (mismatch == "different-performer") f.Queue.기사_운송자 = "other";
        if (mismatch == "member") f.Queue.일반회원배송전용 = true;
        if (mismatch == "source-loaded") f.Source.배차상태 = 기사운송상태코드.상차완료;
        await f.Db.SaveChangesAsync(); var before = Snapshot(f.Queue);
        var result = await f.Service.배차수락취소처리Async("quick-1", "driver-1");
        Assert.False(result.전환여부); await f.AssertPreserved(before, f.Source.배차상태);
    }

    [Theory]
    [InlineData("review")]
    [InlineData("settlement-hold")]
    [InlineData("partial")]
    [InlineData("full")]
    public async Task 픽업전이라도_현재사건검토나업무정산보류를취소로해제하지않는다(string hold)
    {
        await using var f = new Fixture();
        f.Db.비정상운송사건.Add(new()
        {
            사건StableId = "incident-1", 운송Id = f.Queue.Id, 운송의뢰Id = "quick-1",
            상태Code = hold == "review" ? 비정상운송사건상태Codes.운영검토대기 : 비정상운송사건상태Codes.조치결정,
            정산보류적용여부 = hold == "settlement-hold",
            업무통제상태Code = hold == "partial" ? 비정상운송업무통제상태Codes.일부보류
                : hold == "full" ? 비정상운송업무통제상태Codes.전체보류 : 비정상운송업무통제상태Codes.주의진행
        });
        await f.Db.SaveChangesAsync(); var before = Snapshot(f.Queue);
        var result = await f.Service.배차수락취소처리Async("quick-1", "driver-1");
        Assert.False(result.전환여부); await f.AssertPreserved(before);
        Assert.Single(await f.Db.비정상운송사건.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task 미저장업무변경은덮어쓰지않고_취소쓰기없이재확인으로남긴다()
    {
        await using var f = new Fixture(); f.Queue.메모 = "아직 저장하지 않은 현장 내용";
        var result = await f.Service.배차수락취소처리Async("quick-1", "driver-1");
        Assert.False(result.전환여부); Assert.Equal("아직 저장하지 않은 현장 내용", f.Queue.메모);
        await using var read = f.AnotherContext(); Assert.Equal("확정", (await read.운송원장.SingleAsync()).상태);
    }

    [Fact]
    public async Task 실제관계형저장에서도_상차전취소는가능하고_재실행은새기사나재원을바꾸지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options;
        await using var db = new SsalddelContext(options, new Encryption()); await db.Database.EnsureCreatedAsync();
        var queue = Queue(); var source = Source(); db.AddRange(queue, source); await db.SaveChangesAsync();
        var service = Service(db);
        var cancelled = await service.배차수락취소처리Async("quick-1", "driver-1");
        Assert.True(cancelled.전환여부); db.ChangeTracker.Clear();
        var current = await db.운송원장.SingleAsync();
        Assert.Equal(상태값.배차대기상태.대기, current.상태); Assert.Null(current.확정기사Id); Assert.Empty(current.기사_운송자);
        Assert.Equal(상태값.배차큐단계.공개배차, current.배차큐단계); Assert.Null(current.출발_픽업);
        Assert.Equal(4200m, current.기사지급예정액); Assert.Equal("driver-1", current.마지막거절기사Id);
        Assert.False((await service.배차수락취소처리Async("quick-1", "driver-1")).전환여부);
        var storedSource = await db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(상태값.배차상태.매칭중, storedSource.배차상태);
        Assert.Equal(상태값.결제상태.결제완료, storedSource.결제상태); Assert.Equal("원 수납 근거 보존", storedSource.정산메모);
    }

    private static 운송원장 Queue() => new()
    {
        의뢰Id = "quick-1", 운송번호 = "quick-1", 상태 = "확정", 확정기사Id = "driver-1", 기사_운송자 = "driver-1",
        배차큐단계 = 상태값.배차큐단계.확정, 배차노출상태 = 상태값.배차노출상태.확정,
        생활배송배차방식 = NeighborhoodDispatchModes.PublicCall, 기사지급예정액 = 4200m, 첨부_json = "[]"
    };
    private static 화주운송의뢰 Source() => new()
    { 의뢰Id = "quick-1", 화주Id = "owner", 주문자UserId = "owner", 배차상태 = "확정", 결제상태 = 상태값.결제상태.결제완료, 정산메모 = "원 수납 근거 보존" };
    private static string Snapshot(운송원장 queue) => System.Text.Json.JsonSerializer.Serialize(queue);
    private static 배차대기원장전환Service Service(SsalddelContext db)
        => new(db, Options.Create(new 배차큐정책Options()), null!, null!, null!, null!);
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DbContextOptions<SsalddelContext> options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase("cargo-cancel-" + Guid.NewGuid(), SharedDatabaseRoot)
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        public SsalddelContext Db { get; }
        public 운송원장 Queue { get; }
        public 화주운송의뢰 Source { get; }
        public 배차대기원장전환Service Service { get; }
        public Fixture()
        {
            Db = AnotherContext(); Queue = CargoAcceptanceCancellationCustodyTests.Queue(); Source = CargoAcceptanceCancellationCustodyTests.Source();
            Db.AddRange(Queue, Source); Db.SaveChanges(); Service = CargoAcceptanceCancellationCustodyTests.Service(Db);
        }
        public SsalddelContext AnotherContext() => new(options, new Encryption());
        public 배차수락취소CommandHandler Handler(Publisher publisher) => new(Db, Service, publisher, new User(), new 참여자실행권한검사(),
            NullLogger<배차수락취소CommandHandler>.Instance);
        public async Task AssertPreserved(string before, string sourceState = "확정")
        {
            await using var read = AnotherContext(); Assert.Equal(before, Snapshot(await read.운송원장.SingleAsync()));
            var source = await read.화주운송의뢰.SingleAsync(); Assert.Equal(sourceState, source.배차상태);
            Assert.Equal(상태값.결제상태.결제완료, source.결제상태); Assert.Equal("원 수납 근거 보존", source.정산메모);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed record User(string? UserId = "driver-1", string? Role = "기사") : ICurrentUserAccessor;
    private sealed class Publisher : IPublisher
    {
        public int Count { get; private set; }
        public Task Publish(object notification, CancellationToken cancellationToken = default) { Count++; return Task.CompletedTask; }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        { Count++; return Task.CompletedTask; }
    }
    private sealed class InterleavingService(I배차대기원장전환Service inner, Func<Task> before) : I배차대기원장전환Service
    {
        public async Task<배차대기원장전환결과> 배차수락취소처리Async(string id, string driver, string? reason = null, CancellationToken ct = default)
        { await before(); return await inner.배차수락취소처리Async(id, driver, reason, ct); }
        public Task<배차대기원장전환결과> 계획배차에서추천으로전환Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천대기처리Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천시작Async(string id, string driver, int? timeout = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driver, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driver, string? reason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천만료처리Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 공개배차로전환Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 실행주체확정결과동기화Async(string id, DispatchConfirmationBoundaryRequest confirmation, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
