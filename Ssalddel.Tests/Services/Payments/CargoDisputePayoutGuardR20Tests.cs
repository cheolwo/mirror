using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Admin.Settlement;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Settlement;
using Ssalddel.Contracts.Admin.Settlement;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Payments;

public sealed class CargoDisputePayoutGuardR20Tests
{
    private static readonly DateTime Start = new(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("transaction-dispute", "received", true)]
    [InlineData("transaction-dispute", "reviewing", true)]
    [InlineData("transaction-dispute", "reopened", true)]
    [InlineData("transaction-dispute", "result-prepared", true)]
    [InlineData("transaction-dispute", "unknown", true)]
    [InlineData("transaction-dispute", "closed", false)]
    [InlineData("transaction-dispute", "denied", false)]
    [InlineData("privacy-rights", "received", false)]
    [InlineData("privacy-incident", "reviewing", false)]
    [InlineData("inquiry", "received", false)]
    public async Task Only_unresolved_formal_disputes_hold_payment_and_preservation_is_separate(
        string kind, string status, bool held)
    {
        var store = new DisputeStore();
        store.Rows.Add(new() { Kind = kind, StatusCode = status, SourceKind = 보호지원출처Codes.화물의뢰,
            SourceId = "request-a", SourceHoldActive = true,
            RetentionIntent = new(Guid.NewGuid(), "digest", "actor", "hold-record", "basis", Start.AddDays(90), Start) });

        var result = await new 보호지원화물지급보류Query(store).조회Async("request-a");

        Assert.Equal(held, result.Hold);
        Assert.Equal(held ? 화물분쟁지급보류판정.Open : 화물분쟁지급보류판정.Clear, result);
        Assert.Equal(0, store.PreservationReads);
    }

