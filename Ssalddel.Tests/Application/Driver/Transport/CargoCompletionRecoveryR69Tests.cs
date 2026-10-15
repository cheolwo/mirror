using System.Text.Json.Nodes;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Operations;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class CargoCompletionRecoveryR69Tests
{
    [Fact]
    public async Task 완료저장후발행실패는새Context에서복구하고중복인계나입금준비를만들지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var clock = new Clock();
        await using (var first = Db(connection))
        {
            await first.Database.EnsureCreatedAsync(); await Complete(first);
            Assert.Equal("Pending", State(await first.운송이벤트.SingleAsync()));
            Assert.Empty(first.운영체제업무인계); Assert.Empty(first.결제);
        }
        await using var db = Db(connection); var recovery = Service(db, clock);
        Assert.Equal(1, await recovery.ProcessPendingAsync());
        Assert.Equal(0, await recovery.ProcessPendingAsync());
        Assert.False(await recovery.ProcessTransportAsync((await db.운송원장.AsNoTracking().SingleAsync()).Id));
        var recorded = await db.운송이벤트.AsNoTracking().SingleAsync(); Assert.Equal("Done", State(recorded));
        Assert.Single(db.운영체제업무인계); Assert.Single(db.운영체제업무인계Outbox);
        Assert.Single(db.결제); Assert.Equal(3, await db.Command알림Outbox.CountAsync());
        Assert.Equal("입금대기", (await db.화주운송의뢰.AsNoTracking().SingleAsync()).정산상태);
        Assert.Equal("인수완료", (await db.화주운송의뢰.AsNoTracking().SingleAsync()).배차상태);
        Assert.DoesNotContain("private.invalid", recorded.메타데이터); Assert.DoesNotContain("proof/photo.jpg", recorded.메타데이터);
    }

    [Fact]
    public async Task 입금준비저장뒤예외도전체후속트랜잭션을롤백하고같은이벤트로다시마친다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Complete(db);
        var clock = new Clock(); var payment = new FailingPayment(new 운송완료입금요청Service(db));
        var recovery = Service(db, clock, payment: payment);
        Assert.Equal(1, await recovery.ProcessPendingAsync());
        Assert.Equal("Retry", State(await db.운송이벤트.AsNoTracking().SingleAsync()));
        Assert.Empty(db.결제); Assert.Empty(db.운영체제업무인계); Assert.Empty(db.Command알림Outbox);
        Assert.DoesNotContain("private-secret", (await db.운송이벤트.AsNoTracking().SingleAsync()).메타데이터);
        payment.Fail = false; clock.Advance();
        Assert.Equal(1, await recovery.ProcessPendingAsync());
        Assert.Equal("Done", State(await db.운송이벤트.AsNoTracking().SingleAsync()));
        Assert.Single(db.결제); Assert.Single(db.운영체제업무인계); Assert.Equal(3, await db.Command알림Outbox.CountAsync());
    }

    [Fact]
    public async Task 반복실패는상한과이유를남기고명시재시도전까지자동시도하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Complete(db);
        var clock = new Clock(); var handoff = new FailingHandoff(Handoff(db, clock));
        var recovery = Service(db, clock, handoff: handoff);
        Assert.Equal(1, await recovery.ProcessPendingAsync()); clock.Advance();
        Assert.Equal(1, await recovery.ProcessPendingAsync()); clock.Advance();
        Assert.Equal(0, await recovery.ProcessPendingAsync());
        var row = await db.운송이벤트.AsNoTracking().SingleAsync(); Assert.Equal("Failed", State(row));
        Assert.Equal(2, Job(row)["attempts"]!.GetValue<int>()); Assert.Equal("CargoHandoffPreparationFailed", Job(row)["reasonCode"]!.GetValue<string>());
        Assert.False(await recovery.RetryAsync(row.Id, 1)); Assert.True(await recovery.RetryAsync(row.Id, 2));
        handoff.Fail = false; Assert.Equal(1, await recovery.ProcessPendingAsync());
        Assert.Equal("Done", State(await db.운송이벤트.AsNoTracking().SingleAsync())); Assert.Single(db.결제);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("changed-proof")]
    [InlineData("canceled")]
    public async Task 과거이벤트나현재완료근거가달라진건을자동승격하지않는다(string scenario)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Complete(db);
        var row = await db.운송이벤트.SingleAsync(); var transport = await db.운송원장.SingleAsync();
        if (scenario == "legacy") { var node = JsonNode.Parse(row.메타데이터)!.AsObject(); node.Remove("cargoCloseoutR69"); row.메타데이터 = node.ToJsonString(); }
        if (scenario == "changed-proof") transport.첨부_json = "[]";
        if (scenario == "canceled") transport.상태 = "취소";
        await db.SaveChangesAsync();
        Assert.Equal(scenario == "legacy" ? 0 : 1, await Service(db, new Clock()).ProcessPendingAsync());
        Assert.Empty(db.결제); Assert.Empty(db.운영체제업무인계);
        if (scenario != "legacy") Assert.Equal("Blocked", State(await db.운송이벤트.AsNoTracking().SingleAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 살아있는lease는건너뛰고만료된lease는같은이벤트로재개한다(bool expired)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Complete(db);
        var clock = new Clock(); var row = await db.운송이벤트.SingleAsync(); var node = JsonNode.Parse(row.메타데이터)!;
        node["cargoCloseoutR69"]!["state"] = "Processing"; node["cargoCloseoutR69"]!["attempts"] = 1;
        node["cargoCloseoutR69"]!["leaseUntilUtc"] = clock.GetUtcNow().UtcDateTime.AddSeconds(expired ? -1 : 60);
        node["cargoCloseoutR69"]!["leaseToken"] = "prior-process"; row.메타데이터 = node.ToJsonString();
        await db.SaveChangesAsync(); var stale = row.메타데이터;
        Assert.Equal(expired ? 1 : 0, await Service(db, clock).ProcessPendingAsync());
        if (expired)
        {
            Assert.Single(db.결제);
            Assert.Equal(0, await db.운송이벤트.Where(x => x.Id == row.Id && x.메타데이터 == stale)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.메타데이터, stale)));
        }
        else Assert.Empty(db.결제);
    }

    [Fact]
    public async Task 외부업무트랜잭션이열려있으면완료후속을커밋전에실행하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Complete(db);
        var service = Service(db, new Clock());
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            Assert.Equal(0, await service.ProcessPendingAsync()); Assert.Empty(db.결제); Assert.Empty(db.운영체제업무인계);
            await tx.CommitAsync();
        }
        Assert.Equal(1, await service.ProcessPendingAsync()); Assert.Single(db.결제);
    }

    [Fact]
    public async Task SQL재조회가마이크로초로절삭되어도같은완료이벤트로인식한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Complete(db);
        var transport = await db.운송원장.SingleAsync();
        transport.도착 = new DateTime(CargoCompletionRecoveryMarker.UtcMicrosecondTicks(transport.도착!.Value), DateTimeKind.Utc);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(1, await Service(db, new Clock()).ProcessPendingAsync());
        Assert.Equal("Done", State(await db.운송이벤트.AsNoTracking().SingleAsync())); Assert.Single(db.결제);
    }

    [Fact]
    public async Task 첫페이지의미래대기작업은다음페이지의현재처리작업을막지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Complete(db);
        var clock = new Clock(); var original = await db.운송이벤트.SingleAsync(); var ready = original.메타데이터;
        var node = JsonNode.Parse(ready)!; node["cargoCloseoutR69"]!["state"] = "Retry";
        node["cargoCloseoutR69"]!["nextAttemptAtUtc"] = clock.GetUtcNow().UtcDateTime.AddHours(1); original.메타데이터 = node.ToJsonString();
        for (var i = 0; i < 24; i++) db.운송이벤트.Add(new() { 의뢰Id = "cargo-r69", 이벤트타입 = original.이벤트타입, 메타데이터 = node.ToJsonString() });
        db.운송이벤트.Add(new() { 의뢰Id = "cargo-r69", 이벤트타입 = original.이벤트타입, 메타데이터 = ready });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(1, await Service(db, clock).ProcessPendingAsync(1));
        Assert.Equal(1, (await db.운송이벤트.AsNoTracking().ToArrayAsync()).Count(x => State(x) == "Done")); Assert.Single(db.결제);
    }

    [Fact]
    public async Task 알수없는입금준비실패는완료로승격하지않고인계준비와보류사유를구분한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Complete(db);
        Assert.Equal(1, await Service(db, new Clock(), payment: new UnknownPayment()).ProcessPendingAsync());
        var job = Job(await db.운송이벤트.AsNoTracking().SingleAsync());
        Assert.Equal("Blocked", job["state"]!.GetValue<string>()); Assert.True(job["handoffPrepared"]!.GetValue<bool>());
        Assert.False(job["paymentPreparationDone"]!.GetValue<bool>());
        Assert.Equal("CargoPaymentPreparationUnconfirmed", job["reasonCode"]!.GetValue<string>()); Assert.Empty(db.결제);
    }

    private static async Task Complete(SsalddelContext db)
    {
        var row = new 운송원장 { 의뢰Id = "cargo-r69", 운송번호 = "cargo-r69", 기사_운송자 = "driver", 확정기사Id = "driver", 상태 = "하차지도착" };
        db.Add(row); db.Add(new 화주운송의뢰 { 의뢰Id = "cargo-r69", 화주Id = "shipper", 주문자UserId = "shipper", 배차상태 = "하차지도착",
            정산시점 = "운송완료후정산", 수납주체 = "플랫폼", 정산상태 = "청구대기", 결제상태 = "결제대기", 결제예정금액 = 35000 });
        await db.SaveChangesAsync(); var photos = new CargoPhotoTestStorage();
        await photos.SeedReceiptAsync(db, row.Id, "driver", "dropoff-complete-photo", "proof/photo.jpg");
        var executor = new 기사운송상태변경CommandExecutor(db, new 기사운송상태전이Service(), new ThrowingPublisher(),
            new User("driver", "기사"), new 참여자실행권한검사(), NullLogger<기사운송상태변경CommandExecutor>.Instance);
        var result = await new 운송인수완료CommandHandler(executor, new 운송증빙첨부JsonWriter(), db, photos)
            .Handle(new("driver", row.Id) { 하차사진ObjectName = "proof/photo.jpg" }, default);
        Assert.True(result.IsSuccess); db.ChangeTracker.Clear();
    }
    private static CargoCompletionRecoveryService Service(SsalddelContext db, Clock clock,
        I화물운송완료화주인수인계Service? handoff = null, I운송완료입금요청Service? payment = null)
        => new(db, handoff ?? Handoff(db, clock), payment ?? new 운송완료입금요청Service(db),
            Options.Create(new CargoCompletionRecoveryOptions { MaximumAttempts = 2, RetrySeconds = 1 }), clock,
            NullLogger<CargoCompletionRecoveryService>.Instance);
    private static I화물운송완료화주인수인계Service Handoff(SsalddelContext db, Clock clock)
        => new 화물운송완료화주인수인계Service(db, new 운영체제업무인계Coordinator(db, clock), clock);
    private static JsonNode Job(운송이벤트 row) => JsonNode.Parse(row.메타데이터)!["cargoCloseoutR69"]!;
    private static string State(운송이벤트 row) => Job(row)["state"]!.GetValue<string>();
    private static SsalddelContext Db(SqliteConnection connection) => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed record User(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Clock : TimeProvider
    { private DateTimeOffset value = DateTimeOffset.UtcNow.AddMinutes(1); public override DateTimeOffset GetUtcNow() => value; public void Advance() => value = value.AddSeconds(2); }
    private sealed class ThrowingPublisher : IPublisher
    { public Task Publish(object value, CancellationToken ct = default) => throw new InvalidOperationException("publication-interrupted");
      public Task Publish<T>(T value, CancellationToken ct = default) where T : INotification => throw new InvalidOperationException("publication-interrupted"); }
    private sealed class FailingHandoff(I화물운송완료화주인수인계Service inner) : I화물운송완료화주인수인계Service
    {
        public bool Fail { get; set; } = true;
        public Task<운영체제업무인계Dto> 완료결과요청Async(long id, bool evidence, CancellationToken ct = default)
            => Fail ? throw new IOException("private-secret") : inner.완료결과요청Async(id, evidence, ct);
        public Task<운영체제업무인계Dto?> 화주인수Async(string id, CancellationToken ct = default) => inner.화주인수Async(id, ct);
    }
    private sealed class FailingPayment(I운송완료입금요청Service inner) : I운송완료입금요청Service
    {
        public bool Fail { get; set; } = true;
        public Task<운송완료입금요청결과> 조기준비Async(운송상차완료됨Event value, CancellationToken ct = default) => inner.조기준비Async(value, ct);
        public async Task<운송완료입금요청결과> 준비Async(운송인수완료됨Event value, CancellationToken ct = default)
        { var result = await inner.준비Async(value, ct); if (Fail) throw new IOException("private-secret"); return result; }
    }
    private sealed class UnknownPayment : I운송완료입금요청Service
    {
        public Task<운송완료입금요청결과> 조기준비Async(운송상차완료됨Event value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운송완료입금요청결과> 준비Async(운송인수완료됨Event value, CancellationToken ct = default)
            => Task.FromResult(new 운송완료입금요청결과(false, "화주 운송 의뢰 없음"));
    }
}
