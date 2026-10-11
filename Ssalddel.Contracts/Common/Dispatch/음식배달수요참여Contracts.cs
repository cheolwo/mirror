using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Contracts.Common.Dispatch;

public static class 음식배달수요자료상태Code
{
    public const string 최신 = "Current";
    public const string 미확인 = "Unknown";
    public const string 만료 = "Stale";
}

public static class 음식배달수요참여상태Code
{
    public const string 기존활동보호 = "ActiveProtected";
    public const string 추가참여권고 = "AdmissionRecommended";
    public const string 수요대기 = "WaitingForDemand";
    public const string 기존관문재검사 = "ExistingGateRequired";
    public const string 적격조건미충족 = "Ineligible";
}

/// <summary>시간당 처리 목표는 수요 계획용이며 동시 적재·수락 상한과 별개입니다.</summary>
public sealed class 음식배달수요참여설정
{
    public decimal 시간당목표처리건수 { get; set; } = 3.5m;
    public decimal 계획시간창분 { get; set; } = 60m;
    public decimal 최대수요관측경과분 { get; set; } = 5m;
    public decimal 최대기사관측경과분 { get; set; } = 5m;
    public decimal 장기대기기회분 { get; set; } = 30m;
    public int 장기대기추가인원 { get; set; } = 1;
    public int 주문지연추가인원 { get; set; } = 1;
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Contract,
    "권역의 확인된 주문량과 기사 수행 여력을 수요 참여 권고의 입력으로 전달한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 35,
    StepKey = "contract.food-demand-participation",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Boundary = "AI 예측과 실제 주문 관측을 구분한다. 권고는 운행·수신 의사·수락 권한·운임·지급을 변경하지 않는다.")]
public sealed class 음식배달수요참여입력
{
    public string 권역Key { get; set; } = string.Empty;
    public DateTimeOffset 판정시각Utc { get; set; }
    public DateTimeOffset? 수요관측시각Utc { get; set; }
    public int? 대기주문수 { get; set; }
    public int? 최근시간창신규주문수 { get; set; }
    public decimal 관측시간창분 { get; set; } = 15m;
    /// <summary>계획시간창 동안의 향후 주문 예상입니다. 관측률 예측과 더하지 않고 큰 값을 사용합니다.</summary>
    public decimal? 향후수요예측건수 { get; set; }
    public bool 지연위험주문존재 { get; set; }
    public bool 적격활동기사부재 { get; set; }
    public IReadOnlyList<음식배달수요기사입력> 기사목록 { get; set; } = [];
}

public sealed class 음식배달수요기사입력
{
    public string 기사Id { get; set; } = string.Empty;
    /// <summary>현재 미완료 업무 또는 최근 실제 수락 기록에서 판독합니다. 토글 값이 아닙니다.</summary>
    public bool 기존활동여부 { get; set; }
    public DateTimeOffset? 운행시작시각Utc { get; set; }
    /// <summary>운행의 안정된 대기 기준입니다. 새로고침·재연결·수신 토글로 갱신하지 않습니다.</summary>
    public DateTimeOffset? 대기시작시각Utc { get; set; }
    public DateTimeOffset? 마지막관측시각Utc { get; set; }
    /// <summary>기존 약속의 잔여 수행 시간입니다. 미확인은 null이며 0으로 치환하지 않습니다.</summary>
    public decimal? 남은수행시간분 { get; set; }
    public int 최근시간창배차수 { get; set; }
    public bool 적격여부 { get; set; } = true;
}

public sealed class 음식배달수요참여결과
{
    public string 정책판본 { get; set; } = string.Empty;
    public string 권역Key { get; set; } = string.Empty;
    public DateTimeOffset 판정시각Utc { get; set; }
    public string 데이터상태Code { get; set; } = 음식배달수요자료상태Code.미확인;
    public string 사유Code { get; set; } = string.Empty;
    public int? 목표활동인원 { get; set; }
    public decimal? 예상업무량건수 { get; set; }
    public decimal? 기존기사가용처리건수 { get; set; }
    public int 보호활동기사수 { get; set; }
    public int 추가참여인원 { get; set; }
    public IReadOnlyList<음식배달수요기사판정> 기사판정목록 { get; set; } = [];
}

/// <summary>이 평가의 순서·추가 제안 권고입니다. 영속 참여 슬롯이나 배차 수락 결과가 아닙니다.</summary>
public sealed class 음식배달수요기사판정
{
    public string 기사Id { get; set; } = string.Empty;
    public string 상태Code { get; set; } = 음식배달수요참여상태Code.기존관문재검사;
    public string 사유Code { get; set; } = string.Empty;
    public bool 신규제안허용 { get; set; }
    public bool 기존활동여부 { get; set; }
    public bool 승격여부 { get; set; }
    public int 우선순위 { get; set; }
}
