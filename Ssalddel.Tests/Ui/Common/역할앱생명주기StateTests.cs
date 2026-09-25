using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 역할앱생명주기StateTests
{
    [Fact]
    public void 활성온라인과오프라인을_사용자상태로구분한다()
    {
        var state = new 역할앱생명주기State();
        state.초기화("Windows", connected: true);
        state.전환(역할앱생명주기단계.활성);

        Assert.Equal("앱 활성", state.사용자안내);
        Assert.Equal("ready", state.상태Code);

        state.연결상태변경(false);

        Assert.Equal("네트워크 없음", state.사용자안내);
        Assert.Equal("offline", state.상태Code);
    }

    [Fact]
    public void 중단과재개를_업무완료로오인하지않는별도상태로유지한다()
    {
        var state = new 역할앱생명주기State();
        state.초기화("Android", connected: true);

        state.전환(역할앱생명주기단계.일시정지);
        Assert.Equal("잠시 멈춤", state.사용자안내);
        Assert.Equal("paused", state.상태Code);

        state.전환(역할앱생명주기단계.재개중);
        Assert.Equal("다시 여는 중", state.사용자안내);
        Assert.Equal("resuming", state.상태Code);
    }

    [Fact]
    public async Task 재개안내는_짧게표시한뒤_활성상태로복귀한다()
    {
        var state = new 역할앱생명주기State();
        state.초기화("Windows", connected: true);

        await state.재개표시후활성Async(TimeSpan.Zero);

        Assert.Equal(역할앱생명주기단계.활성, state.단계);
        Assert.Equal("앱 활성", state.사용자안내);
    }
}
