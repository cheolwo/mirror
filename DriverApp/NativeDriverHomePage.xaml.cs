using DriverApp.Models.Driver.Samples;
using DriverApp.Models.Driver;
using DriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverOffers;

namespace DriverApp;

public partial class NativeDriverHomePage : ContentPage
{
    private readonly IDriverSampleDataService _sampleDataService;
    private readonly IDriverRecommendationNotificationService _recommendationNotificationService;
    private readonly DriverHomeRoutePlanningService _routePlanningService;
    private readonly I기사푸시토큰등록Service _pushTokenRegistrationService;
    private readonly DriverOperatingProfileService _operatingProfileService;
    private readonly DriverNativeHomeWorkspaceState _workspaceState;
    private readonly IServiceProvider _services;
    private readonly 기사위치표시설정Service _locationDisplayPreferences;
    private readonly DriverForegroundRecommendationSignal _recommendationSignal;
    private long _pageRefreshGeneration;
    private bool _isSubscribed;
    private DriverRequestItem? _incomingRecommendationRequest;
    private 기사운송샘플항목? _currentTransport;
    private double _defaultCenterLatitude = (double)DriverNativeLocationPolicy.DefaultCameraCenter.Latitude;
    private double _defaultCenterLongitude = (double)DriverNativeLocationPolicy.DefaultCameraCenter.Longitude;
    private const double DefaultMapZoom = 11d;
    private IDispatcherTimer? _recommendationCountdownTimer;
    private bool _autoRejectingRecommendation;
    private IDispatcherTimer? _workspaceRefreshTimer;
    private Window? _observedWindow;
    private bool _foreground;
    private bool _workspaceRefreshing;
    private bool _recommendationCommandBusy;

    public NativeDriverHomePage(
        IDriverSampleDataService sampleDataService,
        IDriverRecommendationNotificationService recommendationNotificationService,
        DriverHomeRoutePlanningService routePlanningService,
        I기사푸시토큰등록Service pushTokenRegistrationService,
        DriverOperatingProfileService operatingProfileService,
        DriverNativeHomeWorkspaceState workspaceState,
        IServiceProvider services,
        기사위치표시설정Service locationDisplayPreferences)
    {
        InitializeComponent();
        _sampleDataService = sampleDataService;
        _recommendationNotificationService = recommendationNotificationService;
        _routePlanningService = routePlanningService;
        _pushTokenRegistrationService = pushTokenRegistrationService;
        _operatingProfileService = operatingProfileService;
        _locationDisplayPreferences = locationDisplayPreferences;
        _workspaceState = workspaceState;
        _services = services;
        _recommendationSignal = new(services.GetRequiredService<IAuthSession>(),
            services.GetRequiredService<IDriverCargoRecommendationAlertService>());
        ApplyOperatingProfile();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _foreground = true;
        _recommendationSignal.Resume();
        ApplyDirectionSettings();
        SubscribeEvents();
        ObserveWindow();
        ApplyOperatingProfile();
        await RefreshCurrentWorkspaceAsync();
        StartWorkspaceRefresh();
    }

    private async Task RefreshCurrentWorkspaceAsync(bool periodic = false)
    {
        if (!_isSubscribed || !_foreground || _workspaceRefreshing || _recommendationCommandBusy) return;
        _workspaceRefreshing = true;
        var generation = ++_pageRefreshGeneration;
        var signalTicket = _recommendationSignal.BeginRefresh();
        try
        {
            TransportFooterBar.IsEnabled = false;
            ApplyReceptionControl();
            if (!_workspaceState.IsActive) await _workspaceState.ActivateAsync();
            else if (periodic) { if (!await _workspaceState.PollAsync()) return; }
            else await _workspaceState.RefreshAsync();
            if (!_isSubscribed || !_foreground || generation != _pageRefreshGeneration) return;
            ApplyCurrentWorkspaceDisplay();
            if (_workspaceState.CanRespondToRecommendation)
                _ = _recommendationSignal.TryPublishAsync(signalTicket, _incomingRecommendationRequest);
        }
        finally
        {
            _workspaceRefreshing = false;
            if (_isSubscribed && _foreground)
            {
                ApplyReceptionControl();
                if (generation != _pageRefreshGeneration && !_workspaceState.IsActive)
                    Dispatcher.Dispatch(async () => await RefreshCurrentWorkspaceAsync());
            }
        }
    }

