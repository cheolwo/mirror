using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Transport;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식점수락배차OutboxTests
{
    [Fact]
    public async Task 음식점수락과_배차요청Outbox가_같은Rdb트랜잭션에저장된다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrder());
        var outbox = new 음식배차요청OutboxService(
            database.Context,
            null!, null!, null!, null!, null!, store, null!,
            NullLogger<음식배차요청OutboxService>.Instance);
        var handler = new 음식점주문수락CommandHandler(
            store,
            new NoOpPublisher(),
            database.Context,
            outbox);

        var accepted = await handler.Handle(CreateAcceptCommand(order.주문번호), default);

        Assert.NotNull(accepted);
        Assert.Equal(음식주문상태코드.주문확인, accepted!.상태);
        Assert.Null(accepted.조리시작시각Utc);
        Assert.Null(accepted.조리예상완료시각Utc);
        var request = await database.Context.음식마트원장동기화Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(음식마트원장동기화유형코드.음식배차요청, request.동기화유형);
        Assert.Equal(order.주문번호, request.원천Id);
        Assert.StartsWith("food-dispatch:", request.멱등키, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 배차요청저장이실패하면_음식점수락도_커밋되지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrder());
        var handler = new 음식점주문수락CommandHandler(
            store,
            new NoOpPublisher(),
            database.Context,
            new FailingOutbox());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateAcceptCommand(order.주문번호), default));

        database.Context.ChangeTracker.Clear();
        var preserved = store.GetOrder(order.주문번호);
        Assert.NotNull(preserved);
        Assert.Equal(음식주문상태코드.주문대기, preserved!.상태);
        Assert.Empty(await database.Context.음식마트원장동기화Outbox.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task 배차생성이일시실패하면_Outbox가Pending으로남고_다음실행에서회복한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = CreateOrderSnapshot();
        var queue = new FailOnceQueue();
        var orderStore = new RecordingOrderStore(order);
        var service = new 음식배차요청OutboxService(
            database.Context,
            queue,
            new NoOpTransportSync(),
            new NoOpFoodLedgerOutbox(),
            new NoOpRealtime(),
            new NoOpRestaurantNotification(),
            orderStore,
            new NoOpGeo(),
            NullLogger<음식배차요청OutboxService>.Instance);
        await service.예약Async(order, "restaurant-41", "event-1");

        Assert.Equal(1, await service.대기항목처리Async());
        var item = await database.Context.음식마트원장동기화Outbox.SingleAsync();
        Assert.Equal(OutboxProcessingStatuses.Pending, item.처리상태);
        Assert.Equal(1, item.시도횟수);

        item.UpdatedAtUtc = DateTime.UtcNow - OutboxProcessingPolicy.RetryDelay - TimeSpan.FromSeconds(1);
        await database.Context.SaveChangesAsync();

        Assert.Equal(1, await service.대기항목처리Async());
        Assert.Equal(OutboxProcessingStatuses.Succeeded, item.처리상태);
        Assert.Equal(2, item.시도횟수);
        Assert.Equal(2, queue.Attempts);
        Assert.Equal(1, orderStore.DispatchLinks);
    }

    [Fact]
    public async Task 기존음식마트원장처리기는_배차요청유형을소비하지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
        {
            멱등키 = "food-dispatch:event-1",
            동기화유형 = 음식마트원장동기화유형코드.음식배차요청,
            원천Id = "food-order-1",
            PayloadJson = "{}",
            처리상태 = OutboxProcessingStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();
        var service = new 음식마트원장동기화OutboxService(
            database.Context,
            new NoOpFoodMartMongoSync(),
            NullLogger<음식마트원장동기화OutboxService>.Instance);

        Assert.Equal(0, await service.대기항목처리Async());
        var item = await database.Context.음식마트원장동기화Outbox.SingleAsync();
        Assert.Equal(OutboxProcessingStatuses.Pending, item.처리상태);
        Assert.Equal(0, item.시도횟수);
    }

    private static 음식주문등록요청 CreateOrder() => new()
    {
        클라이언트요청Id = Guid.NewGuid(),
        음식점Id = 41,
        주문자UserId = "customer-1",
        수령인정보 = new 음식주문수령인정보Dto
        {
            수령인명 = "테스트 주문자",
            연락처 = "010-0000-0000",
            주소 = "서울특별시 중랑구 면목로 1",
            상세주소 = "101호",
            주문자본인수령여부 = true
        },
        상품목록 =
        [
            new 음식주문상품Dto { 메뉴Id = 10, 상품명 = "비빔밥", 수량 = 1, 단가 = 9000 }
        ]
    };

    private static 음식점주문수락Command CreateAcceptCommand(string orderNo)
        => new(orderNo, new 음식점주문수락요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            음식점명 = "면목 식당",
            음식점주소 = "서울특별시 중랑구 면목로 2",
            음식점상세주소 = "1층",
            조리예상분 = 10
        }, "restaurant-41");

    private static 음식주문응답 CreateOrderSnapshot() => new()
    {
        주문번호 = "food-order-1",
        음식점Id = 41,
        음식점명 = "면목 식당",
        음식점주소 = "서울특별시 중랑구 면목로 2",
        음식점상세주소 = "1층",
        음식점위도 = 37.58m,
        음식점경도 = 127.09m,
        주문자UserId = "customer-1",
        수령인정보 = new 음식주문수령인정보Dto
        {
            수령인명 = "테스트 주문자",
            주소 = "서울특별시 중랑구 면목로 1",
            상세주소 = "101호"
        },
        상품목록 = [new 음식주문상품Dto { 상품명 = "비빔밥", 수량 = 1, 단가 = 9000 }],
        상태 = 음식주문상태코드.조리중
    };

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }

    private sealed class FailingOutbox : I음식배차요청OutboxService
    {
        public Task 예약Async(음식주문응답 order, string actorUserId, string eventId, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("outbox-write-failed"));

        public Task<bool> 즉시처리Async(string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FailOnceQueue : I운송의뢰배차대기Service
    {
        public int Attempts { get; private set; }

        public Task<운송원장> 생성또는조회Async(
            출고예정운송대상 target,
            운송의뢰배차대기생성옵션? options = null,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts == 1)
            {
                return Task.FromException<운송원장>(new InvalidOperationException("temporary-dispatch-failure"));
            }

            return Task.FromResult(new 운송원장
            {
                Id = 77,
                운송번호 = target.운송의뢰Id ?? throw new InvalidOperationException("운송 의뢰 ID 누락"),
                의뢰Id = target.운송의뢰Id ?? throw new InvalidOperationException("운송 의뢰 ID 누락"),
                화주Id = target.판매자UserId,
                원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
                원본의뢰Id = target.원천참조번호
            });
        }
    }

    private sealed class RecordingOrderStore(음식주문응답 order) : ISsalddelFoodOrderStore
    {
        public int DispatchLinks { get; private set; }
        public 음식주문목록응답 GetOrders() => new() { Items = [order] };
        public 음식주문응답? GetOrder(string orderNo) => orderNo == order.주문번호 ? order : null;
        public 음식주문응답 AddOrder(음식주문등록요청 request) => throw new NotSupportedException();
        public 음식주문응답? 음식점수락(string orderNo, 음식점주문수락요청 request) => throw new NotSupportedException();
        public 음식주문응답? 배차대기반영(string orderNo, long dispatchWaitId, DateTime dispatchRequestedAtUtc)
        {
            if (orderNo != order.주문번호) return null;
            DispatchLinks++;
            order.배차대기Id = dispatchWaitId;
            order.배차상태 = 음식주문배차상태코드.배차대기;
            order.배차요청시각Utc = dispatchRequestedAtUtc;
            return order;
        }
    }

    private sealed class NoOpTransportSync : I운송원장Mongo동기화Service
    {
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 의뢰, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 운송실행투영, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<운송원장Mongo동기화상태> 상태조회Async(string 의뢰Id, CancellationToken cancellationToken = default)
            => Task.FromResult(운송원장Mongo동기화상태.Empty(의뢰Id, string.Empty));
    }

    private sealed class NoOpFoodLedgerOutbox : I음식마트원장동기화OutboxService
    {
        public Task 음식주문예약후즉시처리Async(음식주문응답 order, string updatedBy, string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task 출고원장예약후즉시처리Async(IReadOnlyList<출고예정> outbounds, IReadOnlyList<입고요청> inbounds, string updatedBy, string idempotencyKey, string? currentStageKey = null, string? ledgerTemplateKey = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class NoOpFoodMartMongoSync : I음식마트원장Mongo동기화Service
    {
        public Task<커뮤니티원장Dto?> 음식주문동기화Async(음식주문응답 주문, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<커뮤니티원장Dto?> 출고원장동기화Async(IReadOnlyList<출고예정> 출고목록, IReadOnlyList<입고요청> 입고목록, string updatedBy, string? 현재단계Key = null, string? 원장템플릿Key = null, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
    }

    private sealed class NoOpRealtime : ITransportRequestLedgerRealtimeService
    {
        public Task PublishAsync(string requestId, string eventType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpRestaurantNotification : I음식점주문실시간알림Service
    {
        public Task 신규주문알림발송Async(음식주문응답 order, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task 주문상태변경알림발송Async(음식주문응답 order, string reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpGeo : IKakao좌표변환Service
    {
        public Task<(double 위도, double 경도)?> 도로명주소좌표변환Async(string 주소, CancellationToken cancellationToken = default)
            => Task.FromResult<(double, double)?>(null);
        public Task<Kakao주소정보?> 주소정보조회Async(string 주소, CancellationToken cancellationToken = default)
            => Task.FromResult<Kakao주소정보?>(null);
        public Task<Kakao지역정보?> 좌표지역정보조회Async(decimal 위도, decimal 경도, CancellationToken cancellationToken = default)
            => Task.FromResult<Kakao지역정보?>(null);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, SsalddelContext context)
        {
            _connection = connection;
            Context = context;
        }

        public SsalddelContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SsalddelContext>()
                .UseSqlite(connection)
                .Options;
            var context = new SsalddelContext(options, new DummyEncryption());
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
