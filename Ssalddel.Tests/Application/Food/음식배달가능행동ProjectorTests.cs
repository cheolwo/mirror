using Ssalddel.Application.Food;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식배달가능행동ProjectorTests
{
    [Fact]
    public void 주문자_주문대기는_현재_revision의_취소만_제공한다()
    {
        var actions = 음식배달가능행동Projector.주문자용(음식주문상태코드.주문대기, 4);

        var action = Assert.Single(actions);
        Assert.Equal(음식배달가능행동Ids.주문취소, action.ActionId);
        Assert.Equal(업무Revision종류Codes.음식주문, action.RevisionKindCode);
        Assert.Equal(4, action.ExpectedRevision);
    }

    [Fact]
    public void 주문자_전달완료는_수령확인만_제공하고_종료상태는_비운다()
    {
        var delivered = 음식배달가능행동Projector.주문자용(음식주문상태코드.전달완료, 7);
        var completed = 음식배달가능행동Projector.주문자용(음식주문상태코드.수령확인, 8);

        Assert.Equal(음식배달가능행동Ids.주문수령확인, Assert.Single(delivered).ActionId);
        Assert.Null(delivered[0].ExpectedRevision);
        Assert.Empty(completed);
    }

    [Theory]
    [InlineData(음식주문상태코드.조리중)]
    [InlineData(음식주문상태코드.기사배정)]
    public void 음식점_조리와_기사배정은_조리시간변경과_픽업준비를_제공한다(string status)
    {
        var actions = 음식배달가능행동Projector.음식점용(status, 3);

        Assert.Equal(
            [음식배달가능행동Ids.음식점조리시간변경, 음식배달가능행동Ids.음식점픽업준비완료],
            actions.Select(action => action.ActionId));
        Assert.All(actions, action => Assert.Equal(3, action.ExpectedRevision));
    }

    [Fact]
    public void 기사_제안은_만료시각을_수락과_거절에_함께_제공한다()
    {
        var expiresAtUtc = new DateTime(2026, 9, 14, 4, 0, 0, DateTimeKind.Utc);

        var actions = 음식배달가능행동Projector.기사제안용(expiresAtUtc);

        Assert.Equal(
            [음식배달가능행동Ids.기사제안수락, 음식배달가능행동Ids.기사제안거절],
            actions.Select(action => action.ActionId));
        Assert.All(actions, action => Assert.Equal(expiresAtUtc, action.ExpiresAtUtc));
    }

    [Fact]
    public void 기사_픽업이동은_미도착이면_도착과_픽업을_제공하고_도착후에는_픽업만_제공한다()
    {
        var beforeArrival = 음식배달가능행동Projector.기사배달용(
            DriverWorkOfferStatus.MovingToPickup,
            2,
            null);
        var afterArrival = 음식배달가능행동Projector.기사배달용(
            DriverWorkOfferStatus.MovingToPickup,
            3,
            DateTime.UtcNow);

        Assert.Equal(
            [음식배달가능행동Ids.기사가게도착, 음식배달가능행동Ids.기사픽업확인],
            beforeArrival.Select(action => action.ActionId));
        Assert.Equal(2, beforeArrival[0].ExpectedRevision);
        Assert.Equal(음식배달가능행동Ids.기사픽업확인, Assert.Single(afterArrival).ActionId);
    }

    [Fact]
    public void 기사_전달이동은_전달완료만_제공한다()
    {
        var actions = 음식배달가능행동Projector.기사배달용(
            DriverWorkOfferStatus.MovingToDropoff,
            5,
            DateTime.UtcNow);

        Assert.Equal(음식배달가능행동Ids.기사전달완료, Assert.Single(actions).ActionId);
    }
}
