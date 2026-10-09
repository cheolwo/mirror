using CommerceProtectionPreview;

namespace SsalddelAdminApp.Services;

/// <summary>실제 모바일 운영자 Razor/client를 로컬 브라우저에서 실행하기 위한 합성 세션 어댑터입니다.</summary>
public sealed class AdminAuthSession(RealApiSession session)
{
    public event Action? Changed { add => session.Changed += value; remove => session.Changed -= value; }
    public string? AccessToken => session.AccessToken;
    public string? RefreshToken => null;
    public string UserId => session.AuthenticationOwnerId ?? string.Empty;
    public bool IsServerAdmin => session.GetIdentity("operator").IsAuthenticated;
    public async Task RestoreAsync(CancellationToken ct = default) => await session.EnsureInitializedAsync("operator", ct);
}
public sealed class AdminAuthService(AdminAuthSession session)
{
    public async Task<string?> EnsureAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    { await session.RestoreAsync(cancellationToken); return session.IsServerAdmin ? null : "시험 운영자 세션 없음"; }
}
