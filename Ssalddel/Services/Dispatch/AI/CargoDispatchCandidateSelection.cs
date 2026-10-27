using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Services.Dispatch.AI;

/// <summary>모델은 검증된 후보 사이의 제안만 한다. 응답 뒤 현재 후보를 다시 읽어 최종 반환한다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Application,
    "화물 모델의 제안을 초기·현재 허용 후보와 대조하고 현재 서버 후보 또는 현재 규칙 후보를 반환한다.",
    StepKey = "application.cargo-dispatch-candidate-selection", FlowOrder = 48,
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    Boundary = "모델은 탈락 후보를 복원하지 않는다. 재조회 실패는 전파하며 원장·수락·알림·지급을 변경하지 않는다.")]
public static class CargoDispatchCandidateSelection
{
    public static async Task<배차추천후보?> SelectAsync(
        용달운송후보검토결과 initial,
        ICargoDispatchCandidateAI? ai,
        Func<CancellationToken, Task<용달운송후보검토결과?>> refresh,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (ai?.Enabled != true || initial.선정후보 is null) return initial.선정후보;

        // 초기 허용 집합은 응답을 기다리기 전에 고정한다. 대기 중 새 후보를 모델이 보았다고 간주하지 않는다.
        var initiallyAllowed = CargoDispatchCandidateConstraints.Eligible(initial)
            .Select(x => x.기사Id).ToHashSet(StringComparer.Ordinal);
        CargoDispatchAISelection decision;
        try
        {
            decision = await ai.SelectAsync(initial, ct) ?? new(null, "Unavailable");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { decision = new(null, "Unavailable"); }
        ct.ThrowIfCancellationRequested();

        // 위치·수신 의사·원장·약속이 달라질 수 있다. 모델을 재호출하지 않고 동일 서버 관문만 다시 읽는다.
        // DB/원장 조회 오류를 오래된 후보로 숨기지 않도록 이 호출은 모델 오류 처리 바깥에 둔다.
        var current = await refresh(ct);
        ct.ThrowIfCancellationRequested();
        if (current?.선정후보 is null) return null;

        var selected = decision.StatusCode == "Applied" && decision.DriverId is { } driverId
            && initiallyAllowed.Contains(driverId)
            ? CargoDispatchCandidateConstraints.Eligible(current)
                .FirstOrDefault(x => string.Equals(x.기사Id, driverId, StringComparison.Ordinal))?.서버후보
            : null;
        if (selected is not null) return Tag(selected, "Applied");

        // 임의 상태/모델 문장을 추천사유로 복사하지 않는다. 실패도 현재 규칙의 최신 수행 근거를 보존한다.
        var status = decision.StatusCode switch
        {
            "Disabled" or "InvalidConfiguration" or "NoCandidates" or "SingleCandidate" or "HttpFailure"
                or "ResponseTooLarge" or "InvalidResponse" or "InvalidSelection" or "Timeout"
                or "NetworkFailure" or "Unavailable" or "Busy" or "Cooldown" or "BudgetExhausted" => decision.StatusCode,
            _ => "CandidateChanged"
        };
        return Tag(current.선정후보, status);
    }

    private static 배차추천후보 Tag(배차추천후보 candidate, string status)
        => candidate with { 추천사유 = $"{candidate.추천사유} · CargoDispatchAI.{status}" };
}
