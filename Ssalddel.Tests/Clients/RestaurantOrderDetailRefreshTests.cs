using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantDeskApp.Components.Pages;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantOrderDetailRefreshTests
{
    [Theory]
    [InlineData("기사배정", "기사배정")]
    [InlineData("취소", "미요청")]
    [InlineData("조리중", "추천중")]
    public async Task SelectedOrderNotificationRequeriesCanonicalStateWithoutResettingDraft(string state, string dispatch)
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        fixture.Set("preparationMinutes", 37);
        fixture.Set("rejectionReason", "작성 중 사유");
        fixture.Desk.Current = Item("A", state, dispatch);

        await fixture.Realtime.ChangeAsync("B");
        Assert.Equal(1, fixture.Desk.Reads);
        await fixture.Realtime.ChangeAsync("A");

        Assert.Equal(2, fixture.Desk.Reads);
        Assert.Equal(state, fixture.Get<음식점주문DeskItem>("order").상태);
        Assert.Equal(dispatch, fixture.Get<음식점주문DeskItem>("order").배차상태);
        Assert.Equal(37, fixture.Get<int>("preparationMinutes"));
        Assert.Equal("작성 중 사유", fixture.Get<string>("rejectionReason"));
    }

    [Fact]
    public async Task InitialHubFailureRetriesAfterCanonicalReadRecovers()
    {
        await using var fixture = new Fixture();
        fixture.Realtime.FailNextConnect = true;
        await fixture.MountAsync();
        Assert.Equal(1, fixture.Realtime.Connects);
        Assert.False(fixture.Get<bool>("readFailed"));
        await fixture.Realtime.ChangeAsync("A");
        Assert.Equal(2, fixture.Realtime.Connects);
        Assert.Equal(음식점실시간연결상태.연결됨, fixture.Realtime.연결상태);
    }

    [Fact]
    public async Task PendingConnectionAndItsLateFailureCannotStartOverlappingAttempts()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        await fixture.Realtime.DisconnectNoticeAsync();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Realtime.Connect = _ => { firstStarted.TrySetResult(); return first.Task; };
        var connecting = fixture.Realtime.ReconnectAsync();
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.Realtime.DisconnectNoticeAsync();
        fixture.Desk.Current = Item("B");
        await fixture.MountAsync("B").WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Realtime.ReconnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, fixture.Realtime.Connects);
        first.SetException(new HttpRequestException("controlled late first connection failure"));
        await connecting;

        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Realtime.Connect = _ => { secondStarted.TrySetResult(); return second.Task; };
        var retrying = fixture.Realtime.ChangeAsync("B");
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Realtime.DisconnectNoticeAsync();
        await fixture.Realtime.ReconnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, fixture.Realtime.Connects);
        second.SetResult();
        await retrying;
        await fixture.Realtime.ChangeAsync("B");

        Assert.Equal(3, fixture.Realtime.Connects);
        Assert.Equal("B", fixture.Get<음식점주문DeskItem>("order").주문번호);
        Assert.False(fixture.Get<bool>("realtimeConnectionInFlight"));
        Assert.True(fixture.Get<bool>("realtimeConnectionAttempted"));
    }

    [Fact]
    public async Task DisposalInvalidatesAConnectionAttemptThatIgnoresCancellation()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        await fixture.Realtime.DisconnectNoticeAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Realtime.Connect = _ => { started.TrySetResult(); return connection.Task; };
        var connecting = fixture.Realtime.ReconnectAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Page.Dispose();
        connection.SetException(new UnauthorizedAccessException("controlled late authentication failure"));
        await connecting;
        await fixture.Realtime.ReconnectAsync();
        await fixture.Realtime.ChangeAsync("A");

        Assert.Equal(2, fixture.Realtime.Connects);
        Assert.Equal(0, fixture.Realtime.SubscriptionCount);
        Assert.False(fixture.Get<bool>("realtimeConnectionInFlight"));
        Assert.False(fixture.Get<bool>("workLocked"));
        Assert.DoesNotContain("/login", fixture.Navigation.Uri);
    }

    [Fact]
    public async Task FirstSuccessfulRetryUsesServerPreparationTime()
    {
        await using var fixture = new Fixture();
        fixture.Desk.Read = (_, _) => throw new HttpRequestException("controlled first failure");
        await fixture.MountAsync();
        fixture.Desk.Read = null;
        fixture.Desk.Current.선택조리예상분 = 45;
        await fixture.Realtime.ReconnectAsync();
        Assert.Equal(45, fixture.Get<int>("preparationMinutes"));
        Assert.False(fixture.Get<bool>("readFailed"));
    }

    [Fact]
    public async Task UntouchedPreparationTimeTracksOtherDevices()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        fixture.Desk.Current.선택조리예상분 = 45;
        await fixture.Realtime.ChangeAsync("A");
        Assert.Equal(45, fixture.Get<int>("preparationMinutes"));
        fixture.Set("preparationMinutes", 37);
        fixture.Desk.Current.선택조리예상분 = 12;
        await fixture.Realtime.ChangeAsync("A");
        Assert.Equal(37, fixture.Get<int>("preparationMinutes"));
    }

    [Fact]
    public async Task ReconnectionRefreshesEvenWithoutAnOrderNotification()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        fixture.Desk.Current = Item("A", "픽업완료");

        await fixture.Realtime.ReconnectAsync();

        Assert.Equal("픽업완료", fixture.Get<음식점주문DeskItem>("order").상태);
        Assert.Equal(2, fixture.Desk.Reads);
    }

    [Fact]
    public async Task NotificationsDuringAReadAreCoalescedButNotLost()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<음식점주문DeskItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Desk.Read = (_, _) => { started.TrySetResult(); return delayed.Task; };
        var first = fixture.Realtime.ChangeAsync("A");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Realtime.ChangeAsync("A");
        await fixture.Realtime.ChangeAsync("A");
        fixture.Desk.Read = null;
        fixture.Desk.Current = Item("A", "취소");
        delayed.SetResult(Item("A", "기사배정"));
        await first;

        Assert.Equal(3, fixture.Desk.Reads);
        Assert.Equal("취소", fixture.Get<음식점주문DeskItem>("order").상태);
    }

    [Fact]
    public async Task NotificationDuringACommandIsReadAfterTheCommandFinishes()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new TaskCompletionSource<음식점주문DeskItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Desk.Command = _ => { started.TrySetResult(); return command.Task; };
        var running = fixture.CallAsync("UpdateCookingTimeAsync");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Realtime.ChangeAsync("A");
        Assert.Equal(1, fixture.Desk.Reads);
        fixture.Desk.Current = Item("A", "조리중", "추천중");
        command.SetResult(Item("A", "조리중", "기사배정"));
        await running;

        Assert.Equal(2, fixture.Desk.Reads);
        Assert.Equal("추천중", fixture.Get<음식점주문DeskItem>("order").배차상태);
    }

    [Fact]
    public async Task TransientFailurePreservesSnapshotAndDraftThenRecovers()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        fixture.Set("preparationMinutes", 37);
        fixture.Desk.Read = (_, _) => throw new HttpRequestException("controlled unavailable");
        await fixture.Realtime.ChangeAsync("A");
        Assert.True(fixture.Get<bool>("readFailed"));
        Assert.NotNull(fixture.Get<음식점주문DeskItem>("order"));
        Assert.Equal(37, fixture.Get<int>("preparationMinutes"));

        fixture.Desk.Read = null;
        await fixture.Realtime.ReconnectAsync();
        Assert.False(fixture.Get<bool>("readFailed"));
        Assert.Equal(37, fixture.Get<int>("preparationMinutes"));
    }

    [Fact]
    public async Task NewSelectionIgnoresLateOldResponseAndInitializesOnlyItsOwnDraft()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<음식점주문DeskItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Desk.Read = (_, _) => { started.TrySetResult(); return delayed.Task; };
        var oldRead = fixture.Realtime.ChangeAsync("A");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Desk.Read = null;
        fixture.Desk.Current = Item("B", "주문대기");
        fixture.Desk.Current.선택조리예상분 = 12;
        await fixture.MountAsync("B");
        delayed.SetResult(Item("A", "취소"));
        await oldRead;

        Assert.Equal("B", fixture.Get<음식점주문DeskItem>("order").주문번호);
        Assert.Equal(12, fixture.Get<int>("preparationMinutes"));
        Assert.False(fixture.Get<bool>("isRefreshing"));
    }

    [Fact]
    public async Task AuthenticationLossCannotBeUndoneByAnEarlierRead()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<음식점주문DeskItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Desk.Read = (_, _) => { started.TrySetResult(); return delayed.Task; };
        var oldRead = fixture.Realtime.ChangeAsync("A");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Realtime.RequireLoginAsync();
        delayed.SetResult(Item("A", "기사배정"));
        await oldRead;

        Assert.Null(fixture.Get<음식점주문DeskItem?>("order"));
        Assert.True(fixture.Get<bool>("workLocked"));
        Assert.EndsWith("/login", fixture.Navigation.Uri);
    }

    [Fact]
    public async Task DisposalUnsubscribesAndPreventsLateResponseOrRequery()
    {
        await using var fixture = new Fixture();
        await fixture.MountAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<음식점주문DeskItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Desk.Read = (_, _) => { started.TrySetResult(); return delayed.Task; };
        var oldRead = fixture.Realtime.ChangeAsync("A");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Page.Dispose();
        delayed.SetResult(Item("A", "취소"));
        await oldRead;
        await fixture.Realtime.ReconnectAsync();
        await fixture.Realtime.ChangeAsync("A");

        Assert.Equal("주문대기", fixture.Get<음식점주문DeskItem>("order").상태);
        Assert.Equal(2, fixture.Desk.Reads);
        Assert.Equal(0, fixture.Realtime.SubscriptionCount);
        Assert.False(fixture.Realtime.Disconnected);
    }

    private static 음식점주문DeskItem Item(string orderNo, string state = "주문대기", string dispatch = "미요청")
        => new() { 주문번호 = orderNo, 상태 = state, 배차상태 = dispatch, 추천조리예상분 = 20 };

    // Test-only renderer integration is pinned to the repository's Blazor runtime.
