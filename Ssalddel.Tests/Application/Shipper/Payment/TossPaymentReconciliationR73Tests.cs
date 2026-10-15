using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Payment;
using Ssalddel.Contracts.CommonContents;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.External.Toss;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Payment;

public sealed class TossPaymentReconciliationR73Tests
{
    [Fact]
    public async Task 가상계좌발급은배차Outbox없이대기하고같은결제조회DONE에서한번만확정한다()
    {
        await using var f = await Fixture.Create();
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsSuccess);
        Assert.Equal(상태값.결제상태.결제대기, f.Payment.결제상태); Assert.Empty(f.Db.결제승인완료Outbox);
        Assert.Null(f.Payment.승인일시); Assert.Equal(0, f.Benefits.Consumes);
        Assert.DoesNotContain("deposit-secret", f.Payment.Toss응답Json!); Assert.NotNull(f.Payment.PG입금알림검증Hash);
        f.Gateway.Status = "DONE";
        Assert.True((await f.Use().재조회Async("order")).IsSuccess);
        Assert.True((await f.Use().재조회Async("order")).IsSuccess);
        Assert.Equal(1, f.Gateway.Posts); Assert.Equal(2, f.Gateway.Gets); Assert.Equal(1, f.Benefits.Consumes);
        Assert.Single(f.Db.결제승인완료Outbox); Assert.True(TossPaymentCollectionEvidencePolicy.수납확인됨(f.Payment));
    }

    [Fact]
    public async Task 승인응답유실후동일승인요청도GET으로복구하고POST를반복하지않는다()
    {
        await using var f = await Fixture.Create(); f.Gateway.Uncertain = true;
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsFailed);
        Assert.Equal("key", f.Payment.PaymentKey); Assert.Empty(f.Db.결제승인완료Outbox);
        f.Gateway.Uncertain = false; f.Gateway.Status = "DONE";
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsSuccess);
        Assert.Equal(1, f.Gateway.Posts); Assert.Equal(1, f.Gateway.Gets); Assert.Single(f.Db.결제승인완료Outbox);
    }

    [Fact]
    public async Task 완료뒤대기역순조회는완료와한번소비된혜택을보존한다()
    {
        await using var f = await Fixture.Create(); f.Gateway.Status = "DONE";
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsSuccess);
        f.Gateway.Status = "WAITING_FOR_DEPOSIT"; f.Benefits.Discount = 100;
        Assert.True((await f.Use().재조회Async("order")).IsSuccess);
        Assert.True(TossPaymentCollectionEvidencePolicy.수납확인됨(f.Payment));
        Assert.Equal(1, f.Benefits.Consumes); Assert.Single(f.Db.결제승인완료Outbox);
    }

    [Theory]
    [InlineData("CANCELED")]
    [InlineData("PARTIAL_CANCELED")]
    public async Task PG취소조회는운송완료를되돌리지않고지급을보류한다(string status)
    {
        await using var f = await Fixture.Create(); f.Gateway.Status = "DONE";
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsSuccess);
        f.Source.배차상태 = "인수완료"; await f.Db.SaveChangesAsync(); f.Gateway.Status = status;
        Assert.True((await f.Use().재조회Async("order")).IsSuccess);
        Assert.False(TossPaymentCollectionEvidencePolicy.수납확인됨(f.Payment));
        Assert.Equal("비정상운송검토보류", f.Source.정산상태); Assert.Equal("인수완료", f.Source.배차상태);
        f.Gateway.Status = "DONE"; Assert.True((await f.Use().재조회Async("order")).IsSuccess);
        Assert.Equal(상태값.결제상태.결제취소, f.Payment.결제상태); Assert.Single(f.Db.결제승인완료Outbox);
    }

    [Fact]
    public async Task 입금알림은기본OFF이며활성후에도비밀해시가맞아야PG조회만실행한다()
    {
        await using var f = await Fixture.Create(); Assert.True((await f.Use().승인Async("key", "order", 6000)).IsSuccess);
        f.Gateway.Status = "DONE";
        await f.Use().입금알림Async("order", "deposit-secret"); Assert.Equal(0, f.Gateway.Gets);
        f.Options.EnableDepositWebhook = true;
        await f.Use().입금알림Async("order", "wrong-secret"); Assert.Equal(0, f.Gateway.Gets);
        await f.Use(user: "unrelated").입금알림Async("order", "deposit-secret");
        Assert.Equal(1, f.Gateway.Posts); Assert.Equal(1, f.Gateway.Gets); Assert.Single(f.Db.결제승인완료Outbox);
        Assert.DoesNotContain("deposit-secret", f.Payment.Toss응답Json!);
    }

    [Fact]
    public async Task 기존환불기록에늦은DONE조회가와도완료로복원하지않는다()
    {
        await using var f = await Fixture.Create(); Assert.True((await f.Use().승인Async("key", "order", 6000)).IsSuccess);
        f.Payment.결제상태 = 상태값.결제상태.환불됨; f.Payment.공통결제상태 = 결제공통정의.결제상태.환불완료;
        await f.Db.SaveChangesAsync(); f.Gateway.Status = "DONE";
        Assert.True((await f.Use().재조회Async("order")).IsSuccess);
        Assert.Equal(상태값.결제상태.환불됨, f.Payment.결제상태); Assert.Empty(f.Db.결제승인완료Outbox);
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsFailed);
    }

    [Fact]
    public async Task 다른이용자의재조회는PG를호출하거나상태를바꾸지않는다()
    {
        await using var f = await Fixture.Create(); Assert.True((await f.Use().승인Async("key", "order", 6000)).IsSuccess);
        Assert.True((await f.Use("other").재조회Async("order")).IsFailed);
        Assert.Equal(0, f.Gateway.Gets); Assert.Equal(상태값.결제상태.결제대기, f.Payment.결제상태);
    }

    [Fact]
    public async Task 준비후운임혜택변경은승인과조회전차단한다()
    {
        await using var f = await Fixture.Create(); f.Source.최종운임 = 7000m; await f.Db.SaveChangesAsync();
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsFailed); Assert.Equal(0, f.Gateway.Posts);
        f.Source.최종운임 = 6000m; await f.Db.SaveChangesAsync(); f.Benefits.Discount = 100;
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsFailed); Assert.Equal(0, f.Gateway.Posts);
    }

    [Fact]
    public async Task 구형완료결제재요청은새준비근거없이원래결과만복구한다()
    {
        await using var f = await Fixture.Create(); f.Payment.준비근거Json = null; f.Payment.PaymentKey = "key";
        f.Payment.결제상태 = 상태값.결제상태.결제완료; f.Payment.공통결제상태 = 결제공통정의.결제상태.승인완료;
        f.Payment.Toss응답Json = "{}"; f.Source.최종운임 = 9000m; await f.Db.SaveChangesAsync(); f.Benefits.Discount = 100;
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsSuccess);
        Assert.Equal(0, f.Gateway.Posts); Assert.Equal(0, f.Gateway.Gets); Assert.Equal(0, f.Benefits.Consumes);
        Assert.Empty(f.Db.결제승인완료Outbox);
    }

    [Fact]
    public async Task Simulation승인은시도키도기록하지않고외부PG없이차단한다()
    {
        await using var f = await Fixture.Create();
        var mode = new SsalddelExecutionModePolicy(Microsoft.Extensions.Options.Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
        var flow = new 토스결제입금재조회UseCase(f.Db, f.Gateway, f.Benefits, new Actor("owner"), Microsoft.Extensions.Options.Options.Create(f.Options), mode);
        Assert.True((await flow.승인Async("key", "order", 6000)).IsFailed);
        Assert.Null(f.Payment.PaymentKey); Assert.Equal(0, f.Gateway.Posts); Assert.Empty(f.Db.결제승인완료Outbox);
    }

    [Fact]
    public async Task PG가다른금액을반환하면기존대기와Outbox없음을유지한다()
    {
        await using var f = await Fixture.Create(); f.Gateway.Amount = 1;
        Assert.True((await f.Use().승인Async("key", "order", 6000)).IsFailed);
        Assert.Equal(상태값.결제상태.결제대기, f.Payment.결제상태); Assert.Empty(f.Db.결제승인완료Outbox);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public SsalddelContext Db { get; private init; } = null!;
        public 결제 Payment { get; private init; } = null!; public 화주운송의뢰 Source { get; private init; } = null!;
        public Gateway Gateway { get; } = new(); public Benefits Benefits { get; } = new();
        public TransportPrepaymentOptions Options { get; } = new();
        public 토스결제입금재조회UseCase Use(string user = "owner") => new(Db, Gateway, Benefits, new Actor(user), Microsoft.Extensions.Options.Options.Create(Options));
        public static async Task<Fixture> Create()
        {
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new DummyPersonalDataEncryptionService());
            var source = new 화주운송의뢰 { 의뢰Id = "work", 화주Id = "owner", 주문자UserId = "owner", 최종운임 = 6000m,
                정산시점 = "선결제", 수납주체 = "플랫폼", 결제수단 = "가상계좌", 결제상태 = 상태값.결제상태.결제대기, 배차상태 = "미시작" };
            var payment = new 결제 { 결제Id = "payment", OrderId = "order", 의뢰Id = "work", 대상Id = "work", 화주Id = "owner", 결제금액 = 6000 };
            payment.준비근거Json = 배송료결제준비근거Policy.생성(payment, source, new() { 원운임 = 6000, 최종결제금액 = 6000 }).Value;
            db.화주운송의뢰.Add(source); db.결제.Add(payment); await db.SaveChangesAsync();
            return new() { Db = db, Source = source, Payment = payment };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed record Actor(string UserId) : ICurrentUserAccessor { public string? Role => "화주"; }
    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Benefits : I콘텐츠혜택계산Service
    {
        public int Discount { get; set; } public int Consumes { get; private set; }
        public Task<결제혜택견적응답> 계산Async(string user, int gross, CancellationToken ct = default)
            => Task.FromResult(new 결제혜택견적응답 { 원운임 = gross, 콘텐츠할인금액 = Discount, 최종결제금액 = gross - Discount });
        public Task<int> 보상사용처리Async(string user, CancellationToken ct = default) { Consumes++; return Task.FromResult(1); }
    }
    private sealed class Gateway : I공통결제Service
    {
        public int Posts { get; private set; } public int Gets { get; private set; } public string Status { get; set; } = "WAITING_FOR_DEPOSIT";
        public bool Uncertain { get; set; } public int Amount { get; set; } = 6000;
        public Task<결제승인결과> 결제승인Async(int provider, 결제승인요청 request, CancellationToken ct = default) { Posts++; return Task.FromResult(Result(request)); }
        public Task<결제승인결과> 결제조회Async(int provider, 결제승인요청 request, CancellationToken ct = default) { Gets++; return Task.FromResult(Result(request)); }
        private 결제승인결과 Result(결제승인요청 request) => Uncertain ? new(false, "{}", null, null, IsUncertain: true)
            : new(true, JsonSerializer.Serialize(new { status = Status, paymentKey = request.PaymentKey, orderId = request.OrderId,
                totalAmount = Amount, currency = "KRW", method = "가상계좌", secret = "deposit-secret" }), "가상계좌", request.PaymentKey);
    }
}
