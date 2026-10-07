using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RestaurantDeskApp.Services;

public sealed class RestaurantAccessTokenProvider(ClientAuthSession session)
    : ISsalddelAccessTokenProvider
{
    public string? AccessToken => session.AccessToken;
    public string? AuthenticationOwnerId => session.UserId;
}
