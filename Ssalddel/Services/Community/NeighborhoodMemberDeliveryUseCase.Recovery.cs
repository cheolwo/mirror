using System.Data;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Domain.Community;
using Ssalddel.Services.PrivacySupport;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.Community;

public sealed partial class NeighborhoodMemberDeliveryUseCase
{
    public async Task CancelAsync(string requestId, NeighborhoodMemberCommandRequest request, CancellationToken ct = default)
    {
        var actor = Actor(); var digest = Digest(request);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await ReplayAsync(actor, request.ClientRequestId, "Cancel", requestId, digest, ct)) return;
        var row = await Deliveries.SingleOrDefaultAsync(x => x.RequestId == requestId && x.OwnerUserId == actor, ct);
        Require(row is not null, "MemberDeliveryNotFound", "본인의 배송 의뢰를 찾을 수 없습니다.", 404);
        Require(row!.Revision == request.ExpectedRevision && row.StateCode is "Pending" or "Assigned",
            "MemberPickupRecoveryRequired", "픽업 이후 또는 문제 검토 중에는 지원 사건에서 물품 반환·재전달을 확인해 주세요.");
        if (row.AssignedMemberUserId is { } member)
            await NeighborhoodMemberWorkExclusion.LockAndHasMemberWorkAsync(db, member, ct);
        await CancelRowsAsync(row, ct);
        Receipt(actor, request.ClientRequestId, "Cancel", requestId, digest); await SaveAsync(ct); await tx.CommitAsync(ct); await SyncLedgerAsync(requestId, actor, ct);
    }
    private async Task CancelRowsAsync(NeighborhoodMemberDelivery row, CancellationToken ct)
    {
        var queue = await db.운송원장.SingleAsync(x => x.의뢰Id == row.RequestId, ct);
        Require(queue.일반회원배송전용 && queue.확정기사Id is null && queue.출발_픽업 is null,
            "MemberPickupRecoveryRequired", "물품 인수 이후에는 자동 취소할 수 없습니다.");
        var source = await SourceAsync(row.RequestId, ct);
        row.StateCode = "Cancelled"; row.Revision++; row.UpdatedAtUtc = Now;
        source.상태 = 상태값.의뢰상태.취소; source.배차상태 = 상태값.배차상태.취소; source.UpdatedAt = Now;
        queue.배차큐단계 = 상태값.배차큐단계.종료; queue.배차노출상태 = 상태값.배차노출상태.종료;
        queue.생활배송수락준비완료 = false; queue.생활배송선택판본++; queue.UpdatedAt = Now;
        if (row.AssignedMemberUserId is { } member)
            await Participants.Where(x => x.UserId == member && x.ActiveRequestId == row.RequestId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveRequestId, (string?)null), ct);
        db.운송이벤트.Add(new() { 의뢰Id = row.RequestId, 이벤트타입 = "NeighborhoodMemberCancelled",
            메타데이터 = System.Text.Json.JsonSerializer.Serialize(new { row.Revision }) });
    }
    /// <summary>Controller의 서버관리자 정책과 닫힌 지원 사건 근거를 모두 요구한다. 종결은 지급·송금 완료가 아니다.</summary>
    public async Task<NeighborhoodMemberRecoveryResponse> RecoverAsync(string requestId, NeighborhoodMemberRecoveryRequest request,
        I보호지원Store cases, CancellationToken ct)
    {
        Gate(); var actor = Actor(); var digest = Digest(request);
        Require(request.ResolutionCode is "Resume" or "CancelBeforePickup", "MemberRecoveryInvalid", "재개 또는 픽업 전 취소를 선택해 주세요.", 400);
        var incident = await cases.조회Async(request.SupportCaseId, ct);
        Require(incident is not null && incident.SourceKind == 보호지원출처Codes.생활배송 && incident.SourceId == requestId
            && incident.StatusCode == "closed" && incident.ClosedAtUtc is not null
            && incident.PartyUserIds.Length > 0 && incident.PartyUserIds.All(p => incident.Notifications.Any(n => n.Kind == "result"
                && n.RecipientUserId == p && n.ReviewCycle == incident.ReviewCycle && n.DeliveredAtUtc != default
                && !string.IsNullOrWhiteSpace(n.EvidenceRef))),
            "MemberRecoveryEvidenceRequired", "당사자에게 결과가 통지된 종결 지원 사건이 필요합니다.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await ReplayAsync(actor, request.ClientRequestId, "Recovery", requestId, digest, ct))
        {
            var saved = await Deliveries.AsNoTracking().SingleAsync(x => x.RequestId == requestId, ct);
            return new() { RequestId = requestId, Revision = saved.Revision, StateCode = saved.StateCode };
        }
        var row = await Deliveries.SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
        Require(row is not null && row.StateCode == "Issue" && row.Revision == request.ExpectedRevision && row.SupportCaseId == request.SupportCaseId,
            "MemberRecoveryChanged", "업무 또는 지원 사건이 바뀌었습니다.");
        await NeighborhoodMemberWorkExclusion.LockAndHasMemberWorkAsync(db, row!.AssignedMemberUserId!, ct);
        if (request.ResolutionCode == "CancelBeforePickup")
        {
            Require(row.IssueCode == "BeforePickupReviewRequired", "MemberPickupRecoveryRequired", "물품 인수 이후에는 픽업 전 취소로 처리할 수 없습니다.");
            await CancelRowsAsync(row, ct);
        }
        else
        {
            Require(row.AssignedAtUtc > Now.AddHours(-72), "MemberDisclosureExpired", "정보 제공 동의 기한을 다시 확인해 주세요.");
            await RequireApplicationConsentAsync(row, ct);
            row.StateCode = row.IssueCode == "BeforePickupReviewRequired" ? "Assigned" : "PickedUp";
            row.Revision++; row.UpdatedAtUtc = Now;
            // 이전 사진의 판본은 재개된 명령을 인증하지 않는다. 해당 단계에서 새 사진을 촬영한다.
            if (row.StateCode == "Assigned") row.PickupEvidenceId = null; else row.DeliveryEvidenceId = null;
        }
        Receipt(actor, request.ClientRequestId, "Recovery", requestId, digest);
        db.운송이벤트.Add(new() { 의뢰Id = requestId, 이벤트타입 = "NeighborhoodMemberRecovered",
            메타데이터 = System.Text.Json.JsonSerializer.Serialize(new { row.Revision, request.ResolutionCode, request.SupportCaseId }) });
        await SaveAsync(ct); await tx.CommitAsync(ct);
        await SyncLedgerAsync(requestId, actor, ct);
        return new() { RequestId = requestId, Revision = row.Revision, StateCode = row.StateCode };
    }
}
