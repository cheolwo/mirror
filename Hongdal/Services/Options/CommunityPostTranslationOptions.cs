namespace 홍달.Services.Options;

public sealed class CommunityPostTranslationOptions
{
    public const string SectionName = "CommunityPostTranslation";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "AzureTranslator";
    public string Endpoint { get; set; } = "https://api.cognitive.microsofttranslator.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
    public bool TranslateReportPosts { get; set; }
    public List<string> SupportedLanguageCodes { get; set; } = ["ko-KR", "en-US"];
}
