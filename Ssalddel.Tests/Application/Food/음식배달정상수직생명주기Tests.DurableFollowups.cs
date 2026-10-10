using MediatR;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Food.Events;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Food;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using Ssalddel.Services.Outbox;
using Ssalddel.Services.PrivacyRetention;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.설정;
using 살뜰.도메인.음식;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.Food;

public sealed partial class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task 기사상태와후속예약은같은SQL트랜잭션으로롤백된다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = new EfSsalddelFoodOrderStore(database.Context).AddOrder(CreateOrderRequest());
        var queue = CreateRecommendedTransport("rollback-followup", order.주문번호, "durable-driver");
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        var before = (await database.Context.음식주문.AsNoTracking().SingleAsync()).배차상태;
        await using (var transaction = await database.Context.Database.BeginTransactionAsync())
        {
            var source = await database.Context.음식주문.SingleAsync();
            source.배차상태 = 음식주문배차상태코드.기사배정;
            await DurableOutbox(database.Context).예약Async(Followup(order.주문번호, queue.Id, queue.의뢰Id));
            await database.Context.SaveChangesAsync();
            Assert.Equal(4, await database.Context.음식마트원장동기화Outbox.CountAsync());
            await transaction.RollbackAsync();
        }
        await using var reopened = database.CreateContext();
        Assert.Equal(before, (await reopened.음식주문.AsNoTracking().SingleAsync()).배차상태);
        Assert.Empty(await reopened.음식마트원장동기화Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 기사수락commit뒤즉시처리가중단되어도_새Context에서후속을재개한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        var queue = CreateRecommendedTransport("commit-followup", order.주문번호, "durable-driver");
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        var realOutbox = DurableOutbox(database.Context);
        var service = DurableDriverWork(database.Context, store, new PausedDriverFollowup(realOutbox));
        Assert.True((await service.수락Async("durable-driver", queue.의뢰Id)).IsSuccess);
        var rows = await database.Context.음식마트원장동기화Outbox.AsNoTracking().ToArrayAsync();
        Assert.Equal(4, rows.Length);
        Assert.All(rows, x => { Assert.Equal(OutboxProcessingStatuses.Pending, x.처리상태); Assert.Equal(0, x.시도횟수); });
        AssertMinimalFollowupPayloads(rows);
        var attempt = await database.Context.음식배달시도.AsNoTracking().SingleAsync();
        var committed = store.GetOrder(order.주문번호)!;
        Assert.Equal(음식주문배차상태코드.기사배정, committed.배차상태);

        await using var restarted = database.CreateContext();
        var projection = new FollowupFoodProjection();
        var notification = new FollowupNotification();
        var publisher = new RecordingPublisher();
        var worker = DurableOutbox(restarted, projection, notification, publisher);
        Assert.Equal(4, await worker.대기항목처리Async());
        Assert.All(await restarted.음식마트원장동기화Outbox.ToArrayAsync(), x => Assert.Equal(OutboxProcessingStatuses.Succeeded, x.처리상태));
        Assert.Single(notification.Orders);
        Assert.Single(projection.Orders);
        var activity = Assert.IsType<음식배달인계상태변경됨Event>(Assert.Single(publisher.Notifications));
        Assert.Equal($"food-delivery:{queue.의뢰Id}:기사배정:{queue.UpdatedAt.Ticks}", activity.EventId);
        Assert.Equal(0, await worker.대기항목처리Async());
        Assert.Single(notification.Orders);
        Assert.Single(publisher.Notifications);
        Assert.Equal(committed.Revision, new EfSsalddelFoodOrderStore(restarted).GetOrder(order.주문번호)!.Revision);
        Assert.Equal(attempt.Revision, (await restarted.음식배달시도.AsNoTracking().SingleAsync()).Revision);
    }

    [Fact]
    public async Task 도착픽업전달의후속은상태와함께예약하고_전달완료를수령이나지급완료로바꾸지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        var queue = CreateRecommendedTransport("completed-followup", order.주문번호, "durable-driver");
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        store.배차대기반영(order.주문번호, queue.Id, DateTime.UtcNow);
        var service = DurableDriverWork(database.Context, store, new PausedDriverFollowup(DurableOutbox(database.Context)));
        Assert.True((await service.수락Async("durable-driver", queue.의뢰Id)).IsSuccess);
        var attempt = await database.Context.음식배달시도.SingleAsync();
        var arrival = new 음식배달가게도착요청 { 클라이언트요청Id = Guid.NewGuid(), 예상시도Revision = attempt.Revision };
        Assert.True((await service.가게도착Async("durable-driver", queue.의뢰Id, arrival)).IsSuccess);
        Assert.True((await service.가게도착Async("durable-driver", queue.의뢰Id, arrival)).IsSuccess);
        order = store.GetOrder(order.주문번호)!;
        Assert.Equal(queue.Id, order.배차대기Id);
        Assert.True(order.조리시작가능);
        order = store.음식점진행변경(order.주문번호,
            new() { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision, 작업 = 음식점주문진행작업코드.조리시작 }, "restaurant-41")!.주문;
        store.음식점진행변경(order.주문번호,
            new() { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision, 작업 = 음식점주문진행작업코드.픽업준비 }, "restaurant-41");
        Assert.True((await service.픽업완료Async("durable-driver", queue.의뢰Id)).IsSuccess);
        Assert.True((await service.픽업완료Async("durable-driver", queue.의뢰Id)).IsSuccess);
        Assert.True((await service.전달완료Async("durable-driver", queue.의뢰Id)).IsSuccess);
        Assert.True((await service.전달완료Async("durable-driver", queue.의뢰Id)).IsSuccess);
        await using var reopened = database.CreateContext();
        var current = new EfSsalddelFoodOrderStore(reopened).GetOrder(order.주문번호)!;
        Assert.Equal(음식주문상태코드.전달완료, current.상태);
        Assert.NotEqual(음식주문상태코드.수령확인, current.상태);
        Assert.Single(await reopened.음식주문기사정산.ToArrayAsync());
        Assert.Empty(await reopened.음식주문기사지급검증.ToArrayAsync());
        var rows = await reopened.음식마트원장동기화Outbox
            .Where(x => x.멱등키.StartsWith("food-driver-followup:")).ToArrayAsync();
        // 배정·픽업·전달 각 4관심사와 도착 활동 1건, 재요청은 새 행을 만들지 않습니다.
        Assert.Equal(13, rows.Length);
        Assert.All(rows, x => Assert.Equal(OutboxProcessingStatuses.Pending, x.처리상태));
        AssertMinimalFollowupPayloads(rows);
    }

    [Fact]
    public async Task 후속예약실패는기사수락과현재시도를확정하지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        var queue = CreateRecommendedTransport("blocked-followup", order.주문번호, "durable-driver");
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        var before = store.GetOrder(order.주문번호)!;
        var service = DurableDriverWork(database.Context, store,
            new PausedDriverFollowup(DurableOutbox(database.Context), rejectReservation: true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.수락Async("durable-driver", queue.의뢰Id));
        await using var reopened = database.CreateContext();
        var after = new EfSsalddelFoodOrderStore(reopened).GetOrder(order.주문번호)!;
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.배차상태, after.배차상태);
        Assert.Empty(await reopened.음식배달시도.ToArrayAsync());
        Assert.Empty(await reopened.음식마트원장동기화Outbox.ToArrayAsync());
        Assert.Null((await reopened.운송원장.AsNoTracking().SingleAsync()).확정기사Id);
    }

    [Fact]
    public async Task 후속관심사는독립재시도하고_재시작시현재원장을다시읽는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        var queue = CreateRecommendedTransport("retry-followup", order.주문번호, "durable-driver");
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        var intent = Followup(order.주문번호, queue.Id, queue.의뢰Id);
        var projection = new FollowupFoodProjection { FailuresLeft = 1 };
        var notification = new FollowupNotification();
        var publisher = new RecordingPublisher();
        var outbox = DurableOutbox(database.Context, projection, notification, publisher);
        await outbox.예약Async(intent);
        await outbox.예약Async(intent);
        await database.Context.SaveChangesAsync();
        Assert.Equal(4, await database.Context.음식마트원장동기화Outbox.CountAsync());
        Assert.Equal(4, await outbox.대기항목처리Async());
        var retry = Assert.Single(await database.Context.음식마트원장동기화Outbox.Where(x => x.처리상태 == OutboxProcessingStatuses.Pending).ToArrayAsync());
        Assert.Equal(3, await database.Context.음식마트원장동기화Outbox.CountAsync(x => x.처리상태 == OutboxProcessingStatuses.Succeeded));
        Assert.Single(notification.Orders);
        Assert.Single(publisher.Notifications);
        store.음식점수락멱등(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        var current = store.GetOrder(order.주문번호)!;
        retry.UpdatedAtUtc = DateTime.UtcNow - OutboxProcessingPolicy.RetryDelay - TimeSpan.FromSeconds(1);
        await database.Context.SaveChangesAsync();

        await using var restarted = database.CreateContext();
        var resumedProjection = new FollowupFoodProjection();
        var resumed = DurableOutbox(restarted, resumedProjection, notification, publisher);
        await resumed.예약Async(intent);
        await restarted.SaveChangesAsync();
        Assert.Equal(4, await restarted.음식마트원장동기화Outbox.CountAsync());
        Assert.Equal(1, await resumed.대기항목처리Async());
        Assert.Equal(current.상태, Assert.Single(resumedProjection.Orders).상태);
        Assert.Equal(current.Revision, resumedProjection.Orders[0].Revision);
        Assert.Single(notification.Orders);
        Assert.Single(publisher.Notifications);
        Assert.All(await restarted.음식마트원장동기화Outbox.ToArrayAsync(), x => Assert.Equal(OutboxProcessingStatuses.Succeeded, x.처리상태));
    }

    [Fact]
    public async Task 개인정보복원차단은후속예약과재시도를막고_payload를비운다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = new EfSsalddelFoodOrderStore(database.Context).AddOrder(CreateOrderRequest());
        var queue = CreateRecommendedTransport("privacy-followup", order.주문번호, "durable-driver");
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        var intent = Followup(order.주문번호, queue.Id, queue.의뢰Id);
        var projection = new FollowupFoodProjection();
        var notification = new FollowupNotification();
        var publisher = new RecordingPublisher();
        var denied = DurableOutbox(database.Context, projection, notification, publisher, new FollowupPrivacy(false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => denied.예약Async(intent));
        Assert.Empty(database.Context.음식마트원장동기화Outbox.Local);
        await DurableOutbox(database.Context).예약Async(intent);
        await database.Context.SaveChangesAsync();
        Assert.Equal(4, await denied.대기항목처리Async());
        Assert.All(await database.Context.음식마트원장동기화Outbox.ToArrayAsync(), x =>
        { Assert.Equal(음식개인정보OutboxPolicy.PrivacyExpired, x.처리상태); Assert.Equal("{}", x.PayloadJson); });
        Assert.Empty(projection.Orders);
        Assert.Empty(notification.Orders);
        Assert.Empty(publisher.Notifications);
    }

    private static 음식기사후속의도 Followup(string orderNo, long transportId, string offerId)
        => new(orderNo, transportId, offerId, "durable-driver", "기사배정",
            new DateTime(2026, 10, 9, 2, 0, 0, DateTimeKind.Utc), "기사 배정");

    private static 음식마트원장동기화OutboxService DurableOutbox(SsalddelContext context,
        FollowupFoodProjection? food = null, FollowupNotification? notification = null,
        RecordingPublisher? publisher = null, I개인정보복원차단Service? privacy = null)
        => new(context, food ?? new FollowupFoodProjection(), NullLogger<음식마트원장동기화OutboxService>.Instance,
            privacy, new EfSsalddelFoodOrderStore(context), new NoOpTransportSync(),
            notification ?? new FollowupNotification(), publisher ?? new RecordingPublisher());

    private static 음식배달기사업무Service DurableDriverWork(SsalddelContext context,
        EfSsalddelFoodOrderStore store, I음식기사후속OutboxService followups)
        => new(context, new InMemoryDriverLocationStore(), new NoOpRouteService(), new NoOpQueueTransition(),
            new InMemory음식배달권실행공간Store(), store, new NoOpFoodLedgerOutbox(), new NoOpTransportSync(),
            new NoOpRestaurantNotification(), new NoOpSettlement(), new RecordingPublisher(),
            new TestCurrentUserAccessor("durable-driver", "Driver"), NullLogger<음식배달기사업무Service>.Instance,
            new TestDispatchAvailability(), driverFollowups: followups);

    private static void AssertMinimalFollowupPayloads(IEnumerable<음식마트원장동기화Outbox> rows)
    {
        foreach (var row in rows)
        {
            using var payload = JsonDocument.Parse(row.PayloadJson);
            var intent = payload.RootElement.GetProperty("driverFollowup").GetProperty("intent");
            var allowedProperties = new[] { "주문번호", "운송Id", "배차의뢰Id", "처리기사Id", "상태",
                "발생시각Utc", "음식점알림사유", "원장동기화", "활동발행", "eventId" };
            Assert.All(intent.EnumerateObject(), property => Assert.Contains(property.Name, allowedProperties));
            var decoded = string.Join("|", intent.EnumerateObject().Select(x => x.Value.ToString()));
            foreach (var privateValue in new[] { "테스트 주문자", "010-0000-0000", "면목로", "101호" })
                Assert.DoesNotContain(privateValue, decoded);
        }
    }

    private sealed class PausedDriverFollowup(I음식기사후속OutboxService inner, bool rejectReservation = false) : I음식기사후속OutboxService
    {
        public Task 예약Async(음식기사후속의도 intent, CancellationToken cancellationToken = default)
        {
            if (rejectReservation) throw new InvalidOperationException("reservation unavailable");
            return inner.예약Async(intent, cancellationToken);
        }
        public Task 즉시처리Async(음식기사후속의도 intent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulate process interruption after commit");
    }

    private sealed class FollowupFoodProjection : I음식마트원장Mongo동기화Service
    {
        public int FailuresLeft { get; set; }
        public List<음식주문응답> Orders { get; } = [];
        public Task<커뮤니티원장Dto?> 음식주문동기화Async(음식주문응답 order, string updatedBy, CancellationToken cancellationToken = default)
        {
            if (FailuresLeft > 0) { FailuresLeft--; return Task.FromResult<커뮤니티원장Dto?>(null); }
            Orders.Add(order);
            return Task.FromResult<커뮤니티원장Dto?>(new() { 원장Id = order.주문번호 });
        }
        public Task<커뮤니티원장Dto?> 출고원장동기화Async(IReadOnlyList<출고예정> outbounds, IReadOnlyList<입고요청> inbounds,
            string updatedBy, string? 현재단계Key = null, string? 원장템플릿Key = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FollowupNotification : I음식점주문실시간알림Service
    {
        public List<음식주문응답> Orders { get; } = [];
        public Task 신규주문알림발송Async(음식주문응답 order, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task 주문상태변경알림발송Async(음식주문응답 order, string reason, CancellationToken cancellationToken = default)
        { Orders.Add(order); return Task.CompletedTask; }
    }

    private sealed class FollowupPrivacy(bool allowed) : I개인정보복원차단Service
    {
        public Task<bool> 복원허용Async(string sourceCode, string recordId, CancellationToken cancellationToken = default)
            => Task.FromResult(allowed);
    }
}
