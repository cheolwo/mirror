using 살뜰.도메인.배차;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.Services.Dispatch.Common;

/// <summary>새 제안·수락에만 적용한다. 이미 확정한 업무의 조회·복구·완료에는 적용하지 않는다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Application,
    "음식·화물의 신규 배차 수신 의사와 서버 제한·미응답 자동중지를 함께 확인한다.",
    StepKey = "application.operational-dispatch-new-offer-guard", FlowOrder = 48,
    ExecutionStage = SsalddelCodeExecutionStage.Preview, Effects = SsalddelCodeEffect.PersistentRead,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.None,
    Boundary = "조회 실패는 신규 배차 허용으로 바꾸지 않는다. 기존 업무의 복구·수행·완료에는 적용하지 않는다.")]
public static class 운영배차신규수신Guard
{
    public static async Task<bool> 허용Async(I운영배차공통UseCase? availability, string driverId, CancellationToken ct)
    {
        if (availability is null) return false;
        try { return 운영배차수신상태Policy.신규배차수신허용(await availability.수신상태조회Async(driverId, ct)); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return false; }
    }
}
