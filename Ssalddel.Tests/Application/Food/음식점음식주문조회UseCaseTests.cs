using Ssalddel.Application.Food;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식점음식주문조회UseCaseTests
{
    [Fact]
    public void 목록과상세는_로그인클레임의음식점범위만반환한다()
    {
        var useCase = new 음식점음식주문조회UseCase(
            new InMemorySsalddelFoodOrderStore());

        var orders = useCase.목록(new 음식점주문수신함조회요청(), 101);

        Assert.NotEmpty(orders.Items);
        Assert.All(orders.Items, order => Assert.Equal(101, order.음식점Id));
        var own = useCase.상세(orders.Items[0].주문번호, 101);
        Assert.NotNull(own);
        Assert.True(업무가능행동목록.포함(
            own.AvailableActions,
            음식배달가능행동Ids.음식점주문수락));

        var otherRestaurantOrder = useCase.목록(new 음식점주문수신함조회요청(), 102).Items.First();
        Assert.Null(useCase.상세(otherRestaurantOrder.주문번호, 101));
    }

    [Fact]
    public void 수신함은_미처리의미와최근변경시각을기준으로서버Paging한다()
    {
        var useCase = new 음식점음식주문조회UseCase(
            new InMemorySsalddelFoodOrderStore());

        var firstPage = useCase.목록(
            new 음식점주문수신함조회요청
            {
                처리상태 = 음식점주문수신함처리상태코드.미처리,
                Page = 1,
                PageSize = 1
            },
            101);

        Assert.Equal(2, firstPage.TotalCount);
        Assert.Single(firstPage.Items);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(1, firstPage.PageSize);
        Assert.All(firstPage.Items, item =>
            Assert.True(음식점주문수신함처리상태코드.미처리여부(item.상태)));

        var afterFuture = useCase.목록(
            new 음식점주문수신함조회요청
            {
                처리상태 = 음식점주문수신함처리상태코드.전체,
                UpdatedAfterUtc = DateTime.UtcNow.AddMinutes(1)
            },
            101);

        Assert.Empty(afterFuture.Items);
        Assert.Equal(0, afterFuture.TotalCount);
    }

    [Fact]
    public void 앱이중지된동안등록된주문은_재실행뒤서버수신함에서복구된다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var stoppedAtUtc = DateTime.UtcNow.AddMilliseconds(-1);
        var registered = store.AddOrder(new 음식주문등록요청
        {
            음식점Id = 101,
            주문자UserId = "orderer-offline",
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = "재실행 복구 주문자",
                주소 = "서울시 강서구"
            },
            상품목록 =
            [
                new 음식주문상품Dto
                {
                    상품명 = "비빔밥",
                    수량 = 1,
                    단가 = 9000
                }
            ]
        });
        var restartedUseCase = new 음식점음식주문조회UseCase(store);

        var recovered = restartedUseCase.목록(
            new 음식점주문수신함조회요청
            {
                처리상태 = 음식점주문수신함처리상태코드.미처리,
                UpdatedAfterUtc = stoppedAtUtc,
                Page = 1,
                PageSize = 100
            },
            101);

        Assert.Contains(recovered.Items, order => order.주문번호 == registered.주문번호);
        Assert.DoesNotContain(recovered.Items, order => order.음식점Id != 101);
    }
}