#pragma warning disable BL0006
    private sealed class Fixture : IAsyncDisposable
    {
        public Desk Desk { get; } = new();
        public Realtime Realtime { get; } = new();
        public TestNavigation Navigation { get; } = new();
        public OrderDetail Page { get; }
        private readonly TestRenderer renderer = new();
        private int? id;
        public Fixture() => Page = new OrderDetail { OrderDeskService = Desk, RestaurantRealtimeService = Realtime, NavigationManager = Navigation };
        public Task MountAsync(string orderNo = "A") => renderer.Dispatcher.InvokeAsync(async () =>
        {
            id ??= renderer.Add(Page);
            await renderer.MountAsync(id.Value, ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(OrderDetail.OrderNo)] = orderNo }));
        });
        public T Get<T>(string name) => (T)typeof(OrderDetail).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Page)!;
        public void Set(string name, object value) => typeof(OrderDetail).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Page, value);
        public Task CallAsync(string name) => renderer.Dispatcher.InvokeAsync(() => (Task)typeof(OrderDetail).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Page, null)!);
        public ValueTask DisposeAsync() => renderer.DisposeAsync();
    }

    // Source-linked code-behind needs a dispatcher/handle for real lifecycle callbacks;
    // this fixture deliberately does not claim Razor markup or device rendering proof.
    private sealed class TestRenderer() : Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public int Add(IComponent component) => AssignRootComponentId(component);
        public Task MountAsync(int id, ParameterView parameters) => RenderRootComponentAsync(id, parameters);
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => throw exception;
    }
