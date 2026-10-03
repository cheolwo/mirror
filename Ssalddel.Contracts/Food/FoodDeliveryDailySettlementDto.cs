namespace Ssalddel.Contracts.Food;

/// <summary>기사 본인의 한국 전달 완료일 전체 정산 원장입니다. 최근 40건 조회와 별도이며 실입금 집계가 아닙니다.</summary>
public sealed class FoodDeliveryDailySettlementDto
{
    public string DriverId { get; set; } = string.Empty;
    public DateOnly CompletionDateKst { get; set; }
    public string TimeZoneCode { get; set; } = "Asia/Seoul";
    public string DateBasisCode { get; set; } = "DeliveryCompletedAt";
    public string CurrencyCode { get; set; } = "KRW";
    public DateTime PeriodStartAtUtc { get; set; }
    public DateTime PeriodEndAtUtc { get; set; }
    public bool IsFullDayQuery { get; set; } = true;
    public int CompletedOrderCount { get; set; }
    public int ReceiptConfirmedOrderCount { get; set; }
    public int AwaitingReceiptOrderCount { get; set; }
    public int MissingGrossAmountCount { get; set; }
    public int UnconfirmedDeductionCount { get; set; }
    public int UnconfirmedNetAmountCount { get; set; }
    /// <summary>금액이 확인된 행만 더한 부분 합계입니다. 모든 건의 금액으로 표시하지 않습니다.</summary>
    public decimal KnownGrossAmountTotal { get; set; }
    /// <summary>한 건이라도 금액이 미확정이면 전체 합계도 null입니다.</summary>
    public decimal? GrossAmountTotal { get; set; }
    public decimal? DeductionAmountTotal { get; set; }
    public decimal? NetAmountTotal { get; set; }
    public int SimulationSucceededOrderCount { get; set; }
    public int SimulationFailedOrderCount { get; set; }
    public decimal? SimulationSucceededNetAmountTotal { get; set; }
    public int ActualTransferCompletedOrderCount { get; set; }
    /// <summary>은행/PG 입금 근거가 연결되지 않았으므로 실입금액은 미확인으로 유지합니다.</summary>
    public decimal? ActualTransferAmountTotal { get; set; }
    public string ServerExecutionModeCode { get; set; } = "Unknown";
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<FoodDeliveryOrderSettlementDto> OrderSettlements { get; set; } = [];
}
