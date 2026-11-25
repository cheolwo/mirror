using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class 화주서버통신Tests
{
    private const string Untrusted = "server-private-detail-not-for-display";

    [Theory]
    [InlineData("io")]
    [InlineData("http")]
    [InlineData("json")]
    public async Task 갱신후보안저장실패는_내부원문없이안내하고_업무GET을보내지않는다(string kind)
    {
        var saves = 0;
        var store = new ShipperRecoveryTokenStore
        {
            Save = _ => ++saves == 1 ? Task.CompletedTask : Task.FromException(kind switch
            {
                "http" => new HttpRequestException(Untrusted),
                "json" => new System.Text.Json.JsonException(Untrusted),
                _ => (Exception)new IOException(Untrusted)
            })
        };
        await using var f = await Fixture.CreateAsync((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new Ssalddel.Contracts.Common.토큰응답
            {
                UserId = "synthetic-owner-private-query", UserName = "synthetic-owner-private-query",
                AccessToken = "refreshed", RefreshToken = "refreshed-refresh", Roles = ["화주"],
                AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30), RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            })
        }), store: store, requireRefresh: true);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GetRequestsAsync());
        Assert.Equal("화주 인증 상태를 확인하지 못했습니다. 다시 확인해 주세요.", error.Message);
        AssertSafe(error);
        Assert.Null(error.InnerException);
        Assert.Equal(HttpMethod.Post, Assert.Single(f.Handler.Methods));
        Assert.Equal(2, saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 명시적인빈배열만_정상빈운송목록이다(bool publicList)
    {
        await using var f = await Fixture.CreateAsync((_, _) => Task.FromResult(Json("[]")));
        if (publicList) Assert.Empty(await f.Service.GetPublicCargoAsync());
        else Assert.Empty(await f.Service.GetRequestsAsync());
        Assert.Equal(HttpMethod.Get, Assert.Single(f.Handler.Methods));
    }

    [Theory]
    [InlineData("null", false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("{}", false)]
    [InlineData("[null]", false)]
    [InlineData("<html>server-private-detail-not-for-display</html>", false)]
    [InlineData("null", true)]
    [InlineData("", true)]
    [InlineData("{}", true)]
    [InlineData("[null]", true)]
    [InlineData("<html>server-private-detail-not-for-display</html>", true)]
    public async Task 확인할수없는성공본문을_빈목록성공으로바꾸지않는다(string body, bool publicList)
    {
        await using var f = await Fixture.CreateAsync((_, _) => Task.FromResult(Json(body)));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (publicList) await f.Service.GetPublicCargoAsync();
            else await f.Service.GetRequestsAsync();
        });
        Assert.Contains("응답을 확인하지 못했습니다", error.Message);
        AssertSafe(error);
        Assert.Single(f.Handler.Methods);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "로그인")]
    [InlineData(HttpStatusCode.Forbidden, "권한")]
    [InlineData(HttpStatusCode.Conflict, "최신 상태")]
    [InlineData(HttpStatusCode.TooManyRequests, "요청이 많습니다")]
    [InlineData(HttpStatusCode.InternalServerError, "서버에서 요청을 처리하지 못했습니다")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "서버에서 요청을 처리하지 못했습니다")]
    public async Task HTTP거절은_상태별안전문구를사용하고_조회와POST를자동반복하지않는다(HttpStatusCode status, string expected)
    {
        await using var f = await Fixture.CreateAsync((_, _) => Task.FromResult(new HttpResponseMessage(status)
        { Content = new StringContent($"<html>{Untrusted}</html>") }));
        var readError = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GetRequestsAsync());
        var commandError = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.RegisterReceiptAsync("request-1", new() { 인수증번호 = "receipt-1" }));
        foreach (var error in new[] { readError, commandError })
        {
            Assert.Contains(expected, error.Message);
            Assert.Contains($"HTTP {(int)status}", error.Message);
            AssertSafe(error);
        }
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Post }, f.Handler.Methods);
        Assert.True(f.Auth.IsLoggedIn);
    }

    [Fact]
    public async Task 문제JSON의원문과필드도_사용자오류메시지로전달하지않는다()
    {
        await using var f = await Fixture.CreateAsync((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        { Content = JsonContent.Create(new { detail = Untrusted, errors = new { privateAddress = new[] { Untrusted } } }) }));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GetRequestsAsync());
        Assert.Contains("요청 내용을 확인", error.Message);
        Assert.DoesNotContain("privateAddress", error.ToString());
        AssertSafe(error);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task 본문읽기실패는_원문없는연결실패로남고_빈목록이나확정거절로추정하지않는다(HttpStatusCode status)
    {
        await using var f = await Fixture.CreateAsync((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new FailingContent() }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => f.Service.GetRequestsAsync());
        Assert.Null(error.StatusCode); // 응답 버퍼링이 실패하여 정상 HTTP 응답을 전달받기 전입니다.
        AssertSafe(error);
        Assert.Single(f.Handler.Methods);
    }

    [Fact]
    public async Task 전송예외에있는HTTP상태는_안전문구로바꿔도보존한다()
    {
        await using var f = await Fixture.CreateAsync((_, _) => throw new HttpRequestException(Untrusted, null, HttpStatusCode.ServiceUnavailable));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => f.Service.GetRequestsAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Contains("HTTP 503", error.Message);
        AssertSafe(error);
    }

    [Fact]
    public async Task 유한HttpClient시간초과는_사용자취소와구분하고_자동재요청하지않는다()
    {
        await using var f = await Fixture.CreateAsync(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("must not complete");
        }, TimeSpan.FromMilliseconds(100));
        var error = await Assert.ThrowsAsync<TaskCanceledException>(() => f.Service.GetRequestsAsync());
        Assert.Contains("응답 시간이 초과되었습니다", error.Message);
        AssertSafe(error);
        Assert.Single(f.Handler.Methods);
        Assert.True(f.Auth.IsLoggedIn);
    }

    [Fact]
    public async Task 사용자가취소한요청은_시간초과나빈목록으로바꾸지않는다()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var f = await Fixture.CreateAsync(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("must not complete");
        });
        using var cancellation = new CancellationTokenSource();
        var loading = f.Service.GetRequestsAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading);
        Assert.DoesNotContain("응답 시간이 초과되었습니다", error.Message);
        Assert.True(error.CancellationToken.IsCancellationRequested);
        Assert.Single(f.Handler.Methods);
    }

    [Theory]
    [InlineData("transport")]
    [InlineData("json")]
    [InlineData("body")]
    [InlineData("timeout")]
    public async Task POST결과가불명확해도_한번만실행하고_다음GET으로같은의뢰를확인한다(string failure)
    {
        var committed = false;
        await using var f = await Fixture.CreateAsync(async (request, token) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                committed = true;
                if (failure == "transport") throw new HttpRequestException(Untrusted);
                if (failure == "json") return Json($"<html>{Untrusted}</html>");
                if (failure == "body") return new(HttpStatusCode.OK) { Content = new FailingContent() };
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("must not complete");
            }
            Assert.True(committed);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 화주운송의뢰응답 { 의뢰Id = "request-1" }) };
        }, TimeSpan.FromMilliseconds(100));
        var error = await Record.ExceptionAsync(() => f.Service.RegisterReceiptAsync("request-1", new() { 인수증번호 = "receipt-1" }));
        Assert.NotNull(error);
        AssertSafe(error);
        Assert.Equal(HttpMethod.Post, Assert.Single(f.Handler.Methods));

        var canonical = await f.Service.GetRequestForReceiptAsync("request-1");

        Assert.Equal("request-1", canonical?.의뢰Id);
        Assert.Equal(new[] { HttpMethod.Post, HttpMethod.Get }, f.Handler.Methods);
    }

    [Fact]
    public async Task 단건null과접수복구404의_기존없음의미는유지한다()
    {
        await using var f = await Fixture.CreateAsync((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.Contains("by-client-request", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(Untrusted) }
                : Json("null")));
        Assert.Null(await f.Service.GetRequestAsync("request-1"));
        Assert.Null(await f.Service.GetRequestByClientRequestIdAsync("client-1"));
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Get }, f.Handler.Methods);
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static void AssertSafe(Exception error)
    {
        Assert.DoesNotContain(Untrusted, error.ToString());
        Assert.DoesNotContain("shipperId", error.ToString());
        Assert.DoesNotContain("synthetic-owner-private-query", error.ToString());
        Assert.DoesNotContain("api/v1/", error.ToString());
    }

    private sealed class Fixture(HttpClient client, AuthSession auth, Handler handler,
        TransportRequestLedgerRealtimeClient realtime, ServerBackedShipperOperationsService service) : IAsyncDisposable
    {
        internal AuthSession Auth { get; } = auth;
        internal Handler Handler { get; } = handler;
        internal ServerBackedShipperOperationsService Service { get; } = service;
        internal static async Task<Fixture> CreateAsync(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
            TimeSpan? timeout = null, ShipperRecoveryTokenStore? store = null, bool requireRefresh = false)
        {
            var auth = new AuthSession(store ?? new ShipperRecoveryTokenStore(), new ClientSessionGuard());
            var snapshot = ShipperRecoveryFixture.Snapshot(user: "synthetic-owner-private-query");
            if (requireRefresh) snapshot = snapshot with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(10) };
            await auth.ApplyAsync(snapshot);
            var handler = new Handler(respond);
            var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
            if (timeout is { } value) client.Timeout = value;
            var observer = new TransportRequestLedgerObserver();
            var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
            var authApi = new AuthApiService(client, auth,
                new 꾸미기보유권동기화Service(client, auth, new UnusedDecorationStore(), new PlatformCommunityDecorationStateService()),
                new SsalddelMobilePushInstallationClient(client, new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));
            return new(client, auth, handler, realtime, new(client, auth, authApi, observer, realtime));
        }
        public async ValueTask DisposeAsync() { await realtime.DisposeAsync(); client.Dispose(); }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        internal List<HttpMethod> Methods { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Methods.Add(request.Method); return respond(request, cancellationToken); }
    }

    private sealed class FailingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.FromException(new IOException(Untrusted));
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private sealed class UnusedDecorationStore : I꾸미기보유권LocalStore
    {
        public Task<노드스티커보유권동기화Response?> LoadAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
