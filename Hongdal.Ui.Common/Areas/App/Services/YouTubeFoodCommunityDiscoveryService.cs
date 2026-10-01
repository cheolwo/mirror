using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Common.Content;

namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed class YouTubeFoodCommunityDiscoveryService
{
    private readonly HttpClient _httpClient;
    private readonly IHongdalAccessTokenProvider _accessTokenProvider;

    public YouTubeFoodCommunityDiscoveryService(
        HttpClient httpClient,
        IHongdalAccessTokenProvider accessTokenProvider)
    {
        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;
    }

    public async Task<IReadOnlyList<YouTube음식커뮤니티공유후보Dto>> GetApprovedCandidatesAsync(
        int take = 24,
        CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 100);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/orderer/youtube-food-discovery/products?take={safeTake}");
        var accessToken = _accessTokenProvider.AccessToken?.Trim();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<YouTube음식커뮤니티공유후보Dto>>(
                   cancellationToken)
               ?? [];
    }
}
