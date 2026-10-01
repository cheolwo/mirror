namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed class PlatformCommunityService : CommunityPlatformClient
{
    public PlatformCommunityService(
        HttpClient httpClient,
        HongdalProtectedApiClient protectedApiClient)
        : base(httpClient, protectedApiClient)
    {
    }
}
