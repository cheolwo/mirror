using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.배차;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Domain,
    "기사의 명시적 수신 의사와 서버의 실효 배차 상태를 서로 덮어쓰지 않게 변경한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 25,
    StepKey = "domain.operational-dispatch-availability-policy",
    DependsOnStepKeys = ["contract.operational-dispatch-availability"],
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "연결 오류나 서버 일시정지는 기사 의사를 OFF로 바꾸지 않는다. OFF는 기사의 명시적 의사 변경으로만 만든다.")]
public sealed class 운영배차수신상태Policy
{
    /// <summary>명시적 수신 의사와 기록된 서버 제한을 판정합니다. 운행·위치·업무별 적합성은 소비자가 추가 확인합니다.</summary>
    public static bool 신규배차수신허용(운영배차수신상태Dto 현재)
    {
        ArgumentNullException.ThrowIfNull(현재);
        if (현재.수신의사Code != 운영배차수신의사Code.On
            || !현재.수신의사변경시각Utc.HasValue
            || 현재.실효상태Code == 운영배차실효상태Code.서버일시정지)
        {
            return false;
        }

        // 초기 Ineligible은 실효 판정 부재다. 명시 ON 이후 기존 운행·위치 검사를 생략하는 근거가 아니다.
        return !현재.실효상태변경시각Utc.HasValue
            || 현재.실효상태Code == 운영배차실효상태Code.배차가능;
    }

    public 운영배차수신상태Dto 기사의사변경(
        운영배차수신상태Dto? 현재,
        string 주체Id,
        string 수신의사Code,
        DateTimeOffset 발생시각Utc)
    {
        if (string.IsNullOrWhiteSpace(주체Id))
        {
            throw new ArgumentException("배차 수신 주체 ID가 필요합니다.", nameof(주체Id));
        }

        if (수신의사Code is not (운영배차수신의사Code.On or 운영배차수신의사Code.Off))
        {
            throw new ArgumentException("지원하지 않는 배차 수신 의사 코드입니다.", nameof(수신의사Code));
        }

        현재주체확인(현재, 주체Id);
        return new 운영배차수신상태Dto
        {
            주체Id = 주체Id,
            수신의사Code = 수신의사Code,
            실효상태Code = 현재?.실효상태Code ?? 운영배차실효상태Code.조건부적합,
            실효사유Code = 현재?.실효사유Code ?? string.Empty,
            수신의사변경시각Utc = 발생시각Utc.ToUniversalTime(),
            실효상태변경시각Utc = 현재?.실효상태변경시각Utc,
            서버관측시각Utc = 발생시각Utc.ToUniversalTime()
        };
    }

    public 운영배차수신상태Dto 서버실효상태변경(
        운영배차수신상태Dto 현재,
        string 실효상태Code,
        string 실효사유Code,
        DateTimeOffset 관측시각Utc)
    {
        ArgumentNullException.ThrowIfNull(현재);
        if (실효상태Code is not (운영배차실효상태Code.배차가능
            or 운영배차실효상태Code.서버일시정지
            or 운영배차실효상태Code.조건부적합
            or 운영배차실효상태Code.연결확인불가))
        {
            throw new ArgumentException("지원하지 않는 실효 배차 상태 코드입니다.", nameof(실효상태Code));
        }

        return new 운영배차수신상태Dto
        {
            주체Id = 현재.주체Id,
            수신의사Code = 현재.수신의사Code,
            실효상태Code = 실효상태Code,
            실효사유Code = 실효사유Code ?? string.Empty,
            수신의사변경시각Utc = 현재.수신의사변경시각Utc,
            실효상태변경시각Utc = 관측시각Utc.ToUniversalTime(),
            서버관측시각Utc = 관측시각Utc.ToUniversalTime()
        };
    }

    private static void 현재주체확인(운영배차수신상태Dto? 현재, string 주체Id)
    {
        if (현재 is not null
            && !string.Equals(현재.주체Id, 주체Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("다른 주체의 배차 수신 상태를 변경할 수 없습니다.");
        }
    }
}
