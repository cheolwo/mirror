using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Pricing;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RestaurantDeskApp.Services;

public sealed class RestaurantAgreedRateWorkspaceBridge : IRoleWorkspaceApi, IRoleWorkspaceAccess
{
    private readonly Ssalddel음식주문Client _client;
    private readonly RestaurantAuthService _auth;
    private readonly ClientAuthSession _session;
    public RestaurantAgreedRateWorkspaceBridge(I음식주문ApiClient client, RestaurantAuthService auth, ClientAuthSession session)
    {
        _client = (Ssalddel음식주문Client)client; _auth = auth; _session = session;
        auth.SessionEnding += _ => Changed?.Invoke();
    }
    public event Action? Changed;
    public RoleWorkspaceIdentity GetIdentity(string roleKey)
        => new(_session.UserId, _auth.SessionGeneration, roleKey == "restaurant" && _session.IsAuthenticated);
    public async Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default)
    {
        if (roleKey != "restaurant") throw new RoleWorkspaceAccessException(403, "음식점 화면에서 이용해 주세요.");
        var result = await _auth.EnsureAccessTokenAsync(cancellationToken: cancellationToken);
        if (!result.IsSuccess) throw new RoleWorkspaceAccessException(result.RequiresLogin ? 401 : 503, result.ErrorMessage);
    }
    public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("음식점 로그인 화면을 이용해 주세요.");
    public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("음식점 계정 메뉴를 이용해 주세요.");
    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        => Send<T>(roleKey, HttpMethod.Get, path, null, cancellationToken);
    public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        => await Send<T>(roleKey, HttpMethod.Post, path, body, cancellationToken);
    public async Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        => await Send<T>(roleKey, HttpMethod.Put, path, body, cancellationToken);
    public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
        => throw new NotSupportedException("합의 요금표에는 파일을 올리지 않습니다.");
    private Task<T> Send<T>(string roleKey, HttpMethod method, string path, object? body, CancellationToken token)
    {
        if (roleKey != "restaurant" || !(path == AgreedRateTableClient.Endpoint || path.StartsWith(AgreedRateTableClient.Endpoint + "/", StringComparison.Ordinal)
            || path.StartsWith(AgreedRateTableClient.Endpoint + "?", StringComparison.Ordinal)))
            throw new RoleWorkspaceAccessException(403, "합의 요금표의 허용 경로가 아닙니다.");
        return _client.SendPricingAsync<T>(method, path, body, token);
    }
}
