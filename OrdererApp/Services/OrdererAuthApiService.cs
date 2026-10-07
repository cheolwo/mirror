using Ssalddel.Client.Infrastructure.Security;

namespace OrdererApp.Services;

public sealed record OrdererAuthResult(bool 성공, string? 오류메시지 = null, bool 세션거부 = false);

/// <summary>주문자 앱 로그인·갱신을 공통 인증 HTTP 경계에 연결합니다.</summary>
public sealed class OrdererAuthApiService(HttpClient httpClient, ClientAuthSession session)
{
    private readonly ClientAuthApiSessionService _auth = new(httpClient, session);
    public async Task<OrdererAuthResult> 로그인Async(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
        => Map(await _auth.LoginAsync(userNameOrEmail, password, cancellationToken));
    public async Task<OrdererAuthResult> 갱신Async(string userId, string refreshToken, CancellationToken cancellationToken = default)
        => Map(await _auth.RefreshAsync(userId, refreshToken, cancellationToken));
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => _auth.GetAccessTokenAsync(cancellationToken);
    public Task<string?> RefreshAccessTokenAsync(string? rejectedToken, CancellationToken cancellationToken = default)
        => _auth.RefreshAfterUnauthorizedAsync(rejectedToken, cancellationToken);
    public Task<string?> RefreshAccessTokenAsync(string? rejectedToken, string? expectedOwnerId, CancellationToken cancellationToken = default)
        => _auth.RefreshAfterUnauthorizedAsync(rejectedToken, expectedOwnerId, cancellationToken);
    private static OrdererAuthResult Map(ClientAuthApiResult result) => new(result.IsSuccess, result.ErrorMessage, result.SessionRejected);
}
