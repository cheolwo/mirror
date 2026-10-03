using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Domain.Dispatch;

public sealed class 운영배차수신상태PolicyTests
{
    private readonly 운영배차수신상태Policy _policy = new();

    [Fact]
    public void 초기_OFF와_ON_값만으로는_수신을_허용하지_않는다()
    {
        Assert.False(운영배차수신상태Policy.신규배차수신허용(new()));
        Assert.False(운영배차수신상태Policy.신규배차수신허용(new() { 수신의사Code = 운영배차수신의사Code.On }));
    }

    [Fact]
    public void 명시_ON과_실효판정_부재는_운행_위치_검사로_인계한다()
    {
        var 시각 = new DateTimeOffset(2026, 10, 3, 1, 0, 0, TimeSpan.Zero);
        var on = _policy.기사의사변경(null, "driver-1", 운영배차수신의사Code.On, 시각);
        Assert.True(운영배차수신상태Policy.신규배차수신허용(on));
        Assert.Equal(운영배차실효상태Code.조건부적합, on.실효상태Code);
        Assert.Null(on.실효상태변경시각Utc);
        Assert.Equal(시각, on.수신의사변경시각Utc);
    }

    [Theory]
    [InlineData(운영배차실효상태Code.서버일시정지)]
    [InlineData(운영배차실효상태Code.조건부적합)]
    [InlineData(운영배차실효상태Code.연결확인불가)]
    public void 기록된_서버_제한은_ON_의사를_보존하며_새_배차만_막는다(string 실효Code)
    {
        var 시각 = new DateTimeOffset(2026, 10, 3, 1, 0, 0, TimeSpan.Zero);
        var on = _policy.기사의사변경(null, "driver-1", 운영배차수신의사Code.On, 시각);
        var 제한 = _policy.서버실효상태변경(on, 실효Code, "ServerObserved", 시각.AddMinutes(1));
        Assert.False(운영배차수신상태Policy.신규배차수신허용(제한));
        Assert.Equal(운영배차수신의사Code.On, 제한.수신의사Code);
        Assert.Equal(on.수신의사변경시각Utc, 제한.수신의사변경시각Utc);
        Assert.Equal(실효Code, 제한.실효상태Code);
    }

    [Fact]
    public void 기록된_서버_제한을_Eligible로_재개하면_명시_ON만_허용한다()
    {
        var 시각 = new DateTimeOffset(2026, 10, 3, 1, 0, 0, TimeSpan.Zero);
        var on = _policy.기사의사변경(null, "driver-1", 운영배차수신의사Code.On, 시각);
        var 제한 = _policy.서버실효상태변경(on, 운영배차실효상태Code.서버일시정지, "Paused", 시각.AddMinutes(1));
        var 재개 = _policy.서버실효상태변경(제한, 운영배차실효상태Code.배차가능, "Resumed", 시각.AddMinutes(2));
        Assert.True(운영배차수신상태Policy.신규배차수신허용(재개));
        var off = _policy.기사의사변경(재개, "driver-1", 운영배차수신의사Code.Off, 시각.AddMinutes(3));
        Assert.False(운영배차수신상태Policy.신규배차수신허용(off));
        Assert.Equal(운영배차실효상태Code.배차가능, off.실효상태Code);
    }

    [Fact]
    public void 서버_일시정지는_판정시각_누락이어도_새_배차를_허용하지_않는다()
    {
        var on = _policy.기사의사변경(null, "driver-1", 운영배차수신의사Code.On, DateTimeOffset.UtcNow);
        on.실효상태Code = 운영배차실효상태Code.서버일시정지;
        Assert.False(운영배차수신상태Policy.신규배차수신허용(on));
    }

    [Fact]
    public void 연결확인불가가_되어도_기사의_ON_의사를_보존한다()
    {
        var 시작 = new DateTimeOffset(2026, 9, 10, 1, 0, 0, TimeSpan.Zero);
        var on = _policy.기사의사변경(null, "driver-1", 운영배차수신의사Code.On, 시작);

        var 결과 = _policy.서버실효상태변경(
            on,
            운영배차실효상태Code.연결확인불가,
            "HeartbeatUnavailable",
            시작.AddMinutes(3));

        Assert.Equal(운영배차수신의사Code.On, 결과.수신의사Code);
        Assert.Equal(운영배차실효상태Code.연결확인불가, 결과.실효상태Code);
        Assert.Equal(on.수신의사변경시각Utc, 결과.수신의사변경시각Utc);
    }

    [Fact]
    public void OFF는_기사의_명시적_의사변경으로_기록한다()
    {
        var 시작 = new DateTimeOffset(2026, 9, 10, 1, 0, 0, TimeSpan.Zero);
        var on = _policy.기사의사변경(null, "driver-1", 운영배차수신의사Code.On, 시작);
        var 일시정지 = _policy.서버실효상태변경(
            on,
            운영배차실효상태Code.서버일시정지,
            "ServerMaintenance",
            시작.AddMinutes(1));

        var 결과 = _policy.기사의사변경(
            일시정지,
            "driver-1",
            운영배차수신의사Code.Off,
            시작.AddMinutes(2));

        Assert.Equal(운영배차수신의사Code.Off, 결과.수신의사Code);
        Assert.Equal(운영배차실효상태Code.서버일시정지, 결과.실효상태Code);
        Assert.Equal(시작.AddMinutes(2), 결과.수신의사변경시각Utc);
    }

    [Fact]
    public void 다른_기사의_현재상태를_바꾸려면_거부한다()
    {
        var 기준시각 = new DateTimeOffset(2026, 9, 10, 1, 0, 0, TimeSpan.Zero);
        var 현재 = _policy.기사의사변경(
            null,
            "driver-1",
            운영배차수신의사Code.On,
            기준시각);

        Assert.Throws<InvalidOperationException>(() =>
            _policy.기사의사변경(
                현재,
                "driver-2",
                운영배차수신의사Code.Off,
                기준시각.AddMinutes(1)));
    }
}
