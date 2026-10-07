using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>이번 명령의 응답을 고정하고 같은 원장의 후속 조회가 반영 결과를 확인하는지 대조합니다.</summary>
internal sealed class FoodRoleCompletionExpectation(Func<object, bool> matches)
{
    public bool Matches(object detail) => matches(detail);

    public static FoodRoleCompletionExpectation? Create(string itemId, Guid requestId, object body, object? response,
        long beforeRevision, string? beforeAttemptId, string? originalDetailAddress, string? ownerId)
    {
        if (body is 주문자음식주문취소요청 && response is 음식주문응답 cancellation
            && OrderResponse(cancellation, itemId, requestId, beforeRevision, 음식주문상태코드.취소)
            && cancellation.상태 == 음식주문상태코드.취소)
        {
            // 주문자 상세는 요청 ID와 revision을 노출하지 않습니다. 이번 요청의 결속은 명령 응답에서 확인합니다.
            return new(value => value is 주문자음식주문상세응답 current
                && current.주문.주문번호 == itemId && current.주문.상태 == 음식주문상태코드.취소);
        }

        if (body is 음식점주문수락요청 acceptance && response is 음식주문응답 accepted
            && OrderResponse(accepted, itemId, requestId, beforeRevision, 음식주문상태코드.주문확인,
                acceptance.즉시픽업가능여부 ? "음식점 주문 확인 · 기존 준비 완료" : "음식점 주문 확인 · 배차 후 조리")
            && AcceptedStage(accepted.상태) && accepted.음식점수락시각Utc is { } acceptedAt && acceptedAt != default)
        {
            var minutes = acceptance.즉시픽업가능여부 ? 0 : Math.Clamp(acceptance.조리예상분 ?? 15, 1, 180);
            var detailAddress = string.IsNullOrWhiteSpace(acceptance.음식점상세주소) ? originalDetailAddress ?? "" : acceptance.음식점상세주소;
            var memo = Clean(acceptance.수락메모);
            if (!MinutesMatch(accepted, requestId, minutes, beforeRevision) || accepted.음식점명 != acceptance.음식점명 || accepted.음식점주소 != acceptance.음식점주소
                || accepted.음식점상세주소 != detailAddress || Clean(accepted.수락메모) != memo
                || acceptance.음식점위도.HasValue && accepted.음식점위도 != acceptance.음식점위도
                || acceptance.음식점경도.HasValue && accepted.음식점경도 != acceptance.음식점경도) return null;
            var revision = accepted.Revision;
            var name = acceptance.음식점명;
            var address = acceptance.음식점주소;
            var historyReason = acceptance.즉시픽업가능여부 ? "음식점 주문 확인 · 기존 준비 완료" : "음식점 주문 확인 · 배차 후 조리";
            return new(value => value is 음식주문응답 current && current.주문번호 == itemId && current.Revision >= revision
                && History(current, requestId, 음식주문상태코드.주문확인, historyReason) && AcceptedStage(current.상태)
                && current.음식점수락시각Utc == acceptedAt && MinutesMatch(current, requestId, minutes, beforeRevision)
                && current.음식점명 == name && current.음식점주소 == address && current.음식점상세주소 == detailAddress
                && Clean(current.수락메모) == memo);
        }

        if (body is 음식점주문진행변경요청 progress && response is 음식주문응답 changed)
        {
            var rejecting = progress.작업 == 음식점주문진행작업코드.거절;
            if (progress.작업 is not (음식점주문진행작업코드.거절 or 음식점주문진행작업코드.조리시간변경)) return null;
            var next = rejecting ? 음식주문상태코드.거절 : null;
            var minutes = rejecting ? (int?)null : progress.조리예상분.HasValue ? Math.Clamp(progress.조리예상분.Value, 1, 180) : null;
            var historyReason = rejecting ? "음식점 주문 거절 · " + progress.사유.Trim() : $"조리 예상 시간 변경 · {minutes}분";
            if (!OrderResponse(changed, itemId, requestId, beforeRevision, next, historyReason)
                || rejecting && changed.상태 != 음식주문상태코드.거절
                || !rejecting && (!AcceptedStage(changed.상태) || !progress.조리예상분.HasValue)) return null;
            if (!rejecting && !PreparationHistory(changed, requestId, historyReason)) return null;
            if (!rejecting && !MinutesMatch(changed, requestId, minutes!.Value, beforeRevision)) return null;
            var revision = changed.Revision;
            return new(value => value is 음식주문응답 current && current.주문번호 == itemId && current.Revision >= revision
                && History(current, requestId, next, historyReason) && (rejecting ? current.상태 == 음식주문상태코드.거절
                    : AcceptedStage(current.상태) && PreparationHistory(current, requestId, historyReason)
                        && MinutesMatch(current, requestId, minutes!.Value, beforeRevision)));
        }

        if (body is 음식배달중단요청 && response is FoodDeliveryDriverActionResponse interrupted
            && !string.IsNullOrWhiteSpace(beforeAttemptId) && !string.IsNullOrWhiteSpace(ownerId)
            && interrupted.OfferId == itemId && interrupted.DeliveryAttemptId == beforeAttemptId
            && interrupted.Status == "Interrupted" && interrupted.AttemptRevision is { } attemptRevision && attemptRevision > beforeRevision)
        {
            var attemptId = beforeAttemptId;
            return new(value => value is FoodDeliveryDriverWorkspaceDto workspace && workspace.DriverId == ownerId
                && workspace.ActiveDeliveries.All(active => active.DeliveryAttemptId != attemptId
                    && !(active.OfferId == itemId && string.IsNullOrWhiteSpace(active.DeliveryAttemptId))));
        }

        if (body is 음식배달중단검토요청 review && response is 음식배달시도운영응답 reviewed)
        {
            var responsibility = review.판정Code switch
            {
                음식배달중단검토판정Code.보호 => 운영배차책임Code.보호대상,
                음식배달중단검토판정Code.기사책임 => 운영배차책임Code.기사,
                음식배달중단검토판정Code.음식점책임 => 운영배차책임Code.음식점,
                음식배달중단검토판정Code.플랫폼책임 => 운영배차책임Code.플랫폼,
                _ => null
            };
            var reason = review.판정사유.Trim();
            if (responsibility is null || reviewed.시도StableId != beforeAttemptId || reviewed.Revision <= beforeRevision
                || reviewed.상태Code != 음식배달시도상태Code.중단 || reviewed.책임Code != responsibility
                || reviewed.악용확정여부 != review.악용확정여부 || reviewed.검토사유.Trim() != reason) return null;
            var revision = reviewed.Revision;
            var attemptId = reviewed.시도StableId;
            var abuse = review.악용확정여부;
            return new(value => value is 음식주문운영추적응답 trace && trace.주문번호 == itemId
                && trace.배달시도목록.Any(current => current.시도StableId == attemptId && current.Revision >= revision
                    && current.상태Code == 음식배달시도상태Code.중단 && current.책임Code == responsibility
                    && current.악용확정여부 == abuse && current.검토사유.Trim() == reason));
        }
        return null;
    }

