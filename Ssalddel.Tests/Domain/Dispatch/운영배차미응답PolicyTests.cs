using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Domain.Dispatch;

public sealed class 운영배차미응답PolicyTests
{
    [Fact]
    public void 표시ACK없는_유효만료나_다른기사의표시로는_집계하지않는다()
    {
        var state = new 운영배차수신상태Dto { 주체Id = "driver" };
        var receipt = Event("receipt", 운영배차사건유형Code.제안표시확인, 1);
        receipt.주체Id = "other";
        운영배차미응답Policy.반영(state, [receipt, Event("expiry", 운영배차사건유형Code.무응답, 2)]);
        Assert.Equal(0, state.연속미응답횟수);
    }

    [Theory]
    [InlineData(운영배차제안유효성Code.전달실패)]
    [InlineData(운영배차제안유효성Code.조건부적합)]
    [InlineData(운영배차제안유효성Code.보호사유)]
    public void 실패나부적합으로분류된_제안은_미응답에포함하지않는다(string validity)
    {
        var state = new 운영배차수신상태Dto { 주체Id = "driver" };
        var expired = Event("expiry", 운영배차사건유형Code.무응답, 2);
        expired.제안유효성Code = validity;
        운영배차미응답Policy.반영(state, [Event("receipt", 운영배차사건유형Code.제안표시확인, 1), expired]);
        Assert.Equal(0, state.연속미응답횟수);
    }

    [Fact]
    public void 같은_제안과라운드는_사건ID가달라도_중복집계하지않는다()
    {
        var state = new 운영배차수신상태Dto { 주체Id = "driver" };
        운영배차미응답Policy.반영(state, [Event("receipt", 운영배차사건유형Code.제안표시확인, 1),
            Event("expiry-1", 운영배차사건유형Code.무응답, 2), Event("expiry-2", 운영배차사건유형Code.무응답, 3)]);
        Assert.Equal(1, state.연속미응답횟수);
    }

    [Fact]
    public void 의사변경과서버상태변경은_자동중지및횟수를보존한다()
    {
        var state = new 운영배차수신상태Dto { 주체Id = "driver", 미응답자동중지여부 = true,
            연속미응답횟수 = 3, 미응답자동중지시각Utc = DateTimeOffset.UnixEpoch };
        var policy = new 운영배차수신상태Policy();
        var on = policy.기사의사변경(state, "driver", 운영배차수신의사Code.On, DateTimeOffset.UtcNow);
        var eligible = policy.서버실효상태변경(on, 운영배차실효상태Code.배차가능, "Eligible", DateTimeOffset.UtcNow);
        Assert.True(eligible.미응답자동중지여부);
        Assert.Equal(3, eligible.연속미응답횟수);
        Assert.Equal(DateTimeOffset.UnixEpoch, eligible.미응답자동중지시각Utc);
        Assert.False(운영배차수신상태Policy.신규배차수신허용(eligible));
    }

    private static 운영배차활동사건Dto Event(string id, string type, int seconds) => new()
    {
        사건StableId = id, 발생시각Utc = DateTimeOffset.UnixEpoch.AddSeconds(seconds), 주체Id = "driver",
        주체역할Code = 운영배차주체역할Code.기사, 사건유형Code = type, 제안Id = "offer",
        제안유효성Code = 운영배차제안유효성Code.유효, 상태값Code = 운영배차미응답Code.라운드상태(1),
        사유Code = type == 운영배차사건유형Code.제안표시확인 ? 운영배차미응답Code.앱표시확인 : 운영배차미응답Code.표시제안만료
    };
}
