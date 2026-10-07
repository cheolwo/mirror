using Ssalddel.Application.Food;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Ssalddel.WorkflowRules.Contracts;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식배달수명주기SnapshotTests
{
    [Fact]
    public void 재조리사본은_이전음식의준비와픽업을_현재진행으로표시하지않는다()
    {
        var oldReady = DateTime.UtcNow.AddMinutes(-10);
        var recook = oldReady.AddMinutes(5);
        var order = new 음식주문응답
        {
            주문번호 = "FOOD-RECOOK", Revision = 7, 상태 = 음식주문상태코드.조리중,
            픽업준비시각Utc = oldReady, CurrentPreparationRound = 2,
            RecookingRequestedAtUtc = recook, CurrentCookingStartedAtUtc = recook,
            상태이력 = [new() { 다음상태 = 음식주문상태코드.픽업완료, 전이시각Utc = oldReady.AddMinutes(1) }]
        };
        var waiting = 음식배달수명주기SnapshotFactory.FromOperationalOrder(order, "r7");
        Assert.Null(waiting.ReadyForPickupAtUtc);
        Assert.Null(waiting.PickedUpAtUtc);
        Assert.Equal(oldReady, order.픽업준비시각Utc);

        order.CurrentPickupReadyAtUtc = recook.AddMinutes(2);
        order.상태이력 = order.상태이력.Append(new 음식주문상태전이기록Dto
        {
            다음상태 = 음식주문상태코드.픽업완료, 전이시각Utc = recook.AddMinutes(3)
        }).ToArray();
        var pickedUp = 음식배달수명주기SnapshotFactory.FromOperationalOrder(order, "r9");
        Assert.Equal(order.CurrentPickupReadyAtUtc, pickedUp.ReadyForPickupAtUtc);
        Assert.Equal(recook.AddMinutes(3), pickedUp.PickedUpAtUtc);
    }

    [Fact]
    public void 운영서버사본은_상태이력시각과출처를보존한다()
    {
        var ready = DateTime.UtcNow.AddMinutes(-2);
        var snapshot = 음식배달수명주기SnapshotFactory.FromOperationalOrder(new 음식주문응답
        {
            주문번호 = "FOOD-1", Revision = 3, 음식점Id = 7, 주문자UserId = "orderer",
            상태 = 음식주문상태코드.기사배정, 배차상태 = 음식주문배차상태코드.기사배정,
            픽업준비시각Utc = ready
        }, "server-schema.r1");

        Assert.Equal(음식배달상태원천코드.OperationalServer, snapshot.SourceCode);
        Assert.Equal(ready, snapshot.ReadyForPickupAtUtc);
        Assert.Equal("restaurant:7", snapshot.RestaurantStableId);
    }

    [Fact]
    public async Task 조회UseCase는_주문자소유범위를지키고_확정기사를사본에연결한다()
    {
        await using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(new 음식주문등록요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            음식점Id = 7,
            주문자UserId = "orderer-1",
            수령인정보 = new 음식주문수령인정보Dto { 수령인명 = "주문자", 주소 = "서울 중랑구" },
            상품목록 = [new 음식주문상품Dto { 메뉴Id = 1, 상품명 = "국밥", 수량 = 1, 단가 = 8000 }]
        });
        db.운송원장.Add(new 운송원장
        {
            운송번호 = order.주문번호,
            의뢰Id = order.주문번호,
            원본의뢰Id = order.주문번호,
            원본의뢰유형 = "FoodOrder",
            화주Id = "restaurant:7",
            확정기사Id = "driver-9",
            배차업무유형 = 상태값.배차업무유형.음식배달
        });
        await db.SaveChangesAsync();
        var useCase = new 음식배달수명주기조회UseCase(db, store);

        var denied = await useCase.상세Async(order.주문번호, "other-user", default);
        var snapshot = await useCase.상세Async(order.주문번호, "orderer-1", default);

        Assert.Null(denied);
        Assert.NotNull(snapshot);
        Assert.Equal("driver-9", snapshot!.DriverStableId);
        Assert.StartsWith("food-order:", snapshot.SourceRevision, StringComparison.Ordinal);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"food-lifecycle-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyEncryption());
    }

    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
