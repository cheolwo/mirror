using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperAuthApiRecoveryTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task 동일사용자재로그인도_이전갱신성공과거절을반영하지않는다(HttpStatusCode status)
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.invalid/") };
        var refreshing = CreateApi(client, fixture.Auth).EnsureAccessTokenAsync(forceRefresh: true);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot(token: "same-user-new-login"));
        var revision = fixture.Auth.SessionRevision;
        handler.Response.SetResult(Response(status));
        Assert.Contains("세션이 변경", await refreshing);
        Assert.Equal("same-user-new-login", fixture.Auth.AccessToken);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
    }

    [Fact]
    public async Task 만료된RefreshToken은_현재세션을종료하고_HTTP갱신은보내지않는다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot() with
        {
            AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-2),
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.invalid/") };
        Assert.Contains("만료", await CreateApi(client, fixture.Auth).EnsureAccessTokenAsync(forceRefresh: true));
        Assert.False(fixture.Auth.IsLoggedIn);
        Assert.Null(fixture.Store.Snapshot);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task 이전세션의갱신성공과거절은_새사용자의세션을덮거나지우지않는다(HttpStatusCode status)
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.invalid/") };
        var api = CreateApi(client, fixture.Auth);
        var refreshing = api.EnsureAccessTokenAsync(forceRefresh: true);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Auth.ClearAsync();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot(user: "shipper-b", token: "b"));
        var revision = fixture.Auth.SessionRevision;
        handler.Response.SetResult(Response(status));

        var error = await refreshing;
        Assert.Contains("세션이 변경", error);
        Assert.Equal("shipper-b", fixture.Auth.UserId);
        Assert.Equal("b", fixture.Auth.AccessToken);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal("shipper-b", fixture.Store.Snapshot?.UserId);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task 로그아웃뒤늦은갱신성공은_사용자를다시로그인시키지않는다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.invalid/") };
        var refreshing = CreateApi(client, fixture.Auth).EnsureAccessTokenAsync(forceRefresh: true);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Auth.ClearAsync();
        handler.Response.SetResult(Response(HttpStatusCode.OK));
        await refreshing;
        Assert.False(fixture.Auth.IsLoggedIn);
        Assert.Null(fixture.Auth.UserId);
        Assert.Null(fixture.Store.Snapshot);
    }

    [Fact]
    public async Task 같은세션의일시서버오류는_로그인정보를보존한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var revision = fixture.Auth.SessionRevision;
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.invalid/") };
        var refreshing = CreateApi(client, fixture.Auth).EnsureAccessTokenAsync(forceRefresh: true);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.Response.SetResult(Response(HttpStatusCode.ServiceUnavailable));
        Assert.NotNull(await refreshing);
        Assert.Equal("shipper-a", fixture.Auth.UserId);
        Assert.Equal("access-a", fixture.Auth.AccessToken);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
    }

    [Fact]
    public async Task 같은사용자정상갱신은_새Token을저장하고_조회판본은유지한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var revision = fixture.Auth.SessionRevision;
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.invalid/") };
        var refreshing = CreateApi(client, fixture.Auth).EnsureAccessTokenAsync(forceRefresh: true);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.Response.SetResult(Response(HttpStatusCode.OK));
        Assert.Null(await refreshing);
        Assert.Equal("refreshed", fixture.Auth.AccessToken);
        Assert.Equal("shipper-a", fixture.Auth.UserId);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal("refreshed", fixture.Store.Snapshot?.AccessToken);
        Assert.Equal(1, handler.Calls);
    }

    private static AuthApiService CreateApi(HttpClient client, IAuthSession auth)
        => new(client, auth,
            new 꾸미기보유권동기화Service(client, auth, new UnusedDecorationStore(), new PlatformCommunityDecorationStateService()),
            new SsalddelMobilePushInstallationClient(client, new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));

    private static HttpResponseMessage Response(HttpStatusCode status)
        => new(status)
        {
            Content = JsonContent.Create(new 토큰응답
            {
                UserId = "shipper-a", UserName = "shipper-a", AccessToken = "refreshed",
                RefreshToken = "refreshed-refresh", AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1), Roles = ["화주"]
            })
        };

    private sealed class DelayedAuthHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpResponseMessage> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/api/v1/auth/refresh", request.RequestUri?.AbsolutePath);
            Calls++;
            Started.SetResult();
            return Response.Task;
        }
    }

    private sealed class UnusedDecorationStore : I꾸미기보유권LocalStore
    {
        public Task<노드스티커보유권동기화Response?> LoadAsync(string userId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("갱신 시험은 꾸미기 저장소를 호출하지 않습니다.");
        public Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("갱신 시험은 꾸미기 저장소를 호출하지 않습니다.");
    }
}
