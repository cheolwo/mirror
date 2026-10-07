using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleWorkspaceReliabilityR16Tests
{
    [Fact]
    public async Task 위치_만료는_갱신응답을_기다리지_않고_핀과_연결경로를_제거한다()
    {
        var clock = new Clock();
        var read = new TaskCompletionSource<RoleWorkspaceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Snapshot("orderer", "order", clock.GetUtcNow().AddSeconds(30));
        var adapter = new Adapter("orderer") { Reply = () => Task.FromResult(first) };
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new(), clock);
        await model.InitializeAsync("orderer");
        adapter.Reply = () => read.Task;
        clock.Advance(15);
        var refresh = model.TickAsync();
        Assert.True(model.IsRefreshing);
        Assert.False(model.IsLoading);
        Assert.Equal("order", model.SelectedId);
        clock.Advance(15);
        await model.TickAsync();
        Assert.DoesNotContain(model.RenderState().Markers, value => value.Kind == NeighborhoodMapMarkerKinds.Driver);
        Assert.Single(model.RenderState().Markers);
        Assert.Empty(model.RenderState().Routes);
        Assert.NotNull(model.LocationMessage);
        Assert.Equal(2, adapter.Reads);
        read.SetResult(Snapshot("orderer", "order"));
        await refresh;
    }

    [Fact]
    public async Task 자동조회는_15초간격이며_확인창과_숨김_중에는_요청하지_않는다()
    {
        var clock = new Clock();
        var adapter = new Adapter("restaurant");
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new(), clock);
        await model.InitializeAsync("restaurant");
        clock.Advance(14);
        await model.TickAsync();
        Assert.Equal(1, adapter.Reads);
        clock.Advance(1);
        await model.TickAsync(false);
        Assert.Equal(1, adapter.Reads);
        await model.TickAsync();
        Assert.Equal(2, adapter.Reads);
        model.Suspend();
        clock.Advance(60);
        await model.TickAsync();
        Assert.Equal(2, adapter.Reads);
        Assert.Empty(model.Items);
        Assert.Empty(model.RenderState().Markers);
        await model.ResumeAsync();
        Assert.Equal(3, adapter.Reads);
    }

    [Fact]
    public async Task 자동조회_실패는_마지막카드와_오류를_남기고_명령은_새조회까지_차단한다()
    {
        var clock = new Clock();
        var adapter = new Adapter("restaurant");
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new(), clock);
        await model.InitializeAsync("restaurant");
        adapter.Reply = () => throw new HttpRequestException("연결 중단");
        clock.Advance(15);
        await model.TickAsync();
        Assert.Single(model.Items);
        Assert.NotNull(model.RefreshError);
        Assert.Null(model.Error);
        await model.PerformAsync("current", "advance");
        Assert.Equal(0, adapter.Commands);
        adapter.Reply = null;
        await model.RefreshAsync();
        Assert.Null(model.RefreshError);
        await model.PerformAsync("current", "advance");
        Assert.Equal(1, adapter.Commands);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 자동조회_권한오류는_기존_비공개_지도와_카드를_제거한다(int status)
    {
        var clock = new Clock();
        var adapter = new Adapter("orderer");
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new(), clock);
        await model.InitializeAsync("orderer");
        adapter.Reply = () => throw new RoleWorkspaceAccessException(status, "권한 확인 필요");
        clock.Advance(15);
        await model.TickAsync();
        Assert.Empty(model.Items);
        Assert.Empty(model.RenderState().Markers);
        Assert.Equal(status == 401, model.RequiresLogin);
        Assert.Equal(status == 403, model.AccessDenied);
        clock.Advance(30);
        await model.TickAsync();
        Assert.Equal(2, adapter.Reads);
    }

    [Fact]
    public async Task 처리_중_지도무효화와_수동새로고침은_업무조회에_끼어들지_않는다()
    {
        var clock = new Clock();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var adapter = new Adapter("food-driver") { CommandReply = () => completion.Task };
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new(), clock);
        await model.InitializeAsync("food-driver");
        var command = model.PerformAsync("current", "advance");
        Assert.True(model.CommandBusy);
        clock.Advance(30);
        await model.RefreshAsync();
        await model.TickAsync();
        Assert.Equal(1, adapter.Reads);
        Assert.Single(model.Items);
        Assert.False(model.IsLoading);
        completion.SetResult();
        await command;
        Assert.False(model.CommandBusy);
        Assert.Equal(2, adapter.Reads);
    }

    [Theory]
    [InlineData("food-driver")]
    [InlineData("cargo-driver")]
    [InlineData("warehouse")]
    public async Task 수행_완료뒤_선택카드와_지도는_다음_현재업무로_함께_바뀐다(string role)
    {
        var adapter = new Adapter(role);
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        await model.InitializeAsync(role);
        adapter.Reply = () => Task.FromResult(new RoleWorkspaceSnapshot(role,
            [Item("current") with { IsCurrent = false, Status = "완료", Actions = [] }, Item("next")], SelectedId: "current"));
        await model.PerformAsync("current", "advance");
        Assert.Equal("next", model.SelectedId);
        Assert.Equal("next", model.SelectedItem?.Id);
        Assert.Equal("next:place", model.RenderState().SelectedMarkerId);
    }

    [Theory]
    [InlineData("orderer")]
    [InlineData("restaurant")]
    [InlineData("shipper")]
    [InlineData("operator")]
    public async Task 조회역할의_명시적_선택은_자동조회로_다른업무로_바뀌지_않는다(string role)
    {
        var adapter = new Adapter(role)
        {
            Reply = () => Task.FromResult(new RoleWorkspaceSnapshot(role,
                [Item("chosen") with { IsCurrent = false }, Item("current")], SelectedId: "chosen"))
        };
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        await model.InitializeAsync(role, "chosen");
        await model.RefreshAsync();
        Assert.Equal("chosen", model.SelectedId);
    }

    private static RoleWorkspaceItem Item(string id)
        => new(id, "업무", "진행 중", Actions: [new("advance", "처리", IsPrimary: true)],
            Markers: [new(id + ":place", "장소", NeighborhoodMapMarkerKinds.Pickup, 37.58, 127.08)], IsCurrent: true);

    private static RoleWorkspaceSnapshot Snapshot(string role, string id, DateTimeOffset? expiry = null)
        => new(role, [Item(id) with
        {
            Markers = [new(id + ":place", "장소", NeighborhoodMapMarkerKinds.Pickup, 37.58, 127.08),
                new(id + ":driver", "최근 위치", NeighborhoodMapMarkerKinds.Driver, 37.59, 127.09, ExpiresAt: expiry)],
            Routes = [new(id + ":route", [new(37.59, 127.09), new(37.58, 127.08)], "#2563eb", ExpiresAt: expiry)]
        }], SelectedId: id);

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
    }

    private sealed class Adapter(string role) : IRoleWorkspaceAdapter
    {
        public string RoleKey => role;
        public int Reads { get; private set; }
        public int Commands { get; private set; }
        public Func<Task<RoleWorkspaceSnapshot>>? Reply { get; set; }
        public Func<Task>? CommandReply { get; set; }
        public Task<RoleWorkspaceSnapshot> LoadAsync(string? selected, CancellationToken cancellationToken)
        { ++Reads; return Reply?.Invoke() ?? Task.FromResult(new RoleWorkspaceSnapshot(role, [Item("current")], SelectedId: "current")); }
        public Task PerformAsync(string item, string action, Guid id, CancellationToken cancellationToken)
        { ++Commands; return CommandReply?.Invoke() ?? Task.CompletedTask; }
        public void Clear() { }
    }

    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string role) => new("owner", 1, true);
        public Task EnsureInitializedAsync(string role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string role, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
