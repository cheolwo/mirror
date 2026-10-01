using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using HongdalApp.Options;

namespace HongdalApp.Services.Commerce.Coupang;

public sealed class CoupangWingProductClient : ICoupangWingProductClient
{
    private const string ProductPath = "/v2/providers/seller_api/apis/api/v1/marketplace/seller-products";

    private readonly HttpClient _httpClient;
    private readonly CoupangWingOptions _options;
    private readonly ICoupangWingSignatureGenerator _signatureGenerator;

    public CoupangWingProductClient(
        HttpClient httpClient,
        IOptions<CoupangWingOptions> options,
        ICoupangWingSignatureGenerator signatureGenerator)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _signatureGenerator = signatureGenerator;
    }

    public Task<CoupangWingApiResult> CreateProductAsync(JsonNode payload, CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Post, ProductPath, payload, cancellationToken);

    public Task<CoupangWingApiResult> GetProductAsync(long sellerProductId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"{ProductPath}/{sellerProductId}", cancellationToken);

    public Task<CoupangWingApiResult> UpdateProductAsync(JsonNode payload, CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Put, ProductPath, payload, cancellationToken);

    public Task<CoupangWingApiResult> UpdateProductPartialAsync(long sellerProductId, JsonNode payload, CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Put, $"{ProductPath}/{sellerProductId}/partial", payload, cancellationToken);

    public Task<CoupangWingApiResult> DeleteProductAsync(long sellerProductId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, $"{ProductPath}/{sellerProductId}", cancellationToken);

    private Task<CoupangWingApiResult> SendJsonAsync(HttpMethod method, string path, JsonNode payload, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(payload)
        };

        return SendAsync(request, path, string.Empty, cancellationToken);
    }

    private Task<CoupangWingApiResult> SendAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        return SendAsync(request, path, string.Empty, cancellationToken);
    }

    private async Task<CoupangWingApiResult> SendAsync(HttpRequestMessage request, string path, string query, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            _signatureGenerator.Generate(request.Method.Method, path, query, _options.AccessKey, _options.SecretKey, DateTimeOffset.UtcNow));
        request.Headers.TryAddWithoutValidation("X-EXTENDED-TIMEOUT", "90000");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        var body = string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw);

        return new CoupangWingApiResult(
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            body,
            response.IsSuccessStatusCode ? null : raw);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.AccessKey) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("CoupangWing AccessKey and SecretKey must be configured before calling the API.");
        }
    }
}
