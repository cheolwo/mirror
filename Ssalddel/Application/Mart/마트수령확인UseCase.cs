using System.Data;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Mart;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.도메인.마트;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Mart;

public interface I마트수령확인UseCase
{
    Task<Result<마트수령확인Dto>> 조회Async(long orderId, CancellationToken ct = default);
    Task<Result<마트수령확인Dto>> 확인Async(long orderId, 마트수령확인요청 request, CancellationToken ct = default);
    Task<Result<마트수령확인Dto>> 문제신고Async(long orderId, 마트수령문제신고요청 request, CancellationToken ct = default);
    Task<Result<마트수령확인Dto>> 요청결과조회Async(long orderId, Guid requestId, CancellationToken ct = default);
    Task<Result<마트수령문제해결Dto>> 해결조회Async(long orderId, CancellationToken ct = default);
    Task<Result<마트수령문제해결Dto>> 해결제안Async(long orderId, 마트수령해결제안요청 request, CancellationToken ct = default);
    Task<Result<마트수령문제해결Dto>> 해결동의Async(long orderId, 마트수령해결동의요청 request, CancellationToken ct = default);
    Task<Result<마트수령문제해결Dto>> 해결요청결과조회Async(long orderId, Guid requestId, CancellationToken ct = default);
}

/// <summary>인증된 실제 구매자의 명시적 수령 또는 보류만 저장합니다. 업무·정산·입금 사실을 대신 확정하지 않습니다.</summary>
public sealed partial class 마트수령확인UseCase(SsalddelContext db, ICurrentUserAccessor user,
    I마트기사업무Gate gate, TimeProvider? timeProvider = null) : I마트수령확인UseCase
{
    private DateTime Now => (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public async Task<Result<마트수령확인Dto>> 조회Async(long orderId, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(orderId, Now, ct);
        return scope.IsSuccess ? Result.Ok(ToDto(scope.Value)) : Result.Fail<마트수령확인Dto>(scope.Errors);
    }

    public async Task<Result<마트수령확인Dto>> 요청결과조회Async(long orderId, Guid requestId, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(orderId, Now, ct);
        if (scope.IsFailed) return Result.Fail<마트수령확인Dto>(scope.Errors);
        var state = scope.Value;
        if (!state.StoredEvidenceValid && !state.StoredEvidenceScopeValid) return Fail("MartCustomerReceiptEvidenceInvalid");
        if (requestId == Guid.Empty || state.ConfirmedProof?.ClientRequestId != requestId && state.Hold?.ClientRequestId != requestId)
            return Fail("MartCustomerReceiptRequestNotFound", 404);
        if (!state.StoredEvidenceScopeValid) return Fail("MartCustomerReceiptEvidenceInvalid");
        var dto = ToDto(state); dto.IsReplay = true; return Result.Ok(dto);
    }

    public Task<Result<마트수령확인Dto>> 확인Async(long orderId, 마트수령확인요청 request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var digest = 마트고객수령근거Envelope.ConfirmDigest(orderId, request.ClientRequestId, request.ExpectedAttemptStableId, request.ExpectedRevision, request.FullReceiptConfirmed);
        return MutateAsync(orderId, request.ClientRequestId, request.ExpectedAttemptStableId, request.ExpectedRevision, digest, null, request.FullReceiptConfirmed, ct);
    }

    public Task<Result<마트수령확인Dto>> 문제신고Async(long orderId, 마트수령문제신고요청 request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!마트수령근거Reader.ValidReason(request.ReasonCode)) return Task.FromResult(Fail("MartReceiptIssueReasonInvalid", 400));
        var digest = 마트고객수령근거Envelope.IssueDigest(orderId, request.ClientRequestId, request.ExpectedAttemptStableId, request.ExpectedRevision, request.ReasonCode);
        return MutateAsync(orderId, request.ClientRequestId, request.ExpectedAttemptStableId, request.ExpectedRevision, digest, request.ReasonCode, true, ct);
    }

    private async Task<Result<마트수령확인Dto>> MutateAsync(long orderId, Guid requestId, string expectedAttempt,
        long expectedRevision, string digest, string? issue, bool fullReceipt, CancellationToken ct)
    {
        if (!gate.Enabled) return Fail("MartCustomerReceiptWorkflowDisabled", 403);
        if (!db.Database.IsRelational()) return Fail("MartCustomerReceiptRelationalStoreRequired", 503);
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(expectedAttempt) || expectedAttempt.Length > 220
            || expectedRevision <= 0 || expectedRevision == long.MaxValue) return Fail("MartCustomerReceiptRequestInvalid", 400);
        var retry = 0;
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (retry++ > 0) db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var now = Now;
                var scope = await ScopeAsync(orderId, now, ct);
                if (scope.IsFailed) return Result.Fail<마트수령확인Dto>(scope.Errors);
                var state = scope.Value;
                if (state.AttemptStableId != expectedAttempt) return Fail("MartCustomerReceiptAttemptChanged");
                var proof = state.ConfirmedProof; var hold = state.Hold;
                if (proof?.ClientRequestId == requestId || hold?.ClientRequestId == requestId)
                {
                    var same = issue is null ? proof?.ClientRequestId == requestId && proof.InputDigest == digest
                        : hold?.ClientRequestId == requestId && hold.InputDigest == digest;
                    if (!same) return Fail("MartCustomerReceiptRequestConflict");
                    if (!state.StoredEvidenceScopeValid) return Fail("MartCustomerReceiptEvidenceInvalid");
                    var replay = ToDto(state); replay.IsReplay = true; return Result.Ok(replay);
                }
                if (!state.StoredEvidenceValid) return Fail("MartCustomerReceiptEvidenceInvalid");
                if (!state.CanonicalDeliveryValid) return Fail("MartCustomerDeliveryEvidenceRequired");
                if (issue is null && !fullReceipt) return Fail("MartFullReceiptConfirmationRequired", 400);
                if (hold is not null) return Fail("MartCustomerReceiptHeld");
                if (issue is null && proof is not null) return Fail("MartCustomerReceiptAlreadyConfirmed");
                if (state.AttemptRevision != expectedRevision) return Fail("MartCustomerReceiptRevisionChanged");
                var envelope = state.Envelope ?? new 마트고객수령근거Envelope();
                if (issue is null)
                    envelope = envelope with { ConfirmedProof = new()
                    {
                        ReceiptId = $"mart-customer-receipt:{orderId}:{requestId:N}", MartOrderId = orderId,
                        OrderReference = state.OrderReference, BuyerUserId = state.BuyerUserId, AttemptStableId = state.AttemptStableId,
                        DeliveredRevision = expectedRevision, ConfirmedRevision = expectedRevision + 1, DeliveredAtUtc = state.DeliveredAtUtc,
                        ConfirmedAtUtc = now, LabelId = state.LabelId, LabelRevision = state.LabelRevision, PickedUpAtUtc = state.PickedUpAtUtc,
                        ConfirmedQuantity = state.TotalOrderQuantity, SourceFingerprint = state.SourceFingerprint, Lines = state.Lines,
                        ClientRequestId = requestId, InputDigest = digest
                    } };
                else
                    envelope = envelope with { Hold = new()
                    {
                        MartOrderId = orderId, OrderReference = state.OrderReference, BuyerUserId = state.BuyerUserId,
                        AttemptStableId = state.AttemptStableId, ExpectedRevision = expectedRevision, ResultRevision = expectedRevision + 1,
                        ReasonCode = issue, RecordedAtUtc = now, ClientRequestId = requestId, InputDigest = digest
                    } };
                var json = envelope.Write();
                if (json.Length > 65_536) return Fail("MartCustomerReceiptEvidenceTooLarge");
                var changed = await db.Set<마트배송시도>().Where(row => row.시도StableId == state.AttemptStableId
                        && row.주문참조번호 == state.OrderReference && row.Revision == expectedRevision && row.상태Code == "Delivered")
                    .ExecuteUpdateAsync(set => set.SetProperty(row => row.Revision, expectedRevision + 1)
                        .SetProperty(row => row.고객수령근거Json, json).SetProperty(row => row.UpdatedAtUtc, now), ct);
                if (changed != 1) return Fail("MartCustomerReceiptRevisionChanged");
                db.운송이벤트.Add(new 운송이벤트 { 의뢰Id = state.OrderReference,
                    이벤트타입 = issue is null ? "MartCustomerReceiptConfirmed" : "MartCustomerReceiptIssueReported", 이벤트시각 = now,
                    메타데이터 = JsonSerializer.Serialize(new { MartOrderId = orderId, RequestId = requestId, Revision = expectedRevision + 1, ReasonCode = issue }) });
                await db.SaveChangesAsync(ct);
                var updated = await 마트수령근거Reader.ReadAsync(db, orderId, now, ct);
                if (!updated.StoredEvidenceValid || !updated.CanonicalDeliveryValid) throw new DbUpdateConcurrencyException("MartCustomerReceiptRevalidationFailed");
                await transaction.CommitAsync(ct);
                return Result.Ok(ToDto(updated));
            });
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Fail("MartCustomerReceiptConcurrentChange");
        }
    }

    private async Task<Result<마트수령근거Snapshot>> ScopeAsync(long orderId, DateTime now, CancellationToken ct)
    {
        if (!gate.Enabled) return Result.Fail<마트수령근거Snapshot>(Error("MartCustomerReceiptWorkflowDisabled", 403));
        if (string.IsNullOrWhiteSpace(user.UserId)) return Result.Fail<마트수령근거Snapshot>(Error("MartCustomerReceiptIdentityRequired", 401));
        // 타인 원장의 보호 근거를 읽거나 복호화하기 전에 현재 SQL 구매자 권한부터 확인합니다.
        if (!await db.마트주문.AsNoTracking().AnyAsync(row => row.Id == orderId && row.주문자UserId == user.UserId, ct))
            return Result.Fail<마트수령근거Snapshot>(Error("MartCustomerReceiptNotFound", 404));
        var state = await 마트수령근거Reader.ReadAsync(db, orderId, now, ct);
        return !state.OrderFound || state.BuyerUserId != user.UserId
            ? Result.Fail<마트수령근거Snapshot>(Error("MartCustomerReceiptNotFound", 404)) : Result.Ok(state);
    }

    private static 마트수령확인Dto ToDto(마트수령근거Snapshot state) => new()
    {
        MartOrderId = state.MartOrderId, OrderReference = state.OrderReference, AttemptStableId = state.AttemptStableId,
        Revision = state.AttemptRevision, CanConfirmReceipt = state.CanonicalDeliveryValid && state.StoredEvidenceValid && state.Envelope is null,
        CanReportIssue = state.CanonicalDeliveryValid && state.StoredEvidenceValid && state.Hold is null,
        ReceiptConfirmed = state.ConfirmedProofValid, PaymentReleaseReady = state.PaymentReleaseReady,
        ReceiptRecorded = state.StoredEvidenceScopeValid && state.ConfirmedProof is not null,
        HoldRecorded = state.StoredEvidenceScopeValid && state.Hold is not null,
        ReceiptId = state.ConfirmedProofValid ? state.ConfirmedProof!.ReceiptId : null,
        ReceiptRevision = state.ConfirmedProofValid ? state.ConfirmedProof!.ConfirmedRevision : null,
        ConfirmedQuantity = state.ConfirmedProofValid ? state.ConfirmedProof!.ConfirmedQuantity : null,
        ConfirmedAtUtc = state.ConfirmedProofValid ? state.ConfirmedProof!.ConfirmedAtUtc : null,
        HoldReasonCode = state.StoredEvidenceValid ? state.Hold?.ReasonCode : null,
        HeldAtUtc = state.StoredEvidenceValid ? state.Hold?.RecordedAtUtc : null, WaitingReasonCode = state.WaitingReasonCode
    };

    private static Error Error(string code, int status = 409)
        => new Error(status == 404 ? "주문을 찾을 수 없거나 조회 권한이 없습니다." : "현재 수령 상태를 다시 확인해 주세요.")
            .WithMetadata("StatusCode", status).WithMetadata("ErrorCode", code);
    private static Result<마트수령확인Dto> Fail(string code, int status = 409) => Result.Fail<마트수령확인Dto>(Error(code, status));
}
