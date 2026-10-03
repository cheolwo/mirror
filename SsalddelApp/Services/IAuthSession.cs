using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelApp.Services;

public interface IAuthSession : ISsalddelAccessTokenProvider
{
    string? RefreshToken { get; }
    DateTime AccessTokenExpiresAtUtc { get; }
    DateTime RefreshTokenExpiresAtUtc { get; }
    string? UserId { get; }
    string? UserName { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsLoggedIn { get; }
    long SessionRevision { get; }
    event Action? Changed;
    Task RestoreAsync(CancellationToken cancellationToken = default);
    Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default, bool startsNewSession = true);
    Task<bool> TryRefreshAsync(ClientAuthTokenSnapshot snapshot, long expectedRevision, string? expectedRefreshToken, CancellationToken cancellationToken = default);
    Task<bool> TryClearAsync(long expectedRevision, string? expectedRefreshToken, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
