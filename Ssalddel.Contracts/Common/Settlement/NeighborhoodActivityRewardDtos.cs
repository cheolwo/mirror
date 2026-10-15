using System.Text.Json.Serialization;

namespace Ssalddel.Contracts.Common.Settlement;

/// <summary>계산 준비 상태이며 업무 완료, 실재원, 확정 영업이익 또는 실제 입금을 인증하지 않습니다.</summary>
public static class NeighborhoodActivityRewardCodes
{
    public const string WaitingForEvidence = "WaitingForEvidence", Ready = "Ready", InvalidInput = "InvalidInput";
    public const string SimulationPartyStatement = "SimulationPartyStatement";
    public const string Unit = "Unit", StorageDay = "StorageDay";
    public const string KrwFloorThenLargestRemainder = "KrwFloorThenLargestRemainder";
}

/// <summary>기본 작업 보수와 구별하는 추가 성과금 합의입니다. 동의 여부는 서버의 약정 판본이 소유합니다.</summary>
public sealed class NeighborhoodActivityRewardAgreementDto
{
    public string PayerUserId { get; set; } = "";
    public IReadOnlyList<string> ParticipantUserIds { get; set; } = [];
    public NeighborhoodActivityRewardPolicyDto Policy { get; set; } = new();
    public NeighborhoodActivityRewardFinancialBasisDto FinancialBasis { get; set; } = new();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NeighborhoodActivityRewardEconomicsDto? Economics { get; set; }
}

public sealed class NeighborhoodActivityRewardPolicyDto
{
    public string PolicyVersion { get; set; } = "";
    /// <summary>null은 미합의입니다. 비율의 기본값을 제공하지 않습니다.</summary>
    public decimal? ReturnRatePercent { get; set; }
    public IReadOnlyList<NeighborhoodActivityRewardWeightDto> Weights { get; set; } = [];
}

/// <summary>수행 종류와 단위별 사전 합의 가중치입니다. 0은 명시적 배분 제외이며 null은 미합의입니다.</summary>
public sealed class NeighborhoodActivityRewardWeightDto
{
    public string Kind { get; set; } = "";
    public string UnitCode { get; set; } = "";
    public decimal? Weight { get; set; }
}

/// <summary>
/// Simulation에서 당사자가 합의하는 경제 입력 진술입니다. 기본 보수 비용과 기타 비용을 구별하며
/// 같은 비용을 두 항목에 중복 입력하지 않습니다. 실제 수입·은행 대사·확정 영업이익은 인증하지 않습니다.
/// 금액은 정수 KRW이고 null은 미확인, 0은 명시적으로 확인한 0입니다.
/// </summary>
public sealed class NeighborhoodActivityRewardFinancialBasisDto
{
    public string BasisKind { get; set; } = "";
    public string BasisReference { get; set; } = "";
    public decimal? RevenueAmountKrw { get; set; }
    public decimal? BasicWorkCostAmountKrw { get; set; }
    public decimal? OtherCostAmountKrw { get; set; }
    public decimal? ReserveAmountKrw { get; set; }
}

/// <summary>
/// 서버가 정본 업무와 최종 완료 수량의 연결을 대조한 기여입니다. 요청자가 주장하는 수행 근거로 쓰지 않습니다.
/// Quantity는 실제 완료 수량 또는 확인된 수량×보관일이며 UnitCode가 약정 단위와 일치해야 합니다.
/// </summary>
public sealed class NeighborhoodActivityRewardContributionDto
{
    public string ContributionId { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string UnitCode { get; set; } = "";
    public decimal Quantity { get; set; }
    public string OriginKind { get; set; } = "";
    public string OriginId { get; set; } = "";
    public long OriginRevision { get; set; }
    public IReadOnlyList<string> EvidenceReferences { get; set; } = [];
}

public sealed class NeighborhoodActivityRewardContributionShareDto
{
    public NeighborhoodActivityRewardContributionDto Contribution { get; set; } = new();
    public decimal AgreedWeight { get; set; }
    public decimal WeightedUnits { get; set; }
    public decimal AmountKrw { get; set; }
}

/// <summary>동일인의 여러 역할을 합산한 성과금입니다. 기본 작업 보수는 포함하지 않습니다.</summary>
public sealed class NeighborhoodActivityRewardAllocationDto
{
    public string ActorUserId { get; set; } = "";
    public decimal WeightedUnits { get; set; }
    public decimal AmountKrw { get; set; }
    public IReadOnlyList<string> ContributionIds { get; set; } = [];
}

public sealed class NeighborhoodActivityRewardPreviewDto
{
    public string StatusCode { get; set; } = NeighborhoodActivityRewardCodes.WaitingForEvidence;
    public string CalculationVersion { get; set; } = "";
    public string PolicyVersion { get; set; } = "";
    public string CalculationDigest { get; set; } = "";
    public bool AlreadyAccrued { get; set; }
    public string FundingPayerUserId { get; set; } = "";
    public IReadOnlyList<string> ReasonCodes { get; set; } = [];
    public decimal? SurplusAmountKrw { get; set; }
    public decimal? UnroundedPoolAmountKrw { get; set; }
    public decimal? PoolAmountKrw { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PlatformRetentionAmountKrw { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PlatformRetentionRatePercent { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlatformPayeeUserId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? CostAttributionAmountKrw { get; set; }
    public IReadOnlyList<NeighborhoodInventoryCostAttributionDto> CostAttributions { get; set; } = [];
    /// <summary>성과금 풀을 정수 원으로 내린 뒤 남는 1원 미만 금액입니다. 지급액에 조용히 합치지 않습니다.</summary>
    public decimal? FractionalPoolRemainderKrw { get; set; }
    public string RoundingCode { get; set; } = NeighborhoodActivityRewardCodes.KrwFloorThenLargestRemainder;
    public IReadOnlyList<NeighborhoodActivityRewardAllocationDto> Allocations { get; set; } = [];
    public IReadOnlyList<NeighborhoodActivityRewardContributionShareDto> ContributionShares { get; set; } = [];
    public bool IsActualBankVerified { get; set; }
    public string Notice { get; set; } = "Simulation 당사자 합의 진술에 따른 추가 성과금 계산이며 실재원·확정 영업이익·실제 입금 인증이 아닙니다.";
}
