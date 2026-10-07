using System.Data;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Services.Community;

public interface I생활배송배차선택Service
{
    Task<Result> 선택Async(string requestId, NeighborhoodDispatchChoiceRequest request, CancellationToken ct);
    Task<Result> 협업대기취소Async(생활협업Record work, CancellationToken ct);
}

/// <summary>기사 수락과 같은 SQL 큐를 갱신합니다. 외부 기사 취소나 이미 인수한 물건의 반환을 대신하지 않습니다.</summary>
public sealed class 생활배송배차선택Service(SsalddelContext db, ICurrentUserAccessor currentUser) : I생활배송배차선택Service
{
    private const string EventType = "NeighborhoodDispatchChoice";
    public async Task<Result> 선택Async(string requestId, NeighborhoodDispatchChoiceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) return Fail("AuthenticationRequired", "로그인이 필요합니다.", 401);
        if (request.ClientRequestId == Guid.Empty || request.ExpectedDispatchRevision < 1 || !NeighborhoodDispatchModes.IsKnown(request.DispatchMode))
            return Fail("InvalidDispatchChoice", "배차 방식과 현재 판본을 확인해 주세요.", 400);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var source = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        if (source?.주문자UserId != currentUser.UserId || source.클라이언트요청Id?.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix, StringComparison.Ordinal) != true)
            return Fail("RequestNotFound", "본인 배송 의뢰를 찾을 수 없습니다.", 404);
        var signature = JsonSerializer.Serialize(request);
        var history = await db.운송이벤트.AsNoTracking().Where(x => x.의뢰Id == requestId && x.이벤트타입 == EventType).Select(x => x.메타데이터).ToListAsync(ct);
        foreach (var raw in history)
        {
            var old = JsonSerializer.Deserialize<ChoiceReceipt>(raw)!;
            if (old.ClientRequestId == request.ClientRequestId) return old.Actor == currentUser.UserId && old.Signature == signature ? Result.Ok()
                : Fail("IdempotencyConflict", "같은 요청 번호로 다른 선택을 보낼 수 없습니다.");
        }
        var queue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        if (queue is null || queue.생활배송배차방식 is null || !queue.생활배송수락준비완료
            || queue.상태 != 상태값.배차대기상태.대기 || queue.확정기사Id is not null || queue.배차큐단계 == 상태값.배차큐단계.종료
            || source.상태 != 상태값.의뢰상태.생성됨) return Fail("DispatchChoiceLocked", "기사 확정 또는 처리 중인 배송은 현재 절차를 먼저 확인해 주세요.");
        if (queue.생활배송선택판본 != request.ExpectedDispatchRevision) return Fail("DispatchRevisionConflict", "배차 방식이 바뀌었습니다. 최신 상태를 다시 확인해 주세요.");
        queue.생활배송배차방식 = request.DispatchMode; queue.생활배송선택판본++; queue.추천라운드++;
        생활배송배차Policy.대기적용(queue);
        db.운송이벤트.Add(new() { 의뢰Id = requestId, 이벤트타입 = EventType,
            메타데이터 = JsonSerializer.Serialize(new ChoiceReceipt(request.ClientRequestId, currentUser.UserId!, signature)) });
        try { await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return Result.Ok(); }
        catch (DbUpdateConcurrencyException) { return Fail("DispatchRevisionConflict", "다른 수락 또는 변경이 먼저 저장되었습니다."); }
    }
    public async Task<Result> 협업대기취소Async(생활협업Record work, CancellationToken ct)
    {
        if (currentUser.UserId != work.OwnerUserId && currentUser.UserId != work.RequesterUserId)
            return Fail("CollaborationNotFound", "본인의 협업이 아닙니다.", 404);
        // pending request may not have reached SQL yet. It can only ever create a fenced, non-accepting queue.
        var clientId = work.DeliveryIntent is { } intent ? NeighborhoodDeliveryRoutes.ClientRequestPrefix + intent.ClientRequestId.ToString("N") : null;
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var sourceQuery = db.화주운송의뢰.AsQueryable();
        if (work.LinkedDeliveryRequestId is not null) sourceQuery = sourceQuery.Where(x => x.의뢰Id == work.LinkedDeliveryRequestId);
        else if (work.DeliveryIntent is { } pendingIntent) sourceQuery = sourceQuery.Where(x => x.클라이언트요청Id == clientId && x.주문자UserId == pendingIntent.ActorUserId);
        else return Result.Ok();
        var source = await sourceQuery.SingleOrDefaultAsync(ct);
        if (source is null) return work.LinkedDeliveryRequestId is null ? Result.Ok() : Fail("DeliveryAuthorityUnavailable", "배송 원장을 확인할 수 없습니다.");
        if (source.주문자UserId != work.OwnerUserId && source.주문자UserId != work.RequesterUserId) return Fail("DeliveryLinkNotAllowed", "연결된 의뢰 권한을 확인해 주세요.");
        var queue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == source.의뢰Id, ct);
        if (source.상태 == 상태값.의뢰상태.취소 && (queue is null || queue.배차큐단계 == 상태값.배차큐단계.종료)) return Result.Ok();
        if (queue?.확정기사Id is not null || source.배차상태 is not (상태값.배차상태.미시작 or 상태값.배차상태.매칭중))
            return Fail("DeliveryRecoveryRequired", "기사가 확정된 배송입니다. 배송 상세의 취소·재배차·물건 인수 상태를 먼저 확인해 주세요.");
        if (queue is not null)
        {
            queue.생활배송수락준비완료 = false; queue.생활배송선택판본++; queue.추천라운드++;
            queue.배차큐단계 = 상태값.배차큐단계.종료; queue.현재추천대상기사Id = null;
            queue.추천시작시각 = null; queue.추천만료시각 = null; queue.UpdatedAt = DateTime.UtcNow;
        }
        source.상태 = 상태값.의뢰상태.취소; source.배차상태 = 상태값.배차상태.취소; source.UpdatedAt = DateTime.UtcNow;
        db.운송이벤트.Add(new() { 의뢰Id = source.의뢰Id, 이벤트타입 = "NeighborhoodDeliveryWaitingCancelled", 메타데이터 = JsonSerializer.Serialize(new { work.StableId, work.TermsRevision }) });
        try { await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return Result.Ok(); }
        catch (DbUpdateConcurrencyException) { return Fail("DispatchRevisionConflict", "기사 수락과 변경이 겹쳤습니다. 배송 상태를 다시 확인해 주세요."); }
    }
    private sealed record ChoiceReceipt(Guid ClientRequestId, string Actor, string Signature);
    private static Result Fail(string code, string text, int status = 409) => Result.Fail(new Error(text).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status));
}
