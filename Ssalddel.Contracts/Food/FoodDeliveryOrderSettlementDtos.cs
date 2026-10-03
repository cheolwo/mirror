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

public sealed class FoodDeliverySimulatedPaymentDto
{
    public string PaymentId { get; set; } = string.Empty;
    public string OutcomeCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime VerifiedAtUtc { get; set; }
    public string ExecutionModeCode { get; set; } = "Simulation";
    public bool IsActualTransferCompleted { get; set; }
}
