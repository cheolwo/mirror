using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Admin.Operations;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.설정;
using 살뜰.도메인.운영;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.Admin.Operations;

public sealed class 운영후속처리복구UseCaseTests
{
    [Fact]
    public async Task 목록은_원본식별자와오류본문을노출하지않고_복구책임만보여준다()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        db.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
        {
            멱등키 = "secret-orderer-key",
            동기화유형 = 음식마트원장동기화유형코드.음식주문,
            원천Id = "FOOD-PRIVATE-100",
            변경자 = "private-user-id",
            PayloadJson = "{\"address\":\"private-address\"}",
            처리상태 = OutboxProcessingStatuses.Failed,
            시도횟수 = OutboxProcessingPolicy.MaximumAttempts,
            마지막오류 = "database password was accidentally included",
            CreatedAtUtc = now.AddMinutes(-5),
            UpdatedAtUtc = now.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var useCase = CreateUseCase(db);
        var result = await useCase.목록조회Async();

        var item = Assert.Single(result.항목);
        Assert.Equal(OperatingSystemIds.FoodDelivery, item.현재책임운영체제Id);
        Assert.Equal(운영후속처리복구상태Codes.운영자확인필요, item.상태Code);
        Assert.True(item.재시도예약가능);
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("FOOD-PRIVATE-100", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-user-id", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-address", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 운영체제인계는_현재책임운영체제를보존하고_처리기없는항목을재시도불가로표시한다()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        db.운영체제업무인계.Add(new 운영체제업무인계
        {
            인계StableId = "handoff:100",
            생성멱등Key = "handoff-key",
            출발운영체제Id = OperatingSystemIds.WarehouseCommerceFulfillment,
            도착운영체제Id = OperatingSystemIds.FoodDelivery,
            현재책임운영체제Id = OperatingSystemIds.WarehouseCommerceFulfillment,
            출발업무유형Code = "LastMileDelivery",
            출발업무StableId = "private-work-id",
            상태Code = "Requested",
            요청시각Utc = now.AddMinutes(-10),
            만료시각Utc = now.AddMinutes(20),
            CreatedAt = now.AddMinutes(-10),
            UpdatedAt = now.AddMinutes(-10)
        });
        db.운영체제업무인계Outbox.Add(new 운영체제업무인계Outbox
        {
            멱등Key = "handoff-outbox-key",
            인계StableId = "handoff:100",
            이벤트Type = "OperatingSystemHandoffRequested",
            PayloadJson = "{\"workId\":\"private-work-id\"}",
            처리상태Code = 운영체제업무인계Outbox상태Codes.대기,
            CreatedAt = now.AddMinutes(-10),
            UpdatedAt = now.AddMinutes(-10)
        });
        await db.SaveChangesAsync();

        var result = await CreateUseCase(db).목록조회Async();

        var item = Assert.Single(result.항목);
        Assert.Equal(OperatingSystemIds.WarehouseCommerceFulfillment, item.현재책임운영체제Id);
        Assert.True(item.운영자확인필요);
        Assert.False(item.재시도예약가능);
        Assert.DoesNotContain("private-work-id", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task 실패항목재시도는_원업무를건드리지않고_시도이력을보존한채예약한다()
    {
        await using var db = CreateContext();
        var item = new 음식마트원장동기화Outbox
        {
            멱등키 = "food-ledger:101",
            동기화유형 = 음식마트원장동기화유형코드.음식주문,
            원천Id = "FOOD-101",
            변경자 = "system",
            PayloadJson = "{}",
            처리상태 = OutboxProcessingStatuses.Failed,
            시도횟수 = OutboxProcessingPolicy.MaximumAttempts,
            마지막오류 = "recorded error",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        db.음식마트원장동기화Outbox.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateUseCase(db).재시도예약Async(
            운영후속처리복구원천Codes.음식마트원장동기화,
            item.Id,
            new 운영후속처리재시도요청Dto { 예상처리시도수 = item.시도횟수 });

        Assert.NotNull(result);
        Assert.Equal(OutboxProcessingStatuses.Pending, item.처리상태);
        Assert.Equal(OutboxProcessingPolicy.MaximumAttempts, item.시도횟수);
        Assert.Equal("recorded error", item.마지막오류);
        Assert.True(item.UpdatedAtUtc <= DateTime.UtcNow - OutboxProcessingPolicy.RetryDelay);
    }

    [Fact]
    public async Task 재시도요청은_같은시도횟수로중복실행할수없다()
    {
        await using var db = CreateContext();
        var item = new 음식마트원장동기화Outbox
        {
            멱등키 = "food-ledger:102",
            동기화유형 = 음식마트원장동기화유형코드.음식주문,
            원천Id = "FOOD-102",
            변경자 = "system",
            PayloadJson = "{}",
            처리상태 = OutboxProcessingStatuses.Failed,
            시도횟수 = 5,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        db.음식마트원장동기화Outbox.Add(item);
        await db.SaveChangesAsync();
        var useCase = CreateUseCase(db);
        var request = new 운영후속처리재시도요청Dto { 예상처리시도수 = 5 };

        await useCase.재시도예약Async(
            운영후속처리복구원천Codes.음식마트원장동기화,
            item.Id,
            request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.재시도예약Async(
            운영후속처리복구원천Codes.음식마트원장동기화,
            item.Id,
            request));
    }

    private static 운영후속처리복구UseCase CreateUseCase(SsalddelContext db)
    {
        var sync = new NoopLedgerSync();
        var outbox = new 음식마트원장동기화OutboxService(
            db,
            sync,
            NullLogger<음식마트원장동기화OutboxService>.Instance);
        return new 운영후속처리복구UseCase(db, outbox);
    }

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options,
            new PassThroughEncryption());

    private sealed class NoopLedgerSync : I음식마트원장Mongo동기화Service
    {
        public Task<커뮤니티원장Dto?> 음식주문동기화Async(
            음식주문응답 주문,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(new 커뮤니티원장Dto());

        public Task<커뮤니티원장Dto?> 출고원장동기화Async(
            IReadOnlyList<출고예정> 출고목록,
            IReadOnlyList<입고요청> 입고목록,
            string updatedBy,
            string? 현재단계Key = null,
            string? 원장템플릿Key = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(new 커뮤니티원장Dto());
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
