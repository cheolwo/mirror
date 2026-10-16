using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Contracts.Common.Dispatch;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Contract,
    "인증된 기사 앱이 받은 현재 추천 차수를 확인한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 12,
    StepKey = "contract.operational-dispatch-offer-receipt",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Boundary = "기사 ID와 시각은 서버가 확인한다. 앱 수신 확인은 사람의 열람이나 알림음 청취 증거가 아니다.")]
public sealed class 운영배차제안표시확인요청
{
    public int 추천라운드 { get; set; }
}
