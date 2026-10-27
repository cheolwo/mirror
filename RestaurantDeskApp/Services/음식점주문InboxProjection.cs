using RestaurantDeskApp.Models.Restaurant;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services;

// 서버 응답의 수신함 표현과 로컬 표시 상태를 소유한다. 명령·보호 저장·알림은 실행하지 않는다.
internal sealed class 음식점주문InboxProjection
{
    private readonly object _gate;
    private readonly List<음식점주문DeskItem> _orders = [];
    private readonly I음식점조리시간설정Service _조리시간설정;
    private long _nextId = 1000;

    public 음식점주문InboxProjection(object gate, I음식점조리시간설정Service 조리시간설정)
    {
        _gate = gate;
        _조리시간설정 = 조리시간설정;
    }

    public void Clear()
    {
        lock (_gate) _orders.Clear();
    }

    public IReadOnlyList<음식점주문DeskItem> CaptureSnapshot(IReadOnlySet<string>? currentOrderNos)
    {
        lock (_gate)
        {
            if (currentOrderNos is not null)
                _orders.RemoveAll(order => !currentOrderNos.Contains(order.주문번호));

            return _orders
                .OrderByDescending(x => x.미확인)
                .ThenByDescending(x => x.접수시각)
                .ToArray();
        }
    }

    public long? GetExpectedRevision(string orderNo, string actionId)
    {
        lock (_gate)
        {
            return _orders
                .FirstOrDefault(order => string.Equals(order.주문번호, orderNo, StringComparison.OrdinalIgnoreCase))?
                .AvailableActions
                .FirstOrDefault(action => string.Equals(action.ActionId, actionId, StringComparison.Ordinal))?
                .ExpectedRevision;
        }
    }

    public 음식점주문DeskItem? BeginAcceptance(string 주문번호, int 선택조리예상분)
    {
        lock (_gate)
        {
            var item = FindLocked(주문번호);
            if (item is not null)
            {
                item.선택조리예상분 = 선택조리예상분;
                item.상태 = 음식점주문Desk상태코드.수락처리중;
                item.최근메시지 = $"조리 예상 {선택조리예상분}분으로 서버 주문을 수락하고 있습니다.";
            }
            return item;
        }
    }

    public 음식점주문DeskItem? FailAcceptance(string 주문번호, string message)
    {
        lock (_gate)
        {
            var item = FindLocked(주문번호);
            if (item is not null)
            {
                item.상태 = 음식점주문Desk상태코드.상세조회실패;
                item.최근메시지 = message;
            }
            return item;
        }
    }

    public 음식점주문DeskItem ApplyAcceptedOrder(string 주문번호, 음식주문응답 detail, int 선택조리예상분)
    {
        lock (_gate)
        {
            var item = FindLocked(주문번호);
            // A slower acceptance response cannot overwrite a newer canonical read.
            if (item?.상세주문 is { } current && current.Revision > detail.Revision) return item;
            if (item is null)
            {
                item = CreateDeskItem(detail);
                item.Id = ++_nextId;
                _orders.Add(item);
            }

            ApplyDetail(item, detail);
            item.선택조리예상분 = 선택조리예상분;
            item.상태 = 음식주문상태코드.Normalize(detail.상태);
            item.미확인 = false;
            item.수락시각 = DateTimeOffset.Now;
            item.최근메시지 = $"주문 수락 완료 · 조리 예상 {선택조리예상분}분 · 전표 출력 준비";
            return item;
        }
    }

    public void ApplyRealtimeMessage(음식점주문DeskItem item, string? 본문)
    {
        lock (_gate)
        {
            item.최근메시지 = string.IsNullOrWhiteSpace(본문)
                ? "인증된 서버 원장에서 확인한 실시간 신규 주문"
                : 본문;
        }
    }

    public void RecordPrintRequested(string 주문번호)
    {
        lock (_gate)
        {
            var item = FindLocked(주문번호);
            if (item is not null)
            {
                item.전표출력요청시각 = DateTimeOffset.Now;
                item.최근메시지 = "전표 출력 요청 완료";
            }
        }
    }

    private 음식점주문DeskItem? FindLocked(string 주문번호)
    {
        return _orders.FirstOrDefault(x => string.Equals(x.주문번호, 주문번호, StringComparison.OrdinalIgnoreCase));
    }

