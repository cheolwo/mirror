using FluentResults;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

public sealed partial class 생활협업UseCase
{
    private async Task<Result<NeighborhoodCollaborationResponse>> ConfirmMemberReceiptAsync(생활협업Record record,
        NeighborhoodCollaborationCommandRequest request, string actor, string fingerprint, CancellationToken ct)
    {
        if (memberDeliveries is null) return Fail("MemberReceiptUnavailable", "회원 배송 수령 확인 연결을 확인할 수 없습니다.", 503);
        try
        {
            if (record.ReceiptConfirmationIntent is null)
            {
                var intent = await memberDeliveries.PrepareReceiptAsync(record, actor, request.ClientRequestId, fingerprint, ct);
                var expected = record.Revision;
                record.ReceiptConfirmationIntent = intent; record.Revision++; record.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
                if (!await store.교체Async(record, expected, ct))
                {
                    var concurrent = await store.조회Async(record.StableId, ct);
                    if (concurrent?.Receipts.Any(x => x.ClientRequestId == request.ClientRequestId && x.ActorUserId == actor && x.Fingerprint == fingerprint) == true)
                        return Result.Ok(ToResponse(concurrent, actor, true));
                    if (concurrent?.ReceiptConfirmationIntent is not { StateCode: "pending" } pending
                        || pending.ClientRequestId != request.ClientRequestId || pending.ActorUserId != actor || pending.Fingerprint != fingerprint)
                        return Fail("RevisionConflict", "다른 결정이 먼저 저장되었습니다. 최신 상태를 확인해 주세요.", 409);
                    record = concurrent;
                }
            }
            var confirmedAt = await memberDeliveries.ConfirmReceiptAsync(record, ct);
            await memberDeliveries.SyncReceiptLedgerAsync(record.LinkedDeliveryRequestId!, actor, ct);
            var revision = record.Revision;
            record.ReceiptConfirmationIntent!.StateCode = "confirmed"; record.ReceiptConfirmationIntent.ConfirmedAtUtc = confirmedAt;
            record.StatusCode = NeighborhoodCollaborationStates.Completed; record.CompletedAtUtc = confirmedAt;
            record.Revision++; record.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            record.Receipts.Add(new() { ClientRequestId = request.ClientRequestId, ActorUserId = actor, Fingerprint = fingerprint });
            AddHistory(record, request.Action, Role(record, actor), confirmedAt);
            if (!await store.교체Async(record, revision, ct))
            {
                var current = await store.조회Async(record.StableId, ct);
                if (current?.Receipts.Any(x => x.ClientRequestId == request.ClientRequestId && x.ActorUserId == actor && x.Fingerprint == fingerprint) == true)
                    return Result.Ok(ToResponse(current, actor, true));
                return Fail("ReceiptConfirmationPending", "수령은 기록되었고 협업 반영을 기다립니다. 같은 요청으로 확인해 주세요.", 503);
            }
            return Result.Ok(ToResponse(record, actor));
        }
        catch (NeighborhoodMemberDeliveryException ex)
        {
            var current = await store.조회Async(record.StableId, ct);
            if (current?.Receipts.Any(x => x.ClientRequestId == request.ClientRequestId && x.ActorUserId == actor && x.Fingerprint == fingerprint) == true)
                return Result.Ok(ToResponse(current, actor, true));
            var stillPending = current?.ReceiptConfirmationIntent is { StateCode: "pending" } pending
                && pending.ClientRequestId == request.ClientRequestId && pending.ActorUserId == actor && pending.Fingerprint == fingerprint;
            // CAS 고정 뒤에는 불명확한/보류 결과를 최종 거절(409)로 보내 client의 원 요청 키를 지우지 않습니다.
            return Fail(ex.Code, stillPending
                ? ex.Message + " 같은 수령 확인 요청을 유지해 주세요." : ex.Message,
                stillPending ? 503 : ex.StatusCode);
        }
    }
}
