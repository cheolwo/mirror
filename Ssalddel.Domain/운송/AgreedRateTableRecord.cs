namespace Ssalddel.Domain.Pricing;

/// <summary>당사자 합의와 판본. 새 요율로 기존 수행 원장을 다시 계산하지 않습니다.</summary>
public sealed class AgreedRateTableRecord
{
    public string AgreementId { get; set; } = string.Empty;
    public string BusinessKind { get; set; } = string.Empty;
    public string VehicleCode { get; set; } = string.Empty;
    public string ProposerKind { get; set; } = string.Empty;
    public string ProposerId { get; set; } = string.Empty;
    public string CounterpartyKind { get; set; } = string.Empty;
    public string CounterpartyId { get; set; } = string.Empty;
    public string TermsJson { get; set; } = string.Empty;
    public string TermsDigest { get; set; } = string.Empty;
    public string StatusCode { get; set; } = "Proposed";
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveUntilUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public long Revision { get; set; } = 1;
    public string HistoryJson { get; set; } = "[]";
}

/// <summary>인증된 당사자·요청 ID·입력 지문에 결속한 확정 결과. 응답 유실 뒤 새 합의를 만들지 않습니다.</summary>
public sealed class AgreedRateTableCommandReceipt
{
    public string PartyKind { get; set; } = string.Empty;
    public string PartyId { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public string InputDigest { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}

/// <summary>기존 업무에 명시한 합의 선택. 첫 제안 금액은 기존 업무의 동결 근거가 소유합니다.</summary>
public sealed class AgreedRateTableSelectionRecord
{
    public Guid SelectionRequestId { get; set; }
    public string BusinessKind { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string AgreementId { get; set; } = string.Empty;
    public long AgreementRevision { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string SelectingPartyKind { get; set; } = string.Empty;
    public string SelectingPartyId { get; set; } = string.Empty;
    public DateTime SelectedAtUtc { get; set; }
    public decimal? AgreedAmountKrw { get; set; }
    public decimal? DistanceKm { get; set; }
    public string DistanceBasisCode { get; set; } = "Unknown";
    public string SynchronizationStatusCode { get; set; } = "NotRequired";
}
