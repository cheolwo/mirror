using DriverApp.Models.Driver;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;

namespace Ssalddel.Tests.Clients.Cargo;

public sealed class DriverRecommendationSelectionR69Tests
{
    private static readonly DateTime Now = new(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TwoOffers_ProduceTwoDistinctCards_WithoutSelectingBothForAcceptance()
    {
        var first = Offer("A"); var second = Offer("B");
        var state = new DriverRecommendationSelectionState();
        state.Refresh([first, second], Now);
        Assert.Equal(2, state.Items.Count);
        Assert.Same(first, state.Selected);
        Assert.True(state.Select("B", 1));
        Assert.Same(second, state.Selected);
        Assert.Equal(1, state.SelectedIndex);
        Assert.All(state.Items, item => Assert.Equal("배차대기", item.배차상태));
    }

    [Fact]
    public void ReorderedCanonicalList_KeepsSelectionByRequestAndRound()
    {
        var state = new DriverRecommendationSelectionState();
        state.Refresh([Offer("A"), Offer("B")], Now);
        state.Select("B", 1);
        var refreshed = Offer("B"); refreshed.픽업지 = "새로 확인한 픽업지";
        state.Refresh([refreshed, Offer("C"), Offer("A")], Now.AddSeconds(1));
        Assert.Same(refreshed, state.Selected);
        Assert.Equal(0, state.SelectedIndex);
        Assert.Equal("새로 확인한 픽업지", state.Selected!.픽업지);
    }

    [Fact]
    public void RemovingSelectedCard_SelectsNextValidCardAtTheSamePosition()
    {
        var state = new DriverRecommendationSelectionState();
        state.Refresh([Offer("A"), Offer("B"), Offer("C")], Now);
        state.Select("B", 1);
        state.Refresh([Offer("A"), Offer("C")], Now);
        Assert.Equal("C", state.Selected!.의뢰Id);
        state.Refresh([Offer("A")], Now);
        Assert.Equal("A", state.Selected!.의뢰Id);
        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void CapturedCommandKey_CannotSwitchTargetsWhenUserSwipes()
    {
        var state = new DriverRecommendationSelectionState();
        var first = Offer("A");
        state.Refresh([first, Offer("B")], Now);
        var commandKey = state.SelectedKey!;
        state.Select("B", 1);
        Assert.Same(first, state.FindLive(commandKey, Now.AddSeconds(1)));
        Assert.Equal("B", state.Selected!.의뢰Id);
    }

    [Fact]
    public void NewRoundForSameRequest_CannotSatisfyAnOlderCapturedCommand()
    {
        var state = new DriverRecommendationSelectionState();
        state.Refresh([Offer("A", 1)], Now);
        var oldKey = state.SelectedKey!;
        state.Refresh([Offer("A", 2)], Now.AddSeconds(1));
        Assert.Null(state.FindLive(oldKey, Now.AddSeconds(1)));
        Assert.False(state.Select("A", 1));
        Assert.Equal(2, state.SelectedKey!.Round);
    }

    [Fact]
    public void ExpiredCard_CannotExecuteEvenBeforeTheNextListRefresh()
    {
        var state = new DriverRecommendationSelectionState();
        state.Refresh([Offer("A")], Now);
        var key = state.SelectedKey!;
        Assert.NotNull(state.FindLive(key, Now.AddSeconds(59)));
        Assert.Null(state.FindLive(key, Now.AddSeconds(60)));
        state.Refresh(state.Items, Now.AddSeconds(60));
        Assert.Empty(state.Items);
        Assert.Null(state.SelectedKey);
        Assert.Null(state.Selected);
        Assert.Equal(-1, state.SelectedIndex);
    }

    [Fact]
    public void MissingCoordinates_DoNotHideAnOtherwiseValidRecommendation()
    {
        var item = Offer("A");
        item.픽업_위도 = item.픽업_경도 = item.하차_위도 = item.하차_경도 = null;
        var state = new DriverRecommendationSelectionState();
        state.Refresh([item], Now);
        Assert.Same(item, Assert.Single(state.Items));
        Assert.Null(state.Selected!.픽업_위도);
        Assert.Null(state.Selected.하차_위도);
    }

    [Fact]
    public void InvalidOrNotYetStartedServerOffers_AreNotActionableCards()
    {
        var publicCall = Offer("public"); publicCall.추천시작시각 = publicCall.추천만료시각 = null;
        var future = Offer("future"); future.추천시작시각 = Now.AddSeconds(1);
        var expired = Offer("expired"); expired.추천만료시각 = Now;
        var state = new DriverRecommendationSelectionState();
        state.Refresh([publicCall, future, expired, Offer(""), Offer("round-zero", 0), Offer("live")], Now);
        Assert.Equal("live", Assert.Single(state.Items).의뢰Id);
        Assert.False(state.Select("future", 1));
        Assert.Equal("live", state.Selected!.의뢰Id);
    }

    [Fact]
    public void ServerUtcWithoutZoneSuffix_IsNotReinterpretedAsThePhonesLocalTime()
    {
        var item = Offer("server-utc");
        item.추천시작시각 = DateTime.SpecifyKind(item.추천시작시각!.Value, DateTimeKind.Unspecified);
        item.추천만료시각 = DateTime.SpecifyKind(item.추천만료시각!.Value, DateTimeKind.Unspecified);
        Assert.True(DriverRecommendationSelectionState.IsLive(item, Now));
        var state = new DriverRecommendationSelectionState();
        state.Refresh([item], Now);
        Assert.Same(item, Assert.Single(state.Items));
        Assert.Null(state.FindLive(state.SelectedKey!, Now.AddSeconds(60)));
    }

    [Fact]
    public void DuplicateSnapshotOfSameRound_DoesNotAddAnotherCard()
    {
        var state = new DriverRecommendationSelectionState();
        var item = Offer("A");
        state.Refresh([item, item, Offer("A"), Offer("B")], Now);
        Assert.Equal(new[] { "A", "B" }, state.Items.Select(x => x.의뢰Id));
    }

    [Fact]
    public void DriverAlreadyAtPickup_KeepsTheDeliveryLegWithoutAZeroLengthPickupLeg()
    {
        var routes = new DriverHomeRoutePlanningService().BuildRecommendationReviewRouteOverlays(
            new("현재 위치", 37.588m, 127.085m, Now), null, Marker());
        var delivery = Assert.Single(routes);
        Assert.Equal("#2563eb", delivery.StrokeColor);
        Assert.Equal(37.588, delivery.Points[0].Latitude);
        Assert.Equal(37.590, delivery.Points[1].Latitude);
    }

    [Fact]
    public void CurrentWorkEndsAtRecommendedPickup_KeepsBothIndependentLegsAndCurrentTask()
    {
        var current = new 기사운송샘플항목(1, "current-A", "상자", "현재 픽업", "현재 도착",
            37.580m, 127.080m, 37.588m, 127.085m, "하차 이동", Now, 1, 6000,
            false, false, "별도정산", "하차 확인");
        var routes = new DriverHomeRoutePlanningService().BuildRecommendationReviewRouteOverlays(
            new("현재 위치", 37.581m, 127.081m, Now), current, Marker());
        Assert.Equal(2, routes.Count);
        Assert.Equal("#f97316", routes[0].StrokeColor);
        Assert.Equal("#2563eb", routes[1].StrokeColor);
        Assert.Equal(routes[0].Points[^1].Latitude, routes[1].Points[0].Latitude);
        Assert.Equal(37.590, routes[1].Points[^1].Latitude);
        Assert.Equal("current-A", current.의뢰Id);
        Assert.Equal("하차 이동", current.현재단계);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(91)]
    public void InvalidPickup_DoesNotCreateAnInventedRoute(double latitude)
    {
        var routes = new DriverHomeRoutePlanningService().BuildRecommendationReviewRouteOverlays(
            new("현재 위치", 37.581m, 127.081m, Now), null, Marker() with { PickupLatitude = latitude });
        Assert.Empty(routes);
    }

    [Fact]
    public void MissingDropoff_KeepsOnlyTheKnownPickupLeg()
    {
        var routes = new DriverHomeRoutePlanningService().BuildRecommendationReviewRouteOverlays(
            new("현재 위치", 37.581m, 127.081m, Now), null, Marker() with { DropoffLatitude = 0, DropoffLongitude = 0 });
        Assert.Equal("#f97316", Assert.Single(routes).StrokeColor);
    }

    [Fact]
    public void MissingCurrentPosition_DoesNotHideKnownPickupAndDropoff()
    {
        var routes = new DriverHomeRoutePlanningService().BuildRecommendationReviewRouteOverlays(
            new("미확인", 0, 0, Now), null, Marker());
        Assert.Equal("#2563eb", Assert.Single(routes).StrokeColor);
    }

    private static DriverMapMarkerItem Marker() => new("B", 37.588, 127.085, 37.590, 127.087,
        "새 추천", "경로 확인", "픽업 장소", "전달 장소");

    private static DriverRequestItem Offer(string id, int round = 1) => new()
    {
        의뢰Id = id, 추천라운드 = round, 추천시작시각 = Now.AddSeconds(-10),
        추천만료시각 = Now.AddSeconds(60), 배차상태 = "배차대기", 픽업지 = "픽업 장소", 하차지 = "전달 장소"
    };
}
