using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using HongdalApp.Options;

namespace HongdalApp.Services.Commerce.Naver;

public sealed class NaverCommerceTokenProvider : INaverCommerceTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly NaverCommerceOptions _options;
    private readonly INaverCommerceSignatureGenerator _signatureGenerator;
    private NaverCommerceToken? _cachedToken;
    private DateTimeOffset _expiresAt;

    public NaverCommerceTokenProvider(
        HttpClient httpClient,
        IOptions<NaverCommerceOptions> options,
        INaverCommerceSignatureGenerator signatureGenerator)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _signatureGenerator = signatureGenerator;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _cachedToken.AccessToken;
        }

        EnsureConfigured();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var signature = _signatureGenerator.Generate(_options.ClientId, _options.ClientSecret, timestamp);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["timestamp"] = timestamp.ToString(),
            ["client_secret_sign"] = signature,
            ["grant_type"] = "client_credentials",
            ["type"] = _options.TokenType
        });

        using var response = await _httpClient.PostAsync("v1/oauth2/token", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        _cachedToken = await response.Content.ReadFromJsonAsync<NaverCommerceToken>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Naver Commerce token response was empty.");
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, _cachedToken.ExpiresIn - 60));

        return _cachedToken.AccessToken;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("NaverCommerce ClientId and ClientSecret must be configured before calling the API.");
        }
    }
}
