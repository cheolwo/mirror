using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;

namespace Ssalddel.Tests.Services.Food;

public sealed class InMemorySsalddelFoodOrderStoreTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unsupported-food-status")]
    public void 잘못된저장상태는_수락거절취소배차연결을_변경하지않는다(string raw)
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var order = store.AddOrder(CreateOrderRequest());
        var entity = StoredOrder(store, order.주문번호);
        entity.상태 = raw;
        var historyCount = entity.상태이력.Count;
        Assert.Throws<InvalidOperationException>(() => store.음식점수락멱등(order.주문번호,
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid() }, "restaurant-user"));
        Assert.Throws<InvalidOperationException>(() => store.음식점진행변경(order.주문번호,
            new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.거절, 사유 = "재료 품절" }, "restaurant-user"));
        Assert.Throws<InvalidOperationException>(() => store.주문자취소(order.주문번호,
            new 주문자음식주문취소요청 { 클라이언트요청Id = Guid.NewGuid(), 사유Code = "ChangedMind" }, "orderer-1"));
        Assert.Throws<InvalidOperationException>(() => store.배차대기반영(order.주문번호, 41, DateTime.UtcNow));
        Assert.Equal(raw, entity.상태);
        Assert.Equal(음식주문배차상태코드.미요청, entity.배차상태);
        Assert.Null(entity.배차대기Id);
        Assert.Equal(historyCount, entity.상태이력.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unsupported-food-status")]
    public void 음식점진행Policy는_미지원raw상태를_주문대기로처리하지않는다(string? raw)
        => Assert.Throws<InvalidOperationException>(() => 음식점주문진행Policy.판정(raw,
            new 음식점주문진행변경요청 { 작업 = 음식점주문진행작업코드.거절, 사유 = "재료 품절" }));

    [Fact]
    public void 과거주문접수별칭의_정상수락은_유지한다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var order = store.AddOrder(CreateOrderRequest());
        StoredOrder(store, order.주문번호).상태 = " 주문접수 ";
        Assert.Equal(음식주문상태코드.주문확인, store.음식점수락멱등(order.주문번호,
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid() }, "restaurant-user")!.주문.상태);
    }

    [Theory]
    [InlineData(음식주문상태코드.기사배정, 음식주문배차상태코드.기사배정)]
    [InlineData(음식주문상태코드.조리중, 음식주문배차상태코드.기사배정)]
    [InlineData(음식주문상태코드.픽업완료, 음식주문배차상태코드.기사배정)]
    [InlineData(음식주문상태코드.전달완료, 음식주문배차상태코드.배달완료)]
    [InlineData(음식주문상태코드.수령확인, 음식주문배차상태코드.배달완료)]
    [InlineData(음식주문상태코드.취소, 음식주문배차상태코드.배차대기)]
    public void 같은배차결속은_진행과최초시각을보존하고_다른큐로바꾸지않는다(string status, string dispatchStatus)
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호, new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid() }, "restaurant-user");
        var requestedAt = DateTime.UtcNow.AddMinutes(-10);
        store.배차대기반영(order.주문번호, 41, requestedAt);
        var entity = StoredOrder(store, order.주문번호);
        entity.상태 = status;
        entity.배차상태 = dispatchStatus;
        var changedAt = entity.최근변경시각Utc;
        var historyCount = entity.상태이력.Count;
        var same = store.배차대기반영(order.주문번호, 41, DateTime.UtcNow)!;
        Assert.Equal(status, same.상태);
        Assert.Equal(dispatchStatus, same.배차상태);
        Assert.Equal(requestedAt, same.배차요청시각Utc);
        Assert.Throws<InvalidOperationException>(() => store.배차대기반영(order.주문번호, 42, DateTime.UtcNow));
        Assert.Equal(changedAt, entity.최근변경시각Utc);
        Assert.Equal(historyCount, entity.상태이력.Count);
        Assert.Equal(41, entity.배차대기Id);
    }

    private static 음식주문응답 StoredOrder(InMemorySsalddelFoodOrderStore store, string orderNo)
        => Assert.IsType<List<음식주문응답>>(typeof(InMemorySsalddelFoodOrderStore)
            .GetField("_orders", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(store)).Single(x => x.주문번호 == orderNo);

    [Fact]
    public void 음식점수락은_주문상태와_음식점정보를_갱신한다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var order = store.AddOrder(CreateOrderRequest());

        var accepted = store.음식점수락(
            order.주문번호,
            new 음식점주문수락요청
            {
                음식점명 = "살뜰분식",
                음식점주소 = "서울특별시 마포구 월드컵북로 1",
                음식점상세주소 = "1층",
                조리예상분 = 20,
                수락메모 = "바로 조리 시작"
            });

        Assert.NotNull(accepted);
        Assert.Equal(음식주문상태코드.주문확인, accepted.상태);
        Assert.Equal("살뜰분식", accepted.음식점명);
        Assert.Equal("서울특별시 마포구 월드컵북로 1", accepted.음식점주소);
        Assert.Equal(음식주문배차상태코드.미요청, accepted.배차상태);
        Assert.NotNull(accepted.음식점수락시각Utc);
        Assert.Null(accepted.조리예상완료시각Utc);
        Assert.Null(accepted.조리시작시각Utc);
        Assert.Equal(20, accepted.조리예상분);
        Assert.Contains(accepted.상태이력, x => x.다음상태 == 음식주문상태코드.주문확인);
    }

    [Fact]
    public void 음식점수락후_배차대기반영은_배차상태와_대기Id를_갱신한다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락(
            order.주문번호,
            new 음식점주문수락요청
            {
                음식점명 = "살뜰분식",
                음식점주소 = "서울특별시 마포구 월드컵북로 1",
                즉시픽업가능여부 = true
            });

        var dispatchRequestedAt = DateTime.UtcNow;
        var updated = store.배차대기반영(order.주문번호, 1234, dispatchRequestedAt);

        Assert.NotNull(updated);
        Assert.Equal(음식주문상태코드.주문확인, updated.상태);
        Assert.Equal(음식주문배차상태코드.배차대기, updated.배차상태);
        Assert.Equal(1234, updated.배차대기Id);
        Assert.Equal(dispatchRequestedAt, updated.배차요청시각Utc);
    }

    [Fact]
    public void 같은주문자와클라이언트요청Id는_기존주문을반환한다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var request = CreateOrderRequest();

        var first = store.AddOrder(request);
        request.상품목록 =
        [
            new 음식주문상품Dto
            {
                메뉴Id = 999,
                상품명 = "재시도에서 바뀐 표시값",
                수량 = 9,
                단가 = 1
            }
        ];
        var retried = store.AddOrder(request);

        Assert.Equal(first.주문번호, retried.주문번호);
        Assert.Equal(first.클라이언트요청Id, retried.클라이언트요청Id);
        Assert.Equal(2, Assert.Single(retried.상품목록).수량);
    }

    [Fact]
    public void 음식점진행은_거절과조리시간변경과픽업준비의허용상태를검증한다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var rejectedOrder = store.AddOrder(CreateOrderRequest());

        var rejected = store.음식점진행변경(
            rejectedOrder.주문번호,
            new 음식점주문진행변경요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                작업 = 음식점주문진행작업코드.거절,
                사유 = "재료 품절"
            },
            "restaurant-user");

        Assert.Equal(음식주문상태코드.거절, rejected?.주문.상태);
        Assert.Equal(
            "restaurant-user",
            rejected?.주문.상태이력.Last().처리UserId);

        var cookingOrder = store.AddOrder(CreateOrderRequest());
        store.음식점수락(
            cookingOrder.주문번호,
            new 음식점주문수락요청
            {
                음식점명 = "살뜰분식",
                조리예상분 = 20
            });
        var changed = store.음식점진행변경(
            cookingOrder.주문번호,
            new 음식점주문진행변경요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                작업 = 음식점주문진행작업코드.조리시간변경,
                조리예상분 = 35
            },
            "restaurant-user");
        Assert.Throws<InvalidOperationException>(() => store.음식점진행변경(
            cookingOrder.주문번호,
            new 음식점주문진행변경요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                작업 = 음식점주문진행작업코드.픽업준비
            },
            "restaurant-user"));

        Assert.Equal(음식주문상태코드.주문확인, changed?.주문.상태);
        Assert.Contains("35분", changed?.주문.상태이력.Last().사유);
        Assert.Null(changed?.주문.조리예상완료시각Utc);
    }

    [Fact]
    public void 음식점진행_같은클라이언트요청Id는_상태이력을중복하지않는다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var order = store.AddOrder(CreateOrderRequest());
        var request = new 음식점주문진행변경요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            작업 = 음식점주문진행작업코드.거절,
            사유 = "영업 종료"
        };

        var first = store.음식점진행변경(order.주문번호, request, "restaurant-user");
        var retried = store.음식점진행변경(order.주문번호, request, "restaurant-user");

        Assert.True(first?.새로변경됨);
        Assert.False(retried?.새로변경됨);
        Assert.Single(
            retried!.주문.상태이력,
            history => history.클라이언트요청Id == request.클라이언트요청Id);
    }

    private static 음식주문등록요청 CreateOrderRequest()
        => new()
        {
            클라이언트요청Id = Guid.NewGuid(),
            음식점Id = 42,
            주문자UserId = "orderer-1",
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = "홍길동",
                연락처 = "010-0000-0000",
                주소 = "서울특별시 마포구 양화로 10",
                상세주소 = "101호",
                주문자본인수령여부 = true
            },
            상품목록 =
            [
                new 음식주문상품Dto
                {
                    메뉴Id = 101,
                    상품명 = "김밥",
                    수량 = 2,
                    단가 = 4500
                }
            ],
            결제수단 = "카드"
        };
}
