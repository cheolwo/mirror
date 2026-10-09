using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Common.Fulfillment;

namespace Ssalddel.Contracts.Common.Pricing;

public static class AgreedRateBusinessKinds
{
    public const string FoodDelivery = "FoodDelivery";
    public const string CargoTransport = "CargoTransport";
}

public static class AgreedRatePartyKinds
{
    public const string Restaurant = "Restaurant";
    public const string FoodDriver = "FoodDriver";
    public const string Shipper = "Shipper";
    public const string CargoDriver = "CargoDriver";
}

public sealed record AgreedRatePartyDto(string PartyKind, string PartyId);
public sealed record AgreedCargoPricingDto(decimal BaseFareKrw, decimal PerKmKrw, decimal MinimumFareKrw);
public sealed record AgreedRateTableSnapshotDto(
    string AgreementId, long Revision, string BusinessKind, string VehicleCode,
    AgreedRatePartyDto Proposer, AgreedRatePartyDto Counterparty,
    FoodDeliveryPayoutPricingPolicy? FoodPricing, AgreedCargoPricingDto? CargoPricing,
    DateTime EffectiveFromUtc, DateTime EffectiveUntilUtc, DateTime AcceptedAtUtc, string TermsDigest);

public sealed class AgreedRateTableDto
{
    public string AgreementId { get; set; } = string.Empty;
    public string BusinessKind { get; set; } = string.Empty;
    public string VehicleCode { get; set; } = string.Empty;
    public AgreedRatePartyDto Proposer { get; set; } = new("", "");
    public AgreedRatePartyDto Counterparty { get; set; } = new("", "");
    public AgreedRatePartyDto CurrentParty { get; set; } = new("", "");
    public FoodDeliveryPayoutPricingPolicy? FoodPricing { get; set; }
    public AgreedCargoPricingDto? CargoPricing { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveUntilUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public long Revision { get; set; }
    public string StatusCode { get; set; } = "Proposed";
    public string EffectiveStatusCode { get; set; } = "NotAccepted";
    public bool CanAccept { get; set; }
    public bool CanReject { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanSelect { get; set; }
    public bool CanPreview { get; set; }
    public IReadOnlyList<AgreedRateTableEventDto> History { get; set; } = [];
}
public sealed record AgreedRateTableEventDto(string ActionCode, AgreedRatePartyDto Actor, long Revision, DateTime OccurredAtUtc);

public sealed record AgreedRateTableProposalRequest
{
    public string ActorPartyKind { get; set; } = string.Empty;
    public string CounterpartyPartyKind { get; set; } = string.Empty;
    public string CounterpartyPartyId { get; set; } = string.Empty;
    public string BusinessKind { get; set; } = string.Empty;
    public string VehicleCode { get; set; } = string.Empty;
    public FoodDeliveryPayoutPricingPolicy? FoodPricing { get; set; }
    public AgreedCargoPricingDto? CargoPricing { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveUntilUtc { get; set; }
    public Guid RequestId { get; set; }
}
public sealed class AgreedRateTableActionRequest
{
    public string ActorPartyKind { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public long ExpectedRevision { get; set; }
    public Guid RequestId { get; set; }
}
public sealed record AgreedRateTablePreviewRequest
{
    public string ActorPartyKind { get; set; } = string.Empty;
    public long ExpectedRevision { get; set; }
    public decimal DistanceKm { get; set; }
    public string DistanceBasisCode { get; set; } = string.Empty;
    public decimal WaitingFeeKrw { get; set; }
    public decimal ManualWorkFeeKrw { get; set; }
    public decimal SurchargeKrw { get; set; }
    public DeliveryCostSharingPolicy? CostSharingPolicy { get; set; }
    public decimal? MaxUserChargeKrw { get; set; }
}
public sealed record AgreedRateTablePreviewDto(
    AgreedRateTableSnapshotDto Agreement, decimal TotalAmountKrw,
    FoodDeliveryPayoutReviewLine? FoodBreakdown,
    decimal? RestaurantContributionKrw, decimal? UserChargeKrw,
    string StatusCode = "PreviewOnly");

public sealed class AgreedRateTableSelectionRequest
{
    public string ActorPartyKind { get; set; } = string.Empty;
    public long ExpectedRevision { get; set; }
    /// <summary>음식 주문번호 또는 화주 의뢰 ID. 기존 업무의 소유권과 미확정 상태를 서버에서 검증합니다.</summary>
    public string WorkId { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
}
public sealed record AgreedRateTableSelectionDto(string BusinessKind, string WorkId, AgreedRateTableSnapshotDto Agreement)
{
    public Guid SelectionRequestId { get; init; }
    public bool CanRetrySynchronization { get; init; }
    public decimal? AgreedAmountKrw { get; init; }
    public decimal? DistanceKm { get; init; }
    public string DistanceBasisCode { get; init; } = "Unknown";
    public string StatusCode { get; init; } = "SelectedForNextOffer";
}
