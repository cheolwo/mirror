using System.Net;
using System.Net.Http.Json;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 기사 인증 세션의 저장 완료·호출자 취소 경합. 기기 보안 저장·서버 실행 증거는 아닙니다.</summary>
public sealed class CargoDriverTokenCommitCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장완료뒤취소는_저장과메모리판본알림을맞춘뒤_호출자에게알린다(bool tryApply)
    {
        var store = new TokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        await session.ApplyAsync(Snapshot("original"));
        var originalVersion = session.Version;
        var originalRevision = session.SessionRevision;
        var fresh = Snapshot("committed");
        using var cancellation = new CancellationTokenSource();
        store.AfterCommit = cancellation.Cancel;
        var notifications = 0;
        var notificationMatchedCommittedState = false;
        session.Changed += () =>
        {
            notifications++;
            notificationMatchedCommittedState = ReferenceEquals(fresh, store.Snapshot)
                && session.AccessToken == fresh.AccessToken
                && session.RefreshToken == fresh.RefreshToken
                && session.Version == originalVersion + 1;
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ApplyAsync(
            session, fresh, tryApply, originalVersion, cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Same(fresh, store.Snapshot);
        Assert.Equal(fresh.AccessToken, session.AccessToken);
        Assert.Equal(fresh.RefreshToken, session.RefreshToken);
        Assert.Equal(fresh.UserId, session.UserId);
        Assert.Equal(fresh.Roles, session.Roles);
        Assert.Equal(originalVersion + 1, session.Version);
        Assert.Equal(originalRevision + (tryApply ? 0 : 1), session.SessionRevision);
        Assert.True(session.IsAuthenticated);
        Assert.Equal(1, notifications);
        Assert.True(notificationMatchedCommittedState);
        Assert.Equal(2, store.SaveAttempts);

        var independentlyRestored = new AuthSession(store, new ClientSessionGuard());
        await independentlyRestored.RestoreAsync();
        Assert.Equal(independentlyRestored.AccessToken, session.AccessToken);
        Assert.Equal(independentlyRestored.RefreshToken, session.RefreshToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장전취소는_기존저장과메모리판본알림을바꾸지않는다(bool tryApply)
    {
        var store = new TokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        var original = Snapshot("original");
        await session.ApplyAsync(original);
        var version = session.Version;
        var revision = session.SessionRevision;
        var notifications = 0;
        session.Changed += () => notifications++;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ApplyAsync(
            session, Snapshot("not-saved"), tryApply, version, cancellation.Token));

        Assert.Same(original, store.Snapshot);
        Assert.Equal(original.AccessToken, session.AccessToken);
        Assert.Equal(original.RefreshToken, session.RefreshToken);
        Assert.Equal(version, session.Version);
        Assert.Equal(revision, session.SessionRevision);
        Assert.Equal(0, notifications);
        Assert.Equal(1, store.SaveAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장실패는_기존저장과메모리판본알림을보존하고_다음시도할수있다(bool tryApply)
    {
        var store = new TokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        var original = Snapshot("original");
        await session.ApplyAsync(original);
        var version = session.Version;
        var revision = session.SessionRevision;
        var notifications = 0;
        session.Changed += () => notifications++;
        store.SaveFailure = new IOException("synthetic secure-storage failure before commit");

        await Assert.ThrowsAsync<IOException>(() => ApplyAsync(
            session, Snapshot("failed"), tryApply, version, default));

        Assert.Same(original, store.Snapshot);
        Assert.Equal(original.AccessToken, session.AccessToken);
        Assert.Equal(original.RefreshToken, session.RefreshToken);
        Assert.Equal(version, session.Version);
        Assert.Equal(revision, session.SessionRevision);
        Assert.Equal(0, notifications);
        Assert.Equal(2, store.SaveAttempts);

        store.SaveFailure = null;
        var recovered = Snapshot("recovered");
        await ApplyAsync(session, recovered, tryApply, version, default);
        Assert.Same(recovered, store.Snapshot);
        Assert.Equal(recovered.AccessToken, session.AccessToken);
        Assert.Equal(version + 1, session.Version);
        Assert.Equal(revision + (tryApply ? 0 : 1), session.SessionRevision);
        Assert.Equal(1, notifications);
        Assert.Equal(3, store.SaveAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 정상저장은_한번의판본과알림으로_동일사용자로그인과갱신을구분한다(bool tryApply)
    {
        var store = new TokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        await session.ApplyAsync(Snapshot("original"));
        var version = session.Version;
        var revision = session.SessionRevision;
        var notifications = 0;
        session.Changed += () => notifications++;
        var fresh = Snapshot("normal");

        await ApplyAsync(session, fresh, tryApply, version, default);

        Assert.Same(fresh, store.Snapshot);
        Assert.Equal(fresh.AccessToken, session.AccessToken);
        Assert.Equal(fresh.RefreshToken, session.RefreshToken);
        Assert.Equal(version + 1, session.Version);
        Assert.Equal(revision + (tryApply ? 0 : 1), session.SessionRevision);
        Assert.Equal(1, notifications);
        Assert.Equal(2, store.SaveAttempts);
    }

    [Fact]
    public async Task HTTP갱신저장뒤_화면취소후_다음조회는_새토큰을쓰고_갱신을재전송하지않는다()
    {
        var store = new TokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        var original = Snapshot("original") with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5) };
        await session.ApplyAsync(original);
        var version = session.Version;
        var revision = session.SessionRevision;
        var fresh = Snapshot("http-committed");
        using var cancellation = new CancellationTokenSource();
        store.AfterCommit = cancellation.Cancel;
        var refreshCalls = 0;
        var detailCalls = 0;
        using var http = new HttpClient(new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/v1/auth/refresh")
            {
                refreshCalls++;
                Assert.Equal(HttpMethod.Post, request.Method);
                var body = await request.Content!.ReadFromJsonAsync<토큰갱신요청>();
                Assert.Equal(original.RefreshToken, body!.RefreshToken);
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new 토큰응답
                    {
                        AccessToken = fresh.AccessToken,
                        AccessTokenExpiresAtUtc = fresh.AccessTokenExpiresAtUtc,
                        RefreshToken = fresh.RefreshToken,
                        RefreshTokenExpiresAtUtc = fresh.RefreshTokenExpiresAtUtc,
                        UserId = fresh.UserId,
                        UserName = fresh.UserName,
                        Roles = ["Driver"]
                    })
                };
            }

            detailCalls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/v1/driver/transports/42", request.RequestUri.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(fresh.AccessToken, request.Headers.Authorization?.Parameter);
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new 기사운송상세응답 { Id = 42, 운송번호 = "synthetic-transport-42" })
            };
        })) { BaseAddress = new Uri("https://cargo-commit.example.test/") };
        var authApi = new AuthApiService(http, session);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            authApi.EnsureAccessTokenAsync(cancellationToken: cancellation.Token));

        Assert.Equal(fresh.AccessToken, session.AccessToken);
        Assert.Equal(fresh.RefreshToken, session.RefreshToken);
        Assert.Equal(fresh.AccessToken, store.Snapshot!.AccessToken);
        Assert.Equal(version + 1, session.Version);
        Assert.Equal(revision, session.SessionRevision);
        var client = new DriverApiClient(http, session, authApi, new DriverOperatingProfileService());
        var detail = await client.GetAsync<기사운송상세응답>("api/v1/driver/transports/42", "운송 상세 조회");

        Assert.Equal(42, detail!.Id);
        Assert.Equal(1, refreshCalls);
        Assert.Equal(1, detailCalls);
        Assert.Equal(2, store.SaveAttempts);
        Assert.True(session.IsAuthenticated);
    }

    private static Task ApplyAsync(AuthSession session, ClientAuthTokenSnapshot snapshot,
        bool tryApply, long expectedVersion, CancellationToken cancellationToken)
        => tryApply
            ? session.TryApplyAsync(snapshot, expectedVersion, cancellationToken)
            : session.ApplyAsync(snapshot, cancellationToken);

    private static ClientAuthTokenSnapshot Snapshot(string suffix)
        => new($"test-only-access-{suffix}", DateTime.UtcNow.AddHours(1),
            $"test-only-refresh-{suffix}", DateTime.UtcNow.AddDays(1),
            "synthetic-driver-commit", "합성 기사", ["Driver"]);

    private sealed class TokenStore : IClientSecureTokenStore
    {
        public ClientAuthTokenSnapshot? Snapshot { get; private set; }
        public Action? AfterCommit { get; set; }
        public Exception? SaveFailure { get; set; }
        public int SaveAttempts { get; private set; }

        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Snapshot);
        }

        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveAttempts++;
            if (SaveFailure is not null)
                return Task.FromException(SaveFailure);
            Snapshot = snapshot;
            // 보안 저장은 확정 뒤 취소할 수 없고, 완료 반환 전에 화면 취소가 도착할 수 있다.
            AfterCommit?.Invoke();
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Snapshot = null;
            return Task.CompletedTask;
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return send(request);
        }
    }
}
