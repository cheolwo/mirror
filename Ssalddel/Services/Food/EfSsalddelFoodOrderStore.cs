using System.Text.Json;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.설정;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.Food;

public sealed class EfSsalddelFoodOrderStore : ISsalddelFoodOrderStore, I커뮤니티원장반영가능음식주문Store
{
    private readonly SsalddelContext _db;

    public EfSsalddelFoodOrderStore(SsalddelContext db)
    {
        _db = db;
    }

    public 음식주문목록응답 GetOrders()
        => new()
        {
            Items = _db.음식주문
                .AsNoTracking()
                .Include(x => x.상품목록)
                .Include(x => x.상태이력)
                .OrderByDescending(x => x.CreatedAt)
                .Take(200)
                .AsEnumerable()
                .Select(ToDto)
                .ToArray()
        };

    public 음식주문응답? GetOrder(string orderNo)
    {
        var cleanOrderNo = Clean(orderNo);
        if (cleanOrderNo is null)
        {
            return null;
        }

        var order = _db.음식주문
            .AsNoTracking()
            .Include(x => x.상품목록)
            .Include(x => x.상태이력)
            .FirstOrDefault(x => x.주문번호 == cleanOrderNo);

        return order is null ? null : ToDto(order);
    }

    public 음식주문응답 AddOrder(음식주문등록요청 request)
        => 멱등등록(request).주문;

    public 음식주문저장결과 멱등등록(음식주문등록요청 request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = FindByClientRequest(request);
        if (existing is not null)
        {
            return new 음식주문저장결과(ToDto(existing), false);
        }

        var now = DateTime.UtcNow;
        var orderNo = GenerateOrderNo(now);
        var order = new 음식주문
        {
            주문번호 = orderNo,
            클라이언트요청Id = request.클라이언트요청Id == Guid.Empty
                ? null
                : request.클라이언트요청Id,
            음식점Id = request.음식점Id,
            주문자UserId = Clean(request.주문자UserId) ?? string.Empty,
            수령인명 = Clean(request.수령인정보.수령인명) ?? string.Empty,
            수령인연락처 = Clean(request.수령인정보.연락처) ?? string.Empty,
            수령지주소 = Clean(request.수령인정보.주소) ?? string.Empty,
            수령지상세주소 = Clean(request.수령인정보.상세주소) ?? string.Empty,
            수령요청사항 = Clean(request.수령인정보.요청사항) ?? string.Empty,
            주문자본인수령여부 = request.수령인정보.주문자본인수령여부,
            총주문금액 = request.상품목록.Sum(x => x.단가 * x.수량),
            상태 = 음식주문상태코드.주문대기,
            배차상태 = 음식주문배차상태코드.미요청,
            결제수단 = Clean(request.결제수단),
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var item in request.상품목록)
        {
            order.상품목록.Add(new 음식주문상품
            {
                메뉴Id = item.메뉴Id,
                상품명 = Clean(item.상품명) ?? string.Empty,
                수량 = item.수량,
                단가 = item.단가,
                CreatedAt = now
            });
        }

        order.상태이력.Add(new 음식주문상태이력
        {
            이전상태 = string.Empty,
            다음상태 = 음식주문상태코드.주문대기,
            사유 = "주문 등록",
            전이시각Utc = now
        });

        _db.음식주문.Add(order);
        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException) when (request.클라이언트요청Id != Guid.Empty)
        {
            existing = FindByClientRequest(request);
            if (existing is not null)
            {
                return new 음식주문저장결과(ToDto(existing), false);
            }

            throw;
        }

