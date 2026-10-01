using 홍달.Services.Dispatch.Queue;

namespace Hongdal.Tests.Services.Dispatch.Queue;

public sealed class 기사위치신선도정책Tests
{
    private static readonly DateTime Now = new(2026, 7, 17, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void 수신시각이_없으면_유효하지_않다()
    {
        Assert.False(기사위치신선도정책.유효한가(null, Now, 10));
    }

    [Theory]
    [InlineData(-10, true)]
    [InlineData(-11, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void 설정된_유효시간으로_위치_신선도를_판정한다(int receivedMinutes, bool expected)
    {
        var result = 기사위치신선도정책.유효한가(
            Now.AddMinutes(receivedMinutes),
            Now,
            10);

        Assert.Equal(expected, result);
    }
}
