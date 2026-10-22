using FluentResults;
using System.Text.Json;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Food;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Food;
using Ssalddel.Domain.Community;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Food;
using Ssalddel.Services.Fulfillment;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Options;
using 살뜰.Services.Notifications;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Transport;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Food;

/// <summary>격리 SQLite의 실제 주문 Handler/Store/Coordinator/Adapter/조리 gate를 관통합니다.
/// 경로·주소·외부 게시 포트와 대부분의 큐 생성은 대역이며, 취소 뒤 재개 시험은 실제 큐 생성/배달권 투영을 사용합니다.
/// 실제 배차 엔진 후보 선정·외부 네트워크·PG·청구·지급·현장 운행은 검증하지 않습니다.</summary>
public sealed class FoodAutoFulfillmentWorkflowTests
{
    [Fact]
    public async Task 등록은_메뉴정본과_S판본동의를_같은트랜잭션에보존하고_수락은_공통Outbox만예약한다()
    {
        await using var f = await Fixture.Create();
        var order = await f.Register();
        Assert.Equal(AutoFulfillmentStates.AwaitingSource, order.AutoFulfillment!.StateCode);
        Assert.True((await f.Db.음식주문.SingleAsync()).모의처리처수행);
        Assert.Equal(9000m, order.총주문금액);
        Assert.Empty(f.Db.운송원장);
        Assert.Empty(f.Db.AutoFulfillmentOutbox);
        var consent = await f.Db.AutoFulfillmentConsents.SingleAsync();
        Assert.Equal(1200m, consent.FrozenRestaurantSupportKrw);
        Assert.Equal(1, consent.RestaurantSupportRevision);
        await f.Accept(order.주문번호);
        Assert.Single(await f.Db.AutoFulfillmentOutbox.ToListAsync());
        Assert.Empty(await f.Db.음식마트원장동기화Outbox.ToListAsync());
        Assert.Empty(f.Db.운송원장);
        await using var restarted = f.NewContext();
        Assert.Equal(음식주문상태코드.주문확인, (await restarted.음식주문.SingleAsync()).상태);
        Assert.Equal(order.주문번호, (await restarted.AutoFulfillmentOutbox.SingleAsync()).RequestId);
    }

    [Fact]
    public async Task 식당_S판본이바뀌면_주문등록도롤백되어_동의없는자동원천이남지않는다()
    {
        await using var f = await Fixture.Create();
        var request = f.Request(); request.AutoFulfillment!.ConfirmedRestaurantSupportRevision = 0;
        await Assert.ThrowsAsync<음식주문입력확인Exception>(() => f.Register(request));
        await using var restarted = f.NewContext();
        Assert.Empty(await restarted.음식주문.ToListAsync());
        Assert.Empty(await restarted.AutoFulfillmentConsents.ToListAsync());
        Assert.Empty(await restarted.운영배차활동사건.ToListAsync());
    }

