using Ssalddel.Contracts.Food;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Services.Food;

internal static class 음식배달업무상태전이Guard
{
    internal static string 정본상태확인(string? 상태)
    {
        // 과거 저장 별칭은 보존하되 미지원 raw 상태를 주문대기로 복구하지 않습니다.
        if (!음식주문상태코드.지원여부(상태) && 상태?.Trim() != "주문접수")
            throw new InvalidOperationException("지원하지 않는 음식 주문 상태에서는 업무를 변경할 수 없습니다.");
        return 음식주문상태코드.Normalize(상태);
    }

    internal static 업무상태전이판정 판정(
        string? 현재상태,
        string? 목표상태,
        IBusinessWorkflowRuleEngine? engine = null)
    {
        var decision = (engine ?? BusinessWorkflowRuleEngine.기본).판정(
            new BusinessWorkflowTransitionRequest
            {
                업무흐름코드 = 업무흐름코드.음식배달,
                현재상태코드 = 정본상태확인(현재상태),
                목표상태코드 = 정본상태확인(목표상태),
            });
        return new 업무상태전이판정
        {
            허용여부 = decision.허용여부,
            멱등재시도여부 = decision.멱등재시도여부,
            RuleRevision = decision.RuleRevision,
            SourceStableIds = decision.SourceStableIds,
            차단사유코드목록 = decision.차단사유코드목록,
        };
    }

    internal static void 허용확인(string? 현재상태, string? 목표상태)
    {
        var result = 판정(현재상태, 목표상태);
        if (result.허용여부)
        {
            return;
        }

        throw new InvalidOperationException(
            $"음식배달 공통 업무 규칙이 허용하지 않는 상태 전이입니다. "
            + $"현재상태={음식주문상태코드.Normalize(현재상태)}, "
            + $"목표상태={음식주문상태코드.Normalize(목표상태)}, "
            + $"규칙판본={result.RuleRevision}, "
            + $"차단사유={string.Join(',', result.차단사유코드목록)}");
    }
}
