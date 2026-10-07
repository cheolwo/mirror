using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace 살뜰.Services.External.Google
{
    public interface IGeocodingService
    {
        Task<(decimal lat, decimal lng)?> GeocodeAsync(string address);
    }

    public class GoogleGeocodingService : IGeocodingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly Ssalddel.Services.Commerce.I외부개인정보처리Guard? _privacy;

        public GoogleGeocodingService(HttpClient httpClient, IConfiguration configuration, Ssalddel.Services.Commerce.I외부개인정보처리Guard? privacy = null)
        {
            _httpClient = httpClient;
            _privacy = privacy;
            _apiKey = configuration["GoogleGeocodingApiKey"] ?? string.Empty;
        }

        public async Task<(decimal lat, decimal lng)?> GeocodeAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            if (string.IsNullOrWhiteSpace(_apiKey)) return null;
            if (_privacy is not null && !_privacy.허용("GoogleGeocoding")) return null;

            var encoded = Uri.EscapeDataString(address);
            var response = await _httpClient.GetAsync($"https://maps.googleapis.com/maps/api/geocode/json?address={encoded}&key={_apiKey}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (!root.TryGetProperty("status", out var statusElement) || !string.Equals(statusElement.GetString(), "OK", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!root.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array || resultsElement.GetArrayLength() == 0)
            {
                return null;
            }

            var firstResult = resultsElement[0];
            if (!firstResult.TryGetProperty("geometry", out var geometryElement) ||
                !geometryElement.TryGetProperty("location", out var locationElement) ||
                !locationElement.TryGetProperty("lat", out var latElement) ||
                !locationElement.TryGetProperty("lng", out var lngElement))
            {
                return null;
            }

            return (latElement.GetDecimal(), lngElement.GetDecimal());
        }
    }
}



