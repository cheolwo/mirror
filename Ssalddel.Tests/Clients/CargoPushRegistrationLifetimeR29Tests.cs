using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

public sealed class CargoPushRegistrationLifetimeR29Tests
{
    [Fact]
    public async Task 비전경과익명은등록하지않고_복원된계정으로등록한다()
    {
        var auth = new Auth(); var calls = 0;
        using var registration = new DriverForegroundPushRegistration(auth, _ => { ++calls; return Task.CompletedTask; });
        Assert.False(await registration.TryRegisterAsync());
        registration.Resume(); await auth.ClearAsync();
        Assert.False(await registration.TryRegisterAsync()); Assert.Equal(0, calls);
        await auth.ApplyAsync(Snapshot("restored"));
        Assert.True(await registration.TryRegisterAsync()); Assert.Equal(1, calls);
        Assert.Empty(registration.Notice);
    }

    [Fact]
    public async Task 동시요청과성공뒤재조회는_같은수명의한등록을공유한다()
    {
        var pending = Source(); var calls = 0;
        using var registration = new DriverForegroundPushRegistration(new Auth(), _ => { ++calls; return pending.Task; });
        registration.Resume(); var first = registration.TryRegisterAsync();
        var second = registration.TryRegisterAsync();
        Assert.Same(first, second); Assert.Equal(1, calls);
        pending.SetResult(); Assert.True(await first); Assert.True(await second);
        Assert.True(await registration.TryRegisterAsync()); Assert.Equal(1, calls);
        registration.Resume(); Assert.True(await registration.TryRegisterAsync()); Assert.Equal(1, calls);
    }

