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
using Ssalddel.Contracts.CommonContents;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodFulfillmentPaymentBoundaryTests
{
    [Theory]
    [InlineData(상태값.결제상태.결제대기)]
    [InlineData(상태값.결제상태.결제완료)]
    public async Task 음식모의수행은_공통PG승인호출전차단하고_완료결제로도우회하지않는다(string state)
    {
        await using var db = Context();
        db.음식주문.Add(new 살뜰.도메인.음식.음식주문 { 주문번호 = "request", 주문자UserId = "owner", 모의처리처수행 = true });
        var existing = Payment(state);
        existing.결제대상유형 = 결제공통정의.결제대상유형.음식주문;
        db.결제.Add(existing);
        // 다른 원천의 동일 ID가 식당 결제 접근 범위를 바꾸지 않아야 합니다.
        var cargo = Source(state); cargo.주문자UserId = "other-cargo-owner"; cargo.모의처리처수행 = false;
        db.화주운송의뢰.Add(cargo);
        await db.SaveChangesAsync();
        var payment = new ForbiddenPayment(); var benefits = new ForbiddenBenefits();
        var result = await new 토스결제승인CommandHandler(db, payment, benefits, new Actor())
            .Handle(new("key", "order", 10000), default);
        Assert.True(result.IsFailed);
        Assert.Contains("모의", result.Errors[0].Message);
        Assert.Equal(0, payment.Calls); Assert.Equal(0, benefits.Calls);
        Assert.Empty(await db.결제승인완료Outbox.ToListAsync());
        Assert.Equal(state, (await db.결제.SingleAsync()).결제상태);
    }

    [Theory]
    [InlineData(상태값.결제상태.결제대기)]
    [InlineData(상태값.결제상태.결제완료)]
    public void Simulation_CannotPrepareOrConfirmPayment_EvenIfOldPaymentIsCompleted(string state)
    {
        var source = Source(state);
        var payment = Payment(state);
        Assert.False(화주운송결제진행정책.결제준비요청검증(source, "owner", "화주").통과);
        var result = 화주운송결제진행정책.결제승인요청검증(payment, source, "owner", "화주", 10000);
        Assert.False(result.통과);
        Assert.False(result.이미완료됨);
    }

    [Fact]
    public void Simulation_DoesNotDiscloseItsKind_ToAnotherUser()
    {
        var source = Source(상태값.결제상태.결제대기);
        var result = 화주운송결제진행정책.결제준비요청검증(source, "other", "화주");
        Assert.Equal("의뢰를 찾을 수 없습니다.", result.실패사유);
    }

    [Fact]
    public async Task Simulation_TossPrepare_DoesNotConsumeBenefitsOrCreatePayment()
    {
        await using var db = Context();
        db.화주운송의뢰.Add(Source(상태값.결제상태.결제대기));
        await db.SaveChangesAsync();
        var benefits = new ForbiddenBenefits();
        var handler = new 토스결제준비CommandHandler(db, Options.Create(new TossPaymentsOptions()), benefits, new Actor());
        var result = await handler.Handle(new 토스결제준비Command("request", 10000), default);
        Assert.True(result.IsFailed);
        Assert.Equal(0, benefits.Calls);
        Assert.Empty(await db.결제.ToListAsync());
        Assert.Empty(await db.결제승인완료Outbox.ToListAsync());
    }

    [Fact]
    public async Task Simulation_TossConfirm_DoesNotCallPaymentProviderOrCreateOutbox()
    {
        await using var db = Context();
        db.화주운송의뢰.Add(Source(상태값.결제상태.결제대기));
        db.결제.Add(Payment(상태값.결제상태.결제대기));
        await db.SaveChangesAsync();
        var payment = new ForbiddenPayment(); var benefits = new ForbiddenBenefits();
        var handler = new 토스결제승인CommandHandler(db, payment, benefits, new Actor());
        var result = await handler.Handle(new 토스결제승인Command("key", "order", 10000), default);
        Assert.True(result.IsFailed);
        Assert.Equal(0, payment.Calls); Assert.Equal(0, benefits.Calls);
        Assert.Empty(await db.결제승인완료Outbox.ToListAsync());
        Assert.Equal(상태값.결제상태.결제대기, (await db.결제.SingleAsync()).결제상태);
    }

    [Fact]
    public async Task Simulation_CompletedFakePayment_DoesNotReplayMissingOutbox()
    {
        await using var db = Context();
        db.화주운송의뢰.Add(Source(상태값.결제상태.결제완료));
        var payment = Payment(상태값.결제상태.결제완료);
        payment.PG사 = "FakePG"; payment.결제제공자 = 결제공통정의.결제제공자.FakePG;
        db.결제.Add(payment); await db.SaveChangesAsync();
        var handler = new 페이크결제승인CommandHandler(db, new Actor(), new Host(), new ModePolicy());
        var result = await handler.Handle(new 페이크결제승인Command("request", 10000, "카드", null, "operation"), default);
        Assert.True(result.IsFailed);
        Assert.Empty(await db.결제승인완료Outbox.ToListAsync());
    }

    [Fact]
    public async Task Simulation_DelayedApprovalEvent_DoesNotChangeSettlementOrCreateOwnQueue()
    {
        await using var db = Context();
        db.화주운송의뢰.Add(Source(상태값.결제상태.결제대기));
        await db.SaveChangesAsync();
        // Neither dependency may be invoked for a simulated request; this exercises the real event handler.
        var handler = new 용달운송의뢰결제승인완료EventHandler(db, null!, null!, NullLogger<용달운송의뢰결제승인완료EventHandler>.Instance);
        await handler.Handle(new(1, "payment", 결제공통정의.결제대상유형.용달운송의뢰,
            "request", 결제공통정의.결제제공자.TossPayments, 10000, "KRW", DateTime.UtcNow), default);
        var source = await db.화주운송의뢰.SingleAsync();
        Assert.Equal("모의 수행·정산 제외", source.정산상태);
        Assert.Equal(상태값.결제상태.결제대기, source.결제상태);
        Assert.Empty(await db.운송원장.ToListAsync());
    }

    [Theory]
    [InlineData("fare")]
    [InlineData("cargo")]
    [InlineData("contact")]
    [InlineData("notes")]
    public async Task Simulation_GenericRequestEdit_CannotChangeConfirmedConditions(string field)
    {
        await using var db = Context();
        var source = Source(상태값.결제상태.결제대기);
        db.화주운송의뢰.Add(source); await db.SaveChangesAsync();
        var command = new 의뢰수정Command("request", new(null, null, null), new(null, null, null, null, null, null, null),
            new(null, null, null, null, null, null, null, null), new(null, null, null, null, null, null, null, null), new(null), null);
        command = field switch
        {
            "fare" => command with { 요금옵션 = new PricingDTO { 최종운임 = 5000 } },
            "cargo" => command with { 화물정보 = new(null, null, 3, null, null, null, null) },
            "contact" => command with { 픽업지 = new(null, null, null, null, "변경 연락처", null, null, null) },
            _ => command with { 요청조건 = new("새 요청 사항") }
        };
        var handler = new 의뢰수정CommandHandler(db, new 화주운송업무담당자UseCase(db, new Actor(), TimeProvider.System));
        var result = await handler.Handle(command, default);
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, error => error.Metadata.TryGetValue("ErrorCode", out var code) && Equals(code, "SimulationFulfillmentLocked"));
        Assert.Equal("모의 수행·정산 제외", source.정산상태);
        Assert.Equal("책 한 상자", source.화물종류);
        Assert.Null(source.최종운임);
    }

    private static 화주운송의뢰 Source(string state) => new()
    {
        의뢰Id = "request", 화주Id = "owner", 주문자UserId = "owner", 화물종류 = "책 한 상자",
        모의처리처수행 = true, 배차상태 = 상태값.배차상태.상차완료,
        결제상태 = state, 정산상태 = "모의 수행·정산 제외"
    };
    private static 결제 Payment(string state) => new()
    {
        결제Id = "payment", 의뢰Id = "request", 대상Id = "request", 화주Id = "owner",
        결제대상유형 = 결제공통정의.결제대상유형.용달운송의뢰, 결제제공자 = 결제공통정의.결제제공자.TossPayments,
        결제금액 = 10000, 결제상태 = state, OrderId = "order", 통화 = "KRW"
    };
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed record Actor : ICurrentUserAccessor
    { public string? UserId => "owner"; public string? Role => "화주"; }
    private sealed class ForbiddenBenefits : I콘텐츠혜택계산Service
    {
        public int Calls { get; private set; }
        public Task<결제혜택견적응답> 계산Async(string 사용자Id, int 원운임, CancellationToken cancellationToken = default)
        { Calls++; throw new InvalidOperationException("Simulated request reached benefit calculation."); }
        public Task<int> 보상사용처리Async(string 사용자Id, CancellationToken cancellationToken = default)
        { Calls++; throw new InvalidOperationException("Simulated request reached benefit consumption."); }
    }
    private sealed class ForbiddenPayment : I공통결제Service
    {
        public int Calls { get; private set; }
        public Task<결제승인결과> 결제승인Async(int 결제제공자유형, 결제승인요청 request, CancellationToken cancellationToken = default)
        { Calls++; throw new InvalidOperationException("Simulated request reached payment provider."); }
    }
    private sealed class ModePolicy : ISsalddelExecutionModePolicy
    { public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation; public bool IsSimulation => true; public bool IsOperational => false; }
    private sealed class Host : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ssalddel.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
