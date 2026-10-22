using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Payment;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Application.Shipper.Payment.Handlers;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Options;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Payment;

public sealed class CargoPrepaymentReceptionR16Tests
{
    [Theory]
    [InlineData("결제완료")]
    [InlineData("결제취소")]
    [InlineData("환불됨")]
    public void New_request_cannot_claim_a_payment_result(string state)
    {
        var source = Source();
        var command = 화주운송의뢰일괄등록지원.To의뢰생성Command(new()
        {
            화물종류 = source.화물종류, 픽업도로명주소 = source.픽업_도로명주소,
            하차도로명주소 = source.하차_도로명주소, 픽업연락처전화번호 = source.픽업_연락처_전화번호,
            픽업시간창시작일시 = source.픽업_시간창_시작일시, 픽업시간창종료일시 = source.픽업_시간창_종료일시
        }, new());

        Assert.Null(화주운송의뢰생성입력Policy.입력오류(command with { 결제상태 = null }));
        Assert.Null(화주운송의뢰생성입력Policy.입력오류(command with { 결제상태 = "결제대기" }));
        Assert.Contains("결제 완료는 서버", 화주운송의뢰생성입력Policy.입력오류(command with { 결제상태 = state }));
    }

    [Theory]
    [InlineData("결제대기")]
    [InlineData("결제취소")]
    [InlineData("환불됨")]
    public async Task Mongo_first_unsecured_prepayment_keeps_request_without_dispatch_queue(string paymentState)
    {
        await using var db = Context();
        var source = Source(); source.결제상태 = paymentState;
        var ledger = Ledger(source);
        var snapshot = 운송원장업무투영Snapshot.생성(ledger)!;
        Assert.True(snapshot.IsTransportRequestComplete);
        Assert.False(snapshot.CanCreateCoordinationTransport);

        await Projector(db).동기화Async(ledger);

        var stored = await db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(source.의뢰Id, stored.의뢰Id);
        Assert.Equal(source.픽업_도로명주소, stored.픽업_도로명주소);
        Assert.Equal(paymentState, stored.결제상태);
        Assert.Equal("선결제", stored.정산시점);
        Assert.Empty(await db.운송원장.ToArrayAsync());
    }

    [Fact]
    public async Task Persisted_mirror_cannot_recreate_queue_before_collection_or_trust_its_old_paid_flag()
    {
        await using var db = Context(); var source = Source();
        db.화주운송의뢰.Add(source); await db.SaveChangesAsync();
        var stalePaidCopy = Source(); stalePaidCopy.결제상태 = "결제완료";

        await Projector(db).동기화Async(Ledger(stalePaidCopy, persisted: true));

        Assert.Empty(await db.운송원장.ToArrayAsync());
        Assert.Equal("결제대기", (await db.화주운송의뢰.AsNoTracking().SingleAsync()).결제상태);
    }

    [Theory]
    [InlineData("선결제", "결제완료")]
    [InlineData("운송완료후정산", "결제대기")]
    [InlineData("현장지급", "결제대기")]
    public async Task Paid_prepayment_and_existing_other_settlement_projection_remain_supported(string timing, string payment)
    {
        await using var db = Context(); var source = Source();
        source.정산시점 = timing; source.결제상태 = payment;

        await Projector(db).동기화Async(Ledger(source));

        Assert.Single(await db.화주운송의뢰.ToArrayAsync());
        Assert.Equal(source.의뢰Id, (await db.운송원장.SingleAsync()).의뢰Id);
    }

    [Theory]
    [InlineData("미확인", "결제대기", false)]
    [InlineData("운송완료후정산", "결제대기", false)]
    [InlineData("현장지급", "결제대기", false)]
    [InlineData("선결제", "결제완료", false)]
    [InlineData("선결제", "결제대기", true)]
    public void Existing_queue_cannot_be_recommended_while_prepayment_is_unsecured(string timing, string payment, bool blocked)
    {
        var source = Source(); source.정산시점 = timing; source.결제상태 = payment;
        var queue = Queue(source);

        var reasons = 국내화물배차후보금지정책.운송의뢰후보금지사유(queue, source, DateTime.UtcNow);

        Assert.Equal(blocked, reasons.Count != 0);
    }

    [Theory]
    [InlineData(false, SsalddelExecutionMode.Simulation)]
    [InlineData(true, SsalddelExecutionMode.Operational)]
    public async Task Fake_before_loading_requires_explicit_option_and_simulation(bool enabled, SsalddelExecutionMode mode)
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();

