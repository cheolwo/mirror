using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Services.Orderer;

namespace Ssalddel.Tests.Services.Orderer;

public sealed class 공동구매선적진행PolicyTests
{
    [Theory]
    [InlineData(공동구매선적상태코드.예외)]
    [InlineData("ProviderCustomCode")]
    public void 예외나_이전_미분류상태에서도_마지막정상단계를_보존한다(string current)
    {
        var now = DateTime.UtcNow;
        var last = 공동구매선적진행Policy.LastNormalState(current, null, [공동구매선적상태코드.국내기사상차]);
        Assert.False(공동구매선적진행Policy.CanAdvance(current, now, 공동구매선적상태코드.문서등록, now.AddMinutes(1), last));
        Assert.True(공동구매선적진행Policy.CanAdvance(current, now, 공동구매선적상태코드.완료, now.AddMinutes(1), last));
    }

    [Fact]
    public void 늦게_도착한_과거_이벤트는_현재단계를_바꾸지_않는다()
    {
        var now = DateTime.UtcNow;
        Assert.False(공동구매선적진행Policy.CanAdvance(공동구매선적상태코드.통관완료, now,
            공동구매선적상태코드.국내창고입고, now.AddMinutes(-1)));
    }

    [Theory]
    [InlineData(공동구매선적상태코드.통관진행중)]
    [InlineData(공동구매선적상태코드.예외)]
    [InlineData("ProviderCustomCode")]
    public void 완료는_새로운_조회시각의_과거단계나_예외로_되돌리지_않는다(string incoming)
    {
        var now = DateTime.UtcNow;
        Assert.False(공동구매선적진행Policy.CanAdvance(공동구매선적상태코드.완료, now, incoming, now.AddHours(1)));
    }

    [Fact]
    public void 통관_재조회가_국내_배송_진행을_되돌리지_않는다()
    {
        var now = DateTime.UtcNow;
        Assert.False(공동구매선적진행Policy.CanAdvance(공동구매선적상태코드.국내기사상차, now,
            공동구매선적상태코드.통관완료, now.AddHours(1)));
        Assert.True(공동구매선적진행Policy.CanAdvance(공동구매선적상태코드.국내기사상차, now,
            공동구매선적상태코드.완료, now.AddHours(1)));
    }
}
