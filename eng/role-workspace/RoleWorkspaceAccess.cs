using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Client.RoleWorkspace;

public interface IRoleWorkspaceTokenStoreFactory
{
    IClientSecureTokenStore Create(string roleKey);
}
public interface IRoleWorkspacePrimaryAuth
{
    event Action? Changed;
    RoleWorkspaceIdentity Identity { get; }
    Task InitializeAsync(CancellationToken cancellationToken);
    Task SignInAsync(string name, string password, CancellationToken cancellationToken);
    Task SignOutAsync(CancellationToken cancellationToken);
    Task<string?> GetTokenAsync(CancellationToken cancellationToken);
}

/// <summary>호스트 일반 계정은 재사용하고 음식점·음식기사·관리자 자격은 별도 저장소와 HTTP 요청으로 격리합니다.</summary>
public sealed class RoleWorkspaceAccess : IRoleWorkspaceAccess, IRoleWorkspaceApi, IDisposable
{
    private readonly HttpClient _http;
    private readonly IRoleWorkspacePrimaryAuth _primary;
    private readonly IRoleWorkspaceTokenStoreFactory _stores;
    private readonly Dictionary<string, (ClientAuthSession Session, ClientAuthApiSessionService Api)> _sessions = [];
    public event Action? Changed;
    public RoleWorkspaceAccess(HttpClient http, IRoleWorkspacePrimaryAuth primary, IRoleWorkspaceTokenStoreFactory stores)
    {
        _http = http; _primary = primary; _stores = stores;
        _primary.Changed += NotifyChanged;
    }
    private static bool Separate(string key) => key is "restaurant" or "food-driver" or "operator";
    private static string Key(string key) => RoleWorkspaceCatalog.Find(key)?.Key
        ?? throw new ArgumentException("활동 역할을 확인해 주세요.", nameof(key));
    private (ClientAuthSession Session, ClientAuthApiSessionService Api) Session(string key)
    {
        lock (_sessions)
        {
            if (_sessions.TryGetValue(key, out var existing)) return existing;
            var session = new ClientAuthSession(_stores.Create(key), new ClientSessionGuard());
            var entry = (session, new ClientAuthApiSessionService(_http, session));
            _sessions.Add(key, entry); return entry;
        }
    }
    public RoleWorkspaceIdentity GetIdentity(string roleKey)
    {
        var key = Key(roleKey);
        if (!Separate(key)) return _primary.Identity;
        var session = Session(key).Session;
        return new(session.UserId, session.Revision, session.IsAuthenticated);
    }
    public async Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default)
    {
        var key = Key(roleKey);
        if (!Separate(key)) { await _primary.InitializeAsync(cancellationToken); return; }
        var entry = Session(key);
        var before = GetIdentity(key);
        await entry.Api.GetAccessTokenAsync(cancellationToken);
        if (before != GetIdentity(key)) NotifyChanged();
    }
    public async Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default)
    {
        var key = Key(roleKey);
        if (!Separate(key)) { await _primary.SignInAsync(name, password, cancellationToken); return; }
        var result = await Session(key).Api.LoginAsync(name, password, cancellationToken);
        NotifyChanged();
        if (!result.IsSuccess) throw new InvalidOperationException(result.ErrorMessage ?? "로그인하지 못했습니다.");
    }
    public async Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default)
    {
        var key = Key(roleKey);
        if (!Separate(key)) { await _primary.SignOutAsync(cancellationToken); return; }
        await Session(key).Session.ClearAsync(cancellationToken); NotifyChanged();
    }
    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        => SendAsync<T>(roleKey, path, HttpMethod.Get, null, cancellationToken);
    public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        => await SendAsync<T>(roleKey, path, HttpMethod.Post, body, cancellationToken);
    public async Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        => await SendAsync<T>(roleKey, path, HttpMethod.Put, body, cancellationToken);
    public async Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
        => await SendAsync<T>(roleKey, path, HttpMethod.Post, null, cancellationToken, body);
    private async Task<T> SendAsync<T>(string roleKey, string path, HttpMethod method, object? body, CancellationToken ct, HttpContent? content = null)
    {
        var key = Key(roleKey);
        if (!path.StartsWith("api/v1/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal)
            || path.Contains('\\') || Uri.TryCreate(path, UriKind.Absolute, out _))
            throw new ArgumentException("업무 조회 경로를 확인해 주세요.", nameof(path));
        await EnsureInitializedAsync(key, ct);
        var token = Separate(key) ? await Session(key).Api.GetAccessTokenAsync(ct) : await _primary.GetTokenAsync(ct);
        var identity = GetIdentity(key);
        if (!identity.IsAuthenticated || string.IsNullOrWhiteSpace(token))
            throw new RoleWorkspaceAccessException(401, "로그인한 뒤 업무를 확인해 주세요.");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null) request.Content = content;
        else if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        using var response = await _http.SendAsync(request, ct);
        if (identity != GetIdentity(key)) throw new OperationCanceledException("로그인 상태가 변경되었습니다.", ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // 상태 변경 요청을 자동 재전송하지 않습니다. 새 로그인 이후 같은 요청 ID로 사람이 재시도합니다.
            await SignOutAsync(key, CancellationToken.None);
            throw new RoleWorkspaceAccessException(401, "로그인 세션이 만료되었습니다. 다시 로그인해 주세요.");
        }
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new RoleWorkspaceAccessException(403, "이 업무를 볼 수 있는 권한이 없습니다.");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            string? errorCode = null;
            try
            {
                using var problem = JsonDocument.Parse(errorBody);
                if (problem.RootElement.ValueKind == JsonValueKind.Object && problem.RootElement.TryGetProperty("errorCode", out var code) && code.ValueKind == JsonValueKind.String)
                    errorCode = code.GetString();
            }
            catch (JsonException) { }
            if (identity != GetIdentity(key)) throw new OperationCanceledException("로그인 상태가 변경되었습니다.", ct);
            throw new RoleWorkspaceAccessException((int)response.StatusCode, response.StatusCode == HttpStatusCode.Conflict
                ? "업무 상태가 변경되었습니다. 새로고침한 뒤 확인해 주세요."
                : "업무를 처리하지 못했습니다. 연결과 입력을 확인한 뒤 다시 시도해 주세요.", errorCode, errorBody);
        }
        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0) return default!;
        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        if (identity != GetIdentity(key)) throw new OperationCanceledException("로그인 상태가 변경되었습니다.", ct);
        return result ?? throw new JsonException("업무 응답을 읽을 수 없습니다.");
    }
    private void NotifyChanged() => Changed?.Invoke();
    public void Dispose() => _primary.Changed -= NotifyChanged;
}
