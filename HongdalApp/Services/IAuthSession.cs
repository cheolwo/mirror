using Hongdal.Client.Infrastructure.Security;
using Hongdal.Ui.Common.Areas.App.Services;

namespace HongdalApp.Services;

public interface IAuthSession : IHongdalAccessTokenProvider
{
    string? RefreshToken { get; }
    string? UserId { get; }
    string? UserName { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsLoggedIn { get; }
    Task RestoreAsync(CancellationToken cancellationToken = default);
    Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
