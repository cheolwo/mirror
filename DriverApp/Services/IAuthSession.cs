using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace DriverApp.Services;

public interface IAuthSession : ISsalddelAccessTokenProvider
{
    string? RefreshToken { get; }
    DateTime AccessTokenExpiresAtUtc { get; }
    DateTime RefreshTokenExpiresAtUtc { get; }
    string? UserId { get; }
    string? UserName { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    long Version { get; }
    long SessionRevision { get; }
    event Action? Changed;
    Task RestoreAsync(CancellationToken cancellationToken = default);
    Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
    Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion, CancellationToken cancellationToken = default, bool startsNewSession = false);
    Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default);
}
