using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleWorkspaceActionUsageTests
{
    [Theory]
    [InlineData("timeline")]
    [InlineData("payment")]
    [InlineData("proofs")]
    public void ShipperInquiryToolsStayOptionalEvenWhenHighlighted(string key)
    {
        var item = ShipperWorkspaceAdapter.Map(new() { 의뢰Id = "request-a" });
        var action = Assert.Single(item.Actions!, value => value.Key == key) with { IsPrimary = true };
        Assert.Equal(RoleWorkspaceActionUsage.OptionalTool, RoleWorkspaceActionPolicy.GetUsage("shipper", action));
        Assert.False(RoleWorkspaceActionPolicy.KeepVisible("shipper", action, false));
        Assert.False(RoleWorkspaceActionPolicy.KeepVisible("shipper", action, true));
    }

    [Theory]
    [InlineData("배차확정", "arrive-pickup")]
    [InlineData("상차지도착", "pickup")]
    [InlineData("운송중", "arrive-dropoff")]
    [InlineData("하차지도착", "dropoff")]
    public void CargoProcedureKeepsServerAvailabilityRouteAndConfirmation(string state, string key)
    {
        var item = CargoDriverWorkspaceAdapter.Map(new 기사운송상세응답
        {
            Id = 3, 상태 = state, 가능한행동 = [], 운송진행보류 = true
        });
        var action = Assert.Single(item.Actions!, value => value.Key == key) with { IsPrimary = false };
        var original = action with { };
        Assert.False(action.Enabled);
        Assert.Equal(RoleWorkspaceActionUsage.Required, RoleWorkspaceActionPolicy.GetUsage("cargo-driver", action));
        Assert.True(RoleWorkspaceActionPolicy.KeepVisible("cargo-driver", action, false));
        Assert.Equal(original, action);
        if (key is "pickup" or "dropoff")
        {
            Assert.NotNull(action.Route);
            Assert.False(action.RequiresConfirmation);
        }
        else Assert.Null(action.Route);
        Assert.NotNull(action.DisabledReason);
    }

    [Theory]
    [InlineData("orderer", 음식배달가능행동Ids.주문수령확인)]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점주문수락)]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점조리시작)]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점픽업준비완료)]
    [InlineData("food-driver", 음식배달가능행동Ids.기사제안수락)]
    [InlineData("food-driver", 음식배달가능행동Ids.기사픽업확인)]
    [InlineData("food-driver", 음식배달가능행동Ids.기사전달완료)]
    public void FoodProcedureDoesNotDependOnHighlightOrInputRoute(string role, string key)
    {
        var action = new RoleWorkspaceAction(key, "현재 업무", "/existing/input", IsPrimary: false);
        Assert.Equal(RoleWorkspaceActionUsage.Required, RoleWorkspaceActionPolicy.GetUsage(role, action));
        Assert.True(RoleWorkspaceActionPolicy.KeepVisible(role, action, false));
    }

    [Fact]
    public void RestaurantArrivalStaysConditionalBecausePickupCanRecordMissingArrival()
    {
        // FoodDeliveryDriverWorkService의 픽업 처리에서 가게도착시각Utc ??= changedAtUtc로 보완한다.
        // 별도 도착 기록을 픽업의 절대 선행조건으로 분류하지 않지만 현재 행동은 직접 표시한다.
        var action = new RoleWorkspaceAction(음식배달가능행동Ids.기사가게도착, "음식점 도착", IsPrimary: false);
        Assert.Equal(RoleWorkspaceActionUsage.Conditional, RoleWorkspaceActionPolicy.GetUsage("food-driver", action));
        Assert.True(RoleWorkspaceActionPolicy.KeepVisible("food-driver", action, false));
    }

    [Theory]
    [InlineData("orderer", 음식배달가능행동Ids.주문취소)]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점주문거절)]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점조리시간변경)]
    [InlineData("food-driver", 음식배달가능행동Ids.기사제안거절)]
    [InlineData("food-driver", 음식배달가능행동Ids.기사배달중단)]
    [InlineData("cargo-driver", "issue")]
    [InlineData("operator", OperatorRoleWorkspaceAdapter.ReviewInterruption)]
    public void KnownRecoveryActionsRemainVisibleWithDisabledReason(string role, string key)
    {
        var action = new RoleWorkspaceAction(key, "확인 필요", "/existing/recovery", Enabled: false,
            DisabledReason: "현재 상태를 새로고침해 주세요.");
        Assert.Equal(RoleWorkspaceActionUsage.Conditional, RoleWorkspaceActionPolicy.GetUsage(role, action));
        Assert.True(RoleWorkspaceActionPolicy.KeepVisible(role, action, false));
        Assert.False(action.Enabled);
        Assert.Equal("현재 상태를 새로고침해 주세요.", action.DisabledReason);
    }

    [Theory]
    [InlineData(FoodDriverRoleWorkspaceAdapter.StartWork)]
    [InlineData(FoodDriverRoleWorkspaceAdapter.StopWork)]
    [InlineData(FoodDriverRoleWorkspaceAdapter.EnableDispatch)]
    [InlineData(FoodDriverRoleWorkspaceAdapter.DisableDispatch)]
    [InlineData(FoodDriverRoleWorkspaceAdapter.UpdateLocation)]
    public void DriverEligibilityAndGpsControlsAreDirectConditionalActions(string key)
    {
        var action = new RoleWorkspaceAction(key, "운행 조건", IsPrimary: false);
        Assert.Equal(RoleWorkspaceActionUsage.Conditional, RoleWorkspaceActionPolicy.GetUsage("food-driver", action));
        Assert.True(RoleWorkspaceActionPolicy.KeepVisible("food-driver", action, false));
    }

    [Fact]
    public void CargoOfferReviewRemainsDirectBeforeAcceptingTransport()
    {
        var action = new RoleWorkspaceAction("offer", "추천 조건 확인", "/existing/offer", RequiresConfirmation: false);
        Assert.Equal(RoleWorkspaceActionUsage.Conditional, RoleWorkspaceActionPolicy.GetUsage("cargo-driver", action));
        Assert.True(RoleWorkspaceActionPolicy.KeepVisible("cargo-driver", action, false));
        Assert.False(action.RequiresConfirmation);
    }

    [Theory]
    [InlineData("inbound")]
    [InlineData("inspection")]
    [InlineData("put-away")]
    [InlineData("picking")]
    [InlineData("packing")]
    [InlineData("handoff")]
    [InlineData("outbound")]
    public void WarehouseWorkflowInputsRemainConditionalAndDirect(string key)
    {
        var action = new RoleWorkspaceAction(key, "현재 창고 공정", "/existing/warehouse/input", IsPrimary: false);
        Assert.Equal(RoleWorkspaceActionUsage.Conditional, RoleWorkspaceActionPolicy.GetUsage("warehouse", action));
        Assert.True(RoleWorkspaceActionPolicy.KeepVisible("warehouse", action, false));
    }

    [Theory]
    [InlineData("shipper", "create")]
    [InlineData("orderer", "food.new-order")]
    public void IndependentStartToolsAreClassifiedWithoutChangingTheirAction(string role, string key)
    {
        var action = new RoleWorkspaceAction(key, "새 업무", "/existing/new", IsPrimary: true, RequiresConfirmation: false);
        Assert.Equal(RoleWorkspaceActionUsage.OptionalTool, RoleWorkspaceActionPolicy.GetUsage(role, action));
        Assert.True(action.IsPrimary);
        Assert.Equal("/existing/new", action.Route);
        Assert.False(action.RequiresConfirmation);
    }

    [Theory]
    [InlineData(null, "pickup")]
    [InlineData("unknown-role", "pickup")]
    [InlineData("shipper", "pickup")]
    [InlineData("cargo-driver", "payment")]
    [InlineData("orderer", 음식배달가능행동Ids.기사픽업확인)]
    [InlineData("food-driver", 음식배달가능행동Ids.주문취소)]
    [InlineData("operator", "issue")]
    [InlineData("restaurant", "FoodOrder.StartCooking ")]
    [InlineData("restaurant", "foodorder.startcooking")]
    public void ClassificationRequiresTheKnownRoleAndExactActionKey(string? role, string key)
    {
        var action = new RoleWorkspaceAction(key, "필수 처리", "/existing/action", IsPrimary: true);
        Assert.Equal(RoleWorkspaceActionUsage.Unclassified, RoleWorkspaceActionPolicy.GetUsage(role, action));
        Assert.True(RoleWorkspaceActionPolicy.KeepVisible(role, action, false));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void UnknownActionPreservesExistingHighlightAndDetailsFallback(bool primary, bool expanded, bool visible)
    {
        var action = new RoleWorkspaceAction("future.action", "로그인 또는 문제 신고", "/workspace-login/orderer", IsPrimary: primary);
        Assert.Equal(RoleWorkspaceActionUsage.Unclassified, RoleWorkspaceActionPolicy.GetUsage("orderer", action));
        Assert.Equal(visible, RoleWorkspaceActionPolicy.KeepVisible("orderer", action, expanded));
    }

    [Fact]
    public async Task ClassifyingLoadedOrderActionsDoesNotSendCommandsOrChangeTheSnapshot()
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/food-orders?page=1&pageSize=50"] = new 주문자음식주문목록응답
        {
            Items = [new() { 주문번호 = "order-a", 상태 = 음식주문상태코드.전달완료 }]
        };
        api.Reads["api/v1/food-orders/order-a"] = new 주문자음식주문상세응답
        {
            주문 = new() { 주문번호 = "order-a", 상태 = 음식주문상태코드.전달완료 },
            배달진행 = new() { 수령확인가능 = true },
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.주문수령확인 },
                new() { ActionId = 음식배달가능행동Ids.주문취소 }]
        };
        var snapshot = await new OrdererRoleWorkspaceAdapter(api).LoadAsync(null, default);
        var item = Assert.Single(snapshot.Items);
        var actions = item.Actions!.ToArray();
        foreach (var action in actions)
        {
            Assert.True(RoleWorkspaceActionPolicy.KeepVisible(snapshot.RoleKey, action, false));
            _ = RoleWorkspaceActionPolicy.GetUsage(snapshot.RoleKey, action);
        }
        Assert.Equal(actions, item.Actions);
        Assert.Equal("order-a", snapshot.SelectedId);
        Assert.Equal(2, api.ReadRoles.Count);
        Assert.All(api.ReadRoles, role => Assert.Equal("orderer", role));
        Assert.Empty(api.Writes);
    }
}
