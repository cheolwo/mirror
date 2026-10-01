using System.Text.RegularExpressions;
using Hongdal.Contracts.Common.Content;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.Content;

public interface IAmazonAssociatesLinkBuilder
{
    AmazonAssociateLinkDraftDto? Build(string? productUrl, string? productLabel);
}

public sealed partial class AmazonAssociatesLinkBuilder : IAmazonAssociatesLinkBuilder
{
    private readonly AmazonAssociatesOptions _options;

    public AmazonAssociatesLinkBuilder(IOptions<AmazonAssociatesOptions> options)
    {
        _options = options.Value;
    }

    public AmazonAssociateLinkDraftDto? Build(string? productUrl, string? productLabel)
    {
        if (string.IsNullOrWhiteSpace(productUrl))
        {
            return null;
        }

        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Amazon Associates 링크 생성이 비활성화되어 있습니다.");
        }

        var productUri = AmazonProductUrlPolicy.ValidateProductUrl(productUrl);
        var canonicalUri = AmazonProductUrlPolicy.BuildCanonicalProductUrl(productUri);
        if (!AmazonProductUrlPolicy.TryResolveMarketplaceHost(canonicalUri.Host, out var marketplaceHost)
            || !_options.TrackingIdsByMarketplaceHost.TryGetValue(marketplaceHost, out var trackingId)
            || !TrackingIdPattern().IsMatch(trackingId?.Trim() ?? string.Empty))
        {
            throw new InvalidOperationException(
                $"AmazonAssociates:TrackingIdsByMarketplaceHost:{marketplaceHost} 추적 ID 설정이 필요합니다.");
        }

        var normalizedLabel = Normalize(productLabel, 120) ?? "관련 상품 확인";
        var affiliateUrl = $"{canonicalUri.AbsoluteUri}?tag={Uri.EscapeDataString(trackingId!.Trim())}";
        return new AmazonAssociateLinkDraftDto(
            normalizedLabel,
            canonicalUri.AbsoluteUri,
            affiliateUrl,
            RequiredText(_options.LinkDisclosure, nameof(_options.LinkDisclosure), 500),
            RequiredText(_options.AssociateIdentification, nameof(_options.AssociateIdentification), 500));
    }

    private static string RequiredText(string? value, string propertyName, int maxLength)
        => Normalize(value, maxLength)
           ?? throw new InvalidOperationException($"AmazonAssociates:{propertyName} 설정이 필요합니다.");

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{2,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TrackingIdPattern();
}