    [Theory]
    [InlineData("cargo-request", "different-request", false)]
    [InlineData("food-order", "request-a", false)]
    [InlineData("neighborhood-delivery", "request-a", true)]
    public async Task Dispute_query_uses_exact_existing_source_kind_and_request_pair(string kind, string id, bool held)
    {
        var store = new DisputeStore();
        store.Rows.Add(new() { Kind = 보호지원종류Codes.분쟁, SourceKind = kind, SourceId = id });
        var result = await new 보호지원화물지급보류Query(store).조회Async("request-a");
        Assert.Equal(held, result.Hold);
        Assert.All(store.Reads, read => Assert.Equal("request-a", read.Id));
        Assert.All(store.Reads, read => Assert.Contains(read.Kind, new[] { 보호지원출처Codes.화물의뢰, 보호지원출처Codes.생활배송 }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_source_index_or_store_failure_is_unknown_and_never_clear(bool fail)
    {
        var store = new DisputeStore { Unknown = !fail, Failure = fail ? new InvalidDataException("source binding unavailable") : null };
        var result = await new 보호지원화물지급보류Query(store).조회Async("request-a");
        Assert.Equal(화물분쟁지급보류판정.Unknown, result);
    }

    [Fact]
    public async Task Missing_reader_is_fail_closed_and_cancellation_is_not_swallowed()
    {
        Assert.Equal(화물분쟁지급보류판정.Unknown, await 보호지원화물지급보류Query.확인Async(null, "request-a"));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new 보호지원화물지급보류Query(new DisputeStore())
            .조회Async("request-a", cancelled.Token));
    }

    [Theory]
    [InlineData("open")]
    [InlineData("unknown")]
    [InlineData("failure")]
    [InlineData("unconfigured")]
    public async Task Ready_transport_is_excluded_from_preparable_total_when_formal_dispute_is_open_or_unknown(string state)
    {
        await using var db = Context(); await SeedReadyAsync(db);
        var reader = Reader(state);

        var result = await new 기사지급준비UseCase(db, reader).월별조회Async("driver-a", 2026, 10);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.False(item.IsReadyForPayoutPreparation);
        Assert.Equal(state == "open" ? 화물분쟁지급보류판정.Open.Code : 화물분쟁지급보류판정.Unknown.Code, item.ReadinessCode);
        Assert.Equal(42000m, item.ExpectedPayoutAmount);
        Assert.Equal(42000m, result.Value.ExpectedPayoutTotal);
        Assert.Equal(0m, result.Value.ReadyForPayoutPreparationTotal);
        Assert.Empty(db.기사운송대금지급요청); Assert.Empty(db.기사지급Outbox);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("unknown")]
    [InlineData("failure")]
    [InlineData("unconfigured")]
    public async Task Administrator_cannot_approve_payout_while_dispute_check_holds(string state)
    {
        await using var db = Context(); var transport = await SeedReadyAsync(db);
        var result = await Approvals(db, Reader(state)).승인Async(Approval(transport.Id));
        Assert.True(result.IsFailed);
        Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(state == "open" ? 화물분쟁지급보류판정.Open.Code : 화물분쟁지급보류판정.Unknown.Code,
            result.Errors.Single().Metadata["ErrorCode"]);
        Assert.Empty(db.기사운송대금지급요청); Assert.Empty(db.기사지급Outbox);
    }

    [Fact]
    public async Task Approval_replay_after_new_dispute_does_not_reapprove_or_replace_existing_debt()
    {
        await using var db = Context(); var transport = await SeedReadyAsync(db);
        var reader = CargoPayoutDisputeQueryStub.Clear;
        var use = Approvals(db, reader);
        Assert.True((await use.승인Async(Approval(transport.Id))).IsSuccess);
        reader.Result = 화물분쟁지급보류판정.Open;

        var replay = await use.승인Async(Approval(transport.Id));

        Assert.True(replay.IsFailed);
        Assert.Single(db.기사운송대금지급요청); Assert.Single(db.기사지급Outbox);
        Assert.Equal(기사지급요청상태코드.승인됨, (await db.기사운송대금지급요청.SingleAsync()).상태코드);
        Assert.Equal(42000m, (await db.기사운송대금지급요청.SingleAsync()).지급예정금액);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("unknown")]
    [InlineData("failure")]
    [InlineData("unconfigured")]
    public async Task New_dispute_or_unknown_check_after_approval_blocks_gateway_and_keeps_retryable_hold(string state)
    {
        await using var db = Context(); var transport = await SeedReadyAsync(db);
        Assert.True((await Approvals(db, CargoPayoutDisputeQueryStub.Clear).승인Async(Approval(transport.Id))).IsSuccess);
        var gateway = new CountingGateway();

        await Outbox(db, gateway, Reader(state)).대기항목처리Async();

        Assert.Equal(0, gateway.Calls);
        var payout = await db.기사운송대금지급요청.SingleAsync();
        var outbox = await db.기사지급Outbox.SingleAsync();
        Assert.Equal(기사지급요청상태코드.재시도대기, payout.상태코드);
        Assert.Equal(기사지급Outbox상태코드.재시도대기, outbox.처리상태);
        Assert.Equal(state == "open" ? 화물분쟁지급보류판정.Open.Code : 화물분쟁지급보류판정.Unknown.Code, outbox.마지막결과코드);
        Assert.NotNull(outbox.다음시도시각Utc); Assert.Null(payout.Simulation검증일시Utc);
        Assert.Equal(42000m, payout.지급예정금액);
    }

    [Fact]
    public async Task Closing_support_case_does_not_transfer_money_and_next_explicit_outbox_attempt_rechecks()
    {
        await using var db = Context(); var transport = await SeedReadyAsync(db);
        var reader = CargoPayoutDisputeQueryStub.Clear;
        Assert.True((await Approvals(db, reader).승인Async(Approval(transport.Id))).IsSuccess);
        reader.Result = 화물분쟁지급보류판정.Open;
        var gateway = new CountingGateway(); var service = Outbox(db, gateway, reader);
        await service.대기항목처리Async();
        reader.Result = 화물분쟁지급보류판정.Clear;
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(기사지급요청상태코드.재시도대기, (await db.기사운송대금지급요청.SingleAsync()).상태코드);
        var outbox = await db.기사지급Outbox.SingleAsync(); outbox.다음시도시각Utc = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();

        await service.대기항목처리Async();

        Assert.Equal(1, gateway.Calls); Assert.True(reader.Reads >= 3);
        Assert.Equal(기사지급요청상태코드.Simulation검증완료, (await db.기사운송대금지급요청.SingleAsync()).상태코드);
        Assert.Equal("SimulationNoTransfer", outbox.마지막결과코드);
    }

    [Fact]
    public async Task New_SQL_abnormal_incident_after_approval_blocks_gateway_independently_of_Mongo_clear()
    {
        await using var db = Context(); var transport = await SeedReadyAsync(db);
        Assert.True((await Approvals(db, CargoPayoutDisputeQueryStub.Clear).승인Async(Approval(transport.Id))).IsSuccess);
        db.비정상운송사건.Add(new() { 사건StableId = "r20-incident", 운송Id = transport.Id, 운송의뢰Id = "request-a",
            상태Code = 비정상운송사건상태Codes.운영검토대기, 사건유형Code = 비정상운송사건유형Codes.화물훼손 });
        await db.SaveChangesAsync(); var gateway = new CountingGateway();

        await Outbox(db, gateway, CargoPayoutDisputeQueryStub.Clear).대기항목처리Async();

        Assert.Equal(0, gateway.Calls);
        Assert.Equal("AbnormalTransportReviewHold", (await db.기사지급Outbox.SingleAsync()).마지막결과코드);
    }

    [Fact]
    public async Task Unbound_payout_source_cannot_reach_gateway_even_when_dispute_store_is_clear()
    {
        await using var db = Context(); var transport = await SeedReadyAsync(db);
        Assert.True((await Approvals(db, CargoPayoutDisputeQueryStub.Clear).승인Async(Approval(transport.Id))).IsSuccess);
        var payout = await db.기사운송대금지급요청.SingleAsync(); payout.기사Id = "different-driver"; await db.SaveChangesAsync();
        var gateway = new CountingGateway(); var reader = CargoPayoutDisputeQueryStub.Clear;

        await Outbox(db, gateway, reader).대기항목처리Async();

        Assert.Equal(0, gateway.Calls); Assert.Equal(0, reader.Reads);
        Assert.Equal("CargoPayoutSourceUnconfirmed", (await db.기사지급Outbox.SingleAsync()).마지막결과코드);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("unknown")]
    [InlineData("failure")]
    [InlineData("unconfigured")]
    public async Task Weekly_statement_revalidation_holds_existing_debt_without_new_approval_or_gateway(string state)
    {
        await using var db = Context(); var transport = await SeedReadyAsync(db);
        var approved = await Approvals(db, CargoPayoutDisputeQueryStub.Clear).승인Async(Approval(transport.Id));
        Assert.True(approved.IsSuccess);
        var reader = CargoPayoutDisputeQueryStub.Clear;
        var first = await Batches(db, reader).준비Async(new()
        {
            DriverId = "driver-a", PeriodStartUtc = Start, PeriodEndUtc = Start.AddDays(7),
            PayoutRequestIds = [approved.Value.PayoutRequestId], ConfirmedTotalAmount = 42000m,
            PolicyRevision = "transport-payout-r73", IdempotencyKey = "r20-weekly"
        });
        Assert.True(first.IsSuccess); Assert.Equal(1, first.Value.ReadyCount);

        var checkedAgain = await Batches(db, Reader(state)).재검증Async(first.Value.BatchId, new() { ExpectedRevision = first.Value.Revision });

        Assert.True(checkedAgain.IsSuccess); Assert.Equal(0, checkedAgain.Value.ReadyCount); Assert.Equal(1, checkedAgain.Value.HeldCount);
        Assert.Equal(42000m, checkedAgain.Value.ApprovedTotalAmount); Assert.Equal(0m, checkedAgain.Value.ReadyTotalAmount);
        Assert.Equal(state == "open" ? 화물분쟁지급보류판정.Open.Code : 화물분쟁지급보류판정.Unknown.Code,
            Assert.Single(checkedAgain.Value.Lines).HoldReasonCode);
        Assert.False(checkedAgain.Value.IsActualTransferCompleted);
        Assert.Single(db.기사운송대금지급요청); Assert.Single(db.기사지급Outbox);
    }

    private static I화물분쟁지급보류Query? Reader(string state) => state == "unconfigured" ? null : new CargoPayoutDisputeQueryStub
    {
        Result = state == "open" ? 화물분쟁지급보류판정.Open : 화물분쟁지급보류판정.Unknown,
        Failure = state == "failure" ? new InvalidDataException("unavailable") : null
    };
    private static 기사지급승인UseCase Approvals(SsalddelContext db, I화물분쟁지급보류Query? query)
        => new(db, new Current(), new Execution(), TimeProvider.System, query);
    private static 기사지급OutboxService Outbox(SsalddelContext db, CountingGateway gateway, I화물분쟁지급보류Query? query)
        => new(db, gateway, TimeProvider.System, NullLogger<기사지급OutboxService>.Instance, query);
    private static 배송료정산배치UseCase Batches(SsalddelContext db, I화물분쟁지급보류Query? query)
        => new(db, new Current(), new Execution(), Options.Create(new DriverPayoutBatchOptions { Enabled = true }), TimeProvider.System, query);
    private static 기사지급승인요청 Approval(long id) => new()
    {
        TransportId = id, ConfirmedRequestId = "request-a", ConfirmedExpectedPayoutAmount = 42000m,
        CurrencyCode = "KRW", IdempotencyKey = "r20-approval", ApprovalReason = "합성 지급 준비 검증"
    };
    private static async Task<운송원장> SeedReadyAsync(SsalddelContext db)
    {
        db.용달기사.Add(new() { 기사Id = "driver-a" });
        var transport = new 운송원장 { 의뢰Id = "request-a", 운송번호 = "request-a", 확정기사Id = "driver-a",
            기사_운송자 = "driver-a", 상태 = "인수완료", 도착 = Start.AddHours(2) };
        db.운송원장.Add(transport);
        db.화주운송의뢰.Add(new() { 의뢰Id = "request-a", 화주Id = "shipper-a", 결제상태 = 상태값.결제상태.결제완료,
            정산상태 = 운임정산상태.입금확인완료.ToString(), 정산시점 = 정산시점.운송완료후정산.ToString() });
        db.운임구성.Add(new() { 의뢰Id = "request-a", 최종운임 = 65000m, 기사지급예정운임 = 42000m });
        db.Set<기사정산계좌>().Add(new() { 기사Id = "driver-a", 국가코드 = "KR", 은행명 = "test", 예금주명 = "test", 계좌번호 = "test",
            확인상태 = 기사정산계좌확인상태.확인완료 });
        await db.SaveChangesAsync(); return transport;
    }
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("cargo-dispute-r20-" + Guid.NewGuid().ToString("N")).Options, new Encryption());
    private sealed record Current : ICurrentUserAccessor { public string UserId => "admin"; public string Role => 역할명.서버관리자; }
    private sealed record Execution : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation; public bool IsSimulation => true; public bool IsOperational => false;
    }
    private sealed class CountingGateway : I기사지급Gateway
    {
        public int Calls { get; private set; }
        public Task<기사지급Gateway결과> 처리Async(기사운송대금지급요청 request, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(기사지급Gateway결과.성공("SimulationNoTransfer", "송금 없는 검증")); }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class DisputeStore : I보호지원Store
    {
        public List<보호지원Record> Rows { get; } = [];
        public List<(string Kind, string Id)> Reads { get; } = [];
        public bool Unknown { get; init; } public Exception? Failure { get; init; } public int PreservationReads { get; private set; }
        public Task<bool?> HasOpenCaseAsync(string kind, string id, CancellationToken ct = default)
        { PreservationReads++; throw new InvalidOperationException("Preservation is not a payout decision."); }
        public Task<bool?> HasUnresolvedDisputeAsync(string kind, string id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); Reads.Add((kind, id));
            if (Failure is not null) throw Failure;
            return Task.FromResult<bool?>(Unknown ? null : Rows.Any(x => x.SourceKind == kind && x.SourceId == id
                && 보호지원화물지급보류Query.IsUnresolvedDispute(x)));
        }
        public Task<보호지원Record?> 조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 생성Async(보호지원Record row, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 교체Async(보호지원Record row, long revision, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 목록Async(string? user, string? kind, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime now, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 파기Async(보호지원Record row, DateTime now, CancellationToken ct = default) => throw new NotSupportedException();
    }
}

internal sealed class CargoPayoutDisputeQueryStub : I화물분쟁지급보류Query
{
    public static CargoPayoutDisputeQueryStub Clear => new();
    public 화물분쟁지급보류판정 Result { get; set; } = 화물분쟁지급보류판정.Clear;
    public Exception? Failure { get; init; }
    public int Reads { get; private set; }
    public Task<화물분쟁지급보류판정> 조회Async(string requestId, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); Reads++; if (Failure is not null) throw Failure; return Task.FromResult(Result); }
}
