using System.Globalization;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelAdminApp.Services;

internal readonly record struct AdminAuthenticationContext(
    long OwnerRevision, long Generation, long TokenRevision,
    string UserId, string? AccessToken, string? RefreshToken);

public sealed class AdminAuthSession : ISsalddelAccessTokenProvider
{
    private const string AccessTokenKey = "ssalddel.admin.access_token";
    private const string RefreshTokenKey = "ssalddel.admin.refresh_token";
    private const string ExpiresAtKey = "ssalddel.admin.access_token_expires_at";
    private const string RefreshExpiresAtKey = "ssalddel.admin.refresh_token_expires_at";
    private const string UserIdKey = "ssalddel.admin.user_id";
    private const string UserNameKey = "ssalddel.admin.user_name";
    private const string RolesKey = "ssalddel.admin.roles";
    private readonly IClientSessionGuard sessionGuard;
    private readonly object mutationGate = new();
    private readonly SemaphoreSlim storageGate = new(1, 1);
    private bool restored;
    private long ownerRevision;
    private long authenticationGeneration;
    private long tokenRevision;

    public AdminAuthSession(IClientSessionGuard sessionGuard) => this.sessionGuard = sessionGuard;
    public event Action? Changed;
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public DateTime RefreshTokenExpiresAtUtc { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string? AuthenticationOwnerId => string.IsNullOrWhiteSpace(UserId) ? null : UserId;
    public string UserName { get; private set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public ClientAuthSessionRestoreState CurrentState
    {
        get
        {
            lock (mutationGate)
            {
                var snapshot = CreateSnapshot();
                if (sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow))
                    return ClientAuthSessionRestoreState.Authenticated;
                return sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow)
                    ? ClientAuthSessionRestoreState.RefreshRequired : ClientAuthSessionRestoreState.Anonymous;
            }
        }
    }

    public bool IsLoggedIn => CurrentState == ClientAuthSessionRestoreState.Authenticated;
    public bool IsServerAdmin => IsLoggedIn && Roles.Contains("서버관리자", StringComparer.Ordinal);

    internal AdminAuthenticationContext CaptureAuthentication()
    { lock (mutationGate) return CaptureContext(); }

    internal bool TryCaptureAuthenticationAfterRestore(AdminAuthenticationContext expected, out AdminAuthenticationContext context)
    {
        lock (mutationGate)
        {
            context = CaptureContext();
            // 장치 복원은 계정 값을 채울 수 있지만, 대기 중 새 로그인·로그아웃은 지나가지 못합니다.
            return ownerRevision == expected.OwnerRevision && authenticationGeneration == expected.Generation;
        }
    }

    internal AdminAuthenticationContext BeginAuthenticationChange()
    {
        lock (mutationGate)
        {
            authenticationGeneration++;
            return CaptureContext();
        }
    }

    internal bool IsSameAuthentication(AdminAuthenticationContext expected)
    { lock (mutationGate) return SameAuthentication(expected); }

    internal bool IsCurrentAuthenticationContext(AdminAuthenticationContext expected)
    { lock (mutationGate) return CurrentContext(expected); }

    public async Task<ClientAuthSessionRestoreState> RestoreAsync(CancellationToken cancellationToken = default)
    {
        AdminAuthenticationContext context;
        lock (mutationGate)
        {
            if (restored) return CurrentState;
            context = CaptureContext();
        }
        await storageGate.WaitAsync(cancellationToken);
        var published = false;
        AdminAuthenticationContext publishedContext = default;
        try
        {
            lock (mutationGate)
                if (restored || !CurrentContext(context)) return CurrentState;
            ClientAuthTokenSnapshot? snapshot;
            try { snapshot = await LoadSnapshotAsync(); }
            catch { snapshot = null; }
            cancellationToken.ThrowIfCancellationRequested();
            lock (mutationGate)
            {
                if (!CurrentContext(context)) return CurrentState;
                restored = true;
                if (snapshot is not null) ApplySnapshot(snapshot);
                if (CurrentState == ClientAuthSessionRestoreState.Anonymous) ClearState();
                publishedContext = CaptureContext();
                published = true;
            }
        }
        finally { storageGate.Release(); }
        if (published && IsCurrentAuthenticationContext(publishedContext)) Changed?.Invoke();
        return CurrentState;
    }

    public async Task ApplyAsync(토큰응답 response)
        => await ApplyCoreAsync(response, expected: null, startsNewSession: true);

    internal Task<bool> TryApplyAuthenticationAsync(토큰응답 response, AdminAuthenticationContext expected)
        => ApplyCoreAsync(response, expected, startsNewSession: true);

    internal Task<bool> TryRefreshAsync(토큰응답 response, AdminAuthenticationContext expected)
        => string.Equals(response.UserId, expected.UserId, StringComparison.Ordinal)
            ? ApplyCoreAsync(response, expected, startsNewSession: false) : Task.FromResult(false);

    private async Task<bool> ApplyCoreAsync(토큰응답 response, AdminAuthenticationContext? expected, bool startsNewSession)
    {
        ArgumentNullException.ThrowIfNull(response);
        var snapshot = new ClientAuthTokenSnapshot(response.AccessToken, response.AccessTokenExpiresAtUtc.ToUniversalTime(),
            response.RefreshToken ?? string.Empty, response.RefreshTokenExpiresAtUtc.ToUniversalTime(),
            response.UserId, response.UserName, response.Roles?.ToArray() ?? []);
        long version;
        AdminAuthenticationContext appliedContext;
        lock (mutationGate)
        {
            if (expected.HasValue && !(startsNewSession ? SameAuthentication(expected.Value) : CurrentContext(expected.Value)))
                return false;
            if (startsNewSession) { ownerRevision++; authenticationGeneration++; }
            version = ++tokenRevision;
            ApplySnapshot(snapshot);
            restored = true;
            appliedContext = CaptureContext();
        }
        // 확정한 값의 저장 뒤에 새 로그인·로그아웃의 최신 저장이 남도록 직렬화합니다.
        await PersistSnapshotAsync(snapshot, version);
        var current = startsNewSession ? IsSameAuthentication(appliedContext) : IsCurrentAuthenticationContext(appliedContext);
        if (current) Changed?.Invoke();
        return current;
    }