        var result = await Fake(db, enabled, mode).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default);

        Assert.True(result.IsFailed);
        Assert.Empty(await db.결제.ToArrayAsync());
        Assert.Empty(await db.결제승인완료Outbox.ToArrayAsync());
        Assert.Empty(await db.운송원장.ToArrayAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9999)]
    [InlineData(10001)]
    public async Task Fake_before_loading_rejects_any_amount_not_equal_to_current_server_amount(int amount)
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();

        var result = await Fake(db, true).Handle(new("r16-request", amount, "카드", null, "stable-payment"), default);

        Assert.True(result.IsFailed);
        Assert.Empty(await db.결제.ToArrayAsync());
        Assert.Equal("결제대기", (await db.화주운송의뢰.SingleAsync()).결제상태);
    }

    [Fact]
    public async Task Collected_fake_payment_and_outbox_continue_same_prepared_request_once_without_settling_delivery()
    {
        await using var db = Context();
        await Projector(db).동기화Async(Ledger(Source()));
        Assert.Empty(await db.운송원장.ToArrayAsync());

        var first = await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default);
        Assert.True(first.IsSuccess);
        Assert.Empty(await db.운송원장.ToArrayAsync());
        var outbox = await db.결제승인완료Outbox.SingleAsync();
        var notification = JsonSerializer.Deserialize<결제승인완료Event>(outbox.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var handler = Approved(db);
        await handler.Handle(notification, default);
        await handler.Handle(notification, default);

        Assert.Equal("r16-request", (await db.운송원장.SingleAsync()).의뢰Id);
        var stored = await db.화주운송의뢰.SingleAsync();
        Assert.Equal("매칭중", stored.배차상태);
        Assert.Equal("결제완료", stored.결제상태);
        Assert.Equal("결제완료", stored.정산상태);
        Assert.Null((await db.운송원장.SingleAsync()).확정기사Id);
        Assert.Null((await db.운송원장.SingleAsync()).도착);
        Assert.Null(stored.인수증등록일시);
        Assert.Single(await db.결제.ToArrayAsync());
        Assert.Single(await db.결제승인완료Outbox.ToArrayAsync());

        var replay = await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default);
        Assert.True(replay.IsSuccess); Assert.True(replay.Value.이미완료됨);
        Assert.Equal(first.Value.결제Id, replay.Value.결제Id);
        Assert.Single(await db.운송원장.ToArrayAsync());
    }

    [Fact]
    public async Task Non_owner_cannot_recover_completed_fake_receipt_or_recreate_its_outbox()
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();
        Assert.True((await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default)).IsSuccess);
        db.결제승인완료Outbox.Remove(await db.결제승인완료Outbox.SingleAsync()); await db.SaveChangesAsync();

        var result = await Fake(db, true, user: "another-owner").Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default);

        Assert.True(result.IsFailed); Assert.Empty(await db.결제승인완료Outbox.ToArrayAsync());
        Assert.Single(await db.결제.ToArrayAsync());
    }

    [Fact]
    public async Task Completed_fake_replay_preserves_original_amount_and_refuses_a_different_amount()
    {
        await using var db = Context(); var source = Source(); db.화주운송의뢰.Add(source); await db.SaveChangesAsync();
        var first = await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default);
        source.최종운임 = 12000; source.결제예정금액 = 12000; await db.SaveChangesAsync();

        var different = await Fake(db, true).Handle(new("r16-request", 12000, "카드", null, "stable-payment"), default);
        var original = await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default);

        Assert.True(different.IsFailed); Assert.True(original.IsSuccess);
        Assert.Equal(first.Value.결제Id, original.Value.결제Id);
        Assert.Equal(10000, (await db.결제.SingleAsync()).결제금액);
        Assert.Single(await db.결제승인완료Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task Existing_post_loading_fake_flow_still_works_without_prepayment_option()
    {
        await using var db = Context(); var source = Source();
        source.배차상태 = "상차완료"; source.정산시점 = "운송완료후정산";
        db.화주운송의뢰.Add(source); await db.SaveChangesAsync();

        var result = await Fake(db, false).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("상차완료", source.배차상태);
        Assert.Single(await db.결제승인완료Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task Final_fare_fallback_is_bound_before_fake_outbox_and_survives_its_current_quote_check()
    {
        await using var db = Context(); var source = Source(); source.결제예정금액 = null;
        db.화주운송의뢰.Add(source); await db.SaveChangesAsync();
        Assert.True((await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default)).IsSuccess);
        var payment = await db.결제.SingleAsync();
        Assert.True(배송료결제준비근거Policy.현재근거검증(payment, source).IsSuccess);

        await Approved(db).Handle(Notification(payment), default);

        Assert.Single(await db.운송원장.ToArrayAsync());
    }

    [Fact]
    public async Task Delayed_fake_outbox_cannot_dispatch_a_changed_quote()
    {
        await using var db = Context(); var source = Source(); db.화주운송의뢰.Add(source); await db.SaveChangesAsync();
        Assert.True((await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default)).IsSuccess);
        source.결제예정금액 = 12000; source.최종운임 = 12000; await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Approved(db).Handle(Notification(db.결제.Single()), default));

        Assert.Empty(await db.운송원장.ToArrayAsync());
        Assert.Equal(10000, (await db.결제.SingleAsync()).결제금액);
        Assert.Equal("미시작", source.배차상태);
    }

    [Fact]
    public async Task Cancelled_request_keeps_its_paid_fact_but_cannot_dispatch_on_delayed_outbox()
    {
        await using var db = Context(); var source = Source(); db.화주운송의뢰.Add(source); await db.SaveChangesAsync();
        Assert.True((await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default)).IsSuccess);
        source.상태 = "취소"; source.배차상태 = "취소"; await db.SaveChangesAsync();

        await Approved(db).Handle(Notification(db.결제.Single()), default);

        Assert.Empty(await db.운송원장.ToArrayAsync());
        Assert.Equal("취소", source.배차상태);
        Assert.Equal("결제완료", source.결제상태);
    }

    [Fact]
    public async Task New_fake_prepayment_outbox_never_creates_a_queue_in_operational_mode()
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();
        Assert.True((await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default)).IsSuccess);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Approved(db, SsalddelExecutionMode.Operational)
            .Handle(Notification(db.결제.Single()), default));

        Assert.Empty(await db.운송원장.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owner_prepayment_query_reports_current_option_and_initial_state_without_business_writes(bool enabled)
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();

        var result = await Prepayment(db, enabled).Handle(new("r16-request"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(10000, result.Value.Amount); Assert.Equal(enabled, result.Value.Enabled);
        Assert.Equal(enabled, result.Value.CanPay); Assert.Equal(enabled, result.Value.SimulationAvailable);
        Assert.False(result.Value.PaymentConfirmed); Assert.Equal("미시작", result.Value.DispatchStatus);
        Assert.Empty(await db.결제.ToArrayAsync());
        Assert.Empty(await db.운송원장.ToArrayAsync());
    }

    [Fact]
    public async Task Prepayment_query_never_exposes_another_owners_payment_state()
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();
        Assert.True((await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default)).IsSuccess);

        var result = await Prepayment(db, true, user: "another-owner").Handle(new("r16-request"), default);

        Assert.True(result.IsFailed);
        Assert.Single(await db.결제.ToArrayAsync());
        Assert.Single(await db.결제승인완료Outbox.ToArrayAsync());
    }

    [Theory]
    [InlineData("source-only")]
    [InlineData("payment-only")]
    [InlineData("foreign-target")]
    [InlineData("foreign-shipper")]
    [InlineData("cancelled")]
    public async Task Prepayment_query_requires_matching_uncancelled_payment_and_source_confirmation(string scenario)
    {
        await using var db = Context(); var source = Source(); db.화주운송의뢰.Add(source); await db.SaveChangesAsync();
        Assert.True((await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default)).IsSuccess);
        var payment = await db.결제.SingleAsync();
        switch (scenario)
        {
            case "source-only": db.결제.Remove(payment); break;
            case "payment-only": source.결제상태 = "결제대기"; break;
            case "foreign-target": payment.대상Id = "another-request"; break;
            case "foreign-shipper": payment.화주Id = "another-owner"; break;
            case "cancelled": payment.취소일시 = DateTime.UtcNow; break;
        }
        await db.SaveChangesAsync();

        var result = await Prepayment(db, true).Handle(new("r16-request"), default);

        Assert.True(result.IsSuccess); Assert.False(result.Value.PaymentConfirmed);
        Assert.True(result.Value.ResultCheckRequired);
        Assert.False(result.Value.CanPay);
        Assert.Empty(await db.운송원장.ToArrayAsync());
    }

    [Fact]
    public async Task Prepayment_query_shows_confirmed_fake_fact_without_claiming_dispatch_or_delivery_settlement()
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();
        Assert.True((await Fake(db, true).Handle(new("r16-request", 10000, "카드", null, "stable-payment"), default)).IsSuccess);

        var result = await Prepayment(db, true).Handle(new("r16-request"), default);

        Assert.True(result.IsSuccess); Assert.True(result.Value.PaymentConfirmed); Assert.True(result.Value.SimulationPayment);
        Assert.False(result.Value.CanPay); Assert.Equal("미시작", result.Value.DispatchStatus);
        Assert.Equal("결제완료", (await db.화주운송의뢰.SingleAsync()).정산상태);
        Assert.Empty(await db.운송원장.ToArrayAsync());
        Assert.Single(await db.결제.ToArrayAsync()); Assert.Single(await db.결제승인완료Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task Operational_development_query_does_not_offer_before_loading_fake_payment()
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();

        var result = await Prepayment(db, true, SsalddelExecutionMode.Operational).Handle(new("r16-request"), default);

        Assert.True(result.IsSuccess); Assert.False(result.Value.SimulationAvailable);
        Assert.Empty(await db.결제.ToArrayAsync());
    }

    private static 화주운송의뢰 Source() => new()
    {
        의뢰Id = "r16-request", 화주Id = "r16-owner", 주문자UserId = "r16-owner",
        화물종류 = "합성 상자", 차량종류 = "1톤 카고", 화물수량 = 1, 화물중량Kg = 5,
        화물길이Mm = 500, 화물폭Mm = 400, 화물높이Mm = 300,
        상태 = "생성됨", 배차상태 = "미시작", 결제상태 = "결제대기", 정산상태 = "결제대기",
        정산시점 = "선결제", 최종운임 = 10000, 결제예정금액 = 10000,
        픽업_도로명주소 = "합성 상차지", 픽업_연락처_전화번호 = "010-0000-0001",
        픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_도로명주소 = "합성 하차지",
        하차_위도 = 37.6m, 하차_경도 = 127.1m,
        픽업_시간창_시작일시 = new(2026, 10, 22, 1, 0, 0, DateTimeKind.Utc),
        픽업_시간창_종료일시 = new(2026, 10, 22, 2, 0, 0, DateTimeKind.Utc)
    };

    private static 운송원장 Queue(화주운송의뢰 source) => new()
    {
        의뢰Id = source.의뢰Id, 상태 = 상태값.배차대기상태.대기,
        배차업무유형 = 상태값.배차업무유형.용달운송,
        배차큐단계 = 상태값.배차큐단계.계획배차, 배차노출상태 = 상태값.배차노출상태.계획대기,
        픽업_위도 = source.픽업_위도, 픽업_경도 = source.픽업_경도,
        하차_위도 = source.하차_위도, 하차_경도 = source.하차_경도
    };

    private static 커뮤니티원장Dto Ledger(화주운송의뢰 source, bool persisted = false)
    {
        var save = 운송원장Mongo동기화Builder.저장요청생성(source, null);
        if (persisted) 운송원장RdbSnapshotMarker.Attach(save, source.의뢰Id);
        return new()
        {
            원장Id = save.원장Id!, 원장템플릿Key = save.원장템플릿Key, 상태 = save.상태!, 현재단계Key = save.현재단계Key,
            블록목록 = save.블록목록, 참여자목록 = save.참여자목록, 외부참조 = save.외부참조, 확장속성 = save.확장속성
        };
    }

    private static 운송원장업무투영Handler Projector(SsalddelContext db)
        => new(db, NullLogger<운송원장업무투영Handler>.Instance);

    private static 페이크결제승인CommandHandler Fake(SsalddelContext db, bool enabled,
        SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation, string user = "r16-owner")
        => new(db, new Actor(user), new Host(), new Execution(mode), Options.Create(new TransportPrepaymentOptions { Enabled = enabled }));

    private static 화주선결제조회QueryHandler Prepayment(SsalddelContext db, bool enabled,
        SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation, string user = "r16-owner")
        => new(db, new Actor(user), Options.Create(new TransportPrepaymentOptions { Enabled = enabled }), new Host(), new Execution(mode));

    private static 결제승인완료Event Notification(결제 payment)
        => new(payment.Id, payment.결제Id, payment.결제대상유형, payment.대상Id, payment.결제제공자,
            payment.결제금액, payment.통화, payment.승인일시!.Value);

    private static 용달운송의뢰결제승인완료EventHandler Approved(SsalddelContext db,
        SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation)
        => new(db, new 운송의뢰배차대기Service(db, new 운송의뢰배차원천분류Service(),
                new 운송원장배달권연결Service(new 원장배달권투영Service(db)),
                new InMemory음식배달권실행공간Store(), new InMemory국내화물배달권실행공간Store()),
            new Sync(), NullLogger<용달운송의뢰결제승인완료EventHandler>.Instance,
            Options.Create(new TransportPrepaymentOptions { Enabled = true }), new Execution(mode));

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("prepayment-r16-" + Guid.NewGuid())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, new Encryption());

    private sealed record Actor(string? UserId) : ICurrentUserAccessor { public string? Role => "화주"; }
    private sealed record Execution(SsalddelExecutionMode Value) : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode => Value;
        public bool IsSimulation => Value == SsalddelExecutionMode.Simulation;
        public bool IsOperational => Value == SsalddelExecutionMode.Operational;
    }
    private sealed class Host : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ssalddel.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Sync : I운송원장Mongo동기화Service
    {
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 source, string by, CancellationToken ct = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 transport, string by, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
