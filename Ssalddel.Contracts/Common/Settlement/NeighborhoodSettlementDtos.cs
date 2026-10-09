namespace Ssalddel.Contracts.Common.Settlement;

/// <summary>업무와 금액 흐름을 구분하는 고정 코드입니다. 지급 완료를 의미하지 않습니다.</summary>
public static class NeighborhoodSettlementCodes
{
    public const string PreparedFood = "PreparedFood", StoredGoods = "StoredGoods", TransportOnly = "TransportOnly";
    public const string DistributedOrder = "DistributedOrder", DistributedLot = "DistributedLot", FoodOrder = "FoodOrder", CargoRequest = "CargoRequest";
    public const string OwnerDirect = "OwnerDirect", SellerCollect = "SellerCollect";
    public const string ProductSale = "ProductSale", SalesAgency = "SalesAgency", Storage = "Storage", Inbound = "Inbound", Picking = "Picking", Delivery = "Delivery", OwnerRemittance = "OwnerRemittance", Adjustment = "Adjustment";
    public const string Fixed = "Fixed", Free = "Free", PerUnit = "PerUnit", StoragePeriod = "StoragePeriod";
    public const string Ambient = "Ambient", Chilled = "Chilled", Frozen = "Frozen";
    public const string Draft = "Draft", AwaitingConsent = "AwaitingConsent", Active = "Active", Superseded = "Superseded", Revoked = "Revoked";
    public const string Expected = "Expected", Accrued = "Accrued", Confirmed = "Confirmed", Disputed = "Disputed";
    public const string Reported = "Reported", PartiallyConfirmed = "PartiallyConfirmed", RecipientConfirmed = "RecipientConfirmed";
}

public static class NeighborhoodSettlementRoles
{
    public const string Buyer = "Buyer", Owner = "Owner", Seller = "Seller", SpaceOwner = "SpaceOwner", Receiver = "Receiver", Picker = "Picker", Driver = "Driver";
}

public class NeighborhoodSettlementCommand
{
    public Guid RequestId { get; set; }
    public long ExpectedRevision { get; set; }
}

