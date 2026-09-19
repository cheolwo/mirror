using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Admin.Finance;
using Ssalddel.Application.Finance;
using Ssalddel.Application.Shipper.Payment;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Contracts.Admin.Finance;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Domain.운영;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.정산;

namespace Ssalddel.Tests.Application.Finance;

public sealed class 재무영향원장PipelineTests
{
    [Fact]
    public void 모든Profile의관리계정이Catalog에존재하고_운영전표쓰기는허용하지않는다()
    {
        var profiles = 재무영향ProfileCatalog.GetAll();

        Assert.NotEmpty(profiles);
        Assert.Equal(profiles.Count, profiles.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(profiles, profile =>
        {
            Assert.False(profile.OperationalPostingAllowed);
            Assert.False(string.IsNullOrWhiteSpace(profile.MappingRevision));
            Assert.All(profile.Accounts, account =>
                Assert.NotNull(관리계정Catalog.Find(account.ManagementAccountStableId)));
        });
        Assert.All(관리계정Catalog.GetAll(), account => Assert.False(account.IsActualAccountingAccountApproved));
    }

    [Fact]
    public void 결제Command는_승인된ProfileStableId를설명Metadata로가진다()
    {
        var attribute = typeof(공통결제승인Command)
            .GetCustomAttribute<Ssalddel재무영향ProfileAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(재무영향ProfileIds.결제승인, attribute.ProfileStableId);
        Assert.NotNull(재무영향ProfileCatalog.Find(attribute.ProfileStableId));
    }

    [Fact]
    public async Task 결제승인Event는_중립관리계정두줄로멱등투영된다()
    {
        await using var db = CreateContext();
        var projector = new 결제승인재무원장ProjectorEventHandler(
            db,
            TimeProvider.System,
            NullLogger<결제승인재무원장ProjectorEventHandler>.Instance);
        var notification = CreatePaymentApprovedEvent();

        await projector.Handle(notification, CancellationToken.None);
        await projector.Handle(notification, CancellationToken.None);

        var item = await db.재무사건
            .Include(x => x.관리계정전기목록)
            .Include(x => x.증빙목록)
            .SingleAsync();
        Assert.Equal(재무의미Codes.고객결제승인, item.재무의미Code);
        Assert.Equal(12_300m, item.금액);
        Assert.Equal("KRW", item.통화Code);
        Assert.Null(item.현금이동일시Utc);
        Assert.Equal(2, item.관리계정전기목록.Count);
        Assert.Contains(item.관리계정전기목록, line =>
            line.관리계정StableId == 관리계정StableIds.Pg미수
            && line.전기방향Code == "DebitCandidate");
        Assert.Contains(item.관리계정전기목록, line =>
            line.관리계정StableId == 관리계정StableIds.고객결제정산대기
            && line.전기방향Code == "CreditCandidate");
        Assert.Single(item.증빙목록);
        Assert.Empty(db.재무대사예외);
    }

    [Fact]
    public async Task 같은원본StableId의내용이달라지면_덮어쓰지않고대사예외를남긴다()
    {
        await using var db = CreateContext();
        var projector = new 결제승인재무원장ProjectorEventHandler(
            db,
            TimeProvider.System,
            NullLogger<결제승인재무원장ProjectorEventHandler>.Instance);
        var notification = CreatePaymentApprovedEvent();
        await projector.Handle(notification, CancellationToken.None);

        await projector.Handle(notification with { 결제금액 = 99_999 }, CancellationToken.None);

        Assert.Equal(12_300m, (await db.재무사건.SingleAsync()).금액);
        var exception = await db.재무대사예외.SingleAsync();
        Assert.Equal("FinancialEventSourceConflict", exception.예외Code);
        Assert.Equal(재무대사예외상태Codes.확인필요, exception.상태Code);
    }

    [Fact]
    public async Task 원장기반손익평가는_총거래액을요청이아닌서버사건에서가져온다()
    {
        await using var db = CreateContext();
        var projector = new 결제승인재무원장ProjectorEventHandler(
            db,
            TimeProvider.System,
            NullLogger<결제승인재무원장ProjectorEventHandler>.Instance);
        await projector.Handle(CreatePaymentApprovedEvent(), CancellationToken.None);
        var useCase = new 운영재무조회UseCase(db, new 플랫폼운영경제성Calculator());
        var request = new 플랫폼운영경제성원장평가요청Dto
        {
            시나리오StableId = "scenario:ledger:test",
            국가Code = "KR",
            관할Code = "KR-11-260",
            통화Code = "krw",
            기간시작일 = new DateOnly(2026, 9, 1),
            기간종료일 = new DateOnly(2026, 9, 30),
            완료주문수 = 1,
            기초가용현금 = 100_000m,
            본인대리인가정Code = 플랫폼운영경제성본인대리인가정Codes.미정,
            시나리오항목 =
            [
                Item("revenue", 플랫폼운영경제성금액분류Codes.주문수익후보, 1_000m),
                Item("cost", 플랫폼운영경제성금액분류Codes.고정비용, 500m)
            ]
        };

        var response = await useCase.원장기반경제성평가Async(request, CancellationToken.None);

        Assert.Equal(12_300m, response.평가.총거래액);
        Assert.False(string.IsNullOrWhiteSpace(response.원장SnapshotHash));
        Assert.False(response.평가.운영전표쓰기허용);
    }

    [Fact]
    public async Task 원장기반손익평가요청은_총거래액수동덮어쓰기를거절한다()
    {
        await using var db = CreateContext();
        var useCase = new 운영재무조회UseCase(db, new 플랫폼운영경제성Calculator());
        var request = new 플랫폼운영경제성원장평가요청Dto
        {
            시나리오StableId = "scenario:ledger:test",
            국가Code = "KR",
            관할Code = "KR-11-260",
            통화Code = "KRW",
            기간시작일 = new DateOnly(2026, 9, 1),
            기간종료일 = new DateOnly(2026, 9, 30),
            완료주문수 = 1,
            시나리오항목 = [Item("manual-gmv", 플랫폼운영경제성금액분류Codes.총거래액, 1m)]
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.원장기반경제성평가Async(request, CancellationToken.None));
    }

    [Fact]
    public async Task 현금흐름요약은_현금이동과미수미지급의무를분리하고_기초잔액을추정하지않는다()
    {
        await using var db = CreateContext();
        AddFinancialEvent(
            db,
            "cash-in",
            new DateTime(2026, 9, 18, 1, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 18, 2, 0, 0, DateTimeKind.Utc),
            (관리계정StableIds.가용현금, "DebitCandidate", 150_000m));
        AddFinancialEvent(
            db,
            "cash-out",
            new DateTime(2026, 9, 18, 3, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 18, 4, 0, 0, DateTimeKind.Utc),
            (관리계정StableIds.가용현금, "CreditCandidate", 40_000m));
        AddFinancialEvent(
            db,
            "receivable",
            new DateTime(2026, 9, 18, 5, 0, 0, DateTimeKind.Utc),
            null,
            (관리계정StableIds.Pg미수, "DebitCandidate", 200_000m));
        AddFinancialEvent(
            db,
            "payables",
            new DateTime(2026, 9, 18, 6, 0, 0, DateTimeKind.Utc),
            null,
            (관리계정StableIds.기사정상수행대금미지급, "CreditCandidate", 70_000m),
            (관리계정StableIds.고객환불의무, "CreditCandidate", 20_000m));
        await db.SaveChangesAsync();
        var useCase = new 운영재무조회UseCase(db, new 플랫폼운영경제성Calculator());

        var summary = await useCase.현금흐름요약조회Async(
            "krw",
            new DateOnly(2026, 9, 18),
            new DateOnly(2026, 9, 18),
            CancellationToken.None);

        Assert.Equal("KRW", summary.통화Code);
        Assert.Equal(150_000m, summary.현금유입합계);
        Assert.Equal(40_000m, summary.현금유출합계);
        Assert.Equal(110_000m, summary.순현금변동);
        Assert.Equal(200_000m, summary.기간미수순변동후보);
        Assert.Equal(90_000m, summary.기간지급의무순변동후보);
        Assert.Equal(20_000m, summary.기간고객환불의무순변동후보);
        Assert.Equal(2, summary.현금이동사건수);
        Assert.Equal(4, summary.기간재무사건수);
        Assert.False(summary.가용현금잔액확정가능여부);
        Assert.Equal("OpeningBalanceNotIncluded", summary.가용현금잔액제한Code);
        Assert.False(summary.운영전표쓰기허용);
        Assert.False(string.IsNullOrWhiteSpace(summary.원장SnapshotHash));
    }

    [Fact]
    public async Task 현금흐름요약은_잘못된기간을거절한다()
    {
        await using var db = CreateContext();
        var useCase = new 운영재무조회UseCase(db, new 플랫폼운영경제성Calculator());

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.현금흐름요약조회Async(
            "KRW",
            new DateOnly(2026, 9, 19),
            new DateOnly(2026, 9, 18),
            CancellationToken.None));
    }

