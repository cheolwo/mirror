namespace Ssalddel.Contracts.Admin.Food;

/// <summary>Simulation 전용 공제 시험 입력. 법정 공제 확정 또는 실송금 요청이 아닙니다.</summary>
public sealed class FoodDeliverySimulatedPayoutRequest
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public long ExpectedSettlementRevision { get; set; }
    public decimal ConfirmedGrossAmount { get; set; }
    public decimal? ConfirmedDeductionAmount { get; set; }
    public string DeductionEvidenceReference { get; set; } = string.Empty;
    public string OutcomeCode { get; set; } = "Succeeded";
}