    [Fact]
    public async Task 현재실패만고정안내를표시하고_원문과토큰은노출하지않는다()
    {
        var calls = 0; var events = 0;
        using var registration = new DriverForegroundPushRegistration(new Auth(), _ =>
        { ++calls; throw new HttpRequestException("raw-exception fixture-token-SECRET"); });
        registration.Changed += () => ++events;
        registration.Resume(); Assert.False(await registration.TryRegisterAsync());
        Assert.Equal(DriverForegroundPushRegistration.FailureNotice, registration.Notice);
        Assert.DoesNotContain("SECRET", registration.Notice); Assert.DoesNotContain("raw-exception", registration.Notice);
        Assert.False(await registration.TryRegisterAsync()); Assert.Equal(1, calls); Assert.Equal(1, events);
        registration.Pause(); Assert.Empty(registration.Notice); Assert.Equal(2, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 전경이탈은UI대기를끝내고_취소무시공급자의늦은결과를표시하지않는다(bool fail)
    {
        var pending = Source(); var token = CancellationToken.None;
        using var registration = new DriverForegroundPushRegistration(new Auth(), value => { token = value; return pending.Task; });
        registration.Resume(); var result = registration.TryRegisterAsync(); registration.Pause();
        Assert.False(await result.WaitAsync(TimeSpan.FromSeconds(2))); Assert.True(token.IsCancellationRequested);
        if (fail) pending.SetException(new HttpRequestException("late private error")); else pending.SetResult();
        Assert.Empty(registration.Notice); Assert.False(await registration.TryRegisterAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 복귀한등록은이전실제작업종료까지직렬화하고_늦은실패로새상태를덮지않는다(bool failOld)
    {
        var previous = Source(); var freshEntered = Source(); var fresh = Source(); var calls = 0;
        using var registration = new DriverForegroundPushRegistration(new Auth(), _ =>
        { if (++calls == 1) return previous.Task; freshEntered.SetResult(); return fresh.Task; });
        registration.Resume(); var old = registration.TryRegisterAsync(); registration.Pause();
        Assert.False(await old.WaitAsync(TimeSpan.FromSeconds(2)));
        registration.Resume(); var current = registration.TryRegisterAsync();
        Assert.Equal(1, calls); Assert.False(current.IsCompleted);
        if (failOld) previous.SetException(new HttpRequestException("old failure")); else previous.SetResult();
        await freshEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); Assert.Equal(2, calls);
        fresh.SetResult(); Assert.True(await current.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Empty(registration.Notice); Assert.True(await registration.TryRegisterAsync()); Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("driver-b", false)]
    [InlineData("driver-b", true)]
    [InlineData("driver-a", false)]
    [InlineData("driver-a", true)]
    public async Task 다른계정과같은기사새세션은_이전결과를폐기하고새등록에결속한다(string owner, bool failOld)
    {
        var auth = new Auth(); var pending = Source(); var freshEntered = Source(); var calls = 0;
        using var registration = new DriverForegroundPushRegistration(auth, _ =>
        { if (++calls == 1) return pending.Task; freshEntered.SetResult(); return Task.CompletedTask; });
        registration.Resume(); var old = registration.TryRegisterAsync();
        await auth.ApplyAsync(Snapshot(owner)); Assert.False(await old.WaitAsync(TimeSpan.FromSeconds(2)));
        var current = registration.TryRegisterAsync(); Assert.Equal(1, calls);
        if (failOld) pending.SetException(new HttpRequestException("previous session")); else pending.SetResult();
        await freshEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); Assert.True(await current);
        Assert.Empty(registration.Notice); Assert.Equal(2, calls); Assert.Equal(1, auth.Subscribers);
    }

    [Fact]
    public async Task 실패후로그아웃은안내를지우고_익명등록이나이전성공을사용하지않는다()
    {
        var auth = new Auth(); var calls = 0;
        using var registration = new DriverForegroundPushRegistration(auth, _ =>
        { ++calls; throw new HttpRequestException("offline"); });
        registration.Resume(); Assert.False(await registration.TryRegisterAsync()); Assert.NotEmpty(registration.Notice);
        await auth.ClearAsync(); Assert.Empty(registration.Notice);
        Assert.False(await registration.TryRegisterAsync()); Assert.Equal(1, calls);
    }

    [Fact]
    public async Task 복귀는실패안내를초기화하고_새수명한번만재확인한다()
    {
        var auth = new Auth(); var calls = 0;
        using var registration = new DriverForegroundPushRegistration(auth, _ => ++calls == 1
            ? Task.FromException(new HttpRequestException("offline")) : Task.CompletedTask);
        registration.Resume(); Assert.False(await registration.TryRegisterAsync());
        registration.Pause(); Assert.Empty(registration.Notice); Assert.Equal(0, auth.Subscribers);
        registration.Resume(); Assert.Equal(1, auth.Subscribers);
        Assert.True(await registration.TryRegisterAsync()); Assert.Empty(registration.Notice); Assert.Equal(2, calls);
        registration.Dispose(); Assert.Equal(0, auth.Subscribers);
    }

    [Fact]
    public void 홈화면은등록실패를운송안내와분리하고_별도연결안내를정리한다()
    {
        var root = FindRoot();
        var code = File.ReadAllText(Path.Combine(root, "DriverApp/NativeDriverHomePage.xaml.cs"));
        var view = File.ReadAllText(Path.Combine(root, "DriverApp/NativeDriverHomePage.xaml"));
        Assert.Contains("_pushRegistration.TryRegisterAsync()", code);
        Assert.DoesNotContain("StatusLabel.Text = $\"푸시 토큰 등록", code);
        Assert.Contains("_isSubscribed && _foreground ? _pushRegistration.Notice : string.Empty", code);
        Assert.Contains("x:Name=\"PushConnectionNoticeLabel\"", view);
        Assert.Contains("_pushRegistration.Pause();", code);
    }

    private static TaskCompletionSource Source() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static ClientAuthTokenSnapshot Snapshot(string owner) => new("fixture-access", DateTime.UtcNow.AddHours(1),
        "fixture-refresh", DateTime.UtcNow.AddDays(1), owner, owner, ["Driver"]);
    private static string FindRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "DriverApp/NativeDriverHomePage.xaml.cs"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class Auth : IAuthSession
    {
        private Action? _changed;
        public int Subscribers { get; private set; }
        public string? AccessToken => IsAuthenticated ? "fixture-access" : null;
        public string? RefreshToken => IsAuthenticated ? "fixture-refresh" : null;
        public DateTime AccessTokenExpiresAtUtc => DateTime.UtcNow.AddHours(1);
        public DateTime RefreshTokenExpiresAtUtc => DateTime.UtcNow.AddDays(1);
        public string? UserId { get; private set; } = "driver-a";
        public string? UserName => UserId;
        public IReadOnlyList<string> Roles => ["Driver"];
        public bool IsAuthenticated => UserId is not null;
        public long Version { get; private set; } = 1;
        public long SessionRevision { get; private set; } = 1;
        public event Action? Changed
        { add { _changed += value; ++Subscribers; } remove { _changed -= value; --Subscribers; } }
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { UserId = snapshot.UserId; ++Version; ++SessionRevision; _changed?.Invoke(); return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { UserId = null; ++Version; ++SessionRevision; _changed?.Invoke(); return Task.CompletedTask; }
        public async Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion,
            CancellationToken cancellationToken = default, bool startsNewSession = false)
        { if (Version != expectedVersion) return false; await ApplyAsync(snapshot, cancellationToken); return true; }
        public async Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default)
        { if (Version != expectedVersion) return false; await ClearAsync(cancellationToken); return true; }
    }
}
