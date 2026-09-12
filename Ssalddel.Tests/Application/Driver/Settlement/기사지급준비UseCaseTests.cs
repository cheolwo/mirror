using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Settlement;
using Ssalddel.Contracts.Driver.Settlement;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Settlement;

public sealed class 기사지급준비UseCaseTests
{
    [Fact]
    public async Task 화주수납과_확인계좌가_있으면_지급준비가능으로_분류한다()
    {
        await using var db = CreateContext();
        await SeedDriverAsync(db, "driver-a");
        await SeedCompletedTransportAsync(
            db,
            "driver-a",
            "request-a",
            new DateTime(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc),
            expectedPayout: 42000m,
            paymentStatus: 상태값.결제상태.결제완료);
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
        var useCase = new 기사지급준비UseCase(db);

        var result = await useCase.월별조회Async("driver-a", 2026, 7);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.HasSettlementAccount);
        Assert.Equal(42000m, result.Value.ExpectedPayoutTotal);
        Assert.Equal(42000m, result.Value.ReadyForPayoutPreparationTotal);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(기사지급준비상태코드.지급준비가능, item.ReadinessCode);
        Assert.True(item.IsReadyForPayoutPreparation);
        Assert.Equal("운임구성.기사지급예정운임", item.AmountSource);
    }

    [Fact]
    public async Task 열린비정상운송사건이있으면_수납과계좌가완료되어도_지급준비를보류한다()
    {
        await using var db = CreateContext();
        await SeedDriverAsync(db, "driver-a");
        await SeedCompletedTransportAsync(
            db,
            "driver-a",
            "request-held",
            new DateTime(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc),
            expectedPayout: 42000m,
            paymentStatus: 상태값.결제상태.결제완료);
        db.Set<기사정산계좌>().Add(new 기사정산계좌
        {
            기사Id = "driver-a",
            국가코드 = "KR",
            은행명 = "국민은행",
            예금주명 = "기사 A",
            계좌번호 = "1234567890",
            확인상태 = 기사정산계좌확인상태.확인완료
        });
        db.비정상운송사건.Add(new 비정상운송사건
        {
            사건StableId = "abnormal-transport:1:QuantityMismatch",
            운송Id = 1,
            운송의뢰Id = "request-held",
            사건유형Code = 비정상운송사건유형Codes.수량불일치,
            원본예외Code = "수량불일치",
            단계Code = "상차",
            상태Code = 비정상운송사건상태Codes.운영검토대기,
            최초신고시각Utc = DateTime.UtcNow,
            최근신고시각Utc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var useCase = new 기사지급준비UseCase(db);

        var result = await useCase.월별조회Async("driver-a", 2026, 7);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal(기사지급준비상태코드.비정상운송검토보류, item.ReadinessCode);
        Assert.False(item.IsReadyForPayoutPreparation);
        Assert.Equal(0m, result.Value.ReadyForPayoutPreparationTotal);
    }

    [Fact]
    public async Task 정상분인수와_영향분보류가결정되면_기사운임지급준비를막지않는다()
    {
        await using var db = CreateContext();
        await SeedDriverAsync(db, "driver-a");
        await SeedCompletedTransportAsync(
            db,
            "driver-a",
            "request-partial",
            new DateTime(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc),
            expectedPayout: 42000m,
            paymentStatus: 상태값.결제상태.결제완료);
        db.Set<기사정산계좌>().Add(new 기사정산계좌
        {
            기사Id = "driver-a",
            국가코드 = "KR",
            은행명 = "국민은행",
            예금주명 = "기사 A",
            계좌번호 = "1234567890",
            확인상태 = 기사정산계좌확인상태.확인완료
        });
        db.비정상운송사건.Add(new 비정상운송사건
        {
            사건StableId = "abnormal-transport:1:CargoDamage",
            운송Id = 1,
            운송의뢰Id = "request-partial",
            사건유형Code = 비정상운송사건유형Codes.화물훼손,
            원본예외Code = "화물훼손",
            단계Code = "하차",
            상태Code = 비정상운송사건상태Codes.조치결정,
            업무통제상태Code = 비정상운송업무통제상태Codes.일부보류,
            보류범위Code = 비정상운송보류범위Codes.영향수량,
            전체수량 = 10,
            정상확인수량 = 8,
            영향수량 = 2,
            정산보류적용여부 = false,
            최초신고시각Utc = DateTime.UtcNow,
            최근신고시각Utc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var useCase = new 기사지급준비UseCase(db);

        var result = await useCase.월별조회Async("driver-a", 2026, 7);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal(기사지급준비상태코드.지급준비가능, item.ReadinessCode);
        Assert.True(item.IsReadyForPayoutPreparation);
        Assert.Equal(42000m, result.Value.ReadyForPayoutPreparationTotal);
    }

    [Fact]
    public async Task 화주수납완료는_기사정산계좌_미확인과_기사지급완료를_대신하지_않는다()
    {
        await using var db = CreateContext();
        await SeedDriverAsync(db, "driver-a");
        await SeedCompletedTransportAsync(
            db,
            "driver-a",
            "request-a",
            new DateTime(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc),
            expectedPayout: 42000m,
            paymentStatus: 상태값.결제상태.결제완료);
        db.Set<기사정산계좌>().Add(new 기사정산계좌
        {
            기사Id = "driver-a",
            국가코드 = "KR",
            은행명 = "국민은행",
            예금주명 = "기사 A",
            계좌번호 = "1234567890",
            확인상태 = 기사정산계좌확인상태.미확인
        });
        await db.SaveChangesAsync();
        var useCase = new 기사지급준비UseCase(db);

        var result = await useCase.월별조회Async("driver-a", 2026, 7);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal(기사지급준비상태코드.정산계좌미확인, item.ReadinessCode);
        Assert.False(item.IsReadyForPayoutPreparation);
        Assert.Equal(0m, result.Value.ReadyForPayoutPreparationTotal);
    }

    [Fact]
    public async Task 현장지급은_현장수금확인을_표시하고_플랫폼지급준비액에_포함하지_않는다()
    {
        await using var db = CreateContext();
        await SeedDriverAsync(db, "driver-a");
        await SeedCompletedTransportAsync(
            db,
            "driver-a",
            "request-onsite",
            new DateTime(2026, 7, 21, 8, 30, 0, DateTimeKind.Utc),
            expectedPayout: 38000m,
            paymentStatus: 상태값.결제상태.결제대기,
            settlementTiming: 정산시점.현장지급.ToString(),
            onSiteCollectedAtUtc: new DateTime(2026, 7, 21, 9, 0, 0, DateTimeKind.Utc));
        var useCase = new 기사지급준비UseCase(db);

        var result = await useCase.월별조회Async("driver-a", 2026, 7);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal(기사지급준비상태코드.현장수금확인, item.ReadinessCode);
        Assert.False(item.IsReadyForPayoutPreparation);
        Assert.Equal(38000m, result.Value.OnSiteCollectionConfirmedTotal);
        Assert.Equal(0m, result.Value.ReadyForPayoutPreparationTotal);
    }

    [Fact]
    public async Task 지급예정운임이_없으면_화주결제금액을_기사금액으로_추정하지_않는다()
    {
        await using var db = CreateContext();
        await SeedDriverAsync(db, "driver-a");
        await SeedCompletedTransportAsync(
            db,
            "driver-a",
            "request-a",
            new DateTime(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc),
            expectedPayout: null,
            paymentStatus: 상태값.결제상태.결제완료,
            shipperPaymentAmount: 65000);
        var useCase = new 기사지급준비UseCase(db);

        var result = await useCase.월별조회Async("driver-a", 2026, 7);

        var item = Assert.Single(result.Value.Items);
        Assert.Null(item.ExpectedPayoutAmount);
        Assert.Equal(기사지급준비상태코드.지급예정운임없음, item.ReadinessCode);
        Assert.Equal(0m, result.Value.ExpectedPayoutTotal);
    }

    [Fact]
    public async Task 다른_기사와_다른_월의_완료운송은_조회하지_않는다()
    {
        await using var db = CreateContext();
        await SeedDriverAsync(db, "driver-a");
        await SeedDriverAsync(db, "driver-b");
        await SeedCompletedTransportAsync(
            db,
            "driver-a",
            "request-a",
            new DateTime(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc),
            42000m,
            상태값.결제상태.결제완료);
        await SeedCompletedTransportAsync(
            db,
            "driver-a",
            "request-june",
            new DateTime(2026, 6, 20, 8, 30, 0, DateTimeKind.Utc),
            31000m,
            상태값.결제상태.결제완료);
        await SeedCompletedTransportAsync(
            db,
            "driver-b",
            "request-b",
            new DateTime(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc),
            50000m,
            상태값.결제상태.결제완료);
        var useCase = new 기사지급준비UseCase(db);

        var result = await useCase.월별조회Async("driver-a", 2026, 7);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal("request-a", item.RequestId);
    }

    private static async Task SeedDriverAsync(SsalddelContext db, string driverId)
    {
        db.용달기사.Add(new 용달기사
        {
            기사Id = driverId,
            기사명 = driverId,
            연락처 = "010-0000-0000",
            차량 = "1톤 카고",
            주_활동지역 = "서울"
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCompletedTransportAsync(
        SsalddelContext db,
        string driverId,
        string requestId,
        DateTime completedAtUtc,
        decimal? expectedPayout,
        string paymentStatus,
        string? settlementTiming = null,
        DateTime? onSiteCollectedAtUtc = null,
        int shipperPaymentAmount = 65000)
    {
        db.운송원장.Add(new 운송원장
        {
            운송번호 = requestId,
            의뢰Id = requestId,
            화주Id = "shipper-a",
            원본의뢰Id = requestId,
            확정기사Id = driverId,
            기사_운송자 = driverId,
            상태 = "인수완료",
            도착 = completedAtUtc,
            운임 = shipperPaymentAmount,
            UpdatedAt = completedAtUtc
        });
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = requestId,
            화주Id = "shipper-a",
            결제예정금액 = shipperPaymentAmount,
            결제상태 = paymentStatus,
            정산상태 = paymentStatus == 상태값.결제상태.결제완료
                ? 운임정산상태.입금확인완료.ToString()
                : 운임정산상태.입금대기.ToString(),
            정산시점 = settlementTiming ?? 정산시점.운송완료후정산.ToString(),
            현장수금확인일시 = onSiteCollectedAtUtc
        });
        db.운임구성.Add(new 운임구성
        {
            의뢰Id = requestId,
            최종운임 = shipperPaymentAmount,
            기사지급예정운임 = expectedPayout,
            UpdatedAt = completedAtUtc
        });
        await db.SaveChangesAsync();
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
