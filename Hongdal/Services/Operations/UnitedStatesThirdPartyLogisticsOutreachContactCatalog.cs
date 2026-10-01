using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Operations;

public static class UnitedStatesThirdPartyLogisticsOutreachContactCatalog
{
    public const string CatalogVersion = "US-3PL-OUTREACH-2026-07-18-01";

    public static DateOnly SnapshotReviewedOn { get; } = new(2026, 7, 18);

    public static IReadOnlyList<ThirdPartyLogisticsOutreachContact> Contacts { get; } =
        Array.AsReadOnly(new[]
        {
            Contact(
                "dhl-express-united-states",
                "DHL Express United States contact",
                "https://www.dhl.com/us-en/home/express/help-and-support/contact-dhl-express.html"),
            Contact(
                "fedex-supply-chain",
                "FedEx Supply Chain inquiry form",
                "https://www.fedex.com/en-us/logistics/supply-chain.html"),
            Contact(
                "geodis",
                "GEODIS United States contact an expert",
                "https://geodis.com/us-en/contact-us"),
            Contact(
                "nfi-industries",
                "NFI services request form",
                "https://www.nfiindustries.com/contact-us/nfi-services-request-form/"),
            Contact(
                "phoenix-warehouse",
                "Phoenix Warehouse contact page",
                "https://phoenix-warehouse.com/contact-us/",
                "info@phoenix-warehouse.com"),
            Contact(
                "ryder-supply-chain-solutions",
                "Ryder business needs contact form",
                "https://www.ryder.com/en-us/contact-us"),
            Contact(
                "seko-logistics",
                "SEKO Logistics new business inquiry",
                "https://www.sekologistics.com/en/contact/"),
            Contact(
                "stg-logistics",
                "STG Logistics sales contact",
                "https://www.stgusa.com/contact/"),
            Contact(
                "ups-supply-chain-solutions",
                "UPS Supply Chain Solutions ask an expert",
                "https://www.ups.com/us/en/supplychain/about/contact-us"),
            Contact(
                "world-distribution-services",
                "World Distribution Services contact form",
                "https://www.worldds.net/contact-us/")
        });

    private static ThirdPartyLogisticsOutreachContact Contact(
        string providerKey,
        string sourceTitle,
        string officialInquiryUrl,
        string? publicBusinessEmail = null)
        => new()
        {
            ProviderKey = providerKey,
            ContactChannelCode = string.IsNullOrWhiteSpace(publicBusinessEmail)
                ? ThirdPartyLogisticsProviderOutreachContactChannelCodes
                    .OfficialInquiryForm
                : ThirdPartyLogisticsProviderOutreachContactChannelCodes
                    .VerifiedPublicBusinessEmail,
            OfficialInquiryUrl = officialInquiryUrl,
            PublicBusinessEmail = publicBusinessEmail ?? string.Empty,
            SourceTitle = sourceTitle,
            SourceUrl = officialInquiryUrl,
            ReviewedOn = SnapshotReviewedOn
        };
}

public sealed class ThirdPartyLogisticsOutreachContact
{
    public string ProviderKey { get; init; } = string.Empty;

    public string ContactChannelCode { get; init; } = string.Empty;

    public string OfficialInquiryUrl { get; init; } = string.Empty;

    public string PublicBusinessEmail { get; init; } = string.Empty;

    public string SourceTitle { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public DateOnly ReviewedOn { get; init; }
}
