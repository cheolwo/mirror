namespace Hongdal.Contracts.Common.Orderer;

public static class DomesticProducerDirectoryIntegrationStatuses
{
    public const string NotConnected = "not-connected";
    public const string Connected = "connected";
}

public static class DomesticProducerContactRequestStatuses
{
    public const string Draft = "draft";
    public const string Requested = "requested";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
}

public static class DomesticProducerSupplyOfferReasonCodes
{
    public const string Overproduction = "overproduction";
    public const string OffGrade = "off-grade";
    public const string ShippingDeadline = "shipping-deadline";
    public const string SalesChannelGap = "sales-channel-gap";
    public const string Other = "other";
}

public static class DomesticProducePackagingFormCodes
{
    public const string CorrugatedBox = "corrugated-box";
    public const string ReusableCrate = "reusable-crate";
    public const string Pallet = "pallet";
    public const string Bulk = "bulk";
    public const string Other = "other";
}

public sealed class DomesticProducerCandidateQueryResponse
{
    public string IntegrationStatusCode { get; set; } = DomesticProducerDirectoryIntegrationStatuses.NotConnected;
    public string IntegrationMessage { get; set; } = string.Empty;
    public bool ContactDetailsDisclosed { get; set; }
    public IReadOnlyList<DomesticProducerCandidateResponse> Items { get; set; } = [];
}

public sealed class DomesticProducerCandidateResponse
{
    public string CandidateKey { get; set; } = string.Empty;
    public string MaskedDisplayName { get; set; } = string.Empty;
    public string RegionCode { get; set; } = string.Empty;
    public string RegionLabel { get; set; } = string.Empty;
    public IReadOnlyList<string> ProductTags { get; set; } = [];
    public IReadOnlyList<string> SupportedPackagingFormCodes { get; set; } = [];
    public string SupplyCapacitySummary { get; set; } = string.Empty;
    public string AvailabilitySummary { get; set; } = string.Empty;
    public string VerificationLabel { get; set; } = string.Empty;
    public bool ThirdPartySharingConsentConfirmed { get; set; }
    public bool ContactRequestConsentConfirmed { get; set; }
}

public sealed class DomesticGroupPurchaseRepresentativeCandidateQueryResponse
{
    public string IntegrationStatusCode { get; set; } = DomesticProducerDirectoryIntegrationStatuses.NotConnected;
    public string IntegrationMessage { get; set; } = string.Empty;
    public bool ContactDetailsDisclosed { get; set; }
    public IReadOnlyList<DomesticGroupPurchaseRepresentativeCandidateResponse> Items { get; set; } = [];
}

public sealed class DomesticGroupPurchaseRepresentativeCandidateResponse
{
    public string CandidateKey { get; set; } = string.Empty;
    public string MaskedDisplayName { get; set; } = string.Empty;
    public string OperatingAreaCode { get; set; } = string.Empty;
    public string OperatingAreaLabel { get; set; } = string.Empty;
    public string CommunitySummary { get; set; } = string.Empty;
    public IReadOnlyList<string> InterestedProductTags { get; set; } = [];
    public string TypicalAbsorptionCapacitySummary { get; set; } = string.Empty;
    public IReadOnlyList<string> SupportedReceiptMethods { get; set; } = [];
    public string RecruitmentSummary { get; set; } = string.Empty;
    public string VerificationLabel { get; set; } = string.Empty;
    public bool RepresentativeRoleConfirmed { get; set; }
    public bool ContactRequestConsentConfirmed { get; set; }
}

