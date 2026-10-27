using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.LogisticsProcessing.Warehouse;

public sealed class InboundInspectionCharacterizationR2Tests
{
    [Theory]
    [InlineData(0, -1, 401, "검수수량은 1 이상이어야 합니다.")]
    [InlineData(1, -1, 401, "불량수량은 검수수량보다 클 수 없습니다.")]
    [InlineData(1, 2, 401, "불량수량은 검수수량보다 클 수 없습니다.")]
    [InlineData(100_001, 100_002, 401, "불량수량은 검수수량보다 클 수 없습니다.")]
    [InlineData(100_001, 0, 401, "검수수량은 100,000개 이하여야 합니다.")]
    [InlineData(1, 0, 401, "검수 메모는 400자 이하로 입력해 주세요.")]
    public async Task 입력검증은_수량_불량_상한_메모_인증순서를유지한다(
        int inspectedQuantity,
        int defectiveQuantity,
        int memoLength,
        string expectedMessage)
    {
        await using var db = CreateContext();
        var user = new RecordingCurrentUserAccessor(null);
        var service = new WarehouseOperationService(db, user, null!);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.InspectInboundItemAsync(71, new 입고검수요청
            {
                검수수량 = inspectedQuantity,
                불량수량 = defectiveQuantity,
                검수메모 = new string('가', memoLength)
            }, default));

        Assert.Equal(expectedMessage, error.Message);
        Assert.Equal(0, user.ReadCount);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task null요청은_인증보다먼저_같은매개변수오류를반환한다()
    {
        await using var db = CreateContext();
        var user = new RecordingCurrentUserAccessor(null);
        var service = new WarehouseOperationService(db, user, null!);

        var error = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.InspectInboundItemAsync(71, null!, default));

        Assert.Equal("request", error.ParamName);
        Assert.Equal(0, user.ReadCount);
    }

    [Theory]
    [InlineData(0, "검수완료")]
    [InlineData(2, "검수완료-불량포함")]
    public async Task 검수는_예약수량과기존수량차이_정규메모_동일처리시각을저장한다(
        int defectiveQuantity,
        string expectedStatus)
    {
        await using var db = CreateContext();
        var item = await SeedItemAsync(db);
        item.예약수량 = 3;
        item.가용수량 = 9;
        item.불량수량 = 1;
        await db.SaveChangesAsync();
        var memo = new string('가', 400);
        var service = new WarehouseOperationService(db, new RecordingCurrentUserAccessor("warehouse-owner"), null!);
        var before = DateTime.UtcNow;

        var result = await service.InspectInboundItemAsync(71, new 입고검수요청
        {
            검수수량 = 12,
            불량수량 = defectiveQuantity,
            검수메모 = $"  {memo}\t "
        }, default);

        var after = DateTime.UtcNow;
        var history = await db.재고이력.SingleAsync();
        var movement = await db.재고이동.SingleAsync();
        Assert.Equal(12, item.입고수량);
        Assert.Equal(defectiveQuantity, item.불량수량);
        Assert.Equal(9 - defectiveQuantity, item.가용수량);
        Assert.Equal(3, item.예약수량);
        Assert.Equal(expectedStatus, item.상태);
        Assert.Equal(-defectiveQuantity, history.변경수량);
        Assert.Equal(item.가용수량, history.변경후수량);
        Assert.Equal($"검수 12, 불량 {defectiveQuantity}. {memo}", history.메모);
        Assert.Equal(Math.Abs(defectiveQuantity - 1), movement.수량);
        Assert.Equal(memo, movement.메모);
        Assert.Equal(memo, result.메모);
        Assert.False(result.멱등재시도여부);
        Assert.InRange(result.처리일시, before, after);
        Assert.Equal(result.처리일시, item.UpdatedAt);
        Assert.Equal(result.처리일시, history.처리일시);
        Assert.Equal(result.처리일시, movement.발생일시);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 재시도는_최신처리일시와Id의이력만비교하고_원처리자와시각을유지한다(bool latestMatches)
    {
        await using var db = CreateContext();
        var item = await SeedItemAsync(db);
        item.상태 = "검수완료-불량포함";
        item.불량수량 = 2;
        item.가용수량 = 7;
        item.예약수량 = 20;
        var processedAt = new DateTime(2026, 10, 1, 3, 4, 5, DateTimeKind.Utc);
        db.재고이력.AddRange(
            History(900, processedAt.AddSeconds(-1), "검수 12, 불량 2. 이전"),
            History(100, processedAt, "검수 12, 불량 2. 이전"),
            History(101, processedAt, latestMatches ? "검수 12, 불량 2. 근거" : "검수 12, 불량 2. 근거 "));
        await db.SaveChangesAsync();
        var updatedAt = item.UpdatedAt;
        var service = new WarehouseOperationService(db, new RecordingCurrentUserAccessor("warehouse-owner"), null!);
        var request = new 입고검수요청 { 검수수량 = 12, 불량수량 = 2, 검수메모 = " 근거 " };

        if (latestMatches)
        {
            var result = await service.InspectInboundItemAsync(71, request, default);
            Assert.True(result.멱등재시도여부);
            Assert.Equal("original-inspector", result.처리UserId);
            Assert.Equal(processedAt, result.처리일시);
            Assert.Equal("근거", result.메모);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.InspectInboundItemAsync(71, request, default));
            Assert.Equal("이미 완료된 입고 검수 결과를 다른 수량이나 메모로 다시 저장할 수 없습니다.", error.Message);
        }

        Assert.Equal(3, await db.재고이력.CountAsync());
        Assert.Empty(await db.재고이동.ToArrayAsync());
        Assert.Equal(updatedAt, item.UpdatedAt);
        Assert.Equal(7, item.가용수량);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private static 재고이력 History(long id, DateTime processedAt, string memo)
        => new()
        {
            Id = id,
            입고상품Id = 71,
            이력유형 = "입고검수",
            처리UserId = "original-inspector",
            처리일시 = processedAt,
            메모 = memo
        };

    private static SsalddelContext CreateContext()
        => new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"inbound-inspection-r2-{Guid.NewGuid():N}")
            .Options, new DummyPersonalDataEncryptionService());

    private static async Task<입고상품> SeedItemAsync(SsalddelContext db)
    {
        db.창고.Add(new 창고 { Id = 7, 소유자UserId = "warehouse-owner", 창고명 = "검수 시험 창고", 주소 = "서울" });
        var item = new 입고상품
        {
            Id = 71,
            입고요청Id = 41,
            창고Id = 7,
            상품명 = "감자",
            SKU = "POTATO-01",
            입고수량 = 12,
            가용수량 = 12,
            상태 = "보관중",
            UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.입고상품.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private sealed class RecordingCurrentUserAccessor(string? userId) : ICurrentUserAccessor
    {
        public int ReadCount { get; private set; }
        public string? UserId
        {
            get
            {
                ReadCount++;
                return userId;
            }
        }

        public string? Role => null;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
