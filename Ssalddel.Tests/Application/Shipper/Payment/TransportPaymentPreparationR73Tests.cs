using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Payment;
using Ssalddel.Contracts.CommonContents;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Payment;

public sealed class TransportPaymentPreparationR73Tests
{
    [Fact]
    public void DefaultOff_NewPrepaymentRequiresExplicitOption_AndPostpayStillRequiresPickup()
    {
        var source = Source();
        Assert.False(화주운송결제진행정책.결제준비요청검증(source, "orderer", "화주").통과);
        Assert.True(화주운송결제진행정책.결제준비요청검증(source, "orderer", "화주", true).통과);
        source.정산시점 = "운송완료후정산";
        Assert.False(화주운송결제진행정책.결제준비요청검증(source, "orderer", "화주", true).통과);
        source.배차상태 = 상태값.배차상태.상차완료;
        Assert.True(화주운송결제진행정책.결제준비요청검증(source, "orderer", "화주").통과);
    }

    [Fact]
    public void SeparateOrderingUserKeepsPaymentAuthority_AndShipperIdAloneCannotBypassIt()
    {
        var source = Source();
        Assert.False(화주운송결제진행정책.결제준비요청검증(source, "owner", "화주", true).통과);
        Assert.True(화주운송결제진행정책.결제준비요청검증(source, "orderer", "화주", true).통과);
    }

    [Theory]
    [InlineData("매칭중")]
    [InlineData("배차확정")]
    [InlineData("상차중")]
    [InlineData("취소")]
    public void PrepaymentDoesNotReopenNonInitialRequest(string state)
    {
        var source = Source(); source.배차상태 = state;
        Assert.False(화주운송결제진행정책.결제준비요청검증(source, "orderer", "화주", true).통과);
    }

    [Fact]
    public void SnapshotIgnoresWorkStateAndUpdatedAt_ButPreservesApprovedAmount()
    {
        var source = Source(); var payment = Snapshot(source);
        source.배차상태 = "운송중"; source.UpdatedAt = source.UpdatedAt.AddDays(1);
        source.결제상태 = 상태값.결제상태.결제완료; source.정산상태 = "정산대기";
        payment.결제상태 = 상태값.결제상태.결제완료;
        var frozen = payment.준비근거Json;
        Assert.True(배송료결제준비근거Policy.현재근거검증(payment, source).IsSuccess);
        Assert.Equal(9500, payment.결제금액); Assert.Equal(frozen, payment.준비근거Json);
    }

    [Theory]
    [InlineData("expected")]
    [InlineData("fare")]
    [InlineData("composition")]
    [InlineData("waiting")]
    [InlineData("handling")]
    [InlineData("surcharge")]
    [InlineData("settlement")]
    [InlineData("collector")]
    [InlineData("method")]
    public void ChangedQuoteCannotApproveOldAmount(string field)
    {
        var source = Source(); var payment = Snapshot(source);
        switch (field)
        {
            case "expected": source.결제예정금액++; break;
            case "fare": source.최종운임++; break;
            case "composition": source.운임구성Id++; break;
            case "waiting": source.대기료 = 100; break;
            case "handling": source.수작업비 = 100; break;
            case "surcharge": source.할증 = 100; break;
            case "settlement": source.정산시점 = "운송완료후정산"; break;
            case "collector": source.수납주체 = "기사"; break;
            case "method": source.결제수단 = "가상계좌"; break;
        }
        AssertCode("PreparationQuoteChanged", 배송료결제준비근거Policy.현재근거검증(payment, source));
        Assert.Equal(9500, payment.결제금액);
    }

