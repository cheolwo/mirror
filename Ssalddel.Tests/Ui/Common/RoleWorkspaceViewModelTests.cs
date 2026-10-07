using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleWorkspaceViewModelTests
{
    [Fact]
    public async Task 역할을_바꾼_뒤_도착한_이전_비공개_조회는_적용하지_않는다()
    {
        var late = new TaskCompletionSource<RoleWorkspaceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var food = new Adapter(RoleWorkspaceCatalog.FoodDriver) { Read = (_, _) => late.Task };
        var cargo = new Adapter(RoleWorkspaceCatalog.CargoDriver);
        using var model = new RoleWorkspaceViewModel([food, cargo], new Access(), new());
        var first = model.InitializeAsync(food.RoleKey);
        await model.InitializeAsync(cargo.RoleKey);
        late.SetResult(Snapshot(food.RoleKey, "old-food", "이전 고객 주소"));
        await first;
        Assert.Equal(cargo.RoleKey, model.Role?.Key);
        Assert.Equal("cargo-driver-item", model.SelectedId);
        Assert.DoesNotContain(model.Items, item => item.Subtitle == "이전 고객 주소");
        Assert.DoesNotContain(model.RenderState().Markers, marker => marker.Id.StartsWith("old-food", StringComparison.Ordinal));
        Assert.True(food.ClearCount > 0);
    }

    [Fact]
    public async Task 계정이_바뀌면_이전_선택과_늦은_응답을_재사용하지_않는다()
    {
        var access = new Access();
        var late = new TaskCompletionSource<RoleWorkspaceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var adapter = new Adapter(RoleWorkspaceCatalog.Restaurant)
        {
            Read = (_, _) => ++count == 1 ? late.Task : Task.FromResult(Snapshot(RoleWorkspaceCatalog.Restaurant, "new-account-item"))
        };
        using var model = new RoleWorkspaceViewModel([adapter], access, new());
        var first = model.InitializeAsync(adapter.RoleKey, "old-account-item");
        access.Change(new("second-account", 2, true));
        late.SetResult(Snapshot(adapter.RoleKey, "old-account-item", "이전 계정 고객 정보"));
        await first;
        Assert.Equal("new-account-item", model.SelectedId);
        Assert.Single(model.Items);
        Assert.DoesNotContain(model.Items, item => item.Id == "old-account-item");
    }

    [Fact]
    public async Task 중단은_주소와_핀을_즉시_지우고_재개는_선택ID로_새로_조회한다()
    {
        var adapter = new Adapter(RoleWorkspaceCatalog.CargoDriver);
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        await model.InitializeAsync(adapter.RoleKey);
        Assert.NotEmpty(model.RenderState().Markers);
        model.SetViewport(new(37.6, 127.1, 15));
        model.Suspend();
        Assert.Empty(model.Items);
        Assert.Empty(model.RenderState().Markers);
        Assert.Empty(model.RenderState().Routes);
        Assert.Null(model.RenderState().Viewport);
        var reads = adapter.ReadCount;
        adapter.Read = (id, _) => Task.FromResult(Snapshot(adapter.RoleKey, id ?? "unexpected", "다시 조회한 목적지"));
        await model.ResumeAsync();
        Assert.Equal(reads + 1, adapter.ReadCount);
        Assert.Equal("cargo-driver-item", model.SelectedId);
        Assert.Equal("다시 조회한 목적지", model.SelectedItem?.Subtitle);
    }

    [Fact]
    public async Task 초기_인증_복원의_변경_알림은_재진입_루프없이_새_계정으로_조회한다()
    {
        var access = new Access(new(null, 0, false));
        access.Initialize = (_, _) =>
        {
            access.Initialize = (_, _) => Task.CompletedTask;
            access.Change(new("restored-account", 1, true));
            return Task.CompletedTask;
        };
        var adapter = new Adapter(RoleWorkspaceCatalog.Warehouse);
        using var model = new RoleWorkspaceViewModel([adapter], access, new());
        await model.InitializeAsync(adapter.RoleKey);
        Assert.False(model.IsLoading);
        Assert.Null(model.Error);
        Assert.Single(model.Items);
        Assert.Equal(1, adapter.ReadCount);
        Assert.InRange(access.InitializeCount, 1, 2);
    }

    [Fact]
    public async Task 이전_역할의_인증복원_대기는_현재_역할의_계정변경_재조회를_막지_않는다()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var access = new Access
        {
            Initialize = (role, _) => role == RoleWorkspaceCatalog.FoodDriver ? pending.Task : Task.CompletedTask
        };
        var food = new Adapter(RoleWorkspaceCatalog.FoodDriver);
        var cargo = new Adapter(RoleWorkspaceCatalog.CargoDriver);
        using var model = new RoleWorkspaceViewModel([food, cargo], access, new());
        var oldInitialization = model.InitializeAsync(food.RoleKey);
        await model.InitializeAsync(cargo.RoleKey);
        access.Change(new("new-account", 2, true));
        Assert.Equal(2, cargo.ReadCount);
        pending.SetResult();
        await oldInitialization;
        Assert.Equal(cargo.RoleKey, model.Role?.Key);
        Assert.Single(model.Items);
        Assert.Equal(0, food.ReadCount);
    }

    [Fact]
    public async Task 미확정_명령_재시도는_같은요청ID를_사용하고_성공뒤_원장을_재조회한다()
    {
        var ids = new List<Guid>();
        var adapter = new Adapter(RoleWorkspaceCatalog.Restaurant)
        {
            Perform = (_, _, id, _) =>
            {
                ids.Add(id);
                if (ids.Count == 1) throw new HttpRequestException("응답 유실");
                return Task.CompletedTask;
            }
        };
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        await model.InitializeAsync(adapter.RoleKey);
        await model.PerformAsync(model.SelectedId!, "confirm");
        Assert.NotNull(model.CommandError);
        var reads = adapter.ReadCount;
        await model.PerformAsync(model.SelectedId!, "confirm");
        Assert.Equal(ids[0], ids[1]);
        Assert.Equal(reads + 1, adapter.ReadCount);
        Assert.Null(model.CommandError);
    }

    [Fact]
    public async Task 계정_전환_후_늦은_명령_완료가_새_화면을_재조회하지_않는다()
    {
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var access = new Access();
        var adapter = new Adapter(RoleWorkspaceCatalog.Restaurant) { Perform = (_, _, _, _) => late.Task };
        using var model = new RoleWorkspaceViewModel([adapter], access, new());
        await model.InitializeAsync(adapter.RoleKey);
        var command = model.PerformAsync(model.SelectedId!, "confirm");
        access.Change(new("second-account", 2, true));
        var reads = adapter.ReadCount;
        late.SetResult();
        await command;
        Assert.Equal(reads, adapter.ReadCount);
        Assert.False(model.CommandBusy);
        Assert.Null(model.CommandError);
    }

    [Fact]
    public async Task 권한_실패는_이전_업무_지도와_카드를_제거한다()
    {
        var adapter = new Adapter(RoleWorkspaceCatalog.Operator);
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        await model.InitializeAsync(adapter.RoleKey);
        adapter.Read = (_, _) => throw new RoleWorkspaceAccessException(403, "운영자 권한이 필요합니다.");
        await model.RefreshAsync();
        Assert.True(model.AccessDenied);
        Assert.Empty(model.Items);
        Assert.Empty(model.RenderState().Markers);
        Assert.Equal("운영자 권한이 필요합니다.", model.Error);
    }

    [Fact]
    public async Task 업무가_없는_기사도_서버가_허용한_운행시작을_실행할_수_있다()
    {
        var role = RoleWorkspaceCatalog.FoodDriver;
        var calls = 0;
        var adapter = new Adapter(role)
        {
            Read = (_, _) => Task.FromResult(new RoleWorkspaceSnapshot(role, [],
                EmptyActions: [new("start", "운행 시작", IsPrimary: true)])),
            Perform = (item, action, requestId, _) =>
            {
                Assert.Equal(string.Empty, item);
                Assert.Equal("start", action);
                Assert.NotEqual(Guid.Empty, requestId);
                ++calls;
                return Task.CompletedTask;
            }
        };
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        await model.InitializeAsync(role);
        await model.PerformAsync(string.Empty, "unknown");
        Assert.Equal(0, calls);
        await model.PerformAsync(string.Empty, "start");
        Assert.Equal(1, calls);
        Assert.Equal(2, adapter.ReadCount);
    }

    [Fact]
    public async Task 실행중_인증이_만료되면_업무를_제거하고_로그인_안내를_제공한다()
    {
        var adapter = new Adapter(RoleWorkspaceCatalog.Restaurant)
        {
            Perform = (_, _, _, _) => throw new RoleWorkspaceAccessException(401, "다시 로그인해 주세요.")
        };
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        await model.InitializeAsync(adapter.RoleKey);
        await model.PerformAsync(model.SelectedId!, "confirm");
        Assert.True(model.RequiresLogin);
        Assert.Equal("다시 로그인해 주세요.", model.Error);
        Assert.Empty(model.Items);
        Assert.Empty(model.RenderState().Markers);
        Assert.False(model.CommandBusy);
    }

    [Fact]
    public async Task 현재_역할_로그아웃은_중복실행없이_계정알림으로_비공개업무를_제거한다()
    {
        var access = new Access();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? signedOutRole = null;
        var calls = 0;
        access.SignOut = async (role, _) =>
        {
            signedOutRole = role; ++calls;
            await pending.Task;
            access.Change(new(null, 2, false));
        };
        var adapter = new Adapter(RoleWorkspaceCatalog.FoodDriver)
        {
            Read = (_, _) => access.GetIdentity(RoleWorkspaceCatalog.FoodDriver).IsAuthenticated
                ? Task.FromResult(Snapshot(RoleWorkspaceCatalog.FoodDriver, "private"))
                : throw new RoleWorkspaceAccessException(401, "로그인이 필요합니다.")
        };
        using var model = new RoleWorkspaceViewModel([adapter], access, new());
        await model.InitializeAsync(adapter.RoleKey);
        var signOut = model.SignOutAsync();
        await model.SignOutAsync();
        Assert.True(model.AccountBusy);
        Assert.Equal(1, calls);
        pending.SetResult();
        await signOut;
        Assert.Equal(adapter.RoleKey, signedOutRole);
        Assert.False(model.IsAuthenticated);
        Assert.False(model.AccountBusy);
        Assert.Empty(model.Items);
        Assert.Empty(model.RenderState().Markers);
        Assert.True(model.RequiresLogin);
    }

    [Fact]
    public async Task 마커의_역할별_접미사는_변경하지_않고_연결된_stableID를_선택한다()
    {
        var role = RoleWorkspaceCatalog.CargoDriver;
        var adapter = new Adapter(role)
        {
            Read = (id, _) => Task.FromResult(new RoleWorkspaceSnapshot(role,
                [Item("one"), Item("two")], SelectedId: id))
        };
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        await model.InitializeAsync(role);
        await model.SelectMarkerAsync("two|pickup");
        Assert.Equal("two", model.SelectedId);
        Assert.Equal("two|pickup", model.RenderState().SelectedMarkerId);
    }

    [Fact]
    public async Task 핀과_경로_표시_설정은_업무_재조회나_인증_역할_변경없이_역할별로_기억한다()
    {
        var access = new Access();
        var food = new Adapter(RoleWorkspaceCatalog.FoodDriver);
        var cargo = new Adapter(RoleWorkspaceCatalog.CargoDriver);
        using var model = new RoleWorkspaceViewModel([food, cargo], access, new());
        await model.InitializeAsync(food.RoleKey);
        var identity = access.GetIdentity(food.RoleKey);
        model.SetMapVisibility(false, false);
        Assert.Empty(model.RenderState().Markers);
        Assert.Empty(model.RenderState().Routes);
        Assert.Equal(1, food.ReadCount);
        await model.InitializeAsync(cargo.RoleKey);
        Assert.True(model.ShowMarkers);
        Assert.True(model.ShowRoutes);
        await model.InitializeAsync(food.RoleKey);
        Assert.False(model.ShowMarkers);
        Assert.False(model.ShowRoutes);
        Assert.Equal(identity, access.GetIdentity(food.RoleKey));
    }

    [Fact]
    public async Task 서버가_비활성으로_준_행동은_명령을_보내지_않는다()
    {
        var calls = 0;
        var role = RoleWorkspaceCatalog.Restaurant;
        var adapter = new Adapter(role)
        {
            Read = (_, _) => Task.FromResult(new RoleWorkspaceSnapshot(role,
                [Item("one") with { Actions = [new("confirm", "처리", Enabled: false)] }])),
            Perform = (_, _, _, _) => { ++calls; return Task.CompletedTask; }
        };
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        await model.InitializeAsync(role);
        await model.PerformAsync("one", "confirm");
        Assert.Equal(0, calls);
    }

    private static RoleWorkspaceSnapshot Snapshot(string role, string id, string? subtitle = null)
        => new(role, [Item(id, subtitle)], SelectedId: id);
    private static RoleWorkspaceItem Item(string id, string? subtitle = null)
        => new(id, "현재 업무", "이동 중", subtitle,
            Sections: [new("목적지", [new("주소", subtitle ?? "현재 업무 주소")])],
            Actions: [new("confirm", "처리", IsPrimary: true)],
            Markers: [new(id + "|pickup", "픽업", NeighborhoodMapMarkerKinds.Pickup, 37.6, 127.1)],
            Routes: [new(id, [new(37.6, 127.1), new(37.61, 127.11)], "#f97316")], IsCurrent: true);

    private sealed class Adapter(string role) : IRoleWorkspaceAdapter
    {
        public string RoleKey => role;
        public int ReadCount { get; private set; }
        public int ClearCount { get; private set; }
        public Func<string?, CancellationToken, Task<RoleWorkspaceSnapshot>>? Read { get; set; }
        public Func<string, string, Guid, CancellationToken, Task>? Perform { get; set; }
        public Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
        {
            ++ReadCount;
            return Read?.Invoke(selectedId, cancellationToken) ?? Task.FromResult(Snapshot(role, role + "-item"));
        }
        public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
            => Perform?.Invoke(itemId, actionKey, requestId, cancellationToken) ?? Task.CompletedTask;
        public void Clear() => ++ClearCount;
    }
    private sealed class Access(RoleWorkspaceIdentity? identity = null) : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity _identity = identity ?? new("first-account", 1, true);
        public event Action? Changed;
        public int InitializeCount { get; private set; }
        public Func<string, CancellationToken, Task>? Initialize { get; set; }
        public Func<string, CancellationToken, Task>? SignOut { get; set; }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => _identity;
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default)
        { ++InitializeCount; return Initialize?.Invoke(roleKey, cancellationToken) ?? Task.CompletedTask; }
        public void Change(RoleWorkspaceIdentity identity) { _identity = identity; Changed?.Invoke(); }
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default)
            => SignOut?.Invoke(roleKey, cancellationToken) ?? throw new NotSupportedException();
    }
}
