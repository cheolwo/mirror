using System.Data;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Mart;
using 살뜰.도메인.마트;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Mart;

public sealed partial class 마트수령확인UseCase
{
    public async Task<Result<마트수령문제해결Dto>> 해결조회Async(long orderId, CancellationToken ct = default)
    {
        var scope = await ResolutionScope(orderId, Now, ct);
        return scope.IsSuccess ? Result.Ok(ResolutionDto(scope.Value)) : Result.Fail<마트수령문제해결Dto>(scope.Errors);
    }

    public async Task<Result<마트수령문제해결Dto>> 해결요청결과조회Async(long orderId, Guid requestId, CancellationToken ct = default)
    {
        var scope = await ResolutionScope(orderId, Now, ct);
        if (scope.IsFailed) return Result.Fail<마트수령문제해결Dto>(scope.Errors);
        var state = scope.Value;
        if (!state.StoredEvidenceScopeValid) return ResolutionFail("MartCustomerReceiptEvidenceInvalid");
        if (requestId == Guid.Empty || !state.Envelope!.Resolutions.Any(row => row.ClientRequestId == requestId && row.ActorUserId == user.UserId))
            return ResolutionFail("MartReceiptResolutionRequestNotFound", 404);
        var dto = ResolutionDto(state); dto.IsReplay = true; dto.ResponseRequestId = requestId; return Result.Ok(dto);
    }

