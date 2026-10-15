namespace 살뜰.도메인.기사;

/// <summary>본인 희망 조건과 공통 수락용 SQL 잠금 행. 원 업무·정산 상태는 보유하지 않습니다.</summary>
public sealed class DriverWorkParticipationRecord
{
    public string UserId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public long WorkLockRevision { get; set; }
    public bool FoodDelivery { get; set; }
    public bool QuickDelivery { get; set; }
    public bool CargoTransport { get; set; }
    public bool DirectPicking { get; set; }
    public string VehicleKind { get; set; } = string.Empty;
    public int MaxConcurrentJobs { get; set; } = 1;
    public decimal? CapacityKg { get; set; }
    public decimal? CapacityLitres { get; set; }
    public bool FoodCargoSeparationConfirmed { get; set; }
    public bool ReceiveAdditionalOffers { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; }
}
