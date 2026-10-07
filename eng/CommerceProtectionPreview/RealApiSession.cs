using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace CommerceProtectionPreview;

/// <summary>Development 루프백 검증 서버의 합성 계정만 사용합니다. 제품 로그인 UI·단말 증거가 아닙니다.</summary>
public sealed class RealApiSession : ISsalddelAccessTokenProvider, ISsalddel현재사용자Context, IRoleWorkspaceAccess, IRoleWorkspaceApi, IAsyncDisposable
{
    private readonly IConfiguration configuration;
    private readonly HttpClient http;
    private readonly SsalddelIsmsPClientEncryptionService encryption;
    private string? role;
    private string? owner;
    private string? token;
    private string[] roles = [];
    private long revision;
    public RealApiSession(IConfiguration configuration, IJSRuntime js, RealApiEvidence evidence)
    {
        this.configuration = configuration;
        var file = configuration["COMMERCE_PREVIEW_REAL_ACCOUNTS"];
        Enabled = !string.IsNullOrWhiteSpace(file);
        if (Enabled)
        {
            using var accounts = JsonDocument.Parse(File.ReadAllText(file!));
            Endpoint = new(accounts.RootElement.GetProperty("baseUrl").GetString()!);
            if (!Endpoint.IsLoopback || Endpoint.Scheme != "http" || Endpoint.Port != 5362)
                throw new InvalidOperationException("r26 전용 루프백 서버만 연결할 수 있습니다.");
        }
        http = new(new RealApiEvidenceHandler(evidence) { InnerHandler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false } })
        { BaseAddress = Endpoint, Timeout = TimeSpan.FromSeconds(20) };
        encryption = new(js);
        JsonApi = new SsalddelJsonApiClient(new SsalddelProtectedApiClient(http, encryption, this));
    }
    public bool Enabled { get; }
    public Uri Endpoint { get; } = new("http://127.0.0.1:5362/");
    public ISsalddelJsonApiClient JsonApi { get; }
    public string? AccessToken => token;
    public string? AuthenticationOwnerId => owner;
    public 현재사용자Snapshot 현재사용자 => owner is null ? 현재사용자Snapshot.익명 : new(owner, "격리 검증 계정", roles);
    public event Action? Changed;
    public RoleWorkspaceIdentity GetIdentity(string roleKey) => new(role == roleKey ? owner : null, revision, role == roleKey && token is not null);
    public async Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default)
    {
        if (!Enabled) throw new InvalidOperationException("실제 API 모드를 명시적으로 준비해 주세요.");
        if (role == roleKey && token is not null) return;
        var accountRole = roleKey switch { "orderer" => "orderer", "restaurant" => "restaurant", "food-driver" => "food-driver", "operator" => "admin", _ => throw new InvalidOperationException("음식 네 역할만 지원합니다.") };
        using var accounts = JsonDocument.Parse(await File.ReadAllTextAsync(configuration["COMMERCE_PREVIEW_REAL_ACCOUNTS"]!, cancellationToken));
        var account = accounts.RootElement.GetProperty("accounts").EnumerateArray().Single(x => x.GetProperty("role").GetString() == accountRole);
        token = null; owner = null; roles = []; role = roleKey; revision++; Changed?.Invoke();
        var privateTokens = configuration["COMMERCE_PREVIEW_REAL_TOKENS"];
        if (!string.IsNullOrWhiteSpace(privateTokens))
        {
            using var sessions = JsonDocument.Parse(await File.ReadAllTextAsync(privateTokens, cancellationToken));
            if (sessions.RootElement.GetProperty("baseUrl").GetString() != Endpoint.ToString())
                throw new InvalidOperationException("합성 세션 서버가 일치하지 않습니다.");
            token = sessions.RootElement.GetProperty("accounts").EnumerateArray()
                .Single(x => x.GetProperty("role").GetString() == roleKey).GetProperty("accessToken").GetString();
        }
        else
        {
            using var response = await http.PostAsJsonAsync("api/v1/auth/login", new
            { userNameOrEmail = account.GetProperty("userName").GetString(), password = accounts.RootElement.GetProperty("password").GetString() }, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var login = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            token = login.RootElement.GetProperty("accessToken").GetString();
        }
        if (token is null) throw new InvalidOperationException("토큰 응답 없음");
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        using var jwt = JsonDocument.Parse(Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '=')));
        owner = jwt.RootElement.TryGetProperty("sub", out var sub) ? sub.GetString() : jwt.RootElement.GetProperty("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").GetString();
        roles = jwt.RootElement.TryGetProperty("role", out var roleValue)
            || jwt.RootElement.TryGetProperty("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", out roleValue)
            ? roleValue.ValueKind == JsonValueKind.Array ? roleValue.EnumerateArray().Select(x => x.GetString()!).ToArray() : [roleValue.GetString()!] : [];
        revision++; Changed?.Invoke();
    }
    public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("이 호스트는 실제 계정 입력을 받지 않습니다. 준비된 합성 계정만 사용합니다.");
    public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default)
    { token = owner = role = null; roles = []; revision++; Changed?.Invoke(); return Task.CompletedTask; }
    public async Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
    {
        await Guard(roleKey, cancellationToken);
        try { return await JsonApi.GetAsync<T>(path, "격리 업무 조회", allowNotFound: false, cancellationToken) ?? throw new InvalidOperationException("업무 응답 없음"); }
        catch (SsalddelApiException ex) { throw new RoleWorkspaceAccessException(ex.StatusCode, ex.Message, ex.ErrorCode, ex.ResponseBody); }
    }
    public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
    { await Guard(roleKey, cancellationToken); try { return await JsonApi.SendAsync<object,T>(HttpMethod.Post,path,body,"격리 업무 요청",cancellationToken:cancellationToken); } catch (SsalddelApiException ex) { throw new RoleWorkspaceAccessException(ex.StatusCode,ex.Message,ex.ErrorCode,ex.ResponseBody); } }
    public async Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
    { await Guard(roleKey,cancellationToken); return await JsonApi.SendAsync<object,T>(HttpMethod.Put,path,body,"격리 업무 변경",cancellationToken:cancellationToken); }
    public Task<T?> UploadAsync<T>(string roleKey,string path,HttpContent body,CancellationToken cancellationToken)
        => throw new InvalidOperationException("첨부 업로드는 이번 검증 범위가 아닙니다.");
    private Task Guard(string roleKey,CancellationToken ct) => GetIdentity(roleKey).IsAuthenticated ? Task.CompletedTask : EnsureInitializedAsync(roleKey,ct);
    public async ValueTask DisposeAsync() { http.Dispose(); await encryption.DisposeAsync(); }
}

