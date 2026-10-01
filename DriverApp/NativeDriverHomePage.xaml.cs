using DriverApp.Models.Driver.Samples;
using DriverApp.Models.Driver;
using DriverApp.Services;
using Hongdal.Contracts.Common.Drivers;
using System.Globalization;

namespace DriverApp;

public partial class NativeDriverHomePage : ContentPage
{
    private readonly IDriverSampleDataService _sampleDataService;
    private readonly IDriverHomeMapService _mapService;
    private readonly IDriverRecommendationDecisionService _decisionService;
    private readonly IDriverRecommendationNotificationService _recommendationNotificationService;
    private readonly DriverHomeRoutePlanningService _routePlanningService;
    private readonly I기사푸시토큰등록Service _pushTokenRegistrationService;
    private readonly DriverOperatingProfileService _operatingProfileService;
    private bool _isSubscribed;
    private DriverMapMarkerItem? _incomingRecommendation;
    private DriverRequestItem? _incomingRecommendationRequest;
    private DriverRequestItem? _acceptedRecommendationRequest;
    private 기사운송샘플항목? _currentTransport;
    private IReadOnlyList<DriverMapMarkerItem> _defaultRecommendationMarkers = [];
    private double _defaultCenterLatitude;
    private double _defaultCenterLongitude;
    private const double DefaultMapZoom = 11d;
    private IDispatcherTimer? _recommendationCountdownTimer;
    private bool _autoRejectingRecommendation;

    public NativeDriverHomePage(
        IDriverSampleDataService sampleDataService,
        IDriverHomeMapService mapService,
        IDriverRecommendationDecisionService decisionService,
        IDriverRecommendationNotificationService recommendationNotificationService,
        DriverHomeRoutePlanningService routePlanningService,
        I기사푸시토큰등록Service pushTokenRegistrationService,
        DriverOperatingProfileService operatingProfileService)
    {
        InitializeComponent();
        _sampleDataService = sampleDataService;
        _mapService = mapService;
        _decisionService = decisionService;
        _recommendationNotificationService = recommendationNotificationService;
        _routePlanningService = routePlanningService;
        _pushTokenRegistrationService = pushTokenRegistrationService;
        _operatingProfileService = operatingProfileService;
        ApplyOperatingProfile();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SubscribeEvents();
        ApplyOperatingProfile();
        try
        {
            await _sampleDataService.RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"서버 API 조회 실패: {ex.Message}";
        }

        var currentLocation = _sampleDataService.기사현재위치;
        var markers = _mapService.BuildMarkers(_sampleDataService.추천의뢰목록);
        _defaultRecommendationMarkers = markers;
        _defaultCenterLatitude = (double)currentLocation.위도;
        _defaultCenterLongitude = (double)currentLocation.경도;
        var incoming = _recommendationNotificationService.GetCurrent();
        _incomingRecommendation = incoming?.Marker;
        _incomingRecommendationRequest = incoming?.Request;
        _currentTransport = _sampleDataService.현재운송조회();

        RestoreDefaultMapState();

        if (string.IsNullOrWhiteSpace(StatusLabel.Text) || !StatusLabel.Text.StartsWith("서버 API 조회 실패:", StringComparison.Ordinal))
        {
            StatusLabel.Text = $"{currentLocation.위치명} 기준 추천 운송 {markers.Count}건을 네이티브 지도에 표시합니다.";
        }
        TransportFooterBar.ShowTransport(_currentTransport);
        ShowIncomingRecommendation(incoming);
        RecommendationDetailBanner.IsVisible = false;
        LinkedRouteCard.IsVisible = false;
        RecommendationDecisionButtons.IsEnabled = true;
        await RegisterStoredPushTokenQuietlyAsync();
    }

    protected override void OnDisappearing()
    {
        StopRecommendationCountdown();
        UnsubscribeEvents();
        base.OnDisappearing();
    }

