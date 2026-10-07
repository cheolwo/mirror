using System.Text.Json;
using FluentResults;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

public interface I생활배송협업Guard
{
    Task<Result> 획득Async(NeighborhoodDeliveryRequest request, string actor, string fingerprint, CancellationToken ct);
    Task<Result> 연결확정Async(NeighborhoodDeliveryRequest request, string actor, string requestId, CancellationToken ct);
}

/// <summary>보호된 Mongo CAS로 조건 판본을 고정한 뒤 SQL 배송을 만듭니다. SQL 큐는 연결 완료 전 수락 불가입니다.</summary>
public sealed class 생활배송협업Guard(I생활협업Store store, TimeProvider clock) : I생활배송협업Guard
{
    public async Task<Result> 획득Async(NeighborhoodDeliveryRequest request, string actor, string fingerprint, CancellationToken ct)
    {
        if (request.CollaborationId is null) return Result.Ok();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var work = await store.조회Async(request.CollaborationId, ct);
            if (work is null || (work.OwnerUserId != actor && work.RequesterUserId != actor)) return Fail("CollaborationNotFound");
            var prior = work.DeliveryIntent;
            if (prior?.ClientRequestId == request.ClientRequestId)
                return prior.ActorUserId == actor && prior.Fingerprint == fingerprint && prior.StateCode is "pending" or "confirmed"
                    ? Result.Ok() : Fail("IdempotencyConflict");
            if (prior?.StateCode is "pending" or "confirmed" || work.LinkedDeliveryRequestId is not null) return Fail("DeliveryAlreadyLinked");
            if (work.Kind != NeighborhoodCollaborationKinds.Goods || work.Terms.TransferMethod != NeighborhoodTransferMethods.DriverDelivery
                || !생활협업UseCase.CurrentAgreement(work) || work.TermsRevision != request.ExpectedTermsRevision
                || work.SourcePostId != request.SourcePostId || work.Terms.Quantity != request.Quantity || work.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime
                || work.StatusCode is not (NeighborhoodCollaborationStates.Agreed or NeighborhoodCollaborationStates.InProgress)) return Fail("AgreementRequired");
            var rev = work.Revision;
            work.DeliveryIntent = new() { ClientRequestId = request.ClientRequestId, ActorUserId = actor, TermsRevision = work.TermsRevision,
                Fingerprint = fingerprint, Request = JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(request))! };
            work.Revision++; work.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            if (await store.교체Async(work, rev, ct)) return Result.Ok();
        }
        return Fail("RevisionConflict");
    }
    public async Task<Result> 연결확정Async(NeighborhoodDeliveryRequest request, string actor, string requestId, CancellationToken ct)
    {
        if (request.CollaborationId is null) return Result.Ok();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var work = await store.조회Async(request.CollaborationId, ct);
            var intent = work?.DeliveryIntent;
            if (work is null || intent?.ClientRequestId != request.ClientRequestId || intent.ActorUserId != actor
                || intent.TermsRevision != work.TermsRevision || intent.StateCode is not ("pending" or "confirmed")) return Fail("DeliveryIntentCancelled");
            if (intent.StateCode == "confirmed") return work.LinkedDeliveryRequestId == requestId ? Result.Ok() : Fail("DeliveryAlreadyLinked");
            var rev = work.Revision; work.LinkedDeliveryRequestId = requestId; intent.StateCode = "confirmed";
            work.Revision++; work.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            work.History.Add(new() { Revision = work.Revision, Action = "link-delivery", StatusCode = work.StatusCode,
                ActorRoleCode = actor == work.OwnerUserId ? "owner" : "requester", RecordedAtUtc = work.UpdatedAtUtc });
            if (await store.교체Async(work, rev, ct)) return Result.Ok();
        }
        return Fail("RevisionConflict");
    }
    private static Result Fail(string code) => Result.Fail(new Error("현재 배송 합의와 원 요청 결과를 다시 확인해 주세요.")
        .WithMetadata("ErrorCode", code).WithMetadata("StatusCode", 409));
}