    public async Task LogoutAsync() => await LogoutCoreAsync(expected: null);
    internal Task<bool> TryLogoutAsync(AdminAuthenticationContext expected) => LogoutCoreAsync(expected);

    private async Task<bool> LogoutCoreAsync(AdminAuthenticationContext? expected)
    {
        long version;
        AdminAuthenticationContext clearedContext;
        lock (mutationGate)
        {
            if (expected.HasValue && !CurrentContext(expected.Value)) return false;
            ownerRevision++; authenticationGeneration++;
            version = ++tokenRevision;
            ClearState(); restored = true;
            clearedContext = CaptureContext();
        }
        await storageGate.WaitAsync();
        try
        {
            if (IsCurrentTokenRevision(version))
                foreach (var key in new[] { AccessTokenKey, RefreshTokenKey, ExpiresAtKey, RefreshExpiresAtKey, UserIdKey, UserNameKey, RolesKey })
                    SecureStorage.Default.Remove(key);
        }
        finally { storageGate.Release(); }
        var current = IsCurrentAuthenticationContext(clearedContext);
        if (current) Changed?.Invoke();
        return current;
    }

    private async Task PersistSnapshotAsync(ClientAuthTokenSnapshot snapshot, long version)
    {
        await storageGate.WaitAsync();
        try
        {
            if (!IsCurrentTokenRevision(version)) return;
            await SecureStorage.Default.SetAsync(AccessTokenKey, snapshot.AccessToken);
            await SecureStorage.Default.SetAsync(RefreshTokenKey, snapshot.RefreshToken);
            await SecureStorage.Default.SetAsync(ExpiresAtKey, snapshot.AccessTokenExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
            await SecureStorage.Default.SetAsync(RefreshExpiresAtKey, snapshot.RefreshTokenExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
            await SecureStorage.Default.SetAsync(UserIdKey, snapshot.UserId);
            await SecureStorage.Default.SetAsync(UserNameKey, snapshot.UserName);
            await SecureStorage.Default.SetAsync(RolesKey, JsonSerializer.Serialize(snapshot.Roles));
        }
        finally { storageGate.Release(); }
    }

    private static async Task<ClientAuthTokenSnapshot?> LoadSnapshotAsync()
    {
        var access = await SecureStorage.Default.GetAsync(AccessTokenKey);
        var refresh = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        var user = await SecureStorage.Default.GetAsync(UserIdKey);
        var name = await SecureStorage.Default.GetAsync(UserNameKey);
        var expires = await SecureStorage.Default.GetAsync(ExpiresAtKey);
        var refreshExpires = await SecureStorage.Default.GetAsync(RefreshExpiresAtKey);
        var roles = await SecureStorage.Default.GetAsync(RolesKey);
        return new ClientAuthTokenSnapshot(access ?? string.Empty, ParseDate(expires), refresh ?? string.Empty,
            ParseDate(refreshExpires), user ?? string.Empty, name ?? string.Empty,
            string.IsNullOrWhiteSpace(roles) ? [] : JsonSerializer.Deserialize<string[]>(roles) ?? []);
    }

    private static DateTime ParseDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime() : default;

    private bool IsCurrentTokenRevision(long version)
    { lock (mutationGate) return tokenRevision == version; }

    private AdminAuthenticationContext CaptureContext()
        => new(ownerRevision, authenticationGeneration, tokenRevision, UserId, AccessToken, RefreshToken);
    private bool SameAuthentication(AdminAuthenticationContext context)
        => ownerRevision == context.OwnerRevision && authenticationGeneration == context.Generation
            && string.Equals(UserId, context.UserId, StringComparison.Ordinal);
    private bool CurrentContext(AdminAuthenticationContext context)
        => SameAuthentication(context) && tokenRevision == context.TokenRevision
            && string.Equals(AccessToken, context.AccessToken, StringComparison.Ordinal)
            && string.Equals(RefreshToken, context.RefreshToken, StringComparison.Ordinal);

    private void ApplySnapshot(ClientAuthTokenSnapshot snapshot)
    {
        AccessToken = snapshot.AccessToken; RefreshToken = snapshot.RefreshToken;
        AccessTokenExpiresAtUtc = snapshot.AccessTokenExpiresAtUtc; RefreshTokenExpiresAtUtc = snapshot.RefreshTokenExpiresAtUtc;
        UserId = snapshot.UserId; UserName = snapshot.UserName; Roles = snapshot.Roles;
    }
    private void ClearState()
    {
        AccessToken = null; RefreshToken = null; AccessTokenExpiresAtUtc = default; RefreshTokenExpiresAtUtc = default;
        UserId = string.Empty; UserName = string.Empty; Roles = [];
    }
    private ClientAuthTokenSnapshot? CreateSnapshot()
        => string.IsNullOrWhiteSpace(UserId) ? null : new(AccessToken ?? string.Empty, AccessTokenExpiresAtUtc,
            RefreshToken ?? string.Empty, RefreshTokenExpiresAtUtc, UserId, UserName, Roles);
}