    private void OnMarkerSelected(object? sender, DriverMapMarkerItem marker)
    {
        if (LinkedRouteCard.IsVisible)
        {
            return;
        }

        TransportFooterBar.IsVisible = true;
        TransportFooterBar.ShowMarker(marker);
    }

    private void OnKoreaDriverClicked(object? sender, EventArgs e)
        => _operatingProfileService.SetMarket(Hongdal.Contracts.Common.Operations.OperatingMarketCodes.Korea);

    private void OnUnitedStatesDriverClicked(object? sender, EventArgs e)
        => _operatingProfileService.SetMarket(Hongdal.Contracts.Common.Operations.OperatingMarketCodes.UnitedStates);

    private void OnOperatingProfileChanged()
        => MainThread.BeginInvokeOnMainThread(ApplyOperatingProfile);

    private void ApplyOperatingProfile()
    {
        var profile = _operatingProfileService.Current;
        MapView.MapProviderCode = profile.MapProviderCode;
        OperatingMarketLabel.Text = profile.IsKorea ? "대한민국 운행" : "미국 운행";
        MapProviderLabel.Text = profile.IsKorea
            ? "NAVER 지도 · Directions"
            : "Google 지도 · Routes";

        ApplyProfileButtonState(KoreaDriverButton, profile.IsKorea, "#03c75a");
        ApplyProfileButtonState(UnitedStatesDriverButton, profile.IsUnitedStates, "#4285f4");
    }

    private static void ApplyProfileButtonState(Button button, bool isSelected, string selectedColor)
    {
        button.BackgroundColor = Color.FromArgb(isSelected ? selectedColor : "#f1f5f9");
        button.TextColor = Color.FromArgb(isSelected ? "#ffffff" : "#475569");
    }

    private async Task RegisterStoredPushTokenQuietlyAsync()
    {
        try
        {
            await _pushTokenRegistrationService.저장토큰등록Async();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"푸시 토큰 등록 확인 실패: {ex.Message}";
        }
    }

    private void ShowIncomingRecommendation(DriverIncomingRecommendation? incoming)
    {
        var marker = incoming?.Marker;
        RecommendationBanner.IsVisible = marker is not null;
        if (marker is null)
        {
            RecommendationSummaryLabel.Text = string.Empty;
            RecommendationCountdownLabel.Text = string.Empty;
            RecommendationDetailCountdownLabel.Text = string.Empty;
            RecommendationCountdownProgress.Progress = 0d;
            RecommendationDetailCountdownProgress.Progress = 0d;
            RecommendationDetailBanner.IsVisible = false;
            StopRecommendationCountdown();
            return;
        }

        TransportFooterBar.IsVisible = true;
        RecommendationSummaryLabel.Text = $"{marker.Title} · {marker.PickupAddress} 상차 추천이 도착했습니다. 대기 {incoming!.PendingCount}건";
        RecommendationDetailBanner.IsVisible = false;
        LinkedRouteCard.IsVisible = false;
        StartRecommendationCountdown(incoming);
    }

    private void StartRecommendationCountdown(DriverIncomingRecommendation incoming)
    {
        _autoRejectingRecommendation = false;
        StopRecommendationCountdown();

        _recommendationCountdownTimer = Dispatcher.CreateTimer();
        _recommendationCountdownTimer.Interval = TimeSpan.FromSeconds(1);
        _recommendationCountdownTimer.Tick += async (_, _) => await UpdateRecommendationCountdownAsync(incoming);
        _recommendationCountdownTimer.Start();

        _ = UpdateRecommendationCountdownAsync(incoming);
    }

    private void StopRecommendationCountdown()
    {
        if (_recommendationCountdownTimer is null)
        {
            return;
        }

        _recommendationCountdownTimer.Stop();
        _recommendationCountdownTimer = null;
    }

