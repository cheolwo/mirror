using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Mart;
using Ssalddel.Controllers.Common;
using Ssalddel.Controllers.Driver.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using Ssalddel.Services.Operations;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Operations;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.사용자;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.운영;
using 살뜰.도메인.화주;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Operations;

/// <summary>
/// 실제 마트 포장 원천 → OS 인계/공통 큐 → Controller/기사 Service → 전표/SQL 상태/후속 의도를 검증합니다.
/// SQLite EnsureCreated와 명시 시험 Gate를 사용하며 실제 인증 HTTP, MySQL, Mongo, Redis, 단말/인쇄 시험은 아닙니다.
/// </summary>
public sealed class MartLastMileDriverSoftwareLifecycleTests
{
    private const string Order = "mart-r58-two-lines";
    private const string DriverId = "mart-r58-driver";
    private const string SellerId = "mart-r58-seller";
    private static readonly DateTime InitialTime = new(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 실제포장원천과큐에서_전표제출한번으로픽업하고_새Context중복에도재고와완료를한번만저장한다(bool byNumber)
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedAndDispatchAsync(fixture.Db, fixture.Clock, fixture.Boundaries);
        var queue = await fixture.Db.운송원장.SingleAsync();
        var offerId = queue.의뢰Id;

        var warehouse = new 마트전표Controller(Service(fixture.Db, fixture.Clock, SellerId, 역할명.창고관리자))
            { ControllerContext = ControllerContext(SellerId, 역할명.창고관리자) };
        var slip = Value<마트포장전표Dto>(await warehouse.발급(Order,
            new 마트포장전표발급요청 { ClientRequestId = Guid.NewGuid() }, default));
        Assert.True(slip.Active);
        Assert.Equal(4, slip.ConfirmationCode.Length);
        var labelsBeforeGet = await fixture.Db.Set<마트포장전표>().CountAsync();
        Assert.Equal(slip.Revision, Value<마트포장전표Dto>(await warehouse.조회(Order, default)).Revision);
        Assert.Equal(labelsBeforeGet, await fixture.Db.Set<마트포장전표>().CountAsync());

        var controller = Controller(fixture.Db, fixture.Clock);
        var list = Value<마트기사업무목록Dto>(await controller.조회(default));
        var offered = Assert.Single(list.Items);
        Assert.True(offered.CanAccept);
        var serializedOffer = JsonSerializer.Serialize(offered);
        Assert.DoesNotContain(slip.QrCode, serializedOffer);
        // 무작위 네 자리 번호가 날짜 등에 우연히 포함되는 경우와 실제 필드 유출을 구별한다.
        Assert.DoesNotContain("\"ConfirmationCode\"", serializedOffer);
        Assert.DoesNotContain("\"QrCode\"", serializedOffer);
        var acceptRequest = new 마트기사수락요청
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRecommendationRound = offered.RecommendationRound,
            ExpectedQueueUpdatedAtUtc = offered.QueueUpdatedAtUtc
        };
        var accepted = Value<마트기사업무항목Dto>(await controller.수락(offerId, acceptRequest, default));
        Assert.Equal(1, accepted.Revision);
        // 명령 결과만 추정하지 않고 같은 업무 정본을 재조회한다.
        Assert.True(Value<마트기사업무항목Dto>(await controller.업무조회(offerId, default)).CanPickup);
        Assert.False(accepted.CanDeliver);

