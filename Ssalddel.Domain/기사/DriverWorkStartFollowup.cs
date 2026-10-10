using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.기사;

/// <summary>SQL 근무와 함께 확정하는 인덱스 재구성 예약. 주소나 고객 정보를 복제하지 않습니다.</summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Domain,
    "SQL 근무와 함께 확정할 인덱스 후속의 재시도 상태를 정의한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 31,
    StepKey = "domain.work-start-followup",
    DependsOnStepKeys = ["domain.driver-shift"],
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "ShiftId에 결속된 작업 상태이며 고객 정보나 시작 주소를 복제하지 않는다. SQL 근무 성공과 인덱스 처리 성공은 별도다.")]
public sealed class 기사운행시작후속
{
    public long ShiftId { get; set; }
    public string DriverId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? LastFailureCode { get; set; }
}
