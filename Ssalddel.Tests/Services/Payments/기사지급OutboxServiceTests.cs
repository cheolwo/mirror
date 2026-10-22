using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;
using 살뜰.도메인.기사;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Payments;

public sealed class 기사지급OutboxServiceTests
{
    [Fact]
    public async Task Simulation처리는_송금완료가아닌_검증완료로기록한다()
    {
        await using var db = CreateContext();
        var pair = await SeedAsync(db);
        var service = new 기사지급OutboxService(
            db,
            new 준비전용기사지급Gateway(
                new TestExecutionModePolicy(SsalddelExecutionMode.Simulation)),
            TimeProvider.System,
            NullLogger<기사지급OutboxService>.Instance, CargoPayoutDisputeQueryStub.Clear);

        var processed = await service.대기항목처리Async();

        Assert.Equal(1, processed);
        await db.Entry(pair.Request).ReloadAsync();
        await db.Entry(pair.Outbox).ReloadAsync();
        Assert.Equal(기사지급요청상태코드.Simulation검증완료, pair.Request.상태코드);
        Assert.Equal(기사지급Outbox상태코드.Simulation검증완료, pair.Outbox.처리상태);
        Assert.Equal("SimulationNoTransfer", pair.Request.마지막처리코드);
        Assert.NotNull(pair.Request.Simulation검증일시Utc);
    }

    [Fact]
    public async Task 일시실패는_시도횟수와다음시각을남기고_재시도대기로둔다()
    {
        await using var db = CreateContext();
        var pair = await SeedAsync(db);
        var service = new 기사지급OutboxService(
            db,
            new TransientFailureGateway(),
            TimeProvider.System,
            NullLogger<기사지급OutboxService>.Instance, CargoPayoutDisputeQueryStub.Clear);

        await service.대기항목처리Async();

        await db.Entry(pair.Request).ReloadAsync();
        await db.Entry(pair.Outbox).ReloadAsync();
        Assert.Equal(기사지급요청상태코드.재시도대기, pair.Request.상태코드);
        Assert.Equal(기사지급Outbox상태코드.재시도대기, pair.Outbox.처리상태);
        Assert.Equal(1, pair.Outbox.시도횟수);
        Assert.True(pair.Outbox.다음시도시각Utc > pair.Outbox.마지막시도시각Utc);
        Assert.Equal("TemporaryFailure", pair.Outbox.마지막결과코드);
    }

    [Fact]
    public async Task Operational기본Gateway는_Provider미구성으로차단하고_송금하지않는다()
    {
        await using var db = CreateContext();
        var pair = await SeedAsync(db);
        var service = new 기사지급OutboxService(
            db,
            new 준비전용기사지급Gateway(
                new TestExecutionModePolicy(SsalddelExecutionMode.Operational)),
            TimeProvider.System,
            NullLogger<기사지급OutboxService>.Instance, CargoPayoutDisputeQueryStub.Clear);

        await service.대기항목처리Async();

        await db.Entry(pair.Request).ReloadAsync();
        await db.Entry(pair.Outbox).ReloadAsync();
        Assert.Equal(기사지급요청상태코드.운영Provider미구성, pair.Request.상태코드);
        Assert.Equal(기사지급Outbox상태코드.운영Provider미구성, pair.Outbox.처리상태);
        Assert.Null(pair.Request.Simulation검증일시Utc);
    }

    private static async Task<(기사운송대금지급요청 Request, 기사지급Outbox Outbox)> SeedAsync(
        SsalddelContext db)
    {
        db.운송원장.Add(new 운송원장
        {
            Id = 10, 의뢰Id = "request-10", 운송번호 = "transport-10", 확정기사Id = "driver-a",
            도착 = DateTime.UtcNow.AddHours(-1), 상태 = "인수완료"
        });
        db.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = "request-10" });
        var request = new 기사운송대금지급요청
        {
            운송Id = 10,
            운송번호 = "transport-10",
            의뢰Id = "request-10",
            기사Id = "driver-a",
            지급예정금액 = 42000m,
            통화코드 = "KRW",
            멱등키 = "payout-10",
            상태코드 = 기사지급요청상태코드.승인됨,
            승인관리자Id = "admin-1",
            승인사유 = "test",
            실행모드코드 = "Simulation",
            승인일시Utc = DateTime.UtcNow
        };
        db.기사운송대금지급요청.Add(request);
        await db.SaveChangesAsync();
        var outbox = new 기사지급Outbox
        {
            기사지급요청Id = request.Id,
            멱등키 = request.멱등키,
            PayloadJson = "{}",
            처리상태 = 기사지급Outbox상태코드.대기,
            다음시도시각Utc = DateTime.UtcNow.AddMinutes(-1)
        };
        db.기사지급Outbox.Add(outbox);
        await db.SaveChangesAsync();
        return (request, outbox);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"driver-payout-outbox-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed record TestExecutionModePolicy(SsalddelExecutionMode Mode)
        : ISsalddelExecutionModePolicy
    {
        public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => Mode == SsalddelExecutionMode.Operational;
    }

    private sealed class TransientFailureGateway : I기사지급Gateway
    {
        public Task<기사지급Gateway결과> 처리Async(
            기사운송대금지급요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                기사지급Gateway결과.재시도(
                    "TemporaryFailure",
                    "일시 오류"));
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
