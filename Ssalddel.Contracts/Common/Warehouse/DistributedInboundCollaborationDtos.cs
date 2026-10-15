using System.Text.Json.Serialization;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Contracts.Common.Warehouse;

/// <summary>공급자 소개는 분류나 소유 권한의 근거가 아닙니다. 물품 특성은 별도로 확인합니다.</summary>
public static class DistributedInboundCodes
{
    public const string Agricultural = "agricultural", Industrial = "industrial", PackagedFood = "packaged-food";
    public const string Sealed = "sealed", Boxed = "boxed", Unpacked = "unpacked";
    public const string Ambient = "ambient", Chilled = "chilled", Frozen = "frozen";
    public static IReadOnlyList<string> CategoryCodes { get; } = [Agricultural, Industrial, PackagedFood];
    public static IReadOnlyList<string> PackagingCodes { get; } = [Sealed, Boxed, Unpacked];
    public static IReadOnlyList<string> TemperatureCodes { get; } = [Ambient, Chilled, Frozen];
}

/// <summary>거점 전체에 적용할 수용 조건입니다. 단위 환산이나 상품명에서의 분류 추정을 하지 않습니다.</summary>
public sealed class DistributedSiteAcceptancePolicy
{
    public IReadOnlyList<string> AllowedCategoryCodes { get; set; } = [];
    public IReadOnlyList<string> DeniedCategoryCodes { get; set; } = [];
    public bool AllowFood { get; set; }
    public bool RequireProcessedFood { get; set; }
    public bool RequireSealedPackaging { get; set; }
    public IReadOnlyList<string> AllowedPackagingCodes { get; set; } = [];
    public IReadOnlyList<string> AllowedTemperatureCodes { get; set; } = [];
    public string CapacityUnit { get; set; } = "";
    public decimal MaximumCapacity { get; set; }
    public decimal? MaximumWeightKg { get; set; }
    public decimal? MaximumVolumeM3 { get; set; }
    public int? MaximumStorageDays { get; set; }
    // 자유문자 출입 조건을 자동 시간표로 해석하지 않습니다. 이번 입고 조건의 양측 확인이 필요합니다.
    public string AccessHours { get; set; } = "";
}

public sealed class DistributedSiteAcceptancePolicyRequest : DistributedCommandRequest
{
    public DistributedSiteAcceptancePolicy Policy { get; set; } = new();
}

public sealed class DistributedGoodsFacts
{
    public IReadOnlyList<string> CategoryCodes { get; set; } = [];
    public bool? IsFood { get; set; }
    public bool? IsProcessed { get; set; }
    public string PackagingCode { get; set; } = "";
    public string TemperatureCode { get; set; } = "";
    public string CapacityUnit { get; set; } = "";
    // 입고 수량 한 개가 공간 용량을 얼마나 쓰는지 확인한 값입니다. 재고 수량과 서로 다른 단위일 수 있습니다.
    public decimal CapacityPerItem { get; set; }
    public decimal? WeightKgPerItem { get; set; }
    public decimal? VolumeM3PerItem { get; set; }
}

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.WarehouseInboundVertical, SsalddelCodeLayer.Contract,
    "공급자와 공간 제공자가 같은 판본으로 확인할 물품 특성·수량·일정·보관 합의를 연결한다.",
    Effects = SsalddelCodeEffect.None, StepKey = "contract.distributed-inbound-collaboration", FlowOrder = 20,
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Boundary = "공급자 소개·조건 작성·메시지는 소유 권한, 계약 동의, 입고 승인, 실재고 또는 실제 지급을 확정하지 않는다.")]
public sealed class DistributedInboundCollaborationTerms
{
    public string AgreementId { get; set; } = "";
    public int AgreementTermsVersion { get; set; }
    public string SupplierUserId { get; set; } = "";
    public string SupplierTypeCode { get; set; } = "";
    public string GoodsOwnerUserId { get; set; } = "";
    public string SellerUserId { get; set; } = "";
    public DistributedGoodsFacts Goods { get; set; } = new();
    public int Quantity { get; set; }
    public DateTime ArrivalStartUtc { get; set; }
    public DateTime ArrivalEndUtc { get; set; }
    public DateTime StorageEndsAtUtc { get; set; }
    public bool AccessHoursConfirmed { get; set; }
    public string SupplyPriceOrFreeTerms { get; set; } = "";
    public string SupplierResponsibilityTerms { get; set; } = "";
    public string ReturnTerms { get; set; } = "";
}

public sealed class DistributedInboundProposalRequest : DistributedCommandRequest
{
    public DistributedInboundCollaborationTerms Terms { get; set; } = new();
}
public sealed class DistributedInboundConsentRequest : DistributedCommandRequest
{
    public int TermsVersion { get; set; }
    public bool Granted { get; set; }
}
public sealed class DistributedInboundDecisionRequest : DistributedCommandRequest
{
    public int TermsVersion { get; set; }
    public string Action { get; set; } = ""; // Reject or Cancel; 현장 수령/공급자 반송 완료가 아닙니다.
    public string Reason { get; set; } = "";
}
public sealed class DistributedInboundNoteRequest : DistributedCommandRequest
{
    public int TermsVersion { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>기존 입고 계획의 행에 결속된 협의 사본입니다. 새로운 재고 원장을 만들지 않습니다.</summary>
public sealed class DistributedInboundCollaborationDto
{
    public int TermsVersion { get; set; }
    public string StatusCode { get; set; } = "Draft";
    public DistributedInboundCollaborationTerms Terms { get; set; } = new();
    public string ProposedByUserId { get; set; } = "";
    public bool GoodsOwnerConsented { get; set; }
    public bool SpaceOwnerConsented { get; set; }
    // 한쪽만 동의한 사이 공간 조건이 바뀌면 이전 동의를 재사용하지 않고 새 제안을 받습니다.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ProposedPolicyRevision { get; set; }
    public long? ApprovedPolicyRevision { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    // 변경 협의 중에도 앞서 승인한 물량의 공간을 중복 예약하지 않습니다. 취소/새 확정 때만 교체합니다.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DistributedInboundCollaborationTerms? ReservedTerms { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ReservedTermsVersion { get; set; }
    public IReadOnlyList<DistributedInboundCollaborationHistoryDto> History { get; set; } = [];
}
public sealed record DistributedInboundCollaborationHistoryDto(string Kind, string ActorUserId, int TermsVersion,
    DateTime RecordedAtUtc, string Note)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DistributedInboundCollaborationTerms? Terms { get; init; }
}

/// <summary>호출자가 현재 실재고와 승인 예약을 합산한 용량입니다. 근거가 없으면 IsKnown=false로 전달합니다.</summary>
public sealed class DistributedCapacityUsage
{
    public bool IsKnown { get; set; }
    public string CapacityUnit { get; set; } = "";
    public decimal Capacity { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? VolumeM3 { get; set; }
}
public sealed record DistributedAcceptanceDecision(bool IsAllowed, bool NeedsReview,
    IReadOnlyList<string> ReasonCodes, decimal RequiredCapacity, decimal? RequiredWeightKg, decimal? RequiredVolumeM3);