    private void ApplyCurrentWorkspaceDisplay()
    {
        _currentTransport = _workspaceState.CurrentTransport;
        TransportFooterBar.ShowTransport(_currentTransport);
        TransportFooterBar.IsVisible = true;
        TransportFooterBar.IsEnabled = _workspaceState.IsReady;
        ApplyReceptionControl();
        if (!_workspaceState.IsReady)
        {
            StatusLabel.Text = _workspaceState.ErrorMessage;
            StatusLabel.IsVisible = true;
            RetryWorkspaceButton.IsVisible = true;
            RecommendationBanner.IsVisible = false;
            StopRecommendationCountdown();
            if (_currentTransport is null) { MapView.Markers = []; MapView.RouteOverlays = []; }
            else RestoreDefaultMapState(); // 확인 실패 시 추천 검토 경로를 현재 업무 경로로 오인하지 않게 합니다.
            return;
        }

        var currentLocation = _sampleDataService.기사현재위치;

        var cameraCenter = DriverNativeLocationPolicy.CameraCenter(currentLocation, DateTime.UtcNow);
        _defaultCenterLatitude = (double)cameraCenter.Latitude;
        _defaultCenterLongitude = (double)cameraCenter.Longitude;
        MapView.CurrentLocation = currentLocation;
        ShowLocationNotice(DriverNativeLocationPolicy.Present(currentLocation, DateTime.UtcNow));
        var incoming = ReadCanonicalIncoming();
        _incomingRecommendationRequest = incoming;
        RestoreDefaultMapState();

        var queryWarnings = _workspaceState.MapWarnings;
        RetryWorkspaceButton.IsVisible = queryWarnings.Count > 0 || _workspaceState.UnconfirmedAcceptanceRequestId is not null;
        StatusLabel.IsVisible = queryWarnings.Count > 0;
        StatusLabel.Text = string.Join("\n", queryWarnings.Take(2));
        if (_workspaceState.UnconfirmedAcceptanceRequestId is not null)
        { StatusLabel.Text = "수락 결과를 확인하고 있습니다. 현재 운송은 계속 진행할 수 있습니다."; StatusLabel.IsVisible = true; }
        ShowIncomingRecommendation(incoming);
    }

    protected override void OnDisappearing()
    {
        ++_pageRefreshGeneration;
        _foreground = false;
        _recommendationSignal.Pause();
        MapView.IsDirectionTrackingActive = false;
        StopWorkspaceRefresh();
        StopObservingWindow();
        _workspaceState.Pause();
        ClearCurrentWorkspaceDisplay();
        StopRecommendationCountdown();
        UnsubscribeEvents();
        base.OnDisappearing();
    }

    private void ObserveWindow()
    {
        if (ReferenceEquals(_observedWindow, Window)) return;
        StopObservingWindow(); _observedWindow = Window;
        if (_observedWindow is null) return;
        _observedWindow.Activated += OnWindowActivated;
        _observedWindow.Deactivated += OnWindowDeactivated;
        _observedWindow.Stopped += OnWindowDeactivated;
        _observedWindow.Resumed += OnWindowActivated;
    }

    private void StopObservingWindow()
    {
        if (_observedWindow is null) return;
        _observedWindow.Activated -= OnWindowActivated;
        _observedWindow.Deactivated -= OnWindowDeactivated;
        _observedWindow.Stopped -= OnWindowDeactivated;
        _observedWindow.Resumed -= OnWindowActivated;
        _observedWindow = null;
    }

    private async void OnWindowActivated(object? sender, EventArgs args)
    {
        if (!_isSubscribed || _foreground) return;
        _foreground = true;
        _recommendationSignal.Resume();
        ApplyDirectionSettings();
        await RefreshCurrentWorkspaceAsync(); StartWorkspaceRefresh();
    }

    private void OnWindowDeactivated(object? sender, EventArgs args)
    {
        if (!_isSubscribed || !_foreground) return;
        _foreground = false; ++_pageRefreshGeneration;
        _recommendationSignal.Pause();
        MapView.IsDirectionTrackingActive = false;
        StopWorkspaceRefresh(); StopRecommendationCountdown(); _workspaceState.Pause();
        TransportFooterBar.IsEnabled = false;
        ApplyReceptionControl();
    }