#pragma warning restore BL0006

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/orders/A");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
        protected override void NavigateToCore(string uri, NavigationOptions options) => Uri = ToAbsoluteUri(uri).ToString();
    }

    private sealed class Realtime : I음식점주문SignalRClientService
    {
        public event Func<음식점주문수신알림, Task>? 주문수신;
        public event Func<음식점주문상태변경알림, Task>? 주문상태변경;
        public event Func<음식점실시간연결상태변경, Task>? 상태변경;
        public event Func<Task>? 재연결후재조회요청;
        public 음식점실시간연결상태 연결상태 { get; private set; }
        public bool Disconnected { get; private set; }
        public bool FailNextConnect { get; set; }
        public Func<CancellationToken, Task>? Connect { get; set; }
        public int Connects { get; private set; }
        public int SubscriptionCount => (주문수신?.GetInvocationList().Length ?? 0) + (주문상태변경?.GetInvocationList().Length ?? 0) + (상태변경?.GetInvocationList().Length ?? 0) + (재연결후재조회요청?.GetInvocationList().Length ?? 0);
        public async Task 연결Async(CancellationToken cancellationToken = default)
        {
            Connects++;
            if (FailNextConnect) { FailNextConnect = false; throw new HttpRequestException("controlled hub failure"); }
            if (Connect is not null) await Connect(cancellationToken);
            연결상태 = 음식점실시간연결상태.연결됨;
        }
        public Task 연결해제Async() { Disconnected = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task ChangeAsync(string orderNo) => 주문상태변경?.Invoke(new 음식점주문상태변경알림 { 주문번호 = orderNo }) ?? Task.CompletedTask;
        public Task ReconnectAsync() => 재연결후재조회요청?.Invoke() ?? Task.CompletedTask;
        public Task DisconnectNoticeAsync()
        {
            연결상태 = 음식점실시간연결상태.연결끊김;
            return 상태변경?.Invoke(new(음식점실시간연결상태.연결끊김, "controlled disconnect")) ?? Task.CompletedTask;
        }
        public Task RequireLoginAsync() => 상태변경?.Invoke(new(음식점실시간연결상태.인증필요, "controlled expiry")) ?? Task.CompletedTask;
    }

    private sealed class Desk : I음식점주문DeskService
    {
        public 음식점주문DeskItem Current { get; set; } = Item("A");
        public int Reads { get; private set; }
        public Func<string, CancellationToken, Task<음식점주문DeskItem?>>? Read { get; set; }
        public Func<CancellationToken, Task<음식점주문DeskItem?>>? Command { get; set; }
        public Task<음식점주문DeskItem?> 주문조회Async(string 주문번호, 음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회, CancellationToken cancellationToken = default)
        { Reads++; return Read?.Invoke(주문번호, cancellationToken) ?? Task.FromResult<음식점주문DeskItem?>(Current); }
        public Task<IReadOnlyList<음식점주문DeskItem>> 주문목록조회Async(음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식점주문DeskItem> 주문알림수신Async(음식점주문수신Payload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식점주문수락결과> 주문수락후전표준비Async(string 주문번호, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식점주문수락결과> 주문수락후전표준비Async(string 주문번호, int 조리예상분, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식점주문DeskItem?> 주문거절Async(string 주문번호, string 사유, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식점주문DeskItem?> 조리시간변경Async(string 주문번호, int 조리예상분, CancellationToken cancellationToken = default) => Command?.Invoke(cancellationToken) ?? Task.FromResult<음식점주문DeskItem?>(Current);
        public Task<음식점주문DeskItem?> 픽업준비완료Async(string 주문번호, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task 전표출력완료Async(string 주문번호, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