    private async Task UpdateRecommendationCountdownAsync(DriverIncomingRecommendation incoming)
    {
        var request = incoming.Request;
        var expiresAt = request.추천만료시각?.ToLocalTime() ?? incoming.ReceivedAt.AddSeconds(60);
        var startsAt = request.추천시작시각?.ToLocalTime() ?? incoming.ReceivedAt;
        var totalSeconds = Math.Max(1d, (expiresAt - startsAt).TotalSeconds);
        var remainingSeconds = Math.Max(0d, (expiresAt - DateTime.Now).TotalSeconds);
        var progress = Math.Clamp((totalSeconds - remainingSeconds) / totalSeconds, 0d, 1d);

        var countdownText = remainingSeconds > 0d
            ? $"{Math.Ceiling(remainingSeconds):0}초"
            : "미응답 처리";
        var progressColor = remainingSeconds > 10d ? Color.FromArgb("#22c55e") : Color.FromArgb("#f59e0b");

        RecommendationCountdownLabel.Text = countdownText;
        RecommendationDetailCountdownLabel.Text = countdownText;
        RecommendationCountdownProgress.Progress = progress;
        RecommendationDetailCountdownProgress.Progress = progress;
        RecommendationCountdownProgress.ProgressColor = progressColor;
        RecommendationDetailCountdownProgress.ProgressColor = progressColor;

        if (remainingSeconds > 0d || _autoRejectingRecommendation || _decisionService.GetDecision(request.의뢰Id) is not null)
        {
            return;
        }

        _autoRejectingRecommendation = true;
        try
        {
            var decision = await _decisionService.RejectAsync(request, "60초 미응답 자동 거절");
            RecommendationCountdownLabel.Text = "자동 거절";
            RecommendationDetailCountdownLabel.Text = "자동 거절";
            RecommendationDecisionStatusLabel.Text = BuildDecisionMessage(decision);
            _recommendationNotificationService.MarkHandled(request.의뢰Id);
            RecommendationBanner.IsVisible = false;
            RecommendationDetailBanner.IsVisible = false;
            LinkedRouteCard.IsVisible = false;
            RestoreDefaultMapState();
            StatusLabel.Text = "응답 제한 시간이 지나 추천이 자동 거절되었습니다.";
        }
        catch (Exception ex)
        {
            _autoRejectingRecommendation = false;
            RecommendationCountdownLabel.Text = "처리 실패";
            RecommendationDetailCountdownLabel.Text = "처리 실패";
            StatusLabel.Text = ex.Message;
        }
        finally
        {
            StopRecommendationCountdown();
        }
    }

    private void OnOpenIncomingRecommendationClicked(object? sender, EventArgs e)
    {
        if (_incomingRecommendation is null)
        {
            RecommendationBanner.IsVisible = false;
            return;
        }

        var routeOverlays = _routePlanningService.BuildLinkedRouteOverlays(_sampleDataService.기사현재위치, _currentTransport, _incomingRecommendation, "#16a34a", "연계 추천 경로");
        FocusMapOnRoute(routeOverlays, _incomingRecommendation, reserveBottomSpace: true);
        MapView.Markers = [_incomingRecommendation];
        MapView.RouteOverlays = routeOverlays;
        TransportFooterBar.IsVisible = false;
        RecommendationDetailBanner.IsVisible = false;
        ShowLinkedRouteCard(_sampleDataService.기사현재위치, _currentTransport, _incomingRecommendation, _incomingRecommendationRequest);
        TitleLabel.Text = "배차 추천 확인";
        StatusLabel.Text = "현재 이동 단계와 추천 운송 의뢰의 상차/하차 경로를 함께 표시했습니다.";
        RecommendationBanner.IsVisible = false;
    }

    private void OnDismissRecommendationClicked(object? sender, EventArgs e)
    {
        if (_incomingRecommendationRequest is not null)
        {
            _recommendationNotificationService.MarkDismissed(_incomingRecommendationRequest.의뢰Id);
        }

        RecommendationBanner.IsVisible = false;
        RecommendationDetailBanner.IsVisible = false;
        LinkedRouteCard.IsVisible = false;
        TransportFooterBar.IsVisible = true;
        RestoreDefaultMapState();
        StatusLabel.Text = "추천 알림을 잠시 접었습니다. 지도 마커나 하단 목록에서 다시 확인할 수 있습니다.";
    }

