using DriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;

namespace Ssalddel.Tests.Clients;

public sealed partial class CargoWorkRecoveryR75Tests
{
    [Fact]
    public async Task R64_새운행확정후에만최초수신ON을보내고미응답중지는해제하지않는다()
    {
        var reception = new Reception { State = ReceptionState(paused: true) };
        using var f = new Fixture(reception);
        var result = await f.Start();
        Assert.True(result.ServerConfirmed); Assert.True(result.LocationStarted);
        var sent = Assert.Single(reception.Writes);
        Assert.Equal(운영배차수신의사Code.On, sent.수신의사Code);
        Assert.False(sent.미응답자동중지해제);
        Assert.True(sent.최초On조건부적용);
        Assert.Null(sent.예상수신의사판본Token);
        Assert.NotEqual(Guid.Empty, sent.클라이언트요청Id);
        Assert.True(reception.State.미응답자동중지여부);
        Assert.Null(f.Coordinator.Pending);
    }

    [Fact]
    public async Task R64_기존운행복귀는수신OFF를자동으로켜지않는다()
    {
        var reception = new Reception();
        using var f = new Fixture(reception) { AlreadyRunning = true };
        Assert.True((await f.Start()).LocationStarted);
        Assert.Equal(0, f.Posts); Assert.Empty(reception.Writes); Assert.Equal(0, reception.Reads);
    }

    [Fact]
    public async Task R64_시작중기사의명시OFF가생기면판본비교로최신의사를보존한다()
    {
        var reception = new Reception();
        using var f = new Fixture(reception)
        {
            AfterPost = () => { reception.State.수신의사변경시각Utc = DateTimeOffset.UtcNow.AddDays(-1); reception.State.수신의사판본Token = "manual-off"; }
        };
        // 클라이언트 시계보다 오래된 서버 시각이어도 초기 관측과 달라졌으면 새 의사다.
        Assert.True((await f.Start()).LocationStarted);
        Assert.Empty(reception.Writes);
        Assert.Equal(운영배차수신의사Code.Off, reception.State.수신의사Code);
        Assert.Null(f.Coordinator.Pending);
    }

    [Fact]
    public async Task R64_최종조회후같은시각의수동OFF도조건부ON이덮어쓰지않는다()
    {
        var reception = new Reception { ManualOffBeforeWrite = true };
        using var f = new Fixture(reception);
        Assert.True((await f.Start()).LocationStarted);
        Assert.True(Assert.Single(reception.Writes).최초On조건부적용);
        Assert.Equal(운영배차수신의사Code.Off, reception.State.수신의사Code);
        Assert.Equal("manual-off", reception.State.수신의사판본Token);
        Assert.Null(f.Coordinator.Pending);
    }

    [Fact]
    public async Task R64_수신응답미확인은같은ON요청을보존하며운행POST를반복하지않는다()
    {
        var reception = new Reception { LoseFirstWrite = true };
        using var f = new Fixture(reception);
        var first = await f.Start();
        Assert.True(first.ServerConfirmed); Assert.True(first.LocationStarted);
        Assert.Equal("ConfirmedReceptionPending", f.Coordinator.ResolutionState);
        Assert.NotNull(f.Coordinator.Pending);
        var original = Assert.Single(reception.Writes).클라이언트요청Id;
        Assert.True((await f.Start()).LocationStarted);
        Assert.Equal(1, f.Posts); Assert.Equal(2, reception.Writes.Count);
        Assert.All(reception.Writes, x => Assert.Equal(original, x.클라이언트요청Id));
        Assert.All(reception.Writes, x => Assert.False(x.미응답자동중지해제));
        Assert.Null(f.Coordinator.Pending);
    }

    [Fact]
    public async Task R64_수신ON저장후응답유실은정본으로확인하고재전송하지않는다()
    {
        var reception = new Reception { LoseFirstWrite = true, ApplyBeforeLoss = true };
        using var f = new Fixture(reception);
        Assert.True((await f.Start()).LocationStarted);
        Assert.Single(reception.Writes); Assert.Null(f.Coordinator.Pending);
    }

    [Fact]
    public async Task R64_시작전수신조회실패는운행을허위실패시키거나자동ON근거로사용하지않는다()
    {
        var reception = new Reception { FailFirstRead = true };
        using var f = new Fixture(reception);
        var result = await f.Start();
        Assert.True(result.ServerConfirmed); Assert.True(result.LocationStarted);
        Assert.Empty(reception.Writes); Assert.Contains("토글", result.Message);
        Assert.Null(f.Coordinator.Pending);
    }

    [Fact]
    public async Task R64_다른기사의수신응답으로ON을보내지않는다()
    {
        var reception = new Reception { State = new() { 주체Id = "another-driver" } };
        using var f = new Fixture(reception);
        Assert.True((await f.Start()).LocationStarted);
        Assert.Empty(reception.Writes);
    }

    [Fact]
    public async Task R64_원래시작이거절되거나일치하지않으면수신ON도보내지않는다()
    {
        var reception = new Reception();
        using var f = new Fixture(reception) { Mismatch = "request" };
        Assert.False((await f.Start()).ServerConfirmed);
        Assert.Empty(reception.Writes);
    }

    private static 운영배차수신상태Dto ReceptionState(bool paused = false) => new()
    {
        주체Id = "driver-r75", 수신의사Code = 운영배차수신의사Code.Off,
        미응답자동중지여부 = paused, 연속미응답횟수 = paused ? 3 : 0
    };

    private sealed class Reception : IDriverOperationalDispatchApiService
    {
        public 운영배차수신상태Dto State = ReceptionState();
        public readonly List<운영배차수신의사변경요청> Writes = [];
        public int Reads;
        public bool FailFirstRead, LoseFirstWrite, ApplyBeforeLoss, ManualOffBeforeWrite;
        public Task<운영배차수신상태Dto> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); Reads++;
            if (FailFirstRead && Reads == 1) throw new HttpRequestException("Unavailable");
            return Task.FromResult(State);
        }
        public Task<운영배차수신상태Dto> ChangeIntentAsync(운영배차수신의사변경요청 request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); Writes.Add(request);
            if (ManualOffBeforeWrite) { State.수신의사Code = 운영배차수신의사Code.Off; State.수신의사판본Token = "manual-off"; }
            if (request.최초On조건부적용 && request.예상수신의사판본Token != State.수신의사판본Token)
                return Task.FromResult(State);
            var lose = LoseFirstWrite && Writes.Count == 1;
            if (!lose || ApplyBeforeLoss)
            {
                State.수신의사Code = request.수신의사Code;
                State.수신의사변경시각Utc = DateTimeOffset.UtcNow;
                State.수신의사판본Token = request.클라이언트요청Id.ToString("N");
                if (request.미응답자동중지해제) State.미응답자동중지여부 = false;
            }
            if (lose) throw new HttpRequestException("ResponseLost");
            return Task.FromResult(State);
        }
        public Task AcknowledgeOfferPresentationAsync(string requestId, int recommendationRound,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
