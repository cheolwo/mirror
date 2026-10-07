using MediatR;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Events;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식주문등록CommandHandlerTests
{
    [Fact]
    public async Task 같은클라이언트요청재시도는_주문등록Event를다시발행하지않는다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var publisher = new RecordingPublisher();
        var handler = new 음식주문등록CommandHandler(
            store,
            new PassThroughMenuValidationService(),
            publisher);
        var request = CreateRequest();

        var first = await handler.Handle(
            new 음식주문등록Command(request),
            CancellationToken.None);
        var retried = await handler.Handle(
            new 음식주문등록Command(request),
            CancellationToken.None);

        Assert.Equal(first.주문번호, retried.주문번호);
        Assert.Single(publisher.Notifications);
        Assert.IsType<음식주문등록됨Event>(publisher.Notifications[0]);
    }

    [Fact]
    public async Task 거래자격거절시주문과음식점제안Event를만들지않는다()
    {
        var store = new InMemorySsalddelFoodOrderStore(); var publisher = new RecordingPublisher();
        var handler = new 음식주문등록CommandHandler(store, new PassThroughMenuValidationService(), publisher, commerce: new DeniedCommerceGuard());
        var request = CreateRequest();
        await Assert.ThrowsAsync<Ssalddel.Services.Commerce.거래보호Exception>(() => handler.Handle(new 음식주문등록Command(request), default));
        Assert.Null(store.접수주문조회(request.주문자UserId, request.클라이언트요청Id)); Assert.Empty(publisher.Notifications);
    }

    [Fact]
    public async Task 이미접수된주문은이후거래차단에서도원래접수결과를조회한다()
    {
        var store = new InMemorySsalddelFoodOrderStore(); var publisher = new RecordingPublisher(); var request = CreateRequest();
        var first = await new 음식주문등록CommandHandler(store, new PassThroughMenuValidationService(), publisher).Handle(new 음식주문등록Command(request), default);
        var replay = await new 음식주문등록CommandHandler(store, new PassThroughMenuValidationService(), publisher, commerce: new DeniedCommerceGuard()).Handle(new 음식주문등록Command(request), default);
        Assert.Equal(first.주문번호, replay.주문번호); Assert.Single(publisher.Notifications);
    }

    private sealed class DeniedCommerceGuard : Ssalddel.Services.Commerce.I통신판매거래Guard
    {
        public Task 요구Async(string actorId, string? sellerId, Ssalddel.Contracts.Common.Commerce.거래보호확인Request? notice, string sourceCode, string requestId, CancellationToken cancellationToken)
            => throw new Ssalddel.Services.Commerce.거래보호Exception("CommerceOperationsNotReady", "운영 준비 필요");
        public Task 음식주문요구Async(string actorId, long restaurantId, Ssalddel.Contracts.Common.Commerce.거래보호확인Request? notice, string requestId, CancellationToken cancellationToken)
            => 요구Async(actorId, null, notice, "food-order", requestId, cancellationToken);
    }

    private static 음식주문등록요청 CreateRequest()
        => new()
        {
            클라이언트요청Id = Guid.NewGuid(),
            음식점Id = 101,
            주문자UserId = "orderer-1",
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = "주문자",
                연락처 = "010-1234-5678",
                주소 = "서울특별시 중구 세종대로 1"
            },
            상품목록 =
            [
                new 음식주문상품Dto
                {
                    메뉴Id = 1001,
                    상품명 = "살뜰김밥",
                    수량 = 2,
                    단가 = 4_500
                }
            ]
        };

    private sealed class PassThroughMenuValidationService : I음식주문메뉴검증Service
    {
        public Task<음식주문등록요청> 서버기준요청생성Async(
            음식주문등록요청 request,
            CancellationToken cancellationToken)
            => Task.FromResult(request);
    }

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