    private void StartWorkspaceRefresh()
    {
        if (!_isSubscribed || !_foreground || _workspaceRefreshTimer is not null) return;
        _workspaceRefreshTimer = Dispatcher.CreateTimer();
        _workspaceRefreshTimer.Interval = DriverNativeHomeWorkspaceState.RefreshInterval;
        _workspaceRefreshTimer.Tick += async (_, _) => await RefreshCurrentWorkspaceAsync(periodic: true);
        _workspaceRefreshTimer.Start();
        _ = RegisterStoredPushTokenQuietlyAsync();
    }

    private void StopWorkspaceRefresh()
    { _workspaceRefreshTimer?.Stop(); _workspaceRefreshTimer = null; }

    private void ApplyDirectionSettings()
    {
        MapView.ShowDirectionArrow = _locationDisplayPreferences.방향화살표표시;
        MapView.IsDirectionTrackingActive = _foreground;
    }

    private void OnDirectionSettingsChanged()
        => Dispatcher.Dispatch(() => { if (_isSubscribed && _foreground) ApplyDirectionSettings(); });

    private DriverRequestItem? ReadCanonicalIncoming()
    {
        if (!_workspaceState.CanRespondToRecommendation) return null;
        var prior = _recommendationNotificationService.GetCurrent();
        var offers = _sampleDataService.추천의뢰목록.Where(x => DriverRecommendationSelectionState.IsLive(x, DateTime.UtcNow)).ToArray();
        return offers.FirstOrDefault(x => prior is not null && x.의뢰Id == prior.Request.의뢰Id && x.추천라운드 == prior.Request.추천라운드)
            ?? offers.FirstOrDefault();
    }

    private void ApplyReceptionControl()
    {
        ReceptionSwitch.IsToggled = _workspaceState.ReceivesNewDispatches;
        ReceptionSwitch.IsEnabled = _foreground && !_workspaceRefreshing && _workspaceState.CanChangeReception && !_recommendationCommandBusy;
        ReceptionStateLabel.Text = _workspaceState.IsChangingIntent ? "변경 중" : !_workspaceState.ReceptionKnown ? "확인 필요"
            : _workspaceState.AutomaticallyPaused ? "미응답으로 중지" : _workspaceState.ReceivesNewDispatches ? "켜짐" : "꺼짐";
        ReceptionNoticeLabel.Text = _workspaceState.ReceptionNotice;
        ReceptionNoticeLabel.IsVisible = !string.IsNullOrWhiteSpace(ReceptionNoticeLabel.Text);
    }

    private async void OnReceptionToggled(object? sender, ToggledEventArgs args)
    {
        if (!_foreground || !_workspaceState.CanChangeReception || args.Value == _workspaceState.ReceivesNewDispatches) return;
        var generation = ++_pageRefreshGeneration;
        var signalTicket = _recommendationSignal.BeginRefresh();
        var task = _workspaceState.ToggleReceptionAsync(); ApplyReceptionControl();
        await task;
        if (_isSubscribed && _foreground && generation == _pageRefreshGeneration)
        {
            ApplyCurrentWorkspaceDisplay();
            if (_workspaceState.CanRespondToRecommendation)
                _ = _recommendationSignal.TryPublishAsync(signalTicket, _incomingRecommendationRequest);
        }
    }

    private async void OnRetryWorkspaceClicked(object? sender, EventArgs e)
        => await RefreshCurrentWorkspaceAsync();

