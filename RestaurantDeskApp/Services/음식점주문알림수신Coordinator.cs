using RestaurantDeskApp.Models.Restaurant;

namespace RestaurantDeskApp.Services;

// Desk 수신 경로의 정본 확인→수신함 갱신→소리 순서만 조율한다.
// OS 표시·중복 제어는 기존 RestaurantOrderNotificationCoordinator가 맡는다.
internal sealed class 음식점주문알림수신Coordinator
{
    private readonly I음식주문ApiClient _foodOrderClient;
    private readonly I주문알림Service _orderAlertService;
    private readonly 음식점주문InboxProjection _inbox;
    private readonly 음식점주문ProgressSession _progress;

    public 음식점주문알림수신Coordinator(I음식주문ApiClient foodOrderClient,
        I주문알림Service orderAlertService, 음식점주문InboxProjection inbox, 음식점주문ProgressSession progress)
    {
        _foodOrderClient = foodOrderClient;
        _orderAlertService = orderAlertService;
        _inbox = inbox;
        _progress = progress;
    }

    public async Task<음식점주문DeskItem> 주문알림수신Async(음식점주문수신Payload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.주문번호);
        var actor = _progress.CaptureActor();
        await _progress.RestorePendingProgressAsync(actor, cancellationToken);
        var serverOrder = await _foodOrderClient.주문상세조회Async(
            payload.주문번호,
            cancellationToken);
        _progress.EnsureCurrentActor(actor, cancellationToken);
        if (serverOrder is null || serverOrder.음식점Id != payload.음식점Id)
        {
            throw new InvalidOperationException("인증된 음식점 수신함에서 주문 알림을 확인할 수 없습니다.");
        }

        await _progress.ResolveConfirmedProgressAsync(serverOrder, actor, cancellationToken);
        var item = _inbox.UpsertServerOrder(
            serverOrder,
            음식점주문복구출처.실시간);
        _inbox.ApplyRealtimeMessage(item, payload.본문);

        await _orderAlertService.신규주문알림재생Async(cancellationToken);
        return item;
    }

}
