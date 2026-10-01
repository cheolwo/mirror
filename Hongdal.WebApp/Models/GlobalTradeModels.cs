namespace Hongdal.WebApp.Models;

public static class GlobalTradeReviewStatus
{
    public const string Published = "published";
    public const string PendingReview = "pending-review";
}

public sealed record GlobalTradeProduct(
    long Id,
    string Slug,
    string ProductName,
    string SupplierName,
    string CountryName,
    string CountryCode,
    string Category,
    string Summary,
    string Description,
    decimal SupplyPrice,
    string CurrencyCode,
    int MinimumOrderQuantity,
    bool SampleAvailable,
    string SuggestedHsCode,
    string Incoterm,
    string CertificationSummary,
    string AccentColor,
    string Symbol,
    string? AffiliateUrl,
    string ReviewStatus,
    DateTimeOffset CreatedAt);

public sealed class GlobalSupplierProductDraft
{
    public string SupplierName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = "Home & Living";
    public string Summary { get; set; } = string.Empty;
    public decimal SupplyPrice { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public int MinimumOrderQuantity { get; set; } = 100;
    public bool SampleAvailable { get; set; } = true;
    public string SuggestedHsCode { get; set; } = string.Empty;
    public string Incoterm { get; set; } = "FOB";
    public string CertificationSummary { get; set; } = string.Empty;
}

public sealed class GlobalImportInterestDraft
{
    public string RequesterName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ExpectedQuantity { get; set; } = 100;
    public string Note { get; set; } = string.Empty;
}

public sealed record GlobalImportInterestRequest(
    long Id,
    long ProductId,
    string ProductSlug,
    string ProductName,
    string SupplierName,
    string RequesterName,
    string CompanyName,
    string Email,
    int ExpectedQuantity,
    string Note,
    string Status,
    DateTimeOffset CreatedAt);

public static class GlobalImportOrderStatus
{
    public const string DraftReview = "수입 검토 초안";
    public const string SupplierDiscussion = "공급자 협의";
    public const string Confirmed = "주문 확정";
}

public sealed class GlobalTradeCommunityThread
{
    public long Id { get; init; }
    public long ProductId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorRole { get; init; } = string.Empty;
    public string OriginalLanguage { get; init; } = "en";
    public string OriginalBody { get; init; } = string.Empty;
    public string? TranslatedBody { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public List<GlobalTradeCommunityComment> Comments { get; } = [];
}

public sealed record GlobalTradeCommunityComment(
    long Id,
    string AuthorName,
    string AuthorRole,
    string OriginalLanguage,
    string OriginalText,
    string? TranslatedText,
    string? LedgerKey,
    DateTimeOffset CreatedAt);

public sealed class GlobalImportOrderDraft
{
    public string ImporterName { get; set; } = string.Empty;
    public string ImporterContact { get; set; } = string.Empty;
    public int OrderQuantity { get; set; } = 100;
    public string Incoterm { get; set; } = "FOB";
    public DateTime? TargetInboundDate { get; set; } = DateTime.Today.AddMonths(2);
}

public sealed record GlobalImportOrderLedger(
    long Id,
    string OrderCode,
    long ProductId,
    string ProductSlug,
    string ProductName,
    string ImporterName,
    string ImporterContact,
    string SupplierName,
    string SupplierCountry,
    string DestinationCountryCode,
    int OrderQuantity,
    decimal UnitPrice,
    string CurrencyCode,
    string Incoterm,
    DateTime? TargetInboundDate,
    string Status,
    long? SourceCommunityThreadId,
    long? SourceImportRequestId,
    IReadOnlyList<GlobalLinkedLedgerNode> LinkedLedgers,
    DateTimeOffset CreatedAt);

public sealed record GlobalLinkedLedgerNode(
    string Key,
    string LedgerType,
    string Title,
    string OwnerRole,
    string Status,
    string Description,
    string Tone,
    string Icon,
    string? Href);
