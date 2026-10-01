namespace Hongdal.Contracts.Common.CollectiveProcurement;

public static class CollectiveProcurementCostModelCodes
{
    public const string Fixed = "fixed";
    public const string PerUnit = "per-unit";
    public const string CapacityStep = "capacity-step";
    public const string PercentOfSubtotal = "percent-of-subtotal";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Fixed,
        PerUnit,
        CapacityStep,
        PercentOfSubtotal
    };
}

public static class CollectiveProcurementCostCategoryCodes
{
    public const string Goods = "goods";
    public const string OriginPreparation = "origin-preparation";
    public const string OriginPackagingAndLabeling = "origin-packaging-labeling";
    public const string InternationalFreight = "international-freight";
    public const string DestinationHandling = "destination-handling";
    public const string ReworkRisk = "rework-risk";
    public const string DomesticValueAddedProcessing = "domestic-value-added-processing";
    public const string PackagingLabelingAndTraceability = "packaging-labeling-traceability";
    public const string LocalColdChainDelivery = "local-cold-chain-delivery";
    public const string ProcessingYieldLoss = "processing-yield-loss";
}

public static class CollectiveProcurementBenefitKindCodes
{
    public const string BuyerSavings = "buyer-savings";
    public const string SupplierBenefit = "supplier-benefit";
    public const string ProviderBenefit = "provider-benefit";
    public const string FacilitationBenefit = "facilitation-benefit";
    public const string SharedBenefit = "shared-benefit";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        BuyerSavings,
        SupplierBenefit,
        ProviderBenefit,
        FacilitationBenefit,
        SharedBenefit
    };
}

public static class CollectiveProcurementDisclosureLevelCodes
{
    public const string AggregateOnly = "aggregate-only";
    public const string ExactToPlanParticipants = "exact-to-plan-participants";
}

public static class CollectiveProcurementPlanStatusCodes
{
    public const string CollectingDemand = "collecting-demand";
    public const string ResolvingBenefitTerms = "resolving-benefit-terms";
    public const string AwaitingAcceptance = "awaiting-acceptance";
    public const string TargetAgreed = "target-agreed";
    public const string ReadyForExecution = "ready-for-execution";
}

public sealed class CreateCollectiveProcurementPlanRequest
{
    public string Title { get; set; } = string.Empty;
    public string SourceTypeCode { get; set; } = string.Empty;
    public string SourceReferenceId { get; set; } = string.Empty;
    public List<CollectiveProcurementParticipantRequest> Participants { get; set; } = [];
    public CollectiveProcurementAssessmentRequest Assessment { get; set; } = new();
}

public sealed class RecalculateCollectiveProcurementPlanRequest
{
    public long ExpectedPlanRevision { get; set; }
    public List<CollectiveProcurementParticipantRequest>? Participants { get; set; }
    public CollectiveProcurementAssessmentRequest Assessment { get; set; } = new();
}

public sealed class CollectiveProcurementParticipantRequest
{
    public string ReferenceCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
}

public sealed class CollectiveProcurementAssessmentRequest
{
    public string CurrencyCode { get; set; } = "KRW";
    public string QuantityUnit { get; set; } = string.Empty;
    public decimal CurrentCommittedQuantity { get; set; }
    public decimal CurrentPotentialQuantity { get; set; }
    public decimal MinimumOrderQuantity { get; set; }
    public decimal MaximumSafeQuantity { get; set; }
    public decimal QuantityIncrement { get; set; } = 1m;
    public decimal ComparisonUnitPrice { get; set; }
    public decimal TargetSavingsPercent { get; set; }
    public decimal RiskReservePercent { get; set; }
    public decimal? MaximumSingleParticipantBenefitSharePercent { get; set; }
    public List<decimal> CandidateQuantities { get; set; } = [];
    public List<CollectiveProcurementSupplierPriceTierRequest> SupplierPriceTiers { get; set; } = [];
    public List<CollectiveProcurementCostComponentRequest> CostComponents { get; set; } = [];
    public List<CollectiveProcurementBenefitPositionRequest> BenefitPositions { get; set; } = [];
}

public sealed class CollectiveProcurementSupplierPriceTierRequest
{
    public string Label { get; set; } = string.Empty;
    public decimal MinimumQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public DateTimeOffset? ValidUntilUtc { get; set; }
}

public sealed class CollectiveProcurementCostComponentRequest
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string ModelCode { get; set; } = CollectiveProcurementCostModelCodes.Fixed;
    public decimal Amount { get; set; }
    public decimal? CapacityQuantity { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public DateTimeOffset? ValidUntilUtc { get; set; }
}

public sealed class CollectiveProcurementBenefitPositionRequest
{
    public string ParticipantReferenceCode { get; set; } = string.Empty;
    public string BenefitKindCode { get; set; } = CollectiveProcurementBenefitKindCodes.SharedBenefit;
    public decimal ProposedBenefitAmount { get; set; }
    public decimal MinimumAcceptableBenefitAmount { get; set; }
}

