using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Settlement;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Admin.Settlement;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Admin.Settlement;

public sealed class 기사지급승인UseCaseTests
{
    [Fact]
    public async Task 준비조건을_재검증하고_승인과Outbox를_함께저장한다()
    {
        await using var db = CreateContext();
        var transport = await SeedReadyAsync(db);
        var useCase = CreateUseCase(db);
        var request = CreateRequest(transport.Id);

        var result = await useCase.승인Async(request);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsActualTransferCompleted);
        Assert.False(result.Value.IsIdempotentReplay);
        Assert.Equal("Simulation", result.Value.ExecutionModeCode);
        Assert.Equal("Pending", result.Value.OutboxStatusCode);
        var payout = await db.기사운송대금지급요청.SingleAsync();
        var outbox = await db.기사지급Outbox.SingleAsync();
        Assert.Equal(payout.Id, outbox.기사지급요청Id);
        Assert.DoesNotContain("1234567890", outbox.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("기사 A", outbox.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 같은멱등키와같은자료는_중복저장없이_기존승인을반환한다()
    {
        await using var db = CreateContext();
        var transport = await SeedReadyAsync(db);
        var useCase = CreateUseCase(db);
        var request = CreateRequest(transport.Id);

        var first = await useCase.승인Async(request);
        var replay = await useCase.승인Async(request);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IsIdempotentReplay);
        Assert.Equal(first.Value.PayoutRequestId, replay.Value.PayoutRequestId);
        Assert.Equal(1, await db.기사운송대금지급요청.CountAsync());
        Assert.Equal(1, await db.기사지급Outbox.CountAsync());
    }

    [Fact]
    public async Task 확인금액이현재운임과다르면_승인하지않는다()
    {
        await using var db = CreateContext();
        var transport = await SeedReadyAsync(db);
        var useCase = CreateUseCase(db);
        var request = CreateRequest(transport.Id);
        request.ConfirmedExpectedPayoutAmount = 99999m;

        var result = await useCase.승인Async(request);

        Assert.True(result.IsFailed);
        Assert.Empty(db.기사운송대금지급요청);
        Assert.Empty(db.기사지급Outbox);
    }

    [Fact]
    public async Task 열린비정상운송사건이있으면_관리자도_기사지급을승인할수없다()
    {
        await using var db = CreateContext();
        var transport = await SeedReadyAsync(db);
        db.비정상운송사건.Add(new 비정상운송사건
        {
            사건StableId = $"abnormal-transport:{transport.Id}:CargoDamage",
            운송Id = transport.Id,
            운송의뢰Id = "request-a",
            사건유형Code = 비정상운송사건유형Codes.화물훼손,
            원본예외Code = "화물훼손",
            단계Code = "운행중",
            상태Code = 비정상운송사건상태Codes.운영검토대기,
            최초신고시각Utc = DateTime.UtcNow,
            최근신고시각Utc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var useCase = CreateUseCase(db);

        var result = await useCase.승인Async(CreateRequest(transport.Id));

        Assert.True(result.IsFailed);
        Assert.Contains("비정상 운송", result.Errors.Single().Message, StringComparison.Ordinal);
        Assert.Empty(db.기사운송대금지급요청);
        Assert.Empty(db.기사지급Outbox);
    }

    private static 기사지급승인UseCase CreateUseCase(SsalddelContext db)
        => new(
            db,
            new TestCurrentUserAccessor("admin-1", 역할명.서버관리자),
            new TestExecutionModePolicy(SsalddelExecutionMode.Simulation),
            TimeProvider.System);

    private static 기사지급승인요청 CreateRequest(long transportId)
        => new()
        {
            TransportId = transportId,
            ConfirmedRequestId = "request-a",
            ConfirmedExpectedPayoutAmount = 42000m,
            CurrencyCode = "KRW",
            IdempotencyKey = "payout-request-a-v1",
            ApprovalReason = "운송 완료와 화주 수납 및 계좌 확인"
        };

    private static async Task<운송원장> SeedReadyAsync(SsalddelContext db)
    {
        db.용달기사.Add(new 용달기사
        {
            기사Id = "driver-a",
            기사명 = "기사 A",
            연락처 = "010-0000-0000",
            차량 = "1톤 카고",
            주_활동지역 = "서울"
        });
        var transport = new 운송원장
        {
            운송번호 = "request-a",
            의뢰Id = "request-a",
            화주Id = "shipper-a",
            원본의뢰Id = "request-a",
            확정기사Id = "driver-a",
            기사_운송자 = "driver-a",
            상태 = "인수완료",
            도착 = DateTime.UtcNow.AddHours(-1),
            운임 = 65000
        };
        db.운송원장.Add(transport);
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-a",
            화주Id = "shipper-a",
            결제상태 = 상태값.결제상태.결제완료,
            정산상태 = 운임정산상태.입금확인완료.ToString(),
            정산시점 = 정산시점.운송완료후정산.ToString()
        });
        db.운임구성.Add(new 운임구성
        {
            의뢰Id = "request-a",
            최종운임 = 65000,
            기사지급예정운임 = 42000m,
            UpdatedAt = DateTime.UtcNow
        });
        db.Set<기사정산계좌>().Add(new 기사정산계좌
        {
            기사Id = "driver-a",
            국가코드 = "KR",
            은행명 = "국민은행",
            예금주명 = "기사 A",
            계좌번호 = "1234567890",
            확인상태 = 기사정산계좌확인상태.확인완료
        });
        await db.SaveChangesAsync();
        return transport;
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"driver-payout-approval-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed record TestCurrentUserAccessor(string? UserId, string? Role)
        : ICurrentUserAccessor;

    private sealed record TestExecutionModePolicy(SsalddelExecutionMode Mode)
        : ISsalddelExecutionModePolicy
    {
        public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => Mode == SsalddelExecutionMode.Operational;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
