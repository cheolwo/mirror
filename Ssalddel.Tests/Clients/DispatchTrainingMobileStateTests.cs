using System.Net;
using Ssalddel.Contracts.Admin.DispatchTraining;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelAdminApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class DispatchTrainingMobileStateTests
{
    [Fact]
    public async Task 미지원호스트에서는_인증과업무조회를_시작하지않는다()
    {
        var fixture = new Fixture(); fixture.Client.Enabled = false;
        using var state = fixture.State(); await state.InitializeAsync();
        Assert.True(state.CapabilityChecked); Assert.False(state.Enabled); Assert.Equal(0, fixture.Identity.EnsureCalls);
        Assert.Equal(0, fixture.Client.ReadCalls); Assert.Empty(fixture.Client.Commands);
    }
    [Fact]
    public async Task 익명은_capability만조회하고_훈련명령을_실행하지않는다()
    {
        var fixture = new Fixture(); fixture.Identity.Admin = false; fixture.Identity.User = null;
        using var state = fixture.State(); await state.InitializeAsync(); await state.ExecuteAsync("start");
        Assert.True(state.RequiresLogin); Assert.Equal(0, fixture.Client.ReadCalls); Assert.Empty(fixture.Client.Commands);
    }
    [Fact]
    public async Task 허용행동과다른명령은_상태만보고_보내지않는다()
    {
        var fixture = new Fixture(); using var state = fixture.State(); await state.InitializeAsync();
        await state.ExecuteAsync("resume"); await state.ExecuteAsync("unknown"); Assert.Empty(fixture.Client.Commands);
        await state.ExecuteAsync("start");
        var command = Assert.Single(fixture.Client.Commands);
        Assert.Equal("start", command.Action); Assert.Equal(0, command.Request.ExpectedRevision); Assert.Null(command.Request.RunId);
        Assert.False(state.HasPending); Assert.Equal("run-1", state.Session?.RunId); Assert.True(fixture.Client.ReadCalls >= 2);
    }
    [Fact]
    public async Task 응답유실후_같은요청번호정본조회만하며_자동재전송하지않는다()
    {
        var fixture = new Fixture(); fixture.Client.Send = (_, _) => throw new IOException("private token");
        var pending = new DispatchTrainingPendingSession();
        using (var first = fixture.State(pending))
        {
            await first.InitializeAsync(); await first.ExecuteAsync("start"); Assert.True(first.HasPending);
            Assert.DoesNotContain("private token", first.Error ?? "");
        }
        using var next = fixture.State(pending); await next.InitializeAsync(); await next.ExecuteAsync("start");
        Assert.Single(fixture.Client.Commands); Assert.True(next.HasPending);
        var original = fixture.Client.Commands[0].Request.ClientRequestId;
        fixture.Client.Snapshot = Running() with { Revision = 1, AppliedCommandIds = [original] };
        await next.RefreshAsync(); Assert.False(next.HasPending); Assert.Equal("run-1", next.Session?.RunId); Assert.Single(fixture.Client.Commands);
    }
    [Fact]
    public async Task 계정전환뒤_늦은정본은_다른관리자에게표시하지않는다()
    {
        var delayed = new TaskCompletionSource<DispatchTrainingSessionDto>(); var fixture = new Fixture();
        fixture.Client.Read = () => delayed.Task; using var state = fixture.State(); var loading = state.InitializeAsync();
        fixture.Identity.User = "another-admin"; fixture.Identity.Notify();
        delayed.SetResult(Running()); await loading;
        Assert.Null(state.Session); Assert.False(state.IsBusy); Assert.False(state.Loaded);
    }
    [Fact]
    public async Task 백그라운드에서는_늦은응답을버리고_새GET을시작하지않는다()
    {
        var fixture = new Fixture(); using var state = fixture.State(); await state.InitializeAsync();
        var delayed = new TaskCompletionSource<DispatchTrainingSessionDto>(); fixture.Client.Read = () => delayed.Task;
        var reading = state.RefreshAsync(); var before = fixture.Client.ReadCalls;
        fixture.Lifecycle.전환(역할앱생명주기단계.일시정지); await state.RefreshAsync();
        delayed.SetResult(Running()); await reading;
        Assert.Equal(before, fixture.Client.ReadCalls); Assert.Equal(DispatchTrainingPhaseCodes.Idle, state.Session?.PhaseCode);
        fixture.Client.Read = null; fixture.Client.Snapshot = Running(); fixture.Lifecycle.전환(역할앱생명주기단계.활성);
        await state.RefreshAsync(); Assert.Equal("run-1", state.Session?.RunId);
    }
    [Fact]
    public async Task 불확실명령뒤_로그아웃은_내역과요청번호를비운다()
    {
        var fixture = new Fixture(); fixture.Client.Send = (_, _) => throw new IOException(); var pending = new DispatchTrainingPendingSession();
        using var state = fixture.State(pending); await state.InitializeAsync(); await state.ExecuteAsync("start");
        fixture.Identity.Admin = false; fixture.Identity.User = null; fixture.Identity.Notify();
        Assert.Null(state.Session); Assert.Null(pending.Request); Assert.Empty(state.Decisions);
    }
    [Fact]
    public async Task 지원기사는_현재회차와revision에묶어_한명만참여요청한다()
    {
        var fixture = new Fixture(); fixture.Client.Snapshot = Running(); using var state = fixture.State(); await state.InitializeAsync();
        await state.ExecuteAsync("support-drivers");
        var command = Assert.Single(fixture.Client.Commands); var request = Assert.IsType<DispatchTrainingSupportDriversRequest>(command.Request);
        Assert.Equal(1, request.Count); Assert.Equal("run-1", request.RunId); Assert.Equal(4, request.ExpectedRevision);
    }
    [Fact]
    public async Task 다른회차판단응답은_이전판단을다시표시하지않는다()
    {
        var fixture = new Fixture(); fixture.Client.Snapshot = Running();
        fixture.Client.Decisions = new() { RunId = "wrong-run", Items = [new() { Summary = "wrong decision" }] };
        using var state = fixture.State(); await state.InitializeAsync(true);
        Assert.Empty(state.Decisions); Assert.NotNull(state.Error);
    }
    [Fact]
    public async Task 동시명령경합의명확한거절은_새로조회전까지_행동을차단한다()
    {
        var fixture = new Fixture(); fixture.Client.Send = (_, _) => throw new AdminApiException("raw", HttpStatusCode.Conflict);
        using var state = fixture.State(); await state.InitializeAsync(); await state.ExecuteAsync("start");
        Assert.False(state.HasPending); Assert.NotNull(state.Error); Assert.False(state.CanCommand("start"));
        await state.RefreshAsync(); Assert.True(state.CanCommand("start"));
    }

    private static DispatchTrainingSessionDto Running() => new()
    {
        RunId = "run-1", Revision = 4, PhaseCode = DispatchTrainingPhaseCodes.Running, ActiveDriverCount = 6, AvailableSupportDriverCount = 2,
        AllowedCommands = ["pause", "stop", "support-drivers"]
    };
    private sealed class Fixture
    {
        public Client Client { get; } = new(); public Identity Identity { get; } = new(); public 역할앱생명주기State Lifecycle { get; } = new();
        public DispatchTrainingPageState State(DispatchTrainingPendingSession? pending = null)
        { Lifecycle.전환(역할앱생명주기단계.활성); return new(Client, Identity, pending ?? new(), Lifecycle); }
    }
    private sealed class Identity : IDispatchTrainingIdentity
    {
        public string? User { get; set; } = "admin"; public bool Admin { get; set; } = true; public int EnsureCalls { get; private set; }
        public string? Owner => User; public bool IsAdmin => Admin; public event Action? Changed;
        public void Notify() => Changed?.Invoke(); public Task<string?> EnsureAsync(CancellationToken ct) { EnsureCalls++; return Task.FromResult<string?>(null); }
    }
    private sealed class Client : IDispatchTrainingClient
    {
        public bool Enabled { get; set; } = true; public int ReadCalls { get; private set; }
        public DispatchTrainingSessionDto Snapshot { get; set; } = new() { ActiveDriverCount = 6, AvailableSupportDriverCount = 2, AllowedCommands = ["start"] };
        public DispatchTrainingDecisionsResponse Decisions { get; set; } = new();
        public Func<Task<DispatchTrainingSessionDto>>? Read { get; set; }
        public Func<string, DispatchTrainingCommandRequest, Task<DispatchTrainingCommandResultDto>>? Send { get; set; }
        public List<(string Action, DispatchTrainingCommandRequest Request)> Commands { get; } = [];
        public Task<DispatchTrainingCapabilityDto> CapabilityAsync(CancellationToken ct) => Task.FromResult(new DispatchTrainingCapabilityDto { Enabled = Enabled });
        public Task<DispatchTrainingSessionDto> CurrentAsync(CancellationToken ct) { ReadCalls++; return Read?.Invoke() ?? Task.FromResult(Snapshot); }
        public Task<DispatchTrainingDecisionsResponse> DecisionsAsync(CancellationToken ct) => Task.FromResult(Decisions);
        public Task<DispatchTrainingIncidentsResponse> IncidentsAsync(CancellationToken ct) => Task.FromResult(new DispatchTrainingIncidentsResponse());
        public Task<DispatchTrainingCommandResultDto> CommandAsync(string action, DispatchTrainingCommandRequest request, CancellationToken ct)
        {
            Commands.Add((action, request)); if (Send is not null) return Send(action, request);
            Snapshot = Running() with { Revision = request.ExpectedRevision + 1, AppliedCommandIds = [request.ClientRequestId] };
            return Task.FromResult(new DispatchTrainingCommandResultDto { ClientRequestId = request.ClientRequestId, Session = Snapshot });
        }
    }
}