public sealed class NeighborhoodSettlementTermLine
{
    public string LineKey { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Billing { get; set; } = NeighborhoodSettlementCodes.Fixed;
    /// <summary>null은 미확정 금액이며, 0은 명시적으로 합의한 금액입니다.</summary>
    public decimal? AmountKrw { get; set; }
    public decimal? UnitPriceKrw { get; set; }
    public decimal? Quantity { get; set; }
    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    public string PayerUserId { get; set; } = "";
    public string PayeeUserId { get; set; } = "";
    public string BeneficiaryRole { get; set; } = "";
    public bool IncludedInDelivery { get; set; }
    public string? OriginalLineKey { get; set; }
    /// <summary>기사 원장에 기록된 배송비 한 건을 누가 부담할지 정하는 합의 계획입니다. 지급 기록은 아닙니다.</summary>
    public IReadOnlyList<NeighborhoodSettlementDeliveryCostShare> DeliveryCostShares { get; set; } = [];
}

/// <summary>배송비 부담 주체와 합의 금액입니다. null은 미확정이며 실제 지급 완료를 의미하지 않습니다.</summary>
public sealed class NeighborhoodSettlementDeliveryCostShare
{
    public string PayerUserId { get; set; } = "";
    public decimal? AmountKrw { get; set; }
}

public sealed class NeighborhoodSettlementAgreementRequest : NeighborhoodSettlementCommand
{
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string ActivityKind { get; set; } = "";
    public string GoodsPaymentRoute { get; set; } = NeighborhoodSettlementCodes.OwnerDirect;
    public string? GoodsOwnerUserId { get; set; }
    public string? SellerUserId { get; set; }
    public string Handling { get; set; } = NeighborhoodSettlementCodes.Ambient;
    public bool Perishable { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public IReadOnlyList<NeighborhoodSettlementTermLine> Lines { get; set; } = [];
}

public sealed class NeighborhoodSettlementConsentRequest : NeighborhoodSettlementCommand
{
    public int TermsVersion { get; set; }
    public bool Granted { get; set; }
}

public sealed class NeighborhoodSettlementAccrualLine
{
    public string LineKey { get; set; } = "";
    public decimal? Quantity { get; set; }
    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
}

public sealed class NeighborhoodSettlementAccrualRequest : NeighborhoodSettlementCommand
{
    public string OriginKind { get; set; } = "";
    public string OriginId { get; set; } = "";
    public IReadOnlyList<NeighborhoodSettlementAccrualLine> Lines { get; set; } = [];
}

public sealed class NeighborhoodSettlementEntryConfirmRequest : NeighborhoodSettlementCommand
{
    public string EntryId { get; set; } = "";
}

public sealed class NeighborhoodSettlementDisputeRequest : NeighborhoodSettlementCommand
{
    public string EntryId { get; set; } = "";
    public string Reason { get; set; } = "";
}

/// <summary>조정액은 원본에 연결된 새 증감 항목입니다. 원본 금액을 덮어쓰지 않습니다.</summary>
public sealed class NeighborhoodSettlementAdjustmentRequest : NeighborhoodSettlementCommand
{
    public string OriginalEntryId { get; set; } = "";
    public decimal AmountKrw { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class NeighborhoodSettlementPaymentAllocation
{
    public string EntryId { get; set; } = "";
    public decimal AmountKrw { get; set; }
}

public sealed class NeighborhoodSettlementPaymentReportRequest : NeighborhoodSettlementCommand
{
    public decimal AmountKrw { get; set; }
    public IReadOnlyList<NeighborhoodSettlementPaymentAllocation> Allocations { get; set; } = [];
    public string EvidenceReference { get; set; } = "";
}

/// <summary>수령 확인은 당사자의 진술이며, 은행의 입금 검증을 의미하지 않습니다.</summary>
public sealed class NeighborhoodSettlementPaymentReceiptRequest : NeighborhoodSettlementCommand
{
    public string PaymentId { get; set; } = "";
    public decimal ConfirmedAmountKrw { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class NeighborhoodSettlementConsentDto
{
    public string UserId { get; set; } = "";
    public int TermsVersion { get; set; }
    public bool Granted { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

public sealed class NeighborhoodSettlementAgreementDto
{
    public string AgreementId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public int TermsVersion { get; set; }
    public long Revision { get; set; }
    public string StatusCode { get; set; } = "";
    public NeighborhoodSettlementAgreementRequest Terms { get; set; } = new();
    public IReadOnlyList<NeighborhoodSettlementConsentDto> Consents { get; set; } = [];
    public IReadOnlyList<string> RequiredConsentUserIds { get; set; } = [];
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class NeighborhoodSettlementEntryDto
{
    public string EntryId { get; set; } = "";
    public string AgreementId { get; set; } = "";
    public int TermsVersion { get; set; }
    public string LineKey { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Billing { get; set; } = "";
    public decimal? AmountKrw { get; set; }
    public decimal? UnitPriceKrw { get; set; }
    public decimal? Quantity { get; set; }
    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    public string PayerUserId { get; set; } = "";
    public string PayeeUserId { get; set; } = "";
    public string BeneficiaryRole { get; set; } = "";
    public string OriginKind { get; set; } = "";
    public string OriginId { get; set; } = "";
    public long OriginRevision { get; set; }
    public string CalculationBasis { get; set; } = "";
    public bool IsInternal { get; set; }
    public bool IsTransit { get; set; }
    public bool IncludedInDelivery { get; set; }
    public string? OriginalEntryId { get; set; }
    public string StatusCode { get; set; } = "";
    public long Revision { get; set; }
    public IReadOnlyList<string> ConfirmationUserIds { get; set; } = [];
    public string? DisputeReason { get; set; }
    public IReadOnlyList<string> WorkEvidenceReferences { get; set; } = [];
    public DateTime RecordedAtUtc { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
}

public sealed class NeighborhoodSettlementPaymentDto
{
    public string PaymentId { get; set; } = "";
    public string AgreementId { get; set; } = "";
    public string PayerUserId { get; set; } = "";
    public string PayeeUserId { get; set; } = "";
    public decimal AmountKrw { get; set; }
    public decimal RecipientConfirmedAmountKrw { get; set; }
    public string EvidenceReference { get; set; } = "";
    public IReadOnlyList<NeighborhoodSettlementPaymentAllocation> Allocations { get; set; } = [];
    public string StatusCode { get; set; } = "";
    public long Revision { get; set; }
    public bool IsActualBankVerified { get; set; }
    public string? RecipientReason { get; set; }
    public DateTime ReportedAtUtc { get; set; }
    public DateTime? RecipientConfirmedAtUtc { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
}

public sealed class NeighborhoodSettlementTotalsDto
{
    public decimal KnownAccruedAmountKrw { get; set; }
    public decimal KnownConfirmedAmountKrw { get; set; }
    public decimal ReportedPaymentAmountKrw { get; set; }
    public decimal RecipientConfirmedAmountKrw { get; set; }
    public decimal OutstandingAmountKrw { get; set; }
    public int UnresolvedLineCount { get; set; }
    public int InternalEntryCount { get; set; }
    public int TransitEntryCount { get; set; }
    public bool IsActualBankVerified { get; set; }
}

public sealed class NeighborhoodSettlementSummaryDto
{
    public NeighborhoodSettlementAgreementDto Agreement { get; set; } = new();
    public IReadOnlyList<NeighborhoodSettlementEntryDto> Entries { get; set; } = [];
    public IReadOnlyList<NeighborhoodSettlementPaymentDto> Payments { get; set; } = [];
    public NeighborhoodSettlementTotalsDto Totals { get; set; } = new();
}

public sealed class NeighborhoodSettlementReceiptDto
{
    public Guid RequestId { get; set; }
    public string AgreementId { get; set; } = "";
    public string Kind { get; set; } = "";
    public long Revision { get; set; }
    public string StatusCode { get; set; } = "";
    public IReadOnlyList<string> EntryIds { get; set; } = [];
    public string? PaymentId { get; set; }
    public bool Replayed { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
