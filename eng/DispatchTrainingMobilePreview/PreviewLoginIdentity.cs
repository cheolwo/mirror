using SsalddelAdminApp.Services;

namespace DispatchTrainingMobilePreview;

/// <summary>환경변수로 주어진 합성 계정만 기존 로그인 API에 제출합니다. 인증값을 파일·로그·URL로 내보내지 않습니다.</summary>
public sealed class PreviewLoginIdentity(AdminAuthSession session, AdminAuthService auth, IConfiguration configuration) : IDispatchTrainingIdentity
{
    public string? Owner => session.AuthenticationOwnerId;
    public bool IsAdmin => session.IsServerAdmin;
    public event Action? Changed { add => session.Changed += value; remove => session.Changed -= value; }
    public async Task<string?> EnsureAsync(CancellationToken ct)
    {
        await session.RestoreAsync(ct);
        if (session.IsServerAdmin) return await auth.EnsureAccessTokenAsync(cancellationToken: ct);
        var username = configuration["DISPATCH_TRAINING_ADMIN_USERNAME"] ?? "dispatch-training-admin";
        var password = configuration["DISPATCH_TRAINING_ACCOUNT_PASSWORD"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return "합성 관리자 로그인 환경이 준비되지 않았습니다.";
        var result = await auth.LoginAsync(username, password, ct);
        return result.Succeeded && session.IsServerAdmin ? null : "합성 관리자 로그인을 확인하지 못했습니다.";
    }
}