        return new 음식주문저장결과(ToDto(order), true);
    }

    public 음식주문응답? 음식점수락(string orderNo, 음식점주문수락요청 request)
        => 음식점수락멱등(
            orderNo,
            request,
            Clean(request.처리UserId) ?? "restaurant:legacy")?.주문;

    public 음식주문변경결과? 음식점수락멱등(
        string orderNo,
        음식점주문수락요청 request,
        string 처리UserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cleanOrderNo = Clean(orderNo);
        if (cleanOrderNo is null)
        {
            return null;
        }

        var order = LoadOrderForUpdate(cleanOrderNo);
        if (order is null)
        {
            return null;
        }

        if (FindDuplicate(order, request.클라이언트요청Id))
        {
            return new 음식주문변경결과(ToDto(order), false);
        }

        var currentStatus = 음식주문상태코드.Normalize(order.상태);
        if (!음식주문상태코드.CanRestaurantAccept(currentStatus))
        {
            throw new InvalidOperationException($"음식점 수락이 가능한 주문 상태가 아닙니다. 현재상태={order.상태}");
        }

        var now = DateTime.UtcNow;
        var nextStatus = request.즉시픽업가능여부
            ? 음식주문상태코드.픽업대기
            : 음식주문상태코드.조리중;
        음식배달업무상태전이Guard.허용확인(currentStatus, nextStatus);
        var cookingMinutes = request.즉시픽업가능여부
            ? 0
            : Math.Clamp(request.조리예상분 ?? 15, 1, 180);

        order.상태 = nextStatus;
        order.음식점명 = Clean(request.음식점명) ?? order.음식점명;
        order.음식점주소 = Clean(request.음식점주소) ?? order.음식점주소;
        order.음식점상세주소 = Clean(request.음식점상세주소) ?? order.음식점상세주소;
        order.음식점위도 = request.음식점위도 ?? order.음식점위도;
        order.음식점경도 = request.음식점경도 ?? order.음식점경도;
        order.음식점수락시각Utc = now;
        order.조리예상완료시각Utc = now.AddMinutes(cookingMinutes);
        order.수락메모 = Clean(request.수락메모);
        order.UpdatedAt = now;
        order.상태이력.Add(new 음식주문상태이력
        {
            클라이언트요청Id = request.클라이언트요청Id == Guid.Empty
                ? null
                : request.클라이언트요청Id,
            처리UserId = Clean(처리UserId),
            이전상태 = currentStatus,
            다음상태 = nextStatus,
            사유 = "음식점 주문 수락",
            전이시각Utc = now
        });

        return SaveIdempotentChange(
            order,
            cleanOrderNo,
            request.클라이언트요청Id);
    }

    public 음식주문변경결과? 음식점진행변경(
        string orderNo,
        음식점주문진행변경요청 request,
        string 처리UserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cleanOrderNo = Clean(orderNo);
        if (cleanOrderNo is null)
        {
            return null;
        }

        var order = LoadOrderForUpdate(cleanOrderNo);
        if (order is null)
        {
            return null;
        }

        if (FindDuplicate(order, request.클라이언트요청Id))
        {
            return new 음식주문변경결과(ToDto(order), false);
        }

        var currentStatus = 음식주문상태코드.Normalize(order.상태);
        if (request.예상Revision.HasValue && request.예상Revision.Value != order.상태이력.Count)
        {
            throw new DbUpdateConcurrencyException("음식 주문이 다른 요청에서 먼저 변경되었습니다.");
        }
        var decision = 음식점주문진행Policy.판정(currentStatus, request);
        var now = DateTime.UtcNow;
        order.상태 = decision.다음상태;
        if (decision.조리예상분 is { } cookingMinutes)
        {
            order.조리예상완료시각Utc = now.AddMinutes(cookingMinutes);
        }

        order.UpdatedAt = now;
        order.상태이력.Add(new 음식주문상태이력
        {
            클라이언트요청Id = request.클라이언트요청Id,
            처리UserId = Clean(처리UserId),
            이전상태 = currentStatus,
            다음상태 = decision.다음상태,
            사유 = decision.이력사유,
            전이시각Utc = now
        });

        return SaveIdempotentChange(
            order,
            cleanOrderNo,
            request.클라이언트요청Id);
    }

    public 음식주문변경결과? 주문자수령확인(
        string orderNo,
        주문자음식주문수령확인요청 request,
        string 주문자UserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cleanOrderNo = Clean(orderNo);
        var cleanOrdererUserId = Clean(주문자UserId);
        if (cleanOrderNo is null || cleanOrdererUserId is null)
        {
            return null;
        }

        var order = LoadOrderForUpdate(cleanOrderNo);
        if (order is null
            || !string.Equals(order.주문자UserId, cleanOrdererUserId, StringComparison.Ordinal))
        {
            return null;
        }

        if (FindDuplicate(order, request.클라이언트요청Id)
            || 음식주문상태코드.Normalize(order.상태) == 음식주문상태코드.수령확인)
        {
            return new 음식주문변경결과(ToDto(order), false);
        }

        var currentStatus = 음식주문상태코드.Normalize(order.상태);
        if (currentStatus != 음식주문상태코드.전달완료)
        {
            throw new InvalidOperationException(
                $"기사 전달 완료 상태에서만 수령을 확인할 수 있습니다. 현재상태={order.상태}");
        }

        var now = DateTime.UtcNow;
        음식배달업무상태전이Guard.허용확인(currentStatus, 음식주문상태코드.수령확인);
        order.상태 = 음식주문상태코드.수령확인;
        order.UpdatedAt = now;
        order.상태이력.Add(new 음식주문상태이력
        {
            클라이언트요청Id = request.클라이언트요청Id,
            처리UserId = cleanOrdererUserId,
            이전상태 = currentStatus,
            다음상태 = 음식주문상태코드.수령확인,
            사유 = BuildReceiptConfirmationReason(request.확인메모),
            전이시각Utc = now
        });

        var completedRevision = order.상태이력.Count;
        _db.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
        {
            멱등키 = $"food-delivery-completed-world:{cleanOrderNo}:{completedRevision}",
            동기화유형 = 음식마트원장동기화유형코드.음식배달완료WorldProjection,
            원천Id = cleanOrderNo,
            변경자 = "FoodDeliveryOS",
            PayloadJson = JsonSerializer.Serialize(new { orderRevision = completedRevision }),
            처리상태 = Ssalddel.Services.Outbox.OutboxProcessingStatuses.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        return SaveIdempotentChange(
            order,
            cleanOrderNo,
            request.클라이언트요청Id);
    }

    public 음식주문변경결과? 주문자취소(
        string orderNo,
        주문자음식주문취소요청 request,
        string 주문자UserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cleanOrderNo = Clean(orderNo);
        var cleanOrdererUserId = Clean(주문자UserId);
        if (cleanOrderNo is null || cleanOrdererUserId is null)
        {
            return null;
        }

        var order = LoadOrderForUpdate(cleanOrderNo);
        if (order is null
            || !string.Equals(order.주문자UserId, cleanOrdererUserId, StringComparison.Ordinal))
        {
            return null;
        }

        if (FindDuplicate(order, request.클라이언트요청Id))
        {
            return new 음식주문변경결과(ToDto(order), false);
        }

        var currentStatus = 음식주문상태코드.Normalize(order.상태);
        if (currentStatus != 음식주문상태코드.주문대기)
        {
            throw new InvalidOperationException(
                $"음식점 수락 전 주문대기 상태에서만 주문자가 직접 취소할 수 있습니다. 현재상태={order.상태}");
        }
        if (request.예상Revision.HasValue && request.예상Revision.Value != order.상태이력.Count)
        {
            throw new DbUpdateConcurrencyException("음식 주문이 다른 요청에서 먼저 변경되었습니다.");
        }

        var now = DateTime.UtcNow;
        음식배달업무상태전이Guard.허용확인(currentStatus, 음식주문상태코드.취소);
        order.상태 = 음식주문상태코드.취소;
        order.UpdatedAt = now;
        order.상태이력.Add(new 음식주문상태이력
        {
            클라이언트요청Id = request.클라이언트요청Id,
            처리UserId = cleanOrdererUserId,
            이전상태 = currentStatus,
            다음상태 = 음식주문상태코드.취소,
            사유 = BuildOrdererCancellationReason(request),
            전이시각Utc = now
        });

        return SaveIdempotentChange(
            order,
            cleanOrderNo,
            request.클라이언트요청Id);
    }

    public 음식주문응답? 배차대기반영(string orderNo, long dispatchWaitId, DateTime dispatchRequestedAtUtc)
    {
        var cleanOrderNo = Clean(orderNo);
        if (cleanOrderNo is null)
        {
            return null;
        }

        var order = LoadOrderForUpdate(cleanOrderNo);
        if (order is null)
        {
            return null;
        }

        order.배차상태 = 음식주문배차상태코드.배차대기;
        order.배차대기Id = dispatchWaitId;
        order.배차요청시각Utc = dispatchRequestedAtUtc;
        order.UpdatedAt = DateTime.UtcNow;

        _db.SaveChanges();
        return ToDto(order);
    }

    public 음식주문응답? 커뮤니티원장반영(
        string orderNo,
        string ledgerId,
        string ledgerTemplateKey,
        string ledgerState,
        DateTime syncedAtUtc)
    {
        var cleanOrderNo = Clean(orderNo);
        if (cleanOrderNo is null)
        {
            return null;
        }

        var order = LoadOrderForUpdate(cleanOrderNo);
        if (order is null)
        {
            return null;
        }

        order.커뮤니티원장Id = Clean(ledgerId);
        order.커뮤니티원장템플릿Key = Clean(ledgerTemplateKey);
        order.커뮤니티원장상태 = Clean(ledgerState);
        order.커뮤니티원장동기화시각Utc = syncedAtUtc;
        order.UpdatedAt = DateTime.UtcNow;

        _db.SaveChanges();
        return ToDto(order);
    }

    private 음식주문? LoadOrderForUpdate(string orderNo)
        => _db.음식주문
            .Include(x => x.상품목록)
            .Include(x => x.상태이력)
            .FirstOrDefault(x => x.주문번호 == orderNo);

    private 음식주문? FindByClientRequest(음식주문등록요청 request)
        => request.클라이언트요청Id == Guid.Empty
            ? null
            : _db.음식주문
                .AsNoTracking()
                .Include(x => x.상품목록)
                .Include(x => x.상태이력)
                .FirstOrDefault(x =>
                    x.주문자UserId == request.주문자UserId
                    && x.클라이언트요청Id == request.클라이언트요청Id);

    private static bool FindDuplicate(음식주문 order, Guid clientRequestId)
        => clientRequestId != Guid.Empty
           && order.상태이력.Any(history => history.클라이언트요청Id == clientRequestId);

    private static string BuildReceiptConfirmationReason(string? note)
        => Clean(note) is { } cleanNote
            ? $"주문자 수령 확인 · {cleanNote}"
            : "주문자 수령 확인";

    private static string BuildOrdererCancellationReason(주문자음식주문취소요청 request)
        => Clean(request.사유) is { } cleanReason
            ? $"주문자 취소 · {request.사유Code.Trim()} · {cleanReason}"
            : $"주문자 취소 · {request.사유Code.Trim()}";

    private 음식주문변경결과 SaveIdempotentChange(
        음식주문 order,
        string orderNo,
        Guid clientRequestId)
    {
        try
        {
            _db.SaveChanges();
            return new 음식주문변경결과(ToDto(order), true);
        }
        catch (DbUpdateException) when (clientRequestId != Guid.Empty)
        {
            _db.ChangeTracker.Clear();
            var existing = LoadOrderForUpdate(orderNo);
            if (existing is not null && FindDuplicate(existing, clientRequestId))
            {
                return new 음식주문변경결과(ToDto(existing), false);
            }

            throw;
        }
    }

    private string GenerateOrderNo(DateTime now)
    {
        var prefix = $"FOOD-{now:yyyyMMddHHmmssfff}";
        if (!_db.음식주문.Any(x => x.주문번호 == prefix))
        {
            return prefix;
        }

        for (var index = 1; index <= 99; index++)
        {
            var candidate = $"{prefix}-{index:00}";
            if (!_db.음식주문.Any(x => x.주문번호 == candidate))
            {
                return candidate;
            }
        }

        return $"FOOD-{Guid.NewGuid():N}";
    }

    private static 음식주문응답 ToDto(음식주문 order)
        => new()
        {
            주문번호 = order.주문번호,
            클라이언트요청Id = order.클라이언트요청Id,
            음식점Id = order.음식점Id,
            음식점명 = order.음식점명,
            음식점주소 = order.음식점주소,
            음식점상세주소 = order.음식점상세주소,
            음식점위도 = order.음식점위도,
            음식점경도 = order.음식점경도,
            주문자UserId = order.주문자UserId,
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = order.수령인명,
                연락처 = order.수령인연락처,
                주소 = order.수령지주소,
                상세주소 = order.수령지상세주소,
                요청사항 = order.수령요청사항,
                주문자본인수령여부 = order.주문자본인수령여부
            },
            상품목록 = order.상품목록
                .OrderBy(x => x.Id)
                .Select(x => new 음식주문상품Dto
                {
                    메뉴Id = x.메뉴Id,
                    상품명 = x.상품명,
                    수량 = x.수량,
                    단가 = x.단가
                })
                .ToArray(),
            총주문금액 = order.총주문금액,
            상태 = order.상태,
            배차상태 = order.배차상태,
            배차대기Id = order.배차대기Id,
            결제수단 = order.결제수단,
            음식점수락시각Utc = order.음식점수락시각Utc,
            조리예상완료시각Utc = order.조리예상완료시각Utc,
            픽업준비시각Utc = order.상태이력
                .Where(x => x.다음상태 == 음식주문상태코드.픽업대기
                            || x.사유.StartsWith("음식점 픽업 준비 완료", StringComparison.Ordinal))
                .OrderBy(x => x.전이시각Utc)
                .Select(x => (DateTime?)x.전이시각Utc)
                .FirstOrDefault(),
            배차요청시각Utc = order.배차요청시각Utc,
            수락메모 = order.수락메모,
            커뮤니티원장Id = order.커뮤니티원장Id,
            커뮤니티원장템플릿Key = order.커뮤니티원장템플릿Key,
            커뮤니티원장상태 = order.커뮤니티원장상태,
            커뮤니티원장동기화시각Utc = order.커뮤니티원장동기화시각Utc,
            CreatedAt = order.CreatedAt,
            최근변경시각Utc = order.UpdatedAt,
            Revision = order.상태이력.Count,
            상태이력 = order.상태이력
                .OrderBy(x => x.전이시각Utc)
                .Select(x => new 음식주문상태전이기록Dto
                {
                    클라이언트요청Id = x.클라이언트요청Id,
                    처리UserId = x.처리UserId,
                    이전상태 = x.이전상태,
                    다음상태 = x.다음상태,
                    사유 = x.사유,
                    전이시각Utc = x.전이시각Utc
                })
                .ToArray()
        };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
