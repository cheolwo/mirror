namespace HongdalAdmin.Services;

public sealed class 관리자인증세션Service : Hongdal.Ui.Common.Areas.App.Services.IHongdalAccessTokenProvider
{
    private const string DevelopmentBootstrapSectionName = "AdminAuth:DevelopmentBootstrap";

    public string AccessToken { get; private set; } = string.Empty;
    public string RefreshToken { get; private set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string[] Roles { get; private set; } = Array.Empty<string>();

    public 관리자인증세션Service(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment()
            || !configuration.GetValue($"{DevelopmentBootstrapSectionName}:Enabled", false))
        {
            return;
        }

        var accessToken = configuration[$"{DevelopmentBootstrapSectionName}:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        AccessToken = accessToken;
        RefreshToken = configuration[$"{DevelopmentBootstrapSectionName}:RefreshToken"] ?? string.Empty;
        UserId = configuration[$"{DevelopmentBootstrapSectionName}:UserId"] ?? string.Empty;
        UserName = configuration[$"{DevelopmentBootstrapSectionName}:UserName"] ?? "development-admin";
        Roles = configuration
            .GetSection($"{DevelopmentBootstrapSectionName}:Roles")
            .Get<string[]>()
            ?? ["서버관리자"];
        AccessTokenExpiresAtUtc = configuration.GetValue<DateTime?>($"{DevelopmentBootstrapSectionName}:AccessTokenExpiresAtUtc")
                                  ?? DateTime.UtcNow.AddHours(1);
    }

    public bool 로그인됨 => !string.IsNullOrWhiteSpace(AccessToken);

    public bool 서버관리자인가 => Roles.Contains("서버관리자", StringComparer.Ordinal);

    public bool 관세사인가 => Roles.Contains("관세사", StringComparer.Ordinal);

    public bool HsCode운영자인가 => 서버관리자인가 || 관세사인가;

    public void 로그인적용(토큰응답 response)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        AccessTokenExpiresAtUtc = response.AccessTokenExpiresAtUtc;
        UserId = response.UserId;
        UserName = response.UserName;
        Roles = response.Roles ?? Array.Empty<string>();
    }

    public void 로그아웃()
    {
        AccessToken = string.Empty;
        RefreshToken = string.Empty;
        AccessTokenExpiresAtUtc = default;
        UserId = string.Empty;
        UserName = string.Empty;
        Roles = Array.Empty<string>();
    }
}
