using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Admin.Settlement;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Admin.Settlement;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Controllers.Admin.Settlement;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Admin.Settlement;

public sealed class 배송료정산배치R73Tests
{
    // 한국 시간 2026-10-07 수요일 00:00입니다.
    private static readonly DateTime Start = new(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 주간명세는서버승인채무만참조하고새채무나실송금을만들지않는다()
    {
        await using var f = await Fixture.Create();
        var ids = await f.Seed(2);
        var result = await f.Use().준비Async(Request(ids));
        Assert.True(result.IsSuccess, Errors(result));
        Assert.Equal("Weekly", result.Value.ScheduleCode);
        Assert.Equal("Prepared", result.Value.StatusCode);
        Assert.Equal(85000m, result.Value.ReadyTotalAmount);
        Assert.Equal(2, result.Value.ReadyCount);
        Assert.Equal("WorkCompletedAt", result.Value.PeriodBasisCode);
        Assert.Equal(Start.AddDays(8).AddHours(9), result.Value.PayoutScheduledAtUtc);
        Assert.False(result.Value.BankCalendarApplied);
        Assert.False(result.Value.IsActualTransferCompleted);
        Assert.Equal(2, await f.Db.기사운송대금지급요청.CountAsync());
        Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
        Assert.All(result.Value.Lines, x => Assert.Equal("Prepared", x.StatusCode));
    }

    [Fact]
    public async Task 같은채무를건별에서주간명세로다시넣을수없다()
    {
        await using var f = await Fixture.Create();
        var ids = await f.Seed(1);
        var single = Request(ids, "single"); single.ScheduleCode = "Single"; single.PeriodEndUtc = Start.AddDays(1);
        var first = await f.Use().준비Async(single);
        Assert.True(first.IsSuccess, Errors(first));
        var duplicate = await f.Use().준비Async(Request(ids, "weekly"));
        AssertStatus(duplicate, 409);
        Assert.Equal(1, await f.Db.기사운송대금정산배치.CountAsync());
        Assert.Equal(1, await f.Db.기사운송대금정산배치행.CountAsync());
    }

    [Fact]
    public async Task 멱등재시도는순서와무관하게같은명세를반환하고내용변경은거부한다()
    {
        await using var f = await Fixture.Create();
        var ids = await f.Seed(2);
        var first = await f.Use().준비Async(Request(ids));
        var replay = await f.Use().준비Async(Request(ids.Reverse().ToArray()));
        Assert.True(replay.IsSuccess, Errors(replay));
        Assert.True(replay.Value.IsIdempotentReplay);
        Assert.Equal(first.Value.BatchId, replay.Value.BatchId);
        var changed = Request(ids); changed.ConfirmedTotalAmount = 1m;
        AssertStatus(await f.Use().준비Async(changed), 409);
        Assert.Equal(1, await f.Db.기사운송대금정산배치.CountAsync());
        Assert.Equal(2, await f.Db.기사운송대금정산배치행.CountAsync());
    }

    [Fact]
    public async Task 보류된행만제외하고정상행의명세와금액을유지한다()
    {
        await using var f = await Fixture.Create();
        var ids = await f.Seed(2);
        f.Db.비정상운송사건.Add(new 비정상운송사건
        {
            사건StableId = "incident-b", 운송의뢰Id = "request-1", 사건유형Code = 비정상운송사건유형Codes.화물훼손,
            상태Code = 비정상운송사건상태Codes.운영검토대기, 정산보류적용여부 = true
        });
        await f.Db.SaveChangesAsync();
        var result = await f.Use().준비Async(Request(ids));
        Assert.True(result.IsSuccess, Errors(result));
        Assert.Equal("PartiallyHeld", result.Value.StatusCode);
        Assert.Equal(85000m, result.Value.ApprovedTotalAmount);
        Assert.Equal(42000m, result.Value.ReadyTotalAmount);
        Assert.Single(result.Value.Lines, x => x.StatusCode == "Held");
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Lines.Single(x => x.StatusCode == "Held").HoldReasonCode));
    }

    [Fact]
    public async Task 보류해제시도도계좌와최신수납을재검증하고판본경합을보호한다()
    {
        await using var f = await Fixture.Create();
        var ids = await f.Seed(1);
        var prepared = (await f.Use().준비Async(Request(ids))).Value;
        var account = await f.Db.Set<기사정산계좌>().SingleAsync(); account.확인상태 = "미확인";
        await f.Db.SaveChangesAsync();
        var held = await f.Use().재검증Async(prepared.BatchId, new() { ExpectedRevision = prepared.Revision });
        Assert.True(held.IsSuccess, Errors(held)); Assert.Equal("Held", held.Value.StatusCode); Assert.Equal(0m, held.Value.ReadyTotalAmount);
        AssertStatus(await f.Use().재검증Async(prepared.BatchId, new() { ExpectedRevision = prepared.Revision }), 409);
        account.확인상태 = 기사정산계좌확인상태.확인완료;
        var source = await f.Db.화주운송의뢰.SingleAsync(); source.결제상태 = 상태값.결제상태.환불됨;
        await f.Db.SaveChangesAsync();
        var refunded = await f.Use().재검증Async(prepared.BatchId, new() { ExpectedRevision = held.Value.Revision });
        Assert.Equal("CollectionCancelledOrRefunded", refunded.Value.Lines.Single().HoldReasonCode);
        source.결제상태 = 상태값.결제상태.결제완료;
        await f.Db.SaveChangesAsync();
        var ready = await f.Use().재검증Async(prepared.BatchId, new() { ExpectedRevision = refunded.Value.Revision });
        Assert.Equal("Prepared", ready.Value.StatusCode);
        Assert.Equal(42000m, ready.Value.ReadyTotalAmount);
    }

    [Theory]
    [InlineData("WAITING_FOR_DEPOSIT", 65000, false)]
    [InlineData("PARTIAL_CANCELED", 64000, false)]
    [InlineData("CANCELED", 0, false)]
    [InlineData("DONE", 65000, true)]
    public async Task PG완료문자열만으로수납을확정하지않고원본완료근거를검사한다(string status, int balance, bool expectedReady)
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        f.Db.결제.Add(new 결제
        {
            결제Id = "payment-a", 의뢰Id = "request-0", 대상Id = "request-0", 화주Id = "shipper",
            결제금액 = 65000, 통화 = "KRW", OrderId = "order-a", PaymentKey = "private-key-a",
            결제상태 = 상태값.결제상태.결제완료, 공통결제상태 = 결제공통정의.결제상태.승인완료,
            결제제공자 = 결제공통정의.결제제공자.TossPayments, 승인일시 = Start.AddHours(1),
            Toss응답Json = System.Text.Json.JsonSerializer.Serialize(new
            {
                status, balanceAmount = balance, totalAmount = 65000, currency = "KRW", orderId = "order-a", paymentKey = "private-key-a"
            })
        });
        await f.Db.SaveChangesAsync();
        var result = await f.Use().준비Async(Request(ids)); Assert.True(result.IsSuccess, Errors(result));
        Assert.Equal(expectedReady ? "Prepared" : "Held", result.Value.StatusCode);
        Assert.DoesNotContain("private-key-a", System.Text.Json.JsonSerializer.Serialize(result.Value), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NoArrival", "TransportCompletionChanged")]
    [InlineData("FareChanged", "FareChanged")]
    [InlineData("MissingCollection", "CollectionEvidenceMissing")]
    public async Task 완료와운임과정본수납근거가바뀌면전체보류이유를남긴다(string mutation, string code)
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        switch (mutation)
        {
            case "NoArrival": (await f.Db.운송원장.SingleAsync()).도착 = null; break;
            case "FareChanged": (await f.Db.운임구성.SingleAsync()).기사지급예정운임 = 43000m; break;
            case "MissingCollection": (await f.Db.화주운송의뢰.SingleAsync()).정산상태 = "대기"; break;
        }
        await f.Db.SaveChangesAsync();
        var result = await f.Use().준비Async(Request(ids)); Assert.True(result.IsSuccess, Errors(result));
        Assert.Equal("Held", result.Value.StatusCode); Assert.Equal(code, result.Value.Lines.Single().HoldReasonCode);
        Assert.Equal(0m, result.Value.ReadyTotalAmount); Assert.Equal(42000m, result.Value.ApprovedTotalAmount);
    }

    [Theory]
    [InlineData(false, "Prepared")]
    [InlineData(true, "Held")]
    public async Task 승인혜택의차감결제는동결근거로검사하고원천변경은보류한다(bool changeSource, string expected)
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        var source = await f.Db.화주운송의뢰.SingleAsync();
        var payment = new 결제
        {
            결제Id = "benefit-payment", 의뢰Id = source.의뢰Id, 대상Id = source.의뢰Id, 화주Id = "shipper", OrderId = "benefit-order",
            결제금액 = 62000, 통화 = "KRW", PaymentKey = "fixture-key", 결제상태 = 상태값.결제상태.결제완료,
            공통결제상태 = 결제공통정의.결제상태.승인완료, 결제제공자 = 결제공통정의.결제제공자.TossPayments, 승인일시 = Start.AddHours(1),
            Toss응답Json = "{\"status\":\"DONE\",\"paymentKey\":\"fixture-key\",\"orderId\":\"benefit-order\",\"totalAmount\":62000,\"currency\":\"KRW\",\"balanceAmount\":62000}"
        };
        var evidence = Ssalddel.Application.Shipper.Payment.배송료결제준비근거Policy.생성(payment, source, new()
        {
            원운임 = 65000, 포인트사용가능액 = 1000, 콘텐츠할인금액 = 2000, 최종결제금액 = 62000
        });
        Assert.True(evidence.IsSuccess);
        payment.준비근거Json = evidence.Value;
        f.Db.결제.Add(payment);
        if (changeSource) source.결제예정금액 = 66000;
        await f.Db.SaveChangesAsync();
        var result = await f.Use().준비Async(Request(ids)); Assert.True(result.IsSuccess, Errors(result));
        Assert.Equal(expected, result.Value.StatusCode);
        if (changeSource) Assert.Equal("CollectionPreparationChanged", result.Value.Lines.Single().HoldReasonCode);
        else Assert.Equal(42000m, result.Value.ReadyTotalAmount);
    }

    [Theory]
    [InlineData("DifferentDriver")]
    [InlineData("DifferentMode")]
    [InlineData("DifferentCurrency")]
    public async Task 기사통화모드가다른채무를한배치에묶지않는다(string mutation)
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(2);
        var second = await f.Db.기사운송대금지급요청.SingleAsync(x => x.Id == ids[1]);
        if (mutation == "DifferentDriver") second.기사Id = "other-driver";
        if (mutation == "DifferentMode") second.실행모드코드 = "Operational";
        if (mutation == "DifferentCurrency") second.통화코드 = "USD";
        await f.Db.SaveChangesAsync();
        AssertStatus(await f.Use().준비Async(Request(ids)), 409);
        Assert.Empty(await f.Db.기사운송대금정산배치.ToArrayAsync());
    }

    [Fact]
    public async Task 옵션OFF에서도기존명세는조회가능하고쓰기와비관리자는차단된다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        var created = (await f.Use().준비Async(Request(ids))).Value;
        AssertStatus(await f.Use(enabled: false).준비Async(Request(ids, "off")), 409);
        AssertStatus(await f.Use(enabled: false).재검증Async(created.BatchId, new() { ExpectedRevision = created.Revision }), 409);
        Assert.True((await f.Use(enabled: false).조회Async(created.BatchId)).IsSuccess);
        AssertStatus(await f.Use(role: "기사").조회Async(created.BatchId), 403);
        AssertStatus(await f.Use(role: "기사").준비Async(Request(ids, "unauthorized")), 403);
        AssertStatus(await f.Use(role: "기사").재검증Async(created.BatchId, new() { ExpectedRevision = created.Revision }), 403);
        Assert.Equal("서버관리자전용", typeof(배송료정산배치Controller).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy);
    }

    [Fact]
    public async Task 실행모드와정책판본이바뀐명세는준비상태를재사용하지않는다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        var first = (await f.Use().준비Async(Request(ids))).Value;
        var mode = await f.Use(mode: SsalddelExecutionMode.Operational).재검증Async(first.BatchId, new() { ExpectedRevision = first.Revision });
        Assert.Equal("ExecutionModeMismatch", mode.Value.Lines.Single().HoldReasonCode);
        var policy = await f.Use(policy: "transport-payout-next").재검증Async(first.BatchId, new() { ExpectedRevision = mode.Value.Revision });
        Assert.Equal("PolicyRevisionChanged", policy.Value.Lines.Single().HoldReasonCode);
    }

    [Fact]
    public async Task 클라이언트금액과건별건수와기간을서버에서검사한다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(2);
        var amount = Request(ids); amount.ConfirmedTotalAmount = 1m;
        AssertStatus(await f.Use().준비Async(amount), 409);
        var single = Request(ids); single.ScheduleCode = "Single"; single.PeriodEndUtc = Start.AddDays(1);
        AssertStatus(await f.Use().준비Async(single), 400);
        var period = Request(ids); period.PeriodEndUtc = Start.AddDays(8);
        AssertStatus(await f.Use().준비Async(period), 400);
        Assert.Empty(await f.Db.기사운송대금정산배치.ToArrayAsync());
    }

    [Theory]
    [InlineData(DayOfWeek.Thursday, 8)]
    [InlineData(DayOfWeek.Wednesday, 7)]
    public async Task 늦게승인된채무도운송완료주에귀속하며수목예정시각만계산한다(DayOfWeek payoutDay, int offsetDays)
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        (await f.Db.운송원장.SingleAsync()).도착 = Start.AddDays(6).AddHours(23); // 화요일 23:00 KST
        (await f.Db.기사운송대금지급요청.SingleAsync()).승인일시Utc = Start.AddDays(10); // 다음 주의 지연 승인
        await f.Db.SaveChangesAsync();
        var result = await f.Use(payoutDay: payoutDay).준비Async(Request(ids));
        Assert.True(result.IsSuccess, Errors(result)); Assert.Equal("Prepared", result.Value.StatusCode);
        Assert.Equal(Start, result.Value.PeriodStartUtc);
        Assert.Equal(Start.AddDays(offsetDays).AddHours(9), result.Value.PayoutScheduledAtUtc);
        Assert.False(result.Value.IsActualTransferCompleted); Assert.False(result.Value.BankCalendarApplied);
    }

    [Fact]
    public async Task 같은기사라도수요일경계를넘긴완료건은다른업무주로분리한다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(2);
        var otherTransportId = (await f.Db.기사운송대금지급요청.SingleAsync(x => x.Id == ids[1])).운송Id;
        (await f.Db.운송원장.SingleAsync(x => x.Id == otherTransportId)).도착 = Start.AddDays(7);
        await f.Db.SaveChangesAsync();
        AssertStatus(await f.Use().준비Async(Request(ids)), 409);
        Assert.Empty(await f.Db.기사운송대금정산배치.ToArrayAsync());
    }

    [Fact]
    public async Task 주간기간은한국수요일자정부터정확히칠일이어야한다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        var utcMidnight = Request(ids); utcMidnight.PeriodStartUtc = Start.AddHours(9); utcMidnight.PeriodEndUtc = Start.AddDays(7).AddHours(9);
        AssertStatus(await f.Use().준비Async(utcMidnight), 400);
        var wrongDay = Request(ids); wrongDay.PeriodStartUtc = Start.AddDays(-1); wrongDay.PeriodEndUtc = Start.AddDays(6);
        AssertStatus(await f.Use().준비Async(wrongDay), 400);
        var shortWeek = Request(ids); shortWeek.PeriodEndUtc = Start.AddDays(6);
        AssertStatus(await f.Use().준비Async(shortWeek), 400);
    }

    [Fact]
    public async Task 예정지급요일변경은기존명세의날짜를조용히바꾸지않고보류한다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        var first = (await f.Use().준비Async(Request(ids))).Value;
        var changed = await f.Use(payoutDay: DayOfWeek.Wednesday).재검증Async(first.BatchId, new() { ExpectedRevision = first.Revision });
        Assert.True(changed.IsSuccess, Errors(changed)); Assert.Equal("Held", changed.Value.StatusCode);
        Assert.Equal("PayoutScheduleChanged", changed.Value.Lines.Single().HoldReasonCode);
        Assert.Equal(first.PayoutScheduledAtUtc, changed.Value.PayoutScheduledAtUtc);
    }

    [Fact]
    public async Task SQLite별도연결에서같은채무를동시에편입해도한명세만성공한다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Attempt(string key)
        {
            await using var context = f.OpenDb();
            await start.Task;
            return (await f.Use(context).준비Async(Request(ids, key))).IsSuccess;
        }
        var a = Task.Run(() => Attempt("concurrent-a")); var b = Task.Run(() => Attempt("concurrent-b")); start.SetResult();
        var outcomes = await Task.WhenAll(a, b);
        Assert.Equal(1, outcomes.Count(x => x));
        Assert.Equal(1, await f.Db.기사운송대금정산배치.CountAsync());
        Assert.Equal(1, await f.Db.기사운송대금정산배치행.CountAsync());
        // DB 고유 제약도 직접 검사하여 프로세스 안의 조회 선검사에만 의존하지 않음을 확인합니다.
        await using var duplicateDb = f.OpenDb();
        var existing = await duplicateDb.기사운송대금정산배치행.AsNoTracking().SingleAsync();
        duplicateDb.기사운송대금정산배치행.Add(new()
        {
            정산배치Id = existing.정산배치Id, 지급요청Id = existing.지급요청Id,
            운송Id = existing.운송Id, 의뢰Id = existing.의뢰Id, 동결금액 = existing.동결금액
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
    }

    private static 배송료정산배치준비요청 Request(long[] ids, string key = "batch-a") => new()
    {
        DriverId = "driver", PeriodStartUtc = Start, PeriodEndUtc = Start.AddDays(7), PayoutRequestIds = ids,
        ConfirmedTotalAmount = ids.Length == 1 ? 42000m : 85000m, PolicyRevision = "transport-payout-r73", IdempotencyKey = key
    };
    private static void AssertStatus(FluentResults.Result<배송료정산배치응답> result, int status)
    {
        Assert.True(result.IsFailed); Assert.Equal(status, result.Errors.Single().Metadata["StatusCode"]);
    }
    private static string Errors(FluentResults.Result<배송료정산배치응답> result) => string.Join(";", result.Errors.Select(x => x.Message));
    private sealed record Current(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed record ExecutionPolicy(SsalddelExecutionMode Mode) : ISsalddelExecutionModePolicy
    {
        public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => Mode == SsalddelExecutionMode.Operational;
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Fixture(SqliteConnection keeper, string connectionString, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public SsalddelContext OpenDb() => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connectionString).Options, new Encryption());
        public 배송료정산배치UseCase Use(SsalddelContext? context = null, bool enabled = true, string role = 역할명.서버관리자,
            SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation, string policy = "transport-payout-r73", DayOfWeek payoutDay = DayOfWeek.Thursday)
            => new(context ?? Db, new Current("actor", role), new ExecutionPolicy(mode),
                Options.Create(new DriverPayoutBatchOptions { Enabled = enabled, PolicyRevision = policy, PayoutDay = payoutDay }), TimeProvider.System);
        public static async Task<Fixture> Create()
        {
            var connectionString = "Data Source=payout-r73-" + Guid.NewGuid().ToString("N") + ";Mode=Memory;Cache=Shared;Default Timeout=1";
            var keeper = new SqliteConnection(connectionString); await keeper.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connectionString).Options, new Encryption());
            await db.Database.EnsureCreatedAsync(); return new(keeper, connectionString, db);
        }
        public async Task<long[]> Seed(int count)
        {
            Db.Set<기사정산계좌>().Add(new 기사정산계좌
            {
                기사Id = "driver", 국가코드 = "KR", 은행명 = "fixture-bank", 예금주명 = "fixture-driver", 계좌번호 = "fixture-account",
                확인상태 = 기사정산계좌확인상태.확인완료
            });
            var payouts = new List<기사운송대금지급요청>();
            for (var i = 0; i < count; i++)
            {
                var id = "request-" + i;
                var transport = new 운송원장
                {
                    의뢰Id = id, 운송번호 = id, 확정기사Id = "driver", 기사_운송자 = "driver", 상태 = "완료", 도착 = Start.AddHours(2 + i)
                };
                Db.운송원장.Add(transport);
                Db.화주운송의뢰.Add(new 화주운송의뢰
                {
                    의뢰Id = id, 화주Id = "shipper", 결제상태 = 상태값.결제상태.결제완료,
                    정산상태 = 운임정산상태.입금확인완료.ToString(), 정산시점 = 정산시점.운송완료후정산.ToString(), 결제예정금액 = 65000
                });
                Db.운임구성.Add(new 운임구성 { 의뢰Id = id, 최종운임 = 65000m, 기사지급예정운임 = 42000m + 1000m * i, UpdatedAt = Start });
                await Db.SaveChangesAsync();
                var payout = new 기사운송대금지급요청
                {
                    운송Id = transport.Id, 운송번호 = id, 의뢰Id = id, 기사Id = "driver", 지급예정금액 = 42000m + 1000m * i,
                    멱등키 = "approved-" + i, 실행모드코드 = "Simulation", 승인일시Utc = Start.AddHours(3 + i), 승인관리자Id = "admin"
                };
                Db.기사운송대금지급요청.Add(payout); payouts.Add(payout);
            }
            await Db.SaveChangesAsync(); return payouts.Select(x => x.Id).ToArray();
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await keeper.DisposeAsync(); }
    }
}