    public 음식점주문DeskItem UpsertServerOrder(
        음식주문응답 detail,
        음식점주문복구출처 복구출처)
    {
        lock (_gate)
        {
            var item = FindLocked(detail.주문번호);
            // The PC list and selected detail share this object; ignore older snapshots entirely.
            if (item?.상세주문 is { } current && current.Revision > detail.Revision) return item;
            if (item is null)
            {
                item = CreateDeskItem(detail);
                item.Id = ++_nextId;
                _orders.Add(item);
            }
            else
            {
                ApplyDetail(item, detail);
            }

            ApplyServerStatus(item, detail);
            item.복구출처 = 복구출처;
            item.최근복구시각 = DateTimeOffset.Now;
            return item;
        }
    }

    private 음식점주문DeskItem CreateDeskItem(음식주문응답 detail)
    {
        var item = new 음식점주문DeskItem
        {
            주문번호 = detail.주문번호,
            접수시각 = ToLocalOffset(detail.CreatedAt),
        };

        ApplyDetail(item, detail);
        return item;
    }

    private void ApplyDetail(음식점주문DeskItem item, 음식주문응답 detail)
    {
        item.음식점Id = detail.음식점Id;
        item.고객명 = detail.수령인정보.수령인명;
        item.메뉴요약 = BuildMenuSummary(detail);
        item.상품목록 = Clone상품목록(detail.상품목록);
        item.상품별조리기준 = Build상품별조리기준(item.상품목록);
        var 조리시간설정 = _조리시간설정.현재조회();
        item.추천조리예상분 = 음식점조리시간정책.주문추천분(
            item.상품목록,
            조리시간설정.상품별기본조리분,
            조리시간설정.음식점기본조리분);
        if (detail.조리예상분 is > 0) item.선택조리예상분 = detail.조리예상분;
        item.주문금액 = detail.총주문금액;
        item.배차상태 = detail.배차상태;
        item.배차요청시각Utc = detail.배차요청시각Utc;
        item.AvailableActions = detail.AvailableActions;
        item.상세주문 = detail;
    }

    private static void ApplyServerStatus(
        음식점주문DeskItem item,
        음식주문응답 detail)
    {
        if (detail.상태 == 음식주문상태코드.주문대기)
        {
            item.상태 = 음식점주문Desk상태코드.주문대기;
            item.미확인 = true;
            item.최근메시지 ??= "서버 주문 원장에서 복구한 신규 주문";
            return;
        }

        item.상태 = 음식주문상태코드.Normalize(detail.상태);

        item.미확인 = false;
        item.수락시각 = detail.음식점수락시각Utc.HasValue
            ? ToLocalOffset(detail.음식점수락시각Utc.Value)
            : item.수락시각;
        item.최근메시지 = $"서버 상태 · {detail.상태}";
    }

    private static string BuildMenuSummary(음식주문응답 detail)
    {
        return string.Join(", ", detail.상품목록.Select(x => $"{x.상품명} {x.수량}"));
    }

    private static IReadOnlyList<음식주문상품Dto> Clone상품목록(
        IEnumerable<음식주문상품Dto> 상품목록)
        => 상품목록.Select(item => new 음식주문상품Dto
        {
            메뉴Id = item.메뉴Id,
            상품명 = item.상품명,
            수량 = item.수량,
            단가 = item.단가
        }).ToArray();

    private IReadOnlyList<음식점주문상품조리기준> Build상품별조리기준(
        IEnumerable<음식주문상품Dto> 상품목록)
    {
        var 조리시간설정 = _조리시간설정.현재조회();
        return 상품목록.Select(item =>
        {
            var 상품기본조리분 = 음식점조리시간정책.상품기본분(
                item.상품명,
                조리시간설정.상품별기본조리분,
                조리시간설정.음식점기본조리분);
            var 별도설정있음 = 조리시간설정.상품별기본조리분.Keys.Any(key =>
                string.Equals(key.Trim(), item.상품명.Trim(), StringComparison.OrdinalIgnoreCase));

            return new 음식점주문상품조리기준(
                item.상품명,
                item.수량,
                상품기본조리분,
                !별도설정있음);
        }).ToArray();
    }

    private static DateTimeOffset ToLocalOffset(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return new DateTimeOffset(value).ToLocalTime();
        }

        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Local));
    }
}
