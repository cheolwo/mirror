using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce.Naver;

public sealed class NaverSmartStoreProductClient : INaverSmartStoreProductClient
{
    private readonly HttpClient _httpClient;
    private readonly INaverCommerceTokenProvider _tokenProvider;

    public NaverSmartStoreProductClient(HttpClient httpClient, INaverCommerceTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    public Task<NaverCommerceApiResult> RegisterProductAsync(JsonNode payload, CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Post, "v2/products", payload, cancellationToken);

    public Task<NaverCommerceApiResult> GetChannelProductAsync(long channelProductNo, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"v2/products/channel-products/{channelProductNo}", cancellationToken);

    public Task<NaverCommerceApiResult> UpdateChannelProductAsync(long channelProductNo, JsonNode payload, CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Put, $"v2/products/channel-products/{channelProductNo}", payload, cancellationToken);

    public Task<NaverCommerceApiResult> DeleteChannelProductAsync(long channelProductNo, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, $"v2/products/channel-products/{channelProductNo}", cancellationToken);

    public Task<NaverCommerceApiResult> GetOriginProductAsync(long originProductNo, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"v2/products/origin-products/{originProductNo}", cancellationToken);

    public Task<NaverCommerceApiResult> UpdateOriginProductAsync(long originProductNo, JsonNode payload, CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Put, $"v2/products/origin-products/{originProductNo}", payload, cancellationToken);

    public Task<NaverCommerceApiResult> DeleteOriginProductAsync(long originProductNo, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, $"v2/products/origin-products/{originProductNo}", cancellationToken);

    private Task<NaverCommerceApiResult> SendJsonAsync(HttpMethod method, string requestUri, JsonNode payload, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, requestUri)
        {
            Content = JsonContent.Create(payload)
        };

        return SendAsync(request, cancellationToken);
    }

    private Task<NaverCommerceApiResult> SendAsync(HttpMethod method, string requestUri, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, requestUri);
        return SendAsync(request, cancellationToken);
    }

    private async Task<NaverCommerceApiResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        var body = string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw);

        return new NaverCommerceApiResult(
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            body,
            response.IsSuccessStatusCode ? null : raw);
    }
}
