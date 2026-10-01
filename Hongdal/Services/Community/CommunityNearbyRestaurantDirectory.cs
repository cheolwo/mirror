using System.Net.Http.Json;
using Hongdal.Contracts.Restaurants;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

public sealed record CommunityNearbyRestaurantLookupResult(
    bool SourceAvailable,
    bool IsSimulationSource,
    IReadOnlyList<음식점요약응답> Items);

public interface ICommunityNearbyRestaurantDirectory
{
    Task<CommunityNearbyRestaurantLookupResult> FindAsync(
        decimal latitude,
        decimal longitude,
        decimal radiusKm,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class HttpCommunityNearbyRestaurantDirectory : ICommunityNearbyRestaurantDirectory
{
    private readonly HttpClient _httpClient;
    private readonly CommunityContextDiscoveryOptions _options;
    private readonly ILogger<HttpCommunityNearbyRestaurantDirectory> _logger;

    public HttpCommunityNearbyRestaurantDirectory(
        HttpClient httpClient,
        IOptions<CommunityContextDiscoveryOptions> options,
        ILogger<HttpCommunityNearbyRestaurantDirectory> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CommunityNearbyRestaurantLookupResult> FindAsync(
        decimal latitude,
        decimal longitude,
        decimal radiusKm,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(_options.FoodApiBaseUrl, UriKind.Absolute, out var baseUri))
        {
            return new(false, _options.RestaurantSourceIsSimulation, []);
        }

        var uri = new Uri(
            baseUri,
            $"api/v1/restaurants/nearby?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&radiusKm={radiusKm.ToString(System.Globalization.CultureInfo.InvariantCulture)}&limit={limit}");

        try
        {
            var response = await _httpClient.GetFromJsonAsync<음식점목록응답>(uri, cancellationToken);
            return new(true, _options.RestaurantSourceIsSimulation, response?.Items ?? []);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("음식점 후보 조회가 제한 시간 안에 완료되지 않았습니다.");
            return new(false, _options.RestaurantSourceIsSimulation, []);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "음식점 후보 조회 API를 호출할 수 없습니다.");
            return new(false, _options.RestaurantSourceIsSimulation, []);
        }
    }
}
