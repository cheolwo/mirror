using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Admin.Food;

public interface I음식주문지원후속UseCase
{
    Task<FoodSupportFollowUpContext?> 조회Async(string orderNo, string caseId, Guid? requestId = null, CancellationToken ct = default);
    Task<FoodSupportFollowUpResponse?> 처리Async(string orderNo, FoodSupportFollowUpRequest request, CancellationToken ct = default);
}

/// <summary>통지한 지원 결과를 음식 전문 업무에 결속합니다. 주문 취소·수령·환불·지급을 대신 확정하지 않습니다.</summary>
public sealed class 음식주문지원후속UseCase(SsalddelContext db, 보호지원UseCase support,
    ICurrentUserAccessor actor, I배차대기원장전환Service transition,
    ILogger<음식주문지원후속UseCase> logger) : I음식주문지원후속UseCase
{
    private const string Prefix = "F76|";

    public async Task<FoodSupportFollowUpContext?> 조회Async(string orderNo, string caseId, Guid? requestId = null, CancellationToken ct = default)
    {
        ValidateSource(orderNo, caseId);
        var source = await support.상세Async(caseId, admin: true, ct: ct); // 기존 관리자 권한과 현재 사건을 재검증한다.
        RequireSource(source, orderNo);
        var order = await db.음식주문.AsNoTracking().Include(x => x.상태이력).SingleOrDefaultAsync(x => x.주문번호 == orderNo, ct);
        if (order is null) return null;
        var queue = await QueueAsync(order, ct);
        var ready = ResultReady(source);
        var receipt = requestId.HasValue ? order.상태이력.SingleOrDefault(x => x.클라이언트요청Id == requestId) : null;
        if (receipt is not null && receipt.처리UserId != actor.UserId)
            throw new InvalidOperationException("다른 운영자의 요청 결과는 이 요청 번호로 확인할 수 없습니다.");
        var last = order.상태이력.Where(x => x.사유.StartsWith(Prefix, StringComparison.Ordinal))
            .OrderByDescending(x => x.전이시각Utc).ThenByDescending(x => x.Id)
            .FirstOrDefault(x => TryRead(x, out var prior) && prior.CaseId == caseId);
        return new()
        {
            OrderNo = orderNo, SupportCaseId = caseId, CaseRevision = source.Revision,
            OrderRevision = order.상태이력.Count, OrderStatus = order.상태,
            ResultNoticeVerified = ready,
            CanRetryDispatch = ready && RetryAllowed(order, queue),
            CanRecordOperatorReview = ready,
            BlockReason = ready ? RetryAllowed(order, queue) ? string.Empty : "현재 배차·수행 상태에서는 재탐색을 요청할 수 없습니다. 취소·미수령·재배송 및 비용은 운영 검토로 인계하세요."
                : "최신 처리방안을 모든 당사자에게 안내한 근거가 필요합니다.",
            Receipt = receipt is not null && TryRead(receipt, out var saved) && saved.CaseId == caseId
                ? Response(order, receipt, saved, true) : null,
            LastFollowUp = last is not null && TryRead(last, out var lastSaved) ? Response(order, last, lastSaved, true) : null
        };
    }

    public async Task<FoodSupportFollowUpResponse?> 처리Async(string orderNo, FoodSupportFollowUpRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSource(orderNo, request.SupportCaseId);
        if (request.ClientRequestId == Guid.Empty || request.ExpectedCaseRevision < 1 || request.ExpectedOrderRevision < 1
            || request.ActionCode is not (FoodSupportFollowUpActions.RetryDispatch or FoodSupportFollowUpActions.OperatorReview))
            throw new ArgumentException("요청 번호·사건/주문 판본·음식 후속 처리를 확인해 주세요.");
        var source = await support.상세Async(request.SupportCaseId, admin: true, ct: ct);
        RequireSource(source, orderNo);
        var actorId = actor.UserId;
        if (string.IsNullOrWhiteSpace(actorId)) throw new InvalidOperationException("운영자 식별자를 확인할 수 없습니다.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { orderNo, request, actorId })))).ToLowerInvariant();
        var reason = $"{Prefix}{request.ActionCode}|{request.SupportCaseId}|{request.ExpectedCaseRevision.ToString(CultureInfo.InvariantCulture)}|{fingerprint}";
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var order = await db.음식주문.Include(x => x.상태이력).SingleOrDefaultAsync(x => x.주문번호 == orderNo, ct);
            if (order is null) return (Response: (FoodSupportFollowUpResponse?)null, QueueId: (string?)null);
            var previous = order.상태이력.SingleOrDefault(x => x.클라이언트요청Id == request.ClientRequestId);
            if (previous is not null)
            {
                if (previous.처리UserId != actorId || previous.사유 != reason || !TryRead(previous, out var prior))
                    throw new InvalidOperationException("같은 요청 번호로 다른 음식 후속 처리를 보낼 수 없습니다.");
                await tx.RollbackAsync(ct);
                return (Response: (FoodSupportFollowUpResponse?)Response(order, previous, prior, true), QueueId: (string?)null);
            }
            // Mongo 사건과 SQL 주문의 원자적 전이를 주장하지 않는다. SQL 기록 직전 사건 판본·통지를 다시 확인한다.
            var currentCase = await support.상세Async(request.SupportCaseId, admin: true, ct: ct);
            RequireSource(currentCase, orderNo);
            if (currentCase.Revision != request.ExpectedCaseRevision || order.상태이력.Count != request.ExpectedOrderRevision)
                throw new DbUpdateConcurrencyException("사건 또는 주문이 변경되었습니다. 최신 상태를 확인해 주세요.");
            if (!ResultReady(currentCase)) throw new InvalidOperationException("최신 처리방안을 모든 당사자에게 안내한 뒤 전문 업무를 인계할 수 있습니다.");
            var queue = await QueueAsync(order, ct);
            if (request.ActionCode == FoodSupportFollowUpActions.RetryDispatch && !RetryAllowed(order, queue))
                throw new InvalidOperationException("현재 수행이나 유효한 제안은 변경할 수 없습니다. 음식 배차대기 상태를 확인해 주세요.");
            var now = DateTime.UtcNow;
            var entry = new 음식주문상태이력 { 클라이언트요청Id = request.ClientRequestId, 처리UserId = actorId,
                이전상태 = order.상태, 다음상태 = order.상태, 사유 = reason, 전이시각Utc = now };
            order.상태이력.Add(entry);
            order.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            TryRead(entry, out var evidence);
            return (Response: (FoodSupportFollowUpResponse?)Response(order, entry, evidence, false),
                QueueId: request.ActionCode == FoodSupportFollowUpActions.RetryDispatch ? queue!.의뢰Id : null);
        });
        if (result.QueueId is { } id)
        {
            // 이미 영속 대기열인 같은 요청을 재탐색한다. 실패해도 수행/입금 완료로 기록하지 않는다.
            try { await transition.추천대기처리Async(id, ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            { logger.LogWarning("음식 지원 후속 재탐색은 영속 대기열에서 계속합니다. FailureType={FailureType}", ex.GetType().Name); }
        }
        return result.Response;
    }

    private Task<운송원장?> QueueAsync(음식주문 order, CancellationToken ct)
        => order.배차대기Id is { } id ? db.운송원장.SingleOrDefaultAsync(x => x.Id == id, ct) : Task.FromResult<운송원장?>(null);
    private bool RetryAllowed(음식주문 order, 운송원장? queue)
        => order.상태 is 음식주문상태코드.주문확인 or 음식주문상태코드.조리중 or 음식주문상태코드.픽업대기
            && 음식수행권위Policy.내부허용(db, order)
            && queue is not null && queue.원본의뢰Id == order.주문번호
            && 운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형)
            && queue.배차업무유형 == 상태값.배차업무유형.음식배달
            && queue.상태 == 상태값.배차대기상태.대기 && queue.배차큐단계 == 상태값.배차큐단계.배차추천
            && queue.배차노출상태 is 상태값.배차노출상태.추천대기 or 상태값.배차노출상태.추천후보없음
            && string.IsNullOrWhiteSpace(queue.확정기사Id) && string.IsNullOrWhiteSpace(queue.현재추천대상기사Id);
    private static bool ResultReady(보호지원CaseResponse row)
        => row.StatusCode is "result-prepared" or "closed" && !row.RetentionPending
            && row.PreparedResult is not null && row.ResultNotifiedAtUtc.HasValue;
    private static void ValidateSource(string orderNo, string caseId)
    {
        if (string.IsNullOrWhiteSpace(orderNo) || orderNo.Length > 100 || orderNo != orderNo.Trim()
            || string.IsNullOrWhiteSpace(caseId) || caseId.Length > 80 || caseId.Contains('|') || caseId != caseId.Trim())
            throw new ArgumentException("주문과 지원 사건 식별자를 확인해 주세요.");
    }
    private static void RequireSource(보호지원CaseResponse row, string orderNo)
    {
        if (row.Kind != 보호지원종류Codes.분쟁 || row.SourceKind != 보호지원출처Codes.음식주문 || row.SourceId != orderNo)
            throw new InvalidOperationException("해당 음식 주문에 연결된 거래 지원 사건만 처리할 수 있습니다.");
    }
    private sealed record Evidence(string Action, string CaseId, long CaseRevision);
    private static bool TryRead(음식주문상태이력 row, out Evidence evidence)
    {
        var parts = row.사유.Split('|');
        evidence = new("", "", 0);
        if (parts.Length != 5 || parts[0] != "F76" || !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var revision)) return false;
        evidence = new(parts[1], parts[2], revision); return true;
    }
    private static FoodSupportFollowUpResponse Response(음식주문 order, 음식주문상태이력 entry, Evidence evidence, bool replay)
        => new() { ClientRequestId = entry.클라이언트요청Id!.Value, OrderNo = order.주문번호,
            SupportCaseId = evidence.CaseId, SupportCaseRevision = evidence.CaseRevision,
            OrderRevision = order.상태이력.Count(x => x.전이시각Utc < entry.전이시각Utc || x.전이시각Utc == entry.전이시각Utc && x.Id <= entry.Id),
            ActionCode = evidence.Action, WorkStatusCode = evidence.Action == FoodSupportFollowUpActions.RetryDispatch ? "DispatchRetryRequested" : "AgreementReviewPending",
            Message = evidence.Action == FoodSupportFollowUpActions.RetryDispatch
                ? "기존 배차대기 건의 적격 기사 재탐색 요청을 기록했습니다. 대기열 작업이 활성화되면 재탐색하며 실제 배정은 주문 운영 추적에서 확인하세요."
                : "음식점·주문자와 취소·미수령·재배송·추가 비용을 협의하도록 운영 검토에 인계했습니다. 주문 취소·수령·환불·지급은 별도 확인합니다.", Replay = replay };
}
