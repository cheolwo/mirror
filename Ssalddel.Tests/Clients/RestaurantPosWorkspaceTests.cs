using System.Reflection;
using Microsoft.AspNetCore.Components;
using RestaurantDeskApp.Components.Pages;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantPosWorkspaceTests
{
    [Fact]
    public void AttentionOverlapsNewOrProgressWithoutChangingTheBusinessState()
    {
        var fresh = Item("new", 음식점주문Desk상태코드.주문대기);
        fresh.배차상태 = 음식주문배차상태코드.배차불가;
        var accepted = Item("accepted", 음식주문상태코드.주문확인);
        accepted.배차상태 = 음식주문배차상태코드.배차불가;
        var cooking = Item("cooking", 음식점주문Desk상태코드.조리중);
        var workspace = new RestaurantPosPresentation();
        var orders = new[] { fresh, accepted, cooking };

        workspace.Queue = RestaurantPosQueue.New;
        Assert.Same(fresh, Assert.Single(workspace.VisibleOrders(orders)));
        workspace.Queue = RestaurantPosQueue.InProgress;
        Assert.Equal(new[] { "accepted", "cooking" }, workspace.VisibleOrders(orders).Select(item => item.주문번호));
        workspace.Queue = RestaurantPosQueue.Attention;
        Assert.Equal(new[] { "accepted", "new" }, workspace.VisibleOrders(orders).Select(item => item.주문번호));

        Assert.Equal(음식점주문Desk상태코드.주문대기, fresh.상태);
        Assert.Equal(음식주문상태코드.주문확인, accepted.상태);
        Assert.False(accepted.조리시작가능);
        Assert.False(accepted.픽업준비가능);
    }

    [Theory]
    [InlineData(음식점주문Desk상태코드.취소)]
    [InlineData(음식점주문Desk상태코드.거절)]
    [InlineData(음식점주문Desk상태코드.수령확인)]
    public void ClosedOrdersDoNotReappearInWorkOrAttentionQueuesFromHistoricalDispatchFailure(string status)
    {
        var order = Item("closed", status);
        order.배차상태 = 음식주문배차상태코드.배차불가;
        var workspace = new RestaurantPosPresentation();

        foreach (var queue in new[] { RestaurantPosQueue.New, RestaurantPosQueue.InProgress, RestaurantPosQueue.Attention })
        {
            workspace.Queue = queue;
            Assert.Empty(workspace.VisibleOrders([order]));
        }
        workspace.Queue = RestaurantPosQueue.All;
        Assert.Same(order, Assert.Single(workspace.VisibleOrders([order])));
    }

    [Fact]
    public void MissingDetailRemainsVisibleAsAttentionRatherThanAConfirmedEmptyQueue()
    {
        var order = Item("unknown", 음식점주문Desk상태코드.상세조회실패);
        var workspace = new RestaurantPosPresentation { Queue = RestaurantPosQueue.Attention };

        Assert.Same(order, Assert.Single(workspace.VisibleOrders([order])));
        Assert.Equal("상태 확인 필요", RestaurantPosPresentation.StatusLabel(order));
        Assert.Empty(order.AvailableActions);
    }

    [Fact]
    public void QueueUsesOldestReceiptFirstWithStableOrderNumberTieBreakWithoutMutatingTheSnapshot()
    {
        var earlier = Item("earlier", 음식점주문Desk상태코드.주문대기);
        earlier.접수시각 = ReceiptTime.AddMinutes(-1);
        earlier.미확인 = false;
        var second = Item("B", 음식점주문Desk상태코드.주문대기);
        var first = Item("A", 음식점주문Desk상태코드.주문대기);
        var later = Item("later", 음식점주문Desk상태코드.주문대기);
        later.접수시각 = ReceiptTime.AddMinutes(1);
        var source = new[] { later, second, earlier, first };

        var result = new RestaurantPosPresentation().VisibleOrders(source);

        Assert.Equal(new[] { "earlier", "A", "B", "later" }, result.Select(item => item.주문번호));
        Assert.Equal(new[] { "later", "B", "earlier", "A" }, source.Select(item => item.주문번호));
        Assert.Same(earlier, result[0]);
    }

    [Fact]
    public void NewOrdersQueueChangesAndSelectedOrderLeavingInboxDoNotStealSelection()
    {
        var workspace = new RestaurantPosPresentation();
        Assert.True(workspace.Select("selected"));
        var selected = Item("selected", 음식점주문Desk상태코드.조리중);
        var newOrder = Item("incoming", 음식점주문Desk상태코드.주문대기);

        workspace.VisibleOrders([selected, newOrder]);
        Assert.Equal("selected", workspace.SelectedOrderNo);
        workspace.Queue = RestaurantPosQueue.New;
        Assert.Same(newOrder, Assert.Single(workspace.VisibleOrders([selected, newOrder])));
        Assert.Equal("selected", workspace.SelectedOrderNo);
        workspace.VisibleOrders([newOrder]);
        Assert.Equal("selected", workspace.SelectedOrderNo);
        workspace.VisibleOrders([]);
        Assert.Equal("selected", workspace.SelectedOrderNo);
    }

    [Fact]
    public void CommandBlocksChangingItsTargetUntilCompletionAndClearReleasesTheOldSession()
    {
        var workspace = new RestaurantPosPresentation();
        Assert.True(workspace.Select("A"));
        workspace.SetCommandInProgress(true);

        Assert.False(workspace.Select("B"));
        Assert.Equal("A", workspace.SelectedOrderNo);
        workspace.Queue = RestaurantPosQueue.Attention;
        workspace.VisibleOrders([]);
        Assert.True(workspace.CommandInProgress);
        Assert.Equal("A", workspace.SelectedOrderNo);

        workspace.SetCommandInProgress(false);
        Assert.True(workspace.Select("B"));
        workspace.SetCommandInProgress(true);
        workspace.Clear();
        Assert.Null(workspace.SelectedOrderNo);
        Assert.False(workspace.CommandInProgress);
        Assert.True(workspace.Select("C"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void InvalidSelectionDoesNotEraseTheCurrentOrder(string invalidOrderNo)
    {
        var workspace = new RestaurantPosPresentation();
        workspace.Select("current");

        Assert.False(workspace.Select(invalidOrderNo));
        Assert.Equal("current", workspace.SelectedOrderNo);
    }

    [Theory]
    [InlineData(음식점주문Desk상태코드.픽업대기, "픽업 준비 완료")]
    [InlineData(음식점주문Desk상태코드.픽업완료, "기사 픽업 완료")]
    [InlineData(음식점주문Desk상태코드.전달완료, "전달 완료 · 수령 대기")]
    [InlineData(음식점주문Desk상태코드.수령확인, "수령 확인")]
    public void PreparationPickupDeliveryAndReceiptRemainDifferentResults(string status, string expectedLabel)
    {
        var order = Item("A", status);

        Assert.Equal(expectedLabel, RestaurantPosPresentation.StatusLabel(order));
        Assert.Equal(status == 음식점주문Desk상태코드.수령확인, RestaurantPosPresentation.IsClosed(order));
        Assert.Equal(status != 음식점주문Desk상태코드.수령확인, RestaurantPosPresentation.IsInProgress(order));
    }

    [Theory]
    [InlineData(false, false, "재조리 대기")]
    [InlineData(true, false, "재조리 중")]
    [InlineData(true, true, "재조리 준비 완료")]
    public void RecookingUsesTheCurrentRoundInsteadOfTheInitialPreparationHistory(bool cooking, bool ready, string expectedLabel)
    {
        var initialReady = ReceiptTime.UtcDateTime.AddHours(-1);
        var order = Item("A", ready ? 음식점주문Desk상태코드.픽업대기 : 음식점주문Desk상태코드.조리중);
        order.상세주문 = new()
        {
            CurrentPreparationRound = 2,
            조리시작시각Utc = initialReady.AddMinutes(-20),
            픽업준비시각Utc = initialReady,
            CurrentCookingStartedAtUtc = cooking ? ReceiptTime.UtcDateTime : null,
            CurrentPickupReadyAtUtc = ready ? ReceiptTime.UtcDateTime.AddMinutes(10) : null
        };

        Assert.Equal(expectedLabel, RestaurantPosPresentation.StatusLabel(order));
        Assert.Equal(cooking, order.현재조리시작확인);
        Assert.Equal(ready, order.현재픽업준비완료);
        Assert.Equal(initialReady, order.상세주문.픽업준비시각Utc);
        Assert.False(order.픽업준비가능);
    }

    [Theory]
    [InlineData(음식점주문Desk상태코드.픽업완료, "기사 픽업 완료")]
    [InlineData(음식점주문Desk상태코드.전달완료, "전달 완료 · 수령 대기")]
    [InlineData(음식점주문Desk상태코드.수령확인, "수령 확인")]
    [InlineData(음식점주문Desk상태코드.취소, "주문 취소")]
    [InlineData(음식점주문Desk상태코드.거절, "주문 거절")]
    public void HistoricalRecookingDoesNotReplaceAHandoffOrClosedResult(string status, string expectedLabel)
    {
        var order = Item("A", status);
        order.상세주문 = new()
        {
            CurrentPreparationRound = 2,
            CurrentCookingStartedAtUtc = ReceiptTime.UtcDateTime,
            CurrentPickupReadyAtUtc = ReceiptTime.UtcDateTime.AddMinutes(10)
        };

        Assert.Equal(expectedLabel, RestaurantPosPresentation.StatusLabel(order));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OlderListOrAcceptanceCannotRegressTheSharedSelectedOrderOrItsCurrentPreparationRound(bool acceptanceResponse)
    {
        var projection = new 음식점주문InboxProjection(new object(), new Preparation());
        var latest = CanonicalOrder(12, 음식점주문Desk상태코드.픽업대기, round: 2);
        latest.CurrentCookingStartedAtUtc = ReceiptTime.UtcDateTime;
        latest.CurrentPickupReadyAtUtc = ReceiptTime.UtcDateTime.AddMinutes(10);
        latest.조리예상분 = 45;
        latest.배차상태 = 음식주문배차상태코드.기사배정;
        latest.AvailableActions = [new() { ActionId = 음식배달가능행동Ids.음식점조리시간변경, ExpectedRevision = 12 }];
        var selected = projection.UpsertServerOrder(latest, 음식점주문복구출처.재연결재조회);
        var listReference = Assert.Single(projection.CaptureSnapshot(null));
        var recoveryTime = selected.최근복구시각;
        var products = selected.상품목록;
        var actions = selected.AvailableActions;
        var message = selected.최근메시지;
        var acceptedAt = selected.수락시각;
        var stale = CanonicalOrder(11, 음식주문상태코드.주문확인, round: 1);
        stale.조리예상분 = 8;
        stale.배차상태 = 음식주문배차상태코드.배차불가;
        stale.상품목록 = [new() { 상품명 = "이전 메뉴", 수량 = 3, 단가 = 1000 }];
        stale.총주문금액 = 3000;
        stale.수령인정보.수령인명 = "이전 주문자";

        var result = acceptanceResponse
            ? projection.ApplyAcceptedOrder("A", stale, 8)
            : projection.UpsertServerOrder(stale, 음식점주문복구출처.서버재조회);

        Assert.Same(selected, result);
        Assert.Same(selected, listReference);
        Assert.Same(latest, selected.상세주문);
        Assert.Same(products, selected.상품목록);
        Assert.Same(actions, selected.AvailableActions);
        Assert.Equal(음식점주문Desk상태코드.픽업대기, selected.상태);
        Assert.Equal(음식주문배차상태코드.기사배정, selected.배차상태);
        Assert.Equal("주문자 1", selected.고객명);
        Assert.Equal(9000, selected.주문금액);
        Assert.Equal(45, selected.선택조리예상분);
        Assert.Equal(2, selected.상세주문.CurrentPreparationRound);
        Assert.True(selected.현재픽업준비완료);
        Assert.Equal("재조리 준비 완료", RestaurantPosPresentation.StatusLabel(selected));
        Assert.Equal(음식점주문복구출처.재연결재조회, selected.복구출처);
        Assert.Equal(recoveryTime, selected.최근복구시각);
        Assert.Equal(acceptedAt, selected.수락시각);
        Assert.Equal(message, selected.최근메시지);
        Assert.Equal(12L, projection.GetExpectedRevision("A", 음식배달가능행동Ids.음식점조리시간변경));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameRevisionStillAppliesItsCanonicalRefreshAndPreparationValues(bool acceptanceResponse)
    {
        var projection = new 음식점주문InboxProjection(new object(), new Preparation());
        var original = projection.UpsertServerOrder(CanonicalOrder(12, 음식주문상태코드.주문확인, round: 1), 음식점주문복구출처.실시간);
        var refreshed = CanonicalOrder(12, 음식점주문Desk상태코드.기사배정, round: 2);
        refreshed.조리예상분 = 35;
        refreshed.배차상태 = 음식주문배차상태코드.기사배정;

        var result = acceptanceResponse
            ? projection.ApplyAcceptedOrder("A", refreshed, 35)
            : projection.UpsertServerOrder(refreshed, 음식점주문복구출처.서버재조회);

        Assert.Same(original, result);
        Assert.Same(refreshed, result.상세주문);
        Assert.Equal(음식점주문Desk상태코드.기사배정, result.상태);
        Assert.Equal(35, result.선택조리예상분);
        Assert.Equal(2, result.상세주문.CurrentPreparationRound);
        if (!acceptanceResponse) Assert.Equal(음식점주문복구출처.서버재조회, result.복구출처);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandCompletionKeepsActionsAndTargetLockedUntilParentRefreshFinishesEvenWhenItFails(bool refreshFails)
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var busyChanges = new List<bool>();
        var receiver = new object();
        var page = new OrderDetail
        {
            BusyChanged = EventCallback.Factory.Create<bool>(receiver, value => busyChanges.Add(value)),
            OrderUpdated = EventCallback.Factory.Create(receiver, async () =>
            {
                refreshStarted.TrySetResult();
                await finishRefresh.Task;
                if (refreshFails) throw new InvalidOperationException("controlled parent refresh failure");
            })
        };
        await InvokePageAsync(page, "BeginCommandAsync");
        var ending = InvokePageAsync(page, "EndCommandAsync");
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(PageProperty<bool>(page, "WorkDisabled"));
        Assert.Equal(new[] { true }, busyChanges);
        Assert.False(ending.IsCompleted);

        finishRefresh.SetResult();
        if (refreshFails) await Assert.ThrowsAsync<InvalidOperationException>(() => ending);
        else await ending;

        Assert.False(PageProperty<bool>(page, "WorkDisabled"));
        Assert.Equal(new[] { true, false }, busyChanges);
    }

    private static Task InvokePageAsync(OrderDetail page, string name)
        => (Task)typeof(OrderDetail).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;

    private static T PageProperty<T>(OrderDetail page, string name)
        => (T)typeof(OrderDetail).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;

    private static 음식주문응답 CanonicalOrder(long revision, string state, int round) => new()
    {
        주문번호 = "A", 음식점Id = 1, Revision = revision, 상태 = state,
        CreatedAt = ReceiptTime.UtcDateTime, CurrentPreparationRound = round,
        상품목록 = [new() { 상품명 = "메뉴 1", 수량 = 1, 단가 = 9000 }],
        수령인정보 = new() { 수령인명 = "주문자 1" }, 총주문금액 = 9000
    };

    private sealed class Preparation : I음식점조리시간설정Service
    {
        public 음식점조리시간설정Snapshot 현재조회() => new(20, new Dictionary<string, int>());
        public Task 저장Async(int 음식점기본조리분, IReadOnlyDictionary<string, int> 상품별기본조리분, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static readonly DateTimeOffset ReceiptTime = new(2026, 10, 22, 12, 0, 0, TimeSpan.FromHours(9));

    private static 음식점주문DeskItem Item(string orderNo, string state)
        => new() { 주문번호 = orderNo, 상태 = state, 접수시각 = ReceiptTime };
}
