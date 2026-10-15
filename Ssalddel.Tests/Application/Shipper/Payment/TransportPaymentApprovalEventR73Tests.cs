using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Application.Shipper.Payment.Handlers;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Options;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Shipper.Payment;

public sealed class TransportPaymentApprovalEventR73Tests
{
    [Fact]
    public async Task SecuredNewPrepaymentEntersQueueOnce_AndDelayedReplayKeepsProgress()
    {
        await using var db = TransportPaymentPreparationR73Tests.Context();
        var source = TransportPaymentPreparationR73Tests.Source();
        var payment = TransportPaymentPreparationR73Tests.Snapshot(source);
        Complete(payment); db.화주운송의뢰.Add(source); db.결제.Add(payment); await db.SaveChangesAsync();
        var queue = new Queue(); var sync = new Sync(); var handler = Handler(db, queue, sync);
        await handler.Handle(Event(payment), default);
        Assert.Equal("매칭중", source.배차상태); Assert.Equal("결제완료", source.결제상태); Assert.Equal(1, queue.Calls);
        source.배차상태 = "운송중"; source.정산상태 = "정산대기"; await db.SaveChangesAsync();
        await handler.Handle(Event(payment), default);
        Assert.Equal("운송중", source.배차상태); Assert.Equal("정산대기", source.정산상태);
        Assert.Equal(1, queue.Calls); Assert.Equal(2, sync.Calls);
    }

    [Theory]
    [InlineData("배차확정")]
    [InlineData("상차완료")]
    [InlineData("운송중")]
    [InlineData("하차완료")]
    [InlineData("인수완료")]
    [InlineData("취소")]
    public async Task ApprovalNeverRewindsExistingWorkState(string state)
    {
        await using var db = TransportPaymentPreparationR73Tests.Context();
        var source = TransportPaymentPreparationR73Tests.Source(); source.배차상태 = state; source.정산상태 = "정산완료";
        var payment = TransportPaymentPreparationR73Tests.Snapshot(source); Complete(payment);
        db.화주운송의뢰.Add(source); db.결제.Add(payment); await db.SaveChangesAsync();
        var queue = new Queue();
        await Handler(db, queue, new Sync()).Handle(Event(payment), default);
        Assert.Equal(state, source.배차상태); Assert.Equal("정산완료", source.정산상태); Assert.Equal(0, queue.Calls);
    }

    [Fact]
    public async Task PostpayRetainsPickupStateAndDefaultOffCannotCreateNewQueue()
    {
        await using var db = TransportPaymentPreparationR73Tests.Context();
        var source = TransportPaymentPreparationR73Tests.Source(); source.정산시점 = "운송완료후정산";
        source.배차상태 = "상차완료"; source.정산상태 = "청구대기";
        var payment = TransportPaymentPreparationR73Tests.Snapshot(source); Complete(payment);
        db.화주운송의뢰.Add(source); db.결제.Add(payment); await db.SaveChangesAsync();
        var queue = new Queue(); var handler = Handler(db, queue, new Sync(), enabled: false);
        await handler.Handle(Event(payment), default);
        Assert.Equal("상차완료", source.배차상태); Assert.Equal("결제완료", source.정산상태); Assert.Equal(0, queue.Calls);
    }

    [Fact]
    public async Task ExistingTransportPreventsStaleInitialProjectionFromReenteringQueue()
    {
        await using var db = TransportPaymentPreparationR73Tests.Context();
        var source = TransportPaymentPreparationR73Tests.Source();
        var payment = TransportPaymentPreparationR73Tests.Snapshot(source); Complete(payment);
        db.화주운송의뢰.Add(source); db.결제.Add(payment);
        db.운송원장.Add(new 운송원장 { 의뢰Id = "request", 상태 = "운송중" }); await db.SaveChangesAsync();
        var queue = new Queue();
        await Handler(db, queue, new Sync()).Handle(Event(payment), default);
        Assert.Equal("미시작", source.배차상태); Assert.Equal("운송중", (await db.운송원장.SingleAsync()).상태);
        Assert.Equal(0, queue.Calls);
    }

