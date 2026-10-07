using System.Reflection;
using RestaurantDeskApp.Components.Pages;
using RestaurantDeskApp.Models.Restaurant;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantOrderPresentationTests
{
    [Fact]
    public void RequestUsesExistingTextWithoutInferringInstructionsOrAddingContactInformation()
    {
        var page = Create(new()
        {
            상세주문 = new()
            {
                수령인정보 = new 음식주문수령인정보Dto
                {
                    요청사항 = "  숟가락 3개 요청\n문 앞에 놓아 주세요. <검토>  ",
                    연락처 = "synthetic-contact", 주소 = "synthetic-private-address"
                }
            }
        });
        var text = Property<string>(page, "CustomerRequestText");
        Assert.Equal("숟가락 3개 요청\n문 앞에 놓아 주세요. <검토>", text);
        Assert.DoesNotContain("synthetic-contact", text);
        Assert.DoesNotContain("synthetic-private-address", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ConfirmedEmptyRequestIsDifferentFromMissingOrderDetail(string? request)
    {
        var page = Create(new() { 상세주문 = new() { 수령인정보 = new() { 요청사항 = request } } });
        Assert.Equal("요청사항 없음", Property<string>(page, "CustomerRequestText"));
        typeof(OrderDetail).GetField("order", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, new 음식점주문DeskItem());
        Assert.Contains("확인이 필요", Property<string>(page, "CustomerRequestText"));
    }

    [Fact]
    public void FailedDispatchIsVisibleWithoutGrantingCookingAction()
    {
        var item = new 음식점주문DeskItem { 상태 = 음식주문상태코드.주문확인, 배차상태 = 음식주문배차상태코드.배차불가, 최근메시지 = "통제된 기사 배정 안내" };
        var page = Create(item);
        Assert.True(Property<bool>(page, "ShowDispatchAttention"));
        Assert.False(item.조리시작가능);
        Assert.Contains("아래 배차 안내", Property<string>(page, "NextStepGuide"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReassignmentNoticeIsShownAfterCookingStartsAndRemovedWhenDriverActionReturns(bool driverAssigned)
    {
        var item = new 음식점주문DeskItem
        {
            상태 = 음식점주문Desk상태코드.조리중,
            배차상태 = driverAssigned ? 음식주문배차상태코드.기사배정 : 음식주문배차상태코드.배차대기,
            상세주문 = new() { 조리시작시각Utc = DateTime.UtcNow },
            AvailableActions = driverAssigned
                ? [Action(음식배달가능행동Ids.음식점조리시간변경), Action(음식배달가능행동Ids.음식점픽업준비완료)]
                : [Action(음식배달가능행동Ids.음식점조리시간변경)]
        };
        Assert.Equal(!driverAssigned, Property<bool>(Create(item), "ShowDispatchAttention"));
    }

    [Fact]
    public void NormalPreCookingWaitDoesNotShowExceptionNotice()
    {
        var page = Create(new()
        {
            상태 = 음식주문상태코드.주문확인, 배차상태 = 음식주문배차상태코드.배차대기,
            상세주문 = new()
        });
        Assert.False(Property<bool>(page, "ShowDispatchAttention"));
    }

    [Theory]
    [InlineData("취소")]
    [InlineData("거절")]
    [InlineData("수령확인")]
    public void ClosedOrderDoesNotSurfaceOldDispatchFailure(string state)
        => Assert.False(Property<bool>(Create(new() { 상태 = state, 배차상태 = 음식주문배차상태코드.배차불가 }), "ShowDispatchAttention"));

    [Fact]
    public void FailedRefreshDoesNotPresentOldDispatchReasonAsCurrent()
    {
        var page = Create(new() { 배차상태 = 음식주문배차상태코드.배차불가 });
        typeof(OrderDetail).GetField("readFailed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, true);
        Assert.False(Property<bool>(page, "ShowDispatchAttention"));
        Assert.Contains("상태를 다시 확인", Property<string>(page, "NextStepGuide"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecookingUsesCurrentRoundReadyInsteadOfHistoricalReady(bool ready)
    {
        var initialReady = DateTime.UtcNow.AddHours(-1);
        var recooking = initialReady.AddMinutes(20);
        var item = new 음식점주문DeskItem
        {
            상태 = ready ? 음식점주문Desk상태코드.픽업대기 : 음식점주문Desk상태코드.조리중,
            배차상태 = 음식주문배차상태코드.배차대기,
            상세주문 = new()
            {
                조리시작시각Utc = initialReady.AddMinutes(-20), 픽업준비시각Utc = initialReady,
                CurrentPreparationRound = 2, CurrentCookingStartedAtUtc = recooking,
                RecookingRequestedAtUtc = recooking, CurrentPickupReadyAtUtc = ready ? recooking.AddMinutes(10) : null
            },
            AvailableActions = ready ? [] : [Action(음식배달가능행동Ids.음식점픽업준비완료)]
        };
        var page = Create(item);

        Assert.True(item.재조리주문);
        Assert.True(item.현재조리시작확인);
        Assert.Equal(ready, item.현재픽업준비완료);
        Assert.Equal(!ready, item.픽업준비가능);
        Assert.True(Property<bool>(page, "ShowCurrentRecooking"));
        Assert.Contains(ready ? "재조리 음식이 준비" : "다시 조리 중", Property<string>(page, "CurrentStepLabel"));
        Assert.Contains(ready ? "새 기사가 픽업" : "이번 음식이 완성되면", Property<string>(page, "NextStepGuide"));
        Assert.Contains(ready ? "준비 완료" : "준비 중", Property<string>(page, "CurrentPreparationText"));
        Assert.Equal(initialReady, item.상세주문.픽업준비시각Utc);
    }

    [Fact]
    public void NewRoundDoesNotInferCookingOrReadyFromInitialHistory()
    {
        var item = new 음식점주문DeskItem
        {
            상세주문 = new()
            {
                CurrentPreparationRound = 2,
                조리시작시각Utc = DateTime.UtcNow.AddHours(-1),
                픽업준비시각Utc = DateTime.UtcNow.AddMinutes(-40)
            }
        };
        Assert.True(item.재조리주문);
        Assert.False(item.현재조리시작확인);
        Assert.False(item.현재픽업준비완료);
    }

    [Theory]
    [InlineData(음식점주문Desk상태코드.픽업완료)]
    [InlineData(음식점주문Desk상태코드.전달완료)]
    [InlineData(음식점주문Desk상태코드.수령확인)]
    [InlineData(음식점주문Desk상태코드.거절)]
    [InlineData(음식점주문Desk상태코드.취소)]
    public void FinishedRecookingDoesNotReplaceTheDeliveryOrClosedState(string state)
    {
        var page = Create(new()
        {
            상태 = state,
            상세주문 = new() { CurrentPreparationRound = 2, RecookingRequestedAtUtc = DateTime.UtcNow.AddMinutes(-10) }
        });
        Assert.False(Property<bool>(page, "ShowCurrentRecooking"));
        Assert.DoesNotContain("다시 조리", Property<string>(page, "NextStepGuide"));
    }

    [Fact]
    public void FailedReadDoesNotPresentRecookingReadyAsAConfirmedCurrentAction()
    {
        var page = Create(new()
        {
            상태 = 음식점주문Desk상태코드.조리중,
            상세주문 = new() { CurrentPreparationRound = 2, RecookingRequestedAtUtc = DateTime.UtcNow },
            AvailableActions = [Action(음식배달가능행동Ids.음식점픽업준비완료)]
        });
        typeof(OrderDetail).GetField("readFailed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, true);
        Assert.True(Property<bool>(page, "WorkDisabled"));
        Assert.Contains("최신 상태 확인", Property<string>(page, "CurrentStepLabel"));
    }

    private static 업무가능행동Dto Action(string id) => new() { ActionId = id };
    private static OrderDetail Create(음식점주문DeskItem item)
    {
        var page = new OrderDetail();
        typeof(OrderDetail).GetField("order", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, item);
        return page;
    }
    private static T Property<T>(OrderDetail page, string name)
        => (T)typeof(OrderDetail).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
}
