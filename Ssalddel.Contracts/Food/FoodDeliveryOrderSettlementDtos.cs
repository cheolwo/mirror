namespace Ssalddel.Contracts.Food;

/// <summary>음식 주문별 기사 대금. 공제 미확정과 실송금 여부를 금액 0 또는 성공으로 대체하지 않습니다.</summary>
public sealed class FoodDeliveryOrderSettlementDto
{
    public string SettlementId { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public string DeliveryAttemptId { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    public decimal? GrossAmount { get; set; }
    public decimal? DeductionAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public string CurrencyCode { get; set; } = "KRW";
    public string PricingPolicyRevision { get; set; } = string.Empty;
    /// <summary>수락 때 저장된 요금 근거의 구성입니다. 현재 정책으로 다시 계산하지 않습니다.</summary>
    public FoodDeliverySettlementPricingBreakdownDto PricingBreakdown { get; set; } = new();
    public string SettlementStatusCode { get; set; } = string.Empty;
    public string PayoutStatusCode { get; set; } = "NotRequested";
    public string HoldReason { get; set; } = string.Empty;
    public string DeductionEvidenceReference { get; set; } = string.Empty;
    public string DeductionEvidenceScopeCode { get; set; } = "Unknown";
    /// <summary>저장된 지급 증빙의 실행 모드입니다. 현재 서버의 작업 허용 모드와 구별합니다.</summary>
    public string ExecutionModeCode { get; set; } = string.Empty;
    /// <summary>응답을 생성한 현재 서버의 실행 모드입니다. 제공되지 않으면 추론하지 않습니다.</summary>
    public string ServerExecutionModeCode { get; set; } = "Unknown";
    public DateTime CompletedAtUtc { get; set; }
    public DateTime? ReceiptConfirmedAtUtc { get; set; }
    public string SimulationPaymentId { get; set; } = string.Empty;
    public DateTime? SimulationVerifiedAtUtc { get; set; }
    public bool IsActualTransferCompleted { get; set; }
    public bool IsIdempotentReplay { get; set; }
    public long Revision { get; set; }
    public IReadOnlyList<FoodDeliverySimulatedPaymentDto> SimulationPayments { get; set; } = [];
}

/// <summary>누락 또는 미분류 구성은 null이며, LegacyUnsplit 기본액을 전달비로 해석하지 않습니다.</summary>
public sealed class FoodDeliverySettlementPricingBreakdownDto
{
    public string EvidenceStatusCode { get; set; } = "MissingEvidence";
    public string EvidenceScopeCode { get; set; } = "StoredAcceptedOffer";
    public string BaseSplitCode { get; set; } = "Unknown";
    public string PricingPolicyRevision { get; set; } = string.Empty;
    public DateTime? FrozenAtUtc { get; set; }
    public decimal? BaseAndDistanceAmount { get; set; }
    public decimal? BaseAmount { get; set; }
    public decimal? PickupAmount { get; set; }
    public decimal? DropoffAmount { get; set; }
    public decimal? DistanceAmount { get; set; }
    public decimal? MinimumAdjustmentAmount { get; set; }
    public decimal? TimeSurchargeAmount { get; set; }
    public decimal? WeatherSurchargeAmount { get; set; }
    public decimal? DemandSurchargeAmount { get; set; }
    public decimal? DistanceKm { get; set; }
    public string DistanceBasisCode { get; set; } = "Unknown";
    public string RouteVehicleCode { get; set; } = "Unknown";
    public string RouteOptionCode { get; set; } = string.Empty;
    public string TimeBandCode { get; set; } = string.Empty;
}

public sealed class FoodDeliverySimulatedPaymentDto
{
    public string PaymentId { get; set; } = string.Empty;
    public string OutcomeCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime VerifiedAtUtc { get; set; }
    public string ExecutionModeCode { get; set; } = "Simulation";
    public bool IsActualTransferCompleted { get; set; }
}