        var pickupRequest = new 마트기사픽업요청
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            ExpectedLabelRevision = slip.Revision, ByNumber = byNumber,
            Code = byNumber ? slip.ConfirmationCode : slip.QrCode
        };
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await using var pickupDb = fixture.CreateContext();
        var picked = Value<마트기사업무항목Dto>(await Controller(pickupDb, fixture.Clock).픽업(offerId, pickupRequest, default));
        Assert.Equal(2, picked.Revision);
        Assert.False(picked.CanPickup);
        Assert.True(picked.CanDeliver);
        var pickupState = await StateAsync(pickupDb);

        await using var pickupReplayDb = fixture.CreateContext();
        var pickupReplay = Value<마트기사업무항목Dto>(await Controller(pickupReplayDb, fixture.Clock).픽업(offerId, pickupRequest, default));
        Assert.True(pickupReplay.IsReplay);
        Assert.Equal(pickupState, await StateAsync(pickupReplayDb));
        Assert.All(await pickupReplayDb.입고상품.AsNoTracking().ToListAsync(), item => Assert.Equal(0, item.가용수량));
        Assert.All(await pickupReplayDb.출고예정.AsNoTracking().ToListAsync(), plan => Assert.Equal(출고상태.출고완료, plan.상태));

        var deliveryRequest = new 마트기사전달요청 { ClientRequestId = Guid.NewGuid(), ExpectedRevision = picked.Revision };
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await using var deliverDb = fixture.CreateContext();
        var delivered = Value<마트기사업무항목Dto>(await Controller(deliverDb, fixture.Clock).전달(offerId, deliveryRequest, default));
        Assert.Equal(3, delivered.Revision);
        Assert.False(delivered.CanPickup);
        Assert.False(delivered.CanDeliver);
        var finalState = await StateAsync(deliverDb);

        await using var replayDb = fixture.CreateContext();
        var replayController = Controller(replayDb, fixture.Clock);
        var replay = Value<마트기사업무항목Dto>(await replayController.전달(offerId, deliveryRequest, default));
        Assert.True(replay.IsReplay);
        Assert.Equal(delivered.Revision, replay.Revision);
        Assert.Equal(delivered.AttemptStableId,
            Value<마트기사업무항목Dto>(await replayController.요청결과조회(deliveryRequest.ClientRequestId, default)).AttemptStableId);
        Assert.Equal(finalState, await StateAsync(replayDb));
        var attempt = await replayDb.Set<마트배송시도>().AsNoTracking().SingleAsync();
        Assert.NotNull(attempt.픽업시각Utc);
        Assert.NotNull(attempt.전달시각Utc);
        Assert.Equal(3, attempt.Revision);
        Assert.Single(await replayDb.운영체제업무인계.ToListAsync());
        Assert.Equal(2, await replayDb.운영체제업무인계Outbox.CountAsync());
        Assert.Single(await replayDb.운송원장.ToListAsync());
        Assert.Empty(await replayDb.음식주문.ToListAsync());
        Assert.Empty(await replayDb.음식배달시도.ToListAsync());
        Assert.Empty(await replayDb.화주운송의뢰.ToListAsync());
        Assert.Empty(await replayDb.음식주문기사정산.ToListAsync());
        Assert.Empty(await replayDb.음식주문기사지급검증.ToListAsync());
        Assert.Empty(await replayDb.기사운송대금지급요청.ToListAsync());
        Assert.Empty(await replayDb.기사지급Outbox.ToListAsync());
        Assert.Equal(상태값.배차상태.하차완료, (await replayDb.운송원장.AsNoTracking().SingleAsync()).상태);
        Assert.Equal("전달완료", (await replayDb.마트주문.AsNoTracking().SingleAsync()).현재단계);
        Assert.Equal(2, await replayDb.재고이력.CountAsync(item => item.이력유형 == "포장"));
        Assert.Equal(4, await replayDb.피킹포장작업.CountAsync());
        Assert.Equal(9, await replayDb.Set<마트기사후속Outbox>().CountAsync());
        Assert.Equal(3, await replayDb.Set<마트기사요청영수증>().CountAsync(row => row.작업자UserId == DriverId));
        var events = await replayDb.운송이벤트.AsNoTracking()
            .Where(item => item.이벤트타입 == "MartDriverWorkStateChanged").OrderBy(item => item.이벤트시각).ToListAsync();
        Assert.Equal(3, events.Count);
        for (var index = 0; index < events.Count; index++)
        {
            Assert.Equal(offerId, events[index].의뢰Id);
            using var metadata = JsonDocument.Parse(events[index].메타데이터);
            Assert.Equal(new[] { "Action", "RequestId", "Revision" }, metadata.RootElement.EnumerateObject()
                .Select(property => property.Name).OrderBy(name => name).ToArray());
            Assert.Equal(new[] { "Accept", "Pickup", "Deliver" }[index], metadata.RootElement.GetProperty("Action").GetString());
            Assert.Equal(index + 1, metadata.RootElement.GetProperty("Revision").GetInt32());
        }
    }

    [Fact]
    public async Task 두번째재고현재조건이충돌하면_먼저바뀐재고와출고도함께롤백하고같은전표로다시검토한다()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedAndDispatchAsync(fixture.Db, fixture.Clock, fixture.Boundaries);
        var slip = (await Service(fixture.Db, fixture.Clock, SellerId, 역할명.창고관리자).전표발급Async(Order,
            new 마트포장전표발급요청 { ClientRequestId = Guid.NewGuid() })).Value;
        var service = Service(fixture.Db, fixture.Clock);
        var offered = Assert.Single((await service.조회Async(DriverId)).Value.Items);
        var accepted = (await service.수락Async(DriverId, offered.OfferId, new 마트기사수락요청
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRecommendationRound = offered.RecommendationRound,
            ExpectedQueueUpdatedAtUtc = offered.QueueUpdatedAtUtc
        })).Value;
        // 준비 사본 뒤 실제 출고 CAS가 각각 재고를 재검증해야 한다. 첫 줄은 유효하고 둘째 줄만 예약 충돌이다.
        var lastInventory = await fixture.Db.입고상품.OrderByDescending(item => item.Id).FirstAsync();
        lastInventory.예약수량 = 1;
        await fixture.Db.SaveChangesAsync();
        var before = await StateAsync(fixture.Db);
        var request = new 마트기사픽업요청 { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            ExpectedLabelRevision = slip.Revision, Code = slip.QrCode };

        await using var commandDb = fixture.CreateContext();
        var failed = await Service(commandDb, fixture.Clock).픽업Async(DriverId, offered.OfferId, request);

        Assert.True(failed.IsFailed);
        await using var reloaded = fixture.CreateContext();
        Assert.Equal(before, await StateAsync(reloaded));
        Assert.Equal(1, (await reloaded.Set<마트배송시도>().AsNoTracking().SingleAsync()).Revision);
        Assert.True((await reloaded.Set<마트포장전표>().AsNoTracking().SingleAsync()).활성여부);
        Assert.Equal(0, await reloaded.재고이동.CountAsync());
        Assert.All(await reloaded.출고예정.AsNoTracking().ToListAsync(), plan => Assert.Equal(출고상태.준비중, plan.상태));
        Assert.False(commandDb.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task commit된기사수락후_외부후속실패는_새Context독립재처리하고업무판본을되돌리지않는다()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedAndDispatchAsync(fixture.Db, fixture.Clock, fixture.Boundaries);
        var controller = Controller(fixture.Db, fixture.Clock);
        var offer = Assert.Single(Value<마트기사업무목록Dto>(await controller.조회(default)).Items);
        var accepted = Value<마트기사업무항목Dto>(await controller.수락(offer.OfferId, new 마트기사수락요청
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRecommendationRound = offer.RecommendationRound,
            ExpectedQueueUpdatedAtUtc = offer.QueueUpdatedAtUtc
        }, default));
        var originalAttempt = JsonSerializer.Serialize(await fixture.Db.Set<마트배송시도>().AsNoTracking().SingleAsync());
        var originalQueue = JsonSerializer.Serialize(await fixture.Db.운송원장.AsNoTracking().SingleAsync());
        var originalKeys = await fixture.Db.Set<마트기사후속Outbox>().Select(row => row.멱등키).OrderBy(key => key).ToListAsync();
        Assert.NotEmpty(originalKeys);
        fixture.Boundaries.FailMongoOnce = true;

        await using (var processingDb = fixture.CreateContext())
        {
            await Consumer(processingDb, fixture).처리Async();
            Assert.Contains(await processingDb.Set<마트기사후속Outbox>().AsNoTracking().ToListAsync(),
                row => row.상태Code == "Pending" && row.시도횟수 > 0);
        }
        fixture.Clock.Advance(TimeSpan.FromHours(1));
        await using var retryDb = fixture.CreateContext();
        await Consumer(retryDb, fixture).처리Async();
        Assert.All(await retryDb.Set<마트기사후속Outbox>().AsNoTracking().ToListAsync(), row => Assert.Equal("Succeeded", row.상태Code));
        Assert.Equal(originalKeys, await retryDb.Set<마트기사후속Outbox>().Select(row => row.멱등키).OrderBy(key => key).ToListAsync());
        Assert.Equal(originalAttempt, JsonSerializer.Serialize(await retryDb.Set<마트배송시도>().AsNoTracking().SingleAsync()));
        Assert.Equal(originalQueue, JsonSerializer.Serialize(await retryDb.운송원장.AsNoTracking().SingleAsync()));
        Assert.Equal(accepted.Revision, (await Service(retryDb, fixture.Clock).업무조회Async(DriverId, offer.OfferId)).Value.Revision);
        var callsBeforeReplay = fixture.Boundaries.MongoCalls;
        await Consumer(retryDb, fixture).처리Async();
        Assert.Equal(callsBeforeReplay, fixture.Boundaries.MongoCalls);
        Assert.Empty(await retryDb.음식주문.ToListAsync());
        Assert.Empty(await retryDb.음식배달시도.ToListAsync());
    }

    [Fact]
    public async Task 실제기존원장소비는_픽업을전체완료로올리지않고_전달뒤옛예약도현재마트단계를보존한다()
    {
        const string reference = "r58-plain-work-reference"; // 접두어 추론이 아니라 명시 마트 템플릿을 검증한다.
        const string oldKey = "r58-old-outbound-intent";
        await using var fixture = await DbFixture.CreateAsync();
        await SeedAndDispatchAsync(fixture.Db, fixture.Clock, fixture.Boundaries, reference);
        var service = Service(fixture.Db, fixture.Clock);
        var slip = (await Service(fixture.Db, fixture.Clock, SellerId, 역할명.창고관리자).전표발급Async(reference,
            new 마트포장전표발급요청 { ClientRequestId = Guid.NewGuid() })).Value;
        var offer = Assert.Single((await service.조회Async(DriverId)).Value.Items);
        var accepted = (await service.수락Async(DriverId, offer.OfferId, new 마트기사수락요청
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRecommendationRound = offer.RecommendationRound,
            ExpectedQueueUpdatedAtUtc = offer.QueueUpdatedAtUtc
        })).Value;
        var store = new 기록원장저장소 { FailNextWrite = true };
        await using (var oldDb = fixture.CreateContext())
        {
            // 실패하여 남은 과거 포장/창고 템플릿 payload가 나중에 최신 업무를 되감을 수 없어야 한다.
            await RealLedgerOutbox(oldDb, store).출고원장예약후즉시처리Async(
                await oldDb.출고예정.AsNoTracking().ToListAsync(), [], DriverId, oldKey,
                currentStageKey: "포장", ledgerTemplateKey: CommunityLedgerTemplateKeys.WarehouseOutbound);
            Assert.Equal("Pending", (await oldDb.음식마트원장동기화Outbox.AsNoTracking().SingleAsync()).처리상태);
        }
        var oldServerSnapshot = 원장Dto(Assert.Single(store.SaveRequests));
        Assert.True(운송원장RdbSnapshotMarker.Matches(oldServerSnapshot, reference));

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await using var pickupDb = fixture.CreateContext();
        var picked = (await Service(pickupDb, fixture.Clock).픽업Async(DriverId, offer.OfferId, new 마트기사픽업요청
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            ExpectedLabelRevision = slip.Revision, Code = slip.QrCode
        })).Value;
        store.SaveRequests.Clear();
        await using (var followupDb = fixture.CreateContext())
        {
            await RealConsumer(followupDb, fixture, store).처리Async();
            Assert.NotEmpty(store.SaveRequests);
            Assert.All(store.SaveRequests, request =>
            {
                Assert.Equal(CommunityLedgerTemplateKeys.SsalddelMart, request.원장템플릿Key);
                Assert.Equal("배송중", request.현재단계Key);
                Assert.NotEqual(커뮤니티원장상태.완료, request.상태);
            });
            var order = await followupDb.마트주문.AsNoTracking().Include(item => item.상품목록).SingleAsync();
            Assert.Equal("배송중", order.상태);
            Assert.Equal("배송중", order.현재단계);
            Assert.All(order.상품목록, item => Assert.Equal("배송중", item.상태));
            Assert.Equal(2, (await followupDb.Set<마트배송시도>().AsNoTracking().SingleAsync()).Revision);
        }

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await using (var deliverDb = fixture.CreateContext())
        {
            var delivered = await Service(deliverDb, fixture.Clock).전달Async(DriverId, offer.OfferId,
                new 마트기사전달요청 { ClientRequestId = Guid.NewGuid(), ExpectedRevision = picked.Revision });
            Assert.True(delivered.IsSuccess);
        }
        store.SaveRequests.Clear();
        await using var retryDb = fixture.CreateContext();
        var old = await retryDb.음식마트원장동기화Outbox.SingleAsync(item => item.멱등키 == oldKey);
        old.UpdatedAtUtc = DateTime.UtcNow.AddHours(-1);
        await retryDb.SaveChangesAsync();
        var realOutbox = RealLedgerOutbox(retryDb, store);
        Assert.Equal(1, await realOutbox.대기항목처리Async());
        var reprocessed = Assert.Single(store.SaveRequests);
        Assert.Equal(CommunityLedgerTemplateKeys.SsalddelMart, reprocessed.원장템플릿Key);
        Assert.Equal("전달완료", reprocessed.현재단계Key);
        Assert.NotEqual(커뮤니티원장상태.완료, reprocessed.상태);
        Assert.Equal("Succeeded", old.처리상태);
        await RealConsumer(retryDb, fixture, store).처리Async();
        Assert.All(store.SaveRequests, request =>
        {
            Assert.Equal(CommunityLedgerTemplateKeys.SsalddelMart, request.원장템플릿Key);
            Assert.Equal("전달완료", request.현재단계Key);
        });
        var deliveredOrder = await retryDb.마트주문.AsNoTracking().Include(item => item.상품목록).SingleAsync();
        Assert.Equal("전달 완료", deliveredOrder.상태);
        Assert.Equal("전달완료", deliveredOrder.현재단계);
        Assert.All(deliveredOrder.상품목록, item => Assert.Equal("전달 완료", item.상태));
        Assert.Equal(3, (await retryDb.Set<마트배송시도>().AsNoTracking().SingleAsync()).Revision);
        Assert.All(await retryDb.입고상품.AsNoTracking().ToListAsync(), item => Assert.Equal(0, item.가용수량));
        Assert.Empty(await retryDb.음식주문.AsNoTracking().ToListAsync());
        Assert.Empty(await retryDb.음식배달시도.AsNoTracking().ToListAsync());
        // 이전 SQL 상태 사본의 Mongo 역투영은 새 출고 명령이 아니다. 완료 출고/재고를 되감지 않는다.
        var beforeOldSnapshot = await StateAsync(retryDb);
        await new 입출고원장업무투영Handler(retryDb, NullLogger<입출고원장업무투영Handler>.Instance)
            .동기화Async(oldServerSnapshot);
        Assert.Equal(beforeOldSnapshot, await StateAsync(retryDb));
        Assert.All(await retryDb.출고예정.AsNoTracking().ToListAsync(), item => Assert.Equal(출고상태.출고완료, item.상태));
        Assert.All(await retryDb.입고상품.AsNoTracking().ToListAsync(), item => Assert.Equal(0, item.가용수량));
        var countBeforeReplay = store.SaveRequests.Count;
        Assert.Equal(0, await realOutbox.대기항목처리Async());
        Assert.Equal(0, await RealConsumer(retryDb, fixture, store).처리Async());
        Assert.Equal(countBeforeReplay, store.SaveRequests.Count);
    }

    [Fact]
    public async Task 블록을명시편집해digest가달라진마트원장은_기존협업입출고투영을유지한다()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedAndDispatchAsync(fixture.Db, fixture.Clock, fixture.Boundaries);
        var plans = await fixture.Db.출고예정.ToListAsync();
        foreach (var plan in plans) plan.상태 = 출고상태.예정;
        await fixture.Db.SaveChangesAsync();
        var request = 음식마트원장Mongo동기화Builder.출고원장저장요청생성(plans, [], "포장",
            CommunityLedgerTemplateKeys.SsalddelMart);
        운송원장RdbSnapshotMarker.Attach(request, Order);
        var ledger = 원장Dto(request);
        Assert.True(운송원장RdbSnapshotMarker.Matches(ledger, Order));
        Assert.Single(ledger.블록목록, block => block.BlockId == "picking-packing").State = "명시 편집된 포장 단계";
        Assert.False(운송원장RdbSnapshotMarker.Matches(ledger, Order));

        await using var projectionDb = fixture.CreateContext();
        await new 입출고원장업무투영Handler(projectionDb, NullLogger<입출고원장업무투영Handler>.Instance).동기화Async(ledger);

        Assert.All(await projectionDb.출고예정.AsNoTracking().ToListAsync(), item => Assert.Equal(출고상태.준비중, item.상태));
        Assert.All(await projectionDb.입고상품.AsNoTracking().ToListAsync(), item => Assert.Equal(item.입고수량, item.가용수량));
        Assert.Empty(await projectionDb.Set<마트배송시도>().ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 상태나단계header만편집한마트원장도_변경없는서버사본으로오인하지않는다(bool changeState)
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedAndDispatchAsync(fixture.Db, fixture.Clock, fixture.Boundaries);
        var request = 음식마트원장Mongo동기화Builder.출고원장저장요청생성(
            await fixture.Db.출고예정.AsNoTracking().ToListAsync(), [], "포장", CommunityLedgerTemplateKeys.SsalddelMart);
        운송원장RdbSnapshotMarker.Attach(request, Order);
        request.확장속성 = new Dictionary<string, string>(request.확장속성)
        {
            ["MartDriverSnapshotState"] = request.상태!, ["MartDriverSnapshotStage"] = request.현재단계Key!
        };
        var ledger = 원장Dto(request);
        if (changeState) ledger.상태 = 커뮤니티원장상태.완료;
        else ledger.현재단계Key = "출고 완료";
        // 기존 digest는 블록과 외부참조만 포함한다. header 결속 검사가 편집을 별도로 판별해야 한다.
        Assert.True(운송원장RdbSnapshotMarker.Matches(ledger, Order));

        await using var projectionDb = fixture.CreateContext();
        await new 입출고원장업무투영Handler(projectionDb, NullLogger<입출고원장업무투영Handler>.Instance).동기화Async(ledger);

        Assert.All(await projectionDb.출고예정.AsNoTracking().ToListAsync(), item => Assert.Equal(출고상태.출고완료, item.상태));
        Assert.Empty(await projectionDb.Set<마트배송시도>().ToListAsync());
    }

    [Fact]
    public async Task 기존원장저장중최초기사수락이커밋되면_과거투영은Pending으로남고최신수락상태로재처리한다()
    {
        const string key = "r58-legacy-save-first-accept-race";
        await using var fixture = await DbFixture.CreateAsync();
        await SeedAndDispatchAsync(fixture.Db, fixture.Clock, fixture.Boundaries);
        // 실제 r57 연결은 상품을 포장 완료로 확정한다. 수락 전 정본을 잡아 옛 원장 소비가 되감지 않는지 비교한다.
        var beforeLegacy = await fixture.Db.마트주문.AsNoTracking().Include(order => order.상품목록).SingleAsync();
        var canonicalProductStates = beforeLegacy.상품목록.OrderBy(item => item.Id)
            .Select(item => (item.Id, item.상태, item.UpdatedAt)).ToArray();
        var store = new 기록원장저장소
        {
            BeforeWrite = async () =>
            {
                await using var acceptDb = fixture.CreateContext();
                var service = Service(acceptDb, fixture.Clock);
                var offer = Assert.Single((await service.조회Async(DriverId)).Value.Items);
                var result = await service.수락Async(DriverId, offer.OfferId, new 마트기사수락요청
                {
                    ClientRequestId = Guid.NewGuid(), ExpectedRecommendationRound = offer.RecommendationRound,
                    ExpectedQueueUpdatedAtUtc = offer.QueueUpdatedAtUtc
                });
                Assert.True(result.IsSuccess);
            }
        };
        await using (var legacyDb = fixture.CreateContext())
        {
            await RealLedgerOutbox(legacyDb, store).출고원장예약후즉시처리Async(
                await legacyDb.출고예정.AsNoTracking().ToListAsync(), [], SellerId, key,
                currentStageKey: "포장", ledgerTemplateKey: CommunityLedgerTemplateKeys.SsalddelMart);
            Assert.Equal("Pending", (await legacyDb.음식마트원장동기화Outbox.AsNoTracking().SingleAsync()).처리상태);
        }
        await using var retryDb = fixture.CreateContext();
        var afterAccept = await retryDb.마트주문.AsNoTracking().Include(order => order.상품목록).SingleAsync();
        Assert.Equal("기사 배정", afterAccept.상태);
        Assert.Equal("픽업대기", afterAccept.현재단계);
        Assert.Equal(canonicalProductStates, afterAccept.상품목록.OrderBy(item => item.Id)
            .Select(item => (item.Id, item.상태, item.UpdatedAt)).ToArray());
        Assert.Equal(1, (await retryDb.Set<마트배송시도>().AsNoTracking().SingleAsync()).Revision);
        Assert.All(await retryDb.입고상품.AsNoTracking().ToListAsync(), item => Assert.Equal(item.입고수량, item.가용수량));
        var beforeRetry = await StateAsync(retryDb);
        var old = await retryDb.음식마트원장동기화Outbox.SingleAsync();
        old.UpdatedAtUtc = DateTime.UtcNow.AddHours(-1);
        await retryDb.SaveChangesAsync();
        store.SaveRequests.Clear();

        Assert.Equal(1, await RealLedgerOutbox(retryDb, store).대기항목처리Async());

        Assert.Equal("Succeeded", old.처리상태);
        var request = Assert.Single(store.SaveRequests);
        Assert.Equal(CommunityLedgerTemplateKeys.SsalddelMart, request.원장템플릿Key);
        Assert.Equal("픽업대기", request.현재단계Key);
        Assert.NotEqual(커뮤니티원장상태.완료, request.상태);
        Assert.Equal(beforeRetry, await StateAsync(retryDb));
        var afterRetry = await retryDb.마트주문.AsNoTracking().Include(order => order.상품목록).SingleAsync();
        Assert.Equal(afterAccept.상태, afterRetry.상태);
        Assert.Equal(afterAccept.현재단계, afterRetry.현재단계);
        Assert.Equal(canonicalProductStates, afterRetry.상품목록.OrderBy(item => item.Id)
            .Select(item => (item.Id, item.상태, item.UpdatedAt)).ToArray());
        Assert.Empty(await retryDb.음식주문.ToListAsync());
        Assert.Empty(await retryDb.음식배달시도.ToListAsync());
    }

    private static 커뮤니티원장Dto 원장Dto(커뮤니티원장저장요청 request) => new()
    {
        원장Id = request.원장Id!, 원장템플릿Key = request.원장템플릿Key, 커뮤니티Id = request.커뮤니티Id,
        상태 = request.상태!, 현재단계Key = request.현재단계Key, 블록목록 = request.블록목록,
        외부참조 = request.외부참조, 확장속성 = request.확장속성
    };

    private static 음식마트원장동기화OutboxService RealLedgerOutbox(SsalddelContext db, 기록원장저장소 store)
        => new(db, new 음식마트원장Mongo동기화Service(store, db, NullLogger<음식마트원장Mongo동기화Service>.Instance),
            NullLogger<음식마트원장동기화OutboxService>.Instance);
    private static 마트기사후속OutboxService RealConsumer(SsalddelContext db, DbFixture fixture, 기록원장저장소 store)
        => new(db, fixture.Boundaries, RealLedgerOutbox(db, store), fixture.Boundaries, new NoOpTransition(), fixture.Clock);

    private static 마트기사후속OutboxService Consumer(SsalddelContext db, DbFixture fixture)
        => new(db, fixture.Boundaries, new 기록마트후속Adapter(db), fixture.Boundaries, new NoOpTransition(), fixture.Clock);
    private static 마트기사업무Service Service(SsalddelContext db, MutableClock clock, string actor = DriverId, string role = 역할명.기사)
        => new(db, new Actor(actor, role), new TestGate(), new 마트라스트마일원천Adapter(db), new ProtectedData(),
            new TestDispatchAvailability(), clock);
    private static 마트기사업무Controller Controller(SsalddelContext db, MutableClock clock)
        => new(Service(db, clock)) { ControllerContext = ControllerContext(DriverId, 역할명.기사) };
    private static ControllerContext ControllerContext(string actor, string role) => new() { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Role, role)], "test")) } };
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static async Task SeedAndDispatchAsync(SsalddelContext db, MutableClock clock, Boundaries boundaries,
        string orderReference = Order)
    {
        var warehouse = new 창고 { 소유자UserId = SellerId, 창고명 = "r58 격리 창고", 주소 = "서울 중랑구 표본 창고로 1",
            위도 = 37.58m, 경도 = 127.08m, CreatedAt = InitialTime, UpdatedAt = InitialTime };
        db.창고.Add(warehouse);
        db.주문자프로필.Add(new 주문자프로필 { UserId = "mart-r58-orderer", 표시명 = "표본 주문자",
            기본주소 = "서울 중랑구 표본 수령로 2", CreatedAt = InitialTime, UpdatedAt = InitialTime });
        await db.SaveChangesAsync();
        var order = new 마트주문 { 주문참조번호 = orderReference, 판매자UserId = SellerId, 주문자UserId = "mart-r58-orderer",
            상태 = "포장 진행", 현재단계 = "포장", CreatedAt = InitialTime, UpdatedAt = InitialTime };
        db.마트주문.Add(order);
        for (var index = 1; index <= 2; index++)
        {
            var inventory = new 입고상품 { 창고Id = warehouse.Id, 소유자UserId = SellerId, 판매자UserId = SellerId,
                상품명 = $"r58 표본 {index}", SKU = $"R58-{index}", 입고수량 = index, 가용수량 = index,
                예약수량 = 0, 상태 = "포장완료 - 일반포장", CreatedAt = InitialTime, UpdatedAt = InitialTime };
            db.입고상품.Add(inventory);
            await db.SaveChangesAsync();
            var plan = new 출고예정 { 주문참조번호 = orderReference, 판매자UserId = SellerId, 주문자UserId = "mart-r58-orderer",
                출고창고Id = warehouse.Id, 입고상품Id = inventory.Id, 상품명 = inventory.상품명, SKU = inventory.SKU,
                수량 = index, 상태 = 출고상태.준비중, CreatedAt = InitialTime, UpdatedAt = InitialTime };
            db.출고예정.Add(plan);
            await db.SaveChangesAsync();
            order.상품목록.Add(new 마트주문상품 { 출고예정Id = plan.Id, 상품명 = inventory.상품명, SKU = inventory.SKU,
                수량 = index, 상태 = "포장 진행", CreatedAt = InitialTime, UpdatedAt = InitialTime });
            foreach (var type in new[] { 피킹포장작업유형.피킹, 피킹포장작업유형.포장 })
                db.피킹포장작업.Add(new 피킹포장작업 { 작업Key = $"r58:{type}:{index}", 라인Key = $"r58-line:{index}",
                    작업유형 = type, 상태 = 피킹포장작업상태.완료, 출고예정Id = plan.Id, 입고상품Id = inventory.Id,
                    창고Id = warehouse.Id, 상품명 = inventory.상품명, SKU = inventory.SKU, 수량 = index,
                    주문참조번호 = orderReference, 작업자UserId = SellerId, 시작일시Utc = InitialTime.AddMinutes(1),
                    완료일시Utc = InitialTime.AddMinutes(5), CreatedAt = InitialTime, UpdatedAt = InitialTime });
            db.재고이력.Add(new 재고이력 { 입고상품Id = inventory.Id, 이력유형 = "포장", 원인유형 = "포장작업",
                변경수량 = 0, 변경후수량 = index, 처리UserId = SellerId, 처리일시 = InitialTime.AddMinutes(5) });
            await db.SaveChangesAsync();
        }
        // r57 연결 기반만 명시 시험용 Ready로 연다. 이 Gate를 운영 DI에 등록하지 않는다.
        var readiness = new ReadyPolicy();
        var queueService = new 운송의뢰배차대기Service(db, new 운송의뢰배차원천분류Service(), boundaries, boundaries, boundaries);
        var handoff = new 살뜰마트라스트마일배차인계Service(new 운영체제업무인계Coordinator(db, clock), queueService,
            new 살뜰마트라스트마일배차Policy(), readiness);
        var dispatch = new 알뜰살뜰마트배차대기Service(db, handoff, boundaries, boundaries,
            NullLogger<알뜰살뜰마트배차대기Service>.Instance, clock, readiness);
        Assert.True((await dispatch.주문포장완료후배차대기생성Async(orderReference, SellerId)).생성또는조회됨);
        var queue = await db.운송원장.SingleAsync();
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천중;
        queue.현재추천대상기사Id = DriverId;
        queue.추천시작시각 = clock.GetUtcNow().UtcDateTime;
        queue.추천만료시각 = clock.GetUtcNow().UtcDateTime.AddMinutes(5);
        queue.추천라운드 = 1;
        queue.생활배송수락준비완료 = true;
        queue.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
    }

    private static async Task<string> StateAsync(SsalddelContext db) => JsonSerializer.Serialize(new
    {
        attempts = await db.Set<마트배송시도>().AsNoTracking().OrderBy(row => row.시도StableId).ToListAsync(),
        labels = await db.Set<마트포장전표>().AsNoTracking().OrderBy(row => row.주문참조번호).ToListAsync(),
        receipts = await db.Set<마트기사요청영수증>().AsNoTracking().OrderBy(row => row.작업자UserId).ThenBy(row => row.클라이언트요청Id).ToListAsync(),
        queue = await db.운송원장.AsNoTracking().SingleAsync(),
        inventory = await db.입고상품.AsNoTracking().OrderBy(row => row.Id).ToListAsync(),
        outbounds = await db.출고예정.AsNoTracking().OrderBy(row => row.Id).ToListAsync(),
        movements = await db.재고이동.AsNoTracking().OrderBy(row => row.Id).ToListAsync(),
        events = await db.운송이벤트.AsNoTracking().OrderBy(row => row.Id).ToListAsync(),
        outbox = await db.Set<마트기사후속Outbox>().AsNoTracking().OrderBy(row => row.멱등키).ToListAsync()
    });
    private sealed record Actor(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class TestGate : I마트기사업무Gate { public bool Enabled => true; }
    private sealed class ReadyPolicy : IMartLastMileDispatchReadinessPolicy
    {
        public Task<MartLastMileDispatchReadiness> EvaluateAsync(string reference, CancellationToken cancellationToken)
            => Task.FromResult(new MartLastMileDispatchReadiness(true, "ExplicitTestConsumerReady", string.Empty));
    }
    private sealed class ProtectedData : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "test-protected:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value[15..]));
    }
    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now = new(InitialTime.AddMinutes(10));
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
    private sealed class DbFixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public MutableClock Clock { get; } = new();
        public Boundaries Boundaries { get; } = new();
        public static async Task<DbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new ProtectedData());
            await db.Database.EnsureCreatedAsync();
            return new(connection, db);
        }
        public SsalddelContext CreateContext() => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new ProtectedData());
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }

    // 외부 저장소는 호출 의도만 기록한다. 실제 네트워크/운영 원장 검증으로 해석하지 않는다.
    private sealed class Boundaries : I운송원장배달권연결Service, I음식배달권실행공간Store,
        I국내화물배달권실행공간Store, I운송원장Mongo동기화Service, I음식마트원장동기화OutboxService
    {
        public int MongoCalls { get; private set; }
        public bool FailMongoOnce { get; set; }
        public Task<운송원장배달권연결결과> 투영추적Async(운송원장 queue, CancellationToken cancellationToken = default)
        { var zone = new 배달권판정결과("r58-zone", "시험 권역", "ExplicitTest"); return Task.FromResult(new 운송원장배달권연결결과(zone, zone)); }
        public Task Upsert기사Async(string zone, string driver, IReadOnlyList<string> adjacent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Remove기사Async(string driver, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Upsert운송의뢰Async(string zone, string id, IReadOnlyList<string> adjacent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Remove운송의뢰Async(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        Task<음식배달권실행공간Snapshot?> I음식배달권실행공간Store.GetAsync(string zone, CancellationToken cancellationToken)
            => Task.FromResult<음식배달권실행공간Snapshot?>(null);
        Task<국내화물배달권실행공간Snapshot?> I국내화물배달권실행공간Store.GetAsync(string zone, CancellationToken cancellationToken)
            => Task.FromResult<국내화물배달권실행공간Snapshot?>(null);
        Task<IReadOnlyList<음식배달권실행공간Snapshot>> I음식배달권실행공간Store.SnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<음식배달권실행공간Snapshot>>([]);
        Task<IReadOnlyList<국내화물배달권실행공간Snapshot>> I국내화물배달권실행공간Store.SnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<국내화물배달권실행공간Snapshot>>([]);
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 request, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 queue, string updatedBy, CancellationToken cancellationToken = default)
        { MongoCalls++; if (FailMongoOnce) { FailMongoOnce = false; throw new InvalidOperationException("private-test-mongo-error"); }
            return Task.FromResult<커뮤니티원장Dto?>(new 커뮤니티원장Dto { 원장Id = "test-mart-transport:" + queue.의뢰Id }); }
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(운송원장Mongo동기화상태.Empty(id, "ExplicitTest"));
        public Task 음식주문예약후즉시처리Async(Ssalddel.Contracts.Food.음식주문응답 order, string updatedBy, string key, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("마트 후속은 음식 주문을 동기화할 수 없습니다.");
        public Task 출고원장예약후즉시처리Async(IReadOnlyList<출고예정> outbounds, IReadOnlyList<입고요청> inbounds,
            string updatedBy, string key, string? currentStageKey = null, string? ledgerTemplateKey = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    // 실제 Mongo 소비자가 아닌 기존 child Outbox 성공 계약의 기록 대역입니다.
    private sealed class 기록마트후속Adapter(SsalddelContext db) : I음식마트원장동기화OutboxService
    {
        public Task 음식주문예약후즉시처리Async(Ssalddel.Contracts.Food.음식주문응답 order, string updatedBy, string key,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("마트 후속은 음식 주문을 동기화할 수 없습니다.");

        public async Task 출고원장예약후즉시처리Async(IReadOnlyList<출고예정> outbounds, IReadOnlyList<입고요청> inbounds,
            string updatedBy, string key, string? currentStageKey = null, string? ledgerTemplateKey = null,
            CancellationToken cancellationToken = default)
        {
            if (await db.Set<음식마트원장동기화Outbox>().AnyAsync(item => item.멱등키 == key, cancellationToken)) return;
            db.Set<음식마트원장동기화Outbox>().Add(new 음식마트원장동기화Outbox
            {
                멱등키 = key, 동기화유형 = 음식마트원장동기화유형코드.창고출고, 원천Id = Order,
                변경자 = updatedBy, 처리상태 = "Succeeded", PayloadJson = "{}",
                CreatedAtUtc = InitialTime, UpdatedAtUtc = InitialTime
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    // 원장 요청과 실패 경계만 기록합니다. MongoDB 서버 저장·동시성 구현을 흉내 내지 않습니다.
    private sealed class 기록원장저장소 : I커뮤니티원장저장소
    {
        public List<커뮤니티원장저장요청> SaveRequests { get; } = [];
        public bool FailNextWrite { get; set; }
        public Func<Task>? BeforeWrite { get; set; }
        public async Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy,
            CancellationToken cancellationToken = default)
        {
            SaveRequests.Add(request);
            if (BeforeWrite is { } beforeWrite) { BeforeWrite = null; await beforeWrite(); }
            if (FailNextWrite) { FailNextWrite = false; throw new InvalidOperationException("ExplicitTestLedgerUnavailable"); }
            var ledger = 원장Dto(request);
            ledger.Revision = SaveRequests.Count;
            return ledger;
        }
        public Task<커뮤니티원장Dto?> 원장조회Async(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>([]);
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class NoOpTransition : I배차대기원장전환Service
    {
        private static Task<배차대기원장전환결과> Ignore(string request) => Task.FromResult(배차대기원장전환결과.전환안됨(request, "ExplicitTest", "외부 추천은 시험하지 않습니다."));
        public Task<배차대기원장전환결과> 계획배차에서추천으로전환Async(string id, CancellationToken ct = default) => Ignore(id);
        public Task<배차대기원장전환결과> 추천대기처리Async(string id, CancellationToken ct = default) => Ignore(id);
        public Task<배차대기원장전환결과> 추천시작Async(string id, string driver, int? timeoutSeconds = null, CancellationToken ct = default) => Ignore(id);
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driver, CancellationToken ct = default) => Ignore(id);
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driver, string? reason, CancellationToken ct = default) => Ignore(id);
        public Task<배차대기원장전환결과> 추천만료처리Async(string id, CancellationToken ct = default) => Ignore(id);
        public Task<배차대기원장전환결과> 공개배차로전환Async(string id, CancellationToken ct = default) => Ignore(id);
        public Task<배차대기원장전환결과> 실행주체확정결과동기화Async(string id, DispatchConfirmationBoundaryRequest confirmation, CancellationToken ct = default) => Ignore(id);
        public Task<배차대기원장전환결과> 배차수락취소처리Async(string id, string driver, string? reason = null, CancellationToken ct = default) => Ignore(id);
    }
}