    private static 결제승인완료Event CreatePaymentApprovedEvent()
        => new(
            7001,
            "payment-7001",
            1,
            "order-7001",
            1,
            12_300,
            "krw",
            new DateTime(2026, 9, 17, 3, 30, 0, DateTimeKind.Utc));

    private static 플랫폼운영경제성금액항목Dto Item(string stableId, string classificationCode, decimal amount)
        => new()
        {
            StableId = stableId,
            분류Code = classificationCode,
            금액 = amount,
            근거Revision = "test-r1"
        };

    private static void AddFinancialEvent(
        SsalddelContext db,
        string stableId,
        DateTime occurredAtUtc,
        DateTime? cashMovedAtUtc,
        params (string AccountStableId, string SideCode, decimal Amount)[] lines)
    {
        var financialEvent = new 재무사건
        {
            StableId = stableId,
            원본Event유형 = "TestEvent",
            원본StableId = $"source:{stableId}",
            원본Revision = 1,
            재무영향ProfileStableId = "test-profile",
            재무의미Code = "TestMeaning",
            재무영향유형Code = "TestImpact",
            금액 = lines.Sum(line => line.Amount),
            통화Code = "KRW",
            업무발생일시Utc = occurredAtUtc,
            현금이동일시Utc = cashMovedAtUtc,
            증빙Hash = $"hash:{stableId}",
            CreatedAtUtc = occurredAtUtc,
            UpdatedAtUtc = occurredAtUtc
        };
        var lineNumber = 1;
        foreach (var line in lines)
        {
            financialEvent.관리계정전기목록.Add(new 관리계정전기
            {
                LineNumber = lineNumber++,
                관리계정StableId = line.AccountStableId,
                전기방향Code = line.SideCode,
                금액 = line.Amount,
                통화Code = "KRW",
                매핑Revision = "test-r1",
                CreatedAtUtc = occurredAtUtc
            });
        }

        db.재무사건.Add(financialEvent);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"financial-impact-ledger-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
