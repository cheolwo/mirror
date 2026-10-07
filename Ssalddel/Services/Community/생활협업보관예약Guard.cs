using System.Text.Json;
using FluentResults;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

/// <summary>
/// 두 Mongo 문서 간 예약 intent를 먼저 협업 CAS에 기록합니다. 공간 CAS가 완료/쓰기 fence된 뒤에만
/// 이 서버 전용 포트로 확정·해제하므로 지연된 예약 쓰기와 협업 취소/조건 변경이 교차하지 않습니다.
/// lease 시각만으로 자동 해제하지 않으며 운영 저장 실패를 성공으로 바꾸지 않습니다.
/// </summary>
public sealed class 생활협업보관예약Guard(I생활협업Store store, TimeProvider clock) : I생활협업보관예약Guard
{
    public async Task<Result<생활협업예약IntentRecord>> 획득Async(string collaborationId, long termsRevision, string spaceId,
        string actorUserId, Guid requestId, long expectedSpaceRevision, string fingerprint, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collaborationId) || string.IsNullOrWhiteSpace(spaceId) || string.IsNullOrWhiteSpace(actorUserId)
            || requestId == Guid.Empty || expectedSpaceRevision < 1 || fingerprint.Length is < 16 or > 200)
            return Fail("InvalidReservationIntent", "현재 예약 요청을 확인해 주세요.");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var record = await store.조회Async(collaborationId, cancellationToken);
            if (record is null || (record.OwnerUserId != actorUserId && record.RequesterUserId != actorUserId))
                return Fail("CollaborationNotFound", "본인이 참여한 보관 합의를 찾을 수 없습니다.");
            var existing = record.StorageReservationIntents.SingleOrDefault(x => x.RequestId == requestId && x.ActorUserId == actorUserId);
            if (existing is not null)
                return existing.TermsRevision != termsRevision || existing.SpaceId != spaceId || existing.ExpectedSpaceRevision != expectedSpaceRevision || existing.Fingerprint != fingerprint
                    ? Fail("IdempotencyConflict", "같은 예약 번호로 다른 조건을 사용할 수 없습니다.") : Result.Ok(Copy(existing));
            if (record.StorageReservationIntents.Any(x => x.StateCode is "pending" or "confirmed"))
                return Fail("StorageReservationPending", "기존 보관 예약 결과를 먼저 확인해 주세요.");
            if (record.CreateRequestId == requestId || record.Receipts.Any(x => x.ClientRequestId == requestId))
                return Fail("IdempotencyConflict", "협업 결정 번호를 보관 예약에 다시 사용할 수 없습니다.");
            var now = clock.GetUtcNow().UtcDateTime;
            if (record.Kind != NeighborhoodCollaborationKinds.Storage || record.Terms.StorageSpaceId != spaceId
                || record.TermsRevision != termsRevision || !생활협업UseCase.CurrentAgreement(record)
                || record.StatusCode is not (NeighborhoodCollaborationStates.Agreed or NeighborhoodCollaborationStates.InProgress)
                || record.StatusCode == NeighborhoodCollaborationStates.Agreed && record.ExpiresAtUtc <= now)
                return Fail("AgreementRequired", "양측의 현재 보관 조건과 인계 정보 제공 동의를 확인해 주세요.");
            if (record.StorageReservationIntents.Count >= 100) return Fail("ReservationIntentLimit", "새 협업으로 보관을 신청해 주세요.");
            var intent = new 생활협업예약IntentRecord
            {
                RequestId = requestId, ActorUserId = actorUserId, SpaceId = spaceId, TermsRevision = termsRevision,
                ExpectedSpaceRevision = expectedSpaceRevision, Fingerprint = fingerprint, RecordedAtUtc = now, UpdatedAtUtc = now
            };
            var revision = record.Revision; record.Revision++; record.UpdatedAtUtc = now;
            record.StorageReservationIntents.Add(intent); History(record, "storage-reservation-intent", actorUserId, now);
            if (await store.교체Async(record, revision, cancellationToken)) return Result.Ok(Copy(intent));
        }
        return Fail("RevisionConflict", "상대방의 변경 뒤 현재 합의를 다시 확인해 주세요.");
    }

    public Task<Result<생활협업예약IntentRecord>> 확정Async(string collaborationId, Guid requestId, string actorUserId, CancellationToken cancellationToken = default)
        => ChangeAsync(collaborationId, requestId, actorUserId, "confirmed", cancellationToken);
    public Task<Result<생활협업예약IntentRecord>> 해제Async(string collaborationId, Guid requestId, string actorUserId, CancellationToken cancellationToken = default)
        => ChangeAsync(collaborationId, requestId, actorUserId, "released", cancellationToken);
    public async Task<생활협업예약IntentRecord?> 조회Async(string collaborationId, Guid requestId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var record = await store.조회Async(collaborationId, cancellationToken);
        if (record is null || (record.OwnerUserId != actorUserId && record.RequesterUserId != actorUserId)) return null;
        var intent = record.StorageReservationIntents.SingleOrDefault(x => x.RequestId == requestId && x.ActorUserId == actorUserId);
        return intent is null ? null : Copy(intent);
    }

    private async Task<Result<생활협업예약IntentRecord>> ChangeAsync(string collaborationId, Guid requestId, string actorUserId, string state, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var record = await store.조회Async(collaborationId, cancellationToken);
            if (record is null) return Fail("CollaborationNotFound", "보관 합의를 찾을 수 없습니다.");
            var intent = record.StorageReservationIntents.SingleOrDefault(x => x.RequestId == requestId && x.ActorUserId == actorUserId);
            if (intent is null) return Fail("ReservationIntentNotFound", "저장한 보관 예약 요청을 찾을 수 없습니다.");
            if (intent.StateCode == state) return Result.Ok(Copy(intent));
            if (state == "confirmed" && intent.StateCode != "pending") return Fail("ReservationIntentReleased", "해제한 예약 요청으로 다시 예약할 수 없습니다.");
            var revision = record.Revision; var now = clock.GetUtcNow().UtcDateTime;
            intent.StateCode = state; intent.UpdatedAtUtc = now; record.Revision++; record.UpdatedAtUtc = now;
            History(record, "storage-reservation-" + state, actorUserId, now);
            if (await store.교체Async(record, revision, cancellationToken)) return Result.Ok(Copy(intent));
        }
        return Fail("RevisionConflict", "보관 예약 결과를 다시 확인해 주세요.");
    }
    private static 생활협업예약IntentRecord Copy(생활협업예약IntentRecord record) => JsonSerializer.Deserialize<생활협업예약IntentRecord>(JsonSerializer.Serialize(record))!;
    private static Result<생활협업예약IntentRecord> Fail(string code, string message)
        => Result.Fail<생활협업예약IntentRecord>(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", 409));
    private static void History(생활협업Record record, string action, string actor, DateTime now) => record.History.Add(new()
    { Revision = record.Revision, Action = action, StatusCode = record.StatusCode, ActorRoleCode = actor == record.OwnerUserId ? "owner" : "requester", RecordedAtUtc = now });
}
