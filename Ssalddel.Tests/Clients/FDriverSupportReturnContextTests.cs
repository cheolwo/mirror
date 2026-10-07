using FDriverApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverSupportReturnContextTests
{
    [Fact]
    public void 재인증복귀는같은기사에게한번만주문번호를제공한다()
    {
        var context = new FDriverSupportReturnContext(); context.Capture("order-one", "driver"); context.Arm();
        Assert.Null(context.Take(null, false)); Assert.True(context.Pending); Assert.Null(context.Take("driver", false));
        Assert.Equal("order-one", context.Take("driver", true)); Assert.Null(context.Take("driver", true)); Assert.False(context.Pending);
    }
    [Fact]
    public void 다른계정로그인은이전기사의복귀대상을삭제한다()
    {
        var context = new FDriverSupportReturnContext(); context.Capture("private-order", "first"); context.Arm();
        Assert.Null(context.Take("second", true)); Assert.False(context.Pending); Assert.Null(context.Take("first", true));
    }
    [Fact]
    public void 복귀대상은15분이지나면폐기되고새배달선택은이전대상을바꾼다()
    {
        var clock = new Clock(); var context = new FDriverSupportReturnContext(clock); context.Capture("old", "driver"); context.Arm();
        clock.Now += TimeSpan.FromMinutes(15); Assert.Null(context.Take("driver", true)); Assert.False(context.Pending);
        context.Capture("old", "driver"); context.Arm(); context.Capture("new", "driver"); Assert.False(context.Pending);
        context.Arm(); Assert.Equal("new", context.Take("driver", true));
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
}