    private void ShowLinkedRouteCard(
        기사현재위치샘플 currentLocation,
        기사운송샘플항목? currentTransport,
        DriverMapMarkerItem recommendation,
        DriverRequestItem? request)
    {
        LinkedRouteCard.IsVisible = true;
        var routeState = _routePlanningService.BuildLinkedRouteCardState(currentLocation, currentTransport, recommendation);
        LinkedRouteSummaryLabel.Text = routeState.Summary;
        LinkedRouteBenefitLabel.Text = routeState.Benefit;
        CurrentDropoffRouteLabel.Text = routeState.CurrentRouteLabel;
        RecommendationPickupRouteLabel.Text = routeState.RecommendationPickupRouteLabel;
        RecommendationDropoffRouteLabel.Text = routeState.RecommendationDropoffRouteLabel;
        RecommendationCompactInfoLabel.Text = BuildRecommendationCompactInfo(request);
        RecommendationDecisionStatusLabel.Text = "경로와 수익을 확인한 뒤 수락, 보류, 거절을 선택할 수 있습니다.";
        RecommendationDecisionButtons.IsEnabled = true;
    }

    private async void OnAcceptRecommendationClicked(object? sender, EventArgs e)
    {
        if (_incomingRecommendationRequest is null)
        {
            RecommendationDecisionStatusLabel.Text = "처리할 추천 의뢰를 찾지 못했습니다.";
            return;
        }

        RecommendationDecisionButtons.IsEnabled = false;
        RecommendationDecisionState decision;
        try
        {
            decision = await _decisionService.AcceptAsync(_incomingRecommendationRequest);
        }
        catch (Exception ex)
        {
            RecommendationDecisionStatusLabel.Text = ex.Message;
            RecommendationDecisionButtons.IsEnabled = true;
            return;
        }

        _acceptedRecommendationRequest = _incomingRecommendationRequest;
        RecommendationDecisionStatusLabel.Text = BuildDecisionMessage(decision);
        RecommendationBanner.IsVisible = false;
        RecommendationDetailBanner.IsVisible = false;
        StopRecommendationCountdown();
        LinkedRouteBenefitLabel.Text = "수락됨";
        ShowAcceptedRecommendationOnMap();
        _recommendationNotificationService.MarkHandled(_incomingRecommendationRequest.의뢰Id);
        StatusLabel.Text = "배차 추천을 수락했습니다. 이제 추천 상차지로 이동해 상차 확인을 진행합니다.";
    }

    private void OnHoldRecommendationClicked(object? sender, EventArgs e)
    {
        if (_incomingRecommendationRequest is null)
        {
            RecommendationDecisionStatusLabel.Text = "처리할 추천 의뢰를 찾지 못했습니다.";
            return;
        }

        var decision = _decisionService.Hold(_incomingRecommendationRequest);
        RecommendationDecisionStatusLabel.Text = BuildDecisionMessage(decision);
        StatusLabel.Text = "추천을 보류했습니다. 지도나 추천 목록에서 다시 확인할 수 있습니다.";
    }

    private async void OnRejectRecommendationClicked(object? sender, EventArgs e)
    {
        if (_incomingRecommendationRequest is null)
        {
            RecommendationDecisionStatusLabel.Text = "처리할 추천 의뢰를 찾지 못했습니다.";
            return;
        }

        RecommendationDecisionButtons.IsEnabled = false;
        RecommendationDecisionState decision;
        try
        {
            decision = await _decisionService.RejectAsync(_incomingRecommendationRequest, "지도 연계 경로에서 거절");
        }
        catch (Exception ex)
        {
            RecommendationDecisionStatusLabel.Text = ex.Message;
            RecommendationDecisionButtons.IsEnabled = true;
            return;
        }

        RecommendationDecisionStatusLabel.Text = BuildDecisionMessage(decision);
        LinkedRouteBenefitLabel.Text = "거절됨";
        StopRecommendationCountdown();
        _recommendationNotificationService.MarkHandled(_incomingRecommendationRequest.의뢰Id);
        LinkedRouteCard.IsVisible = false;
        RecommendationBanner.IsVisible = false;
        RecommendationDetailBanner.IsVisible = false;
        _incomingRecommendation = null;
        _incomingRecommendationRequest = null;
        RestoreDefaultMapState();
        StatusLabel.Text = "추천을 거절했습니다. 다른 추천이 들어오면 다시 알려드립니다.";
    }

