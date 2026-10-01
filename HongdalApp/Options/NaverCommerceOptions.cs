namespace HongdalApp.Options;

public sealed class NaverCommerceOptions
{
    public const string SectionName = "NaverCommerce";

    public string BaseUrl { get; set; } = "https://api.commerce.naver.com/external/";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string TokenType { get; set; } = "SELF";
}
