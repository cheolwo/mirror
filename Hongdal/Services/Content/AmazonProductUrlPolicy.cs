using System.Text.RegularExpressions;

namespace Hongdal.Services.Content;

public static partial class AmazonProductUrlPolicy
{
    private static readonly IReadOnlySet<string> AmazonDomains = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "amazon.com",
        "amazon.co.uk",
        "amazon.de",
        "amazon.fr",
        "amazon.it",
        "amazon.es",
        "amazon.ca",
        "amazon.co.jp",
        "amazon.in",
        "amazon.com.au",
        "amazon.com.mx"
    };

    public static Uri ValidateProductUrl(string value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !TryResolveMarketplaceHost(uri.Host, out _))
        {
            throw new ArgumentException("지원하는 Amazon HTTPS 상품 URL이 필요합니다.", nameof(value));
        }

        _ = ExtractAsin(uri);
        return uri;
    }

    public static Uri ValidateReturnedUrl(string? value, Uri fallback)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !TryResolveMarketplaceHost(uri.Host, out _))
        {
            return fallback;
        }

        return ProductPathRegex().IsMatch(uri.AbsolutePath) ? uri : fallback;
    }

    public static string ExtractAsin(Uri uri)
    {
        var match = ProductPathRegex().Match(uri.AbsolutePath);
        if (!match.Success)
        {
            throw new ArgumentException(
                "Amazon 검색·카테고리 URL이 아닌 상품 상세 URL이 필요합니다.",
                nameof(uri));
        }

        return match.Groups[1].Value.ToUpperInvariant();
    }

    public static string ResolveCountryCode(string host)
    {
        if (!TryResolveMarketplaceHost(host, out var marketplaceHost))
        {
            return "ZZ";
        }

        return marketplaceHost switch
        {
            "amazon.co.uk" => "GB",
            "amazon.co.jp" => "JP",
            "amazon.com.au" => "AU",
            "amazon.com.mx" => "MX",
            "amazon.de" => "DE",
            "amazon.fr" => "FR",
            "amazon.it" => "IT",
            "amazon.es" => "ES",
            "amazon.ca" => "CA",
            "amazon.in" => "IN",
            _ => "US"
        };
    }

    public static bool TryResolveMarketplaceHost(string host, out string marketplaceHost)
    {
        marketplaceHost = AmazonDomains.FirstOrDefault(domain =>
            string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return marketplaceHost.Length > 0;
    }

    public static Uri BuildCanonicalProductUrl(Uri source)
    {
        var asin = ExtractAsin(source);
        if (!TryResolveMarketplaceHost(source.Host, out var marketplaceHost))
        {
            throw new ArgumentException("지원하지 않는 Amazon 마켓플레이스입니다.", nameof(source));
        }

        return new Uri($"https://www.{marketplaceHost}/dp/{asin}");
    }

    [GeneratedRegex(
        @"/(?:dp|gp/product)/([A-Za-z0-9]{10})(?:[/?]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProductPathRegex();
}
