using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

public sealed class CargoTransportKstDisplayTests
{
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void 서버UTC와SQL시각은_한국날짜경계를포함해같은국내시각으로표시한다(DateTimeKind kind)
    {
        var serverTime = new DateTime(2026, 9, 30, 18, 15, 0, kind);

        Assert.Equal("10-01 03:15 (한국시간)", 기사국내시각표시.표시(serverTime));
        Assert.Equal(TimeSpan.FromHours(9), 기사국내시각표시.한국시간(serverTime).Offset);
        Assert.Equal(serverTime.Ticks, 기사국내시각표시.한국시간(serverTime).UtcDateTime.Ticks);
    }

    [Fact]
    public void 기기로컬Kind는_같은순간을한국시간으로표시하며미확인은만들지않는다()
    {
        var utc = new DateTime(2026, 9, 30, 18, 15, 0, DateTimeKind.Utc);
        Assert.Equal(기사국내시각표시.표시(utc), 기사국내시각표시.표시(utc.ToLocalTime()));
        Assert.Equal("시각 미확인", 기사국내시각표시.표시(null));
        Assert.Equal("예약 시각 미확인", 기사국내시각표시.표시(null, fallback: "예약 시각 미확인"));
    }

    [Fact]
    public void 기존진행운송표시사본의예정시각도_UTC를직접표시하지않는다()
    {
        var utc = new DateTime(2026, 9, 30, 18, 15, 0, DateTimeKind.Utc);
        var summary = new 기사운송요약응답 { Id = 11, 출발_픽업 = utc, UpdatedAt = utc.AddHours(1) };

        var display = 기사운송표시Mapper.Map(summary);

        Assert.Equal(new DateTime(2026, 10, 1, 3, 15, 0), display.예정시각);
        Assert.Equal(utc, summary.출발_픽업);
        Assert.Equal(utc.AddHours(1), summary.UpdatedAt);
    }
}
