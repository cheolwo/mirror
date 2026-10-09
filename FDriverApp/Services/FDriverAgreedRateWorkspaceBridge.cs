using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Pricing;

namespace FDriverApp.Services;

/// <summary>음식기사의 기존 인증·재조회 경계를 합의 요금표 화면에 연결합니다.</summary>
public sealed class FDriverAgreedRateWorkspaceBridge : IRoleWorkspaceApi, IRoleWorkspaceAccess
{
    private readonly FoodDeliveryDriverApiService _api;
    private readonly IFDriverAuthSession _session;
    private readonly FDriverAuthApiService _auth;
    public FDriverAgreedRateWorkspaceBridge(HttpClient http, IFDriverAuthSession session, FDriverAuthApiService auth)
    {
        _session = session; _auth = auth; _api = new(http, session, auth);
        session.SessionChanged += (_, _) => Changed?.Invoke();
    }
    public event Action? Changed;
    public RoleWorkspaceIdentity GetIdentity(string roleKey)
        => new(_session.UserId, _session.SessionRevision, roleKey == "food-driver" && _session.IsAuthenticated);
    public async Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default)
    {
        if (roleKey != "food-driver") throw new RoleWorkspaceAccessException(403, "음식기사 화면에서 이용해 주세요.");
        var result = await _auth.EnsureAccessTokenResultAsync(cancellationToken: cancellationToken);
        if (!result.IsSuccess) throw new RoleWorkspaceAccessException((int?)result.StatusCode ?? 401, result.ErrorMessage ?? "로그인을 확인해 주세요.");
    }
    public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("내 정보의 로그인 화면을 이용해 주세요.");
    public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("내 정보의 계정 메뉴를 이용해 주세요.");
    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        => Send<T>(roleKey, HttpMethod.Get, path, null, cancellationToken);
    public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        => await Send<T>(roleKey, HttpMethod.Post, path, body, cancellationToken);
    public async Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        => await Send<T>(roleKey, HttpMethod.Put, path, body, cancellationToken);
    public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
        => throw new NotSupportedException("합의 요금표에는 파일을 올리지 않습니다.");
    private async Task<T> Send<T>(string roleKey, HttpMethod method, string path, object? body, CancellationToken token)
    {
        if (roleKey != "food-driver" || !(path == AgreedRateTableClient.Endpoint || path.StartsWith(AgreedRateTableClient.Endpoint + "/", StringComparison.Ordinal)
            || path.StartsWith(AgreedRateTableClient.Endpoint + "?", StringComparison.Ordinal)))
            throw new RoleWorkspaceAccessException(403, "합의 요금표의 허용 경로가 아닙니다.");
        try { return await _api.SendPricingAsync<T>(method, path, body, token); }
        catch (FDriverApiException ex) { throw new RoleWorkspaceAccessException((int?)ex.StatusCode ?? 503, ex.Message); }
    }
}
