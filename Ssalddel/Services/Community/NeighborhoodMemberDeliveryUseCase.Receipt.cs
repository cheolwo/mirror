using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.Community;

public sealed partial class NeighborhoodMemberDeliveryUseCase
{
    /// <summary>협업 수령 intent를 만들기 전 실제 전달과 당사자·사진 결속을 검증합니다.</summary>
    public async Task<생활협업수령IntentRecord> PrepareReceiptAsync(생활협업Record collaboration, string actor,
        Guid clientRequestId, string fingerprint, CancellationToken ct = default)
    {
        Gate(); Require(Actor() == actor, "MemberReceiptActorMismatch", "현재 로그인 당사자를 다시 확인해 주세요.", 403);
        db.ChangeTracker.Clear();
        var row = await ValidateReceiptWorkAsync(collaboration, actor, ct);
        return new() { ClientRequestId = clientRequestId, ActorUserId = actor, Fingerprint = fingerprint,
            RequestId = row.RequestId, TermsRevision = collaboration.TermsRevision, WorkRevision = row.Revision,
            DeliveryEvidenceId = row.DeliveryEvidenceId!, RecordedAtUtc = Now };
    }

    /// <summary>기존 협업 명령 내부에서만 호출합니다. Mongo CAS 고정 없이는 SQL 수령을 확정하지 않습니다.</summary>
    public async Task<DateTime> ConfirmReceiptAsync(생활협업Record collaboration, CancellationToken ct = default)
    {
        Gate(); var actor = Actor(); var intent = collaboration.ReceiptConfirmationIntent;
        Require(intent is { StateCode: "pending" } && intent.ActorUserId == actor,
            "MemberReceiptIntentRequired", "같은 수령 확인 요청의 결과를 확인해 주세요.");
        Require(memberCollaborations is not null, "MemberCollaborationUnavailable", "협업 저장소 연결을 확인할 수 없습니다.", 503);
        var stored = await memberCollaborations!.조회Async(collaboration.StableId, ct);
        Require(stored is not null && stored.Revision == collaboration.Revision && stored.TermsRevision == intent!.TermsRevision
            && stored.LinkedDeliveryRequestId == intent.RequestId && stored.ReceiptConfirmationIntent?.StateCode == "pending"
            && stored.ReceiptConfirmationIntent.ClientRequestId == intent.ClientRequestId
            && stored.ReceiptConfirmationIntent.ActorUserId == actor && stored.ReceiptConfirmationIntent.Fingerprint == intent.Fingerprint,
            "MemberReceiptIntentChanged", "협업 수령 판본이 변경되었습니다. 같은 요청 결과를 확인해 주세요.");
        var digest = Digest(new { collaboration.StableId, intent!.TermsRevision, intent.RequestId, intent.WorkRevision,
            intent.DeliveryEvidenceId, intent.Fingerprint });
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await ReplayAsync(actor, intent.ClientRequestId, "ReceiptConfirmation", intent.RequestId, digest, ct))
        {
            var prior = await Receipts.AsNoTracking().SingleAsync(x => x.ActorUserId == actor && x.ClientRequestId == intent.ClientRequestId, ct);
            await tx.CommitAsync(ct); return prior.RecordedAtUtc;
        }
        Require(!await Receipts.AnyAsync(x => x.AggregateId == intent.RequestId && x.Kind == "ReceiptConfirmation", ct),
            "MemberReceiptAlreadyConfirmed", "이미 수령 확인된 배송입니다.");
        Require(await db.운송원장.Where(x => x.의뢰Id == intent.RequestId && x.일반회원배송전용 && x.확정기사Id == null
            && x.생활배송협업Id == collaboration.StableId && x.생활배송조건판본 == intent.TermsRevision
            && x.배차큐단계 == 상태값.배차큐단계.종료)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.생활배송선택판본, x => x.생활배송선택판본), ct) == 1,
            "MemberReceiptBindingChanged", "현재 회원 배송과 협업 연결을 확인해 주세요.");
        var work = await ValidateReceiptWorkAsync(stored!, actor, ct);
        Require(work.Revision == intent.WorkRevision && work.DeliveryEvidenceId == intent.DeliveryEvidenceId,
            "MemberRevisionConflict", "전달 증빙과 업무 판본이 변경되었습니다.");
        Require(await Deliveries.Where(x => x.RequestId == intent.RequestId && x.Revision == intent.WorkRevision && x.StateCode == "Delivered")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revision, x => x.Revision + 1).SetProperty(x => x.UpdatedAtUtc, Now), ct) == 1,
            "MemberRevisionConflict", "다른 수령 확인이 먼저 처리되었습니다.");
        var source = await SourceAsync(intent.RequestId, ct);
        var queue = await db.운송원장.SingleAsync(x => x.의뢰Id == intent.RequestId, ct);
        source.배차상태 = 상태값.배차상태.인수완료; source.인수증등록일시 = Now; source.UpdatedAt = Now;
        queue.생활배송선택판본++; queue.UpdatedAt = Now;
        Receipt(actor, intent.ClientRequestId, "ReceiptConfirmation", intent.RequestId, digest);
        db.운송이벤트.Add(new() { 의뢰Id = intent.RequestId, 이벤트타입 = "NeighborhoodMemberReceiptConfirmed",
            메타데이터 = JsonSerializer.Serialize(new { CollaborationId = collaboration.StableId, intent.TermsRevision,
                intent.ClientRequestId, ActorUserId = actor, WorkRevision = intent.WorkRevision + 1, intent.DeliveryEvidenceId }) });
        await SaveAsync(ct); await tx.CommitAsync(ct);
        // 결제·정산 상태와 담당 회원의 Delivered 상태는 수령 사실로 덮어쓰지 않습니다.
        return source.인수증등록일시!.Value;
    }

    public async Task SyncReceiptLedgerAsync(string requestId, string actor, CancellationToken ct = default)
    {
        if (ledgerSync is null) return;
        var queue = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == requestId && x.일반회원배송전용, ct);
        Require(await ledgerSync.운송실행투영동기화Async(queue, actor, ct) is not null,
            "MemberReceiptProjectionPending", "수령은 기록되었고 원장 반영을 기다립니다. 같은 요청으로 확인해 주세요.", 503);
    }

    private async Task<Ssalddel.Domain.Community.NeighborhoodMemberDelivery> ValidateReceiptWorkAsync(생활협업Record collaboration, string actor, CancellationToken ct)
    {
        var recipient = collaboration.ProviderRoleCode == "owner" ? collaboration.RequesterUserId
            : collaboration.ProviderRoleCode == "requester" ? collaboration.OwnerUserId : null;
        Require(recipient is not null && recipient == actor, "MemberRecipientRequired", "실제 물품 수령 당사자만 확인할 수 있습니다.", 403);
        Require(collaboration.Kind == NeighborhoodCollaborationKinds.Goods && 생활협업UseCase.CurrentAgreement(collaboration)
            && collaboration.StatusCode == NeighborhoodCollaborationStates.CompletionProposed
            && collaboration.LinkedDeliveryRequestId is not null && collaboration.DeliveryIntent?.StateCode == "confirmed"
            && collaboration.DeliveryIntent.TermsRevision == collaboration.TermsRevision,
            "MemberReceiptAgreementRequired", "현재 양측의 물품 협업과 배송 연결을 확인해 주세요.");
        var row = await Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == collaboration.LinkedDeliveryRequestId, ct);
        Require(row is not null && row.StateCode == "Delivered" && row.CompletedAtUtc.HasValue && row.DeliveryEvidenceId is not null
            && row.AssignedMemberUserId is not null && row.AssignedAtUtc > Now.AddHours(-72),
            "MemberDeliveryRequired", "사진 전달 완료된 현재 회원 배송만 수령 확인할 수 있습니다.");
        await RequireApplicationConsentAsync(row!, ct);
        var source = await SourceAsync(row!.RequestId, ct);
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == row.RequestId, ct);
        Require(queue is { 일반회원배송전용: true, 확정기사Id: null } && queue.일반회원수행자UserId == row.AssignedMemberUserId
            && queue.생활배송협업Id == collaboration.StableId && queue.생활배송조건판본 == collaboration.TermsRevision
            && queue.배차큐단계 == 상태값.배차큐단계.종료 && source.배차상태 == 상태값.배차상태.하차완료
            && source.상태 == 상태값.의뢰상태.생성됨 && source.주문자UserId == row.OwnerUserId
            && source.주문자UserId == collaboration.DeliveryIntent!.ActorUserId
            && 생활배송출처Evidence.Read(source.정산메모) == collaboration.SourcePostId && collaboration.SourcePostId is > 0,
            "MemberReceiptBindingChanged", "원 의뢰·협업 조건·회원 수행 연결이 일치하지 않습니다.");
        var photo = await Photos.AsNoTracking().SingleOrDefaultAsync(x => x.EvidenceId == row.DeliveryEvidenceId, ct);
        Require(photo is not null && photo.StateCode == "Bound" && photo.StageCode == "Delivery" && photo.RequestId == row.RequestId
            && photo.ActorUserId == row.AssignedMemberUserId && photo.WorkRevision == row.Revision - 1,
            "MemberDeliveryPhotoRequired", "이 회원 배송의 전달 사진이 확인되어야 합니다.");
        var pointer = Open<PhotoPointer>(photo!.PrivatePayload);
        var bytes = await storage.DownloadAsync(pointer.Container, pointer.ObjectName, ct);
        Require(Convert.ToHexString(SHA256.HashData(bytes)) == photo.StoredSha256,
            "MemberPhotoStorageIntegrityMismatch", "전달 사진의 실제 저장을 확인하지 못했습니다.", 503);
        return row;
    }
}
