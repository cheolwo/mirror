using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Options;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantRealtimeSessionOwnershipTests
{
    [Fact]
    public async Task AuthenticationRejectionDetachesPreviousConnectionBeforePublishingLoginFailure()
    {
        await using var fixture = new Fixture();
        var previous = fixture.Attach("restaurant-A");
        var failures = 0;
        fixture.Service.상태변경 += change =>
        {
            if (change.상태 == 음식점실시간연결상태.인증필요)
            {
                failures++;
                Assert.Null(fixture.Connection);
                Assert.Null(fixture.Owner);
            }
            return Task.CompletedTask;
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.연결Async());

        Assert.Equal(1, failures);
        Assert.Null(fixture.Connection);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => previous.StartAsync());
    }

    [Fact]
    public async Task ChangedUserAndReplacedConnectionCannotDeliverPreviousRestaurantNotifications()
    {
        await using var fixture = new Fixture();
        await fixture.AuthenticateAsync("restaurant-A");
        var previous = fixture.Attach("restaurant-A");
        var received = new List<string>();
        fixture.Service.주문수신 += notification => { received.Add(notification.주문번호); return Task.CompletedTask; };
        await fixture.NotifyAsync(previous, "restaurant-A", "A-before-logout");

        await fixture.AuthenticateAsync("restaurant-B");
        await fixture.NotifyAsync(previous, "restaurant-A", "A-after-user-change");
        var current = fixture.Attach("restaurant-B");
        await fixture.NotifyAsync(previous, "restaurant-A", "A-after-replacement");
        await fixture.NotifyAsync(current, "restaurant-B", "B-current");

        Assert.Equal(new[] { "A-before-logout", "B-current" }, received);
    }

    [Fact]
    public async Task AnonymousSessionCannotDeliverOrderChangesOrReconnectReads()
    {
        await using var fixture = new Fixture();
        await fixture.AuthenticateAsync("restaurant-A");
        var connection = fixture.Attach("restaurant-A");
        var callbacks = 0;
        fixture.Service.주문상태변경 += _ => { callbacks++; return Task.CompletedTask; };
        fixture.Service.재연결후재조회요청 += () => { callbacks++; return Task.CompletedTask; };
        await fixture.Session.ClearAsync();

        await fixture.CallAsync("Publish주문상태변경Async", connection, "restaurant-A",
            new 음식점주문상태변경알림 { 주문번호 = "A-old" });
        await fixture.CallAsync("Publish재연결후재조회Async", connection, "restaurant-A");

        Assert.Equal(0, callbacks);
    }

    [Fact]
    public async Task DelayedPreviousClosedPublicationCannotReachNewSessionSubscribers()
    {
        await using var fixture = new Fixture();
        await fixture.AuthenticateAsync("restaurant-A");
        var previous = fixture.Attach("restaurant-A");
        var laterSubscriberStates = new List<음식점실시간연결상태>();
        fixture.Service.상태변경 += async change =>
        {
            if (change.상태 != 음식점실시간연결상태.연결끊김) return;
            await fixture.AuthenticateAsync("restaurant-B");
            var current = fixture.Attach("restaurant-B");
            await fixture.PublishStateAsync(음식점실시간연결상태.연결됨, current, "restaurant-B");
        };
        fixture.Service.상태변경 += change => { laterSubscriberStates.Add(change.상태); return Task.CompletedTask; };

        await fixture.PublishStateAsync(음식점실시간연결상태.연결끊김, previous, "restaurant-A");

        Assert.Equal(음식점실시간연결상태.연결됨, fixture.Service.연결상태);
        Assert.Equal(new[] { 음식점실시간연결상태.연결됨 }, laterSubscriberStates);
        Assert.Equal("restaurant-B", fixture.Owner);
    }

    [Fact]
    public async Task LastAuthenticationFailureSubscriberCanReplaceSessionWithoutRejectingItsNewOwner()
    {
        await using var fixture = new Fixture();
        fixture.Attach("restaurant-A");
        fixture.Service.상태변경 += async change =>
        {
            if (change.상태 != 음식점실시간연결상태.인증필요) return;
            await fixture.AuthenticateAsync("restaurant-B");
            var current = fixture.Attach("restaurant-B");
            await fixture.PublishStateAsync(음식점실시간연결상태.연결됨, current, "restaurant-B");
        };

        await fixture.Service.연결Async().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(fixture.Session.IsAuthenticated);
        Assert.Equal("restaurant-B", fixture.Owner);
        Assert.Equal(음식점실시간연결상태.연결됨, fixture.Service.연결상태);
    }

    [Fact]
    public async Task SubscriberCanDisconnectAndConcurrentDisconnectsRemainIdempotent()
    {
        await using var fixture = new Fixture();
        await fixture.AuthenticateAsync("restaurant-A");
        var connection = fixture.Attach("restaurant-A");
        fixture.Service.주문수신 += _ => fixture.Service.연결해제Async();

        await fixture.NotifyAsync(connection, "restaurant-A", "A-reentrant").WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(fixture.Service.연결해제Async(), fixture.Service.연결해제Async());

        Assert.Null(fixture.Connection);
        Assert.Null(fixture.Owner);
        Assert.Equal(음식점실시간연결상태.연결끊김, fixture.Service.연결상태);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly HttpClient http = new(new NoNetworkHandler());
        private readonly List<HubConnection> connections = [];
        public ClientAuthSession Session { get; } = new(new MemoryStore(), new ClientSessionGuard());
        public 음식점주문SignalRClientService Service { get; }
        public HubConnection? Connection => Get<HubConnection?>("_connection");
        public string? Owner => Get<string?>("_connectionUserId");

        public Fixture()
        {
            http.BaseAddress = new Uri("http://127.0.0.1:5321/");
            Service = new 음식점주문SignalRClientService(Options.Create(new RestaurantDeskOptions()),
                new RestaurantAuthService(http, Session), Session);
        }

        public Task AuthenticateAsync(string userId)
            => Session.ApplyAsync(new ClientAuthTokenSnapshot("synthetic-access", DateTime.UtcNow.AddHours(1),
                "synthetic-refresh", DateTime.UtcNow.AddDays(1), userId, userId, ["음식점"]));

        public HubConnection Attach(string userId)
        {
            var connection = (HubConnection)Method("CreateConnection").Invoke(Service, [userId])!;
            connections.Add(connection);
            Set("_connection", connection);
            Set("_connectionUserId", userId);
            Set("_connectionGeneration", Get<long>("_connectionGeneration") + 1);
            return connection;
        }

        public Task NotifyAsync(HubConnection connection, string userId, string orderNo)
            => CallAsync("Publish주문수신Async", connection, userId, new 음식점주문수신알림 { 주문번호 = orderNo });

        public Task PublishStateAsync(음식점실시간연결상태 state, HubConnection connection, string userId)
            => CallAsync("Publish상태Async", state, "synthetic connection state",
                (Func<bool>)(() => (bool)Method("IsCurrentConnection").Invoke(Service, [connection, userId])!));

        public Task CallAsync(string name, params object?[] args) => (Task)Method(name).Invoke(Service, args)!;
        private static MethodInfo Method(string name) => typeof(음식점주문SignalRClientService).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        private T Get<T>(string name) => (T)Field(name).GetValue(Service)!;
        private void Set(string name, object? value) => Field(name).SetValue(Service, value);
        private static FieldInfo Field(string name) => typeof(음식점주문SignalRClientService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            foreach (var connection in connections) await connection.DisposeAsync();
            http.Dispose();
        }
    }

    private sealed class MemoryStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? snapshot;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default) { snapshot = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { snapshot = null; return Task.CompletedTask; }
    }

    private sealed class NoNetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("No HTTP request is permitted in this fixture.");
    }
}
