using FDriverApp.Services;
using Ssalddel.Contracts.Driver.Food;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private IFDriverFoodNotificationService _foodNotifications = NullFDriverFoodNotificationService.Instance;
    private FDriverFoodNotificationState _foodNotificationState = new(NullFDriverFoodNotificationService.Instance);
    private bool _notificationResumeInProgress;
    private string? _foodNotificationFocus;
    public string? FoodNotificationFocus => _foodNotificationFocus;

    public string? TakeFoodNotificationFocus()
    {
        var focus = _foodNotificationFocus;
        _foodNotificationFocus = null;
        return focus;
    }

    private void InitializeFoodNotifications(IFDriverFoodNotificationService? notifications)
    {
        _foodNotifications = notifications ?? NullFDriverFoodNotificationService.Instance;
        _foodNotificationState = new(_foodNotifications);
    }

    private void ApplyFoodRecommendations(FoodDeliveryDriverWorkspaceDto workspace)
    {
        var now = DateTime.UtcNow;
        var acceptedIds = workspace.ActiveDeliveries.Select(x => x.OfferId).ToHashSet(StringComparer.Ordinal);
        var recommendations = workspace.Recommendations.Where(x => !acceptedIds.Contains(x.OfferId)
                && (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc.Value > now))
                .GroupBy(x => x.OfferId, StringComparer.Ordinal).Select(x => x.First()).ToArray();
        RecommendedTicketItems.Clear();
        foreach (var offer in recommendations) RecommendedTicketItems.Add(DeliveryTicketPreview.From(offer));
        _foodNotificationState.BindAccount(IsAuthenticated ? _authSession.UserId : null);
        _foodNotificationState.Reconcile(DispatchIntentKnown && ReceivesNewDispatches ? RecommendedTicketItems.ToArray() : [], now);
        UpdateFoodRecommendationNotice();
    }

    private void ReconcileFoodNotificationCountdowns(DateTime utcNow)
    {
        var expired = RecommendedTicketItems.Where(x => x.IsExpired).ToArray();
        foreach (var item in expired) RecommendedTicketItems.Remove(item);
        var currentIds = RecommendedTicketItems.Select(x => x.TicketId).ToHashSet(StringComparer.Ordinal);
        foreach (var bundle in BundleCandidateItems.Where(x => x.OfferIds.Any(id => !currentIds.Contains(id))).ToArray())
            BundleCandidateItems.Remove(bundle);
        if (SelectedTicket is not null && !currentIds.Contains(SelectedTicket.TicketId))
        {
            SelectedTicket = RecommendedTicketItems.FirstOrDefault();
            if (ActiveDelivery is null) { SelectedRouteOverlays = []; RouteStatusText = "새 추천을 확인해 주세요."; }
        }
        PendingDeliveryTickets = RecommendedTicketItems.Count + ActiveDeliveryItems.Count;
        RecommendedTickets = RecommendedTicketItems.Count;
        TodayExpectedPayout = RecommendedTicketItems.Sum(x => x.DriverPayout);
        MapMarkers = RecommendedTicketItems.Select(ToMapMarker).Concat(ActiveDeliveryItems.Select(ToMapMarker)).ToArray();
        _foodNotificationState.Reconcile(DispatchIntentKnown && ReceivesNewDispatches ? RecommendedTicketItems.ToArray() : [], utcNow);
        UpdateFoodRecommendationNotice();
        NotifyWorkspaceState();
    }

    private void UpdateFoodRecommendationNotice()
    {
        HasNewRecommendations = _foodNotificationState.UnreadCount > 0;
        NewRecommendationNotice = HasNewRecommendations
            ? $"새 추천 배차 {_foodNotificationState.UnreadCount}건이 도착했습니다."
            : "확인할 새 추천 배차가 없습니다.";
    }

    private void ClearFoodNotifications()
    {
        _foodNotificationState.Clear();
        _foodNotificationFocus = null;
        HasNewRecommendations = false;
        NewRecommendationNotice = "확인할 새 추천 배차가 없습니다.";
    }

    private async Task OpenLatestFoodRecommendationAsync()
    {
        if (!_workspaceActive || !IsAuthenticated || IsBusy || ExceptionEditor.IsOpen || ExceptionEditor.HasPendingRequest) return;
        var requestedOfferId = _foodNotificationState.LatestOfferId;
        await RunApiAsync(async token =>
        {
            // 알림은 조회 힌트이며 캐시 제안을 수락할 권한이 아니다.
            await LoadWorkspaceAsync(refreshRoute: false, cancellationToken: token);
            token.ThrowIfCancellationRequested();
            var ticket = requestedOfferId is null
                ? RecommendedTicketItems.FirstOrDefault(x => !x.IsExpired)
                : RecommendedTicketItems.FirstOrDefault(x => x.TicketId == requestedOfferId && !x.IsExpired);
            _foodNotificationState.MarkRead(); UpdateFoodRecommendationNotice();
            if (ticket is null)
            {
                StatusMessage = "해당 추천은 종료됐습니다. 현재 배달과 새 추천을 확인해 주세요.";
                return;
            }
            SelectedTicket = ticket;
            Navigation.Select(FDriverWorkspaceSection.Delivery);
            await RefreshRouteAsync(token);
            token.ThrowIfCancellationRequested();
            StatusMessage = $"{ticket.RestaurantName} 배달 요청입니다. 경로를 확인한 뒤 수락하거나 거절해 주세요.";
        });
    }

    // 정본 조회 성공 뒤에만 호출하며 조회 실패에는 재시도용 대상 포인터를 보존한다.
    private Task RestoreFoodNotificationTargetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = _foodNotifications.ReadOpenTarget();
        if (target is null || !IsAuthenticated) return Task.CompletedTask;
        if (!string.Equals(target.UserId, _authSession.UserId, StringComparison.Ordinal))
        {
            _foodNotifications.ClearOpenTarget(); _foodNotifications.Cancel();
            return Task.CompletedTask;
        }
        if (ExceptionEditor.IsOpen || ExceptionEditor.HasPendingRequest) return Task.CompletedTask;
        var active = ActiveDeliveryItems.FirstOrDefault(x => x.OfferId == target.OfferId);
        var ticket = RecommendedTicketItems.FirstOrDefault(x => x.TicketId == target.OfferId && !x.IsExpired);
        _foodNotifications.ClearOpenTarget();
        _foodNotificationState.MarkRead(); UpdateFoodRecommendationNotice();
        Navigation.Select(FDriverWorkspaceSection.Delivery);
        if (active is not null)
        {
            ActiveDelivery = active;
            SetWorkStage();
            StatusMessage = "수락한 현재 배달의 최신 상태를 확인했습니다.";
            _foodNotificationFocus = "delivery";
        }
        else if (ticket is not null)
        {
            SelectedTicket = ticket;
            StatusMessage = $"{ticket.RestaurantName} 배달 요청의 최신 상태를 확인했습니다.";
            _foodNotificationFocus = "dispatch";
        }
        else
        {
            StatusMessage = "해당 추천은 종료됐습니다. 현재 배달과 새 추천을 확인해 주세요.";
            _foodNotificationFocus = "workspace";
        }
        OnPropertyChanged(nameof(FoodNotificationFocus));
        return Task.CompletedTask;
    }

    public async Task ResumeFoodNotificationWorkspaceAsync()
    {
        // MainPage의 최초 초기화가 인증을 복원하므로 시작 조회를 중복 실행하지 않는다.
        if (!_initialized || !_workspaceActive || !IsAuthenticated || IsBusy || _notificationResumeInProgress) return;
        _notificationResumeInProgress = true;
        try
        {
            await ReloadAsync(updateLocation: false);
            await StartMonitoringAsync();
        }
        finally { _notificationResumeInProgress = false; }
    }
}
