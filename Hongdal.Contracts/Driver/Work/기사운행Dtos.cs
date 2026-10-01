namespace Hongdal.Contracts.Driver.Work;

public sealed class 기사운행시작요청
{
    public string 시작모드 { get; set; } = "immediate";
    public DateTime? 시작시각 { get; set; }
    public string 시작위치 { get; set; } = string.Empty;
    public string? 복귀지 { get; set; }
    public string? 오늘의복귀지주소 { get; set; }
    public decimal? 오늘의복귀지위도 { get; set; }
    public decimal? 오늘의복귀지경도 { get; set; }
    public bool 기본복귀지사용 { get; set; }
    public string? 복귀지출처 { get; set; }
    public string? 복귀콜선호 { get; set; }
    public bool 커뮤니티운행공개 { get; set; } = true;
    public bool 커뮤니티구단위위치공개동의 { get; set; }
}

public sealed class 기사운행시작응답
{
    public string DriverId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long ShiftId { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? 적용복귀지 { get; set; }
    public string? 복귀지출처 { get; set; }
    public string? 복귀콜선호 { get; set; }
    public bool 커뮤니티운행공개됨 { get; set; }
    public Guid? 커뮤니티운행공개글Id { get; set; }
    public string 커뮤니티공개안내 { get; set; } = string.Empty;
    public bool 커뮤니티구단위위치공개동의됨 { get; set; }
}

public sealed class 기사운행상태응답
{
    public string DriverId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public decimal? 현재위도 { get; set; }
    public decimal? 현재경도 { get; set; }
    public DateTime? 최근위치수신시각 { get; set; }
    public decimal? Aging점수 { get; set; }
    public DateTime? Aging기준시각 { get; set; }
    public string? 복귀콜선호 { get; set; }
}

public sealed class 기사위치갱신요청
{
    public string? AppKey { get; set; }
    public decimal? 위도 { get; set; }
    public decimal? 경도 { get; set; }
    public decimal? 정확도_m { get; set; }
    public decimal? 상차접근허용반경Km { get; set; }
    public string? 운행상태 { get; set; }
    public DateTime? 기록시각 { get; set; }
}

public sealed class 기사위치갱신응답
{
    public string DriverId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? 현재위도 { get; set; }
    public decimal? 현재경도 { get; set; }
    public DateTime? 최근위치수신시각 { get; set; }
    public decimal Aging점수 { get; set; }
    public DateTime Aging기준시각 { get; set; }
    public decimal? 상차접근허용반경Km { get; set; }
    public int 권장위치전송간격초 { get; set; } = 300;
    public string? 커뮤니티현재공개지역 { get; set; }
}

public sealed class 기사현재근무응답
{
    public long? ShiftId { get; set; }
    public string DriverId { get; set; } = string.Empty;
    public string 운행상태 { get; set; } = string.Empty;
    public string 시작모드 { get; set; } = string.Empty;
    public DateTime? 시작시각 { get; set; }
    public string 시작위치 { get; set; } = string.Empty;
    public string? 복귀지 { get; set; }
    public string? 오늘의복귀지주소 { get; set; }
    public decimal? 오늘의복귀지위도 { get; set; }
    public decimal? 오늘의복귀지경도 { get; set; }
    public string? 복귀지출처 { get; set; }
}

public sealed class 기사근무요약응답
{
    public long Id { get; set; }
    public string DriverId { get; set; } = string.Empty;
    public string StartMode { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string? ReturnDestination { get; set; }
    public string? TodayReturnDestination { get; set; }
    public decimal? TodayReturnLatitude { get; set; }
    public decimal? TodayReturnLongitude { get; set; }
    public string? ReturnDestinationSource { get; set; }
    public bool IsReserved { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime UpdatedAt { get; set; }
}