    private async void OnCancelAcceptedRecommendationClicked(object? sender, EventArgs e)
    {
        var request = _acceptedRecommendationRequest ?? _incomingRecommendationRequest;
        if (request is null)
        {
            RecommendationDecisionStatusLabel.Text = "취소할 추천 의뢰를 찾지 못했습니다.";
            return;
        }

        var currentDecision = _decisionService.GetDecision(request.의뢰Id);
        if (currentDecision?.Decision != DriverRecommendationDecisionCode.Accepted)
        {
            RecommendationDecisionStatusLabel.Text = "수락한 추천만 취소할 수 있습니다.";
            return;
        }

        RecommendationDecisionButtons.IsEnabled = false;
        RecommendationDecisionState decision;
        try
        {
            decision = await _decisionService.CancelAcceptedAsync(request, "지도 화면에서 수락 취소");
        }
        catch (Exception ex)
        {
            RecommendationDecisionStatusLabel.Text = ex.Message;
            RecommendationDecisionButtons.IsEnabled = true;
            return;
        }

        RecommendationDecisionStatusLabel.Text = BuildDecisionMessage(decision);
        LinkedRouteBenefitLabel.Text = "재배차";
        StopRecommendationCountdown();
        _recommendationNotificationService.MarkHandled(request.의뢰Id);
        _acceptedRecommendationRequest = null;
        LinkedRouteCard.IsVisible = false;
        RecommendationBanner.IsVisible = false;
        RecommendationDetailBanner.IsVisible = false;
        _incomingRecommendation = null;
        _incomingRecommendationRequest = null;
        RestoreDefaultMapState();
        StatusLabel.Text = "수락을 취소했습니다. 서버에서는 재배차와 화주 알림이 필요한 상태로 처리됩니다.";
    }

    private static string BuildDecisionMessage(RecommendationDecisionState decision)
        => $"{decision.Decision} · {decision.DecidedAt:HH:mm} · {decision.Memo} / 서버 후속: {decision.FollowUpPlan.OperationalMemo}";

    private void RestoreDefaultMapState()
    {
        MapView.CenterLatitude = _defaultCenterLatitude;
        MapView.CenterLongitude = _defaultCenterLongitude;
        MapView.Zoom = DefaultMapZoom;
        MapView.Markers = _defaultRecommendationMarkers;
        MapView.RouteOverlays = [];
        TransportFooterBar.IsVisible = true;
        TransportFooterBar.ShowTransport(_currentTransport);
    }

    private void ShowAcceptedRecommendationOnMap()
    {
        if (_incomingRecommendation is null)
        {
            return;
        }

        MapView.Markers = [_incomingRecommendation];
        var acceptedRoute = _routePlanningService.BuildAcceptedRouteOverlays(_sampleDataService.기사현재위치, _currentTransport, _incomingRecommendation);
        var routeOverlays = acceptedRoute.Count > 0
            ? acceptedRoute
            : _routePlanningService.BuildLinkedRouteOverlays(_sampleDataService.기사현재위치, _currentTransport, _incomingRecommendation);
        FocusMapOnRoute(routeOverlays, _incomingRecommendation, reserveBottomSpace: true);
        MapView.RouteOverlays = routeOverlays;
        TransportFooterBar.IsVisible = false;
    }