    [Fact]
    public async Task 수락뒤_공통예약실패는_식당확인과함께롤백된다()
    {
        await using var f = await Fixture.Create();
        var order = await f.Register();
        var handler = new 음식점주문수락CommandHandler(f.Store, new Publisher(), f.Db,
            fulfillment: new FailingReserve(f.Coordinator));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(f.AcceptCommand(order.주문번호), default));
        await using var restarted = f.NewContext();
        Assert.Equal(음식주문상태코드.주문대기, (await restarted.음식주문.SingleAsync()).상태);
        Assert.Empty(await restarted.AutoFulfillmentOutbox.ToListAsync());
    }

    [Fact]
    public async Task 외부접수만으로조리를허용하지않고_배정_명시적조리_준비_픽업_전달이_별도영속된다()
    {
        await using var f = await Fixture.Create();
        var state = await f.Route();
        Assert.Equal(AutoFulfillmentStates.Accepted, state.StateCode);
        Assert.False(f.Store.GetOrder(state.SourceId)!.조리시작가능);
        Assert.Throws<InvalidOperationException>(() => f.Progress(state.SourceId, 음식점주문진행작업코드.조리시작));
        Assert.Empty(f.Db.운송원장); Assert.Empty(f.Db.음식배달시도);
        state = await f.Evidence(state, FulfillmentEvidenceKinds.DriverAssigned);
        Assert.True(f.Store.GetOrder(state.SourceId)!.조리시작가능);
        Assert.Equal(음식주문상태코드.조리중, f.Progress(state.SourceId, 음식점주문진행작업코드.조리시작).상태);
        var ready = f.Progress(state.SourceId, 음식점주문진행작업코드.픽업준비);
        Assert.Equal(음식주문상태코드.픽업대기, ready.상태);
        // 외부 모의 배정 증거는 내부 기사 사용자나 운송 원장을 추정하는 근거가 아닙니다.
        var progressNotices = await f.Db.Command알림Outbox.AsNoTracking()
            .Where(row => row.FeatureName == 업무진행알림Recorder.FeatureName).ToArrayAsync();
        Assert.Single(progressNotices, row => row.EventName == "food.ready");
        Assert.All(progressNotices, row =>
        {
            using var payload = JsonDocument.Parse(row.PayloadJson);
            Assert.Equal(ready.주문자UserId, payload.RootElement.GetProperty("targetUserId").GetString());
            Assert.Equal($"/workspace/orderer?selected={Uri.EscapeDataString(ready.주문번호)}",
                payload.RootElement.GetProperty("deepLink").GetString());
        });
        state = await f.Evidence(state, FulfillmentEvidenceKinds.PickedUp);
        Assert.Equal(AutoFulfillmentStates.InTransit, state.StateCode);
        state = await f.Evidence(state, FulfillmentEvidenceKinds.Delivered);
        Assert.Equal(AutoFulfillmentStates.Completed, state.StateCode);
        await using var restarted = f.NewContext();
        Assert.Equal(음식주문상태코드.전달완료, (await restarted.음식주문.SingleAsync()).상태);
        Assert.Empty(await restarted.운송원장.ToListAsync()); Assert.Empty(await restarted.음식배달시도.ToListAsync());
        Assert.Empty(await restarted.음식주문기사정산.ToListAsync());
        Assert.False(state.IsOperationalSettlementEligible);
    }

    [Fact]
    public async Task 배정해제뒤_옛배정판본은조리를열수없고_새배정증거만허용한다()
    {
        await using var f = await Fixture.Create();
        var accepted = await f.Route();
        var assigned = await f.Evidence(accepted, FulfillmentEvidenceKinds.DriverAssigned);
        var stale = new FulfillmentMockEvidenceRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            EvidenceKind = FulfillmentEvidenceKinds.DriverAssigned, ExternalDriverReference = "mock-driver" };
        var released = await f.Evidence(assigned, FulfillmentEvidenceKinds.AssignmentReleased);
        Assert.False(f.Store.GetOrder(released.SourceId)!.조리시작가능);
        Assert.Equal(음식주문상태코드.주문확인, f.Store.GetOrder(released.SourceId)!.상태);
        Assert.True((await f.Coordinator.모의증거기록Async(released.AttemptId!, stale)).IsFailed);
        Assert.Throws<InvalidOperationException>(() => f.Progress(released.SourceId, 음식점주문진행작업코드.조리시작));
        await f.Evidence(released, FulfillmentEvidenceKinds.DriverAssigned);
        Assert.True(f.Store.GetOrder(released.SourceId)!.조리시작가능);
    }

    [Fact]
    public async Task 조리뒤_업체거절은이력을보존하고_자동으로다른업체를접수하지않는다()
    {
        await using var f = await Fixture.Create();
        var state = await f.Route(providers: ["mock-quick", "mock-local"]);
        state = await f.Evidence(state, FulfillmentEvidenceKinds.DriverAssigned);
        f.Progress(state.SourceId, 음식점주문진행작업코드.조리시작);
        var next = await f.Evidence(state, FulfillmentEvidenceKinds.Rejected, cost: 0);
        Assert.Equal(AutoFulfillmentStates.NeedsReview, next.StateCode);
        Assert.Equal("CookingAlreadyStarted", next.ReviewCode);
        Assert.False(next.CanRenewConsent);
        Assert.Equal("CookingAlreadyStarted", next.ConsentRenewalLockedReasonCode);
        var renewal = f.Request().AutoFulfillment!;
        renewal.ExpectedRevision = next.ConsentRevision;
        var key = new FulfillmentSourceKey(FulfillmentSourceKinds.FoodOrder, state.SourceId);
        var denied = await f.Coordinator.사전동의저장Async(key, "owner", renewal);
        Assert.Equal(next.ConsentRenewalLockedReasonCode, denied.Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal(next.ConsentRevision, (await f.Db.AutoFulfillmentConsents.SingleAsync()).Revision);
        var revoked = await f.Coordinator.사전동의저장Async(key, "owner", new()
            { ClientRequestId = Guid.NewGuid(), ExpectedRevision = next.ConsentRevision, Revoked = true });
        Assert.True(revoked.IsSuccess);
        Assert.False(revoked.Value.CanRenewConsent);
        Assert.True((await f.Db.AutoFulfillmentConsents.SingleAsync()).Revoked);
        Assert.Equal(음식주문상태코드.조리중, f.Store.GetOrder(state.SourceId)!.상태);
        Assert.Single(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Single(await f.Db.FulfillmentMockJobs.ToListAsync());
    }

    [Fact]
    public async Task 모의업체는시간이흘러도자동완료되지않고_새Context에서도접수로남는다()
    {
        await using var f = await Fixture.Create();
        var state = await f.Route();
        f.Clock.Advance(TimeSpan.FromHours(8));
        await using var restarted = f.NewContext();
        var other = f.MakeCoordinator(restarted, new EfSsalddelFoodOrderStore(restarted));
        var after = await other.재확인Async(new(FulfillmentSourceKinds.FoodOrder, state.SourceId),
            new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = state.Revision });
        Assert.True(after.IsSuccess, string.Join(";", after.Errors));
        Assert.Equal(AutoFulfillmentStates.Accepted, after.Value.StateCode);
        Assert.Equal(state.AttemptId, after.Value.AttemptId);
        Assert.Single(await restarted.FulfillmentMockJobs.ToListAsync());
    }

    [Fact]
    public async Task 확인된취소비용과_후속견적누적에는_주문전체S가한번만적용된다()
    {
        await using var f = await Fixture.Create();
        var state = await f.Route(providers: ["mock-quick", "mock-local"]);
        var first = state.TotalCostKrw!.Value;
        var next = await f.Evidence(state, FulfillmentEvidenceKinds.Rejected, cost: 800m);
        Assert.Equal("mock-local", next.ProviderId);
        Assert.Equal(1200m, next.FrozenRestaurantSupportKrw);
        Assert.Equal(1200m, next.RestaurantContributionKrw);
        Assert.Equal(next.TotalCostKrw!.Value - 1200m, next.UserChargeKrw);
        Assert.True(next.TotalCostKrw > first);
        Assert.Equal(2, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
    }

    [Fact]
    public async Task 알수없는경로나자동차기본값은음식자동접수후보로쓰지않는다()
    {
        await using var f = await Fixture.Create(); f.RoutePort.Available = false;
        var state = await f.Route();
        Assert.Equal(AutoFulfillmentStates.NeedsReview, state.StateCode);
        Assert.Empty(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Equal("traavoidcaronly", f.RoutePort.LastOption);
        Assert.Empty(f.Db.운송원장);
    }

    [Fact]
    public async Task 내부접수는기존음식큐로연결하지만_실제기사수락전에는조리를열지않는다()
    {
        await using var f = await Fixture.Create();
        var state = await f.Route(providers: ["internal"]);
        Assert.Equal("internal", state.ProviderId);
        var queue = await f.Db.운송원장.SingleAsync();
        Assert.Equal(상태값.배차업무유형.음식배달, queue.배차업무유형);
        Assert.False(f.Store.GetOrder(state.SourceId)!.조리시작가능);
        queue.배차큐단계 = 상태값.배차큐단계.배차추천; queue.배차노출상태 = 상태값.배차노출상태.추천중;
        queue.현재추천대상기사Id = "driver"; queue.추천만료시각 = DateTime.UtcNow.AddMinutes(5); queue.추천라운드 = 1;
        await f.Db.SaveChangesAsync();
        var driver = new 음식배달기사업무Service(f.Db, new InMemoryDriverLocationStore(), f.RoutePort, null!,
            new InMemory음식배달권실행공간Store(), f.Store, null!, null!, null!, null!, new Publisher(),
            new User("driver", "Driver"), NullLogger<음식배달기사업무Service>.Instance, new TestDispatchAvailability(), f.Coordinator);
        var result = await driver.수락Async("driver", state.SourceId);
        Assert.True(result.IsSuccess, string.Join(";", result.Errors));
        Assert.Single(await f.Db.음식배달시도.ToListAsync());
        Assert.True(f.Store.GetOrder(state.SourceId)!.조리시작가능);
        Assert.Empty(await f.Db.기사월정산.ToListAsync());
    }

    [Fact]
    public async Task 외부현재선정은이전내부큐결속재생을차단한다()
    {
        await using var f = await Fixture.Create();
        var state = await f.Route();
        Assert.Throws<InvalidOperationException>(() => f.Store.배차대기반영(state.SourceId, 777, DateTime.UtcNow));
        Assert.Null(f.Store.GetOrder(state.SourceId)!.배차대기Id);
        Assert.Empty(f.Db.운송원장);
    }

    [Theory]
    [InlineData(AutoFulfillmentStates.CancelPending)]
    [InlineData(AutoFulfillmentStates.NeedsReview)]
    public async Task 현재수행이보류되면_이전Accepted시도와추천으로기사수락을할수없다(string currentState)
    {
        await using var f = await Fixture.Create();
        var state = await f.Route(providers: ["internal"]);
        var current = await f.Db.NeighborhoodFulfillments.SingleAsync(); current.StateCode = currentState;
        var queue = await f.Db.운송원장.SingleAsync();
        queue.배차큐단계 = 상태값.배차큐단계.배차추천; queue.배차노출상태 = 상태값.배차노출상태.추천중;
        queue.현재추천대상기사Id = "driver"; queue.추천만료시각 = DateTime.UtcNow.AddMinutes(5); queue.추천라운드 = 1;
        await f.Db.SaveChangesAsync();
        Assert.Equal(AutoFulfillmentStates.Accepted, (await f.Db.NeighborhoodFulfillmentAttempts.SingleAsync()).StateCode);
        Assert.False(음식수행권위Policy.내부허용(f.Db, await f.Db.음식주문.Include(x => x.상태이력).SingleAsync()));
        Assert.False(await f.Coordinator.내부수행허용Async(new(FulfillmentSourceKinds.FoodOrder, state.SourceId)));
        var driver = new 음식배달기사업무Service(f.Db, new InMemoryDriverLocationStore(), f.RoutePort, null!,
            new InMemory음식배달권실행공간Store(), f.Store, null!, null!, null!, null!, new Publisher(),
            new User("driver", "Driver"), NullLogger<음식배달기사업무Service>.Instance, new TestDispatchAvailability(), f.Coordinator);
        Assert.True((await driver.수락Async("driver", state.SourceId)).IsFailed);
        Assert.Null(queue.확정기사Id); Assert.Empty(await f.Db.음식배달시도.ToListAsync());
    }

    [Fact]
    public async Task 확인된미배정내부취소뒤_새동의는_실제큐서비스의같은큐를안전하게재개한다()
    {
        await using var f = await Fixture.Create(useRealQueue: true);
        var state = await f.Route(providers: ["internal"]);
        var queueId = (await f.Db.운송원장.SingleAsync()).Id;
        var cancelled = await f.Coordinator.취소Async(new(FulfillmentSourceKinds.FoodOrder, state.SourceId),
            new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = state.Revision });
        Assert.True(cancelled.IsSuccess, string.Join(";", cancelled.Errors));
        Assert.Equal(상태값.배차상태.취소, (await f.Db.운송원장.SingleAsync()).상태);
        Assert.Null(f.Store.GetOrder(state.SourceId)!.배차대기Id);
        var renewed = f.Request(["internal"]).AutoFulfillment!; renewed.ExpectedRevision = cancelled.Value.ConsentRevision;
        var consent = await f.Coordinator.사전동의저장Async(new(FulfillmentSourceKinds.FoodOrder, state.SourceId), "owner", renewed);
        Assert.True(consent.IsSuccess, string.Join(";", consent.Errors));
        var routed = await f.Coordinator.선정실행Async(new(FulfillmentSourceKinds.FoodOrder, state.SourceId), Guid.NewGuid());
        Assert.True(routed.IsSuccess, string.Join(";", routed.Errors));
        Assert.Equal(AutoFulfillmentStates.Accepted, routed.Value.StateCode); Assert.NotEqual(state.AttemptId, routed.Value.AttemptId);
        var queue = await f.Db.운송원장.SingleAsync();
        Assert.Equal(queueId, queue.Id); Assert.Equal(상태값.배차대기상태.대기, queue.상태);
        Assert.Equal(상태값.배차큐단계.계획배차, queue.배차큐단계); Assert.Null(queue.확정기사Id);
        Assert.Equal(queueId, f.Store.GetOrder(state.SourceId)!.배차대기Id);
        Assert.Equal(2, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
        Assert.Equal(1200m, routed.Value.FrozenRestaurantSupportKrw);
        Assert.Empty(await f.Db.음식배달시도.ToListAsync()); Assert.Empty(await f.Db.기사월정산.ToListAsync());
    }

    [Fact]
    public async Task 자동기능OFF는_새공통표없이_음식원천기록만으로_일시중지상태를표시하고_legacy는null이다()
    {
        await using var f = await Fixture.Create();
        var state = await f.Route(); state = await f.Evidence(state, FulfillmentEvidenceKinds.DriverAssigned);
        var legacyRequest = f.Request(); legacyRequest.AutoFulfillment = null;
        var legacy = await f.Register(legacyRequest);
        await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE neighborhood_fulfillment_attempts");
        await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE neighborhood_fulfillment");
        await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE fulfillment_routing_consents");
        var store = new EfSsalddelFoodOrderStore(f.Db, Options.Create(new FulfillmentRoutingOptions { Enabled = false }));
        var paused = store.GetOrder(state.SourceId)!;
        Assert.True(paused.AutoFulfillment!.ExecutionPaused); Assert.Null(paused.AutoFulfillment.MaxUserChargeKrw);
        Assert.Null(paused.AutoFulfillment.TotalCostKrw); Assert.False(paused.AutoFulfillment.CanCancel);
        Assert.False(paused.AutoFulfillment.CanRenewConsent); Assert.False(paused.조리시작가능);
        Assert.Null(store.GetOrder(legacy.주문번호)!.AutoFulfillment);
        var coordinator = f.MakeCoordinator(f.Db, store, enabled: false);
        var detail = await new 주문자음식주문조회UseCase(f.Db, coordinator).상세Async(state.SourceId, "owner", default);
        Assert.True(detail.IsSuccess); Assert.True(detail.Value.AutoFulfillment!.ExecutionPaused);
        Assert.Equal(음식주문상태코드.기사배정, paused.상태);
        var mode = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
        var api = await new 음식자동수행UseCase(f.Db, coordinator, mode, f.Clock).상태Async(state.SourceId, "owner", default);
        Assert.True(api.IsSuccess); Assert.True(api.Value.ExecutionPaused);
        Assert.Null((await new 주문자음식주문조회UseCase(f.Db, coordinator).상세Async(legacy.주문번호, "owner", default)).Value.AutoFulfillment);
    }

    [Fact]
    public async Task 자동기능OFF는_식당수락만저장하고_공통예약을누락하지않는다()
    {
        await using var f = await Fixture.Create();
        var order = await f.Register();
        var store = new EfSsalddelFoodOrderStore(f.Db, Options.Create(new FulfillmentRoutingOptions { Enabled = false }));
        var coordinator = f.MakeCoordinator(f.Db, store, enabled: false);
        var handler = new 음식점주문수락CommandHandler(store, new Publisher(), f.Db, fulfillment: coordinator);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(f.AcceptCommand(order.주문번호), default));
        await using var restarted = f.NewContext();
        Assert.Equal(음식주문상태코드.주문대기, (await restarted.음식주문.SingleAsync()).상태);
        Assert.Empty(await restarted.AutoFulfillmentOutbox.ToListAsync()); Assert.Empty(await restarted.운송원장.ToListAsync());
    }

    [Theory]
    [InlineData("CustomerPays")]
    [InlineData("FixedContribution")]
    [InlineData("PercentageShare")]
    [InlineData("RestaurantPays")]
    public async Task 음식의네비용정책은_실제원주문과현재견적에서_각부담액을계산한다(string mode)
    {
        await using var f = await Fixture.Create();
        var policy = Policy(mode);
        await f.SetPolicy(policy);
        var request = await f.ConfirmedPolicyRequest();
        var state = await f.Route(request: request);
        Assert.Equal(AutoFulfillmentStates.Accepted, state.StateCode);
        var total = state.TotalCostKrw!.Value;
        var expectedRestaurant = mode switch
        {
            "CustomerPays" => 0m,
            "FixedContribution" => Math.Min(1200m, total),
            "PercentageShare" => decimal.Floor(total * 33.33m / 100m),
            "RestaurantPays" => total,
            _ => throw new InvalidOperationException()
        };
        Assert.Equal(expectedRestaurant, state.RestaurantContributionKrw);
        Assert.Equal(total - expectedRestaurant, state.UserChargeKrw);
        Assert.Equal(mode, state.CostSharingPolicy!.Mode);
        Assert.Equal(expectedRestaurant, f.Store.GetOrder(state.SourceId)!.AutoFulfillment!.RestaurantContributionKrw);
        await using var restarted = f.NewContext();
        var restored = await f.MakeCoordinator(restarted, new EfSsalddelFoodOrderStore(restarted)).상태Async(new(FulfillmentSourceKinds.FoodOrder, state.SourceId));
        Assert.Equal(mode, restored!.CostSharingPolicy!.Mode);
        Assert.Equal(state.RestaurantContributionKrw, restored.RestaurantContributionKrw);
        Assert.Empty(await restarted.음식주문기사정산.ToListAsync());
    }

    [Fact]
    public async Task 식당전액상한을초과하면_고객상한이넉넉해도_초과액을전가하거나접수하지않는다()
    {
        await using var f = await Fixture.Create();
        await f.SetPolicy(new() { Mode = "RestaurantPays", MaximumRestaurantChargeKrw = 1 });
        var request = await f.ConfirmedPolicyRequest(); request.AutoFulfillment!.MaxUserChargeKrw = 100000;
        var state = await f.Route(request: request);
        Assert.Equal(AutoFulfillmentStates.NeedsReview, state.StateCode);
        Assert.Null(state.AttemptId); Assert.Equal("RestaurantPays", state.CostSharingPolicy!.Mode);
        Assert.Empty(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Empty(await f.Db.FulfillmentMockJobs.ToListAsync()); Assert.Empty(await f.Db.운송원장.ToListAsync());
    }

    [Fact]
    public async Task 주문접수전_식당전체정책이나판본이바뀌면_재확인없이자동원천을만들지않는다()
    {
        await using var f = await Fixture.Create();
        await f.SetPolicy(Policy("PercentageShare"));
        var stale = await f.ConfirmedPolicyRequest();
        await f.SetPolicy(Policy("CustomerPays"));
        var error = await Assert.ThrowsAsync<음식주문입력확인Exception>(() => f.Register(stale));
        Assert.Equal("RestaurantSupportChanged", error.ErrorCode);
        await using var restarted = f.NewContext();
        Assert.Empty(await restarted.음식주문.ToListAsync());
        Assert.Empty(await restarted.AutoFulfillmentConsents.ToListAsync());
        Assert.Empty(await restarted.운영배차활동사건.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 비정액정책은_구형정액확인이나_자동접수미동의로우회할수없다(bool missingAutomaticAgreement)
    {
        await using var f = await Fixture.Create();
        await f.SetPolicy(Policy("PercentageShare"));
        var request = await f.ConfirmedPolicyRequest();
        if (missingAutomaticAgreement) request.AutoFulfillment!.AutomaticSubmissionAgreed = false;
        else request.AutoFulfillment!.ConfirmedCostSharingPolicy = null;
        var error = await Assert.ThrowsAsync<음식주문입력확인Exception>(() => f.Register(request));
        Assert.Equal(missingAutomaticAgreement ? "FulfillmentConsentRequired" : "CostSharingPolicyConfirmationRequired", error.ErrorCode);
        await using var restarted = f.NewContext();
        Assert.Empty(await restarted.음식주문.ToListAsync()); Assert.Empty(await restarted.AutoFulfillmentConsents.ToListAsync());
        Assert.Empty(await restarted.FulfillmentMockJobs.ToListAsync());
    }

    [Fact]
    public async Task 최초주문정책은_식당설정변경과_확인된거절후재선정_동의갱신에도누적으로동결된다()
    {
        await using var f = await Fixture.Create();
        await f.SetPolicy(Policy("PercentageShare"));
        var request = await f.ConfirmedPolicyRequest(["mock-quick", "mock-local"]);
        var order = await f.Register(request);
        var frozenRevision = request.AutoFulfillment!.ConfirmedRestaurantSupportRevision;
        await f.SetPolicy(Policy("CustomerPays"));
        await f.Accept(order.주문번호); Assert.Equal(1, await f.Coordinator.대기처리Async());
        var state = (await f.Coordinator.상태Async(new(FulfillmentSourceKinds.FoodOrder, order.주문번호)))!;
        state = await f.Evidence(state, FulfillmentEvidenceKinds.Rejected, cost: 801m);
        Assert.Equal("mock-local", state.ProviderId); Assert.Equal(frozenRevision, state.RestaurantSupportRevision);
        Assert.Equal("PercentageShare", state.CostSharingPolicy!.Mode);
        Assert.Equal(decimal.Floor(state.TotalCostKrw!.Value * 33.33m / 100m), state.RestaurantContributionKrw);
        state = await f.Evidence(state, FulfillmentEvidenceKinds.Rejected, cost: 999m);
        Assert.Equal(AutoFulfillmentStates.NeedsReview, state.StateCode);
        var changedRenewal = await f.ConfirmedPolicyRequest(["mock-quick"]);
        changedRenewal.AutoFulfillment!.ExpectedRevision = state.ConsentRevision;
        var rejectedRenewal = await f.Coordinator.사전동의저장Async(new(FulfillmentSourceKinds.FoodOrder, state.SourceId), "owner", changedRenewal.AutoFulfillment);
        Assert.True(rejectedRenewal.IsFailed);
        Assert.Equal("FrozenCostSharingPolicyChanged", rejectedRenewal.Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal(state.ConsentRevision, (await f.Db.AutoFulfillmentConsents.SingleAsync()).Revision);
        var renewal = await f.ConfirmedPolicyRequest(["mock-quick"]);
        renewal.AutoFulfillment!.ExpectedRevision = state.ConsentRevision;
        renewal.AutoFulfillment.ConfirmedCostSharingPolicy = request.AutoFulfillment.ConfirmedCostSharingPolicy;
        renewal.AutoFulfillment.ConfirmedRestaurantSupportRevision = frozenRevision;
        renewal.AutoFulfillment.ConfirmedRestaurantSupportKrw = request.AutoFulfillment.ConfirmedRestaurantSupportKrw;
        var renewed = await f.Coordinator.사전동의저장Async(new(FulfillmentSourceKinds.FoodOrder, state.SourceId), "owner", renewal.AutoFulfillment);
        Assert.True(renewed.IsSuccess, string.Join(";", renewed.Errors));
        var routed = await f.Coordinator.선정실행Async(new(FulfillmentSourceKinds.FoodOrder, state.SourceId), Guid.NewGuid());
        Assert.True(routed.IsSuccess, string.Join(";", routed.Errors));
        Assert.Equal(AutoFulfillmentStates.Accepted, routed.Value.StateCode);
        Assert.Equal(frozenRevision, routed.Value.RestaurantSupportRevision);
        Assert.Equal("PercentageShare", routed.Value.CostSharingPolicy!.Mode);
        var total = routed.Value.TotalCostKrw!.Value;
        Assert.True(total >= 1800m);
        Assert.Equal(decimal.Floor(total * 33.33m / 100m), routed.Value.RestaurantContributionKrw);
        Assert.Equal(total - routed.Value.RestaurantContributionKrw, routed.Value.UserChargeKrw);
        Assert.Equal(3, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
    }

    private static DeliveryCostSharingPolicy Policy(string mode) => new()
    { Mode = mode, FixedAmountKrw = mode == "FixedContribution" ? 1200 : null,
        RestaurantSharePercent = mode == "PercentageShare" ? 33.33m : null };

    private sealed record User(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Publisher : IPublisher
    {
        public Task Publish(object value, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<T>(T value, CancellationToken ct = default) where T : INotification => Task.CompletedTask;
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly bool useRealQueue;
        public SsalddelContext Db { get; }
        public EfSsalddelFoodOrderStore Store { get; }
        public Clock Clock { get; } = new();
        public Route RoutePort { get; } = new();
        public 자동수행Coordinator Coordinator { get; }
        private Fixture(SqliteConnection connection, SsalddelContext db, bool useRealQueue)
        { this.connection = connection; this.useRealQueue = useRealQueue; Db = db; Store = new(db); Coordinator = MakeCoordinator(db, Store); }
        public SsalddelContext NewContext() => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        public 자동수행Coordinator MakeCoordinator(SsalddelContext db, EfSsalddelFoodOrderStore store, bool enabled = true)
        {
            var mode = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
            var pricing = new 음식배달기사제안요금Service(db, RoutePort, new Weather(), Clock, mode);
            I운송의뢰배차대기Service queue = useRealQueue ? new 운송의뢰배차대기Service(db, new 운송의뢰배차원천분류Service(),
                new 운송원장배달권연결Service(new 원장배달권투영Service(db)), new InMemory음식배달권실행공간Store(), new InMemory국내화물배달권실행공간Store()) : new PersistedQueue(db);
            var source = new 음식수행원천Adapter(db, store, new 음식배차원천Builder(db, queue, new Geo()), pricing, Clock);
            return new(db, [source], [new 자체자동수행Provider(Clock),
                new 모의자동수행Provider(db, Clock, "mock-quick", "모의 퀵", .8m, 20, 30),
                new 모의자동수행Provider(db, Clock, "mock-local", "모의 지역", 1.1m, 30, 30)],
                Options.Create(new FulfillmentRoutingOptions { Enabled = enabled }), mode, Clock);
        }
        public static async Task<Fixture> Create(bool useRealQueue = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            db.음식점공개프로필.Add(new() { Id = 41, 상호명 = "테스트 식당", 공개주소 = "서울 면목로 2", 위도 = 37.58m,
                경도 = 127.09m, 공개여부 = true, 주문가능여부 = true, 최소주문금액 = 0, 예상조리분 = 10 });
            db.음식점메뉴.Add(new() { Id = 10, 음식점공개프로필Id = 41, 메뉴명 = "비빔밥", 판매가 = 9000, 공개여부 = true });
            db.RestaurantFulfillmentSupports.Add(new() { RestaurantId = 41, Revision = 1, FixedContributionKrw = 1200, Enabled = true });
            await db.SaveChangesAsync(); return new(connection, db, useRealQueue);
        }
        public 음식주문등록요청 Request(string[]? providers = null) => new()
        {
            클라이언트요청Id = Guid.NewGuid(), 음식점Id = 41, 주문자UserId = "owner",
            수령인정보 = new() { 수령인명 = "시험 수령인", 연락처 = "010-0000-0000", 주소 = "서울 면목로 1" },
            상품목록 = [new() { 메뉴Id = 10, 상품명 = "비빔밥", 단가 = 9000, 수량 = 1 }],
            AutoFulfillment = new() { ClientRequestId = Guid.NewGuid(), MaxUserChargeKrw = 20000,
                DesiredCompletionUtc = Clock.GetUtcNow().UtcDateTime.AddHours(2), AllowedProviderIds = providers ?? ["mock-quick"],
                ProviderInformationSharingAgreed = true, AutomaticSubmissionAgreed = true,
                ConfirmedRestaurantSupportKrw = 1200, ConfirmedRestaurantSupportRevision = 1 }
        };
        public Task<음식주문응답> Register(음식주문등록요청? request = null)
            => new 음식주문등록CommandHandler(Store, new 음식주문메뉴검증Service(Db), new Publisher(), Db, fulfillment: Coordinator)
                .Handle(new(request ?? Request()), default);
        public 음식점주문수락Command AcceptCommand(string orderNo) => new(orderNo,
            new() { 클라이언트요청Id = Guid.NewGuid(), 음식점명 = "테스트 식당", 음식점주소 = "서울 면목로 2",
                음식점위도 = 37.58m, 음식점경도 = 127.09m, 조리예상분 = 10 }, "restaurant");
        public Task<음식주문응답?> Accept(string orderNo) => new 음식점주문수락CommandHandler(Store, new Publisher(), Db, fulfillment: Coordinator)
            .Handle(AcceptCommand(orderNo), default);
        public async Task<AutoFulfillmentSnapshot> Route(string[]? providers = null, 음식주문등록요청? request = null)
        {
            var order = await Register(request ?? Request(providers)); await Accept(order.주문번호);
            Assert.Equal(1, await Coordinator.대기처리Async());
            return (await Coordinator.상태Async(new(FulfillmentSourceKinds.FoodOrder, order.주문번호)))!;
        }
        private 음식자동수행UseCase Settings() => new(Db, Coordinator,
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })), Clock);
        public async Task SetPolicy(DeliveryCostSharingPolicy policy)
        {
            var current = await Settings().식당설정조회Async(41, default);
            Assert.True(current.IsSuccess);
            var result = await Settings().식당설정저장Async(41, "restaurant", new()
            { ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Value.Revision, CostSharingPolicy = policy,
                FixedContributionKrw = policy.FixedAmountKrw ?? 0, Enabled = true, Agreed = true }, default);
            Assert.True(result.IsSuccess, string.Join(";", result.Errors));
        }
        public async Task<음식주문등록요청> ConfirmedPolicyRequest(string[]? providers = null)
        {
            var quote = await Settings().조건조회Async(41, default);
            Assert.True(quote.IsSuccess); Assert.True(quote.Value.Enabled);
            var request = Request(providers);
            request.AutoFulfillment!.ConfirmedRestaurantSupportRevision = quote.Value.RestaurantSupportRevision;
            request.AutoFulfillment.ConfirmedRestaurantSupportKrw = quote.Value.RestaurantSupportKrw;
            request.AutoFulfillment.ConfirmedCostSharingPolicy = quote.Value.CostSharingPolicy;
            return request;
        }
        public 음식주문응답 Progress(string id, string action) => Store.음식점진행변경(id,
            new() { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = Store.GetOrder(id)!.Revision, 작업 = action }, "restaurant")!.주문;
        public async Task<AutoFulfillmentSnapshot> Evidence(AutoFulfillmentSnapshot state, string kind, decimal? cost = null)
        {
            var result = await Coordinator.모의증거기록Async(state.AttemptId!, new() { ClientRequestId = Guid.NewGuid(),
                ExpectedRevision = state.Revision, EvidenceKind = kind, ExternalDriverReference = "mock-driver", ConfirmedIncurredCostKrw = cost });
            Assert.True(result.IsSuccess, string.Join(";", result.Errors)); return result.Value;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class PersistedQueue(SsalddelContext db) : I운송의뢰배차대기Service
    {
        public async Task<운송원장> 생성또는조회Async(출고예정운송대상 target, 운송의뢰배차대기생성옵션? options = null, CancellationToken ct = default)
        {
            var existing = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == options!.의뢰Id, ct);
            if (existing is not null) return existing;
            var queue = new 운송원장 { 의뢰Id = options!.의뢰Id!, 운송번호 = options.의뢰Id!, 화주Id = options.화주Id!,
                원본의뢰Id = options.원본의뢰Id!, 원본의뢰유형 = options.원본의뢰유형!, 배차업무유형 = options.배차업무유형!.Value,
                상태 = 상태값.배차대기상태.대기, 픽업_위도 = target.상차위도, 픽업_경도 = target.상차경도,
                하차_위도 = target.하차위도, 하차_경도 = target.하차경도 };
            db.운송원장.Add(queue); return queue;
        }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Geo : IKakao좌표변환Service
    {
        public Task<(double 위도, double 경도)?> 도로명주소좌표변환Async(string address, CancellationToken ct = default) => Task.FromResult<(double, double)?>(null);
        public Task<Kakao주소정보?> 주소정보조회Async(string address, CancellationToken ct = default) => Task.FromResult<Kakao주소정보?>(new(address, address, "서울", "중랑구", "면목동", 37.57m, 127.08m));
        public Task<Kakao지역정보?> 좌표지역정보조회Async(decimal lat, decimal lng, CancellationToken ct = default) => Task.FromResult<Kakao지역정보?>(null);
    }
    private sealed class Weather : I픽업지기상관측Client
    {
        public Task<픽업지기상관측결과> 조회Async(decimal? lat, decimal? lng, CancellationToken ct = default) => Task.FromResult(new 픽업지기상관측결과(
            false, false, 픽업지기상자료상태Code.MissingServiceKey, null, null, null, 픽업지기상관측결과.공식자료출처, null));
    }
    private sealed class Route : I배차추천경로Service
    {
        public bool Available { get; set; } = true; public string? LastOption { get; private set; }
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? goal) => throw new InvalidOperationException("DefaultCarMustNotBeUsed");
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? goal, string option, CancellationToken ct)
        { LastOption = option; return Task.FromResult<배차경로예상결과?>(new(2m, TimeSpan.FromMinutes(7), 0, Available ? "Directions5" : "Approximate", Available) { RouteOption = option }); }
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 from, 배차경로좌표 to) => 2;
    }
    private sealed class FailingReserve(I자동수행Coordinator inner) : I자동수행Coordinator
    {
        public bool Enabled => true;
        public Task 예약Async(FulfillmentSourceKey key, Guid id, CancellationToken ct = default) => throw new InvalidOperationException("routing-outbox-failed");
        public Task<Result<AutoFulfillmentSnapshot>> 사전동의저장Async(FulfillmentSourceKey key, string owner, AutoFulfillmentConsent request, CancellationToken ct = default) => inner.사전동의저장Async(key, owner, request, ct);
        public Task<Result<AutoFulfillmentSnapshot>> 선정실행Async(FulfillmentSourceKey key, Guid id, CancellationToken ct = default) => inner.선정실행Async(key, id, ct);
        public Task<AutoFulfillmentSnapshot?> 상태Async(FulfillmentSourceKey key, CancellationToken ct = default) => inner.상태Async(key, ct);
        public Task<Result<AutoFulfillmentSnapshot>> 재확인Async(FulfillmentSourceKey key, AutoFulfillmentActionRequest request, CancellationToken ct = default) => inner.재확인Async(key, request, ct);
        public Task<Result<AutoFulfillmentSnapshot>> 취소Async(FulfillmentSourceKey key, AutoFulfillmentActionRequest request, CancellationToken ct = default) => inner.취소Async(key, request, ct);
        public Task<bool> 내부수행허용Async(FulfillmentSourceKey key, string? attemptId = null, CancellationToken ct = default) => inner.내부수행허용Async(key, attemptId, ct);
        public Task<Result<AutoFulfillmentSnapshot>> 모의증거기록Async(string id, FulfillmentMockEvidenceRequest request, CancellationToken ct = default) => inner.모의증거기록Async(id, request, ct);
        public Task<int> 대기처리Async(int take = 100, CancellationToken ct = default) => inner.대기처리Async(take, ct);
    }
}
