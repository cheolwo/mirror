using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

public sealed class CargoDriverRouteAuthenticationTests
{
    [Theory]
    [InlineData("login")]
    [InlineData("/login?returnUrl=%2Fdriver%2Ftransports%2Fcurrent")]
    [InlineData("/")]
    [InlineData("community")]
    [InlineData("/common-contents/story")]
    [InlineData("/driver-help")]
    public async Task PublicRoutes_DoNotRestoreOrRequireAuthentication(string route)
    {
        var auth = new Auth { Restore = _ => throw new InvalidOperationException("Must not restore public content.") };
        using var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute(route);

        await state.CheckAsync();

        Assert.Equal(DriverRouteAuthenticationStatus.Public, state.Status);
        Assert.False(state.RequiresAuthentication);
        Assert.Equal(0, auth.RestoreCount);
    }

    [Theory]
    [InlineData("driver/home/summary")]
    [InlineData("/driver/transports/current")]
    [InlineData("/driver/transports/detail/synthetic-order?section=loading")]
    public async Task AnonymousWorkEntry_RequiresIndependentLoginAndPreservesInternalReturnRoute(string route)
    {
        var auth = new Auth();
        using var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute(route);
        Assert.Equal(DriverRouteAuthenticationStatus.Checking, state.Status);

        await state.CheckAsync();
        await state.CheckAsync();

        Assert.Equal(DriverRouteAuthenticationStatus.LoginRequired, state.Status);
        Assert.Null(state.ErrorMessage);
        var expectedRoute = route.StartsWith('/') ? route : "/" + route;
        Assert.Equal(DriverRoutes.LoginFor(expectedRoute), state.LoginRoute);
        Assert.Equal(1, auth.RestoreCount);
    }

    [Fact]
    public async Task RestorePending_DoesNotExposeWorkEvenWhenMemoryHasPreviousUser()
    {
        var pending = NewCompletion();
        var auth = Authenticated();
        auth.Restore = _ => pending.Task;
        using var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute("driver/home/summary");

        var checking = state.CheckAsync();
        Assert.Equal(DriverRouteAuthenticationStatus.Checking, state.Status);
        Assert.Null(state.WorkspaceSessionRevision);
        pending.SetResult();
        await checking;

        Assert.Equal(DriverRouteAuthenticationStatus.Authenticated, state.Status);
        Assert.Equal(auth.SessionRevision, state.WorkspaceSessionRevision);
    }

    [Fact]
    public async Task StorageFailure_IsUnconfirmedAndRetryableWithoutEndingSession()
    {
        var auth = Authenticated();
        var originalRevision = auth.SessionRevision;
        var failed = true;
        auth.Restore = _ => failed
            ? Task.FromException(new InvalidOperationException("private storage failure detail"))
            : Task.CompletedTask;
        using var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute("driver/home/summary");

        await state.CheckAsync();

        Assert.Equal(DriverRouteAuthenticationStatus.Unavailable, state.Status);
        Assert.Contains("다시 시도", state.ErrorMessage);
        Assert.DoesNotContain("private storage", state.ErrorMessage);
        Assert.True(auth.IsAuthenticated);
        Assert.Equal(originalRevision, auth.SessionRevision);
        Assert.Null(state.WorkspaceSessionRevision);

        failed = false;
        await state.CheckAsync();
        Assert.Equal(DriverRouteAuthenticationStatus.Authenticated, state.Status);
        Assert.Null(state.ErrorMessage);
        Assert.Equal(2, auth.RestoreCount);
    }