    public Task<Result<마트수령문제해결Dto>> 해결제안Async(long orderId, 마트수령해결제안요청 request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!마트수령문제해결Policy.OutcomeValid(request.OutcomeCode) || request.ConfirmedQuantity < 0
            || !마트수령문제해결Policy.NoteValid(request.EvidenceNote))
            return Task.FromResult(ResolutionFail("MartReceiptResolutionInputInvalid", 400));
        return Resolve(orderId, request.ClientRequestId, request.ExpectedAttemptStableId, request.ExpectedRevision,
            마트수령문제해결Policy.ProposalDigest(orderId, request), request, null, ct);
    }

    public Task<Result<마트수령문제해결Dto>> 해결동의Async(long orderId, 마트수령해결동의요청 request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ProposalId) || request.ProposalId.Length > 220 || request.ConfirmedQuantity < 0
            || !마트수령문제해결Policy.NoteValid(request.EvidenceNote))
            return Task.FromResult(ResolutionFail("MartReceiptResolutionInputInvalid", 400));
        return Resolve(orderId, request.ClientRequestId, request.ExpectedAttemptStableId, request.ExpectedRevision,
            마트수령문제해결Policy.ConsentDigest(orderId, request), null, request, ct);
    }

    private async Task<Result<마트수령문제해결Dto>> Resolve(long orderId, Guid requestId, string expectedAttempt,
        long expectedRevision, string digest, 마트수령해결제안요청? proposalInput, 마트수령해결동의요청? consentInput, CancellationToken ct)
    {
        if (!gate.Enabled) return ResolutionFail("MartCustomerReceiptWorkflowDisabled", 403);
        if (!db.Database.IsRelational()) return ResolutionFail("MartCustomerReceiptRelationalStoreRequired", 503);
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(expectedAttempt) || expectedAttempt.Length > 220
            || expectedRevision <= 0 || expectedRevision == long.MaxValue) return ResolutionFail("MartReceiptResolutionInputInvalid", 400);
        var retry = 0;
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (retry++ > 0) db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var now = Now; var scope = await ResolutionScope(orderId, now, ct);
                if (scope.IsFailed) return Result.Fail<마트수령문제해결Dto>(scope.Errors);
                var state = scope.Value;
                if (state.AttemptStableId != expectedAttempt) return ResolutionFail("MartCustomerReceiptAttemptChanged");
                var existing = state.Envelope?.Resolutions?.FirstOrDefault(row => row.ClientRequestId == requestId);
                if (existing is not null)
                {
                    if (existing.ActorUserId != user.UserId || existing.InputDigest != digest) return ResolutionFail("MartCustomerReceiptRequestConflict");
                    if (!state.StoredEvidenceScopeValid) return ResolutionFail("MartCustomerReceiptEvidenceInvalid");
                    var replay = ResolutionDto(state); replay.IsReplay = true; replay.ResponseRequestId = requestId; return Result.Ok(replay);
                }
                if (state.ConfirmedProof?.ClientRequestId == requestId || state.Hold?.ClientRequestId == requestId)
                    return ResolutionFail("MartCustomerReceiptRequestConflict");
                if (!state.StoredEvidenceValid || !state.StoredEvidenceScopeValid) return ResolutionFail("MartCustomerReceiptEvidenceInvalid");
                if (!state.CanonicalDeliveryValid) return ResolutionFail("MartCustomerDeliveryEvidenceRequired");
                if (state.Hold is null) return ResolutionFail("MartReceiptResolutionHoldRequired");
                if (state.BuyerUserId == state.SellerUserId) return ResolutionFail("MartReceiptResolutionDistinctPartiesRequired");
                if (state.PaymentReleaseReady || 마트수령문제해결Policy.HistorySealed(state)) return ResolutionFail("MartReceiptResolutionAlreadyResolved");
                if (state.AttemptRevision != expectedRevision) return ResolutionFail("MartCustomerReceiptRevisionChanged");
                var events = state.Envelope!.Resolutions;
                if (events.Count >= 마트수령문제해결Policy.MaxEvents) return ResolutionFail("MartReceiptResolutionHistoryLimit");
                var p = 마트수령문제해결Policy.LatestProposal(state);
                if (proposalInput is not null && (proposalInput.ConfirmedQuantity > state.TotalOrderQuantity
                    || proposalInput.OutcomeCode == MartReceiptResolutionCodes.FullReceiptReconfirmed && proposalInput.ConfirmedQuantity != state.TotalOrderQuantity))
                    return ResolutionFail("MartFullReceiptConfirmationRequired", 400);
                if (consentInput is not null)
                {
                    if (p is null || p.ProposalId != consentInput.ProposalId) return ResolutionFail("MartReceiptResolutionProposalChanged");
                    if (p.SourceFingerprint != state.SourceFingerprint) return ResolutionFail("MartReceiptResolutionSourceChanged");
                    if (consentInput.ConfirmedQuantity != p.ConfirmedQuantity) return ResolutionFail("MartReceiptResolutionQuantityChanged", 400);
                    if (마트수령문제해결Policy.Consents(state).Any(row => row.ActorUserId == user.UserId)) return ResolutionFail("MartReceiptResolutionConsentAlreadyRecorded");
                }
                var entry = new 마트수령문제해결근거
                {
                    Kind = proposalInput is null ? "Consent" : "Proposal",
                    ProposalId = proposalInput is null ? p!.ProposalId : $"mart-receipt-resolution:{orderId}:{requestId:N}",
                    MartOrderId = orderId, OrderReference = state.OrderReference, AttemptStableId = state.AttemptStableId,
                    HoldRequestId = state.Hold.ClientRequestId, BuyerUserId = state.BuyerUserId, SellerUserId = state.SellerUserId,
                    ActorUserId = user.UserId!, ClientRequestId = requestId, ExpectedRevision = expectedRevision, ResultRevision = expectedRevision + 1,
                    OutcomeCode = proposalInput?.OutcomeCode ?? p!.OutcomeCode, ConfirmedQuantity = proposalInput?.ConfirmedQuantity ?? p!.ConfirmedQuantity,
                    SourceFingerprint = proposalInput is null ? p!.SourceFingerprint : state.SourceFingerprint, Granted = consentInput?.Granted,
                    EvidenceNote = proposalInput?.EvidenceNote ?? consentInput!.EvidenceNote, RecordedAtUtc = now, InputDigest = digest
                };
                var envelope = state.Envelope with { Resolutions = events.Append(entry).ToArray() };
                var json = envelope.Write();
                if (json.Length > 65_536) return ResolutionFail("MartCustomerReceiptEvidenceTooLarge");
                var changed = await db.Set<마트배송시도>().Where(row => row.시도StableId == expectedAttempt && row.주문참조번호 == state.OrderReference
                        && row.Revision == expectedRevision && row.상태Code == "Delivered")
                    .ExecuteUpdateAsync(set => set.SetProperty(row => row.Revision, expectedRevision + 1)
                        .SetProperty(row => row.고객수령근거Json, json).SetProperty(row => row.UpdatedAtUtc, now), ct);
                if (changed != 1) return ResolutionFail("MartCustomerReceiptRevisionChanged");
                db.운송이벤트.Add(new 운송이벤트 { 의뢰Id = state.OrderReference, 이벤트타입 = proposalInput is null ? "MartReceiptResolutionConsentRecorded" : "MartReceiptResolutionProposed",
                    이벤트시각 = now, 메타데이터 = JsonSerializer.Serialize(new { MartOrderId = orderId, RequestId = requestId, Revision = expectedRevision + 1, entry.ProposalId }) });
                await db.SaveChangesAsync(ct);
                var updated = await 마트수령근거Reader.ReadAsync(db, orderId, now, ct);
                if (!updated.CanonicalDeliveryValid || !updated.StoredEvidenceValid || !updated.StoredEvidenceScopeValid)
                    throw new DbUpdateConcurrencyException("MartReceiptResolutionRevalidationFailed");
                await tx.CommitAsync(ct); var response = ResolutionDto(updated); response.ResponseRequestId = requestId; return Result.Ok(response);
            });
        }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return ResolutionFail("MartCustomerReceiptConcurrentChange"); }
    }

    private async Task<Result<마트수령근거Snapshot>> ResolutionScope(long orderId, DateTime now, CancellationToken ct)
    {
        if (!gate.Enabled) return Result.Fail<마트수령근거Snapshot>(Error("MartCustomerReceiptWorkflowDisabled", 403));
        if (string.IsNullOrWhiteSpace(user.UserId)) return Result.Fail<마트수령근거Snapshot>(Error("MartCustomerReceiptIdentityRequired", 401));
        // 보호 JSON을 읽기 전에 실제 구매자/판매자를 현재 SQL에서 검사합니다. 기사·관리자 권한을 추가하지 않습니다.
        if (!await db.마트주문.AsNoTracking().AnyAsync(row => row.Id == orderId && (row.주문자UserId == user.UserId || row.판매자UserId == user.UserId), ct))
            return Result.Fail<마트수령근거Snapshot>(Error("MartCustomerReceiptNotFound", 404));
        var state = await 마트수령근거Reader.ReadAsync(db, orderId, now, ct);
        return state.OrderFound && (state.BuyerUserId == user.UserId || state.SellerUserId == user.UserId)
            ? Result.Ok(state) : Result.Fail<마트수령근거Snapshot>(Error("MartCustomerReceiptNotFound", 404));
    }

    private 마트수령문제해결Dto ResolutionDto(마트수령근거Snapshot state)
    {
        var status = 마트수령문제해결Policy.Status(state);
        var proposal = state.StoredEvidenceScopeValid ? 마트수령문제해결Policy.LatestProposal(state) : null;
        var consents = state.StoredEvidenceScopeValid ? 마트수령문제해결Policy.Consents(state) : [];
        var enabled = state.Hold is not null && state.StoredEvidenceValid && state.CanonicalDeliveryValid && !state.PaymentReleaseReady
            && !마트수령문제해결Policy.HistorySealed(state)
            && state.BuyerUserId != state.SellerUserId && (state.Envelope?.Resolutions?.Count ?? 0) < 마트수령문제해결Policy.MaxEvents;
        return new() { MartOrderId = state.MartOrderId, AttemptStableId = state.AttemptStableId, Revision = state.AttemptRevision,
            MyRole = state.BuyerUserId == user.UserId ? "Buyer" : "Seller", TotalOrderQuantity = state.TotalOrderQuantity,
            HoldRecorded = state.StoredEvidenceScopeValid && state.Hold is not null, HoldReasonCode = state.StoredEvidenceScopeValid ? state.Hold?.ReasonCode : null,
            ResolutionStatus = status, NextActor = 마트수령문제해결Policy.NextActor(state),
            WaitingReasonCode = status == "ResolvedFullReceipt" ? "" : status == "OperationalReview" ? "MartReceiptResolutionOperationalReviewRequired" : state.WaitingReasonCode,
            ProposalId = proposal?.ProposalId, OutcomeCode = proposal?.OutcomeCode, ProposedQuantity = proposal?.ConfirmedQuantity,
            BuyerConsented = consents.Any(row => row.ActorUserId == state.BuyerUserId && row.Granted == true),
            SellerConsented = consents.Any(row => row.ActorUserId == state.SellerUserId && row.Granted == true),
            CanProposeResolution = enabled, CanConsentResolution = enabled && proposal is not null && proposal.SourceFingerprint == state.SourceFingerprint
                && !consents.Any(row => row.ActorUserId == user.UserId), PaymentReleaseReady = state.PaymentReleaseReady };
    }
    private static Result<마트수령문제해결Dto> ResolutionFail(string code, int status = 409)
        => Result.Fail<마트수령문제해결Dto>(Error(code, status));
}