    private void OnWorkspaceInvalidated()
        => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_isSubscribed || _workspaceState.IsReady) return;
            ++_pageRefreshGeneration;
            ClearCurrentWorkspaceDisplay();
            StatusLabel.Text = _workspaceState.ErrorMessage;
            StatusLabel.IsVisible = true;
            RetryWorkspaceButton.IsVisible = true;
        });

    private void ClearCurrentWorkspaceDisplay()
    {
        _recommendationSignal.Invalidate();
        _recommendationCommandBusy = false;
        StopRecommendationCountdown();
        _currentTransport = null;
        _incomingRecommendationRequest = null;
        MapView.CurrentLocation = null;
        _defaultCenterLatitude = (double)DriverNativeLocationPolicy.DefaultCameraCenter.Latitude;
        _defaultCenterLongitude = (double)DriverNativeLocationPolicy.DefaultCameraCenter.Longitude;
        MapView.CenterLatitude = _defaultCenterLatitude;
        MapView.CenterLongitude = _defaultCenterLongitude;
        MapProviderLabel.Text = "NAVER 지도 · Directions";
        MapView.Markers = [];
        MapView.RouteOverlays = [];
        TransportFooterBar.ShowTransport(null);
        TransportFooterBar.IsVisible = false;
        RecommendationBanner.IsVisible = false;
    }

    private void OnOperatingProfileChanged()
        => MainThread.BeginInvokeOnMainThread(ApplyOperatingProfile);

    private void ApplyOperatingProfile()
    {
        var profile = _operatingProfileService.Current;
        MapView.MapProviderCode = profile.MapProviderCode;
        OperatingMarketLabel.Text = "대한민국 운행";
        MapProviderLabel.Text = "NAVER 지도 · Directions";
        if (_workspaceState.IsReady)
            ShowLocationNotice(DriverNativeLocationPolicy.Present(MapView.CurrentLocation, DateTime.UtcNow));
    }

    private void OnLocationPresentationChanged(object? sender, DriverNativeLocationPresentation presentation)
    {
        if (_isSubscribed && _workspaceState.IsReady) ShowLocationNotice(presentation);
    }

    private void ShowLocationNotice(DriverNativeLocationPresentation presentation)
        => MapProviderLabel.Text = $"NAVER 지도 · Directions\n{presentation.Notice}";

    private 기사현재위치샘플 RouteCurrentLocation()
        => DriverNativeLocationPolicy.ForCurrentRoute(MapView.CurrentLocation ?? _sampleDataService.기사현재위치, DateTime.UtcNow);

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

    private void ShowIncomingRecommendation(DriverRequestItem? request)
    {
        RecommendationBanner.IsVisible = request is not null && _workspaceState.CanRespondToRecommendation;
        if (request is null)
        {
            RecommendationCountdownLabel.Text = string.Empty;
            RecommendationCountdownProgress.Progress = 0d;
            StopRecommendationCountdown(); return;
        }
        var visual = request.OfferVisual;
        RecommendationWorkIcon.Data = (Microsoft.Maui.Controls.Shapes.Geometry)
            new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(visual.IconPath)!;
        RecommendationPrimaryBar.Color = Color.FromArgb(visual.PrimaryColor);
        RecommendationSecondaryBar.IsVisible = visual.IsCombined;
        RecommendationSecondaryBar.Color = Color.FromArgb(visual.SecondaryColor ?? DriverOfferPresentation.WarehouseColor);
        var count = _sampleDataService.추천의뢰목록.Count(x => DriverRecommendationSelectionState.IsLive(x, DateTime.UtcNow));
        RecommendationWorkLabel.Text = $"{visual.Label} · 새 추천 {count}건";
        RecommendationActivityLabel.Text = visual.ActivityText + " · 눌러서 한 건씩 확인";
        StopRecommendationCountdown(); _autoRejectingRecommendation = false;
        _recommendationCountdownTimer = Dispatcher.CreateTimer();
        _recommendationCountdownTimer.Interval = TimeSpan.FromSeconds(1);
        _recommendationCountdownTimer.Tick += async (_, _) => await UpdateRecommendationCountdownAsync(request);
        _recommendationCountdownTimer.Start();
        _ = UpdateRecommendationCountdownAsync(request);
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

    private async Task UpdateRecommendationCountdownAsync(DriverRequestItem request)
    {
        var expiresAtUtc = DriverRecommendationSelectionState.NormalizeServerUtc(request.추천만료시각!.Value);
        var startsAtUtc = DriverRecommendationSelectionState.NormalizeServerUtc(request.추천시작시각!.Value);
        var total = Math.Max(1d, (expiresAtUtc - startsAtUtc).TotalSeconds);
        var remaining = Math.Max(0d, (expiresAtUtc - DateTime.UtcNow).TotalSeconds);
        RecommendationCountdownLabel.Text = remaining > 0 ? $"{Math.Ceiling(remaining):0}초" : "현재 상태 확인 중";
        RecommendationCountdownProgress.Progress = Math.Clamp((total - remaining) / total, 0d, 1d);
        if (remaining > 0 || _autoRejectingRecommendation || !_foreground || !_isSubscribed) return;
        _autoRejectingRecommendation = true;
        StopRecommendationCountdown();
        await RefreshCurrentWorkspaceAsync(); // 만료는 서버 재조회만 하며 거절 명령을 만들지 않습니다.
    }

    private async void OnOpenIncomingRecommendationClicked(object? sender, EventArgs e)
    {
        if (_incomingRecommendationRequest is not { } request || !_workspaceState.CanRespondToRecommendation
            || _recommendationCommandBusy) return;
        _recommendationCommandBusy = true;
        try
        {
            var page = _services.GetRequiredService<NativeDriverRecommendationPage>();
            page.Prefer(request.의뢰Id, request.추천라운드);
            await Navigation.PushAsync(page);
        }
        finally { _recommendationCommandBusy = false; }
    }

    private void RestoreDefaultMapState()
    {
        MapView.CenterLatitude = _defaultCenterLatitude;
        MapView.CenterLongitude = _defaultCenterLongitude;
        MapView.Zoom = DefaultMapZoom;
        var currentMarker = _routePlanningService.CurrentMarker(_currentTransport);
        MapView.Markers = currentMarker is null ? [] : [currentMarker];
        MapView.RouteOverlays = _routePlanningService.BuildCurrentRouteOverlays(RouteCurrentLocation(), _currentTransport);
        TransportFooterBar.IsVisible = true;
        TransportFooterBar.ShowTransport(_currentTransport);
        TransportFooterBar.IsEnabled = _workspaceState.IsReady;
    }

    private void OnIncomingRecommendationChanged(DriverIncomingRecommendation? incoming)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_isSubscribed || !_foreground || !_workspaceState.IsReady || _recommendationCommandBusy) return;
            // 푸시는 식별 힌트입니다. 새 메시지만으로 현재 수행이나 검토 중인 경로를 교체하지 않습니다.
            _ = RefreshCurrentWorkspaceAsync();
        });
    }

    private void SubscribeEvents()
    {
        if (_isSubscribed)
        {
            return;
        }

        MapView.LocationPresentationChanged += OnLocationPresentationChanged;
        _recommendationNotificationService.Changed += OnIncomingRecommendationChanged;
        _operatingProfileService.Changed += OnOperatingProfileChanged;
        _locationDisplayPreferences.Changed += OnDirectionSettingsChanged;
        _workspaceState.Invalidated += OnWorkspaceInvalidated;
        _isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_isSubscribed)
        {
            return;
        }

        MapView.LocationPresentationChanged -= OnLocationPresentationChanged;
        _recommendationNotificationService.Changed -= OnIncomingRecommendationChanged;
        _operatingProfileService.Changed -= OnOperatingProfileChanged;
        _locationDisplayPreferences.Changed -= OnDirectionSettingsChanged;
        _workspaceState.Invalidated -= OnWorkspaceInvalidated;
        _isSubscribed = false;
    }

    private async void OnOpenLegacyMenuClicked(object? sender, EventArgs e)
    {
        await OpenBlazorWorkspaceAsync(DriverRoutes.Menu);
    }

    private async void OnOpenCurrentTransportClicked(object? sender, EventArgs e)
    {
        await OpenBlazorWorkspaceAsync(DriverRoutes.CurrentTransport);
    }
    private async void OnOpenDistributedRoundClicked(object? sender, EventArgs e)
    {
        await OpenBlazorWorkspaceAsync("/driver/distributed/round");
    }

    private async Task OpenBlazorWorkspaceAsync(string route)
    {
        if (Navigation.NavigationStack.Count > 1
            && Navigation.NavigationStack[^2] is MainPage workspace)
        {
            await Navigation.PopAsync();
            // Android의 숨겨진 WebView는 dispatch를 처리하지 않을 수 있습니다.
            // 먼저 기존 업무 화면을 복원한 다음 살아 있는 Blazor 범위에 전달합니다.
            await workspace.OpenRouteAsync(route);
            return;
        }

        await Navigation.PushAsync(new MainPage(route));
    }
}
