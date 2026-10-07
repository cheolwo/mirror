using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.WebApp.Services;

public sealed class WebAuthSessionService : ISsalddelAccessTokenProvider
{
    private const string StorageKey = "ssalddel.web.auth.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly IClientSessionGuard _sessionGuard;

    private ClientAuthTokenSnapshot? _snapshot;

    public WebAuthSessionService(
        HttpClient httpClient,
        IJSRuntime jsRuntime,
        IClientSessionGuard sessionGuard)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _sessionGuard = sessionGuard;
    }

    public string? AccessToken => _snapshot?.AccessToken;
    public string? UserId => _snapshot?.UserId;
    public string? UserName => _snapshot?.UserName;
    public string? PreferredLanguageCode => _snapshot?.PreferredLanguageCode;
    public IReadOnlyList<string> Roles => _snapshot?.Roles ?? [];
    public string PrimaryRole => WebRoleThemeResolver.ResolvePrimaryRole(Roles);
    public WebRoleTheme CurrentTheme => WebRoleThemeResolver.Resolve(Roles);
    public bool IsLoggedIn => _sessionGuard.IsAccessTokenUsable(_snapshot, DateTime.UtcNow);
    public event Action? Changed;

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            _snapshot = null;
            NotifyChanged();
            return;
        }

        try
        {
            _snapshot = JsonSerializer.Deserialize<ClientAuthTokenSnapshot>(json, JsonOptions);
            if (!_sessionGuard.IsAccessTokenUsable(_snapshot, DateTime.UtcNow))
            {
                _snapshot = null;
            }
        }
        catch
        {
            _snapshot = null;
        }

        NotifyChanged();
    }

    public async Task LoginAsync(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("아이디와 비밀번호를 입력해 주세요.");
        }

        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/auth/login",
            new 로그인요청
            {
                UserNameOrEmail = userNameOrEmail.Trim(),
                Password = password
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? $"서버 로그인에 실패했습니다. HTTP {(int)response.StatusCode}"
                : $"서버 로그인에 실패했습니다. HTTP {(int)response.StatusCode}: {body}");
        }

        var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("서버 로그인 응답에서 토큰을 읽을 수 없습니다.");
        }

        _snapshot = token.ToClientAuthTokenSnapshot();

        await SaveSnapshotAsync(cancellationToken);
        NotifyChanged();
    }

    public async Task<커뮤니티회원가입응답> RegisterCommunityAsync(
        string userName,
        string email,
        string password,
        bool privacyConsentAccepted,
        string privacyConsentVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("아이디, 이메일과 비밀번호를 입력해 주세요.");
        }

        if (!커뮤니티회원가입개인정보동의문.유효한동의(
                privacyConsentAccepted,
                privacyConsentVersion))
        {
            throw new InvalidOperationException("현재 개인정보 수집·이용 안내를 확인하고 동의해 주세요.");
        }

        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/auth/register/community",
            new 커뮤니티회원가입요청
            {
                UserName = userName.Trim(),
                Email = email.Trim(),
                Password = password,
                PrivacyConsentAccepted = privacyConsentAccepted,
                PrivacyConsentVersion = privacyConsentVersion
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? $"회원가입에 실패했습니다. HTTP {(int)response.StatusCode}"
                : $"회원가입에 실패했습니다. HTTP {(int)response.StatusCode}: {body}");
        }

        var registration = await response.Content.ReadFromJsonAsync<커뮤니티회원가입응답>(cancellationToken);
        if (registration is null || string.IsNullOrWhiteSpace(registration.UserId))
        {
            throw new InvalidOperationException("회원가입 응답에서 계정 정보를 읽을 수 없습니다.");
        }

        await LoginAsync(userName, password, cancellationToken);
        return registration;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _snapshot = null;
        NotifyChanged();
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, StorageKey);
    }

    public async Task<string> SetPreferredLanguageAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        if (!IsLoggedIn || _snapshot is null)
        {
            throw new InvalidOperationException("로그인 계정의 언어를 저장하려면 먼저 로그인해 주세요.");
        }

        using var response = await _httpClient.PutAsJsonAsync(
            "api/v1/auth/preferences/language",
            new 표시언어설정요청 { LanguageCode = languageCode },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? $"화면 언어 저장에 실패했습니다. HTTP {(int)response.StatusCode}"
                : $"화면 언어 저장에 실패했습니다. HTTP {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<표시언어설정응답>(cancellationToken)
                     ?? throw new InvalidOperationException("화면 언어 저장 응답이 비어 있습니다.");
        _snapshot = _snapshot with { PreferredLanguageCode = result.LanguageCode };
        await SaveSnapshotAsync(cancellationToken);
        NotifyChanged();
        return result.LanguageCode;
    }

    private async Task SaveSnapshotAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_snapshot, JsonOptions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey, json);
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
