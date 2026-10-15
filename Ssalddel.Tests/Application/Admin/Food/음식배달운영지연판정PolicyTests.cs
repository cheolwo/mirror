using Ssalddel.Application.Admin.Food;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Application.Admin.Food;

public sealed class 음식배달운영지연판정PolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(4, false, 음식배달운영지연상태Codes.없음)]
    [InlineData(5, false, 음식배달운영지연상태Codes.주의)]
    [InlineData(10, false, 음식배달운영지연상태Codes.운영자확인필요)]
    [InlineData(60, true, 음식배달운영지연상태Codes.없음)]
    public void 기사확보대기는_기존운영확인기준을따르고_유효배정이면해소된다(
        int minutes, bool assigned, string expected)
    {
        var result = 음식배달운영지연판정Policy.판정(new(음식주문상태코드.주문확인, Now,
            기사확보대기시작시각Utc: Now.AddMinutes(-minutes), 유효기사배정: assigned));
        Assert.Equal(expected, result.기사확보지연상태Code);
        Assert.Equal(assigned ? 0 : minutes, result.기사확보대기분);
    }

    [Theory]
    [InlineData(음식주문상태코드.주문대기)]
    [InlineData(음식주문상태코드.픽업완료)]
    [InlineData(음식주문상태코드.취소)]
    public void 배차대기외단계는_장기기사확보대기로오인하지않는다(string status)
    {
        var result = 음식배달운영지연판정Policy.판정(new(status, Now,
            기사확보대기시작시각Utc: Now.AddHours(-2)));
        Assert.Equal(음식배달운영지연상태Codes.없음, result.기사확보지연상태Code);
    }

    [Theory]
    [InlineData(4, 음식배달운영지연상태Codes.없음)]
    [InlineData(5, 음식배달운영지연상태Codes.주의)]
    [InlineData(9, 음식배달운영지연상태Codes.주의)]
    [InlineData(10, 음식배달운영지연상태Codes.운영자확인필요)]
    public void 음식점준비는_예상완료초과5분부터주의_10분부터운영자확인이다(
        int overdueMinutes,
        string expectedState)
    {
        var result = 음식배달운영지연판정Policy.판정(
            new 음식배달운영지연판정입력(
                음식주문상태코드.조리중,
                Now,
                조리예상완료시각Utc: Now.AddMinutes(-overdueMinutes)));

        Assert.Equal(expectedState, result.음식점준비지연상태Code);
        Assert.Equal(overdueMinutes, result.음식점준비초과분);
        Assert.Equal(음식배달운영지연상태Codes.없음, result.배달진행지연상태Code);
    }

    [Fact]
    public void 픽업준비가확정되면_과거예상시각을현재준비지연으로중복표시하지않는다()
    {
        var result = 음식배달운영지연판정Policy.판정(
            new 음식배달운영지연판정입력(
                음식주문상태코드.픽업대기,
                Now,
                조리예상완료시각Utc: Now.AddMinutes(-20),
                픽업준비시각Utc: Now.AddMinutes(-1)));

        Assert.Equal(음식배달운영지연상태Codes.없음, result.음식점준비지연상태Code);
        Assert.Equal(0, result.음식점준비초과분);
    }

    [Theory]
    [InlineData(46, 음식배달운영지연상태Codes.없음, 4)]
    [InlineData(47, 음식배달운영지연상태Codes.주의, 5)]
    [InlineData(52, 음식배달운영지연상태Codes.운영자확인필요, 10)]
    public void 픽업이후진행은_42분기준뒤5분부터주의_10분부터운영자확인이다(
        int minutesAfterPickup,
        string expectedState,
        int expectedOverdueMinutes)
    {
        var result = 음식배달운영지연판정Policy.판정(
            new 음식배달운영지연판정입력(
                음식주문상태코드.픽업완료,
                Now,
                픽업완료시각Utc: Now.AddMinutes(-minutesAfterPickup)));

        Assert.Equal(expectedState, result.배달진행지연상태Code);
        Assert.Equal(expectedOverdueMinutes, result.배달진행초과분);
        Assert.Equal(음식배달운영지연상태Codes.없음, result.음식점준비지연상태Code);
    }

    [Fact]
    public void 전달완료뒤에는_현재배달진행지연을표시하지않는다()
    {
        var result = 음식배달운영지연판정Policy.판정(
            new 음식배달운영지연판정입력(
                음식주문상태코드.전달완료,
                Now,
                픽업완료시각Utc: Now.AddMinutes(-80),
                전달완료시각Utc: Now.AddMinutes(-10)));

        Assert.Equal(음식배달운영지연상태Codes.없음, result.배달진행지연상태Code);
        Assert.Equal(0, result.배달진행초과분);
    }
}
