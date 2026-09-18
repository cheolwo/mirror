using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Domain.운영;

namespace Ssalddel.Application.Admin.Operations;

public interface I플랫폼운영경제성UseCase
{
    플랫폼운영경제성평가응답Dto 평가(플랫폼운영경제성평가요청Dto 요청);
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.PlatformOperatingEconomics,
    SsalddelCodeLayer.Application,
    "관리자의 국가·관할·통화별 운영경제성 Preview를 순수 계산기에 위임합니다.",
    StepKey = "application.platform-operating-economics-preview",
    DependsOnStepKeys = ["domain.platform-operating-economics-calculate"],
    FlowOrder = 30,
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    Effects = SsalddelCodeEffect.None,
    ContractType = typeof(플랫폼운영경제성평가요청Dto),
    Boundary = "결과는 경영 시뮬레이션 전용이며 정산, 지급, 세금 신고나 운영 회계 전표를 생성하지 않습니다.")]
public sealed class 플랫폼운영경제성UseCase(I플랫폼운영경제성Calculator calculator)
    : I플랫폼운영경제성UseCase
{
    public 플랫폼운영경제성평가응답Dto 평가(플랫폼운영경제성평가요청Dto 요청)
        => calculator.평가(요청);
}