    private static string BuildRecommendationCompactInfo(DriverRequestItem? request)
    {
        if (request is null)
        {
            return "운송 의뢰 상세 정보는 추천 상세 화면에서 확인할 수 있습니다.";
        }

        var fare = request.예상수익.HasValue
            ? $"예상수익 {request.예상수익.Value.ToString("N0", CultureInfo.CurrentCulture)}원"
            : "예상수익 미정";
        var distance = request.주행거리Km.HasValue
            ? $"주행 {request.주행거리Km.Value.ToString("0.0", CultureInfo.CurrentCulture)}km"
            : request.직선거리Km.HasValue
                ? $"직선 {request.직선거리Km.Value.ToString("0.0", CultureInfo.CurrentCulture)}km"
                : "거리 미정";

        return $"{request.추천업무유형표시} · {request.차량조건표시} · {request.시간조건표시} · {fare} · {distance}";
    }

    private void FocusMapOnRoute(
        IReadOnlyList<DriverMapRouteOverlay> routeOverlays,
        DriverMapMarkerItem recommendation,
        bool reserveBottomSpace = false)
    {
        var points = routeOverlays
            .SelectMany(x => x.Points)
            .Where(x => x.Latitude != 0d && x.Longitude != 0d)
            .ToList();

        if (points.Count == 0)
        {
            MapView.CenterLatitude = reserveBottomSpace
                ? recommendation.PickupLatitude + 0.015d
                : recommendation.PickupLatitude;
            MapView.CenterLongitude = recommendation.PickupLongitude;
            MapView.Zoom = reserveBottomSpace ? 12.5d : 13d;
            return;
        }

        var minLatitude = points.Min(x => x.Latitude);
        var maxLatitude = points.Max(x => x.Latitude);
        var minLongitude = points.Min(x => x.Longitude);
        var maxLongitude = points.Max(x => x.Longitude);
        var span = Math.Max(maxLatitude - minLatitude, maxLongitude - minLongitude);

        var latitudeSpan = maxLatitude - minLatitude;
        var centerLatitude = (minLatitude + maxLatitude) / 2d;
        if (reserveBottomSpace)
        {
            centerLatitude += Math.Max(latitudeSpan * 0.18d, 0.012d);
        }

        MapView.CenterLatitude = centerLatitude;
        MapView.CenterLongitude = (minLongitude + maxLongitude) / 2d;
        var zoom = span switch
        {
            <= 0.03d => 13.5d,
            <= 0.08d => 12.5d,
            <= 0.18d => 11.5d,
            <= 0.35d => 10.5d,
            <= 0.7d => 9.5d,
            _ => 8.5d
        };
        MapView.Zoom = reserveBottomSpace ? Math.Max(8d, zoom - 1.25d) : zoom;
    }

    private void OnIncomingRecommendationChanged(DriverIncomingRecommendation? incoming)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _incomingRecommendation = incoming?.Marker;
            _incomingRecommendationRequest = incoming?.Request;
            ShowIncomingRecommendation(incoming);
            if (incoming is not null)
            {
                RecommendationDetailBanner.IsVisible = false;
                LinkedRouteCard.IsVisible = false;
                RestoreDefaultMapState();
            }
        });
    }

    private void SubscribeEvents()
    {
        if (_isSubscribed)
        {
            return;
        }

        MapView.MarkerSelected += OnMarkerSelected;
        _recommendationNotificationService.Changed += OnIncomingRecommendationChanged;
        _operatingProfileService.Changed += OnOperatingProfileChanged;
        _isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_isSubscribed)
        {
            return;
        }

        MapView.MarkerSelected -= OnMarkerSelected;
        _recommendationNotificationService.Changed -= OnIncomingRecommendationChanged;
        _operatingProfileService.Changed -= OnOperatingProfileChanged;
        _isSubscribed = false;
    }

    private async void OnOpenLegacyMenuClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new MainPage());
    }

    private async void OnOpenCurrentTransportClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new MainPage());
    }
}