public sealed class RealApiEvidence
{
    private readonly object gate = new();
    private readonly List<object> rows = [];
    public void Add(string method,string path,int? status,string? error = null)
    { lock(gate) rows.Add(new { method,path,status,error,atUtc=DateTime.UtcNow }); }
    public object Snapshot() { lock(gate) return new { schemaVersion="commerce-preview-real-http.r26",serverEndpoint="http://127.0.0.1:5362/",credentialBodiesRecorded=false,rows=rows.ToArray() }; }
}
sealed class RealApiEvidenceHandler(RealApiEvidence evidence) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    { try { var response=await base.SendAsync(request,ct); evidence.Add(request.Method.Method,request.RequestUri!.AbsolutePath,(int)response.StatusCode); return response; } catch(Exception ex) { evidence.Add(request.Method.Method,request.RequestUri!.AbsolutePath,null,ex.GetType().Name); throw; } }
}
public sealed class RealFixtureLocationProvider : IRoleWorkspaceLocationProvider
{
    public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult<RoleWorkspaceLocation?>(new(37.588,127.084,1,DateTimeOffset.UtcNow)); }
}

/// <summary>외부 지도나 임의 좌표 표현을 호출하지 않고 실제 업무 목록 검증만 제공합니다.</summary>
public sealed class RealApiMapUnavailableHost : INeighborhoodMapHost
{
    public Task<Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapHostStatus> RenderAsync(string elementId,
        Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapRenderState state,
        Func<string, Task> markerSelected, CancellationToken cancellationToken = default)
        => Task.FromResult(new Ssalddel.Ui.Common.Areas.App.Models.NeighborhoodMapHostStatus("unavailable", "격리 검증은 실제 업무 목록을 사용합니다. 외부 지도·실제 GPS는 검증하지 않습니다."));
    public Task HideAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