    [Fact]
    public void MissingOrTamperedEvidenceIsHeld_AndBenefitChangesAreDetected()
    {
        var source = Source(); var payment = Snapshot(source);
        AssertCode("PreparationBenefitChanged", 배송료결제준비근거Policy.혜택근거검증(payment, Benefit(10000, 0, 0)));
        payment.준비근거Json = payment.준비근거Json!.Replace("9500", "9400", StringComparison.Ordinal);
        AssertCode("PreparationEvidenceInvalid", 배송료결제준비근거Policy.현재근거검증(payment, source));
        payment.준비근거Json = null;
        AssertCode("PreparationEvidenceMissing", 배송료결제준비근거Policy.현재근거검증(payment, source));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9999)]
    [InlineData(10001)]
    public async Task ArbitraryAmountFailsBeforeBenefitsOrPaymentCreation(int amount)
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();
        var benefits = new Benefits();
        var result = await Handler(db, benefits).Handle(new("request", amount), default);
        Assert.True(result.IsFailed); Assert.Equal(0, benefits.Calls); Assert.Empty(await db.결제.ToListAsync());
        Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("ErrorCode", out var code) && Equals(code, "PreparationAmountMismatch"));
    }

    [Fact]
    public async Task DefaultHandlerKeepsNewPrepaymentOff_ButRetainsPostPickupPayment()
    {
        await using var db = Context(); var source = Source(); db.화주운송의뢰.Add(source); await db.SaveChangesAsync();
        var benefits = new Benefits();
        var handler = new 토스결제준비CommandHandler(db, Options.Create(new TossPaymentsOptions()), benefits, new Actor());
        Assert.True((await handler.Handle(new("request", 10000), default)).IsFailed);
        Assert.Equal(0, benefits.Calls); Assert.Empty(await db.결제.ToListAsync());
        source.정산시점 = "운송완료후정산"; source.배차상태 = "상차완료"; await db.SaveChangesAsync();
        Assert.True((await handler.Handle(new("request", 10000), default)).IsSuccess);
        Assert.Single(await db.결제.ToListAsync());
    }

    [Fact]
    public void StoredShipperAmountHasPriority_AndMissingAmountIsNeverMadeUp()
    {
        var source = Source(); source.결제예정금액 = 12000; source.최종운임 = 10000;
        Assert.Equal(12000, 배송료결제준비근거Policy.서버원운임조회(source).Value);
        source.결제예정금액 = null;
        Assert.Equal(10000, 배송료결제준비근거Policy.서버원운임조회(source).Value);
        source.최종운임 = 10000.5m;
        Assert.True(배송료결제준비근거Policy.서버원운임조회(source).IsFailed);
        source.최종운임 = null;
        Assert.True(배송료결제준비근거Policy.서버원운임조회(source).IsFailed);
    }

    [Fact]
    public async Task PrepareUsesGrossServerAmount_AndReturnsFrozenBenefitNet_ReusesSameEvidence()
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();
        var benefits = new Benefits(); var handler = Handler(db, benefits);
        var first = await handler.Handle(new("request", 10000), default);
        var second = await handler.Handle(new("request", 10000), default);
        Assert.True(first.IsSuccess); Assert.True(second.IsSuccess);
        Assert.Equal(9500, first.Value.Amount); Assert.Equal(first.Value.결제Id, second.Value.결제Id);
        var payment = Assert.Single(await db.결제.ToListAsync());
        Assert.True(배송료결제준비근거Policy.현재근거검증(payment, await db.화주운송의뢰.SingleAsync()).IsSuccess);
        Assert.True((await 배송료결제준비근거Policy.현재혜택검증Async(payment, benefits)).IsSuccess);
        Assert.Equal(10000, benefits.LastGross);
    }

    [Fact]
    public async Task ExplicitReprepareDoesNotUpgradeLegacyEvidenceOrRewriteItsAmount()
    {
        await using var db = Context(); var source = Source(); db.화주운송의뢰.Add(source);
        var legacy = Payment(); legacy.결제금액 = 9500; db.결제.Add(legacy); await db.SaveChangesAsync();
        var result = await Handler(db, new Benefits()).Handle(new("request", 10000), default);
        Assert.True(result.IsSuccess); Assert.NotEqual(legacy.결제Id, result.Value.결제Id);
        Assert.Null(legacy.준비근거Json); Assert.Equal(9500, legacy.결제금액);
        Assert.Equal(2, await db.결제.CountAsync());
    }

    [Fact]
    public async Task ChangedBenefitsWithSameNetCannotReuseDifferentBenefitEvidence()
    {
        await using var db = Context(); var source = Source(); db.화주운송의뢰.Add(source);
        var old = Snapshot(source); db.결제.Add(old); await db.SaveChangesAsync();
        var benefits = new Benefits { Points = 0, Discount = 500 };
        var result = await Handler(db, benefits).Handle(new("request", 10000), default);
        Assert.True(result.IsSuccess); Assert.NotEqual(old.결제Id, result.Value.결제Id);
        AssertCode("PreparationBenefitChanged", await 배송료결제준비근거Policy.현재혜택검증Async(old, benefits));
    }

    [Fact]
    public async Task InFlightPaymentWithOtherAmountRequiresResultCheckBeforeAnotherPreparation()
    {
        await using var db = Context(); var source = Source(); db.화주운송의뢰.Add(source);
        var old = Snapshot(source); old.PaymentKey = "existing-key"; db.결제.Add(old); await db.SaveChangesAsync();
        source.결제예정금액 = 11000; source.최종운임 = 11000; await db.SaveChangesAsync();
        var result = await Handler(db, new Benefits()).Handle(new("request", 11000), default);
        Assert.True(result.IsFailed); Assert.Single(await db.결제.ToListAsync());
        Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("ErrorCode", out var code) && Equals(code, "PreparationResultCheckRequired"));
    }

    [Fact]
    public async Task InvalidBenefitArithmeticCannotCreatePayment()
    {
        await using var db = Context(); db.화주운송의뢰.Add(Source()); await db.SaveChangesAsync();
        var result = await Handler(db, new Benefits { InvalidNet = true }).Handle(new("request", 10000), default);
        Assert.True(result.IsFailed); Assert.Empty(await db.결제.ToListAsync());
    }

    internal static 화주운송의뢰 Source() => new()
    {
        의뢰Id = "request", 화주Id = "owner", 주문자UserId = "orderer", 결제예정금액 = 10000,
        최종운임 = 10000, 운임구성Id = 1, 정산시점 = "선결제", 수납주체 = "플랫폼", 결제수단 = "카드",
        배차상태 = 상태값.배차상태.미시작, 정산상태 = "결제대기"
    };
    internal static 결제 Payment() => new()
    {
        결제Id = Guid.NewGuid().ToString("N"), OrderId = Guid.NewGuid().ToString("N"), 의뢰Id = "request",
        대상Id = "request", 화주Id = "owner", 결제금액 = 9500, 통화 = "KRW"
    };
    internal static 결제 Snapshot(화주운송의뢰 source)
    {
        var payment = Payment();
        payment.준비근거Json = 배송료결제준비근거Policy.생성(payment, source, Benefit(10000, 200, 300)).Value;
        return payment;
    }
    internal static 결제혜택견적응답 Benefit(int gross, int points, int discount) => new()
    { 원운임 = gross, 포인트사용가능액 = points, 콘텐츠할인금액 = discount, 최종결제금액 = gross - points - discount };
    internal static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("prepayment-r73-" + Guid.NewGuid()).Options, new Encryption());
    private static 토스결제준비CommandHandler Handler(SsalddelContext db, Benefits benefits) => new(db,
        Options.Create(new TossPaymentsOptions()), benefits, new Actor(), Options.Create(new TransportPrepaymentOptions { Enabled = true }));
    private static void AssertCode(string code, Result result)
    { Assert.True(result.IsFailed); Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("ErrorCode", out var actual) && Equals(actual, code)); }
    private sealed class Actor : ICurrentUserAccessor { public string? UserId => "orderer"; public string? Role => "화주"; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Benefits : I콘텐츠혜택계산Service
    {
        public int Calls; public int LastGross; public int Points = 200; public int Discount = 300; public bool InvalidNet;
        public Task<결제혜택견적응답> 계산Async(string id, int gross, CancellationToken ct = default)
        { Calls++; LastGross = gross; var benefit = Benefit(gross, Points, Discount); if (InvalidNet) benefit.최종결제금액--; return Task.FromResult(benefit); }
        public Task<int> 보상사용처리Async(string id, CancellationToken ct = default) => throw new InvalidOperationException("준비는 혜택을 소비하지 않습니다.");
    }
}
