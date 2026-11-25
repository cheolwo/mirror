using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperAuthApiRecoveryTests
{
    private const string Untrusted = "synthetic-private-refresh-detail";

    [Fact]
    public async Task 갱신commit뒤사용자취소도_보안저장을완료하고_부가Push는시작하지않는다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var revision = fixture.Auth.SessionRevision;
        using var cancellation = new CancellationTokenSource();
        fixture.Auth.Changed += () => { if (fixture.Auth.AccessToken == "refreshed") cancellation.Cancel(); };
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var push = new CountingPushTokenProvider();
        var pending = CreateApi(client, fixture.Auth, push).EnsureAccessTokenAsync(forceRefresh: true,
            cancellationToken: cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.Response.SetResult(Response(HttpStatusCode.OK));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal("refreshed", fixture.Auth.AccessToken);
        Assert.Equal("refreshed", fixture.Store.Snapshot?.AccessToken);
        Assert.Equal("refreshed-refresh", fixture.Store.Snapshot?.RefreshToken);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal(0, push.Calls);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("access")]
    [InlineData("access-expired")]
    [InlineData("access-near-expiry")]
    [InlineData("refresh")]
    [InlineData("refresh-expired")]
    [InlineData("refresh-near-expiry")]
    [InlineData("user")]
    [InlineData("roles")]
    public async Task 잘못된갱신200은_기존Token과보안저장과판본을보존한다(string invalid)
    {
        using var fixture = new ShipperRecoveryFixture();
        var original = ShipperRecoveryFixture.Snapshot();
        await fixture.Auth.ApplyAsync(original);
        var revision = fixture.Auth.SessionRevision;
        var notifications = 0;
        fixture.Auth.Changed += () => notifications++;
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var pending = CreateApi(client, fixture.Auth).EnsureAccessTokenAsync(forceRefresh: true);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var token = Token();
        switch (invalid)
        {
            case "access": token.AccessToken = ""; break;
            case "access-expired": token.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1); break;
            case "access-near-expiry": token.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(90); break;
            case "refresh": token.RefreshToken = ""; break;
            case "refresh-expired": token.RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1); break;
            case "refresh-near-expiry": token.RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(90); break;
            case "user": token.UserId = "other-owner"; break;
            case "roles": token.Roles = null!; break;
        }
        handler.Response.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(token) });

        Assert.Equal("서버 인증 응답을 읽을 수 없습니다.", await pending);
        Assert.Equal(original.AccessToken, fixture.Auth.AccessToken);
        Assert.Equal(original.RefreshToken, fixture.Auth.RefreshToken);
        Assert.Equal(original.Roles, fixture.Auth.Roles);
        Assert.Same(original, fixture.Store.Snapshot);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal(0, notifications);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("transport", "연결")]
    [InlineData("json", "인증 응답")]
    [InlineData("empty", "인증 응답")]
    [InlineData("body", "연결")]
    [InlineData("timeout", "시간이 초과")]
    public async Task 목록조회전갱신실패는_안전안내와원세션을유지하고_다음GET에서복귀한다(string failure, string expected)
    {
        using var fixture = new ShipperRecoveryFixture();
        var original = ShipperRecoveryFixture.Snapshot() with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        await fixture.Auth.ApplyAsync(original);
        var revision = fixture.Auth.SessionRevision;
        using var handler = new RefreshRecoveryHandler(failure);
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        if (failure == "timeout") client.Timeout = TimeSpan.FromMilliseconds(100);
        var api = CreateApi(client, fixture.Auth);
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress,
            _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(client, fixture.Auth, api, observer, realtime);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetRequestsAsync());
        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain(Untrusted, error.ToString());
        Assert.DoesNotContain("test.invalid", error.ToString());
        Assert.Equal(original.AccessToken, fixture.Auth.AccessToken);
        Assert.Equal(original.RefreshToken, fixture.Auth.RefreshToken);
        Assert.Same(original, fixture.Store.Snapshot);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal(new[] { HttpMethod.Post }, handler.Methods);

        Assert.Empty(await service.GetRequestsAsync());
        Assert.Equal("refreshed", fixture.Auth.AccessToken);
        Assert.Equal("refreshed-refresh", fixture.Store.Snapshot?.RefreshToken);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal(new[] { HttpMethod.Post, HttpMethod.Post, HttpMethod.Get }, handler.Methods);
    }

    [Fact]
    public async Task 취소비협조갱신200도_사용자취소후Token을저장하지않는다()
    {
        using var fixture = new ShipperRecoveryFixture();
        var original = ShipperRecoveryFixture.Snapshot();
        await fixture.Auth.ApplyAsync(original);
        var revision = fixture.Auth.SessionRevision;
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        using var cancellation = new CancellationTokenSource();
        var pending = CreateApi(client, fixture.Auth).EnsureAccessTokenAsync(forceRefresh: true,
            cancellationToken: cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        handler.Response.SetResult(Response(HttpStatusCode.OK));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(original.AccessToken, fixture.Auth.AccessToken);
        Assert.Same(original, fixture.Store.Snapshot);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("json")]
    [InlineData("io")]
    public async Task 갱신주보안저장실패는_전송오류안내로숨기지않는다(string kind)
    {
        using var fixture = new ShipperRecoveryFixture();
        var original = ShipperRecoveryFixture.Snapshot();
        await fixture.Auth.ApplyAsync(original);
        Exception failure = kind switch
        {
            "http" => new HttpRequestException(Untrusted),
            "json" => new JsonException(Untrusted),
            _ => new IOException(Untrusted)
        };
        fixture.Store.Save = _ => Task.FromException(failure);
        using var handler = new DelayedAuthHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var pending = CreateApi(client, fixture.Auth).EnsureAccessTokenAsync(forceRefresh: true);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.Response.SetResult(Response(HttpStatusCode.OK));
        Assert.Same(failure, await Record.ExceptionAsync(() => pending));
        Assert.Equal("refreshed", fixture.Auth.AccessToken); // 기존 메모리 확정/저장 실패 의미를 보존.
        Assert.Same(original, fixture.Store.Snapshot);
        Assert.Equal(1, handler.Calls);
    }

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

    private static AuthApiService CreateApi(HttpClient client, IAuthSession auth,
        ISsalddelMobilePushTokenProvider? push = null)
        => new(client, auth,
            new 꾸미기보유권동기화Service(client, auth, new UnusedDecorationStore(), new PlatformCommunityDecorationStateService()),
            new SsalddelMobilePushInstallationClient(client, push ?? new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));

    private static HttpResponseMessage Response(HttpStatusCode status)
        => new(status)
        {
            Content = JsonContent.Create(Token())
        };

    private static 토큰응답 Token() => new()
    {
        UserId = "shipper-a", UserName = "shipper-a", AccessToken = "refreshed",
        RefreshToken = "refreshed-refresh", AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
        RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1), Roles = ["화주"]
    };

    private sealed class RefreshRecoveryHandler(string failure) : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];
        private int _refreshCalls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            if (request.RequestUri?.AbsolutePath == "/api/v1/shipper/requests")
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
            }
            Assert.Equal("/api/v1/auth/refresh", request.RequestUri?.AbsolutePath);
            Assert.Equal(HttpMethod.Post, request.Method);
            if (++_refreshCalls > 1) return Response(HttpStatusCode.OK);
            if (failure == "transport") throw new HttpRequestException(Untrusted);
            if (failure == "body") return new(HttpStatusCode.OK) { Content = new FailingContent() };
            if (failure == "timeout")
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("timeout handler must not complete normally");
            }
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(failure == "empty" ? "" : "{"+Untrusted,
                    System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FailingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.FromException(new IOException(Untrusted));
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private sealed class CountingPushTokenProvider : ISsalddelMobilePushTokenProvider
    {
        public int Calls { get; private set; }
        public Task<SsalddelMobilePushTokenSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<SsalddelMobilePushTokenSnapshot?>(null);
        }
    }

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