public sealed class DomesticProducerContactRequestDraftRequest
{
    public Guid GroupPurchaseCampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string ProducerCandidateKey { get; set; } = string.Empty;
    public string ProducerMaskedDisplayName { get; set; } = string.Empty;
    public string ProductSummary { get; set; } = string.Empty;
    public string RequestedQuantitySummary { get; set; } = string.Empty;
    public string RequiredPackagingFormCode { get; set; } = DomesticProducePackagingFormCodes.CorrugatedBox;
    public string PackagingUnitSummary { get; set; } = string.Empty;
    public string QualityGradeSummary { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal MaximumAbsorptionQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public bool CanReceiveSplitShipments { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class DomesticProducerContactRequestDraftResponse
{
    public Guid DraftId { get; set; }
    public Guid GroupPurchaseCampaignId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public string ProducerCandidateKey { get; set; } = string.Empty;
    public string ProducerMaskedDisplayName { get; set; } = string.Empty;
    public string CampaignTitle { get; set; } = string.Empty;
    public string ProductSummary { get; set; } = string.Empty;
    public string RequestedQuantitySummary { get; set; } = string.Empty;
    public string RequiredPackagingFormCode { get; set; } = DomesticProducePackagingFormCodes.CorrugatedBox;
    public string PackagingUnitSummary { get; set; } = string.Empty;
    public string QualityGradeSummary { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal MaximumAbsorptionQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public bool CanReceiveSplitShipments { get; set; }
    public string Message { get; set; } = string.Empty;
    public string StatusCode { get; set; } = DomesticProducerContactRequestStatuses.Draft;
    public bool ContactDetailsDisclosed { get; set; }
    public bool IsDurablyPersisted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string GuidanceMessage { get; set; } = string.Empty;
}

public sealed class DomesticProducerSupplyOfferDraftRequest
{
    public Guid GroupPurchaseCampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string RepresentativeCandidateKey { get; set; } = string.Empty;
    public string RepresentativeMaskedDisplayName { get; set; } = string.Empty;
    public string ProducerMaskedDisplayName { get; set; } = string.Empty;
    public string ProducerRegionSummary { get; set; } = string.Empty;
    public string ProductSummary { get; set; } = string.Empty;
    public string AvailableQuantitySummary { get; set; } = string.Empty;
    public IReadOnlyList<string> SupportedPackagingFormCodes { get; set; } = [];
    public decimal AvailableQuantity { get; set; }
    public decimal MinimumTakeQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public bool CanSplitShipments { get; set; }
    public string ExpectedPriceSummary { get; set; } = string.Empty;
    public string SupplyDeadlineSummary { get; set; } = string.Empty;
    public string OfferReasonCode { get; set; } = DomesticProducerSupplyOfferReasonCodes.Overproduction;
    public string QualityDisclosure { get; set; } = string.Empty;
    public bool FoodSafetyConfirmed { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class DomesticProducerSupplyOfferDraftResponse
{
    public Guid DraftId { get; set; }
    public Guid GroupPurchaseCampaignId { get; set; }
    public string OfferedByUserId { get; set; } = string.Empty;
    public string RepresentativeCandidateKey { get; set; } = string.Empty;
    public string RepresentativeMaskedDisplayName { get; set; } = string.Empty;
    public string ProducerMaskedDisplayName { get; set; } = string.Empty;
    public string CampaignTitle { get; set; } = string.Empty;
    public string ProductSummary { get; set; } = string.Empty;
    public string AvailableQuantitySummary { get; set; } = string.Empty;
    public IReadOnlyList<string> SupportedPackagingFormCodes { get; set; } = [];
    public decimal AvailableQuantity { get; set; }
    public decimal MinimumTakeQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public bool CanSplitShipments { get; set; }
    public string ExpectedPriceSummary { get; set; } = string.Empty;
    public string SupplyDeadlineSummary { get; set; } = string.Empty;
    public string OfferReasonCode { get; set; } = string.Empty;
    public string QualityDisclosure { get; set; } = string.Empty;
    public bool FoodSafetyConfirmed { get; set; }
    public string Message { get; set; } = string.Empty;
    public string StatusCode { get; set; } = DomesticProducerContactRequestStatuses.Draft;
    public bool ContactDetailsDisclosed { get; set; }
    public bool IsDurablyPersisted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string GuidanceMessage { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseSupplyCompatibilityPreviewRequest
{
    public string BuyerRequiredPackagingFormCode { get; set; } = DomesticProducePackagingFormCodes.CorrugatedBox;
    public decimal BuyerRequestedQuantity { get; set; }
    public decimal BuyerMaximumAbsorptionQuantity { get; set; }
    public bool BuyerCanReceiveSplitShipments { get; set; }
    public IReadOnlyList<string> ProducerSupportedPackagingFormCodes { get; set; } = [];
    public decimal ProducerAvailableQuantity { get; set; }
    public decimal ProducerMinimumTakeQuantity { get; set; }
    public bool ProducerCanSplitShipments { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseSupplyCompatibilityPreviewResponse
{
    public bool ProducerCanMeetPackaging { get; set; }
    public bool ProducerCanMeetRequestedQuantity { get; set; }
    public bool BuyerMeetsMinimumTakeQuantity { get; set; }
    public bool BuyerCanAbsorbFullOffer { get; set; }
    public bool SplitShipmentCanResolveVolumeGap { get; set; }
    public bool IsMutuallyFeasible { get; set; }
    public IReadOnlyList<string> UnresolvedConditions { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}
