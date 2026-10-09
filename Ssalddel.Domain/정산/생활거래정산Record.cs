namespace 살뜰.도메인.정산;

/// <summary>Current agreement pointer. Monetary terms and participant consents are encrypted JSON.</summary>
public sealed class NeighborhoodSettlementAgreementRecord
{
    public string AgreementId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string CreatorUserId { get; set; } = "";
    public string ActivityKind { get; set; } = "";
    public int TermsVersion { get; set; } = 1;
    public long Revision { get; set; } = 1;
    public string TermsJson { get; set; } = "{}";
    public string SourceSnapshotJson { get; set; } = "{}";
    public string ConsentsJson { get; set; } = "[]";
    public string HistoryJson { get; set; } = "[]";
    public string StatusCode { get; set; } = "Draft";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Immutable terms/source snapshot for one version; consent evidence may be appended for that exact version.</summary>
public sealed class NeighborhoodSettlementAgreementVersionRecord
{
    public string AgreementId { get; set; } = "";
    public int TermsVersion { get; set; }
    public string TermsJson { get; set; } = "{}";
    public string SourceSnapshotJson { get; set; } = "{}";
    public string ConsentsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Frozen accrual basis and participant money are private details, separate from work completion.</summary>
public sealed class NeighborhoodSettlementEntryRecord
{
    public string EntryId { get; set; } = "";
    public string AgreementId { get; set; } = "";
    public int TermsVersion { get; set; }
    public string LineKey { get; set; } = "";
    public string Kind { get; set; } = "";
    public string OriginKind { get; set; } = "";
    public string OriginId { get; set; } = "";
    public long OriginRevision { get; set; }
    public string AccrualKey { get; set; } = "";
    public string? CanonicalDeliveryOrigin { get; set; }
    public string? OriginalEntryId { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public string StatusCode { get; set; } = "Accrued";
    public long Revision { get; set; } = 1;
    public DateTime RecordedAtUtc { get; set; }
}

/// <summary>A payer report and recipient statement. No bank verification is inferred.</summary>
public sealed class NeighborhoodSettlementPaymentRecord
{
    public string PaymentId { get; set; } = "";
    public string AgreementId { get; set; } = "";
    public string DetailsJson { get; set; } = "{}";
    public string StatusCode { get; set; } = "Reported";
    public long Revision { get; set; } = 1;
    public DateTime ReportedAtUtc { get; set; }
    public DateTime? RecipientConfirmedAtUtc { get; set; }
}

/// <summary>Exact actor/request replay receipt; private response does not expose another participant's ledger.</summary>
public sealed class NeighborhoodSettlementCommandReceiptRecord
{
    public string ActorUserId { get; set; } = "";
    public Guid RequestId { get; set; }
    public string AgreementId { get; set; } = "";
    public string InputDigest { get; set; } = "";
    public string Kind { get; set; } = "";
    public long Revision { get; set; }
    public string ResponseJson { get; set; } = "{}";
    public DateTime RecordedAtUtc { get; set; }
}
