using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Hongdal.Contracts.Common.Localization;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

public sealed record CommunityTextTranslationResult(
    string Title,
    string Body,
    string Provider,
    string ProviderModelVersion);

public interface ICommunityTextTranslationProvider
{
    bool IsAvailable { get; }

    Task<CommunityTextTranslationResult> TranslateAsync(
        string title,
        string body,
        string sourceLanguageCode,
        string targetLanguageCode,
        CancellationToken cancellationToken);
}

public sealed class AzureCommunityTextTranslationProvider : ICommunityTextTranslationProvider
{
    private const string ProviderName = "AzureTranslator";
    private readonly HttpClient _httpClient;
    private readonly CommunityPostTranslationOptions _options;

    public AzureCommunityTextTranslationProvider(
        HttpClient httpClient,
        IOptions<CommunityPostTranslationOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public bool IsAvailable
        => _options.Enabled
           && string.Equals(_options.Provider, ProviderName, StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<CommunityTextTranslationResult> TranslateAsync(
        string title,
        string body,
        string sourceLanguageCode,
        string targetLanguageCode,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Azure 게시글 번역 설정이 활성화되지 않았습니다.");
        }

        var source = DisplayLanguageCodes.ToNeutralCode(sourceLanguageCode);
        var target = DisplayLanguageCodes.ToNeutralCode(targetLanguageCode);
        var path = $"translate?api-version=3.0&from={Uri.EscapeDataString(source)}&to={Uri.EscapeDataString(target)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new[]
            {
                new AzureTranslationRequest(title),
                new AzureTranslationRequest(body)
            })
        };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
        if (!string.IsNullOrWhiteSpace(_options.Region))
        {
            request.Headers.Add("Ocp-Apim-Subscription-Region", _options.Region.Trim());
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AzureTranslationResponse[]>(
            cancellationToken: cancellationToken);

        if (payload is not { Length: >= 2 }
            || payload[0].Translations.Count == 0
            || payload[1].Translations.Count == 0)
        {
            throw new InvalidOperationException("Azure Translator가 게시글 번역 결과를 반환하지 않았습니다.");
        }

        return new CommunityTextTranslationResult(
            payload[0].Translations[0].Text,
            payload[1].Translations[0].Text,
            ProviderName,
            "v3.0-general");
    }

    private sealed record AzureTranslationRequest(
        [property: JsonPropertyName("Text")] string Text);

    private sealed class AzureTranslationResponse
    {
        [JsonPropertyName("translations")]
        public List<AzureTranslatedText> Translations { get; set; } = [];
    }

    private sealed class AzureTranslatedText
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}
