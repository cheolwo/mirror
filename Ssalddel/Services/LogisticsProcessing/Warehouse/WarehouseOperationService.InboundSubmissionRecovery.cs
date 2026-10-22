using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Inbound;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

public sealed partial class WarehouseOperationService
{
    /// <summary>본인이 실제 전송한 일반 입고 원요청만 확인한다. 결과 없음은 미적용 확정이 아니다.</summary>
    public async Task<입고요청항목응답?> GetInboundByClientRequestIdAsync(
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        if (clientRequestId == Guid.Empty) return null;
        var entity = await 일반입고원요청조회Async(RequireUserId(), clientRequestId, cancellationToken);
        return entity is null ? null : await GetInboundAsync(entity.Id, cancellationToken);
    }

    private async Task<입고요청항목응답> 원요청입고저장Async(
        입고요청 entity,
        입고요청저장요청 request,
        CancellationToken cancellationToken)
    {
        var requestId = request.ClientRequestId!.Value;
        var fingerprint = InboundSubmissionIdentity.Compute(entity, request);
        var existing = await 일반입고원요청조회Async(entity.주문자UserId, requestId, cancellationToken);
        if (existing is not null)
            return await 동일입고원요청결과Async(existing, fingerprint, cancellationToken);

        가입고사진신청? photoRequest = null;
        if (request.가입고사진신청Id is { } photoId)
        {
            photoRequest = await _db.Set<가입고사진신청>().Include(x => x.입고요청)
                .SingleOrDefaultAsync(x => x.Id == photoId && x.작성자UserId == entity.주문자UserId, cancellationToken);
            if (photoRequest is null || !string.Equals(photoRequest.작성자UserId, entity.주문자UserId, StringComparison.Ordinal))
                throw new InboundSubmissionConflictException();
            if (photoRequest.입고요청 is not null)
            {
                // 최초 조회 직후 같은 원요청이 연결됐을 수 있다. 동일 지문이면 현재 정본으로 복귀한다.
                var concurrentlyLinked = await 일반입고원요청조회Async(entity.주문자UserId, requestId, cancellationToken);
                if (concurrentlyLinked is not null)
                    return await 동일입고원요청결과Async(concurrentlyLinked, fingerprint, cancellationToken);
                throw new InboundSubmissionConflictException();
            }
            // 이 개정과 입고 FK는 하나의 SaveChanges 트랜잭션으로 함께 저장한다.
            photoRequest.Revision++;
            photoRequest.UpdatedAtUtc = DateTime.UtcNow;
        }

        entity.일반입고클라이언트요청Id = requestId;
        entity.일반입고최초요청해시 = fingerprint;
        _db.입고요청.Add(entity);
        var progress = await 창고업무진행알림.입고신청Async(_db, entity, entity.주문자UserId, cancellationToken);
        try
        {
            // 요청 식별자와 최초 입력 지문은 업무 원장과 함께 한 번에 저장한다.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            창고업무진행알림.분리(_db, progress);
            _db.Entry(entity).State = EntityState.Detached;
            if (photoRequest is not null) _db.Entry(photoRequest).State = EntityState.Detached;
            // 다른 요청이 유일키를 먼저 저장했는지 확인한다. 다른 저장 실패는 성공으로 숨기지 않는다.
            var concurrent = await 일반입고원요청조회Async(entity.주문자UserId, requestId, cancellationToken);
            if (concurrent is null) throw;
            return await 동일입고원요청결과Async(concurrent, fingerprint, cancellationToken);
        }
        catch { 창고업무진행알림.분리(_db, progress); throw; }

        return ToInboundResponse(entity);
    }

    private async Task<입고요청?> 일반입고원요청조회Async(
        string userId,
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        var item = await 접근가능입고Query(userId).AsNoTracking().SingleOrDefaultAsync(
            inbound => inbound.주문자UserId == userId
                && inbound.일반입고클라이언트요청Id == clientRequestId,
            cancellationToken);
        // DB의 대소문자 무시 정렬 규칙을 작성자 인증의 동일성으로 사용하지 않는다.
        return item is not null && string.Equals(item.주문자UserId, userId, StringComparison.Ordinal) ? item : null;
    }

    private async Task<입고요청항목응답> 동일입고원요청결과Async(
        입고요청 existing,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        // 이후 수정/완료된 내용 대신 최초 생성 때의 지문과 비교한다.
        if (!string.Equals(existing.일반입고최초요청해시, fingerprint, StringComparison.Ordinal))
            throw new InboundSubmissionConflictException();

        var response = await GetInboundAsync(existing.Id, cancellationToken)
            ?? throw new InvalidOperationException("입고 신청 결과를 확인할 수 없습니다.");
        response.멱등재시도여부 = true;
        return response;
    }
}
