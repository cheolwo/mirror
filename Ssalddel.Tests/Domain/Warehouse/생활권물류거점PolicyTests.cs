using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Domain.Warehouse;

public sealed class 생활권물류거점PolicyTests
{
    [Fact]
    public void Pilot은_검토상태에서만_진입한다()
    {
        Assert.True(생활권물류거점Policy.CanTransition(생활권물류거점상태Codes.UnderReview, 생활권물류거점상태Codes.Pilot));
        Assert.False(생활권물류거점Policy.CanTransition(생활권물류거점상태Codes.Candidate, 생활권물류거점상태Codes.Pilot));
        Assert.False(생활권물류거점Policy.CanTransition(생활권물류거점상태Codes.Active, 생활권물류거점상태Codes.Closed));
    }

    [Fact]
    public void 준비판정은_세가지승인과_현장확인_보호위치_상온밀봉_용량을_요구한다()
    {
        var hub = ReadyHub();
        Assert.Null(생활권물류거점Policy.GetReadinessError(hub));

        hub.관리주체동의 = false;
        Assert.Contains("관리주체", 생활권물류거점Policy.GetReadinessError(hub));

        hub = ReadyHub();
        hub.상온밀봉품만허용 = false;
        Assert.Contains("상온 밀봉품", 생활권물류거점Policy.GetReadinessError(hub));
    }

    [Fact]
    public void 주거공간은_첫Pilot에서_거부한다()
    {
        var hub = ReadyHub();
        hub.공간유형Code = "ResidentialRoom";

        Assert.Contains("비주거", 생활권물류거점Policy.GetReadinessError(hub));
    }

    private static 생활권물류거점 ReadyHub() => new()
    {
        신청가원장Id = "ledger-1",
        공간유형Code = 생활권물류거점공간유형Codes.독립비주거공간,
        정확위치보호참조 = "protected-place:1",
        소유자동의 = true,
        관리주체동의 = true,
        플랫폼승인 = true,
        현장확인시각Utc = DateTime.UtcNow,
        단기보관가능 = true,
        기사인계가능 = true,
        상온밀봉품만허용 = true,
        최대동시보관건수 = 3,
        최대총중량Kg = 30,
        최대보관시간분 = 240
    };
}
