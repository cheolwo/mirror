namespace Ssalddel.Contracts.Common.Settlement;

/// <summary>원주문의 현재 참조를 따라 읽은 본인 관계 금액입니다. 주문 전체 손익이나 은행 입금 인증이 아닙니다.</summary>
public sealed class 주문별원가보수Dto
{
    public string RootOrderLedgerId { get; set; } = "";
    public long RootRevision { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public string ScopeCode { get; set; } = "ParticipantFinancialOnly";
    public IReadOnlyList<주문정산원천Dto> Sources { get; set; } = [];
    public IReadOnlyList<주문귀속원가Dto> InventoryCosts { get; set; } = [];
    public IReadOnlyList<주문금액분류Dto> AmountGroups { get; set; } = [];
    /// <summary>거래대금이 플랫폼 수입이라는 근거가 없으므로 손익을 임의 계산하지 않습니다.</summary>
    public decimal? PlatformProfitAmountKrw { get; set; }
    public string ProfitStatusCode { get; set; } = "RevenueAndCostBasisUnconfirmed";
    public bool IsActualBankVerified { get; set; }
    public IReadOnlyList<string> ReasonCodes { get; set; } = [];
}

public sealed class 주문정산원천Dto
{
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string SourceDigest { get; set; } = "";
    public DateTime ObservedAtUtc { get; set; }
    public IReadOnlyList<주문작업보수근거Dto> MyCompletedWork { get; set; } = [];
    /// <summary>기존 정산 조회의 본인 지급/수령 관계 필터를 그대로 적용한 결과입니다.</summary>
    public IReadOnlyList<NeighborhoodSettlementSummaryDto> Settlements { get; set; } = [];
}

public sealed class 주문작업보수근거Dto
{
    public string Kind { get; set; } = "";
    public string OriginKind { get; set; } = "";
    public string OriginId { get; set; } = "";
    public long OriginRevision { get; set; }
    public string ActorUserId { get; set; } = "";
    public decimal ConfirmedQuantity { get; set; }
    public decimal AccruedQuantity { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public IReadOnlyList<string> EvidenceReferences { get; set; } = [];
    public string CompensationStatusCode { get; set; } = "AgreementRequired";
    public IReadOnlyList<string> EntryIds { get; set; } = [];
    public IReadOnlyList<string> ReferenceEntryIds { get; set; } = [];
}

/// <summary>한 주문에 이미 귀속된 입고/보관 원가입니다. 원천 작업자에게 다시 지급할 금액이 아닙니다.</summary>
public sealed class 주문귀속원가Dto
{
    public string AttributionId { get; set; } = "";
    public string CalculationDigest { get; set; } = "";
    public NeighborhoodInventoryCostAttributionDto Attribution { get; set; } = new();
    public string CostBearerUserId { get; set; } = "";
    public string SourceEntryStatusCode { get; set; } = "";
    public long CurrentSourceEntryRevision { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public string AccountingCode { get; set; } = "AllocatedCostNotNewPayable";
}

/// <summary>발생한 본인 항목만 분류합니다. 상품대금, 보수, 통과자금, 원가 귀속을 하나의 수입으로 합치지 않습니다.</summary>
public sealed class 주문금액분류Dto
{
    public string Kind { get; set; } = "";
    public string AccountingCode { get; set; } = "Payable";
    public int ReferenceEntryCount { get; set; }
    public decimal? ReferenceAmountKrw { get; set; }
    public int UnknownReferenceCount { get; set; }
    public int AccruedEntryCount { get; set; }
    public decimal KnownAccruedAmountKrw { get; set; }
    /// <summary>항목이 없거나 금액 미확인이면 null입니다. 명시적인 0원과 구별합니다.</summary>
    public decimal? CompleteAccruedAmountKrw { get; set; }
    public int UnknownAccruedCount { get; set; }
    public int PendingAgreementLineCount { get; set; }
    public decimal ReportedPaymentAmountKrw { get; set; }
    public decimal RecipientConfirmedAmountKrw { get; set; }
    public bool IsActualBankVerified { get; set; }
}
