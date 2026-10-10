using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

/// <summary>연결 배송의 복구 입력과 원 요청의 조회 근거만 보유합니다. 사건·계정·전송 수명은 부모가 조율합니다.</summary>
internal sealed class MemberDeliveryRecoveryState
{
    public NeighborhoodMemberRecoveryContextResponse? Context { get; private set; }
    public string Resolution { get; set; } = "Resume";
    public bool Confirmed { get; set; }
    public NeighborhoodMemberRecoveryRequest? Pending { get; private set; }
    public string? RequestId { get; private set; }
    public bool Acknowledged { get; private set; }
    public bool ReadSucceeded { get; private set; }
    public bool HasPending => Pending is not null;
    public bool CanRetry => HasPending && !Acknowledged && ReadSucceeded;

    public bool CanSubmit(string requestId, string caseId) => Confirmed && Context is { } current
        && requestId == current.RequestId && caseId == current.SupportCaseId
        && (Resolution == "Resume" && current.CanResume || Resolution == "CancelBeforePickup" && current.CanCancelBeforePickup);

    public void ApplyLoadedContext(NeighborhoodMemberRecoveryContextResponse row, string requestId, string caseId)
    {
        ValidateContext(row, requestId, caseId);
        Context = row;
        if (!row.CanResume && row.CanCancelBeforePickup) Resolution = "CancelBeforePickup";
    }

    public void ClearContext() => Context = null;

    public void PrepareSubmission()
    {
        RequestId = Context!.RequestId;
        Pending = new()
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = Context.Revision,
            SupportCaseId = Context.SupportCaseId, ResolutionCode = Resolution
        };
        Acknowledged = ReadSucceeded = false;
    }

    public void BeginRead() => ReadSucceeded = false;

    public void ApplyAcknowledgement(NeighborhoodMemberRecoveryResponse response, string requestId, NeighborhoodMemberRecoveryRequest request)
    {
        if (response.RequestId != requestId || response.Revision <= request.ExpectedRevision) throw new InvalidOperationException();
        Acknowledged = true;
        ReadSucceeded = false;
    }

    public MemberDeliveryRecoveryReadOutcome ApplyRead(NeighborhoodMemberRecoveryContextResponse row, string requestId,
        NeighborhoodMemberRecoveryRequest request)
    {
        ValidateContext(row, requestId, request.SupportCaseId);
        Context = row;
        if (row.AppliedClientRequestId == request.ClientRequestId && row.AppliedResolutionCode == request.ResolutionCode
            && row.Revision > request.ExpectedRevision)
        {
            ClearPending();
            return MemberDeliveryRecoveryReadOutcome.Applied;
        }
        if (row.Revision != request.ExpectedRevision || row.StateCode != "Issue")
        {
            ClearPending();
            return MemberDeliveryRecoveryReadOutcome.Changed;
        }
        ReadSucceeded = request.ResolutionCode == "Resume" ? row.CanResume : row.CanCancelBeforePickup;
        if (!ReadSucceeded)
        {
            ClearPending();
            return MemberDeliveryRecoveryReadOutcome.Blocked;
        }
        return MemberDeliveryRecoveryReadOutcome.Pending;
    }

    public void RejectPendingRead()
    {
        ClearPending();
        Context = null;
    }

    public void Clear()
    {
        Context = null;
        ClearPending();
        Acknowledged = false;
        Resolution = "Resume";
    }

    private void ClearPending()
    {
        Pending = null;
        RequestId = null;
        ReadSucceeded = Confirmed = false;
    }

    private static void ValidateContext(NeighborhoodMemberRecoveryContextResponse row, string requestId, string caseId)
    {
        if (row.RequestId != requestId || row.SupportCaseId != caseId || row.Revision < 1) throw new InvalidOperationException();
    }
}

internal enum MemberDeliveryRecoveryReadOutcome { Pending, Applied, Changed, Blocked }
