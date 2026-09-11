using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식배달완료WorldProjectionTests
{
    [Fact]
    public async Task 정상완료주기는_익명온라인사본으로_한번만발행된다()
    {
        await using var db = CreateContext();
        var order = CompletedOrder("FOOD-PRIVATE-1", includeAllStages: true);
        db.음식주문.Add(order);
        db.운송원장.Add(Transport(order.주문번호, "driver-private-9"));
        db.음식마트원장동기화Outbox.Add(ProjectionRequest(order.주문번호, order.상태이력.Count));
        await db.SaveChangesAsync();
        var service = new 음식배달완료WorldProjectionService(
            db,
            new 음식배달완료WorldAreaResolver(),
            NullLogger<음식배달완료WorldProjectionService>.Instance);

        Assert.Equal(1, await service.대기항목처리Async());
        Assert.Equal(0, await service.대기항목처리Async());

        var stored = Assert.Single(await db.음식배달완료WorldSnapshot.ToListAsync());
        Assert.Equal(음식배달완료WorldAreaStableIds.Myeonmok, stored.AreaStableId);
        Assert.Equal(OutboxProcessingStatuses.Succeeded,
            (await db.음식마트원장동기화Outbox.SingleAsync()).처리상태);

        var response = await new 음식배달완료WorldSnapshot조회UseCase(db)
            .지역목록Async(음식배달완료WorldAreaStableIds.Myeonmok, 100, default);
        var snapshot = Assert.Single(response.Items);
        Assert.False(snapshot.LocalStorageAllowed);
        Assert.False(snapshot.ReplayAllowed);
        Assert.Equal(7, snapshot.Milestones.Length);
        Assert.All(snapshot.Actors.GetType().GetProperties(), property =>
            Assert.StartsWith("actor:synthetic:", property.GetValue(snapshot.Actors)?.ToString(), StringComparison.Ordinal));

        var publicJson = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain(order.주문번호, publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain(order.주문자UserId, publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("driver-private-9", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain(order.수령지주소, publicJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 필수단계가빠진완료주기는_그항목만재시도하고_다음정상항목은발행한다()
    {
        await using var db = CreateContext();
        var incomplete = CompletedOrder("FOOD-INCOMPLETE-1", includeAllStages: false);
        var valid = CompletedOrder("FOOD-VALID-AFTER-FAILURE", includeAllStages: true);
        db.음식주문.AddRange(incomplete, valid);
        db.운송원장.AddRange(
            Transport(incomplete.주문번호, "driver-private-2"),
            Transport(valid.주문번호, "driver-private-3"));
        db.음식마트원장동기화Outbox.AddRange(
            ProjectionRequest(incomplete.주문번호, incomplete.상태이력.Count),
            ProjectionRequest(valid.주문번호, valid.상태이력.Count));
        await db.SaveChangesAsync();
        var service = new 음식배달완료WorldProjectionService(
            db,
            new 음식배달완료WorldAreaResolver(),
            NullLogger<음식배달완료WorldProjectionService>.Instance);

        Assert.Equal(2, await service.대기항목처리Async());

        var snapshot = Assert.Single(await db.음식배달완료WorldSnapshot.ToListAsync());
        Assert.Equal(7, snapshot.LifecycleRevision);
        var failed = await db.음식마트원장동기화Outbox
            .SingleAsync(x => x.원천Id == incomplete.주문번호);
        var succeeded = await db.음식마트원장동기화Outbox
            .SingleAsync(x => x.원천Id == valid.주문번호);
        Assert.Equal(OutboxProcessingStatuses.Pending, failed.처리상태);
        Assert.Equal(1, failed.시도횟수);
        Assert.Contains("필수 단계", failed.마지막오류);
        Assert.Equal(OutboxProcessingStatuses.Succeeded, succeeded.처리상태);
    }

    [Fact]
    public async Task 만료된사본은_지역장면조회에서제외된다()
    {
        await using var db = CreateContext();
        db.음식배달완료WorldSnapshot.Add(new 살뜰.도메인.음식.음식배달완료WorldSnapshot
        {
            원천OutboxId = 77,
            SnapshotStableId = "food-delivery-completed:expired",
            AreaStableId = 음식배달완료WorldAreaStableIds.Junghwa,
            LifecycleRevision = 7,
            OutcomeCode = "ReceiptConfirmed",
            CompletedAtUtc = DateTime.UtcNow.AddHours(-2),
            PublishedAtUtc = DateTime.UtcNow.AddHours(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
            OrdererActorStableId = "actor:synthetic:expired:orderer",
            RestaurantActorStableId = "actor:synthetic:expired:restaurant",
            DriverActorStableId = "actor:synthetic:expired:driver"
        });
        await db.SaveChangesAsync();

        var response = await new 음식배달완료WorldSnapshot조회UseCase(db)
            .지역목록Async(음식배달완료WorldAreaStableIds.Junghwa, 100, default);

        Assert.Empty(response.Items);
    }

    private static 음식주문 CompletedOrder(string orderNo, bool includeAllStages)
    {
        var startedAt = DateTime.UtcNow.AddMinutes(-20);
        var order = new 음식주문
        {
            주문번호 = orderNo,
            음식점Id = 17,
            음식점명 = "비공개 음식점",
            음식점주소 = "서울특별시 중랑구 면목동 1",
            주문자UserId = "orderer-private-1",
            수령인명 = "비공개 주문자",
            수령인연락처 = "010-0000-0000",
            수령지주소 = "서울특별시 중랑구 면목동 2",
            상태 = 음식주문상태코드.수령확인,
            배차상태 = 음식주문배차상태코드.배달완료,
            CreatedAt = startedAt,
            UpdatedAt = startedAt.AddMinutes(18)
        };
        var stages = includeAllStages
            ? new[]
            {
                음식주문상태코드.주문대기,
                음식주문상태코드.조리중,
                음식주문상태코드.픽업대기,
                음식주문상태코드.기사배정,
                음식주문상태코드.픽업완료,
                음식주문상태코드.전달완료,
                음식주문상태코드.수령확인
            }
            : new[] { 음식주문상태코드.주문대기, 음식주문상태코드.수령확인 };
        for (var index = 0; index < stages.Length; index++)
        {
            order.상태이력.Add(new 음식주문상태이력
            {
                이전상태 = index == 0 ? string.Empty : stages[index - 1],
                다음상태 = stages[index],
                사유 = "시험 상태 전이",
                전이시각Utc = startedAt.AddMinutes(index * 3)
            });
        }

        return order;
    }

    private static 운송원장 Transport(string orderNo, string driverId)
        => new()
        {
            운송번호 = $"transport:{orderNo}",
            의뢰Id = orderNo,
            원본의뢰Id = orderNo,
            원본의뢰유형 = "FoodOrder",
            화주Id = "restaurant-private-17",
            확정기사Id = driverId,
            배차업무유형 = 상태값.배차업무유형.음식배달,
            픽업_도로명주소 = "서울특별시 중랑구 면목동 1"
        };

    private static 음식마트원장동기화Outbox ProjectionRequest(string orderNo, int revision)
        => new()
        {
            멱등키 = $"food-delivery-completed-world:{orderNo}:{revision}",
            동기화유형 = 음식마트원장동기화유형코드.음식배달완료WorldProjection,
            원천Id = orderNo,
            변경자 = "FoodDeliveryOS",
            PayloadJson = JsonSerializer.Serialize(new { orderRevision = revision }),
            처리상태 = OutboxProcessingStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"food-delivery-world-projection-{Guid.NewGuid():N}")
                .Options,
            new PassThroughEncryptionService());

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
