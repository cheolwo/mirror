using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Queue;

public sealed partial class 배차대기원장전환Service
{
    // 수집은 새 추천 전의 짧은 보류다. CreatedAt/UpdatedAt을 바꾸거나 제안·요금·알림을 만들지 않는다.
    private async Task<배차대기원장전환결과?> 음식배달수집판정Async(
        운송원장 queue, CancellationToken cancellationToken)
    {
        if (_dispatchTiming?.Enabled != true
            || queue.배차업무유형 != 상태값.배차업무유형.음식배달
            || queue.원본의뢰유형 is 운송의뢰배차원천유형.살뜰마트주문 or 운송의뢰배차원천유형.살뜰마트음식주문
            || queue.추천라운드 > 0 || !string.IsNullOrWhiteSpace(queue.현재추천대상기사Id)
            || !string.IsNullOrWhiteSpace(queue.확정기사Id)
            || queue.상태 != 상태값.배차대기상태.대기
            || queue.배차큐단계 != 상태값.배차큐단계.배차추천
            || queue.배차노출상태 is not (상태값.배차노출상태.추천대기 or 상태값.배차노출상태.추천후보없음))
            return null;

        var before = 음식배달선정원장판본.생성(queue);
        var createdAt = queue.CreatedAt;
        var evaluation = await _dispatchTiming.EvaluateAsync(queue, cancellationToken);
        var entry = _db.Entry(queue);
        await entry.ReloadAsync(cancellationToken);
        if (entry.State == EntityState.Detached || before != 음식배달선정원장판본.생성(queue)
            || createdAt != queue.CreatedAt)
            return 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨, "FoodDispatchTiming.QueueChanged");

        var decision = evaluation.Decision;
        // 원장 재조회가 늦어져도 응답 당시의 Current를 그대로 적용하지 않습니다.
        var now = _timingClock.GetUtcNow();
        var information = evaluation.Input.InformationReadiness;
        var currentInformation = information is { MaxAgeSeconds: >= 1 and <= 120 }
            ? new FoodDispatchInformationReadinessPolicy().Evaluate(
                evaluation.Input.Applicable && !evaluation.Input.HasActiveOffer && !evaluation.Input.IsRetry,
                now, information.MaxAgeSeconds, information.Evidence)
            : null;
        if (decision.ActionCode == FoodDispatchTimingActionCodes.Collect
            && currentInformation?.SchedulingStateCode != FoodDispatchInformationStateCodes.Current)
            decision = decision with
            {
                ActionCode = FoodDispatchTimingActionCodes.DispatchNow,
                ReasonCode = currentInformation?.SchedulingStateCode == FoodDispatchInformationStateCodes.Stale
                    ? "InformationStaleAfterReload" : "InformationMissingAfterReload",
                RecheckAtUtc = null,
                AllowedActions = [FoodDispatchTimingActionCodes.DispatchNow]
            };
        _timingLogger?.LogInformation(
            "FoodDispatchTiming ActionCode={ActionCode} ReasonCode={ReasonCode} PolicyVersion={PolicyVersion} RecheckAtUtc={RecheckAtUtc} HardDeadlineUtc={HardDeadlineUtc} AIStatusCode={AIStatusCode} SchedulingInformation={SchedulingInformation} CoordinationInformation={CoordinationInformation}",
            decision.ActionCode, decision.ReasonCode, decision.PolicyVersion,
            decision.RecheckAtUtc, decision.HardDeadlineUtc, evaluation.AIStatusCode,
            currentInformation?.SchedulingStateCode ?? FoodDispatchInformationStateCodes.Missing,
            currentInformation?.CoordinationStateCode ?? FoodDispatchInformationStateCodes.Missing);
        // 모델이나 자료 조회가 늦었으면 보류를 새로 시작하지 않는다. 기존 최종 후보 관문은 그대로 적용한다.
        if (currentInformation?.SchedulingStateCode != FoodDispatchInformationStateCodes.Current
            || decision.ActionCode != "Collect" || !decision.RecheckAtUtc.HasValue
            || now >= decision.RecheckAtUtc.Value || now >= decision.HardDeadlineUtc)
            return null;
        return 전환안됨(queue, "FoodDispatchTiming.Collect",
            $"주문 조합을 잠시 검토합니다. 사유={decision.ReasonCode}; 다시 판단={decision.RecheckAtUtc.Value:O}");
    }
}
