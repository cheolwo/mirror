using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.External.Naver;

public sealed record NaverDistrictRegion(string SidoName, string SigunguName);

public interface INaverMapsReverseGeocodingService
{
    Task<NaverDistrictRegion?> ResolveDistrictAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default);
}

public sealed class NaverMapsReverseGeocodingService : INaverMapsReverseGeocodingService
{
    private readonly HttpClient httpClient;
    private readonly NaverMapsOptions options;

    public NaverMapsReverseGeocodingService(HttpClient httpClient, IOptions<NaverMapsOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
    }

    public async Task<NaverDistrictRegion?> ResolveDistrictAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            return null;
        }

        var coordinates = string.Create(
            CultureInfo.InvariantCulture,
            $"{longitude},{latitude}");
        var path = $"{options.ReverseGeocodingPath}?coords={Uri.EscapeDataString(coordinates)}&sourcecrs=epsg:4326&orders=admcode&output=json";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("x-ncp-apigw-api-key-id", options.ClientId);
        request.Headers.TryAddWithoutValidation("x-ncp-apigw-api-key", options.ClientSecret);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ReverseGeocodingResponse>(cancellationToken);
        if (payload?.Status?.Code != 0)
        {
            return null;
        }

        var region = payload.Results?
            .FirstOrDefault(item => string.Equals(item.Name, "admcode", StringComparison.OrdinalIgnoreCase))?
            .Region;
        var sido = region?.Area1?.Name?.Trim();
        var sigungu = region?.Area2?.Name?.Trim();
        return string.IsNullOrWhiteSpace(sido) || string.IsNullOrWhiteSpace(sigungu)
            ? null
            : new NaverDistrictRegion(sido, sigungu);
    }

    private sealed class ReverseGeocodingResponse
    {
        public ReverseGeocodingStatus? Status { get; set; }
        public List<ReverseGeocodingResult>? Results { get; set; }
    }

    private sealed class ReverseGeocodingStatus
    {
        public int Code { get; set; }
    }

    private sealed class ReverseGeocodingResult
    {
        public string Name { get; set; } = string.Empty;
        public ReverseGeocodingRegion? Region { get; set; }
    }

    private sealed class ReverseGeocodingRegion
    {
        public ReverseGeocodingArea? Area1 { get; set; }
        public ReverseGeocodingArea? Area2 { get; set; }
    }

    private sealed class ReverseGeocodingArea
    {
        public string Name { get; set; } = string.Empty;
    }
}
