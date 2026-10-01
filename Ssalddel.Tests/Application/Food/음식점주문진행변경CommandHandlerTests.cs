using MediatR;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Events;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식점주문진행변경CommandHandlerTests
{
    [Fact]
    public async Task 거절은_인증된처리자와사유를이벤트에남긴다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var publisher = new RecordingPublisher();
        var handler = new 음식점주문진행변경CommandHandler(store, publisher);
        var order = store.AddOrder(CreateOrderRequest());

        var result = await handler.Handle(
            new 음식점주문진행변경Command(
                order.주문번호,
                new 음식점주문진행변경요청
                {
                    클라이언트요청Id = Guid.NewGuid(),
                    작업 = 음식점주문진행작업코드.거절,
                    사유 = "재료 품절"
                },
                " restaurant-user "),
            CancellationToken.None);

        Assert.Equal(음식주문상태코드.거절, result?.상태);
        var notification = Assert.IsType<음식점주문진행변경됨Event>(
            Assert.Single(publisher.Notifications));
        Assert.Equal("restaurant-user", notification.처리UserId);
        Assert.Contains("재료 품절", notification.변경사유);
    }

    [Fact]
    public async Task 같은요청재시도는_상태변경이벤트를다시발행하지않는다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var publisher = new RecordingPublisher();
        var handler = new 음식점주문진행변경CommandHandler(store, publisher);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락(
            order.주문번호,
            new 음식점주문수락요청
            {
                음식점명 = "살뜰분식",
                조리예상분 = 20
            });
        var command = new 음식점주문진행변경Command(
            order.주문번호,
            new 음식점주문진행변경요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                작업 = 음식점주문진행작업코드.조리시간변경,
                조리예상분 = 30
            },
            "restaurant-user");

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        Assert.Single(publisher.Notifications);
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
                주소 = "서울특별시 마포구 양화로 10"
            },
            상품목록 =
            [
                new 음식주문상품Dto
                {
                    메뉴Id = 101,
                    상품명 = "김밥",
                    수량 = 1,
                    단가 = 4500
                }
            ]
        };

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Notifications { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
