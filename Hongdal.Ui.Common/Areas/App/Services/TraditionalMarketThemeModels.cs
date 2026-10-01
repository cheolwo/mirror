namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed record TraditionalMarketThemeSlot(
    string Key,
    string Title,
    string Color,
    string? ImageUrl = null,
    string? AltText = null)
{
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
}

public sealed record TraditionalMarketThemeManifest(
    string Version,
    string RendererProfile,
    string ReviewStatus,
    string AssociationApprovalStatus,
    string LicenseLabel,
    string MarketScopeKey,
    string MarketName,
    string PreviewBackground,
    TraditionalMarketThemeSlot MarketHeader,
    TraditionalMarketThemeSlot BoardMarker,
    TraditionalMarketThemeSlot MarketDayBanner,
    TraditionalMarketThemeSlot ProductMarker,
    TraditionalMarketThemeSlot PickupSign,
    TraditionalMarketThemeSlot StoryCover,
    bool PreservesCriticalInformation,
    string DesignerCompensationPolicyLabel)
{
    public IReadOnlyList<TraditionalMarketThemeSlot> Slots =>
    [
        MarketHeader,
        BoardMarker,
        MarketDayBanner,
        ProductMarker,
        PickupSign,
        StoryCover
    ];

    public string AccentColor => MarketDayBanner.Color;

    public bool IsOfficiallyApplicable
        => string.Equals(ReviewStatus, "승인", StringComparison.OrdinalIgnoreCase)
           && string.Equals(AssociationApprovalStatus, "승인", StringComparison.OrdinalIgnoreCase)
           && PreservesCriticalInformation;
}

internal static class TraditionalMarketThemePolicy
{
    internal const string ScopePrefix = "traditional-market:";

    internal static bool TryPrepareDraft(
        TraditionalMarketThemeManifest manifest,
        out TraditionalMarketThemeManifest normalizedManifest,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (!IsMarketScopeKey(manifest.MarketScopeKey))
        {
            normalizedManifest = manifest;
            message = $"전통시장 범위 키는 {ScopePrefix} 형식이어야 합니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.MarketName))
        {
            normalizedManifest = manifest;
            message = "적용할 전통시장 이름을 입력해 주세요.";
            return false;
        }

        normalizedManifest = Normalize(manifest with
        {
            ReviewStatus = "초안",
            AssociationApprovalStatus = "검토 전",
            PreservesCriticalInformation = true
        });
        message = string.Empty;
        return true;
    }

    internal static bool IsMarketScopeKey(string? scopeKey)
    {
        var normalized = scopeKey?.Trim();
        return normalized is not null
               && normalized.StartsWith(ScopePrefix, StringComparison.OrdinalIgnoreCase)
               && normalized.Length > ScopePrefix.Length;
    }

    private static TraditionalMarketThemeManifest Normalize(TraditionalMarketThemeManifest manifest)
        => manifest with
        {
            MarketScopeKey = manifest.MarketScopeKey.Trim(),
            MarketName = manifest.MarketName.Trim(),
            PreviewBackground = NormalizeColor(manifest.PreviewBackground),
            MarketHeader = NormalizeSlot(manifest.MarketHeader),
            BoardMarker = NormalizeSlot(manifest.BoardMarker),
            MarketDayBanner = NormalizeSlot(manifest.MarketDayBanner),
            ProductMarker = NormalizeSlot(manifest.ProductMarker),
            PickupSign = NormalizeSlot(manifest.PickupSign),
            StoryCover = NormalizeSlot(manifest.StoryCover)
        };

    private static TraditionalMarketThemeSlot NormalizeSlot(TraditionalMarketThemeSlot slot)
        => slot with
        {
            Color = NormalizeColor(slot.Color),
            ImageUrl = NormalizeOptionalText(slot.ImageUrl),
            AltText = NormalizeOptionalText(slot.AltText)
        };

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeColor(string? color)
    {
        var candidate = color?.Trim();
        return candidate is { Length: 7 }
               && candidate[0] == '#'
               && candidate[1..].All(Uri.IsHexDigit)
            ? candidate
            : "#2563eb";
    }
}
