using Ssalddel.Client.Infrastructure.Security;

namespace WarehouseManagerApp.Services;

public sealed record WarehouseAuthResult(bool IsSuccess, string ErrorMessage, bool SessionRejected = false)
{
    public static WarehouseAuthResult Success { get; } = new(true, string.Empty);
}

/// <summary>창고 앱 로그인·갱신을 공통 인증 HTTP 경계에 연결합니다.</summary>
public sealed class WarehouseAuthApiService(HttpClient httpClient, ClientAuthSession session)
{
    private readonly ClientAuthApiSessionService _auth = new(httpClient, session);
    public async Task<WarehouseAuthResult> LoginAsync(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
        => Map(await _auth.LoginAsync(userNameOrEmail, password, cancellationToken));
    public async Task<WarehouseAuthResult> RefreshAsync(string userId, string refreshToken, CancellationToken cancellationToken = default)
        => Map(await _auth.RefreshAsync(userId, refreshToken, cancellationToken));
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => _auth.GetAccessTokenAsync(cancellationToken);
    public Task<string?> RefreshAccessTokenAsync(string? rejectedToken, CancellationToken cancellationToken = default)
        => _auth.RefreshAfterUnauthorizedAsync(rejectedToken, cancellationToken);
    public Task<string?> RefreshAccessTokenAsync(string? rejectedToken, string? expectedOwnerId, CancellationToken cancellationToken = default)
        => _auth.RefreshAfterUnauthorizedAsync(rejectedToken, expectedOwnerId, cancellationToken);
    private static WarehouseAuthResult Map(ClientAuthApiResult result) => new(result.IsSuccess, result.ErrorMessage ?? string.Empty, result.SessionRejected);
}
