namespace 살뜰.도메인.음식;

/// <summary>완료된 최종 배달 시도의 동결 대금과 별도 공제·모의 지급 근거를 보존합니다.</summary>
public sealed class 음식주문기사정산
{
    public long Id { get; set; }
    public string 정산StableId { get; set; } = string.Empty;
    public long 음식주문Id { get; set; }
    public string 주문번호 { get; set; } = string.Empty;
    public string 음식점명 { get; set; } = string.Empty;
    public long 배달시도Id { get; set; }
    public 음식배달시도 배달시도 { get; set; } = null!;
    public string 배달시도StableId { get; set; } = string.Empty;
    public long 운송Id { get; set; }
    public string 기사Id { get; set; } = string.Empty;
    public decimal? 세전대금 { get; set; }
    public string 요금정책판본 { get; set; } = string.Empty;
    public string 요금계산근거Json { get; set; } = string.Empty;
    public decimal? 공제액 { get; set; }
    public decimal? 수령액 { get; set; }
    public string 공제근거참조 { get; set; } = string.Empty;
    public string 공제근거범위Code { get; set; } = "Unknown";
    public string 정산상태Code { get; set; } = 음식주문기사정산상태Code.수령확인대기;
    public string 지급상태Code { get; set; } = 음식주문기사지급상태Code.미요청;
    public string 보류사유 { get; set; } = string.Empty;
    public string 실행모드Code { get; set; } = string.Empty;
    public DateTime 전달완료시각Utc { get; set; }
    public DateTime? 수령확인시각Utc { get; set; }
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<음식주문기사지급검증> 지급검증목록 { get; set; } = [];
}

/// <summary>은행/PG를 호출하지 않은 Simulation 시험 증빙. 실패 이력도 보존합니다.</summary>
public sealed class 음식주문기사지급검증
{
    public long Id { get; set; }
    public long 정산Id { get; set; }
    public 음식주문기사정산 정산 { get; set; } = null!;
    public string 지급StableId { get; set; } = string.Empty;
    public string 멱등키 { get; set; } = string.Empty;
    public decimal 확인세전대금 { get; set; }
    public decimal 확인공제액 { get; set; }
    public decimal 모의수령액 { get; set; }
    public string 공제근거참조 { get; set; } = string.Empty;
    public string 결과Code { get; set; } = string.Empty;
    public string 검증관리자Id { get; set; } = string.Empty;
    public DateTime 검증시각Utc { get; set; }
}

public static class 음식주문기사정산상태Code
{
    public const string 수령확인대기 = "AwaitingReceipt";
    public const string 공제확인대기 = "AwaitingDeductions";
    public const string 모의검증준비 = "ReadyForSimulation";
    public const string 요금근거없음 = "BlockedMissingQuote";
}

public static class 음식주문기사지급상태Code
{
    public const string 미요청 = "NotRequested";
    public const string 모의성공 = "SimulationSucceeded";
    public const string 모의실패 = "SimulationFailed";
}
