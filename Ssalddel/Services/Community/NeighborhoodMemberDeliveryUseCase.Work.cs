using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Storage;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Services.Community;

public sealed partial class NeighborhoodMemberDeliveryUseCase
{
    public async Task<IReadOnlyList<NeighborhoodMemberWorkResponse>> WorkListAsync(CancellationToken ct = default)
    {
        var actor = Actor(); var rows = await Deliveries.AsNoTracking().Where(x => x.AssignedMemberUserId == actor)
            .OrderByDescending(x => x.AssignedAtUtc).Take(100).ToListAsync(ct);
        var result = new List<NeighborhoodMemberWorkResponse>();
        foreach (var row in rows) result.Add(await WorkDtoAsync(row, ct)); return result;
    }
    public async Task<NeighborhoodMemberWorkResponse?> WorkAsync(string requestId, CancellationToken ct = default)
    {
        var actor = Actor(); var row = await Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == requestId && x.AssignedMemberUserId == actor, ct);
        return row is null ? null : await WorkDtoAsync(row, ct);
    }
    private async Task<NeighborhoodMemberWorkResponse> WorkDtoAsync(NeighborhoodMemberDelivery row, CancellationToken ct)
    {
        var source = await SourceAsync(row.RequestId, ct);
        var accessible = Enabled && (row.StateCode is "Assigned" or "PickedUp" or "Issue")
            && row.AssignedAtUtc > Now.AddHours(-72) && source.상태 == 상태값.의뢰상태.생성됨;
        if (accessible)
        {
            try { await RequireApplicationConsentAsync(row, ct); }
            catch (NeighborhoodMemberDeliveryException) { accessible = false; }
        }
        return new() { RequestId = row.RequestId, Revision = row.Revision, StateCode = row.StateCode,
            CargoName = source.화물종류, Quantity = source.화물수량 ?? 1, WeightKg = source.화물중량Kg ?? 0,
            FareKrw = row.FareKrw, DistanceKm = row.DistanceKm, RequestNotes = accessible ? source.요청사항 : null,
            Pickup = accessible && row.StateCode != "PickedUp" ? Location(source, true) : null,
            Dropoff = accessible ? Location(source, false) : null,
            EvidenceStageCode = accessible ? row.StateCode == "Assigned" ? "Pickup" : row.StateCode == "PickedUp" ? "Delivery" : "" : "",
            CanPickup = accessible && row.StateCode == "Assigned" && row.PickupEvidenceId is not null,
            CanDeliver = accessible && row.StateCode == "PickedUp" && row.DeliveryEvidenceId is not null,
            PickupEvidenceId = row.PickupEvidenceId, DeliveryEvidenceId = row.DeliveryEvidenceId,
            IssueCode = row.IssueCode, SupportCaseId = row.SupportCaseId, CompletedAtUtc = row.CompletedAtUtc,
            ReceiptConfirmedAtUtc = await Receipts.AsNoTracking().Where(x => x.AggregateId == row.RequestId && x.Kind == "ReceiptConfirmation")
                .Select(x => (DateTime?)x.RecordedAtUtc).SingleOrDefaultAsync(ct) };
    }
    private static LocationContactDTO Location(화주운송의뢰 s, bool pickup)
        => new()
        {
            주소 = new AddressDTO { 도로명주소 = pickup ? s.픽업_도로명주소 : s.하차_도로명주소,
                상세주소 = pickup ? s.픽업_상세주소 : s.하차_상세주소,
                위도 = pickup ? s.픽업_위도 : s.하차_위도, 경도 = pickup ? s.픽업_경도 : s.하차_경도 },
            연락처 = new ContactDTO { 이름 = pickup ? s.픽업_연락처_이름 : s.하차_연락처_이름,
                전화번호 = pickup ? s.픽업_연락처_전화번호 : s.하차_연락처_전화번호 },
            시간창 = pickup ? new() { 시작일시 = s.픽업_시간창_시작일시, 종료일시 = s.픽업_시간창_종료일시 }
                : s.하차_시간창_시작일시 is { } start && s.하차_시간창_종료일시 is { } end
                    ? new() { 시작일시 = start, 종료일시 = end } : null
        };
    private async Task<NeighborhoodMemberDelivery> AssignedAsync(string requestId, string actor, long revision, CancellationToken ct)
    {
        Require(await db.운송원장.Where(x => x.의뢰Id == requestId && x.일반회원배송전용
            && x.일반회원수행자UserId == actor && x.확정기사Id == null && x.배차큐단계 == 상태값.배차큐단계.확정)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.생활배송선택판본, x => x.생활배송선택판본), ct) == 1,
            "MemberAssignmentChanged", "현재 배정된 수행자와 업무 단계를 다시 확인해 주세요.");
        var row = await Deliveries.SingleOrDefaultAsync(x => x.RequestId == requestId && x.AssignedMemberUserId == actor, ct);
        Require(row is not null, "MemberWorkNotFound", "본인이 배정된 생활 배송을 찾을 수 없습니다.", 404);
        Require(row!.Revision == revision && row.StateCode is "Assigned" or "PickedUp" or "Issue",
            "MemberRevisionConflict", "수행 상태가 바뀌었습니다. 최신 업무를 다시 확인해 주세요.");
        var queue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        var source = await SourceAsync(requestId, ct);
        Require(queue is { 일반회원배송전용: true } && queue.일반회원수행자UserId == actor && queue.확정기사Id is null
            && queue.배차큐단계 == 상태값.배차큐단계.확정 && source.상태 == 상태값.의뢰상태.생성됨
            && row.AssignedAtUtc > Now.AddHours(-72), "MemberAssignmentChanged", "담당·동의·업무 기한을 다시 확인해 주세요.");
        await RequireApplicationConsentAsync(row, ct); return row;
    }
    private sealed record PhotoPointer(string Container, string ObjectName);
    public async Task<NeighborhoodMemberEvidenceReceiptResponse?> EvidenceReceiptAsync(string requestId, Guid clientRequestId, CancellationToken ct = default)
    {
        var actor = Actor();
        var photo = await Photos.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor && x.RequestId == requestId && x.UploadRequestId == clientRequestId, ct);
        if (photo is null || !await Deliveries.AsNoTracking().AnyAsync(x => x.RequestId == requestId && x.AssignedMemberUserId == actor, ct)) return null;
        return new() { RequestId = requestId, ClientRequestId = clientRequestId, EvidenceId = photo.EvidenceId,
            StageCode = photo.StageCode, StateCode = photo.StateCode, WorkRevision = photo.WorkRevision,
            Committed = await Receipts.AsNoTracking().AnyAsync(x => x.ActorUserId == actor && x.ClientRequestId == clientRequestId
                && x.Kind == "Evidence" && x.AggregateId == requestId, ct) };
    }
    public async Task<NeighborhoodMemberWorkResponse> EvidenceAsync(string requestId, NeighborhoodMemberEvidenceRequest request, CancellationToken ct = default)
    {
        Gate(); var actor = Actor(); var digest = Digest(request); db.ChangeTracker.Clear();
        if (await ReplayAsync(actor, request.ClientRequestId, "Evidence", requestId, digest, ct)) return (await WorkAsync(requestId, ct))!;
        Require(request.StageCode is "Pickup" or "Delivery", "MemberPhotoStageInvalid", "촬영할 업무 단계를 확인해 주세요.", 400);
        Require(storage.IsConfigured(ObjectStorageAccess.Private) && !string.IsNullOrWhiteSpace(storage.GetContainerName(ObjectStorageAccess.Private)),
            "MemberPrivatePhotoStorageUnavailable", "비공개 사진 저장소가 준비되지 않았습니다.", 503);
        byte[] bytes;
        try { bytes = DistributedPhotoEvidenceService.Normalize(DistributedPhotoEvidenceService.Decode(request.ContentBase64, request.ContentType)); }
        catch (DistributedFulfillmentException) { throw new NeighborhoodMemberDeliveryException("MemberPhotoInvalid", "실제 JPEG·PNG 사진을 5MiB 이내로 촬영해 주세요.", 400); }
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var photo = await ExecuteDatabaseMutationAsync<NeighborhoodMemberPhoto?>(async () =>
        {
            await using var prepare = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await ReplayAsync(actor, request.ClientRequestId, "Evidence", requestId, digest, ct)) return null;
            var work = await AssignedAsync(requestId, actor, request.ExpectedRevision, ct);
            Require(request.StageCode == "Pickup" && work.StateCode == "Assigned" || request.StageCode == "Delivery" && work.StateCode == "PickedUp",
                "MemberPhotoStageChanged", "현재 업무 단계에서 필요한 사진을 촬영해 주세요.");
            var pendingPhoto = await Photos.SingleOrDefaultAsync(x => x.ActorUserId == actor && x.UploadRequestId == request.ClientRequestId, ct);
            if (pendingPhoto is null)
            {
                // 이미 배정된 업무 증빙은 비용 경고를 기록하되 한도 초과로 중단하지 않는다.
                if (operationsBudget is not null)
                    await operationsBudget.ConsumeAsync("photos", true, ct);
                var id = Stable("NMP-", actor + ":" + request.ClientRequestId.ToString("N"));
                var name = "neighborhood-member-proof/" + Stable("", actor) + "/" + id + ".jpg";
                pendingPhoto = new() { EvidenceId = id, ActorUserId = actor, UploadRequestId = request.ClientRequestId,
                    RequestId = requestId, WorkRevision = work.Revision, StageCode = request.StageCode, InputDigest = digest,
                    StoredSha256 = hash, StateCode = "Uploading", CreatedAtUtc = Now,
                    PrivatePayload = Seal(new PhotoPointer(storage.GetContainerName(ObjectStorageAccess.Private)!, name)) };
                db.Add(pendingPhoto); await SaveAsync(ct);
            }
            Require(pendingPhoto.RequestId == requestId && pendingPhoto.StageCode == request.StageCode && pendingPhoto.InputDigest == digest
                && pendingPhoto.StoredSha256 == hash && pendingPhoto.WorkRevision == work.Revision && pendingPhoto.StateCode == "Uploading",
                "IdempotencyConflict", "같은 사진 요청에 다른 업무·사진을 보낼 수 없습니다.");
            await prepare.CommitAsync(ct);
            return pendingPhoto;
        }, ct);
        if (photo is null) return (await WorkAsync(requestId, ct))!;
        // 파기와 같은 행 잠금을 유지한다. SQL 재시도는 외부 쓰기를 다시 보내지 않고,
        // 새 잠금에서 아직 유효한 Uploading 정본과 실제 객체의 해시를 다시 확인한다.
        Task<ObjectStorageUploadResult>? uploadTask = null;
        async Task<ObjectStorageUploadResult> UploadOnceAsync(PhotoPointer pointer)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return await storage.UploadImmutableAsync(stream, pointer.ObjectName, "image/jpeg", ObjectStorageAccess.Private,
                new Dictionary<string, string> { ["sha256"] = hash }, ct);
        }
        await ExecuteDatabaseMutationAsync(async () =>
        {
            await using var uploadLock = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await ReplayAsync(actor, request.ClientRequestId, "Evidence", requestId, digest, ct)) return;
            var lockedCount = await Photos.Where(x => x.EvidenceId == photo.EvidenceId && x.StateCode == "Uploading" && x.CreatedAtUtc > Now.AddHours(-24))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StateCode, "Uploading"), ct);
            if (lockedCount != 1)
            {
                // 같은 요청의 다른 호출이 먼저 사진을 확정했을 수 있다. 만료로 오판하지 않는다.
                if (await ReplayAsync(actor, request.ClientRequestId, "Evidence", requestId, digest, ct)) return;
                Require(false, "MemberPhotoUploadExpired", "사진 요청이 만료되었습니다. 최신 업무에서 다시 촬영해 주세요.");
            }
            var locked = await Photos.AsNoTracking().SingleAsync(x => x.EvidenceId == photo.EvidenceId, ct);
            Require(locked.ActorUserId == actor && locked.RequestId == requestId && locked.StageCode == request.StageCode
                && locked.InputDigest == digest && locked.StoredSha256 == hash && locked.WorkRevision == photo.WorkRevision
                && locked.PrivatePayload == photo.PrivatePayload, "IdempotencyConflict", "사진 요청의 정본이 바뀌었습니다.");
            var pointer = Open<PhotoPointer>(locked.PrivatePayload);
            var upload = await (uploadTask ??= UploadOnceAsync(pointer));
            Require(upload.ContainerName == pointer.Container && upload.ObjectName == pointer.ObjectName,
                "MemberPhotoStorageIdentityMismatch", "사진 저장 위치를 검증하지 못했습니다.", 503);
            var downloaded = await storage.DownloadAsync(pointer.Container, pointer.ObjectName, ct);
            Require(downloaded.Length == bytes.Length && Convert.ToHexString(SHA256.HashData(downloaded)) == hash,
                "MemberPhotoStorageIntegrityMismatch", "저장된 사진의 무결성을 확인하지 못했습니다.", 503);
            await uploadLock.CommitAsync(ct);
        }, ct);
        db.ChangeTracker.Clear();
        await ExecuteDatabaseMutationAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await ReplayAsync(actor, request.ClientRequestId, "Evidence", requestId, digest, ct)) return;
            var current = await AssignedAsync(requestId, actor, request.ExpectedRevision, ct);
            var record = await Photos.SingleAsync(x => x.EvidenceId == photo.EvidenceId, ct);
            Require(record.StateCode == "Uploading" && record.WorkRevision == current.Revision, "MemberPhotoStageChanged", "촬영 이후 업무 상태가 바뀌었습니다.");
            Require(record.CreatedAtUtc > Now.AddHours(-24), "MemberPhotoUploadExpired", "사진 요청이 만료되었습니다. 최신 업무에서 다시 촬영해 주세요.");
            current.Revision++; current.UpdatedAtUtc = Now; record.WorkRevision = current.Revision; record.StateCode = "Uploaded";
            if (request.StageCode == "Pickup") current.PickupEvidenceId = record.EvidenceId; else current.DeliveryEvidenceId = record.EvidenceId;
            Receipt(actor, request.ClientRequestId, "Evidence", requestId, digest); await SaveAsync(ct); await tx.CommitAsync(ct);
        }, ct);
        return (await WorkAsync(requestId, ct))!;
    }
    public Task<NeighborhoodMemberWorkResponse> PickupAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct = default)
        => CompleteAsync(id, request, "Pickup", ct);
    public Task<NeighborhoodMemberWorkResponse> DeliverAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct = default)
        => CompleteAsync(id, request, "Delivery", ct);
    private async Task<NeighborhoodMemberWorkResponse> CompleteAsync(string requestId, NeighborhoodMemberCommandRequest request, string stage, CancellationToken ct)
    {
        Gate(); var actor = Actor(); var digest = Digest(request); db.ChangeTracker.Clear();
        await ExecuteDatabaseMutationAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await ReplayAsync(actor, request.ClientRequestId, stage, requestId, digest, ct)) return;
            var work = await AssignedAsync(requestId, actor, request.ExpectedRevision, ct);
            Require(stage == "Pickup" && work.StateCode == "Assigned" || stage == "Delivery" && work.StateCode == "PickedUp",
                "MemberWorkStageChanged", "현재 업무 단계의 행동만 수행할 수 있습니다.");
            var evidence = stage == "Pickup" ? work.PickupEvidenceId : work.DeliveryEvidenceId;
            var photo = evidence is null ? null : await Photos.SingleOrDefaultAsync(x => x.EvidenceId == evidence, ct);
            Require(photo is not null && photo.ActorUserId == actor && photo.RequestId == requestId && photo.StageCode == stage
                && photo.WorkRevision == work.Revision && photo.StateCode == "Uploaded" && photo.CreatedAtUtc > Now.AddHours(-24),
                "MemberPhotoRequired", "이 업무 단계의 사진을 촬영·전송한 뒤 완료할 수 있습니다.");
            var pointer = Open<PhotoPointer>(photo!.PrivatePayload);
            var bytes = await storage.DownloadAsync(pointer.Container, pointer.ObjectName, ct);
            Require(Convert.ToHexString(SHA256.HashData(bytes)) == photo.StoredSha256, "MemberPhotoStorageIntegrityMismatch", "완료에 필요한 실제 사진을 확인하지 못했습니다.", 503);
            var source = await SourceAsync(requestId, ct); var queue = await db.운송원장.SingleAsync(x => x.의뢰Id == requestId, ct);
            photo.StateCode = "Bound"; work.StateCode = stage == "Pickup" ? "PickedUp" : "Delivered"; work.Revision++; work.UpdatedAtUtc = Now;
            source.배차상태 = stage == "Pickup" ? 상태값.배차상태.상차완료 : 상태값.배차상태.하차완료; source.UpdatedAt = Now;
            if (stage == "Pickup") queue.출발_픽업 = Now;
            else
            {
                work.CompletedAtUtc = Now; queue.도착 = Now; queue.배차큐단계 = 상태값.배차큐단계.종료; queue.배차노출상태 = 상태값.배차노출상태.종료;
                await Participants.Where(x => x.UserId == actor && x.ActiveRequestId == requestId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveRequestId, (string?)null), ct);
            }
            queue.생활배송선택판본++; queue.UpdatedAt = Now;
            Receipt(actor, request.ClientRequestId, stage, requestId, digest);
            db.운송이벤트.Add(new() { 의뢰Id = requestId, 이벤트타입 = "NeighborhoodMember" + stage,
                메타데이터 = System.Text.Json.JsonSerializer.Serialize(new { ActorUserId = actor, work.Revision, EvidenceId = evidence }) });
            await SaveAsync(ct); await tx.CommitAsync(ct);
        }, ct);
        await SyncLedgerAsync(requestId, actor, ct); return (await WorkAsync(requestId, ct))!;
    }
    public async Task<NeighborhoodMemberWorkResponse> IssueAsync(string requestId, NeighborhoodMemberIssueRequest request, CancellationToken ct = default)
    {
        Gate(); var actor = Actor(); var digest = Digest(request); db.ChangeTracker.Clear();
        Require(!string.IsNullOrWhiteSpace(request.Reason) && request.Reason.Length <= 1000, "MemberIssueReasonRequired", "문제 내용을 입력해 주세요.", 400);
        Require(support is not null, "MemberSupportUnavailable", "지원 사건 접수 연결을 확인할 수 없습니다.", 503);
        await ExecuteDatabaseMutationAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await ReplayAsync(actor, request.ClientRequestId, "Issue", requestId, digest, ct))
            {
                var work = await AssignedAsync(requestId, actor, request.ExpectedRevision, ct);
                Require(work.StateCode is "Assigned" or "PickedUp", "MemberWorkStageChanged", "현재 진행 중인 업무를 확인해 주세요.");
                work.IssueCode = work.StateCode == "Assigned" ? "BeforePickupReviewRequired" : "AfterPickupRecoveryRequired";
                work.StateCode = "Issue"; work.PrivateIssuePayload = Seal(request.Reason); work.Revision++; work.UpdatedAtUtc = Now;
                db.운송이벤트.Add(new() { 의뢰Id = requestId, 이벤트타입 = "NeighborhoodMemberIssue",
                    메타데이터 = System.Text.Json.JsonSerializer.Serialize(new { ActorUserId = actor, work.IssueCode, work.Revision }) });
                Receipt(actor, request.ClientRequestId, "Issue", requestId, digest); await SaveAsync(ct);
            }
            await tx.CommitAsync(ct);
        }, ct);
        // SQL 접수 후 중단되어도 같은 ClientRequestId의 기존 지원 원장에서 회복한다. 그 전에는 배정/적재 책임을 유지한다.
        var incident = await support!.분쟁접수Async(new Ssalddel.Contracts.Common.PrivacySupport.거래분쟁접수Request
        {
            ClientRequestId = request.ClientRequestId, SourceKind = Ssalddel.Contracts.Common.PrivacySupport.보호지원출처Codes.생활배송,
            SourceId = requestId, Summary = "생활 배송 수행 문제", PrivateEvidence = request.Reason
        }, ct);
        await Deliveries.Where(x => x.RequestId == requestId && x.AssignedMemberUserId == actor && x.StateCode == "Issue")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SupportCaseId, incident.CaseId), ct);
        db.ChangeTracker.Clear();
        return (await WorkAsync(requestId, ct))!;
    }
}
