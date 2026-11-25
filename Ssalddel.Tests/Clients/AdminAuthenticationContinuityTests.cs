using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Food;
using SsalddelAdminApp.Services;
using SecureStorage = SsalddelAdminApp.Services.SecureStorage;

namespace Ssalddel.Tests.Clients;

/// <summary>
/// 실제 관리자 인증/업무 Client와 세션을 연결합니다. HTTP·장치 저장소만 합성 대역이며,
/// 이 검사는 실제 서버·SecureStorage·APK·휴대폰 검증을 대신하지 않습니다.
/// </summary>
public sealed class AdminAuthenticationContinuityTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private const string Owner = "synthetic-admin-a";
    private const string AccessKey = "ssalddel.admin.access_token";
    private const string RefreshKey = "ssalddel.admin.refresh_token";
    private const string OwnerKey = "ssalddel.admin.user_id";
    private const string ReviewPath = "/api/v1/admin/food-orders/delivery-attempts/synthetic-attempt/interruption-review";

    [Fact]
    public async Task ColdLoginWaitingForRestoreCannotSendAfterLogout()
    {
        using var storage = SecureStorage.UseForTest();
        var next = Token("cold-late-login", "synthetic-admin-b");
        using var fixture = await Fixture.CreateColdAsync((call, _) =>
        {
            Assert.Equal("/api/v1/auth/login", call.Path);
            return Task.FromResult(Json(HttpStatusCode.OK, next));
        });
        using var blocked = BlockAccessRead("initial-access");
        var login = fixture.Auth.LoginAsync(next.UserName, "synthetic-password");
        Task logout;
        try
        {
            await blocked.Started.Task.WaitAsync(Limit);
            logout = fixture.Session.LogoutAsync();
        }
        finally { blocked.Release.TrySetResult(); }
        await logout.WaitAsync(Limit);
        await AssertSupersededLoginAsync(login);
        SecureStorage.Default.AfterGetAsync = null;
        await AssertAnonymousAsync(fixture.Session);
        Assert.Empty(fixture.Handler.Calls);
    }

    [Fact]
    public async Task ColdApiWaitingForRestoreCannotSendUnderReplacementSession()
    {
        using var storage = SecureStorage.UseForTest();
        const string path = "/api/v1/admin/food-orders/operations";
        using var fixture = await Fixture.CreateColdAsync((call, _) =>
        {
            Assert.Equal(path, call.Path);
            return Task.FromResult(Json(HttpStatusCode.OK, new AdminFoodOrderListDto()));
        });
        using var blocked = BlockAccessRead("initial-access");
        var request = fixture.Api.GetAsync<AdminFoodOrderListDto>(path);
        var next = Token("new-login", "synthetic-admin-b");
        Task logout;
        Task replacement;
        try
        {
            await blocked.Started.Task.WaitAsync(Limit);
            logout = fixture.Session.LogoutAsync();
            replacement = fixture.Session.ApplyAsync(next);
        }
        finally { blocked.Release.TrySetResult(); }
        await logout.WaitAsync(Limit);
        await replacement.WaitAsync(Limit);
        Exception? stopped = null;
        try { await request.WaitAsync(Limit); }
        catch (OperationCanceledException ex) { stopped = ex; }
        catch (AdminApiException ex) { stopped = ex; }
        SecureStorage.Default.AfterGetAsync = null;
        await AssertSessionAsync(fixture.Session, next);
        Assert.NotNull(stopped);
        Assert.Empty(fixture.Handler.Calls);
    }

    [Fact]
    public async Task PublicRefreshQueuedForOldSessionCannotRefreshReplacementSession()
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        var next = Token("new-expired-login", "synthetic-admin-b");
        next.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            Assert.Equal("/api/v1/auth/refresh", call.Path);
            var body = JsonSerializer.Deserialize<토큰갱신요청>(call.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            if (body.UserId == Owner) return delayed.WaitAsync(ct);
            Assert.Equal(next.UserId, body.UserId);
            return Task.FromResult(Json(HttpStatusCode.OK, Token("unwanted-b-refresh", next.UserId)));
        });
        var originalRefresh = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        Task<string?> queued;
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            queued = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
            Assert.False(queued.IsCompleted);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token("late-a-refresh", Owner))); }
        await ObserveSupersededAsync(originalRefresh);
        var cancelled = false;
        string? error = null;
        try { error = await queued.WaitAsync(Limit); }
        catch (OperationCanceledException) { cancelled = true; }
        await AssertRefreshRequiredSessionAsync(fixture.Session, next);
        Assert.True(cancelled || error is not null);
        var only = Assert.Single(fixture.Handler.Calls);
        var original = JsonSerializer.Deserialize<토큰갱신요청>(only.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(Owner, original.UserId);
    }

    [Fact]
    public async Task ColdSavedSessionCanRestoreAndStartItsCurrentLogin()
    {
        using var storage = SecureStorage.UseForTest();
        var next = Token("cold-current-login", "synthetic-admin-b");
        using var fixture = await Fixture.CreateColdAsync((call, _) =>
        {
            Assert.Equal("/api/v1/auth/login", call.Path);
            return Task.FromResult(Json(HttpStatusCode.OK, next));
        });
        Assert.Empty(fixture.Session.UserId);
        Assert.Equal("initial-access", await SecureStorage.Default.GetAsync(AccessKey).WaitAsync(Limit));
        Assert.True((await fixture.Auth.LoginAsync(next.UserName, "synthetic-password").WaitAsync(Limit)).Succeeded);
        await AssertSessionAsync(fixture.Session, next);
        Assert.Single(fixture.Handler.Calls);
    }

    [Fact]
    public async Task LoginSavingTokensStillSucceedsWhenSameOwnersOrdinaryRefreshWins()
    {
        using var storage = SecureStorage.UseForTest();
        var loginToken = Token("login-a-saving", Owner);
        var refreshed = Token("same-a-refreshed", Owner);
        var refreshObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = await Fixture.CreateAsync((call, _) =>
        {
            if (call.Path == "/api/v1/auth/login") return Task.FromResult(Json(HttpStatusCode.OK, loginToken));
            Assert.Equal("/api/v1/auth/refresh", call.Path);
            refreshObserved.TrySetResult();
            // 작은 메모리 응답은 현재 토큰 적용 뒤 저장 gate에서만 대기하게 합니다.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(refreshed), System.Text.Encoding.UTF8, "application/json")
            });
        });
        using var blocked = new StorageBarrier();
        var blockedOnce = 0;
        SecureStorage.Default.BeforeSetAsync = (key, value) =>
            key == AccessKey && value == loginToken.AccessToken && Interlocked.Exchange(ref blockedOnce, 1) == 0
                ? blocked.WaitAsync() : Task.CompletedTask;
        var login = fixture.Auth.LoginAsync(Owner, "synthetic-password");
        Task<string?> refresh;
        try
        {
            await blocked.Started.Task.WaitAsync(Limit);
            refresh = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
            await refreshObserved.Task.WaitAsync(Limit);
            Assert.Equal(refreshed.AccessToken, fixture.Session.AccessToken);
            Assert.False(refresh.IsCompleted);
        }
        finally { blocked.Release.TrySetResult(); }
        var loginResult = await login.WaitAsync(Limit);
        var refreshError = await refresh.WaitAsync(Limit);
        SecureStorage.Default.BeforeSetAsync = null;
        await AssertSessionAsync(fixture.Session, refreshed);
        Assert.True(loginResult.Succeeded);
        Assert.Null(refreshError);
        Assert.Equal(2, fixture.Handler.Calls.Count);
    }

    [Fact]
    public async Task AnonymousLoginSuccessCannotRestoreSessionLoggedOutAfterRequest()
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            Assert.Equal("/api/v1/auth/login", call.Path);
            return delayed.WaitAsync(ct);
        });
        await fixture.Session.LogoutAsync().WaitAsync(Limit);
        var login = fixture.Auth.LoginAsync(Owner, "synthetic-password");
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            await fixture.Session.LogoutAsync().WaitAsync(Limit);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token("late-login", Owner))); }
        await AssertSupersededLoginAsync(login);
        await AssertAnonymousAsync(fixture.Session);
        Assert.Single(fixture.Handler.Calls);
    }

    [Fact]
    public async Task OlderLoginSuccessCannotReplaceLaterSuccessfulLogin()
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        var next = Token("new-login", "synthetic-admin-b");
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            Assert.Equal("/api/v1/auth/login", call.Path);
            var body = JsonSerializer.Deserialize<로그인요청>(call.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            if (body.UserNameOrEmail == Owner) return delayed.WaitAsync(ct);
            Assert.Equal(next.UserName, body.UserNameOrEmail);
            return Task.FromResult(Json(HttpStatusCode.OK, next));
        });
        await fixture.Session.LogoutAsync().WaitAsync(Limit);
        var earlier = fixture.Auth.LoginAsync(Owner, "synthetic-password");
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            Assert.True((await fixture.Auth.LoginAsync(next.UserName, "synthetic-password").WaitAsync(Limit)).Succeeded);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token("late-login", Owner))); }
        await AssertSupersededLoginAsync(earlier);
        await AssertSessionAsync(fixture.Session, next);
        Assert.Equal(2, fixture.Handler.Calls.Count);
        Assert.All(fixture.Handler.Calls, call => Assert.Equal("/api/v1/auth/login", call.Path));
    }

    [Fact]
    public async Task OlderSuccessfulGetBodyCannotReturnAfterAuthenticationChanges()
    {
        using var storage = SecureStorage.UseForTest();
        using var blocked = new StorageBarrier();
        const string path = "/api/v1/admin/food-orders/operations";
        var oldResult = new AdminFoodOrderListDto
        {
            TotalCount = 1,
            Items = [new AdminFoodOrderListItemDto { OrderNo = "SYNTHETIC-OLD-OWNER-ORDER" }]
        };
        using var fixture = await Fixture.CreateAsync((call, _) =>
        {
            Assert.Equal(path, call.Path);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new DelayedJsonContent(JsonSerializer.SerializeToUtf8Bytes(oldResult), blocked)
            });
        });
        var request = fixture.Api.GetAsync<AdminFoodOrderListDto>(path);
        var next = Token("new-login", "synthetic-admin-b");
        try
        {
            await blocked.Started.Task.WaitAsync(Limit);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { blocked.Release.TrySetResult(); }
        AdminFoodOrderListDto? returned = null;
        Exception? stopped = null;
        try { returned = await request.WaitAsync(Limit); }
        catch (OperationCanceledException ex) { stopped = ex; }
        catch (AdminApiException ex) { stopped = ex; }
        await AssertSessionAsync(fixture.Session, next);
        Assert.NotNull(stopped);
        Assert.Null(returned);
        var call = Assert.Single(fixture.Handler.Calls);
        Assert.Equal(HttpMethod.Get, call.Method);
        Assert.Equal("initial-access", call.Bearer);
        Assert.DoesNotContain(fixture.Handler.Calls, item => item.Path == "/api/v1/auth/refresh");
    }

    [Fact]
    public async Task RequestQueuedBehindRefreshCannotRefreshAnotherOwnersExpiredSession()
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        const string path = "/api/v1/admin/food-orders/operations";
        var next = Token("new-expired-login", "synthetic-admin-b");
        next.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            if (call.Path == "/api/v1/auth/refresh")
            {
                var body = JsonSerializer.Deserialize<토큰갱신요청>(call.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                if (body.UserId == Owner) return delayed.WaitAsync(ct);
                Assert.Equal(next.UserId, body.UserId);
                return Task.FromResult(Json(HttpStatusCode.OK, Token("unwanted-b-refresh", next.UserId)));
            }
            Assert.Equal(path, call.Path);
            return Task.FromResult(Json(HttpStatusCode.OK, new AdminFoodOrderListDto()));
        });
        var originalRefresh = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        Task<AdminFoodOrderListDto> request;
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            // 직접 호출은 현재 A 문맥을 캡처한 뒤 이미 점유한 refreshGate에서 대기합니다.
            request = fixture.Api.GetAsync<AdminFoodOrderListDto>(path);
            Assert.False(request.IsCompleted);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token("late-a-refresh", Owner))); }
        await ObserveSupersededAsync(originalRefresh);
        Exception? stopped = null;
        try { await request.WaitAsync(Limit); }
        catch (OperationCanceledException ex) { stopped = ex; }
        catch (AdminApiException ex) { stopped = ex; }
        await AssertRefreshRequiredSessionAsync(fixture.Session, next);
        Assert.NotNull(stopped);
        var only = Assert.Single(fixture.Handler.Calls);
        Assert.Equal("/api/v1/auth/refresh", only.Path);
        var original = JsonSerializer.Deserialize<토큰갱신요청>(only.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(Owner, original.UserId);
        Assert.DoesNotContain(fixture.Handler.Calls, call => call.Path == path);
    }

    [Fact]
    public async Task PendingLoginSurvivesCurrentOwnersOrdinaryTokenRefresh()
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        var refreshed = Token("current-a-refreshed", Owner);
        var next = Token("new-login", "synthetic-admin-b");
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            if (call.Path == "/api/v1/auth/login") return delayed.WaitAsync(ct);
            Assert.Equal("/api/v1/auth/refresh", call.Path);
            return Task.FromResult(Json(HttpStatusCode.OK, refreshed));
        });
        var login = fixture.Auth.LoginAsync(next.UserName, "synthetic-password");
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            Assert.Null(await fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true).WaitAsync(Limit));
            await AssertSessionAsync(fixture.Session, refreshed);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, next)); }
        Assert.True((await login.WaitAsync(Limit)).Succeeded);
        await AssertSessionAsync(fixture.Session, next);
        Assert.Equal(2, fixture.Handler.Calls.Count);
        Assert.Single(fixture.Handler.Calls, call => call.Path == "/api/v1/auth/login");
        Assert.Single(fixture.Handler.Calls, call => call.Path == "/api/v1/auth/refresh");
    }

    [Fact]
    public async Task OlderLoginSavingTokensCannotSucceedAfterNewerLoginIntent()
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        var next = Token("new-login", "synthetic-admin-b");
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            Assert.Equal("/api/v1/auth/login", call.Path);
            var body = JsonSerializer.Deserialize<로그인요청>(call.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            if (body.UserNameOrEmail == Owner) return Task.FromResult(Json(HttpStatusCode.OK, Token("login-a-saving", Owner)));
            Assert.Equal(next.UserName, body.UserNameOrEmail);
            return delayed.WaitAsync(ct);
        });
        await fixture.Session.LogoutAsync().WaitAsync(Limit);
        using var blocked = BlockAccessWrite("login-a-saving");
        var earlier = fixture.Auth.LoginAsync(Owner, "synthetic-password");
        Task<AdminLoginResult> later;
        AdminLoginResult? earlierResult = null;
        var earlierCancelled = false;
        try
        {
            await blocked.Started.Task.WaitAsync(Limit);
            later = fixture.Auth.LoginAsync(next.UserName, "synthetic-password");
            await delayed.Started.Task.WaitAsync(Limit);
            blocked.Release.TrySetResult();
            try { earlierResult = await earlier.WaitAsync(Limit); }
            catch (OperationCanceledException) { earlierCancelled = true; }
        }
        finally
        {
            blocked.Release.TrySetResult();
            delayed.Complete(Json(HttpStatusCode.OK, next));
        }
        var laterResult = await later.WaitAsync(Limit);
        SecureStorage.Default.BeforeSetAsync = null;
        await AssertSessionAsync(fixture.Session, next);
        Assert.True(earlierCancelled || earlierResult?.Succeeded == false);
        Assert.True(laterResult.Succeeded);
        Assert.Equal(2, fixture.Handler.Calls.Count);
    }

    [Fact]
    public async Task LateRefreshSuccessCannotRestoreLoggedOutSession()
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            Assert.Equal("/api/v1/auth/refresh", call.Path);
            return delayed.WaitAsync(ct);
        });
        var refresh = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            await fixture.Session.LogoutAsync().WaitAsync(Limit);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token("late-refresh", Owner))); }
        await ObserveSupersededAsync(refresh);
        await AssertAnonymousAsync(fixture.Session);
        Assert.Single(fixture.Handler.Calls);
    }

    [Theory]
    [InlineData("synthetic-admin-b")]
    [InlineData(Owner)]
    public async Task LateRefreshSuccessCannotReplaceNewSessionEvenForSameOwner(string nextOwner)
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        using var fixture = await Fixture.CreateAsync((_, ct) => delayed.WaitAsync(ct));
        var refresh = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        var next = Token("new-login", nextOwner);
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token("late-refresh", Owner))); }
        await ObserveSupersededAsync(refresh);
        await AssertSessionAsync(fixture.Session, next);
        Assert.Single(fixture.Handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "synthetic-admin-b")]
    [InlineData(HttpStatusCode.Unauthorized, "synthetic-admin-b")]
    [InlineData(HttpStatusCode.BadRequest, Owner)]
    [InlineData(HttpStatusCode.Unauthorized, Owner)]
    public async Task LateRefreshRejectionCannotClearNewSession(HttpStatusCode status, string nextOwner)
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        using var fixture = await Fixture.CreateAsync((_, ct) => delayed.WaitAsync(ct));
        var refresh = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        var next = Token("new-login", nextOwner);
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { delayed.Complete(Json(status, new { errorCode = "SyntheticRejectedRefresh" })); }
        await ObserveSupersededAsync(refresh);
        await AssertSessionAsync(fixture.Session, next);
        Assert.Single(fixture.Handler.Calls);
    }

    [Theory]
    [InlineData("synthetic-admin-b")]
    [InlineData(Owner)]
    public async Task OldBusinessUnauthorizedCannotRefreshOrReplayUnderNewSession(string nextOwner)
    {
        using var storage = SecureStorage.UseForTest();
        using var delayed = new DelayedResponse();
        var next = Token("new-login", nextOwner);
        var reviewCalls = 0;
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            if (call.Path == "/api/v1/auth/refresh") return Task.FromResult(Json(HttpStatusCode.OK, next));
            Assert.Equal(ReviewPath, call.Path);
            return ++reviewCalls == 1
                ? delayed.WaitAsync(ct)
                : Task.FromResult(Json(HttpStatusCode.OK, new 음식배달시도운영응답 { 시도StableId = "synthetic-attempt", Revision = 8 }));
        });
        var body = new 음식배달중단검토요청
        {
            클라이언트요청Id = Guid.NewGuid(), 예상Revision = 7,
            판정Code = 음식배달중단검토판정Code.보호, 판정사유 = "합성 검증", 악용확정여부 = false
        };
        var request = fixture.Api.PutAsync<음식배달중단검토요청, 음식배달시도운영응답>(ReviewPath, body);
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { delayed.Complete(Json(HttpStatusCode.Unauthorized, new { errorCode = "SyntheticOldTokenRejected" })); }

        Exception? stopped = null;
        try { await request.WaitAsync(Limit); }
        catch (OperationCanceledException ex) { stopped = ex; }
        catch (AdminApiException ex) { stopped = ex; }
        await AssertSessionAsync(fixture.Session, next);
        Assert.NotNull(stopped);
        var call = Assert.Single(fixture.Handler.Calls);
        Assert.Equal(HttpMethod.Put, call.Method);
        Assert.Equal(ReviewPath, call.Path);
        Assert.Equal("initial-access", call.Bearer);
        Assert.DoesNotContain(fixture.Handler.Calls, x => x.Path == "/api/v1/auth/refresh");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task CurrentRefreshRejectionClearsCurrentSessionAndStoredCredentials(HttpStatusCode status)
    {
        using var storage = SecureStorage.UseForTest();
        using var fixture = await Fixture.CreateAsync((_, _) => Task.FromResult(Json(status, new { errorCode = "SyntheticRejectedRefresh" })));
        var error = await fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true).WaitAsync(Limit);
        Assert.NotNull(error);
        await AssertAnonymousAsync(fixture.Session);
        Assert.Single(fixture.Handler.Calls);
    }

    [Fact]
    public async Task CurrentRefreshServerFailurePreservesCurrentSessionAndStoredCredentials()
    {
        using var storage = SecureStorage.UseForTest();
        using var fixture = await Fixture.CreateAsync((_, _) => Task.FromResult(Json(HttpStatusCode.InternalServerError, new { errorCode = "SyntheticTransientFailure" })));
        var error = await fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true).WaitAsync(Limit);
        Assert.NotNull(error);
        await AssertSessionAsync(fixture.Session, Token("initial-access", Owner));
        Assert.Single(fixture.Handler.Calls);
    }

    [Fact]
    public async Task CurrentRefreshSuccessUpdatesCurrentSessionAndStoredCredentials()
    {
        using var storage = SecureStorage.UseForTest();
        var refreshed = Token("refreshed-access", Owner);
        using var fixture = await Fixture.CreateAsync((_, _) => Task.FromResult(Json(HttpStatusCode.OK, refreshed)));
        Assert.Null(await fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true).WaitAsync(Limit));
        await AssertSessionAsync(fixture.Session, refreshed);
        Assert.Single(fixture.Handler.Calls);
    }

    [Fact]
    public async Task LogoutWhileApplyIsSavingLeavesAnonymousMemoryAndStorage()
    {
        using var storage = SecureStorage.UseForTest();
        var session = await CreateSessionAsync();
        using var blocked = BlockAccessWrite("older-apply");
        var applying = session.ApplyAsync(Token("older-apply", Owner));
        Task logout;
        try
        {
            await blocked.Started.Task.WaitAsync(Limit);
            logout = session.LogoutAsync();
        }
        finally { blocked.Release.TrySetResult(); }
        await ObserveSupersededAsync(applying);
        await logout.WaitAsync(Limit);
        SecureStorage.Default.BeforeSetAsync = null;
        await AssertAnonymousAsync(session);
    }

    [Fact]
    public async Task NewApplyWhileOlderApplyIsSavingWinsInMemoryAndStorage()
    {
        using var storage = SecureStorage.UseForTest();
        var session = await CreateSessionAsync();
        using var blocked = BlockAccessWrite("older-apply");
        var applying = session.ApplyAsync(Token("older-apply", Owner));
        var next = Token("new-login", "synthetic-admin-b");
        Task replacement;
        try
        {
            await blocked.Started.Task.WaitAsync(Limit);
            replacement = session.ApplyAsync(next);
        }
        finally { blocked.Release.TrySetResult(); }
        await ObserveSupersededAsync(applying);
        await replacement.WaitAsync(Limit);
        SecureStorage.Default.BeforeSetAsync = null;
        await AssertSessionAsync(session, next);
    }

    [Fact]
    public async Task NewApplyWhileRestoreHasReadOldAccessTokenWinsInMemoryAndStorage()
    {
        using var storage = SecureStorage.UseForTest();
        await CreateSessionAsync();
        var session = new AdminAuthSession(new ClientSessionGuard());
        using var blocked = new StorageBarrier();
        SecureStorage.Default.AfterGetAsync = (key, value) =>
            key == AccessKey && value == "initial-access" ? blocked.WaitAsync() : Task.CompletedTask;
        var restoring = session.RestoreAsync();
        var next = Token("new-login", "synthetic-admin-b");
        Task replacement;
        try
        {
            await blocked.Started.Task.WaitAsync(Limit);
            replacement = session.ApplyAsync(next);
        }
        finally { blocked.Release.TrySetResult(); }
        await ObserveSupersededAsync(restoring);
        await replacement.WaitAsync(Limit);
        SecureStorage.Default.AfterGetAsync = null;
        await AssertSessionAsync(session, next);
    }

    private static StorageBarrier BlockAccessWrite(string token)
    {
        var blocked = new StorageBarrier();
        SecureStorage.Default.BeforeSetAsync = (key, value) =>
            key == AccessKey && value == token ? blocked.WaitAsync() : Task.CompletedTask;
        return blocked;
    }

    private static StorageBarrier BlockAccessRead(string token)
    {
        var blocked = new StorageBarrier();
        SecureStorage.Default.AfterGetAsync = (key, value) =>
            key == AccessKey && value == token ? blocked.WaitAsync() : Task.CompletedTask;
        return blocked;
    }

    private static async Task<AdminAuthSession> CreateSessionAsync()
    {
        var session = new AdminAuthSession(new ClientSessionGuard());
        await session.ApplyAsync(Token("initial-access", Owner)).WaitAsync(Limit);
        return session;
    }

    private static async Task AssertSupersededLoginAsync(Task<AdminLoginResult> pending)
    {
        try { Assert.False((await pending.WaitAsync(Limit)).Succeeded); }
        catch (OperationCanceledException) { /* 이전 로그인 작업의 명시적 취소도 허용합니다. */ }
    }

    private static async Task AssertRefreshRequiredSessionAsync(AdminAuthSession session, 토큰응답 expected)
    {
        var persistedAccess = await SecureStorage.Default.GetAsync(AccessKey).WaitAsync(Limit);
        var persistedRefresh = await SecureStorage.Default.GetAsync(RefreshKey).WaitAsync(Limit);
        var persistedOwner = await SecureStorage.Default.GetAsync(OwnerKey).WaitAsync(Limit);
        var reloaded = new AdminAuthSession(new ClientSessionGuard());
        var restored = await reloaded.RestoreAsync().WaitAsync(Limit);
        Assert.Equal(expected.UserId, session.UserId);
        Assert.Equal(expected.AccessToken, session.AccessToken);
        Assert.Equal(expected.RefreshToken, session.RefreshToken);
        Assert.Equal(ClientAuthSessionRestoreState.RefreshRequired, session.CurrentState);
        Assert.Equal(expected.AccessToken, persistedAccess);
        Assert.Equal(expected.RefreshToken, persistedRefresh);
        Assert.Equal(expected.UserId, persistedOwner);
        Assert.Equal(ClientAuthSessionRestoreState.RefreshRequired, restored);
        Assert.Equal(expected.UserId, reloaded.UserId);
        Assert.Equal(expected.AccessToken, reloaded.AccessToken);
        Assert.Equal(expected.RefreshToken, reloaded.RefreshToken);
    }

    private static async Task ObserveSupersededAsync(Task pending)
    {
        try { await pending.WaitAsync(Limit); }
        catch (OperationCanceledException) { /* 이전 인증 작업의 명시적 취소도 허용합니다. */ }
    }

    private static async Task AssertAnonymousAsync(AdminAuthSession session)
    {
        var persistedAccess = await SecureStorage.Default.GetAsync(AccessKey).WaitAsync(Limit);
        var persistedRefresh = await SecureStorage.Default.GetAsync(RefreshKey).WaitAsync(Limit);
        var persistedOwner = await SecureStorage.Default.GetAsync(OwnerKey).WaitAsync(Limit);
        var reloaded = new AdminAuthSession(new ClientSessionGuard());
        var restored = await reloaded.RestoreAsync().WaitAsync(Limit);
        Assert.Equal(string.Empty, session.UserId);
        Assert.Null(session.AccessToken);
        Assert.Null(session.RefreshToken);
        Assert.False(session.IsServerAdmin);
        Assert.Null(persistedAccess);
        Assert.Null(persistedRefresh);
        Assert.Null(persistedOwner);
        Assert.Equal(ClientAuthSessionRestoreState.Anonymous, restored);
        Assert.Equal(string.Empty, reloaded.UserId);
        Assert.Null(reloaded.AccessToken);
    }

    private static async Task AssertSessionAsync(AdminAuthSession session, 토큰응답 expected)
    {
        var persistedAccess = await SecureStorage.Default.GetAsync(AccessKey).WaitAsync(Limit);
        var persistedRefresh = await SecureStorage.Default.GetAsync(RefreshKey).WaitAsync(Limit);
        var persistedOwner = await SecureStorage.Default.GetAsync(OwnerKey).WaitAsync(Limit);
        var reloaded = new AdminAuthSession(new ClientSessionGuard());
        var restored = await reloaded.RestoreAsync().WaitAsync(Limit);
        Assert.Equal(expected.UserId, session.UserId);
        Assert.Equal(expected.AccessToken, session.AccessToken);
        Assert.Equal(expected.RefreshToken, session.RefreshToken);
        Assert.True(session.IsServerAdmin);
        Assert.Equal(expected.AccessToken, persistedAccess);
        Assert.Equal(expected.RefreshToken, persistedRefresh);
        Assert.Equal(expected.UserId, persistedOwner);
        Assert.Equal(ClientAuthSessionRestoreState.Authenticated, restored);
        Assert.Equal(expected.UserId, reloaded.UserId);
        Assert.Equal(expected.AccessToken, reloaded.AccessToken);
        Assert.Equal(expected.RefreshToken, reloaded.RefreshToken);
        Assert.True(reloaded.IsServerAdmin);
    }

    private static 토큰응답 Token(string access, string owner) => new()
    {
        AccessToken = access, RefreshToken = "refresh-" + access,
        AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
        RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1),
        UserId = owner, UserName = owner, Roles = ["서버관리자"]
    };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T body)
        => new(status) { Content = JsonContent.Create(body) };

    // HttpClient의 기본 본문 버퍼링을 지연합니다. 실제 네트워크/장치 입력은 없습니다.
    private sealed class DelayedJsonContent(byte[] payload, StorageBarrier barrier) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await barrier.WaitAsync();
            await stream.WriteAsync(payload.AsMemory());
        }
        protected override bool TryComputeLength(out long length)
        {
            length = payload.Length;
            return true;
        }
    }

    private sealed class StorageBarrier : IDisposable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync()
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(Limit);
        }
        public void Dispose() => Release.TrySetResult();
    }

    private sealed class DelayedResponse : IDisposable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource<HttpResponseMessage> response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<HttpResponseMessage> WaitAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            return await response.Task.WaitAsync(Limit, ct);
        }
        public void Complete(HttpResponseMessage value) => response.TrySetResult(value);
        public void Dispose() => response.TrySetCanceled();
    }

    private sealed record Call(HttpMethod Method, string Path, string? Bearer, string? Body);

    private sealed class Handler(Func<Call, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly List<Call> calls = [];
        public IReadOnlyList<Call> Calls { get { lock (calls) return calls.ToArray(); } }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var call = new Call(request.Method, request.RequestUri!.AbsolutePath,
                request.Headers.Authorization?.Parameter,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
            lock (calls) calls.Add(call);
            return await respond(call, ct);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public Handler Handler { get; }
        private HttpClient Http { get; }
        public AdminAuthSession Session { get; }
        public AdminAuthService Auth { get; }
        public AdminAuthenticatedApiClient Api { get; }
        private Fixture(Handler handler, HttpClient http, AdminAuthSession session)
        {
            Handler = handler; Http = http; Session = session;
            Auth = new AdminAuthService(http, session);
            Api = new AdminAuthenticatedApiClient(http, session, Auth);
        }
        public static async Task<Fixture> CreateAsync(Func<Call, CancellationToken, Task<HttpResponseMessage>> response)
        {
            var handler = new Handler(response);
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://fixture.invalid/") };
            return new(handler, http, await CreateSessionAsync());
        }
        public static async Task<Fixture> CreateColdAsync(Func<Call, CancellationToken, Task<HttpResponseMessage>> response)
        {
            await CreateSessionAsync();
            var handler = new Handler(response);
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://fixture.invalid/") };
            return new(handler, http, new AdminAuthSession(new ClientSessionGuard()));
        }
        public void Dispose() => Http.Dispose();
    }
}