    [Theory]
    [InlineData("event-amount")]
    [InlineData("pending-payment")]
    [InlineData("changed-fare")]
    [InlineData("missing-evidence")]
    public async Task UnconfirmedOrChangedFinancialEvidenceCannotCreateDispatch(string change)
    {
        await using var db = TransportPaymentPreparationR73Tests.Context();
        var source = TransportPaymentPreparationR73Tests.Source();
        var payment = TransportPaymentPreparationR73Tests.Snapshot(source); Complete(payment);
        db.화주운송의뢰.Add(source); db.결제.Add(payment); await db.SaveChangesAsync();
        if (change == "pending-payment") payment.공통결제상태 = 결제공통정의.결제상태.요청생성;
        if (change == "changed-fare") source.결제예정금액 = 11000;
        if (change == "missing-evidence") payment.준비근거Json = null;
        await db.SaveChangesAsync(); var message = Event(payment);
        if (change == "event-amount") message = message with { 결제금액 = 1 };
        var queue = new Queue();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(db, queue, new Sync()).Handle(message, default));
        Assert.Equal("미시작", source.배차상태); Assert.Equal(0, queue.Calls);
        Assert.Equal("결제대기", source.결제상태); Assert.Equal(9500, payment.결제금액);
    }

    [Fact]
    public async Task LegacyCompletedFlagsWithWaitingDepositCannotReplayIntoDispatch()
    {
        await using var db = TransportPaymentPreparationR73Tests.Context();
        var source = TransportPaymentPreparationR73Tests.Source();
        var payment = TransportPaymentPreparationR73Tests.Snapshot(source); Complete(payment);
        // 과거 HTTP 성공만으로 완료 플래그가 기록돼도 실제 입금 대기는 확보 완료가 아닙니다.
        payment.Toss응답Json = ProviderResponse(payment, "WAITING_FOR_DEPOSIT");
        payment.원본응답Json = payment.Toss응답Json;
        db.화주운송의뢰.Add(source); db.결제.Add(payment); await db.SaveChangesAsync();
        var queue = new Queue(); var sync = new Sync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(db, queue, sync).Handle(Event(payment), default));
        Assert.Contains("수납 완료 근거", error.Message);
        Assert.Equal("미시작", source.배차상태); Assert.Equal("결제대기", source.결제상태);
        Assert.Equal("결제대기", source.정산상태); Assert.Equal(0, queue.Calls); Assert.Equal(0, sync.Calls);
    }

    [Fact]
    public async Task LegacyFakeApprovalDoesNotRequireTossPreparationOrRewindCompletedTransport()
    {
        await using var db = TransportPaymentPreparationR73Tests.Context();
        var source = TransportPaymentPreparationR73Tests.Source(); source.배차상태 = "하차완료";
        var payment = TransportPaymentPreparationR73Tests.Payment(); Complete(payment);
        payment.결제제공자 = 결제공통정의.결제제공자.FakePG;
        db.화주운송의뢰.Add(source); db.결제.Add(payment); await db.SaveChangesAsync();
        var queue = new Queue();
        await Handler(db, queue, new Sync(), false).Handle(Event(payment), default);
        Assert.Equal("하차완료", source.배차상태); Assert.Equal(0, queue.Calls);
    }

    private static void Complete(결제 payment)
    {
        payment.결제상태 = 상태값.결제상태.결제완료;
        payment.공통결제상태 = 결제공통정의.결제상태.승인완료;
        payment.PaymentKey = "test-key-" + payment.결제Id;
        payment.Toss응답Json = ProviderResponse(payment, "DONE");
        payment.원본응답Json = payment.Toss응답Json;
    }
    private static string ProviderResponse(결제 payment, string status) => JsonSerializer.Serialize(new
    {
        status, paymentKey = payment.PaymentKey, orderId = payment.OrderId,
        totalAmount = payment.결제금액, currency = payment.통화,
        balanceAmount = payment.결제금액, cancels = Array.Empty<object>()
    });
    private static 결제승인완료Event Event(결제 payment) => new(payment.Id, payment.결제Id, payment.결제대상유형,
        payment.대상Id, payment.결제제공자, payment.결제금액, payment.통화, DateTime.UtcNow);
    private static 용달운송의뢰결제승인완료EventHandler Handler(SsalddelContext db, Queue queue, Sync sync, bool enabled = true)
        => new(db, queue, sync, NullLogger<용달운송의뢰결제승인완료EventHandler>.Instance,
            Options.Create(new TransportPrepaymentOptions { Enabled = enabled }));
    private sealed class Queue : I운송의뢰배차대기Service
    {
        public int Calls;
        public Task<운송원장> 생성또는조회Async(출고예정운송대상 target, 운송의뢰배차대기생성옵션? options = null, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new 운송원장 { 의뢰Id = target.운송의뢰Id!, 상태 = "매칭중" }); }
    }
    private sealed class Sync : I운송원장Mongo동기화Service
    {
        public int Calls;
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(살뜰.도메인.화주.화주운송의뢰 source, string by, CancellationToken ct = default)
        { Calls++; return Task.FromResult<커뮤니티원장Dto?>(null); }
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 transport, string by, CancellationToken ct = default)
            => throw new InvalidOperationException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken ct = default)
            => throw new InvalidOperationException();
    }
}