    [Fact]
    public async Task ServerRejectedSession_RemovesWorkImmediatelyAndRequestsSameRouteAfterLogin()
    {
        var auth = Authenticated();
        using var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute("driver/transports/current");
        await state.CheckAsync();
        var previousWorkspace = state.WorkspaceSessionRevision;

        auth.EndSession();

        Assert.Equal(DriverRouteAuthenticationStatus.LoginRequired, state.Status);
        Assert.NotEqual(previousWorkspace, state.WorkspaceSessionRevision);
        Assert.Equal(DriverRoutes.LoginFor(DriverRoutes.CurrentTransport), state.LoginRoute);
        Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public async Task DifferentAccountLogin_ChangesWorkComponentKeyWithoutReusingPreviousHome()
    {
        var auth = Authenticated();
        using var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute("driver/home/summary");
        await state.CheckAsync();
        var previousWorkspace = state.WorkspaceSessionRevision;

        auth.Login("synthetic-other-driver");

        Assert.Equal(DriverRouteAuthenticationStatus.Authenticated, state.Status);
        Assert.NotEqual(previousWorkspace, state.WorkspaceSessionRevision);
        Assert.Equal(auth.SessionRevision, state.WorkspaceSessionRevision);
    }

    [Fact]
    public async Task TokenRefreshForSameSession_DoesNotRecreateWorkComponent()
    {
        var auth = Authenticated();
        using var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute("driver/home/summary");
        await state.CheckAsync();
        var previousWorkspace = state.WorkspaceSessionRevision;

        auth.RenewToken();

        Assert.Equal(DriverRouteAuthenticationStatus.Authenticated, state.Status);
        Assert.Equal(previousWorkspace, state.WorkspaceSessionRevision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateRestoreAfterNewLogin_CannotReplaceNewSessionState(bool failOldRead)
    {
        var pending = NewCompletion();
        var auth = new Auth { Restore = _ => pending.Task };
        using var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute("driver/home/summary");
        var previousRead = state.CheckAsync();

        auth.Login("synthetic-new-driver");
        var currentWorkspace = state.WorkspaceSessionRevision;
        if (failOldRead) pending.SetException(new InvalidOperationException("old read failed"));
        else pending.SetResult();
        await previousRead;

        Assert.Equal(DriverRouteAuthenticationStatus.Authenticated, state.Status);
        Assert.Equal(currentWorkspace, state.WorkspaceSessionRevision);
        Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public async Task LateRestoreFailureAfterOpeningPublicLogin_DoesNotHideLogin()
    {
        var pending = NewCompletion();
        var auth = new Auth { Restore = _ => pending.Task };
        using var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute("driver/home/summary");
        var previousRead = state.CheckAsync();
        state.SetRoute("login?returnUrl=%2Fdriver%2Fhome%2Fsummary");

        pending.SetException(new InvalidOperationException("old read failed"));
        await previousRead;

        Assert.Equal(DriverRouteAuthenticationStatus.Public, state.Status);
        Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public async Task Disposal_CancelsPendingRestoreAndUnsubscribesSessionChanges()
    {
        var pending = NewCompletion();
        var token = CancellationToken.None;
        var auth = new Auth { Restore = cancellation => { token = cancellation; return pending.Task; } };
        var state = new DriverRouteAuthenticationState(auth);
        state.SetRoute("driver/home/summary");
        var previousRead = state.CheckAsync();
        var notifications = 0;
        state.Changed += () => notifications++;

        state.Dispose();
        auth.Login("synthetic-other-driver");
        pending.SetException(new OperationCanceledException(token));
        await previousRead;

        Assert.True(token.IsCancellationRequested);
        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData("/driver/transports/current?next=https://external.invalid")]
    [InlineData("/driver/transports/\\external.invalid")]
    [InlineData("/driver/home/summary\r\nother")]
    [InlineData("/driver")]
    [InlineData("/DRIVER/home/summary")]
    [InlineData("/driver%2Fhome%2Fsummary")]
    public async Task UnsafeOrUnsupportedReturnText_KeepsAuthenticationGateAndUsesSafeHome(string route)
    {
        using var state = new DriverRouteAuthenticationState(new Auth());
        state.SetRoute(route);
        await state.CheckAsync();

        Assert.Equal(DriverRouteAuthenticationStatus.LoginRequired, state.Status);
        Assert.Equal(DriverRoutes.LoginFor(DriverRoutes.HomeSummary), state.LoginRoute);
    }

    private static TaskCompletionSource NewCompletion()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Auth Authenticated()
    {
        var auth = new Auth();
        auth.Login("synthetic-driver");
        return auth;
    }

    private sealed class Auth : IAuthSession
    {
        public Func<CancellationToken, Task> Restore { get; set; } = _ => Task.CompletedTask;
        public int RestoreCount { get; private set; }
        public string? AccessToken => IsAuthenticated ? "synthetic-token" : null;
        public string? RefreshToken => IsAuthenticated ? "synthetic-refresh" : null;
        public DateTime AccessTokenExpiresAtUtc => DateTime.UtcNow.AddHours(1);
        public DateTime RefreshTokenExpiresAtUtc => DateTime.UtcNow.AddDays(1);
        public string? UserId { get; private set; }
        public string? UserName => UserId;
        public IReadOnlyList<string> Roles => ["Driver"];
        public bool IsAuthenticated => UserId is not null;
        public long Version { get; private set; }
        public long SessionRevision { get; private set; }
        public event Action? Changed;

        public Task RestoreAsync(CancellationToken cancellationToken = default)
        {
            RestoreCount++;
            return Restore(cancellationToken);
        }

        public void Login(string userId)
        {
            UserId = userId;
            Version++;
            SessionRevision++;
            Changed?.Invoke();
        }

        public void EndSession()
        {
            UserId = null;
            Version++;
            SessionRevision++;
            Changed?.Invoke();
        }

        public void RenewToken()
        {
            Version++;
            Changed?.Invoke();
        }

        public Task ClearAsync(CancellationToken cancellationToken = default) { EndSession(); return Task.CompletedTask; }
        public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion, CancellationToken cancellationToken = default, bool startsNewSession = false)
            => throw new NotSupportedException();
        public Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
