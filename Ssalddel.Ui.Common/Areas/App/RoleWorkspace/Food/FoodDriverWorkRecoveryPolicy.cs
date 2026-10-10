using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Transport;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

public enum FoodDriverCanonicalWorkState { Unknown, Stopped, Driving }
public enum FoodDriverStartRecoveryDecision { None, AwaitCanonical, ClearStopped, EnableReception, Complete }

/// <summary>기기·저장소·HTTP와 무관한 운행 시작 결과의 정본 판정입니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.Domain,
    "운행 시작의 동일 요청·정본 상태와 응답 유실 복구를 순수 판정",
    StepKey = "ui.recovery-policy", FlowOrder = 200, ExecutionStage = SsalddelCodeExecutionStage.Definition,
    DependsOnStepKeys = new[] { "contract.work-start-response", "contract.work-status" },
    ReadsFrom = SsalddelCodeDataScope.ClientPresentation, Effects = SsalddelCodeEffect.None,
    Boundary = "전달받은 값만 판정하며 HTTP·GPS·암호화 저장·서버 상태 변경은 호출자가 소유한다.")]
public static class FoodDriverWorkRecoveryPolicy
{
    /// <summary>완료된 4xx 응답은 시작 거절이며, 응답 유실이나 5xx와 구분합니다.</summary>
    public static bool StartWasRejected(int? statusCode) => statusCode is >= 400 and < 500;

    public const string StartOwnershipNotice = "요청한 음식 배달 운행 시작을 확인하지 못했습니다. 기존 운행과 신규 배차 수신 설정은 유지합니다.";
    public static bool StartOwnershipMatches(Guid? requestId, Guid? canonicalRequestId, string? executionType)
        => requestId.HasValue && requestId != Guid.Empty && requestId == canonicalRequestId
            && executionType == 운송실행유형코드.음식배달;

    public static FoodDriverCanonicalWorkState WorkState(string? status)
        => string.Equals(status, "운행중", StringComparison.OrdinalIgnoreCase) ? FoodDriverCanonicalWorkState.Driving
            : string.Equals(status, "대기", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "운행종료", StringComparison.OrdinalIgnoreCase)
                ? FoodDriverCanonicalWorkState.Stopped : FoodDriverCanonicalWorkState.Unknown;

    public static FoodDriverStartRecoveryDecision ResolveStart(bool pending, string? status, string? intent)
        => !pending ? FoodDriverStartRecoveryDecision.None : WorkState(status) switch
        {
            FoodDriverCanonicalWorkState.Unknown => FoodDriverStartRecoveryDecision.AwaitCanonical,
            FoodDriverCanonicalWorkState.Stopped => FoodDriverStartRecoveryDecision.ClearStopped,
            _ => intent == 운영배차수신의사Code.On ? FoodDriverStartRecoveryDecision.Complete
                : intent == 운영배차수신의사Code.Off ? FoodDriverStartRecoveryDecision.EnableReception
                : FoodDriverStartRecoveryDecision.AwaitCanonical
        };

    public static bool OfferCurrent(DateTime? expiresAtUtc, DateTime nowUtc)
        => !expiresAtUtc.HasValue || expiresAtUtc.Value > nowUtc;
}
