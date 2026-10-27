using Ssalddel.Contracts.Food;
using Microsoft.Extensions.Options;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Options;

namespace RestaurantDeskApp.Services;

public sealed class 음식점주문DeskService : I음식점주문DeskService, IDisposable
{
    private readonly SemaphoreSlim _serverInboxGate = new(1, 1);
    private readonly I음식주문ApiClient _foodOrderClient;
    private readonly 음식점전표DraftFactory _slipFactory;
    private readonly I음식점조리시간설정Service _조리시간설정;
    private readonly RestaurantDeskOptions _options;
    private readonly 음식점주문InboxProjection _inbox;
    private readonly 음식점주문ProgressSession _progress;
    private readonly 음식점주문알림수신Coordinator _notifications;

    public 음식점주문DeskService(
        I음식주문ApiClient foodOrderClient,
        I주문알림Service orderAlertService,
        음식점전표DraftFactory slipFactory,
        I음식점조리시간설정Service 조리시간설정,
        IOptions<RestaurantDeskOptions> options,
        RestaurantAuthService? authService = null,
        IRestaurantProgressPendingStore? pendingStore = null)
    {
        _foodOrderClient = foodOrderClient;
        _slipFactory = slipFactory;
        _조리시간설정 = 조리시간설정;
        _options = options.Value;
        var gate = new object();
        _inbox = new 음식점주문InboxProjection(gate, 조리시간설정);
        _progress = new 음식점주문ProgressSession(gate, _inbox, foodOrderClient, authService,
            pendingStore ?? new RestaurantMemoryProgressPendingStore());
        _notifications = new 음식점주문알림수신Coordinator(foodOrderClient, orderAlertService, _inbox, _progress);
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

        var actor = _progress.CaptureActor();
        await _progress.RestorePendingProgressAsync(actor, cancellationToken);
        var detail = await _foodOrderClient.주문상세조회Async(
            주문번호.Trim(),
            cancellationToken);
        _progress.EnsureCurrentActor(actor, cancellationToken);
        if (detail is not null) await _progress.ResolveConfirmedProgressAsync(detail, actor, cancellationToken);
        return detail is null ? null : _inbox.UpsertServerOrder(detail, 복구출처);
    }

    public async Task<IReadOnlyList<음식점주문DeskItem>> 주문목록조회Async(
        음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회,
        CancellationToken cancellationToken = default)
    {
        var actor = _progress.CaptureActor();
        await _progress.RestorePendingProgressAsync(actor, cancellationToken);
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
                _progress.EnsureCurrentActor(actor, cancellationToken);
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
                await _progress.ResolveConfirmedProgressAsync(serverOrder, actor, cancellationToken);
                _inbox.UpsertServerOrder(serverOrder, 복구출처);
            }

            return _inbox.CaptureSnapshot(fullSnapshotCaptured
                ? serverOrders.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : null);
        }
        finally
        {
            _serverInboxGate.Release();
        }
    }

    public Task<음식점주문DeskItem> 주문알림수신Async(음식점주문수신Payload payload, CancellationToken cancellationToken = default)
        => _notifications.주문알림수신Async(payload, cancellationToken);

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
        var item = _inbox.BeginAcceptance(주문번호, 선택조리예상분);

        음식주문응답? detail;
        try
        {
            detail = await _foodOrderClient.음식점수락Async(
                주문번호,
                new 음식점주문수락요청
                {
                    클라이언트요청Id = _progress.GetOperationRequestId(
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
            item = _inbox.FailAcceptance(주문번호, "서버 주문 수락에 실패했습니다. 다시 시도할 수 있습니다.");

            throw;
        }

        if (detail is null)
        {
            item = _inbox.FailAcceptance(주문번호, "서버에서 주문 상세를 찾지 못했습니다.");

            return new 음식점주문수락결과
            {
                성공 = false,
                메시지 = "주문 상세를 찾지 못해 전표를 만들 수 없습니다.",
                주문 = item
            };
        }

        var draft = _slipFactory.Create주문전표Draft(detail);
        item = _progress.CompleteAcceptance(주문번호, detail, 선택조리예상분);

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

        return _progress.진행변경Async(
            주문번호,
            음식점주문진행작업코드.거절,
            조리예상분: null,
            사유.Trim(),
            cancellationToken);
    }

    public Task<음식점주문DeskItem?> 조리시작Async(
        string 주문번호, int 조리예상분, CancellationToken cancellationToken = default)
        => _progress.진행변경Async(주문번호, 음식점주문진행작업코드.조리시작,
            음식점조리시간정책.Clamp(조리예상분), string.Empty, cancellationToken);

    public Task<음식점주문DeskItem?> 조리시간변경Async(
        string 주문번호,
        int 조리예상분,
        CancellationToken cancellationToken = default)
        => _progress.진행변경Async(
            주문번호,
            음식점주문진행작업코드.조리시간변경,
            음식점조리시간정책.Clamp(조리예상분),
            사유: string.Empty,
            cancellationToken);

    public Task<음식점주문DeskItem?> 픽업준비완료Async(
        string 주문번호,
        CancellationToken cancellationToken = default)
        => _progress.진행변경Async(
            주문번호,
            음식점주문진행작업코드.픽업준비,
            조리예상분: null,
            사유: string.Empty,
            cancellationToken);

    public async Task<음식점주문수락결과> 전표준비Async(string 주문번호, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(주문번호);
        var item = await 주문조회Async(주문번호, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var detail = item?.상세주문;
        if (detail is null || !detail.음식점수락시각Utc.HasValue
            || detail.상태 is 음식주문상태코드.취소 or 음식주문상태코드.거절)
        {
            return new 음식점주문수락결과
            {
                성공 = false, 주문 = item, 상세주문 = detail,
                메시지 = "확인된 주문의 최신 전표를 만들 수 없습니다. 주문 상태를 다시 확인해 주세요."
            };
        }

        return new 음식점주문수락결과
        {
            성공 = true, 주문 = item, 상세주문 = detail,
            전표Draft = _slipFactory.Create주문전표Draft(detail),
            메시지 = "최신 주문으로 전표를 준비했습니다. 주문을 다시 수락하지 않습니다."
        };
    }

    // 기존 호출 이름의 호환성을 유지한다. 출력 장치의 완료 여부는 이 앱에서 확인할 수 없다.
    public Task 전표출력요청기록Async(string 주문번호, CancellationToken cancellationToken = default)
        => 전표출력완료Async(주문번호, cancellationToken);

    public Task 전표출력완료Async(string 주문번호, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inbox.RecordPrintRequested(주문번호);

        return Task.CompletedTask;
    }

    public void Dispose() => _progress.Dispose();
}
