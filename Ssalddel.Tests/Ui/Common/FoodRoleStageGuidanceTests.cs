using Ssalddel.Application.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodRoleStageGuidanceTests
{
    [Fact]
    public async Task RestaurantCurrentRoundTransitionsFromPreparationToPickupDeliveryAndReceipt()
    {
        var order = new 음식주문응답
        {
            주문번호 = "order-current", 상태 = 음식주문상태코드.기사배정, 배차상태 = 음식주문배차상태코드.기사배정,
            CurrentPreparationRound = 2, CurrentCookingStartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            RecookingRequestedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            // 첫 음식의 이력은 이번 음식의 준비 완료가 아닙니다.
            픽업준비시각Utc = DateTime.UtcNow.AddHours(-1), 수령인정보 = new() { 요청사항 = "견과류를 빼 주세요." }
        };
        var api = RestaurantApi(order);
        var adapter = new RestaurantRoleWorkspaceAdapter(api);
        음식배달가능행동Projector.음식점용(order);
        var cooking = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Equal("재조리 중", Summary(cooking).Metrics!.Single(field => field.Label == "조리 단계").Value);
        Assert.Equal("미확인", Summary(cooking).Metrics!.Single(field => field.Label == "준비 완료").Value);
        Assert.Equal(음식배달가능행동Ids.음식점픽업준비완료, Assert.Single(cooking.Actions!, action => action.IsPrimary).Key);
        Assert.Contains(cooking.Sections!.SelectMany(section => section.Fields), field => field.Label == "요청사항" && field.Value == "견과류를 빼 주세요.");
        Assert.Contains(cooking.Sections!.SelectMany(section => section.Fields), field => field.Label == "조리 회차" && field.Value == "2회차");

        order.CurrentPickupReadyAtUtc = DateTime.UtcNow;
        음식배달가능행동Projector.음식점용(order);
        var prepared = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Contains("인계해 주세요", Summary(prepared).Request!.Value);
        Assert.Empty(prepared.Actions!);

        foreach (var (status, expected) in new[]
        {
            (음식주문상태코드.픽업완료, "기사가 음식을 전달하고 있습니다."),
            (음식주문상태코드.전달완료, "음식 전달이 완료되었습니다. 주문자의 수령 확인을 기다립니다."),
            (음식주문상태코드.수령확인, "주문자가 수령을 확인했습니다. 이 주문은 완료됐습니다.")
        })
        {
            order.상태 = status;
            음식배달가능행동Projector.음식점용(order);
            var item = Assert.Single((await adapter.LoadAsync(order.주문번호, default)).Items);
            Assert.Equal(status, Summary(item).Metrics!.Single(field => field.Label == "조리 단계").Value);
            Assert.Equal(expected, Summary(item).Request!.Value);
            Assert.Empty(item.Actions!);
        }
        Assert.Empty(api.Writes);
    }

    [Theory]
    [InlineData(음식주문배차상태코드.배차대기)]
    [InlineData(음식주문배차상태코드.추천중)]
    [InlineData(음식주문배차상태코드.배차불가)]
    [InlineData("추천만료")]
    [InlineData("수락취소")]
    [InlineData("배차취소")]
    public async Task RestaurantReadyFoodWaitsForValidAssignmentInsteadOfRequestingHandoff(string dispatch)
    {
        var order = new 음식주문응답 { 주문번호 = "order-current", 상태 = 음식주문상태코드.픽업대기,
            배차상태 = dispatch, CurrentPickupReadyAtUtc = DateTime.UtcNow };
        음식배달가능행동Projector.음식점용(order);
        var adapter = new RestaurantRoleWorkspaceAdapter(RestaurantApi(order));
        var item = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.DoesNotContain("인계해 주세요", Summary(item).Request!.Value);
        Assert.Empty(item.Actions!);
        Assert.Contains(dispatch is 음식주문배차상태코드.배차대기 or 음식주문배차상태코드.추천중 ? "기사 재배정" : "기사 배정 확인", Summary(item).Request!.Value);
    }

    [Fact]
    public async Task RestaurantStartedCookingAndExpiredPreparationActionKeepRecoveryVisible()
    {
        var order = new 음식주문응답 { 주문번호 = "order-current", 상태 = 음식주문상태코드.조리중,
            배차상태 = 음식주문배차상태코드.배차대기, CurrentCookingStartedAtUtc = DateTime.UtcNow,
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.음식점픽업준비완료, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) }] };
        var adapter = new RestaurantRoleWorkspaceAdapter(RestaurantApi(order));
        var item = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Contains("기사 재배정", Summary(item).Request!.Value);
        Assert.DoesNotContain(item.Actions!, action => action.Enabled || action.IsPrimary);
    }

    [Fact]
    public async Task RestaurantAllowedPreparationDoesNotLoseDispatchFailureAttention()
    {
        var order = new 음식주문응답 { 주문번호 = "order-current", 상태 = 음식주문상태코드.조리중,
            배차상태 = 음식주문배차상태코드.배차불가, CurrentPreparationRound = 2,
            CurrentCookingStartedAtUtc = DateTime.UtcNow, RecookingRequestedAtUtc = DateTime.UtcNow };
        음식배달가능행동Projector.음식점용(order);
        var item = Assert.Single((await new RestaurantRoleWorkspaceAdapter(RestaurantApi(order)).LoadAsync(null, default)).Items);
        Assert.Equal(음식배달가능행동Ids.음식점픽업준비완료, Assert.Single(item.Actions!, action => action.IsPrimary).Key);
        Assert.Contains("픽업 준비 완료", Summary(item).Request!.Value);
        Assert.Contains("기사 배정 확인", Summary(item).Request!.Value);
    }

    [Fact]
    public async Task RestaurantMayFinishCookingWhileClearlyWaitingForReassignment()
    {
        var order = new 음식주문응답 { 주문번호 = "order-current", 상태 = 음식주문상태코드.조리중,
            배차상태 = 음식주문배차상태코드.추천중, CurrentPreparationRound = 2, CurrentCookingStartedAtUtc = DateTime.UtcNow };
        음식배달가능행동Projector.음식점용(order);
        var item = Assert.Single((await new RestaurantRoleWorkspaceAdapter(RestaurantApi(order)).LoadAsync(null, default)).Items);
        Assert.True(Assert.Single(item.Actions!, action => action.IsPrimary && action.Key == 음식배달가능행동Ids.음식점픽업준비완료).Enabled);
        Assert.Contains("픽업 준비 완료", Summary(item).Request!.Value);
        Assert.Contains("기사 재배정", Summary(item).Request!.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DriverWaitsForCurrentFoodAndPickupPermissionThenMovesToDelivery(bool recooking)
    {
        var active = new FoodDeliveryDriverActiveDeliveryDto
        {
            OfferId = "offer-current", WorkStatus = DriverWorkOfferStatus.MovingToPickup,
            DeliveryAttemptId = "attempt-current", AttemptRevision = 5, RestaurantArrivedAtUtc = DateTime.UtcNow,
            CurrentPreparationRound = recooking ? 2 : 1, RecookingRequestedAtUtc = recooking ? DateTime.UtcNow : null,
            // 예상 완료시각이 지났어도 실제 준비 완료/허용 행동을 대신하지 않습니다.
            DisplayedPreparationReadyAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Pickup = new() { Address = "픽업 주소" }, Dropoff = new() { Address = "전달 주소" }
        };
        var api = DriverApi(active);
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new NoLocation());
        active.AvailableActions = 음식배달가능행동Projector.기사배달용(active.WorkStatus, 5, active.RestaurantArrivedAtUtc, false);
        var waiting = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Contains("준비 완료를 기다려 주세요", Summary(waiting).Request!.Value);
        var pickup = Assert.Single(waiting.Actions!, action => action.IsPrimary);
        Assert.False(pickup.Enabled);
        Assert.Equal(Summary(waiting).Request!.Value, pickup.DisabledReason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync(active.OfferId, pickup.Key, Guid.NewGuid(), default));
        Assert.Empty(api.Writes);

        active.CurrentPickupReadyAtUtc = DateTime.UtcNow;
        // 준비시각만 생기고 서버 가능 행동이 없으면 계속 비활성입니다.
        var readyOnly = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Contains("픽업 가능 여부를 새로고침", Summary(readyOnly).Request!.Value);
        Assert.False(Assert.Single(readyOnly.Actions!, action => action.IsPrimary).Enabled);

        active.AvailableActions = 음식배달가능행동Projector.기사배달용(active.WorkStatus, 5, active.RestaurantArrivedAtUtc, true);
        var ready = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Contains("픽업해 주세요", Summary(ready).Request!.Value);
        Assert.True(Assert.Single(ready.Actions!, action => action.IsPrimary).Enabled);

        active.WorkStatus = DriverWorkOfferStatus.MovingToDropoff;
        active.AvailableActions = 음식배달가능행동Projector.기사배달용(active.WorkStatus, 5, active.RestaurantArrivedAtUtc, true);
        var delivery = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Equal("전달지", Summary(delivery).DestinationLabel);
        Assert.Equal(음식배달가능행동Ids.기사전달완료, Assert.Single(delivery.Actions!, action => action.IsPrimary).Key);
        Assert.DoesNotContain("픽업", Summary(delivery).Request!.Value);
        Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task DriverNoticeSeparatesCurrentRecookingDeliveryNewOfferAndWaitingWithoutHidingDispatchWarnings()
    {
        var active = new FoodDeliveryDriverActiveDeliveryDto
        {
            OfferId = "offer-current", WorkStatus = DriverWorkOfferStatus.MovingToPickup,
            DeliveryAttemptId = "attempt-current", AttemptRevision = 5, RestaurantArrivedAtUtc = DateTime.UtcNow,
            CurrentPreparationRound = 2, RecookingRequestedAtUtc = DateTime.UtcNow,
            AvailableActions = 음식배달가능행동Projector.기사배달용(DriverWorkOfferStatus.MovingToPickup, 5, DateTime.UtcNow, false)
        };
        var offer = new FoodDeliveryDriverOfferDto { OfferId = "offer-new", AvailableActions = 음식배달가능행동Projector.기사제안용(null) };
        var api = DriverApi(active);
        var workspace = Assert.IsType<FoodDeliveryDriverWorkspaceDto>(api.Reads["api/v1/driver/food-deliveries/workspace"]);
        workspace.Recommendations = [offer];
        var availability = Assert.IsType<운영배차수신상태Dto>(api.Reads["api/v1/driver/operational-dispatch/availability"]);
        var work = Assert.IsType<기사운행상태응답>(api.Reads["api/v1/driver/food-deliveries/work/status"]);
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new NoLocation());

        // 신규 제안을 선택하려 해도 현재 배달이 우선이며 재조리 준비 대기를 신규 요청 대기로 표시하지 않습니다.
        var current = await adapter.LoadAsync(offer.OfferId, default);
        Assert.Equal(active.OfferId, current.SelectedId);
        Assert.Contains("현재 배달", current.Message);
        Assert.DoesNotContain("새로운 배달 요청을 기다리고", current.Message);
        var item = Assert.Single(current.Items, value => value.Id == active.OfferId);
        Assert.Contains("재조리 음식이 아직 준비되지", Summary(item).Request!.Value);
        Assert.False(Assert.Single(item.Actions!, action => action.IsPrimary).Enabled);
        var suggestion = Assert.Single(current.Items, value => value.Id == offer.OfferId);
        Assert.Equal("배달 제안 · 수락 전", suggestion.Status);
        Assert.False(suggestion.IsCurrent);
        Assert.True(Assert.Single(suggestion.Actions!, action => action.Key == 음식배달가능행동Ids.기사제안수락).Enabled);

        foreach (var (code, expected) in new[]
        {
            (운영배차실효상태Code.서버일시정지, "신규 배차가 일시 중지"),
            (운영배차실효상태Code.연결확인불가, "배차 연결 상태를 확인할 수 없습니다"),
            (운영배차실효상태Code.조건부적합, "신규 배차 조건을 확인")
        })
        {
            availability.실효상태Code = code;
            Assert.Contains(expected, (await adapter.LoadAsync(null, default)).Message);
        }
        availability.실효상태Code = 운영배차실효상태Code.배차가능;
        availability.수신의사Code = 운영배차수신의사Code.Off;
        Assert.Contains("신규 배차 받기를 켜면", (await adapter.LoadAsync(null, default)).Message);
        availability.수신의사Code = 운영배차수신의사Code.On;
        work.Status = "운행종료";
        Assert.Contains("운행을 시작하면", (await adapter.LoadAsync(null, default)).Message);

        work.Status = "운행중";
        workspace.ActiveDeliveries = [];
        var offered = await adapter.LoadAsync(null, default);
        Assert.Equal(offer.OfferId, offered.SelectedId);
        Assert.Contains("새로운 배달 제안을 확인", offered.Message);
        Assert.DoesNotContain("현재 배달", offered.Message);
        workspace.Recommendations = [];
        var waiting = await adapter.LoadAsync(null, default);
        Assert.Empty(waiting.Items);
        Assert.Contains("새로운 배달 요청을 기다리고", waiting.Message);
        Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task DriverExpiredPickupPermissionDoesNotInstructPickup()
    {
        var active = new FoodDeliveryDriverActiveDeliveryDto { OfferId = "offer-current", WorkStatus = DriverWorkOfferStatus.MovingToPickup,
            CurrentPreparationRound = 2, CurrentPickupReadyAtUtc = DateTime.UtcNow, RestaurantArrivedAtUtc = DateTime.UtcNow,
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사픽업확인, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) }] };
        var item = Assert.Single((await new FoodDriverRoleWorkspaceAdapter(DriverApi(active), new NoLocation()).LoadAsync(null, default)).Items);
        Assert.DoesNotContain("픽업해 주세요", Summary(item).Request!.Value);
        Assert.Contains("새로고침", Summary(item).Request!.Value);
        Assert.False(Assert.Single(item.Actions!, action => action.IsPrimary).Enabled);
    }

    [Theory]
    [InlineData(음식주문상태코드.주문확인, "기사 배정 대기")]
    [InlineData(음식주문상태코드.기사배정, "기사 배정됨 · 준비 상태 확인")]
    public async Task OrdererDoesNotPresentAssignmentAsCooking(string status, string expected)
    {
        var detail = new 주문자음식주문상세응답 { 주문 = new() { 주문번호 = "order-current", 상태 = status } };
        var item = Assert.Single((await new OrdererRoleWorkspaceAdapter(OrdererApi(detail)).LoadAsync(null, default)).Items);
        Assert.Equal(expected, Summary(item).Destination);
        Assert.Empty(item.Actions!);
    }

    [Theory]
    [InlineData(음식주문배차상태코드.배차불가, false)]
    [InlineData("추천만료", false)]
    [InlineData("수락취소", false)]
    [InlineData("배차취소", false)]
    [InlineData(음식주문배차상태코드.배차불가, true)]
    public async Task OrdererDispatchRecoveryUsesExistingGuideAndDoesNotInventCancellation(string dispatch, bool fromOrder)
    {
        var detail = new 주문자음식주문상세응답
        {
            주문 = new() { 주문번호 = "order-current", 상태 = 음식주문상태코드.조리중,
                배차상태 = fromOrder ? dispatch : 음식주문배차상태코드.기사배정 },
            배달진행 = new() { 현재운송상태 = fromOrder ? 음식주문배차상태코드.미요청 : dispatch }
        };
        var item = Assert.Single((await new OrdererRoleWorkspaceAdapter(OrdererApi(detail)).LoadAsync(null, default)).Items);
        Assert.Equal("조리 중", Summary(item).Destination);
        Assert.Contains("주문 취소나 환불", Summary(item).Request!.Value);
        Assert.Contains(dispatch == 음식주문배차상태코드.배차불가 ? "자동 확정되는 것은 아니며" : "별도 확인", Summary(item).Request!.Value);
        Assert.Empty(item.Actions!);
    }

    private static RoleWorkspaceSummary Summary(RoleWorkspaceItem item) => Assert.IsType<RoleWorkspaceSummary>(item.Summary);

    private static FoodRoleWorkspaceAdapterTests.Api RestaurantApi(음식주문응답 order)
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/food-orders/restaurant/inbox?처리상태=" + Uri.EscapeDataString("미처리") + "&Page=1&PageSize=50"] = new 음식점주문수신함응답 { Items = [order] };
        api.Reads["api/v1/food-orders/restaurant/inbox/order-current"] = order;
        return api;
    }

    private static FoodRoleWorkspaceAdapterTests.Api DriverApi(FoodDeliveryDriverActiveDeliveryDto delivery)
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/driver/food-deliveries/workspace"] = new FoodDeliveryDriverWorkspaceDto { ActiveDeliveries = [delivery] };
        api.Reads["api/v1/driver/food-deliveries/work/status"] = new 기사운행상태응답 { Status = "운행중" };
        api.Reads["api/v1/driver/operational-dispatch/availability"] = new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능 };
        return api;
    }

    private static FoodRoleWorkspaceAdapterTests.Api OrdererApi(주문자음식주문상세응답 detail)
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/food-orders?page=1&pageSize=50"] = new 주문자음식주문목록응답 { Items = [detail.주문] };
        api.Reads["api/v1/food-orders/order-current"] = detail;
        return api;
    }

    private sealed class NoLocation : IRoleWorkspaceLocationProvider
    {
        public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult<RoleWorkspaceLocation?>(null);
    }
}
