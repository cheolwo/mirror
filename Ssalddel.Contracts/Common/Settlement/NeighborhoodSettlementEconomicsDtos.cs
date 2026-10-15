namespace Ssalddel.Contracts.Common.Settlement;

/// <summary>선택 약정의 비용 범위는 출고 기본비용/기타비용과 원천 입고·보관비를 중복 입력하지 않는 별도 범위입니다.</summary>
public sealed class NeighborhoodActivityRewardEconomicsDto
{
    public string PlatformPayeeUserId { get; set; } = "";
    public decimal? PlatformRetentionRatePercent { get; set; }
    public string BasisCode { get; set; } = "PostCostSurplus";
    public IReadOnlyList<NeighborhoodInventoryCostSelectionDto> CostAllocations { get; set; } = [];
}

/// <summary>원천 비용 선택만 받습니다. 귀속 수량·금액은 실제 예약과 확정 원장으로 서버가 계산합니다.</summary>
public sealed class NeighborhoodInventoryCostSelectionDto
{
    public string SourceEntryId { get; set; } = "";
    public long ExpectedEntryRevision { get; set; }
}

/// <summary>불변 회계 귀속입니다. 물리 입고회차 추적이나 원천 작업자의 신규 지급채무가 아닙니다.</summary>
public sealed class NeighborhoodInventoryCostAttributionDto
{
    public string SourceEntryId { get; set; } = "";
    public string SourceAgreementId { get; set; } = "";
    public string LotId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string Kind { get; set; } = "";
    public long SourceEntryRevision { get; set; }
    public decimal SourceQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal AmountKrw { get; set; }
    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    /// <summary>원천 약정·조정·누적 귀속의 변경 감지용 지문입니다. 증빙·개인정보 원문은 포함하지 않습니다.</summary>
    public string EvidenceDigest { get; set; } = "";
}
