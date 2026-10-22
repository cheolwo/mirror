namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed partial class InboundRequestPageViewModel
{
    /// <summary>사진 준비 기록을 새 신청에 연결합니다. 미확정인 다른 신청의 번호는 바꾸지 않습니다.</summary>
    public bool BindPhotoRequest(Guid? photoRequestId)
    {
        if (photoRequestId == Guid.Empty || State.RecoveryBlocked) return false;
        if (photoRequestId.HasValue && State.Current is { } current
            && current.가입고사진신청Id != photoRequestId) return false;
        if (_inboundPending is { } pending && photoRequestId.HasValue
            && (pending.RequestId != photoRequestId.Value || !IsServerRequest(pending)))
            return false;
        if (_inboundPending is null || photoRequestId.HasValue)
            CreateDraft.PhotoRequestId = photoRequestId;
        return true;
    }
}
