using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace WarehouseManagerApp.Services;

public sealed class WarehouseAccessTokenProvider(ClientAuthSession session, WarehouseAuthApiService? auth = null) : ISsalddelAccessTokenProvider
{
    public string? AuthenticationOwnerId => session.UserId;
    public string? AccessToken => session.IsAuthenticated ? session.AccessToken : null;
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        => auth?.GetAccessTokenAsync(cancellationToken) ?? Task.FromResult(AccessToken);
    public Task<string?> RefreshAccessTokenAsync(string? rejectedToken, CancellationToken cancellationToken = default)
        => auth?.RefreshAccessTokenAsync(rejectedToken, cancellationToken) ?? Task.FromResult<string?>(null);
    public Task<string?> RefreshAccessTokenAsync(string? rejectedToken, string? expectedOwnerId, CancellationToken cancellationToken = default)
        => auth?.RefreshAccessTokenAsync(rejectedToken, expectedOwnerId, cancellationToken) ?? Task.FromResult<string?>(null);
}
