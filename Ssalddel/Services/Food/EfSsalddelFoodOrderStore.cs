using System.Text.Json;
using System.Data;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.설정;
using 살뜰.도메인.음식;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.공통;

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

    public 음식주문응답? 접수주문조회(string 주문자UserId, Guid 클라이언트요청Id)
    {
        var owner = Clean(주문자UserId);
        if (owner is null || 클라이언트요청Id == Guid.Empty) return null;
        var order = FindByClientRequest(new 음식주문등록요청
        {
            주문자UserId = owner,
            클라이언트요청Id = 클라이언트요청Id
        });
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

        var currentStatus = 음식배달업무상태전이Guard.정본상태확인(order.상태);
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
        order.음식점명 = Clean(request.음식점명) ?? order.음식점명;
        order.음식점주소 = Clean(request.음식점주소) ?? order.음식점주소;
        order.음식점상세주소 = Clean(request.음식점상세주소) ?? order.음식점상세주소;
        order.음식점위도 = request.음식점위도 ?? order.음식점위도;
        order.음식점경도 = request.음식점경도 ?? order.음식점경도;
        order.음식점수락시각Utc = now;
        order.적용조리분 = cookingMinutes;
        // 준비된 음식은 조리 시작과 구별하며 새 조리는 배차 확정 뒤 시작합니다.
        order.조리예상완료시각Utc = request.즉시픽업가능여부 ? now : null;
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
            사유 = request.즉시픽업가능여부 ? "음식점 주문 확인 · 기존 준비 완료" : "음식점 주문 확인 · 배차 후 조리",
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
        // 직접 저장소 호출도 배차 해제와 조리 시작의 경쟁을 같은 DB 경계에서 처리합니다.
        using var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null
            ? _db.Database.BeginTransaction(IsolationLevel.Serializable)
            : null;
        var result = 진행변경Core(orderNo, request, 처리UserId);
        transaction?.Commit();
        return result;
    }

    private 음식주문변경결과? 진행변경Core(
        string orderNo, 음식점주문진행변경요청 request, string 처리UserId)
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

        var currentStatus = 음식배달업무상태전이Guard.정본상태확인(order.상태);
        if (request.예상Revision.HasValue && request.예상Revision.Value != order.상태이력.Count)
        {
            throw new DbUpdateConcurrencyException("음식 주문이 다른 요청에서 먼저 변경되었습니다.");
        }
        var preparation = 음식주문현재조리Policy.계산(order);
        var started = preparation.CookingStartedAtUtc.HasValue;
        var prepared = preparation.ReadyAtUtc.HasValue;
        var decision = 음식점주문진행Policy.판정(currentStatus, request,
            유효한배차확정(order) && order.상태이력.Any(x => x.다음상태 == 음식주문상태코드.주문확인),
            started || prepared, order.적용조리분);
        var now = DateTime.UtcNow;
        order.상태 = decision.다음상태;
        if (decision.조리예상분 is { } cookingMinutes)
        {
            order.적용조리분 = cookingMinutes;
            if (started || request.작업.Trim() is 음식점주문진행작업코드.조리시작 or 음식점주문진행작업코드.픽업준비)
                order.조리예상완료시각Utc = now.AddMinutes(cookingMinutes);
        }
        if (request.작업.Trim() == 음식점주문진행작업코드.조리시작)
        {
            var attempt = _db.음식배달시도.Where(x => x.주문번호 == order.주문번호)
                .OrderByDescending(x => x.시도순번).ThenByDescending(x => x.Id).First();
            attempt.표시준비예정시각Utc = order.조리예상완료시각Utc;
            attempt.Revision++;
            attempt.UpdatedAtUtc = now;
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

        var currentStatus = 음식배달업무상태전이Guard.정본상태확인(order.상태);
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
        // 수령 확인과 주문별 정산 보류 상태는 같은 SaveChanges에서 확정합니다.
        var settlement = _db.음식주문기사정산.SingleOrDefault(x => x.주문번호 == cleanOrderNo);
        if (settlement is not null && !settlement.수령확인시각Utc.HasValue)
        {
            Ssalddel.Application.Food.음식주문기사정산Recorder.상태반영(settlement, order);
            settlement.Revision++;
            settlement.UpdatedAtUtc = now;
        }
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

        var currentStatus = 음식배달업무상태전이Guard.정본상태확인(order.상태);
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
        if (dispatchWaitId <= 0) throw new ArgumentOutOfRangeException(nameof(dispatchWaitId));
        var cleanOrderNo = Clean(orderNo);
        if (cleanOrderNo is null)
        {
            return null;
        }

        var ownsTransaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null;
        // MySQL의 재시도 전략 안에서 시작·읽기·저장·commit을 하나의 단위로 실행합니다.
        // 호출자가 이미 가진 transaction은 이 메서드가 재시도하거나 commit하지 않습니다.
        return ownsTransaction
            ? _db.Database.CreateExecutionStrategy().Execute(ApplyBinding)
            : ApplyBinding();

        음식주문응답? ApplyBinding()
        {
            // 최초 결속도 기사 수락과 경쟁할 수 있으므로 현재 주문을 같은 DB 경계에서 읽습니다.
            using var transaction = ownsTransaction
                ? _db.Database.BeginTransaction(IsolationLevel.Serializable)
                : null;
            var order = LoadOrderForUpdate(cleanOrderNo);
            if (order is null)
            {
                return null;
            }

            // 실패한 SaveChanges가 Modified 값을 남겨도 재시도는 영속된 현재 결속을 다시 읽습니다.
            // 외부 transaction에서 아직 저장하지 않은 호출자 변경은 덮어쓰지 않습니다.
            if (ownsTransaction || _db.Entry(order).State == EntityState.Unchanged) _db.Entry(order).Reload();
            var current = 음식배달업무상태전이Guard.정본상태확인(order.상태);
            if (order.배차대기Id.HasValue)
            {
                if (order.배차대기Id != dispatchWaitId)
                    throw new InvalidOperationException("음식 주문에 이미 다른 배차대기가 연결되어 있습니다.");
                // 같은 큐의 후속 투영 재시도는 진행 상태와 최초 요청시각을 다시 쓰지 않습니다.
                return GetOrder(cleanOrderNo);
            }
            if (current is 음식주문상태코드.주문대기 or 음식주문상태코드.거절 or 음식주문상태코드.취소)
                throw new InvalidOperationException("음식점 수락 뒤의 주문에만 배차대기를 연결할 수 있습니다.");

            if (order.배차상태 == 음식주문배차상태코드.미요청)
                order.배차상태 = 음식주문배차상태코드.배차대기;
            order.배차대기Id = dispatchWaitId;
            order.배차요청시각Utc ??= dispatchRequestedAtUtc;
            order.UpdatedAt = DateTime.UtcNow;

            _db.SaveChanges();
            transaction?.Commit();
            return GetOrder(cleanOrderNo);
        }
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

    private bool 유효한배차확정(음식주문 order)
    {
        if (order.배차대기Id is not { } id || order.배차상태 != 음식주문배차상태코드.기사배정)
            return false;
        var queue = _db.운송원장.AsNoTracking().SingleOrDefault(x => x.Id == id);
        if (queue is null || queue.원본의뢰Id != order.주문번호
            || queue.배차업무유형 != 상태값.배차업무유형.음식배달
            || queue.상태 != 상태값.배차대기상태.확정 || queue.배차큐단계 != 상태값.배차큐단계.확정
            || string.IsNullOrWhiteSpace(queue.확정기사Id)) return false;
        var attempt = _db.음식배달시도.AsNoTracking()
            .Where(x => x.주문번호 == order.주문번호)
            .OrderByDescending(x => x.시도순번).ThenByDescending(x => x.Id).FirstOrDefault();
        return attempt is not null && attempt.제안Id == queue.의뢰Id && attempt.기사Id == queue.확정기사Id
            && !attempt.중단시각Utc.HasValue && !attempt.전달완료시각Utc.HasValue
            && !attempt.픽업완료시각Utc.HasValue;
    }

    private static DateTime? 조리시작시각(음식주문 order)
        => order.상태이력.Where(x => x.사유 == "음식점 조리 시작"
            || (x.사유 == "음식점 주문 수락" && x.다음상태 == 음식주문상태코드.조리중))
            .OrderBy(x => x.전이시각Utc).Select(x => (DateTime?)x.전이시각Utc).FirstOrDefault();

    private 음식주문응답 ToDto(음식주문 order)
    {
        var preparation = 음식주문현재조리Policy.계산(order);
        return new()
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
            결제승인 = order.결제승인Id is not null && order.결제승인금액.HasValue
                && order.결제승인통화 is not null && order.결제승인시각Utc.HasValue
                ? new 음식주문결제승인Dto
                {
                    결제Id = order.결제승인Id,
                    승인금액 = order.결제승인금액.Value,
                    통화 = order.결제승인통화,
                    승인시각Utc = DateTime.SpecifyKind(order.결제승인시각Utc.Value, DateTimeKind.Utc)
                } : null,
            음식점수락시각Utc = order.음식점수락시각Utc,
            조리예상분 = order.적용조리분,
            조리시작시각Utc = 조리시작시각(order),
            조리시작가능 = order.상태 == 음식주문상태코드.기사배정
                && !preparation.CookingStartedAtUtc.HasValue && order.적용조리분 != 0
                && order.상태이력.Any(x => x.다음상태 == 음식주문상태코드.주문확인)
                && !preparation.ReadyAtUtc.HasValue
                && 유효한배차확정(order),
            조리예상완료시각Utc = order.조리예상완료시각Utc,
            픽업준비시각Utc = order.상태이력
                .Where(x => x.다음상태 == 음식주문상태코드.픽업대기
                            || x.사유.StartsWith("음식점 픽업 준비 완료", StringComparison.Ordinal)
                            || x.사유 == "음식점 주문 확인 · 기존 준비 완료")
                .OrderBy(x => x.전이시각Utc)
                .Select(x => (DateTime?)x.전이시각Utc)
                .FirstOrDefault(),
            CurrentPreparationRound = preparation.Round,
            CurrentCookingStartedAtUtc = preparation.CookingStartedAtUtc,
            CurrentPickupReadyAtUtc = preparation.ReadyAtUtc,
            RecookingRequestedAtUtc = preparation.RecookingRequestedAtUtc,
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
                .ThenBy(x => x.Id)
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
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
