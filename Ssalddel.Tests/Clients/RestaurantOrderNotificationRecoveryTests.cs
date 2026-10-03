using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantOrderNotificationRecoveryTests
{
    [Fact]
    public async Task DuplicateSignalsAreCheckedOnceAndContainOnlyStableIds()
    {
        using var f = await Fixture.CreateAsync();
        await f.Realtime.ReceiveAsync();
        await f.Realtime.ReceiveAsync();
        Assert.Equal(1, f.Orders.Reads);
        Assert.Equal(new RestaurantNotificationActivation("A", "owner"), Assert.Single(f.Platform.Shown));
        Assert.Equal(1, f.Sound.Calls);
        Assert.Equal(0, f.Orders.Commands);
    }

    [Theory]
    [InlineData("취소", 1)]
    [InlineData("주문확인", 1)]
    [InlineData("주문대기", 2)]
    public async Task StaleOrOtherRestaurantSignalsNeverAlert(string status, long restaurant)
    {
        using var f = await Fixture.CreateAsync();
        f.Orders.Current.상태 = status;
        f.Orders.Current.음식점Id = restaurant;
        await f.Realtime.ReceiveAsync();
        Assert.Empty(f.Platform.Shown);
        Assert.Equal(0, f.Sound.Calls);
    }

    [Fact]
    public async Task CanonicalFailureDoesNotMarkSignalSeenAndAllowsExplicitRetry()
    {
        using var f = await Fixture.CreateAsync();
        f.Orders.Read = (_, _) => throw new HttpRequestException("controlled offline");
        await f.Realtime.ReceiveAsync();
        Assert.Empty(f.Platform.Shown);
        f.Orders.Read = null;
        await f.Realtime.ReceiveAsync();
        Assert.Single(f.Platform.Shown);
        Assert.Equal(2, f.Orders.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedCanonicalResultAfterLogoutOrAccountChangeCannotShow(bool changeAccount)
    {
        using var f = await Fixture.CreateAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<음식주문응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Orders.Read = (_, _) => { started.SetResult(); return result.Task; };
        var receive = f.Realtime.ReceiveAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (changeAccount) await f.ApplyAsync("other-owner");
        else await f.Auth.LogoutAsync();
        result.SetResult(f.Orders.Current);
        await receive;
        Assert.Empty(f.Platform.Shown);
        Assert.Equal(0, f.Sound.Calls);
    }

    [Fact]
    public async Task TapReauthenticatesThenRequeriesCanonicalWithoutExecutingCommands()
    {
        using var f = await Fixture.CreateAsync();
        f.Orders.Current.상태 = "기사배정";
        f.Platform.Activate("A", "owner");
        var target = await f.Coordinator.ResolvePendingNavigationAsync();
        Assert.Equal("/orders/A", target);
        Assert.Equal(1, f.Orders.Reads);
        Assert.Equal(0, f.Orders.Commands);
        Assert.Null(await f.Coordinator.ResolvePendingNavigationAsync());
    }

    [Fact]
    public async Task TapWaitsForSameAccountLoginButDropsDifferentAccount()
    {
        using var f = await Fixture.CreateAsync();
        await f.Session.ClearAsync();
        f.Platform.Activate("A", "owner");
        Assert.Equal("/login", await f.Coordinator.ResolvePendingNavigationAsync());
        Assert.Equal(0, f.Orders.Reads);
        await f.ApplyAsync("owner");
        Assert.Equal("/orders/A", await f.Coordinator.ResolvePendingNavigationAsync());
        f.Platform.Activate("B", "owner");
        await f.ApplyAsync("other-owner");
        Assert.Null(await f.Coordinator.ResolvePendingNavigationAsync());
        Assert.Equal(1, f.Orders.Reads);
    }

    [Fact]
    public async Task ExplicitLogoutDiscardsPendingTapAndClearsLocalNotifications()
    {
        using var f = await Fixture.CreateAsync();
        f.Platform.Activate("A", "owner");
        var clearBefore = f.Platform.Clears;
        await f.Auth.LogoutAsync();
        await f.ApplyAsync("owner");
        Assert.Null(await f.Coordinator.ResolvePendingNavigationAsync());
        Assert.True(f.Platform.Clears > clearBefore);
        Assert.Equal(0, f.Orders.Reads);
    }

    [Fact]
    public async Task ProcessRestartConsumesIdOnlyActivationAndUsesFreshServerState()
    {
        var platform = new Platform { Pending = new("A", "owner") };
        using var f = await Fixture.CreateAsync(platform);
        f.Orders.Current.상태 = "수령확인";
        Assert.Null(platform.Pending);
        Assert.Equal("/orders/A", await f.Coordinator.ResolvePendingNavigationAsync());
        Assert.Equal(1, f.Orders.Reads);
        Assert.Empty(platform.Shown);
    }

    [Fact]
    public async Task ResumeRequestsCanonicalRefreshButNeverFabricatesOrderOrNotification()
    {
        using var f = await Fixture.CreateAsync();
        var refreshes = 0;
        f.Coordinator.CanonicalRefreshRequested += () => { refreshes++; return Task.CompletedTask; };
        await f.Coordinator.ResumeAsync();
        Assert.Equal(1, refreshes);
        Assert.Empty(f.Platform.Shown);
        await f.Auth.LogoutAsync();
        await f.Coordinator.ResumeAsync();
        Assert.Equal(1, refreshes);
    }

    [Fact]
    public async Task DisposeRemovesSubscriptionsAndDropsLateCanonicalRead()
    {
        using var f = await Fixture.CreateAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<음식주문응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Orders.Read = (_, _) => { started.SetResult(); return result.Task; };
        var receive = f.Realtime.ReceiveAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Coordinator.Dispose();
        result.SetResult(f.Orders.Current);
        await receive;
        await f.Realtime.ReceiveAsync();
        f.Platform.Activate("A", "owner");
        Assert.Empty(f.Platform.Shown);
        Assert.Equal(1, f.Orders.Reads);
        Assert.Null(await f.Coordinator.ResolvePendingNavigationAsync());
    }

    [Fact]
    public async Task DeniedPermissionDoesNotChangeOrderOrPreventCanonicalRead()
    {
        using var f = await Fixture.CreateAsync();
        Assert.False(await f.Coordinator.RequestPermissionAsync());
        f.Platform.Activate("A", "owner");
        Assert.Equal("/orders/A", await f.Coordinator.ResolvePendingNavigationAsync());
        Assert.Equal(0, f.Orders.Commands);
    }

    private sealed class Fixture : IDisposable
    {
        public ClientAuthSession Session { get; } = new(new Store(), new ClientSessionGuard());
        public RestaurantAuthService Auth { get; }
        public Orders Orders { get; } = new();
        public Realtime Realtime { get; } = new();
        public Sound Sound { get; } = new();
        public Platform Platform { get; }
        public RestaurantOrderNotificationCoordinator Coordinator { get; }
        private Fixture(Platform platform)
        {
            Platform = platform;
            Auth = new RestaurantAuthService(new HttpClient { BaseAddress = new("http://localhost/") }, Session);
            Coordinator = new(Realtime, Orders, Auth, Session, Sound, Platform);
        }
        public static async Task<Fixture> CreateAsync(Platform? platform = null)
        {
            var result = new Fixture(platform ?? new Platform());
            await result.ApplyAsync("owner");
            await result.Coordinator.SynchronizeAsync();
            return result;
        }
        public Task ApplyAsync(string owner) => Session.ApplyAsync(new("test-access", DateTime.UtcNow.AddHours(1),
            "test-refresh", DateTime.UtcNow.AddDays(1), owner, "test", ["음식점"]));
        public void Dispose() => Coordinator.Dispose();
    }
    private sealed class Store : IClientSecureTokenStore
    {
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<ClientAuthTokenSnapshot?>(null);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Platform : IRestaurantOrderNotificationPlatform
    {
        public bool IsSupported => true;
        public event Action<RestaurantNotificationActivation>? Activated;
        public RestaurantNotificationActivation? Pending;
        public List<RestaurantNotificationActivation> Shown { get; } = [];
        public int Clears;
        public void Activate(string order, string owner) => Activated?.Invoke(new(order, owner));
        public RestaurantNotificationActivation? TakePendingActivation() { var value = Pending; Pending = null; return value; }
        public Task<bool> RequestPermissionAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public Task ShowAsync(string orderNo, string ownerId, CancellationToken cancellationToken) { Shown.Add(new(orderNo, ownerId)); return Task.CompletedTask; }
        public void Clear() => Clears++;
    }
    private sealed class Sound : I주문알림Service
    {
        public int Calls;
        public Task 신규주문알림재생Async(CancellationToken cancellationToken = default) { Calls++; return Task.CompletedTask; }
    }
    private sealed class Orders : I음식주문ApiClient
    {
        public 음식주문응답 Current { get; } = new() { 주문번호 = "A", 음식점Id = 1, 상태 = "주문대기" };
        public int Reads;
        public int Commands;
        public Func<string, CancellationToken, Task<음식주문응답?>>? Read;
        public Task<음식주문응답?> 주문상세조회Async(string 주문번호, CancellationToken cancellationToken = default)
        { Reads++; return Read?.Invoke(주문번호, cancellationToken) ?? Task.FromResult<음식주문응답?>(Current); }
        public Task<음식점주문수신함응답> 주문목록조회Async(음식점주문수신함조회요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식주문응답?> 음식점수락Async(string 주문번호, 음식점주문수락요청 request, CancellationToken cancellationToken = default) { Commands++; throw new NotSupportedException(); }
        public Task<음식주문응답?> 음식점진행변경Async(string 주문번호, 음식점주문진행변경요청 request, CancellationToken cancellationToken = default) { Commands++; throw new NotSupportedException(); }
    }
    private sealed class Realtime : I음식점주문SignalRClientService
    {
        public event Func<음식점주문수신알림, Task>? 주문수신;
        public event Func<음식점주문상태변경알림, Task>? 주문상태변경 { add { } remove { } }
        public event Func<음식점실시간연결상태변경, Task>? 상태변경 { add { } remove { } }
        public event Func<Task>? 재연결후재조회요청 { add { } remove { } }
        public 음식점실시간연결상태 연결상태 => 음식점실시간연결상태.연결됨;
        public Task 연결Async(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task 연결해제Async() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task ReceiveAsync() => 주문수신?.Invoke(new() { 주문번호 = "A", 음식점Id = 1, 고객명 = "must not expose", 메뉴요약 = "must not expose" }) ?? Task.CompletedTask;
    }
}
