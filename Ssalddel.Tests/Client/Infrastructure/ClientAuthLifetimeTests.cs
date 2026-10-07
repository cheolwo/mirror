using System.Net;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using OrdererApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;
using WarehouseManagerApp.Services;
using WarehouseManagerApp.ViewModels.Warehouse;

namespace Ssalddel.Tests.Client.Infrastructure;

public sealed class ClientAuthLifetimeTests
{
    [Fact]
    public async Task 복원후시간이흐르면_만료를다시판단한다()
    {
        var clock = new Clock(DateTimeOffset.UtcNow);
        var session = new ClientAuthSession(new Store(), new ClientSessionGuard(), clock);
        await session.ApplyAsync(Snapshot(clock.GetUtcNow().UtcDateTime));
        Assert.True(session.IsAuthenticated);
        clock.Now = clock.Now.AddMinutes(31);
        Assert.False(session.IsAuthenticated);
        Assert.Equal(ClientAuthSessionRestoreState.RefreshRequired, await session.RestoreAsync());
    }

    [Fact]
    public async Task 동시갱신은_한번만HTTP를보낸다()
    {
        var session = await ExpiredSession();
        var count = 0;
        var api = Auth(session, _ => { Interlocked.Increment(ref count); return Task.FromResult(Token()); });
        var result = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => api.GetAccessTokenAsync()));
        Assert.All(result, token => Assert.Equal("new-access", token));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task 갱신응답이로그아웃보다늦어도_세션이되살아나지않는다()
    {
        var session = await ExpiredSession();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = Auth(session, _ => { started.SetResult(); return reply.Task; });
        var refresh = api.GetAccessTokenAsync();
        await started.Task;
        await session.ClearAsync();
        reply.SetResult(Token());
        Assert.Null(await refresh);
        Assert.Null(session.UserId);
    }

    [Fact]
    public async Task 늦은로그인응답은_새계정을덮어쓰지않는다()
    {
        var session = new ClientAuthSession(new Store(), new ClientSessionGuard());
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var api = Auth(session, _ => Interlocked.Increment(ref count) == 1 ? WaitFirst() : Task.FromResult(Token("new-owner")));
        Task<HttpResponseMessage> WaitFirst() { firstStarted.SetResult(); return firstReply.Task; }
        var first = api.LoginAsync("old-owner", "test-only");
        await firstStarted.Task;
        Assert.True((await api.LoginAsync("new-owner", "test-only")).IsSuccess);
        firstReply.SetResult(Token("old-owner"));
        Assert.False((await first).IsSuccess);
        Assert.Equal("new-owner", session.UserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 역할복원Wrapper의_늦은거절은_새계정을지우지않는다(bool warehouse)
    {
        var session = await ExpiredSession();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var http = Http(_ => { started.SetResult(); return reply.Task; });
        Task restore = warehouse
            ? new 창고로그인ViewModel(session, new WarehouseAuthApiService(http, session), new WarehouseAccessPolicyService()).초기화Async()
            : new OrdererSessionService(session, new OrdererAuthApiService(http, session), new PendingStore()).복원Async();
        await started.Task;
        await session.ApplyAsync(Snapshot(DateTime.UtcNow) with { UserId = "new-owner" });
        reply.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        await restore;
        Assert.Equal("new-owner", session.UserId);
        Assert.True(session.IsAuthenticated);
    }

    [Fact]
    public async Task 늦은로그아웃정리는_새소유자의제출기록을지우지않는다()
    {
        var session = new ClientAuthSession(new Store(), new ClientSessionGuard());
        await session.ApplyAsync(Snapshot(DateTime.UtcNow));
        var pending = new PendingStore { BlockLoad = true };
        var service = new OrdererSessionService(session, new OrdererAuthApiService(Http(_ => throw new InvalidOperationException()), session), pending);
        var logout = service.로그아웃Async();
        await pending.Started.Task;
        await session.ApplyAsync(Snapshot(DateTime.UtcNow) with { UserId = "new-owner" });
        pending.Reply.SetResult(new FoodOrderPendingSubmission("new-owner", new 음식주문등록요청 { 클라이언트요청Id = Guid.NewGuid() }, DateTime.UtcNow));
        await logout;
        Assert.False(pending.Cleared);
        Assert.Equal("new-owner", session.UserId);
    }

    [Fact]
    public async Task 다른소유자의갱신응답은_거부한다()
    {
        var session = await ExpiredSession();
        var api = Auth(session, _ => Task.FromResult(Token("other-owner")));
        Assert.Null(await api.GetAccessTokenAsync());
        Assert.Null(session.UserId);
    }

    [Fact]
    public async Task 인증서버일시실패는_소유자와갱신토큰을보존한다()
    {
        var session = await ExpiredSession();
        var api = Auth(session, _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await Assert.ThrowsAsync<HttpRequestException>(() => api.GetAccessTokenAsync());
        Assert.Equal("owner", session.UserId);
        Assert.Equal("refresh", session.RefreshToken);
    }

    [Fact]
    public async Task 보안저장실패는_새인증을공개하지않는다()
    {
        var session = new ClientAuthSession(new Store { FailSave = true }, new ClientSessionGuard());
        await Assert.ThrowsAsync<IOException>(() => session.ApplyAsync(Snapshot(DateTime.UtcNow)));
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.AccessToken);
    }

    [Theory]
    [InlineData("GET", 2, 1)]
    [InlineData("POST", 1, 0)]
    [InlineData("PUT", 1, 0)]
    [InlineData("DELETE", 1, 0)]
    public async Task 조회만한번갱신재시도하고_업무쓰기는재전송하지않는다(string method, int expectedRequests, int expectedRefresh)
    {
        var sent = new List<string?>();
        var provider = new Provider();
        var http = Http(_ => { sent.Add(_.Headers.Authorization?.Parameter); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); });
        var client = new SsalddelProtectedApiClient(http, new SsalddelIsmsPClientEncryptionService(new NoJs()), provider);
        using var response = await client.SendAsync(new HttpMethod(method), "api/v1/test");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(expectedRequests, sent.Count);
        Assert.Equal(expectedRefresh, provider.RefreshCount);
        Assert.Equal("access", sent[0]);
        if (sent.Count == 2) Assert.Equal("refreshed", sent[1]);
    }

    [Fact]
    public async Task 업무POST전_만료세션을먼저갱신한다()
    {
        var session = await ExpiredSession();
        var sequence = new List<string>();
        var http = Http(request =>
        {
            sequence.Add(request.RequestUri!.AbsolutePath);
            if (request.RequestUri.AbsolutePath.EndsWith("auth/refresh")) return Task.FromResult(Token());
            Assert.Equal("new-access", request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var provider = new OrdererAccessTokenProvider(session, new OrdererAuthApiService(http, session));
        var client = new SsalddelProtectedApiClient(http, new SsalddelIsmsPClientEncryptionService(new NoJs()), provider);
        using var response = await client.PostAsProtectedJsonAsync("api/v1/test", new { RequestId = "one" });
        Assert.Equal(new[] { "/api/v1/auth/refresh", "/api/v1/test" }, sequence);
    }

    [Fact]
    public async Task 이전계정GET의401은_새계정으로재조회하지않는다()
    {
        var provider = new OwnerProvider();
        var calls = 0;
        var http = Http(_ => { calls++; provider.Owner = "new-owner"; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); });
        var client = new SsalddelProtectedApiClient(http, new SsalddelIsmsPClientEncryptionService(new NoJs()), provider);
        using var response = await client.GetAsync("api/v1/old-owner/order");
        Assert.Equal(1, calls);
        Assert.Equal(0, provider.RefreshCount);
    }

    [Fact]
    public async Task 토큰준비중계정이바뀌면_원래업무POST를보내지않는다()
    {
        var provider = new OwnerProvider { ChangeDuringPrepare = true };
        var calls = 0;
        var http = Http(_ => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); });
        var client = new SsalddelProtectedApiClient(http, new SsalddelIsmsPClientEncryptionService(new NoJs()), provider);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsProtectedJsonAsync("api/v1/old-owner/order", new { RequestId = "original" }));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task 보호본문준비중계정이바뀌면_암호화된이전업무를보내지않는다()
    {
        var provider = new OwnerProvider();
        var businessRequests = 0;
        var http = Http(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("public-key"))
            {
                provider.Owner = "new-owner";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new IsmsPClientEncryptionPublicKeyResponse(
                        "fixture", IsmsPTransportEncryptionAlgorithmCode.RsaOaepSha256Aes256Gcm,
                        "fixture-key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)))
                });
            }
            businessRequests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = new SsalddelProtectedApiClient(http, new SsalddelIsmsPClientEncryptionService(new EncryptedJs()), provider);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsProtectedJsonAsync(
            "api/v1/old-owner/order", new ProtectedRequest { Address = "synthetic fixture" }));
        Assert.Equal(0, businessRequests);
    }

    [Fact]
    public async Task 로그아웃시작후취소되어도_진행중저장뒤토큰정리를끝낸다()
    {
        var store = new BlockingStore();
        var session = new ClientAuthSession(store, new ClientSessionGuard());
        var apply = session.ApplyAsync(Snapshot(DateTime.UtcNow));
        await store.SaveStarted.Task;
        using var cancellation = new CancellationTokenSource();
        var clear = session.ClearAsync(cancellation.Token);
        cancellation.Cancel();
        store.SaveReply.SetResult();
        await apply;
        await clear;
        Assert.Null(store.Saved);
        var restarted = new ClientAuthSession(store, new ClientSessionGuard());
        Assert.Equal(ClientAuthSessionRestoreState.Anonymous, await restarted.RestoreAsync());
    }

    private static ClientAuthApiSessionService Auth(ClientAuthSession session, Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        => new(Http(send), session);
    private static HttpClient Http(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        => new(new Handler(send)) { BaseAddress = new Uri("https://local-fixture.test/") };
    private static async Task<ClientAuthSession> ExpiredSession()
    {
        var session = new ClientAuthSession(new Store(), new ClientSessionGuard());
        await session.ApplyAsync(Snapshot(DateTime.UtcNow.AddHours(-1)) with { RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1) });
        return session;
    }
    private static ClientAuthTokenSnapshot Snapshot(DateTime now)
        => new("access", now.AddMinutes(30), "refresh", now.AddDays(1), "owner", "owner", []);
    private static HttpResponseMessage Token(string owner = "owner") => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new 토큰응답 { AccessToken = "new-access", AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            RefreshToken = "new-refresh", RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1), UserId = owner, UserName = owner })
    };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
    private sealed class Store : IClientSecureTokenStore
    {
        public bool FailSave { get; init; }
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<ClientAuthTokenSnapshot?>(null);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
            => FailSave ? Task.FromException(new IOException("private fixture")) : Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class BlockingStore : IClientSecureTokenStore
    {
        public ClientAuthTokenSnapshot? Saved { get; private set; }
        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SaveReply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Saved);
        public async Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { SaveStarted.SetResult(); await SaveReply.Task; Saved = snapshot; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Saved = null; return Task.CompletedTask; }
    }
    private sealed class Provider : ISsalddelAccessTokenProvider
    {
        public string AccessToken => "access";
        public int RefreshCount { get; private set; }
        public Task<string?> RefreshAccessTokenAsync(string? rejectedToken, CancellationToken cancellationToken = default)
        { RefreshCount++; return Task.FromResult<string?>("refreshed"); }
    }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("No encrypted fixture transport expected.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class ProtectedRequest
    {
        [IsmsPProtectedData(PersonalDataFieldKey.DetailedAddress, "fixture")]
        public string Address { get; init; } = string.Empty;
    }
    private sealed class EncryptedJs : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult((TValue)(identifier == "import" ? (object)this
                : new IsmsPEncryptedTransportEnvelope("fixture", "fixture", "key", "nonce", "cipher")));
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class PendingStore : IFoodOrderPendingSubmissionStore
    {
        public bool BlockLoad { get; init; }
        public bool Cleared { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<FoodOrderPendingSubmission?> Reply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<FoodOrderPendingSubmission?> LoadAsync(CancellationToken cancellationToken = default)
        { Started.TrySetResult(); return BlockLoad ? Reply.Task : Task.FromResult<FoodOrderPendingSubmission?>(null); }
        public Task SaveAsync(FoodOrderPendingSubmission snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(Guid requestId, CancellationToken cancellationToken = default) { Cleared = true; return Task.CompletedTask; }
    }
    private sealed class OwnerProvider : ISsalddelAccessTokenProvider
    {
        public string Owner { get; set; } = "owner";
        public bool ChangeDuringPrepare { get; init; }
        public string? AuthenticationOwnerId => Owner;
        public string? AccessToken => "access";
        public int RefreshCount { get; private set; }
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        { if (ChangeDuringPrepare) Owner = "new-owner"; return Task.FromResult<string?>("access"); }
        public Task<string?> RefreshAccessTokenAsync(string? rejectedToken, string? expectedOwnerId, CancellationToken cancellationToken = default)
        { RefreshCount++; return Task.FromResult<string?>("new-access"); }
    }
}
