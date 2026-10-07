using System.Text.Json;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Application.Food;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Food;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.Services.Weather;
using 살뜰.Services.Settlement;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Transport;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

/// <summary>
/// 배차 후보 선정은 배차 엔진의 독립 시험 범위로 두고, 그 결과로 만들어진 추천 운송 원장부터
/// 실제 음식 주문 Handler와 기사 업무 Service, 완료 World 투영까지 한 판본으로 관통합니다.
/// </summary>
public sealed class 음식배달정상수직생명주기Tests
{
    [Theory]
    [InlineData("transport", "assigned")]
    [InlineData("food", "assigned")]
    [InlineData("transport", "cooking")]
    [InlineData("food", "cooking")]
    [InlineData("transport", "picked")]
    [InlineData("food", "picked")]
    [InlineData("transport", "delivered")]
    [InlineData("food", "delivered")]
    [InlineData("transport", "received")]
    [InlineData("food", "received")]
    public async Task 배차연결후_투영실패와_기사수락을거친_새Context재시도는_현재진행을보존한다(string failAt, string phase)
    {
        await using var database = await TestDatabase.CreateAsync();
        const string driverId = "food-driver-r24";
        var originalStore = new EfSsalddelFoodOrderStore(database.Context);
        var order = originalStore.AddOrder(CreateOrderRequest());
        order = originalStore.음식점수락멱등(order.주문번호,
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41")!.주문;
        var sync = new FailOnceTransportSync { FailNext = failAt == "transport" };
        var foodProjection = new FailOnceFoodLedgerOutbox { FailNext = failAt == "food" };
        var eventId = "food-r24-" + Guid.NewGuid().ToString("N");
        await CreateDispatchOutbox(database.Context, originalStore,
            new PersistedFoodQueue(database.Context, driverId), sync, foodProjection).예약Async(order, "restaurant-41", eventId);

        // 큐와 주문 결속 저장 뒤 실패한다. 재시도 전에 다른 요청 Context가 기사 수락을 저장한다.
        await using (var first = database.CreateContext())
        {
            var firstStore = new EfSsalddelFoodOrderStore(first);
            Assert.Equal(1, await CreateDispatchOutbox(first, firstStore,
                new PersistedFoodQueue(first, driverId), sync, foodProjection).대기항목처리Async());
            var pending = await first.음식마트원장동기화Outbox.SingleAsync();
            Assert.Equal(OutboxProcessingStatuses.Pending, pending.처리상태);
            Assert.Equal(1, pending.시도횟수);
            Assert.NotNull(firstStore.GetOrder(order.주문번호)!.배차대기Id);
            pending.UpdatedAtUtc = DateTime.UtcNow - OutboxProcessingPolicy.RetryDelay - TimeSpan.FromSeconds(1);
            await first.SaveChangesAsync();
        }

        음식주문응답 before;
        운송원장 beforeQueue;
        음식배달시도 beforeAttempt;
        await using (var accepted = database.CreateContext())
        {
            var acceptedStore = new EfSsalddelFoodOrderStore(accepted);
            var driver = CreateDriverWork(accepted, acceptedStore, new RecordingPublisher(), driverId);
            Assert.True((await driver.수락Async(driverId, order.주문번호)).IsSuccess);
            before = acceptedStore.GetOrder(order.주문번호)!;
            Assert.True(before.조리시작가능);
            if (phase != "assigned")
            {
                before = acceptedStore.음식점진행변경(order.주문번호, new 음식점주문진행변경요청
                {
                    클라이언트요청Id = Guid.NewGuid(), 예상Revision = before.Revision,
                    작업 = 음식점주문진행작업코드.조리시작
                }, "restaurant-41")!.주문;
            }
            if (phase is "picked" or "delivered" or "received")
            {
                acceptedStore.음식점진행변경(order.주문번호, new 음식점주문진행변경요청
                {
                    클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.픽업준비
                }, "restaurant-41");
                Assert.True((await driver.픽업완료Async(driverId, order.주문번호)).IsSuccess);
            }
            if (phase is "delivered" or "received")
                Assert.True((await driver.전달완료Async(driverId, order.주문번호)).IsSuccess);
            if (phase == "received")
                acceptedStore.주문자수령확인(order.주문번호,
                    new 주문자음식주문수령확인요청 { 클라이언트요청Id = Guid.NewGuid() }, "customer-1");
            before = acceptedStore.GetOrder(order.주문번호)!;
            beforeQueue = await accepted.운송원장.AsNoTracking().SingleAsync();
            beforeAttempt = await accepted.음식배달시도.AsNoTracking().SingleAsync();
        }

        await using (var retry = database.CreateContext())
        {
            var retryStore = new EfSsalddelFoodOrderStore(retry);
            Assert.Equal(1, await CreateDispatchOutbox(retry, retryStore,
                new PersistedFoodQueue(retry, driverId), sync, foodProjection).대기항목처리Async());
            var succeeded = await retry.음식마트원장동기화Outbox.SingleAsync(x => x.멱등키 == "food-dispatch:" + eventId);
            Assert.Equal(OutboxProcessingStatuses.Succeeded, succeeded.처리상태);
            Assert.Equal(2, succeeded.시도횟수);
            var after = retryStore.GetOrder(order.주문번호)!;
            Assert.Equal(before.상태, after.상태);
            Assert.Equal(before.배차상태, after.배차상태);
            Assert.Equal(before.배차대기Id, after.배차대기Id);
            Assert.Equal(before.배차요청시각Utc, after.배차요청시각Utc);
            Assert.Equal(before.Revision, after.Revision);
            Assert.Equal(before.조리시작가능, after.조리시작가능);
            Assert.Equal(before.CurrentPreparationRound, after.CurrentPreparationRound);
            Assert.Equal(before.CurrentCookingStartedAtUtc, after.CurrentCookingStartedAtUtc);
            Assert.Equal(before.CurrentPickupReadyAtUtc, after.CurrentPickupReadyAtUtc);
            var queue = await retry.운송원장.AsNoTracking().SingleAsync();
            Assert.Equal(beforeQueue.Id, queue.Id);
            Assert.Equal(beforeQueue.상태, queue.상태);
            Assert.Equal(beforeQueue.배차큐단계, queue.배차큐단계);
            Assert.Equal(beforeQueue.배차노출상태, queue.배차노출상태);
            Assert.Equal(driverId, queue.확정기사Id);
            var attempt = await retry.음식배달시도.AsNoTracking().SingleAsync();
            Assert.Equal(beforeAttempt.시도StableId, attempt.시도StableId);
            Assert.Equal(beforeAttempt.Revision, attempt.Revision);
            Assert.Equal(beforeAttempt.픽업완료시각Utc, attempt.픽업완료시각Utc);
            Assert.Equal(beforeAttempt.전달완료시각Utc, attempt.전달완료시각Utc);
            Assert.Equal(after.상태, foodProjection.LastOrder!.상태);
            Assert.Equal(after.배차상태, foodProjection.LastOrder.배차상태);
            Assert.Equal(queue.상태, sync.LastQueue!.상태);
        }

        await using var replay = database.CreateContext();
        Assert.Equal(0, await CreateDispatchOutbox(replay, new EfSsalddelFoodOrderStore(replay),
            new PersistedFoodQueue(replay, driverId), sync, foodProjection).대기항목처리Async());
        Assert.Single(await replay.운송원장.ToListAsync());
        Assert.Equal(before.Revision, new EfSsalddelFoodOrderStore(replay).GetOrder(order.주문번호)!.Revision);
    }

    private static 음식배차요청OutboxService CreateDispatchOutbox(
        SsalddelContext db, EfSsalddelFoodOrderStore store, I운송의뢰배차대기Service queue,
        I운송원장Mongo동기화Service sync, I음식마트원장동기화OutboxService projection)
        => new(db, queue, sync, projection, new NoOpRealtime(), new NoOpRestaurantNotification(),
            store, new NoOpGeo(), NullLogger<음식배차요청OutboxService>.Instance);

    private sealed class PersistedFoodQueue(SsalddelContext db, string driverId) : I운송의뢰배차대기Service
    {
        public async Task<운송원장> 생성또는조회Async(출고예정운송대상 target,
            운송의뢰배차대기생성옵션? options = null, CancellationToken cancellationToken = default)
        {
            var id = options!.의뢰Id!;
            var queue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == id, cancellationToken);
            if (queue is not null) return queue;
            queue = CreateRecommendedTransport(id, target.원천참조번호!, driverId);
            db.운송원장.Add(queue);
            return queue;
        }
    }

