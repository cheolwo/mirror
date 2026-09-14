using Ssalddel.Contracts.Food;
using Microsoft.Extensions.Options;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Options;

namespace RestaurantDeskApp.Services;

public sealed class 음식점주문DeskService : I음식점주문DeskService
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _serverInboxGate = new(1, 1);
    private readonly List<음식점주문DeskItem> _orders;
    private readonly Dictionary<string, Guid> _operationRequestIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly I음식주문ApiClient _foodOrderClient;
    private readonly I주문알림Service _orderAlertService;
    private readonly 음식점전표DraftFactory _slipFactory;
    private readonly I음식점조리시간설정Service _조리시간설정;
    private readonly RestaurantDeskOptions _options;
    private long _nextId = 1000;

    public 음식점주문DeskService(
        I음식주문ApiClient foodOrderClient,
        I주문알림Service orderAlertService,
        음식점전표DraftFactory slipFactory,
        I음식점조리시간설정Service 조리시간설정,
        IOptions<RestaurantDeskOptions> options)
    {
        _foodOrderClient = foodOrderClient;
        _orderAlertService = orderAlertService;
        _slipFactory = slipFactory;
        _조리시간설정 = 조리시간설정;
        _options = options.Value;
        _orders = [];
    }

    public async Task<음식점주문DeskItem?> 주문조회Async(
        string 주문번호,
        음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(주문번호))
        {
            return null;
        }

        var detail = await _foodOrderClient.주문상세조회Async(
            주문번호.Trim(),
            cancellationToken);
        return detail is null ? null : UpsertServerOrder(detail, 복구출처);
    }

    public async Task<IReadOnlyList<음식점주문DeskItem>> 주문목록조회Async(
        음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회,
        CancellationToken cancellationToken = default)
    {
        await _serverInboxGate.WaitAsync(cancellationToken);
        try
        {
            const int pageSize = 100;
            var page = 1;
            var expectedTotal = 0;
            var serverOrders = new Dictionary<string, 음식주문응답>(StringComparer.OrdinalIgnoreCase);
            var fullSnapshotCaptured = false;

            while (true)
            {
                var previousOrderCount = serverOrders.Count;
                var response = await _foodOrderClient.주문목록조회Async(
                    new 음식점주문수신함조회요청
                    {
                        처리상태 = 음식점주문수신함처리상태코드.미처리,
                        Page = page,
                        PageSize = pageSize
                    },
                    cancellationToken);
                expectedTotal = response.TotalCount;
                foreach (var serverOrder in response.Items)
                {
                    serverOrders[serverOrder.주문번호] = serverOrder;
                }

                if (serverOrders.Count >= expectedTotal)
                {
                    fullSnapshotCaptured = true;
                    break;
                }

                if (response.Items.Count == 0 || serverOrders.Count == previousOrderCount)
                {
                    break;
                }

                page++;
            }

            foreach (var serverOrder in serverOrders.Values)
            {
                UpsertServerOrder(serverOrder, 복구출처);
            }

            lock (_gate)
            {
                if (fullSnapshotCaptured)
                {
                    var currentOrderNos = serverOrders.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    _orders.RemoveAll(order => !currentOrderNos.Contains(order.주문번호));
                }

                return _orders
                    .OrderByDescending(x => x.미확인)
                    .ThenByDescending(x => x.접수시각)
                    .ToArray();
            }
        }
        finally
        {
            _serverInboxGate.Release();
        }
    }

    public async Task<음식점주문DeskItem> 주문알림수신Async(음식점주문수신Payload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.주문번호);
        var serverOrder = await _foodOrderClient.주문상세조회Async(
            payload.주문번호,
            cancellationToken);
        if (serverOrder is null || serverOrder.음식점Id != payload.음식점Id)
        {
            throw new InvalidOperationException("인증된 음식점 수신함에서 주문 알림을 확인할 수 없습니다.");
        }

        var item = UpsertServerOrder(
            serverOrder,
            음식점주문복구출처.실시간);
        lock (_gate)
        {
            item.최근메시지 = string.IsNullOrWhiteSpace(payload.본문)
                ? "인증된 서버 원장에서 확인한 실시간 신규 주문"
                : payload.본문;
        }

        await _orderAlertService.신규주문알림재생Async(cancellationToken);
        return item;
    }

    public async Task<음식점주문수락결과> 주문수락후전표준비Async(string 주문번호, CancellationToken cancellationToken = default)
    {
        var item = await 주문조회Async(
            주문번호,
            음식점주문복구출처.서버재조회,
            cancellationToken);
        var 조리시간설정 = _조리시간설정.현재조회();
        var 조리예상분 = item?.선택조리예상분
            ?? item?.추천조리예상분
            ?? 조리시간설정.음식점기본조리분;

        return await 주문수락후전표준비Async(주문번호, 조리예상분, cancellationToken);
    }

    public async Task<음식점주문수락결과> 주문수락후전표준비Async(
        string 주문번호,
        int 조리예상분,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(주문번호))
        {
            return new 음식점주문수락결과 { 성공 = false, 메시지 = "주문번호가 없습니다." };
        }

        var 선택조리예상분 = 음식점조리시간정책.Clamp(조리예상분);
        음식점주문DeskItem? item;
        lock (_gate)
        {
            item = FindLocked(주문번호);
            if (item is not null)
            {
                item.선택조리예상분 = 선택조리예상분;
                item.상태 = 음식점주문Desk상태코드.수락처리중;
                item.최근메시지 = $"조리 예상 {선택조리예상분}분으로 서버 주문을 수락하고 있습니다.";
            }
        }

        음식주문응답? detail;
        try
        {
            detail = await _foodOrderClient.음식점수락Async(
                주문번호,
                new 음식점주문수락요청
                {
                    클라이언트요청Id = GetOperationRequestId(
                        주문번호,
                        "acceptance"),
                    음식점명 = _options.RestaurantName,
                    음식점주소 = _options.RestaurantAddress,
                    음식점상세주소 = _options.RestaurantDetailAddress,
                    음식점위도 = _options.RestaurantLatitude,
                    음식점경도 = _options.RestaurantLongitude,
                    조리예상분 = 선택조리예상분,
                    즉시픽업가능여부 = false,
                    수락메모 = "Restaurant Desk에서 수락"
                },
                cancellationToken);
        }
        catch
        {
            lock (_gate)
            {
                item = FindLocked(주문번호);
                if (item is not null)
                {
                    item.상태 = 음식점주문Desk상태코드.상세조회실패;
                    item.최근메시지 = "서버 주문 수락에 실패했습니다. 다시 시도할 수 있습니다.";
                }
            }

            throw;
        }

        if (detail is null)
        {
            lock (_gate)
            {
                item = FindLocked(주문번호);
                if (item is not null)
                {
                    item.상태 = 음식점주문Desk상태코드.상세조회실패;
                    item.최근메시지 = "서버에서 주문 상세를 찾지 못했습니다.";
                }
            }

            return new 음식점주문수락결과
            {
                성공 = false,
                메시지 = "주문 상세를 찾지 못해 전표를 만들 수 없습니다.",
                주문 = item
            };
        }

        var draft = _slipFactory.Create주문전표Draft(detail);
        lock (_gate)
        {
            item = FindLocked(주문번호);
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
            RemoveOperationRequestId(주문번호, "acceptance");
        }

        return new 음식점주문수락결과
        {
            성공 = true,
            메시지 = $"주문을 수락했고 조리 예상시간을 {선택조리예상분}분으로 설정했습니다.",
            주문 = item,
            상세주문 = detail,
            전표Draft = draft
        };
    }

    public Task<음식점주문DeskItem?> 주문거절Async(
        string 주문번호,
        string 사유,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(사유))
        {
            throw new ArgumentException("주문 거절 사유를 입력해 주세요.", nameof(사유));
        }

        return 진행변경Async(
            주문번호,
            음식점주문진행작업코드.거절,
            조리예상분: null,
            사유.Trim(),
            cancellationToken);
    }

    public Task<음식점주문DeskItem?> 조리시간변경Async(
        string 주문번호,
        int 조리예상분,
        CancellationToken cancellationToken = default)
        => 진행변경Async(
            주문번호,
            음식점주문진행작업코드.조리시간변경,
            음식점조리시간정책.Clamp(조리예상분),
            사유: string.Empty,
            cancellationToken);

    public Task<음식점주문DeskItem?> 픽업준비완료Async(
        string 주문번호,
        CancellationToken cancellationToken = default)
        => 진행변경Async(
            주문번호,
            음식점주문진행작업코드.픽업준비,
            조리예상분: null,
            사유: string.Empty,
            cancellationToken);

    public Task 전표출력완료Async(string 주문번호, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var item = FindLocked(주문번호);
            if (item is not null)
            {
                item.상태 = 음식점주문Desk상태코드.전표출력됨;
                item.전표출력시각 = DateTimeOffset.Now;
                item.최근메시지 = "전표 출력 요청 완료";
            }
        }

        return Task.CompletedTask;
    }

    private 음식점주문DeskItem? FindLocked(string 주문번호)
    {
        return _orders.FirstOrDefault(x => string.Equals(x.주문번호, 주문번호, StringComparison.OrdinalIgnoreCase));
    }

    private 음식점주문DeskItem UpsertServerOrder(
        음식주문응답 detail,
        음식점주문복구출처 복구출처)
    {
        lock (_gate)
        {
            var item = FindLocked(detail.주문번호);
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

    private async Task<음식점주문DeskItem?> 진행변경Async(
        string 주문번호,
        string 작업,
        int? 조리예상분,
        string 사유,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(주문번호);

        var detail = await _foodOrderClient.음식점진행변경Async(
            주문번호,
            new 음식점주문진행변경요청
            {
                클라이언트요청Id = GetOperationRequestId(주문번호, 작업),
                예상Revision = GetExpectedRevision(주문번호, 작업),
                작업 = 작업,
                조리예상분 = 조리예상분,
                사유 = 사유
            },
            cancellationToken);
        if (detail is null)
        {
            return null;
        }

        RemoveOperationRequestId(주문번호, 작업);
        return UpsertServerOrder(
            detail,
            음식점주문복구출처.서버재조회);
    }

    private long? GetExpectedRevision(string orderNo, string operation)
    {
        var actionId = operation switch
        {
            음식점주문진행작업코드.거절 => 음식배달가능행동Ids.음식점주문거절,
            음식점주문진행작업코드.조리시간변경 => 음식배달가능행동Ids.음식점조리시간변경,
            음식점주문진행작업코드.픽업준비 => 음식배달가능행동Ids.음식점픽업준비완료,
            _ => null
        };
        if (actionId is null)
        {
            return null;
        }

        lock (_gate)
        {
            return _orders
                .FirstOrDefault(order => string.Equals(order.주문번호, orderNo, StringComparison.OrdinalIgnoreCase))?
                .AvailableActions
                .FirstOrDefault(action => string.Equals(action.ActionId, actionId, StringComparison.Ordinal))?
                .ExpectedRevision;
        }
    }

    private Guid GetOperationRequestId(string orderNo, string operation)
    {
        var key = $"{orderNo.Trim()}::{operation}";
        lock (_gate)
        {
            if (_operationRequestIds.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var created = Guid.NewGuid();
            _operationRequestIds[key] = created;
            return created;
        }
    }

    private void RemoveOperationRequestId(string orderNo, string operation)
    {
        var key = $"{orderNo.Trim()}::{operation}";
        lock (_gate)
        {
            _operationRequestIds.Remove(key);
        }
    }

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
