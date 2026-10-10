using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Contracts.Driver.Work;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "기사의 운행 시작 입력과 선택적 클라이언트 요청 식별자를 정의한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 10,
    StepKey = "contract.work-start-request",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "시작 요청 ID와 배차 수신 변경 ID는 별개다. 계약 자체는 시작 POST 재전송이나 상태 변경을 실행하지 않는다.")]
public sealed class 기사운행시작요청
{
    /// <summary>동일 시작 요청의 재조회·재전송을 구분하는 선택적 식별자입니다.</summary>
    public Guid? ClientRequestId { get; set; }
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

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "SQL 근무 확정 결과와 인덱스 후속 대기 여부를 전달한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 11,
    StepKey = "contract.work-start-response",
    DependsOnStepKeys = ["contract.work-start-request"],
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "FollowupPending은 SQL 저장 실패가 아니며 응답만으로 배차 수신 ON이나 외부 후속 완료를 확정하지 않는다.")]
public sealed class 기사운행시작응답
{
    /// <summary>근무 저장은 완료됐으며 조회용 인덱스 확인이 아직 끝나지 않았는지 나타냅니다.</summary>
    public bool FollowupPending { get; set; }
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

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "운행 정본의 근무와 시작 요청 소유권 및 위치 투영을 전달한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 12,
    StepKey = "contract.work-status",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "운행 상태와 시작 요청 ID는 SQL 기준이고 위치·Aging은 인덱스 투영이다. 구형 근무의 null 요청 ID를 현재 요청으로 추정하지 않는다.")]
public sealed class 기사운행상태응답
{
    public string DriverId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    /// <summary>응답 유실 후 현재 근무가 본인의 시작 요청인지 대조합니다. 구형 근무에는 요청 ID가 없습니다.</summary>
    public long? ShiftId { get; set; }
    public Guid? WorkStartClientRequestId { get; set; }
    public string? 운송실행유형 { get; set; }
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
    public string 운송실행유형 { get; set; } = string.Empty;
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
    public string TransportExecutionType { get; set; } = string.Empty;
    public string? ReturnDestination { get; set; }
    public string? TodayReturnDestination { get; set; }
    public decimal? TodayReturnLatitude { get; set; }
    public decimal? TodayReturnLongitude { get; set; }
    public string? ReturnDestinationSource { get; set; }
    public bool IsReserved { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime UpdatedAt { get; set; }
}