    private sealed class FailOnceTransportSync : I운송원장Mongo동기화Service
    {
        public bool FailNext { get; set; }
        public 운송원장? LastQueue { get; private set; }
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 의뢰, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 queue, string updatedBy, CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("food-r24-post-binding-sync-failure");
            }
            LastQueue = queue;
            return Task.FromResult<커뮤니티원장Dto?>(null);
        }
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(운송원장Mongo동기화상태.Empty(id, string.Empty));
    }

    private sealed class FailOnceFoodLedgerOutbox : I음식마트원장동기화OutboxService
    {
        public bool FailNext { get; set; }
        public 음식주문응답? LastOrder { get; private set; }
        public Task 음식주문예약후즉시처리Async(음식주문응답 order, string updatedBy, string key, CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("food-r24-post-binding-food-projection-failure");
            }
            LastOrder = order;
            return Task.CompletedTask;
        }
        public Task 출고원장예약후즉시처리Async(IReadOnlyList<출고예정> outbounds, IReadOnlyList<입고요청> inbounds,
            string updatedBy, string key, string? currentStageKey = null, string? ledgerTemplateKey = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class NoOpRealtime : ITransportRequestLedgerRealtimeService
    {
        public Task PublishAsync(string id, string type, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpGeo : IKakao좌표변환Service
    {
        public Task<(double 위도, double 경도)?> 도로명주소좌표변환Async(string address, CancellationToken cancellationToken = default)
            => Task.FromResult<(double, double)?>(null);
        public Task<Kakao주소정보?> 주소정보조회Async(string address, CancellationToken cancellationToken = default)
            => Task.FromResult<Kakao주소정보?>(null);
        public Task<Kakao지역정보?> 좌표지역정보조회Async(decimal latitude, decimal longitude, CancellationToken cancellationToken = default)
            => Task.FromResult<Kakao지역정보?>(null);
    }

    [Fact]
    public async Task 주문등록부터_수령확인과_Unity용익명상태사본까지_한생명주기로이어진다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingPublisher();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var register = new 음식주문등록CommandHandler(
            store,
            new PassThroughMenuValidationService(),
            publisher,
            database.Context);
        var accept = new 음식점주문수락CommandHandler(
            store,
            publisher,
            database.Context);
        var prepare = new 음식점주문진행변경CommandHandler(
            store,
            publisher,
            database.Context);

        var order = await register.Handle(
            new 음식주문등록Command(CreateOrderRequest()),
            default);
        order = Assert.IsType<음식주문응답>(await accept.Handle(
            new 음식점주문수락Command(
                order.주문번호,
                new 음식점주문수락요청
                {
                    클라이언트요청Id = Guid.NewGuid(),
                    음식점명 = "면목 식당",
                    음식점주소 = "서울특별시 중랑구 면목동 면목로 2",
                    음식점상세주소 = "1층",
                    음식점위도 = 37.5801m,
                    음식점경도 = 127.0888m,
                    조리예상분 = 10
                },
                "restaurant-41"),
            default));
        Assert.Equal(음식주문상태코드.주문확인, order.상태);
        Assert.Null(order.조리시작시각Utc);
        Assert.Null(order.조리예상완료시각Utc);

        var driverId = "food-driver-7";
        var offerId = $"food-dispatch:{order.주문번호}";
        var transport = CreateRecommendedTransport(offerId, order.주문번호, driverId);
        transport.배차노출상태 = 상태값.배차노출상태.추천대기;
        transport.현재추천대상기사Id = null;
        transport.추천라운드 = 0;
        database.Context.음식운영정책.Add(new 음식운영정책 { 기사픽업지급액 = 700m });
        database.Context.Set<배달기사>().Add(new() { 기사Id = driverId, 차량 = "자동차" });
        database.Context.운송원장.Add(transport);
        await database.Context.SaveChangesAsync();
        var transition = CreatePricingTransition(database.Context);
        Assert.True((await transition.추천시작Async(offerId, driverId, 300)).전환여부);
        database.Context.ChangeTracker.Clear();
        var priced = await database.Context.운송원장.SingleAsync();
        var evidence = JsonSerializer.Deserialize<음식배달기사제안요금산정결과>(priced.기사제안요금계산근거Json!)!;
        Assert.Equal(2500m, priced.기사지급예정액);
        Assert.Equal(700m, evidence.요금.기본요금구성!.PickupFeeKrw);
        Assert.Equal(1800m, evidence.요금.기본요금구성.DropoffFeeKrw);
        // 제안 이후 현재 요율이 바뀌어도 이미 제시한 금액은 바꾸지 않는다.
        (await database.Context.음식운영정책.SingleAsync()).기사기본지급액 = 9000m;
        await database.Context.SaveChangesAsync();
        store.배차대기반영(order.주문번호, database.Context.운송원장.Single().Id, DateTime.UtcNow);

        var driverWork = CreateDriverWork(database, store, publisher, driverId);
        var offer = Assert.Single(await driverWork.제안조회Async(driverId));
        Assert.Equal(2500m, offer.DriverPayout);

        Assert.True((await driverWork.수락Async(driverId, offerId)).IsSuccess);
        order = store.GetOrder(order.주문번호)!;
        Assert.True(order.조리시작가능);
        Assert.True((await driverWork.픽업완료Async(driverId, offerId)).IsFailed);
        var startRequest = new 음식점주문진행변경요청
        {
            클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
            작업 = 음식점주문진행작업코드.조리시작
        };
        order = Assert.IsType<음식주문응답>(await prepare.Handle(
            new 음식점주문진행변경Command(order.주문번호, startRequest, "restaurant-41"), default));
        var startedRevision = order.Revision;
        var duplicateStart = await prepare.Handle(
            new 음식점주문진행변경Command(order.주문번호, startRequest, "restaurant-41"), default);
        Assert.Equal(startedRevision, duplicateStart!.Revision);
        Assert.Single(publisher.Notifications.OfType<Ssalddel.Application.Food.Events.음식점주문진행변경됨Event>());
        order = Assert.IsType<음식주문응답>(await prepare.Handle(
            new 음식점주문진행변경Command(
                order.주문번호,
                new 음식점주문진행변경요청
                {
                    클라이언트요청Id = Guid.NewGuid(),
                    작업 = 음식점주문진행작업코드.픽업준비,
                    사유 = "조리와 포장을 마쳐 기사 픽업을 기다립니다."
                },
                "restaurant-41"),
            default));

        Assert.True((await driverWork.픽업완료Async(driverId, offerId)).IsSuccess);
        Assert.True((await driverWork.전달완료Async(driverId, offerId)).IsSuccess);
        database.Context.ChangeTracker.Clear();
        var completedTransport = await database.Context.운송원장.SingleAsync();
        Assert.Equal(2500m, completedTransport.기사지급예정액);
        Assert.Equal(priced.기사제안요금계산근거Json, completedTransport.기사제안요금계산근거Json);

        var receiptRequest = new 주문자음식주문수령확인요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            확인메모 = "정상 수령"
        };
        var receipt = new 주문자음식주문수령확인CommandHandler(store, publisher);
        var completed = Assert.IsType<음식주문응답>(await receipt.Handle(
            new 주문자음식주문수령확인Command(order.주문번호, receiptRequest, "customer-1"),
            default));
        var duplicate = Assert.IsType<음식주문응답>(await receipt.Handle(
            new 주문자음식주문수령확인Command(order.주문번호, receiptRequest, "customer-1"),
            default));

        var expectedStages = new[]
        {
            음식주문상태코드.주문대기,
            음식주문상태코드.주문확인,
            음식주문상태코드.기사배정,
            음식주문상태코드.조리중,
            음식주문상태코드.픽업대기,
            음식주문상태코드.픽업완료,
            음식주문상태코드.전달완료,
            음식주문상태코드.수령확인
        };
        Assert.Equal(음식주문상태코드.수령확인, completed.상태);
        Assert.Equal(completed.Revision, duplicate.Revision);
        Assert.Equal(expectedStages, completed.상태이력.Select(x => x.다음상태));
        Assert.Single(await database.Context.음식마트원장동기화Outbox
            .Where(x => x.동기화유형 == 음식마트원장동기화유형코드.음식배달완료WorldProjection)
            .ToListAsync());

        var projection = new 음식배달완료WorldProjectionService(
            database.Context,
            new 음식배달완료WorldAreaResolver(),
            NullLogger<음식배달완료WorldProjectionService>.Instance);
        Assert.Equal(1, await projection.대기항목처리Async());
        Assert.Equal(0, await projection.대기항목처리Async());

        var response = await new 음식배달완료WorldSnapshot조회UseCase(database.Context)
            .지역목록Async(음식배달완료WorldAreaStableIds.Myeonmok, 10, default);
        var snapshot = Assert.Single(response.Items);
        Assert.Equal(completed.Revision, snapshot.LifecycleRevision);
        Assert.Equal(expectedStages, snapshot.Milestones.Select(x => x.StageCode));
        Assert.False(snapshot.LocalStorageAllowed);
        Assert.False(snapshot.ReplayAllowed);

        var publicJson = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain(order.주문번호, publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("customer-1", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain(driverId, publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("101호", publicJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 기사이탈은_조리전에는_확인대기_조리후에는_조리이력을_유지한다(bool started)
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingPublisher();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호,
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        const string driverId = "food-driver-7";
        var offerId = $"food-dispatch:{order.주문번호}";
        database.Context.운송원장.Add(CreateRecommendedTransport(offerId, order.주문번호, driverId));
        await database.Context.SaveChangesAsync();
        store.배차대기반영(order.주문번호, database.Context.운송원장.Single().Id, DateTime.UtcNow);
        var driverWork = CreateDriverWork(database, store, publisher, driverId);
        Assert.True((await driverWork.수락Async(driverId, offerId)).IsSuccess);
        order = store.GetOrder(order.주문번호)!;
        if (started)
            order = store.음식점진행변경(order.주문번호,
                new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
                    작업 = 음식점주문진행작업코드.조리시작 }, "restaurant-41")!.주문;
        var cookingStartedAt = order.조리시작시각Utc;
        var result = await driverWork.중단Async(driverId, offerId,
            new 음식배달중단요청 { 클라이언트요청Id = Guid.NewGuid(), 사유Code = 음식배달중단사유Code.사고 });
        Assert.True(result.IsSuccess, string.Join(",", result.Errors.Select(x => x.Message)));
        database.Context.ChangeTracker.Clear();
        var after = store.GetOrder(order.주문번호)!;
        Assert.Equal(started ? 음식주문상태코드.조리중 : 음식주문상태코드.주문확인, after.상태);
        Assert.Equal(음식주문배차상태코드.배차대기, after.배차상태);
        Assert.False(after.조리시작가능);
        Assert.Equal(cookingStartedAt, after.조리시작시각Utc);
        Assert.NotNull(database.Context.음식배달시도.Single().중단시각Utc);
    }

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(false, true, null)]
    [InlineData(true, false, null)]
    [InlineData(true, true, null)]
    [InlineData(true, false, "")]
    [InlineData(true, true, "  ")]
    public async Task 픽업후중단은_현재재조리준비만으로_새기사인계와단일정산을완료한다(
        bool originallyPrepared, bool readyBeforeNewAcceptance, string? legacyOriginalSource)
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var publisher = new RecordingPublisher();
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호,
            new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10,
                즉시픽업가능여부 = originallyPrepared }, "restaurant-41");
        const string firstDriver = "first-recook-driver";
        const string nextDriver = "replacement-recook-driver";
        var offerId = legacyOriginalSource is null ? $"food-dispatch:{order.주문번호}" : order.주문번호;
        var transport = CreateRecommendedTransport(offerId, order.주문번호, firstDriver);
        transport.원본의뢰Id = legacyOriginalSource ?? order.주문번호;
        database.Context.운송원장.Add(transport);
        await database.Context.SaveChangesAsync();
        store.배차대기반영(order.주문번호, database.Context.운송원장.Single().Id, DateTime.UtcNow);
        var firstWork = CreateDriverWork(database, store, publisher, firstDriver);
        Assert.True((await firstWork.수락Async(firstDriver, offerId)).IsSuccess);
        order = store.GetOrder(order.주문번호)!;
        음식점주문진행변경요청? oldReadyRequest = null;
        if (!originallyPrepared)
        {
            order = store.음식점진행변경(order.주문번호,
                new() { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
                    작업 = 음식점주문진행작업코드.조리시작 }, "restaurant-41")!.주문;
            oldReadyRequest = new() { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
                작업 = 음식점주문진행작업코드.픽업준비 };
            order = store.음식점진행변경(order.주문번호, oldReadyRequest, "restaurant-41")!.주문;
        }
        var historicalReadyAt = order.픽업준비시각Utc;
        var historicalStartedAt = order.조리시작시각Utc;
        Assert.NotNull(order.CurrentPickupReadyAtUtc);
        Assert.True((await firstWork.픽업완료Async(firstDriver, offerId)).IsSuccess);
        var firstAttempt = await database.Context.음식배달시도.SingleAsync();
        var interruptionRequest = new 음식배달중단요청
        { 클라이언트요청Id = Guid.NewGuid(), 예상시도Revision = firstAttempt.Revision,
            사유Code = 음식배달중단사유Code.사고 };
        Assert.True((await firstWork.중단Async(firstDriver, offerId, interruptionRequest)).IsSuccess);
        var interruptedRevision = store.GetOrder(order.주문번호)!.Revision;
        Assert.True((await firstWork.중단Async(firstDriver, offerId, interruptionRequest)).IsSuccess);
        database.Context.ChangeTracker.Clear();
        order = store.GetOrder(order.주문번호)!;
        Assert.Equal(interruptedRevision, order.Revision);
        Assert.Equal(2, order.CurrentPreparationRound);
        Assert.Equal(음식주문상태코드.조리중, order.상태);
        Assert.Equal(historicalReadyAt, order.픽업준비시각Utc);
        Assert.Equal(historicalStartedAt, order.조리시작시각Utc);
        Assert.Null(order.CurrentPickupReadyAtUtc);
        Assert.NotNull(order.RecookingRequestedAtUtc);
        Assert.Equal(order.RecookingRequestedAtUtc, order.CurrentCookingStartedAtUtc);
        Assert.True(order.조리예상분 > 0);
        Assert.True(order.조리예상완료시각Utc > order.RecookingRequestedAtUtc);
        Assert.Contains(음식배달가능행동Projector.음식점용(order).AvailableActions,
            x => x.ActionId == 음식배달가능행동Ids.음식점픽업준비완료);
        Assert.Empty(await database.Context.음식주문기사정산.ToListAsync());
        if (oldReadyRequest is not null)
        {
            var replay = store.음식점진행변경(order.주문번호, oldReadyRequest, "restaurant-41")!;
            Assert.False(replay.새로변경됨);
            Assert.Null(replay.주문.CurrentPickupReadyAtUtc);
            Assert.Equal(order.Revision, replay.주문.Revision);
        }
        var staleReady = new 음식점주문진행변경요청
        { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision - 1,
            작업 = 음식점주문진행작업코드.픽업준비 };
        Assert.Throws<DbUpdateConcurrencyException>(() => store.음식점진행변경(order.주문번호, staleReady, "restaurant-41"));
        var currentReadyRequest = new 음식점주문진행변경요청
        { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
            작업 = 음식점주문진행작업코드.픽업준비 };
        if (readyBeforeNewAcceptance)
            order = store.음식점진행변경(order.주문번호, currentReadyRequest, "restaurant-41")!.주문;
        await RecommendReplacementAsync(database.Context, offerId, nextDriver);
        var nextWork = CreateDriverWork(database, store, publisher, nextDriver);
        Assert.True((await nextWork.수락Async(nextDriver, offerId)).IsSuccess);
        Assert.True((await firstWork.픽업완료Async(firstDriver, offerId)).IsFailed);
        Assert.True((await firstWork.전달완료Async(firstDriver, offerId)).IsFailed);
        var workspace = new FoodDeliveryDriverWorkspaceUseCase(database.Context, nextWork,
            new 배달기사월정산UseCase(database.Context, new NoOpSettlement()),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            new RecookingFeatureFlags());
        var active = Assert.Single((await workspace.GetAsync(nextDriver, default)).ActiveDeliveries);
        Assert.Equal(2, active.CurrentPreparationRound);
        Assert.Equal(order.RecookingRequestedAtUtc, active.RecookingRequestedAtUtc);
        Assert.Equal(readyBeforeNewAcceptance, active.CurrentPickupReadyAtUtc.HasValue);
        Assert.Equal(store.GetOrder(order.주문번호)!.CurrentPickupReadyAtUtc, active.CurrentPickupReadyAtUtc);
        Assert.Equal(readyBeforeNewAcceptance, active.AvailableActions.Any(x => x.ActionId == 음식배달가능행동Ids.기사픽업확인));
        if (!readyBeforeNewAcceptance)
        {
            Assert.True((await nextWork.픽업완료Async(nextDriver, offerId)).IsFailed);
            order = store.GetOrder(order.주문번호)!;
            currentReadyRequest.예상Revision = order.Revision;
            order = store.음식점진행변경(order.주문번호, currentReadyRequest, "restaurant-41")!.주문;
        }
        var duplicateReady = store.음식점진행변경(order.주문번호, currentReadyRequest, "restaurant-41")!;
        Assert.False(duplicateReady.새로변경됨);
        Assert.Equal(store.GetOrder(order.주문번호)!.Revision, duplicateReady.주문.Revision);
        Assert.Equal(historicalReadyAt, duplicateReady.주문.픽업준비시각Utc);
        Assert.True(duplicateReady.주문.CurrentPickupReadyAtUtc >= duplicateReady.주문.RecookingRequestedAtUtc);
        var beforePickup = 음식배달수명주기SnapshotFactory.FromOperationalOrder(duplicateReady.주문, "recooking-fixture");
        Assert.Null(beforePickup.PickedUpAtUtc);
        Assert.Equal(duplicateReady.주문.CurrentPickupReadyAtUtc, beforePickup.ReadyForPickupAtUtc);
        active = Assert.Single((await workspace.GetAsync(nextDriver, default)).ActiveDeliveries);
        Assert.Equal(store.GetOrder(order.주문번호)!.CurrentPickupReadyAtUtc, active.CurrentPickupReadyAtUtc);
        Assert.Contains(active.AvailableActions, x => x.ActionId == 음식배달가능행동Ids.기사픽업확인);
        Assert.True((await nextWork.픽업완료Async(nextDriver, offerId)).IsSuccess);
        Assert.True((await nextWork.픽업완료Async(nextDriver, offerId)).IsSuccess);
        Assert.True((await nextWork.전달완료Async(nextDriver, offerId)).IsSuccess);
        Assert.True((await nextWork.전달완료Async(nextDriver, offerId)).IsSuccess);
        database.Context.ChangeTracker.Clear();
        var attempts = await database.Context.음식배달시도.OrderBy(x => x.시도순번).ToListAsync();
        Assert.Equal(2, attempts.Count);
        Assert.NotNull(attempts[0].중단시각Utc);
        Assert.NotNull(attempts[0].재조리요청StableId);
        Assert.Null(attempts[0].전달완료시각Utc);
        Assert.NotNull(attempts[1].전달완료시각Utc);
        var deliveredSnapshot = 음식배달수명주기SnapshotFactory.FromOperationalOrder(store.GetOrder(order.주문번호)!, "recooking-fixture");
        Assert.Equal(attempts[1].픽업완료시각Utc, deliveredSnapshot.PickedUpAtUtc);
        Assert.Equal(attempts[1].전달완료시각Utc, deliveredSnapshot.DeliveredAtUtc);
        var settlement = Assert.Single(await database.Context.음식주문기사정산.ToListAsync());
        Assert.Equal(nextDriver, settlement.기사Id);
        Assert.Equal(attempts[1].시도StableId, settlement.배달시도StableId);
        var receipt = new 주문자음식주문수령확인요청 { 클라이언트요청Id = Guid.NewGuid() };
        store.주문자수령확인(order.주문번호, receipt, "customer-1");
        store.주문자수령확인(order.주문번호, receipt, "customer-1");
        Assert.Equal(음식주문상태코드.수령확인, store.GetOrder(order.주문번호)!.상태);
        Assert.Single(await database.Context.음식주문기사정산.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 픽업전재배차는_일반조리와즉시준비의현재Ready를유지한다(bool originallyPrepared)
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var publisher = new RecordingPublisher();
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(),
            즉시픽업가능여부 = originallyPrepared, 조리예상분 = 10 }, "restaurant-41");
        const string firstDriver = "first-ready-driver";
        const string nextDriver = "next-ready-driver";
        var offerId = $"food-dispatch:{order.주문번호}";
        database.Context.운송원장.Add(CreateRecommendedTransport(offerId, order.주문번호, firstDriver));
        await database.Context.SaveChangesAsync();
        store.배차대기반영(order.주문번호, database.Context.운송원장.Single().Id, DateTime.UtcNow);
        var first = CreateDriverWork(database, store, publisher, firstDriver);
        Assert.True((await first.수락Async(firstDriver, offerId)).IsSuccess);
        order = store.GetOrder(order.주문번호)!;
        if (!originallyPrepared)
        {
            order = store.음식점진행변경(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(),
                예상Revision = order.Revision, 작업 = 음식점주문진행작업코드.조리시작 }, "restaurant-41")!.주문;
            order = store.음식점진행변경(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(),
                예상Revision = order.Revision, 작업 = 음식점주문진행작업코드.픽업준비 }, "restaurant-41")!.주문;
        }
        var readyAt = order.CurrentPickupReadyAtUtc;
        Assert.NotNull(readyAt);
        Assert.True((await first.중단Async(firstDriver, offerId, new() { 클라이언트요청Id = Guid.NewGuid(),
            사유Code = 음식배달중단사유Code.사고 })).IsSuccess);
        order = store.GetOrder(order.주문번호)!;
        Assert.Equal(1, order.CurrentPreparationRound);
        Assert.Equal(readyAt, order.CurrentPickupReadyAtUtc);
        Assert.Null(order.RecookingRequestedAtUtc);
        await RecommendReplacementAsync(database.Context, offerId, nextDriver);
        var next = CreateDriverWork(database, store, publisher, nextDriver);
        Assert.True((await next.수락Async(nextDriver, offerId)).IsSuccess);
        Assert.True((await next.픽업완료Async(nextDriver, offerId)).IsSuccess);
        Assert.True((await next.전달완료Async(nextDriver, offerId)).IsSuccess);
        Assert.Equal(readyAt, store.GetOrder(order.주문번호)!.CurrentPickupReadyAtUtc);
    }

    private static async Task RecommendReplacementAsync(SsalddelContext db, string offerId, string driverId)
    {
        // 후보 선정 엔진은 이 fixture 범위 밖입니다. 영속 중단 결과를 보존한 다음 추천 결과만 준비합니다.
        db.ChangeTracker.Clear();
        var queue = await db.운송원장.SingleAsync(x => x.의뢰Id == offerId);
        Assert.Null(queue.확정기사Id);
        queue.상태 = 상태값.배차대기상태.대기;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천중;
        queue.현재추천대상기사Id = driverId;
        queue.추천시작시각 = DateTime.UtcNow;
        queue.추천만료시각 = DateTime.UtcNow.AddMinutes(5);
        queue.추천라운드++;
        await db.SaveChangesAsync();
    }

    private sealed class RecookingFeatureFlags : IVersionFeatureFlagService
    {
        public bool IsEnabled(string featureKey) => false;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>();
    }

    private static 음식배달기사업무Service CreateDriverWork(
        TestDatabase database, EfSsalddelFoodOrderStore store, RecordingPublisher publisher, string driverId,
        I운영배차공통UseCase? receiving = null)
        => CreateDriverWork(database.Context, store, publisher, driverId, receiving);

    private static 음식배달기사업무Service CreateDriverWork(
        SsalddelContext context, EfSsalddelFoodOrderStore store, RecordingPublisher publisher, string driverId,
        I운영배차공통UseCase? receiving = null)
        => new 음식배달기사업무Service(
            context,
            new InMemoryDriverLocationStore(),
            new NoOpRouteService(),
            new NoOpQueueTransition(),
            new InMemory음식배달권실행공간Store(),
            store,
            new NoOpFoodLedgerOutbox(),
            new NoOpTransportSync(),
            new NoOpRestaurantNotification(),
            new NoOpSettlement(),
            publisher,
            new TestCurrentUserAccessor(driverId, "Driver"),
            NullLogger<음식배달기사업무Service>.Instance,
            receiving ?? new TestDispatchAvailability());

    [Fact]
    public async Task 수신OFF는기존추천수락을차단하지만_수락된업무와도착중단을유지한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호, new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        const string driverId = "intent-driver";
        var offerId = $"food-dispatch:{order.주문번호}";
        database.Context.운송원장.Add(CreateRecommendedTransport(offerId, order.주문번호, driverId));
        await database.Context.SaveChangesAsync();
        store.배차대기반영(order.주문번호, database.Context.운송원장.Single().Id, DateTime.UtcNow);
        var intent = new 운영배차공통UseCase(new Ef운영배차활동원장Store(database.Context),
            new InMemory운영배차판정ProjectionStore(), new 살뜰.도메인.배차.운영배차수신상태Policy(),
            new 살뜰.도메인.배차.운영배차단기지표Calculator(), TimeProvider.System,
            NullLogger<운영배차공통UseCase>.Instance);
        var driver = CreateDriverWork(database, store, new RecordingPublisher(), driverId, intent);
        Assert.Empty(await driver.제안조회Async(driverId));
        var blocked = await driver.수락Async(driverId, offerId);
        Assert.True(blocked.IsFailed);
        Assert.Equal("FoodDelivery.DispatchReceivingOff", blocked.Errors[0].Metadata["ErrorCode"]);
        Assert.Empty(database.Context.음식배달시도);
        await intent.기사의사변경Async(driverId, new() { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.On });
        Assert.Single(await driver.제안조회Async(driverId));
        Assert.True((await driver.수락Async(driverId, offerId)).IsSuccess);
        await intent.기사의사변경Async(driverId, new() { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.Off });
        var active = Assert.Single(await driver.제안조회Async(driverId));
        Assert.NotEqual(Ssalddel.Contracts.Common.Drivers.DriverWorkOfferStatus.Recommended, active.Status);
        var attempt = await database.Context.음식배달시도.SingleAsync();
        Assert.True((await driver.가게도착Async(driverId, offerId, new() { 클라이언트요청Id = Guid.NewGuid(), 예상시도Revision = attempt.Revision })).IsSuccess);
        var interruption = new 음식배달중단요청 { 클라이언트요청Id = Guid.NewGuid(), 예상시도Revision = attempt.Revision, 사유Code = 음식배달중단사유Code.사고, 메모 = "응답 유실 검증" };
        Assert.True((await driver.중단Async(driverId, offerId, interruption)).IsSuccess);
        var revision = attempt.Revision;
        var events = await database.Context.운영배차활동사건.CountAsync();
        Assert.True((await driver.중단Async(driverId, offerId, interruption)).IsSuccess);
        Assert.Equal(revision, attempt.Revision);
        Assert.Equal(events, await database.Context.운영배차활동사건.CountAsync());
        Assert.True((await driver.중단Async("another-driver", offerId, interruption)).IsFailed);
        interruption.메모 = "다른 내용";
        Assert.True((await driver.중단Async(driverId, offerId, interruption)).IsFailed);
        Assert.Equal(운영배차수신의사Code.Off, (await intent.수신상태조회Async(driverId)).수신의사Code);
        Assert.Equal(2, await database.Context.운영배차활동사건.CountAsync(x => x.사건유형Code == 운영배차사건유형Code.수신의사변경));
    }

    [Fact]
    public async Task 새배차의거리미확인은_추천전저장과알림을차단하고_확인후재시도가가능하다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var queue = CreateRecommendedTransport("pricing-missing", "order-test", "driver-test");
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        queue.현재추천대상기사Id = null;
        queue.추천라운드 = 0;
        queue.하차_위도 = null;
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        var notification = new PricingNotification();
        database.Context.Set<살뜰.도메인.기사.배달기사>().Add(new() { 기사Id = "driver-test", 차량 = "자동차" });
        await database.Context.SaveChangesAsync();
        var transition = CreatePricingTransition(database.Context, notification);
        var blocked = await transition.추천시작Async(queue.의뢰Id, "driver-test");
        Assert.False(blocked.전환여부);
        Assert.Equal(배차대기원장전환결과코드.배차구성오류, blocked.결과코드);
        database.Context.ChangeTracker.Clear();
        var after = await database.Context.운송원장.SingleAsync();
        Assert.Null(after.기사지급예정액);
        Assert.Null(after.기사제안요금계산근거Json);
        Assert.Equal(0, after.추천라운드);
        Assert.Equal(0, notification.Count);
        after.하차_위도 = 37.5792m;
        await database.Context.SaveChangesAsync();
        Assert.True((await transition.추천시작Async(queue.의뢰Id, "driver-test")).전환여부);
        Assert.Equal(1, notification.Count);
        var frozen = after.기사제안요금계산근거Json;
        after.배차노출상태 = 상태값.배차노출상태.추천대기;
        after.현재추천대상기사Id = null;
        await database.Context.SaveChangesAsync();
        Assert.True((await transition.추천시작Async(queue.의뢰Id, "driver-test")).전환여부);
        Assert.Equal(frozen, after.기사제안요금계산근거Json);
        after.배차노출상태 = 상태값.배차노출상태.추천대기;
        after.현재추천대상기사Id = null;
        await database.Context.SaveChangesAsync();
        var count = notification.Count;
        Assert.False((await transition.추천시작Async(queue.의뢰Id, "unknown-driver")).전환여부);
        Assert.Equal(count, notification.Count);
        Assert.Equal(frozen, after.기사제안요금계산근거Json);
        database.Context.Set<살뜰.도메인.기사.배달기사>().Add(new() { 기사Id = "next-driver", 차량 = "오토바이" });
        await database.Context.SaveChangesAsync();
        Assert.True((await transition.추천시작Async(queue.의뢰Id, "next-driver")).전환여부);
        database.Context.ChangeTracker.Clear();
        var repriced = await database.Context.운송원장.SingleAsync();
        var nextEvidence = JsonSerializer.Deserialize<음식배달기사제안요금산정결과>(repriced.기사제안요금계산근거Json!)!;
        Assert.Equal("next-driver", nextEvidence.기사Id);
        Assert.Equal("Motorcycle", nextEvidence.경로차량Code);
        Assert.Equal("traavoidcaronly", nextEvidence.경로옵션Code);
        Assert.Equal(3850m, repriced.기사지급예정액);
        Assert.NotEqual(frozen, repriced.기사제안요금계산근거Json);
    }

    [Fact]
    public async Task 네이버실패의근사거리로_배차제안이나알림을_생성하지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var queue = CreateRecommendedTransport("pricing-route-unavailable", "order-test", "driver-test");
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        queue.현재추천대상기사Id = null;
        queue.추천라운드 = 0;
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        var notification = new PricingNotification();
        database.Context.Set<살뜰.도메인.기사.배달기사>().Add(new() { 기사Id = "driver-test", 차량 = "자동차" });
        await database.Context.SaveChangesAsync();
        var transition = CreatePricingTransition(database.Context, notification, routeAvailable: false);
        var blocked = await transition.추천시작Async(queue.의뢰Id, "driver-test");
        Assert.False(blocked.전환여부);
        Assert.Equal(배차대기원장전환결과코드.배차구성오류, blocked.결과코드);
        database.Context.ChangeTracker.Clear();
        var after = await database.Context.운송원장.SingleAsync();
        Assert.Null(after.기사지급예정액);
        Assert.Null(after.기사제안요금계산근거Json);
        Assert.Equal(0, after.추천라운드);
        Assert.Equal(0, notification.Count);
    }

    private static 배차대기원장전환Service CreatePricingTransition(SsalddelContext db, PricingNotification? notification = null, bool routeAvailable = true)
        => new(db, Options.Create(new 배차큐정책Options()), null!, notification ?? new PricingNotification(),
            new PricingDriverState(), new 음식배달배차흐름Resolver(), new 음식배달기사제안요금Service(db,
                new NoOpRouteService(routeAvailable), new PricingWeather(), TimeProvider.System,
                new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions
                    { Mode = SsalddelExecutionMode.Simulation }))), new PricingFoodRecommendationRecord());

    private sealed class PricingFoodRecommendationRecord : I음식배달기사추천기록Service
    {
        public Task 추천기록Async(string driverId, DateTime 추천시각Utc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class PricingWeather : I픽업지기상관측Client
    {
        public Task<픽업지기상관측결과> 조회Async(decimal? latitude, decimal? longitude, CancellationToken cancellationToken = default)
            => Task.FromResult(new 픽업지기상관측결과(false, false, 픽업지기상자료상태Code.MissingServiceKey,
                null, null, null, 픽업지기상관측결과.공식자료출처, null));
    }

    private sealed class PricingNotification : I배차추천알림Service
    {
        public int Count { get; private set; }
        public Task 추천알림요청생성Async(long 배차대기Id, string 의뢰Id, string 기사Id, int 추천라운드,
            CancellationToken cancellationToken = default) { Count++; return Task.CompletedTask; }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class PricingDriverState : I국내화물운송기사상태Service
    {
        public Task<국내화물운송기사상태Snapshot?> 추천기록Async(string driverId, DateTime 추천시각Utc,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("음식 배달 요금 전환은 화물 기사 상태 Port를 호출할 수 없습니다.");
        public Task<국내화물운송기사상태Snapshot> 운행시작Async(string driverId, long shiftId, DateTime startedAtUtc,
            string startMode, string startLocation, string? returnDestination, string? 복귀콜선호 = null,
            string? appKey = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<국내화물운송기사상태Snapshot> 위치갱신Async(DriverLocationSnapshot location, long? shiftId = null,
            decimal? 상차접근허용반경Km = null, string? appKey = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<국내화물운송기사상태Snapshot?> 후보없음기록Async(string driverId, DateTime 기준시각Utc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task 운행종료Async(string driverId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static 음식주문등록요청 CreateOrderRequest() => new()
    {
        클라이언트요청Id = Guid.NewGuid(),
        음식점Id = 41,
        주문자UserId = "customer-1",
        수령인정보 = new 음식주문수령인정보Dto
        {
            수령인명 = "테스트 주문자",
            연락처 = "010-0000-0000",
            주소 = "서울특별시 중랑구 면목동 면목로 1",
            상세주소 = "101호",
            주문자본인수령여부 = true
        },
        상품목록 =
        [
            new 음식주문상품Dto { 메뉴Id = 10, 상품명 = "비빔밥", 수량 = 1, 단가 = 9000 }
        ]
    };

    private static 운송원장 CreateRecommendedTransport(string offerId, string orderNo, string driverId)
        => new()
        {
            운송번호 = offerId,
            의뢰Id = offerId,
            원본의뢰Id = orderNo,
            원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
            화주Id = "restaurant-41",
            배차업무유형 = 상태값.배차업무유형.음식배달,
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = driverId,
            추천시작시각 = DateTime.UtcNow,
            추천만료시각 = DateTime.UtcNow.AddMinutes(5),
            추천라운드 = 1,
            픽업_도로명주소 = "서울특별시 중랑구 면목동 면목로 2",
            픽업_상세주소 = "1층",
            픽업_위도 = 37.5801m,
            픽업_경도 = 127.0888m,
            하차_도로명주소 = "서울특별시 중랑구 면목동 면목로 1",
            하차_상세주소 = "101호",
            하차_위도 = 37.5792m,
            하차_경도 = 127.0875m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private sealed class PassThroughMenuValidationService : I음식주문메뉴검증Service
    {
        public Task<음식주문등록요청> 서버기준요청생성Async(
            음식주문등록요청 request,
            CancellationToken cancellationToken = default)
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

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed record TestCurrentUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;

    private sealed class NoOpRouteService(bool routeAvailable = true) : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string routeOption, CancellationToken cancellationToken)
            => Task.FromResult<배차경로예상결과?>(new(routeOption == "traavoidcaronly" ? 2.5m : 0m, TimeSpan.FromMinutes(5), 0m,
                routeAvailable ? "Directions5" : "좌표기반도로보정", routeAvailable) { RouteOption = routeOption });

        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => Task.FromResult<배차경로예상결과?>(routeAvailable ? new(0m, TimeSpan.Zero, 0m, "Directions5", true) : new(1m, TimeSpan.FromMinutes(5), null, "좌표기반도로보정", false));

        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default)
            => Task.FromResult<배차경로예상결과?>(null);

        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
            => Task.FromResult<배차삽입경로예상결과?>(null);

        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => 0m;
    }

    private sealed class NoOpQueueTransition : I배차대기원장전환Service
    {
        public Task<배차대기원장전환결과> 계획배차에서추천으로전환Async(string requestId, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 추천대기처리Async(string requestId, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 추천시작Async(string requestId, string driverId, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
            => NotChanged(requestId, driverId);

        public Task<배차대기원장전환결과> 추천거절처리Async(string requestId, string driverId, CancellationToken cancellationToken = default)
            => NotChanged(requestId, driverId);

        public Task<배차대기원장전환결과> 추천거절처리Async(string requestId, string driverId, string? reasonCode, CancellationToken cancellationToken = default)
            => NotChanged(requestId, driverId);

        public Task<배차대기원장전환결과> 추천만료처리Async(string requestId, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 공개배차로전환Async(string requestId, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 실행주체확정결과동기화Async(string requestId, DispatchConfirmationBoundaryRequest confirmation, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 배차수락취소처리Async(string requestId, string driverId, string? reason = null, CancellationToken cancellationToken = default)
            => NotChanged(requestId, driverId);

        private static Task<배차대기원장전환결과> NotChanged(string requestId, string? driverId = null)
            => Task.FromResult(배차대기원장전환결과.전환안됨(
                requestId,
                배차대기원장전환결과코드.단계불일치,
                "정상 생명주기 시험에서는 재배차 전환을 사용하지 않습니다.",
                driverId));
    }

    private sealed class NoOpSettlement : I기사월정산Service
    {
        public Task<기사월정산> 배차확정반영Async(string 기사Id, DateTime? 기준시각Utc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(기사Id, 기준시각Utc));

        public Task<기사월정산> 월마감처리Async(string 기사Id, int 년도, int 월, DateTime? 기준시각Utc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(기사Id, 기준시각Utc));

        public Task<기사월정산> 월말청구결제완료처리Async(string 기사Id, int 년도, int 월, DateTime? 기준시각Utc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(기사Id, 기준시각Utc));

        private static 기사월정산 Create(string driverId, DateTime? now)
        {
            var at = now ?? DateTime.UtcNow;
            return new 기사월정산 { 기사Id = driverId, 년도 = at.Year, 월 = at.Month };
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

        public Task 출고원장예약후즉시처리Async(
            IReadOnlyList<출고예정> outbounds,
            IReadOnlyList<입고요청> inbounds,
            string updatedBy,
            string idempotencyKey,
            string? currentStageKey = null,
            string? ledgerTemplateKey = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class NoOpRestaurantNotification : I음식점주문실시간알림Service
    {
        public Task 신규주문알림발송Async(음식주문응답 order, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task 주문상태변경알림발송Async(음식주문응답 order, string reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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

        public SsalddelContext CreateContext()
            => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(_connection).Options,
                new PassThroughEncryptionService());

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SsalddelContext>()
                .UseSqlite(connection)
                .Options;
            var context = new SsalddelContext(options, new PassThroughEncryptionService());
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
