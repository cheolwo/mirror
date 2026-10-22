using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using Ssalddel.Services.Community;
using 살뜰.Services.Notifications;
using 살뜰.도메인.설정;

namespace Ssalddel.Tests.Application.Food;

public sealed partial class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task WorkflowProgress_서버확정단계만저장하고현재기사에게만준비완료를알린다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Context;
        var store = new EfSsalddelFoodOrderStore(db);
        var request = CreateOrderRequest();
        var order = store.멱등등록(request).주문;
        Assert.False(store.멱등등록(request).새로생성됨);
        var acceptance = new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 };
        store.음식점수락멱등(order.주문번호, acceptance, "restaurant-41");
        Assert.False(store.음식점수락멱등(order.주문번호, acceptance, "restaurant-41")!.새로변경됨);
        var queue = CreateRecommendedTransport("workflow-progress-offer", order.주문번호, "durable-driver");
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        store.배차대기반영(order.주문번호, queue.Id, DateTime.UtcNow);
        var driver = DurableDriverWork(db, store, new PausedDriverFollowup(DurableOutbox(db)));
        Assert.True((await driver.수락Async("durable-driver", queue.의뢰Id)).IsSuccess);
        // 기존 수락 계약은 이미 확정된 제안을 새 수락으로 받아들이지 않습니다.
        Assert.True((await driver.수락Async("durable-driver", queue.의뢰Id)).IsFailed);
        Assert.Single(await db.Command알림Outbox.Where(x => x.EventName == "food.assigned").ToArrayAsync());
        var attempt = await db.음식배달시도.SingleAsync();
        var arrival = new 음식배달가게도착요청
        {
            클라이언트요청Id = Guid.NewGuid(), 예상시도Revision = attempt.Revision
        };
        Assert.True((await driver.가게도착Async("durable-driver", queue.의뢰Id, arrival)).IsSuccess);
        Assert.True((await driver.가게도착Async("durable-driver", queue.의뢰Id, arrival)).IsSuccess);
        order = store.GetOrder(order.주문번호)!;
        var cooking = new 음식점주문진행변경요청
        {
            클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
            작업 = 음식점주문진행작업코드.조리시작
        };
        store.음식점진행변경(order.주문번호, cooking, "restaurant-41");
        Assert.False(store.음식점진행변경(order.주문번호, cooking, "restaurant-41")!.새로변경됨);
        order = store.GetOrder(order.주문번호)!;
        store.음식점진행변경(order.주문번호, new()
        {
            클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
            작업 = 음식점주문진행작업코드.조리시간변경, 조리예상분 = 12
        }, "restaurant-41");
        Assert.Single(await db.Command알림Outbox.Where(x => x.EventName == "food.cooking").ToArrayAsync());
        order = store.GetOrder(order.주문번호)!;
        var ready = new 음식점주문진행변경요청
        {
            클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
            작업 = 음식점주문진행작업코드.픽업준비
        };
        store.음식점진행변경(order.주문번호, ready, "restaurant-41");
        Assert.False(store.음식점진행변경(order.주문번호, ready, "restaurant-41")!.새로변경됨);
        Assert.True((await driver.픽업완료Async("durable-driver", queue.의뢰Id)).IsSuccess);
        Assert.True((await driver.픽업완료Async("durable-driver", queue.의뢰Id)).IsSuccess);
        Assert.True((await driver.전달완료Async("durable-driver", queue.의뢰Id)).IsSuccess);
        Assert.True((await driver.전달완료Async("durable-driver", queue.의뢰Id)).IsSuccess);
        var receipt = new 주문자음식주문수령확인요청 { 클라이언트요청Id = Guid.NewGuid() };
        store.주문자수령확인(order.주문번호, receipt, request.주문자UserId);
        Assert.False(store.주문자수령확인(order.주문번호, receipt, request.주문자UserId)!.새로변경됨);

        await using var reopened = database.CreateContext();
        var rows = await reopened.Command알림Outbox.AsNoTracking()
            .Where(x => x.FeatureName == 업무진행알림Recorder.FeatureName).ToArrayAsync();
        Assert.Equal(10, rows.Length);
        var customerStages = rows.Where(x => ProgressRecipient(x) == request.주문자UserId)
            .Select(x => x.EventName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "food.submitted", "food.accepted", "food.assigned", "food.arrived", "food.cooking",
            "food.ready", "food.picked-up", "food.delivered", "food.received" }
            .OrderBy(x => x, StringComparer.Ordinal).ToArray(), customerStages);
        var driverRow = Assert.Single(rows.Where(x => ProgressRecipient(x) == "durable-driver"));
        Assert.Equal("food.ready", driverRow.EventName);
        using var driverPayload = JsonDocument.Parse(driverRow.PayloadJson);
        Assert.Equal("/workspace/food-driver?selected=workflow-progress-offer",
            driverPayload.RootElement.GetProperty("deepLink").GetString());
        Assert.All(rows, AssertMinimalProgressNotification);
        Assert.Equal(음식주문상태코드.수령확인,
            new EfSsalddelFoodOrderStore(reopened).GetOrder(order.주문번호)!.상태);
        Assert.Empty(await reopened.음식주문기사지급검증.ToArrayAsync());
    }

    [Theory]
    [InlineData(true, "food.cancelled")]
    [InlineData(false, "food.rejected")]
    public async Task WorkflowProgress_취소거절은본인원천의새전이에한번만예약한다(bool cancel, string stage)
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var request = CreateOrderRequest();
        var order = store.AddOrder(request);
        if (cancel)
        {
            var change = new 주문자음식주문취소요청 { 클라이언트요청Id = Guid.NewGuid(), 사유Code = "Other" };
            Assert.Null(store.주문자취소(order.주문번호, change, "other-customer"));
            store.주문자취소(order.주문번호, change, request.주문자UserId);
            Assert.False(store.주문자취소(order.주문번호, change, request.주문자UserId)!.새로변경됨);
        }
        else
        {
            var change = new 음식점주문진행변경요청
            {
                클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.거절,
                사유 = "가상의 품절 사유", 예상Revision = order.Revision
            };
            store.음식점진행변경(order.주문번호, change, "restaurant-41");
            Assert.False(store.음식점진행변경(order.주문번호, change, "restaurant-41")!.새로변경됨);
        }
        await using var reopened = database.CreateContext();
        var row = Assert.Single(await reopened.Command알림Outbox.Where(x => x.EventName == stage).ToArrayAsync());
        Assert.Equal(request.주문자UserId, ProgressRecipient(row));
        AssertMinimalProgressNotification(row);
        Assert.Equal(2, await reopened.Command알림Outbox.CountAsync());
    }

    [Fact]
    public async Task WorkflowProgress_주문상태와알림예약은함께롤백되고새Context에서같이재개한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        var acceptance = new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 };
        await using (var transaction = await database.Context.Database.BeginTransactionAsync())
        {
            store.음식점수락멱등(order.주문번호, acceptance, "restaurant-41");
            Assert.Equal(2, await database.Context.Command알림Outbox.CountAsync());
            await transaction.RollbackAsync();
        }
        await using var reopened = database.CreateContext();
        var restartedStore = new EfSsalddelFoodOrderStore(reopened);
        Assert.Equal(음식주문상태코드.주문대기, restartedStore.GetOrder(order.주문번호)!.상태);
        Assert.Single(await reopened.Command알림Outbox.ToArrayAsync());
        Assert.True(restartedStore.음식점수락멱등(order.주문번호, acceptance, "restaurant-41")!.새로변경됨);
        Assert.False(restartedStore.음식점수락멱등(order.주문번호, acceptance, "restaurant-41")!.새로변경됨);
        Assert.Single(await reopened.Command알림Outbox.Where(x => x.EventName == "food.accepted").ToArrayAsync());
    }

    [Fact]
    public async Task WorkflowProgress_알림저장실패는주문수락을롤백하고추적예약도제거한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Context;
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateOrderRequest());
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_food_progress BEFORE INSERT ON "Command_알림_Outbox"
            WHEN NEW.event_name = 'food.accepted'
            BEGIN SELECT RAISE(ABORT, 'test-notification-save-failure'); END;
            """);
        Assert.Throws<DbUpdateException>(() => store.음식점수락멱등(order.주문번호,
            new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41"));
        Assert.DoesNotContain(db.Command알림Outbox.Local, x => x.EventName == "food.accepted");
        await using var reopened = database.CreateContext();
        Assert.Equal(음식주문상태코드.주문대기, new EfSsalddelFoodOrderStore(reopened).GetOrder(order.주문번호)!.상태);
        Assert.Single(await reopened.Command알림Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task WorkflowProgress_기사알림저장실패는배정과같이롤백하고진행예약을남기지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Context;
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호,
            new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        var queue = CreateRecommendedTransport("workflow-save-failure", order.주문번호, "durable-driver");
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_driver_progress BEFORE INSERT ON "Command_알림_Outbox"
            WHEN NEW.event_name = 'food.assigned'
            BEGIN SELECT RAISE(ABORT, 'test-notification-save-failure'); END;
            """);
        var driver = DurableDriverWork(db, store, new PausedDriverFollowup(DurableOutbox(db)));
        Assert.True((await driver.수락Async("durable-driver", queue.의뢰Id)).IsFailed);
        Assert.DoesNotContain(db.Command알림Outbox.Local, x => x.EventName == "food.assigned");
        await using var reopened = database.CreateContext();
        Assert.Null((await reopened.운송원장.SingleAsync()).확정기사Id);
        Assert.Empty(await reopened.음식배달시도.ToArrayAsync());
        Assert.DoesNotContain(await reopened.Command알림Outbox.ToArrayAsync(), x => x.EventName == "food.assigned");
    }

    private static string? ProgressRecipient(Command알림Outbox row)
    {
        using var payload = JsonDocument.Parse(row.PayloadJson);
        return payload.RootElement.GetProperty("targetUserId").GetString();
    }

    [Fact]
    public async Task WorkflowProgress_묶음수락중두번째예약예외는첫번째진행알림을남기지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Context;
        var store = new EfSsalddelFoodOrderStore(db);
        var offers = new List<string>();
        for (var index = 0; index < 2; index++)
        {
            var order = store.AddOrder(CreateOrderRequest());
            store.음식점수락멱등(order.주문번호,
                new() { 클라이언트요청Id = Guid.NewGuid(), 즉시픽업가능여부 = true }, "restaurant-41");
            var offerId = $"workflow-bundle-failure-{index}";
            db.운송원장.Add(CreateRecommendedTransport(offerId, order.주문번호, "durable-driver"));
            offers.Add(offerId);
        }
        await db.SaveChangesAsync();
        var driver = DurableDriverWork(db, store, new FailSecondProgressReservation(DurableOutbox(db)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.묶음수락Async("durable-driver", offers));
        Assert.DoesNotContain(db.Command알림Outbox.Local, x => x.EventName == "food.assigned");
        await using var reopened = database.CreateContext();
        Assert.DoesNotContain(await reopened.Command알림Outbox.ToArrayAsync(), x => x.EventName == "food.assigned");
        Assert.All(await reopened.운송원장.ToArrayAsync(), queue => Assert.Null(queue.확정기사Id));
    }

    private sealed class FailSecondProgressReservation(I음식기사후속OutboxService inner) : I음식기사후속OutboxService
    {
        private int _calls;
        public Task 예약Async(음식기사후속의도 intent, CancellationToken cancellationToken = default)
        {
            if (++_calls == 2) throw new InvalidOperationException("test-second-reservation-failure");
            return inner.예약Async(intent, cancellationToken);
        }
        public Task 즉시처리Async(음식기사후속의도 intent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("test-no-immediate-send");
    }

    private static void AssertMinimalProgressNotification(Command알림Outbox row)
    {
        Assert.Equal("Pending", row.Status);
        Assert.Equal(0, row.RetryCount);
        using var payload = JsonDocument.Parse(row.PayloadJson);
        var root = payload.RootElement;
        var allowed = new[] { "notificationType", "eventId", "workflowKey", "stageCode", "sourceId",
            "targetUserId", "userId", "occurredAtUtc", "deepLink", "title", "body", "channels" };
        Assert.All(root.EnumerateObject(), property => Assert.Contains(property.Name, allowed));
        var decoded = string.Join("|", root.EnumerateObject().Select(property => property.Value.ToString()));
        foreach (var privateValue in new[] { "테스트 주문자", "010-0000-0000", "면목로", "101호", "비빔밥", "가상의 품절 사유" })
            Assert.DoesNotContain(privateValue, decoded);
        Assert.DoesNotContain("restaurant:", decoded);
        Assert.Equal(64, row.TraceId.Length);
    }
}
