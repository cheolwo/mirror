using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Services.Food;

public sealed class InMemorySsalddelFoodOrderStore : ISsalddelFoodOrderStore, I커뮤니티원장반영가능음식주문Store
{
    private readonly object _gate = new();
    private readonly List<음식주문응답> _orders;

    public InMemorySsalddelFoodOrderStore()
    {
        _orders = FoodOrderSampleData.CreateOrders().Select(FoodOrderSampleData.Clone).ToList();
    }

    public 음식주문목록응답 GetOrders()
    {
        lock (_gate)
        {
            return new 음식주문목록응답
            {
                Items = _orders.OrderByDescending(x => x.CreatedAt).Select(FoodOrderSampleData.Clone).ToArray()
            };
        }
    }

    public 음식주문응답? GetOrder(string orderNo)
    {
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            return null;
        }

        lock (_gate)
        {
            return _orders
                .Where(x => string.Equals(x.주문번호, orderNo, StringComparison.OrdinalIgnoreCase))
                .Select(FoodOrderSampleData.Clone)
                .FirstOrDefault();
        }
    }

    public 음식주문응답 AddOrder(음식주문등록요청 request)
        => 멱등등록(request).주문;

    public 음식주문저장결과 멱등등록(음식주문등록요청 request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            if (request.클라이언트요청Id != Guid.Empty)
            {
                var existing = _orders.FirstOrDefault(x =>
                    x.클라이언트요청Id == request.클라이언트요청Id
                    && string.Equals(x.주문자UserId, request.주문자UserId, StringComparison.Ordinal));
                if (existing is not null)
                {
                    return new 음식주문저장결과(FoodOrderSampleData.Clone(existing), false);
                }
            }
        }

        var now = DateTime.UtcNow;
        var order = new 음식주문응답
        {
            주문번호 = $"FOOD-{now:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString("N")[..6]}",
            클라이언트요청Id = request.클라이언트요청Id == Guid.Empty
                ? null
                : request.클라이언트요청Id,
            음식점Id = request.음식점Id,
            주문자UserId = request.주문자UserId.Trim(),
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = request.수령인정보.수령인명,
                연락처 = request.수령인정보.연락처,
                주소 = request.수령인정보.주소,
                상세주소 = request.수령인정보.상세주소,
                요청사항 = request.수령인정보.요청사항,
                주문자본인수령여부 = request.수령인정보.주문자본인수령여부
            },
            상품목록 = request.상품목록.Select(x => new 음식주문상품Dto
            {
                메뉴Id = x.메뉴Id,
                상품명 = x.상품명,
                수량 = x.수량,
                단가 = x.단가
            }).ToArray(),
            총주문금액 = request.상품목록.Sum(x => x.단가 * x.수량),
            상태 = 음식주문상태코드.주문대기,
            배차상태 = 음식주문배차상태코드.미요청,
            결제수단 = request.결제수단,
            CreatedAt = now,
            최근변경시각Utc = now,
            상태이력 =
            [
                new 음식주문상태전이기록Dto
                {
                    이전상태 = string.Empty,
                    다음상태 = 음식주문상태코드.주문대기,
                    사유 = "주문 등록",
                    전이시각Utc = now
                }
            ]
        };

        lock (_gate)
        {
            _orders.Add(order);
        }

        return new 음식주문저장결과(FoodOrderSampleData.Clone(order), true);
    }

    public 음식주문응답? 음식점수락(string orderNo, 음식점주문수락요청 request)
        => 음식점수락멱등(
            orderNo,
            request,
            NormalizeOptional(request.처리UserId) ?? "restaurant:legacy")?.주문;

    public 음식주문변경결과? 음식점수락멱등(
        string orderNo,
        음식점주문수락요청 request,
        string 처리UserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var order = _orders.FirstOrDefault(x => string.Equals(x.주문번호, orderNo, StringComparison.OrdinalIgnoreCase));
            if (order is null)
            {
                return null;
            }

            var duplicate = FindDuplicate(order, request.클라이언트요청Id);
            if (duplicate)
            {
                return new 음식주문변경결과(FoodOrderSampleData.Clone(order), false);
            }

            var currentStatus = 음식주문상태코드.Normalize(order.상태);
            if (!음식주문상태코드.CanRestaurantAccept(currentStatus))
            {
                throw new InvalidOperationException($"음식점 수락이 가능한 주문 상태가 아닙니다. 현재상태={order.상태}");
            }

            var now = DateTime.UtcNow;
            var nextStatus = 음식주문상태코드.주문확인;
            음식배달업무상태전이Guard.허용확인(currentStatus, nextStatus);
            var cookingMinutes = request.즉시픽업가능여부
                ? 0
                : Math.Clamp(request.조리예상분 ?? 15, 1, 180);

            order.상태 = nextStatus;
            order.음식점명 = NormalizeOptional(request.음식점명) ?? order.음식점명;
            order.음식점주소 = NormalizeOptional(request.음식점주소) ?? order.음식점주소;
            order.음식점상세주소 = NormalizeOptional(request.음식점상세주소) ?? order.음식점상세주소;
            order.음식점위도 = request.음식점위도 ?? order.음식점위도;
            order.음식점경도 = request.음식점경도 ?? order.음식점경도;
            order.음식점수락시각Utc = now;
            order.조리예상분 = cookingMinutes;
            order.조리예상완료시각Utc = request.즉시픽업가능여부 ? now : null;
            order.픽업준비시각Utc = request.즉시픽업가능여부 ? now : null;
            order.수락메모 = NormalizeOptional(request.수락메모);
            order.최근변경시각Utc = now;
            order.상태이력 = AppendHistory(
                order,
                currentStatus,
                nextStatus,
                request.즉시픽업가능여부 ? "음식점 주문 확인 · 기존 준비 완료" : "음식점 주문 확인 · 배차 후 조리",
                now,
                request.클라이언트요청Id,
                처리UserId);

            return new 음식주문변경결과(FoodOrderSampleData.Clone(order), true);
        }
    }

    public 음식주문변경결과? 음식점진행변경(
        string orderNo,
        음식점주문진행변경요청 request,
        string 처리UserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var order = _orders.FirstOrDefault(x =>
                string.Equals(x.주문번호, orderNo, StringComparison.OrdinalIgnoreCase));
            if (order is null)
            {
                return null;
            }

            if (FindDuplicate(order, request.클라이언트요청Id))
            {
                return new 음식주문변경결과(FoodOrderSampleData.Clone(order), false);
            }

            var currentStatus = 음식주문상태코드.Normalize(order.상태);
            if (request.예상Revision.HasValue && request.예상Revision.Value != order.상태이력.Count)
                throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("음식 주문이 다른 요청에서 먼저 변경되었습니다.");
            // 샘플 저장소에는 실제 기사 배차 원장이 없으므로 조리 시작을 허용하지 않습니다.
            var started = order.조리시작시각Utc.HasValue
                || order.상태이력.Any(x => x.사유 == "음식점 주문 수락" && x.다음상태 == 음식주문상태코드.조리중);
            var decision = 음식점주문진행Policy.판정(currentStatus, request,
                배차확정: false, 조리시작됨: started || order.픽업준비시각Utc.HasValue,
                계획조리분: order.조리예상분);
            var now = DateTime.UtcNow;
            order.상태 = decision.다음상태;
            if (decision.조리예상분 is { } cookingMinutes)
            {
                order.조리예상분 = cookingMinutes;
                if (started || request.작업.Trim() == 음식점주문진행작업코드.픽업준비)
                    order.조리예상완료시각Utc = now.AddMinutes(cookingMinutes);
            }

            order.최근변경시각Utc = now;
            order.상태이력 = AppendHistory(
                order,
                currentStatus,
                decision.다음상태,
                decision.이력사유,
                now,
                request.클라이언트요청Id,
                처리UserId);

            return new 음식주문변경결과(FoodOrderSampleData.Clone(order), true);
        }
    }

    public 음식주문변경결과? 주문자수령확인(
        string orderNo,
        주문자음식주문수령확인요청 request,
        string 주문자UserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var order = _orders.FirstOrDefault(x =>
                string.Equals(x.주문번호, orderNo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.주문자UserId, 주문자UserId, StringComparison.Ordinal));
            if (order is null)
            {
                return null;
            }

            if (FindDuplicate(order, request.클라이언트요청Id)
                || 음식주문상태코드.Normalize(order.상태) == 음식주문상태코드.수령확인)
            {
                return new 음식주문변경결과(FoodOrderSampleData.Clone(order), false);
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
            order.최근변경시각Utc = now;
            order.상태이력 = AppendHistory(
                order,
                currentStatus,
                음식주문상태코드.수령확인,
                BuildReceiptConfirmationReason(request.확인메모),
                now,
                request.클라이언트요청Id,
                주문자UserId);

            return new 음식주문변경결과(FoodOrderSampleData.Clone(order), true);
        }
    }

    public 음식주문변경결과? 주문자취소(
        string orderNo,
        주문자음식주문취소요청 request,
        string 주문자UserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var order = _orders.FirstOrDefault(x =>
                string.Equals(x.주문번호, orderNo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.주문자UserId, 주문자UserId, StringComparison.Ordinal));
            if (order is null)
            {
                return null;
            }

            if (FindDuplicate(order, request.클라이언트요청Id))
            {
                return new 음식주문변경결과(FoodOrderSampleData.Clone(order), false);
            }

            var currentStatus = 음식주문상태코드.Normalize(order.상태);
            if (currentStatus != 음식주문상태코드.주문대기)
            {
                throw new InvalidOperationException(
                    $"음식점 수락 전 주문대기 상태에서만 주문자가 직접 취소할 수 있습니다. 현재상태={order.상태}");
            }
            if (request.예상Revision.HasValue && request.예상Revision.Value != order.상태이력.Count)
            {
                throw new InvalidOperationException("음식 주문이 다른 요청에서 먼저 변경되었습니다.");
            }

            var now = DateTime.UtcNow;
            음식배달업무상태전이Guard.허용확인(currentStatus, 음식주문상태코드.취소);
            order.상태 = 음식주문상태코드.취소;
            order.최근변경시각Utc = now;
            order.상태이력 = AppendHistory(
                order,
                currentStatus,
                음식주문상태코드.취소,
                BuildOrdererCancellationReason(request),
                now,
                request.클라이언트요청Id,
                주문자UserId);

            return new 음식주문변경결과(FoodOrderSampleData.Clone(order), true);
        }
    }

    public 음식주문응답? 배차대기반영(string orderNo, long dispatchWaitId, DateTime dispatchRequestedAtUtc)
    {
        lock (_gate)
        {
            var order = _orders.FirstOrDefault(x => string.Equals(x.주문번호, orderNo, StringComparison.OrdinalIgnoreCase));
            if (order is null)
            {
                return null;
            }

            order.배차상태 = 음식주문배차상태코드.배차대기;
            order.배차대기Id = dispatchWaitId;
            order.배차요청시각Utc = dispatchRequestedAtUtc;
            order.최근변경시각Utc = DateTime.UtcNow;

            return FoodOrderSampleData.Clone(order);
        }
    }

    public 음식주문응답? 커뮤니티원장반영(
        string orderNo,
        string ledgerId,
        string ledgerTemplateKey,
        string ledgerState,
        DateTime syncedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            return null;
        }

        lock (_gate)
        {
            var order = _orders.FirstOrDefault(x => string.Equals(x.주문번호, orderNo, StringComparison.OrdinalIgnoreCase));
            if (order is null)
            {
                return null;
            }

            order.커뮤니티원장Id = ledgerId;
            order.커뮤니티원장템플릿Key = ledgerTemplateKey;
            order.커뮤니티원장상태 = ledgerState;
            order.커뮤니티원장동기화시각Utc = syncedAtUtc;
            order.최근변경시각Utc = DateTime.UtcNow;

            return FoodOrderSampleData.Clone(order);
        }
    }

    private static IReadOnlyList<음식주문상태전이기록Dto> AppendHistory(
        음식주문응답 order,
        string previousStatus,
        string nextStatus,
        string reason,
        DateTime changedAtUtc,
        Guid clientRequestId = default,
        string? actorUserId = null)
    {
        return order.상태이력
            .Concat(
            [
                new 음식주문상태전이기록Dto
                {
                    클라이언트요청Id = clientRequestId == Guid.Empty ? null : clientRequestId,
                    처리UserId = NormalizeOptional(actorUserId),
                    이전상태 = previousStatus,
                    다음상태 = nextStatus,
                    사유 = reason,
                    전이시각Utc = changedAtUtc
                }
            ])
            .ToArray();
    }

    private static bool FindDuplicate(음식주문응답 order, Guid clientRequestId)
        => clientRequestId != Guid.Empty
           && order.상태이력.Any(history => history.클라이언트요청Id == clientRequestId);

    private static string BuildReceiptConfirmationReason(string? note)
        => NormalizeOptional(note) is { } cleanNote
            ? $"주문자 수령 확인 · {cleanNote}"
            : "주문자 수령 확인";

    private static string BuildOrdererCancellationReason(주문자음식주문취소요청 request)
        => NormalizeOptional(request.사유) is { } cleanReason
            ? $"주문자 취소 · {request.사유Code.Trim()} · {cleanReason}"
            : $"주문자 취소 · {request.사유Code.Trim()}";

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