    private static bool OrderResponse(음식주문응답 response, string itemId, Guid requestId, long beforeRevision, string? next, string? reason = null)
        => response.주문번호 == itemId && response.Revision > beforeRevision && History(response, requestId, next, reason);
    private static bool History(음식주문응답 order, Guid requestId, string? next, string? reason = null)
        => order.상태이력.Any(history => history.클라이언트요청Id == requestId && (next is null || history.다음상태 == next)
            && (reason is null || history.사유 == reason));
    private static bool PreparationHistory(음식주문응답 order, Guid requestId, string reason)
        => order.상태이력.Any(history => history.클라이언트요청Id == requestId && history.사유 == reason
            && history.이전상태 == history.다음상태 && history.다음상태 is (음식주문상태코드.주문확인 or 음식주문상태코드.기사배정 or 음식주문상태코드.조리중));
    private static bool MinutesMatch(음식주문응답 order, Guid requestId, int minutes, long responseRevision)
    {
        if (order.조리예상분 == minutes) return true;
        if (order.Revision <= responseRevision) return false;
        var history = order.상태이력.ToArray();
        var own = Array.FindLastIndex(history, value => value.클라이언트요청Id == requestId);
        if (own < 0) return false;
        int? lastMinutes = null;
        const string changePrefix = "조리 예상 시간 변경 · ";
        foreach (var later in history.Skip(own + 1))
        {
            if (later.사유 == "음식점 픽업 준비 완료") lastMinutes = 0;
            else if (later.사유.StartsWith(changePrefix, StringComparison.Ordinal)
                && later.사유.EndsWith('분') && int.TryParse(later.사유[changePrefix.Length..^1], out var changed) && changed is >= 1 and <= 180)
                lastMinutes = changed;
            else if (later.전이시각Utc >= history[own].전이시각Utc && CookingMinutesMatch(order, later, history))
                lastMinutes = order.조리예상분;
        }
        return lastMinutes.HasValue && order.조리예상분 == lastMinutes;
    }
    private static bool CookingMinutesMatch(음식주문응답 order, 음식주문상태전이기록Dto later, 음식주문상태전이기록Dto[] history)
    {
        // 새 조리 시작의 적용 분은 이력에 없으므로 현재 차수의 시작·완료 예정 시각까지 결속합니다.
        if (later.전이시각Utc == default || later.다음상태 != 음식주문상태코드.조리중
            || order.CurrentCookingStartedAtUtc != later.전이시각Utc || order.조리예상분 is not (> 0 and <= 180)
            || order.조리예상완료시각Utc != later.전이시각Utc.AddMinutes(order.조리예상분.Value)) return false;
        if (later.사유 == "음식점 조리 시작" && later.이전상태 == 음식주문상태코드.기사배정)
            return order.CurrentPreparationRound == 1 && order.RecookingRequestedAtUtc is null;
        const string recookingReason = "픽업 후 배달 중단 · 재조리·재배차";
        return later.사유 == recookingReason && later.이전상태 == 음식주문상태코드.픽업완료
            && order.RecookingRequestedAtUtc == later.전이시각Utc
            && order.CurrentPreparationRound == 1 + history.Count(value => value.사유 == recookingReason
                && value.이전상태 == 음식주문상태코드.픽업완료 && value.다음상태 == 음식주문상태코드.조리중);
    }
    private static bool AcceptedStage(string status) => status is 음식주문상태코드.주문확인 or 음식주문상태코드.기사배정
        or 음식주문상태코드.조리중 or 음식주문상태코드.픽업대기 or 음식주문상태코드.픽업완료 or 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인;
    private static string Clean(string? value) => value?.Trim() ?? "";
}
