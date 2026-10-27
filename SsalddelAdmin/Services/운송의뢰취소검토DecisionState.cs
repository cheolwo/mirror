using Ssalddel.Contracts.Admin.Transport;
using Ssalddel.Contracts.Common.Community;

namespace SsalddelAdmin.Services;

/// <summary>취소 검토 대상과 명시 확인 입력만 소유하며 실제 명령 순서는 페이지가 조립한다.</summary>
internal sealed class 운송의뢰취소검토DecisionState
{
    public 지도신청가원장Response? Review { get; set; }
    public bool ShowConfirmation { get; set; }
    public string? Reason { get; set; }
    public string? ConfirmationRequestId { get; set; }

    public 관리자운송의뢰취소환불판단 Decision(화주운송의뢰응답? request)
        => request is null
            ? new(false, false, "처리 불가", "의뢰를 먼저 조회해 주세요.")
            : 관리자운송의뢰취소환불정책.평가(
                request.의뢰상태,
                request.결제상태,
                request.정산상태,
                request.배차상태);

    public string? ConfirmationError(string? requestId)
        => 관리자운송의뢰취소환불정책.명시적확인오류(requestId, ConfirmationRequestId, Reason);

    public bool ConfirmationStarted
        => !string.IsNullOrWhiteSpace(ConfirmationRequestId)
           || !string.IsNullOrWhiteSpace(Reason);

    public bool CanSubmitCancellation(화주운송의뢰응답? request, bool isSaving)
        => !isSaving && Decision(request).처리가능 && ConfirmationError(request?.의뢰Id) is null;

    public bool CanSubmitReviewDecision(화주운송의뢰응답? request, bool isSaving)
        => !isSaving && Review is not null && ConfirmationError(request?.의뢰Id) is null;

    public void Open()
    {
        ShowConfirmation = true;
        Reason = null;
        ConfirmationRequestId = null;
    }

    public void Close()
    {
        ShowConfirmation = false;
        Reason = null;
        ConfirmationRequestId = null;
    }

    public void SelectReview(IReadOnlyList<지도신청가원장Response> reviews, string? ledgerId, string requestId)
        => Review = reviews.FirstOrDefault(item =>
            string.Equals(item.원장Id, ledgerId, StringComparison.Ordinal)
            && string.Equals(item.운영원본Id, requestId, StringComparison.Ordinal));
}