public sealed class UpdateCollectiveProcurementDisclosureConsentRequest
{
    public long ExpectedPlanRevision { get; set; }
    public int CalculationRevision { get; set; }
    public bool AllowExactBenefitDisclosure { get; set; }
    public bool ConfirmCommercialInformationHandling { get; set; }
    public bool ConfirmIndependentPricingDecision { get; set; }
}

public sealed class AcceptCollectiveProcurementRevisionRequest
{
    public long ExpectedPlanRevision { get; set; }
    public int CalculationRevision { get; set; }
    public bool ConfirmValuesAreEstimates { get; set; }
    public bool ConfirmIndependentDecision { get; set; }
}

public sealed class CollectiveProcurementPlanResponse
{
    public Guid PlanId { get; set; }
    public long PlanRevision { get; set; }
    public int CurrentCalculationRevision { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SourceTypeCode { get; set; } = string.Empty;
    public string SourceReferenceId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = CollectiveProcurementPlanStatusCodes.CollectingDemand;
    public bool ExactBenefitDisclosureActive { get; set; }
    public bool AllParticipantsAcceptedCurrentRevision { get; set; }
    public bool ExecutionReady { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public CollectiveProcurementDisclosurePolicyResponse DisclosurePolicy { get; set; } = new();
    public CollectiveProcurementAssessmentResponse CurrentAssessment { get; set; } = new();
    public IReadOnlyList<CollectiveProcurementParticipantResponse> Participants { get; set; } = [];
}

public sealed class CollectiveProcurementDisclosurePolicyResponse
{
    public string DefaultDisclosureLevelCode { get; set; } = CollectiveProcurementDisclosureLevelCodes.AggregateOnly;
    public bool ExactDisclosureRequiresEveryParticipantConsent { get; set; } = true;
    public bool PrivateMinimumsAreNeverShared { get; set; } = true;
    public bool NewCalculationRequiresNewConsent { get; set; } = true;
    public bool NewCalculationRequiresNewAcceptance { get; set; } = true;
    public bool PlatformRecommendsCommonPrice { get; set; }
    public string Notice { get; set; }
        = "원가·가격·입찰 등 경쟁상 민감한 정보는 기본 비공개이며, 계산 결과는 참여자의 독립적인 의사결정을 보조하는 추정치입니다.";
}

public sealed class CollectiveProcurementAssessmentResponse
{
    public string CurrencyCode { get; set; } = string.Empty;
    public string QuantityUnit { get; set; } = string.Empty;
    public decimal CurrentCommittedQuantity { get; set; }
    public decimal CurrentPotentialQuantity { get; set; }
    public decimal MinimumOrderQuantity { get; set; }
    public decimal MaximumSafeQuantity { get; set; }
    public decimal? MinimumViableQuantity { get; set; }
    public decimal? RecommendedQuantity { get; set; }
    public decimal? AdditionalQuantityToMinimumViable { get; set; }
    public decimal? AdditionalQuantityToRecommended { get; set; }
    public decimal BenefitPoolAmount { get; set; }
    public decimal TotalProposedBenefitAmount { get; set; }
    public decimal UnallocatedBenefitAmount { get; set; }
    public bool AllocationWithinBenefitPool { get; set; }
    public bool AllParticipantsMeetPrivateMinimums { get; set; }
    public bool BenefitConcentrationWithinAgreedPolicy { get; set; }
    public bool BenefitAgreementReady { get; set; }
    public bool CurrentQuantityEconomicallyViable { get; set; }
    public CollectiveProcurementQuantityScenarioResponse? CurrentCommittedScenario { get; set; }
    public CollectiveProcurementQuantityScenarioResponse? CurrentPotentialScenario { get; set; }
    public CollectiveProcurementQuantityScenarioResponse? MinimumViableScenario { get; set; }
    public CollectiveProcurementQuantityScenarioResponse? RecommendedScenario { get; set; }
    public IReadOnlyList<CollectiveProcurementQuantityScenarioResponse> CandidateScenarios { get; set; } = [];
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public sealed class CollectiveProcurementQuantityScenarioResponse
{
    public decimal Quantity { get; set; }
    public decimal ComparisonTotalCost { get; set; }
    public decimal EstimatedTotalCost { get; set; }
    public decimal EstimatedUnitLandedCost { get; set; }
    public decimal TotalExpectedBenefit { get; set; }
    public decimal SavingsPercent { get; set; }
    public bool MeetsMinimumOrderQuantity { get; set; }
    public bool WithinMaximumSafeQuantity { get; set; }
    public bool MeetsTargetSavings { get; set; }
    public bool EconomicallyViable { get; set; }
}

public sealed class CollectiveProcurementParticipantResponse
{
    public string ReferenceCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string BenefitKindCode { get; set; } = string.Empty;
    public bool IsCurrentViewer { get; set; }
    public bool HasExactDisclosureConsentForCurrentRevision { get; set; }
    public bool HasAcceptedCurrentRevision { get; set; }
    public decimal? ProposedBenefitAmount { get; set; }
    public decimal? BenefitSharePercent { get; set; }
    public decimal? MinimumAcceptableBenefitAmount { get; set; }
    public bool? MeetsPrivateMinimum { get; set; }
}
