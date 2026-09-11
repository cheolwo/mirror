using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel : ObservableObject
{
    private const string DrivingStatus = "운행중";
    private static readonly TimeSpan WorkspaceRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LocationHeartbeatInterval = TimeSpan.FromMinutes(2);
    private readonly FDriverAppProfile _profile;
    private readonly IFDriverAuthSession _authSession;
    private readonly FDriverAuthApiService _authApi;
    private readonly IFoodDeliveryDriverApiService _api;
    private readonly IFDriverLocationService _locationService;
    private readonly IFDriverDispatchRealtimeService _realtimeService;
    private bool _initialized;
    private bool _monitorRefreshInProgress;
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private DateTime? _lastLocationSentAtUtc;
    private readonly HashSet<string> _knownRecommendedTicketIds = new(StringComparer.Ordinal);

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private bool _isOnDuty;
    [ObservableProperty] private string _loginId = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _today = DateTime.Now.ToString("M월 d일 dddd");
    [ObservableProperty] private string _statusMessage = "기사 로그인 후 배달 업무를 시작할 수 있습니다.";
    [ObservableProperty] private string _currentArea = "현재 위치 확인 전";
    [ObservableProperty] private int _pendingDeliveryTickets;
    [ObservableProperty] private int _recommendedTickets;
    [ObservableProperty] private decimal _todayExpectedPayout;
    [ObservableProperty] private DeliveryTicketPreview? _selectedTicket;
    [ObservableProperty] private ActiveDeliveryPreview? _activeDelivery;
    [ObservableProperty] private string _workStage = "추천 대기";
    [ObservableProperty] private string _nextActionGuide = "운행을 시작하면 현재 위치 기준 추천을 확인합니다.";
    [ObservableProperty] private string _settlementText = "이번 달 정산 조회 전";
    [ObservableProperty] private string _routeStatusText = "경로 조회 전";
    [ObservableProperty] private string _workspaceSyncText = "업무 자동 갱신 대기 · 30초 주기";
    [ObservableProperty] private string _realtimeConnectionText = "실시간 배차 연결 대기 · 30초 자동 조회 보조";
    [ObservableProperty] private string _locationSyncText = "기사 위치 전송 대기";
    [ObservableProperty] private bool _dispatchAutomationEnabled;
    [ObservableProperty] private int _maxActiveDeliveries;
    [ObservableProperty] private string _dispatchAutomationNotice = "자동 배차 상태 확인 전";
    [ObservableProperty] private IReadOnlyList<DriverMapMarkerItem> _mapMarkers = [];
    [ObservableProperty] private IReadOnlyList<DriverMapRouteOverlay> _selectedRouteOverlays = [];
    [ObservableProperty] private double _mapCenterLatitude = 37.5665d;
    [ObservableProperty] private double _mapCenterLongitude = 126.9780d;
    [ObservableProperty] private double _currentLocationLatitude;
    [ObservableProperty] private double _currentLocationLongitude;
    [ObservableProperty] private bool _hasCurrentLocation;
    [ObservableProperty] private bool _hasNewRecommendations;
    [ObservableProperty] private string _newRecommendationNotice = "새 추천 배차가 도착했습니다.";

    public MainPageModel(
        FDriverAppProfile profile,
        IFDriverAuthSession authSession,
        FDriverAuthApiService authApi,
        IFoodDeliveryDriverApiService api,
        IFDriverLocationService locationService,
        IFDriverDispatchRealtimeService realtimeService)
    {
        _profile = profile;
        _authSession = authSession;
        _authApi = authApi;
        _api = api;
        _locationService = locationService;
        _realtimeService = realtimeService;
        _realtimeService.RecommendationsReceived += OnRealtimeRecommendationsReceivedAsync;
        _realtimeService.StatusChanged += OnRealtimeStatusChanged;
    }

    public string AppName => _profile.DisplayName;
    public string DriverRole => _profile.DriverRole;
    public bool IsSignedOut => !IsAuthenticated;
    public string SignedInUserText => string.IsNullOrWhiteSpace(_authSession.UserName)
        ? DriverRole
        : $"{_authSession.UserName} · {DriverRole}";
    public string WorkToggleText => IsOnDuty ? "추천 대기 종료" : "운행 시작";
    public string WorkToggleColor => IsOnDuty ? "#B91C1C" : "#0F766E";
    public ObservableCollection<DeliveryTicketPreview> RecommendedTicketItems { get; } = [];
    public ObservableCollection<ActiveDeliveryPreview> ActiveDeliveryItems { get; } = [];
    public ObservableCollection<FoodDeliveryBundlePreview> BundleCandidateItems { get; } = [];

    public string PickupPointText => CurrentRouteOffer is null
        ? "음식점 픽업지 없음"
        : $"픽업: {CurrentRouteOffer.Pickup.Label} · {CurrentRouteOffer.Pickup.Address}";
    public string DropoffPointText => CurrentRouteOffer is null
        ? "고객 전달지 없음"
        : $"전달: {CurrentRouteOffer.Dropoff.Address}";
    public string SelectedRouteText => CurrentRouteOffer is null
        ? "선택된 배달권 없음"
        : $"{CurrentRouteOffer.Pickup.Label} → {CurrentRouteOffer.Dropoff.Label}";
    public string ActiveWorkSummary => ActiveDeliveryItems.Count switch
    {
        0 => "진행 중인 배달 없음",
        1 => $"{ActiveDeliveryItems[0].OrderSummary} · {WorkStage}",
        _ => $"묶음 배달 {ActiveDeliveryItems.Count}건 · {WorkStage}"
    };
    public string ActiveWorkRouteText => ActiveDelivery is null
        ? "수락한 배달권이 없습니다."
        : $"{ActiveDelivery.Pickup.Label} → {ActiveDelivery.Dropoff.Label}";
    public string ActiveWorkPayoutText => ActiveDeliveryItems.Count == 0
        ? "정산 예정 없음"
        : $"{ActiveDeliveryItems.Sum(x => x.DriverPayout).ToString("N0", CultureInfo.CurrentCulture)}원";
    public bool HasActiveWork => ActiveDelivery is not null;
    public bool CanConfirmPickup => !IsBusy && ActiveDelivery?.WorkStatus == DriverWorkOfferStatus.MovingToPickup;
    public bool CanCompleteDelivery => !IsBusy && ActiveDelivery?.WorkStatus == DriverWorkOfferStatus.MovingToDropoff;
    public bool CanAcceptSelectedTicket => !IsBusy
                                           && SelectedTicket is { IsExpired: false }
                                           && MaxActiveDeliveries > 0
                                           && ActiveDeliveryItems.Count < MaxActiveDeliveries;
    public bool HasBundleCandidates => BundleCandidateItems.Count > 0;
    public bool HasMultipleActiveDeliveries => ActiveDeliveryItems.Count > 1;

    private DriverWorkOfferDto? CurrentRouteOffer
        => ActiveDelivery?.ToDriverWorkOffer(_profile) ?? SelectedTicket?.ToDriverWorkOffer(_profile);

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            if (IsAuthenticated)
            {
                await ReloadAsync(updateLocation: IsOnDuty);
            }

            return;
        }

        _initialized = true;
        var restoreState = await _authSession.RestoreAsync();
        if (restoreState == Ssalddel.Client.Infrastructure.Security.ClientAuthSessionRestoreState.RefreshRequired)
        {
            var refreshError = await _authApi.EnsureAccessTokenAsync();
            if (refreshError is not null)
            {
                StatusMessage = refreshError;
            }
        }

        IsAuthenticated = _authSession.IsAuthenticated;
        if (IsAuthenticated)
        {
            await ReloadAsync(updateLocation: true);
        }
    }

    public async Task StartMonitoringAsync()
    {
        if (!IsAuthenticated)
        {
            return;
        }

        if (_monitorTask is not { IsCompleted: false })
        {
            _monitorCancellation?.Dispose();
            _monitorCancellation = new CancellationTokenSource();
            _monitorTask = MonitorWorkspaceAsync(_monitorCancellation.Token);
        }

        await _realtimeService.StartAsync();
    }

    public async Task StopMonitoringAsync()
    {
        _monitorCancellation?.Cancel();
        _monitorCancellation?.Dispose();
        _monitorCancellation = null;
        _monitorTask = null;
        await _realtimeService.StopAsync();
    }

    public void ApplyEntryFocus(string? focus)
    {
        StatusMessage = focus?.Trim().ToLowerInvariant() switch
        {
            "restaurant" => "음식점 픽업 정보를 확인하세요. 선택한 배달권의 음식점 위치와 픽업 경로를 지도에 표시합니다.",
            "dispatch" => "배차 추천을 확인하세요. 지도 마커나 추천 배달권을 선택하면 경로가 이어집니다.",
            "delivery" => "운송·배달 흐름을 확인하세요. 픽업 확인부터 주문자 전달 완료까지 현재 단계를 이어서 처리합니다.",
            "customer" => "주문자 전달 정보를 확인하세요. 선택한 배달권의 전달 위치와 도착 경로를 지도에 표시합니다.",
            "bundle" => "묶음 배달 후보를 확인하세요. 동선과 예상 정산을 비교한 뒤 한 묶음만 선택할 수 있습니다.",
            "route" => "현재 위치와 픽업·전달 경로를 확인하세요. 선택한 배달권의 실제 도로 경로를 우선 표시합니다.",
            "settlement" => $"정산 현황을 확인하세요. {SettlementText}",
            "workspace" => "음식 배달 업무 공간을 열었습니다. 배차부터 전달 완료까지 한 흐름으로 처리합니다.",
            _ => StatusMessage
        };
    }

    [RelayCommand]
    private async Task Login()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "기사 계정을 확인하고 있습니다.";
        var error = await _authApi.LoginAsync(LoginId, Password);
        if (error is not null)
        {
            StatusMessage = error;
            IsBusy = false;
            return;
        }

        Password = string.Empty;
        IsAuthenticated = true;
        IsBusy = false;
        await ReloadAsync(updateLocation: true);
        await StartMonitoringAsync();
    }

    [RelayCommand]
    private async Task Logout()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        NotifyCommandState();
        try
        {
            if (IsOnDuty)
            {
                await _api.StopWorkAsync();
            }

            await StopMonitoringAsync();
            await _authSession.ClearAsync();
            IsAuthenticated = false;
            IsOnDuty = false;
            ClearWorkspace();
            StatusMessage = "추천 대기를 종료하고 로그아웃했습니다.";
        }
        catch (FDriverApiException ex)
        {
            StatusMessage = $"추천 대기를 종료하지 못해 로그아웃을 보류했습니다. {ShortMessage(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandState();
        }
    }

    [RelayCommand]
    private void OpenProfile()
    {
        StatusMessage = $"로그인 기사: {SignedInUserText}";
    }

    [RelayCommand]
    private void OpenCurrentDelivery()
    {
        StatusMessage = ActiveDelivery is null
            ? "진행 중인 음식 배달이 없습니다."
            : $"{ActiveDelivery.OfferId} 현재 단계: {WorkStage}";
    }

    [RelayCommand]
    private async Task OpenNewRecommendations()
    {
        HasNewRecommendations = false;
        var ticket = RecommendedTicketItems.FirstOrDefault(x => !x.IsExpired)
                     ?? RecommendedTicketItems.FirstOrDefault();
        if (ticket is null)
        {
            StatusMessage = "확인할 새 추천 배차가 없습니다.";
            return;
        }

        await SelectTicket(ticket);
        StatusMessage = $"{ticket.TicketId} 새 추천 배차를 지도에 표시했습니다. 경로를 확인한 뒤 수락하거나 거절해 주세요.";
    }

    [RelayCommand]
    private async Task ToggleWork()
    {
        if (!IsAuthenticated || IsBusy)
        {
            return;
        }

        await RunApiAsync(async () =>
        {
            if (IsOnDuty)
            {
                await _api.StopWorkAsync();
                IsOnDuty = false;
                StatusMessage = "운행을 종료했습니다. 진행 중 배달은 계속 확인할 수 있습니다.";
            }
            else
            {
                var location = await CaptureLocationAsync(sendToServer: false);
                var startLocation = location is null
                    ? "음식 배달 앱 운행 시작"
                    : FormattableString.Invariant($"{location.Latitude:0.000000},{location.Longitude:0.000000}");
                await _api.StartWorkAsync(startLocation);
                IsOnDuty = true;
                await CaptureLocationAsync(sendToServer: true);
                StatusMessage = "운행을 시작했습니다. 현재 위치 기준 추천을 불러왔습니다.";
            }

            NotifyWorkState();
            await LoadWorkspaceAsync();
        });
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (!IsAuthenticated || IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        await ReloadAsync(updateLocation: IsOnDuty);
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task SelectTicket(DeliveryTicketPreview ticket)
    {
        SelectedTicket = ticket;
        StatusMessage = $"{ticket.TicketId} 배달권을 선택했습니다.";
        await RefreshRouteAsync();
    }

    public Task SelectTicketByIdAsync(string ticketId)
    {
        var ticket = RecommendedTicketItems.FirstOrDefault(x => string.Equals(x.TicketId, ticketId, StringComparison.Ordinal));
        if (ticket is not null)
        {
            return SelectTicket(ticket);
        }

        var active = ActiveDeliveryItems.FirstOrDefault(
            x => string.Equals(x.OfferId, ticketId, StringComparison.Ordinal));
        return active is null ? Task.CompletedTask : SelectActiveDelivery(active);
    }

    [RelayCommand]
    private async Task AcceptTicket(DeliveryTicketPreview ticket)
    {
        if (ticket.IsExpired)
        {
            StatusMessage = "응답 시간이 지난 배달권입니다. 새 추천을 확인해 주세요.";
            return;
        }

        SelectedTicket = ticket;
        await AcceptOfferAsync(ticket.TicketId);
    }

    [RelayCommand]
    private async Task RejectTicket(DeliveryTicketPreview ticket)
    {
        await RunApiAsync(async () =>
        {
            var result = await _api.RejectAsync(ticket.TicketId);
            StatusMessage = result.Message;
            await LoadWorkspaceAsync();
        });
    }

    [RelayCommand]
    private async Task SelectActiveDelivery(ActiveDeliveryPreview delivery)
    {
        ActiveDelivery = delivery;
        StatusMessage = $"{delivery.OrderSummary} 진행 배달을 선택했습니다.";
        SetWorkStage();
        await RefreshRouteAsync();
    }

    [RelayCommand]
    private async Task AcceptSelectedTicket()
    {
        if (SelectedTicket is null)
        {
            StatusMessage = "선택된 배달권이 없습니다.";
            return;
        }

        await AcceptOfferAsync(SelectedTicket.TicketId);
    }

    [RelayCommand]
    private async Task AcceptBundle(FoodDeliveryBundlePreview bundle)
    {
        await RunApiAsync(async () =>
        {
            var result = await _api.AcceptBundleAsync(bundle.OfferIds);
            StatusMessage = result.Message;
            await LoadWorkspaceAsync();
        });
    }

    [RelayCommand]
    private async Task ConfirmPickup()
    {
        if (ActiveDelivery is null)
        {
            StatusMessage = "진행 중인 배달이 없습니다.";
            return;
        }

        await RunApiAsync(async () =>
        {
            var result = await _api.ConfirmPickupAsync(ActiveDelivery.OfferId);
            StatusMessage = result.Message;
            await LoadWorkspaceAsync();
        });
    }

    [RelayCommand]
    private async Task CompleteDelivery()
    {
        if (ActiveDelivery is null)
        {
            StatusMessage = "진행 중인 배달이 없습니다.";
            return;
        }

        await RunApiAsync(async () =>
        {
            var result = await _api.CompleteAsync(ActiveDelivery.OfferId);
            StatusMessage = result.Message;
            await LoadWorkspaceAsync();
        });
    }

    private async Task AcceptOfferAsync(string offerId)
    {
        var ticket = RecommendedTicketItems.FirstOrDefault(x => x.TicketId == offerId);
        if (ticket?.IsExpired == true)
        {
            StatusMessage = "응답 시간이 지난 배달권입니다. 새 추천을 확인해 주세요.";
            return;
        }

        await RunApiAsync(async () =>
        {
            var result = await _api.AcceptAsync(offerId);
            StatusMessage = result.Message;
            await LoadWorkspaceAsync();
        });
    }

    private async Task ReloadAsync(bool updateLocation)
    {
        await RunApiAsync(async () =>
        {
            var workStatus = await _api.GetWorkStatusAsync();
            IsOnDuty = string.Equals(workStatus?.Status, DrivingStatus, StringComparison.OrdinalIgnoreCase);
            NotifyWorkState();
            if (updateLocation && IsOnDuty)
            {
                await CaptureLocationAsync(sendToServer: true);
            }

            await LoadWorkspaceAsync();
        });
    }

    private async Task LoadWorkspaceAsync(bool refreshRoute = true)
    {
        var workspace = await _api.GetWorkspaceAsync();
        var selectedTicketId = SelectedTicket?.TicketId;
        var activeOfferId = ActiveDelivery?.OfferId;
        var incomingRecommendationIds = workspace.Recommendations
            .Select(x => x.OfferId)
            .Where(x => !_knownRecommendedTicketIds.Contains(x))
            .ToArray();
        RecommendedTicketItems.Clear();
        foreach (var item in workspace.Recommendations)
        {
            RecommendedTicketItems.Add(DeliveryTicketPreview.From(item));
        }
        _knownRecommendedTicketIds.UnionWith(workspace.Recommendations.Select(x => x.OfferId));
        if (incomingRecommendationIds.Length > 0)
        {
            NewRecommendationNotice = incomingRecommendationIds.Length == 1
                ? "새 추천 배차 1건이 도착했습니다."
                : $"새 추천 배차 {incomingRecommendationIds.Length}건이 도착했습니다.";
            HasNewRecommendations = true;
        }

        ActiveDeliveryItems.Clear();
        foreach (var item in workspace.ActiveDeliveries)
        {
            ActiveDeliveryItems.Add(ActiveDeliveryPreview.From(item));
        }

        BundleCandidateItems.Clear();
        foreach (var item in workspace.BundleCandidates)
        {
            BundleCandidateItems.Add(FoodDeliveryBundlePreview.From(item));
        }

        ActiveDelivery = activeOfferId is null
            ? ActiveDeliveryItems.FirstOrDefault()
            : ActiveDeliveryItems.FirstOrDefault(x => x.OfferId == activeOfferId)
              ?? ActiveDeliveryItems.FirstOrDefault();
        SelectedTicket = selectedTicketId is null
            ? RecommendedTicketItems.FirstOrDefault()
            : RecommendedTicketItems.FirstOrDefault(x => x.TicketId == selectedTicketId)
              ?? RecommendedTicketItems.FirstOrDefault();
        PendingDeliveryTickets = RecommendedTicketItems.Count + ActiveDeliveryItems.Count;
        RecommendedTickets = RecommendedTicketItems.Count;
        TodayExpectedPayout = RecommendedTicketItems.Sum(x => x.DriverPayout);
        SettlementText = $"{workspace.Settlement.년도}년 {workspace.Settlement.월}월 · "
                         + $"배차 {workspace.Settlement.배차건수:N0}건 · 이용료 {workspace.Settlement.이용료:N0}원"
                          + (workspace.Settlement.결제완료 ? " · 납부 완료" : string.Empty);
        DispatchAutomationEnabled = workspace.DispatchAutomationEnabled;
        MaxActiveDeliveries = workspace.MaxActiveDeliveries;
        DispatchAutomationNotice = workspace.DispatchAutomationNotice;
        WorkspaceSyncText = $"업무 동기화 {workspace.UpdatedAtUtc.ToLocalTime():HH:mm:ss} · 다음 자동 갱신 30초 이내";
        MapMarkers = RecommendedTicketItems.Select(ToMapMarker)
            .Concat(ActiveDeliveryItems.Select(ToMapMarker))
            .ToArray();
        SetWorkStage();
        NotifyWorkspaceState();
        UpdateRecommendationCountdowns();
        if (refreshRoute)
        {
            await RefreshRouteAsync();
        }

        if (string.IsNullOrWhiteSpace(StatusMessage) || StatusMessage.Contains("불러오", StringComparison.Ordinal))
        {
            StatusMessage = $"추천 {RecommendedTicketItems.Count}건, 진행 {ActiveDeliveryItems.Count}건을 확인했습니다.";
        }
    }

    private async Task<FDriverLocationSnapshot?> CaptureLocationAsync(bool sendToServer)
    {
        var location = await _locationService.GetCurrentAsync();
        if (location is null)
        {
            CurrentArea = "위치 권한 또는 GPS 확인 필요";
            return null;
        }

        CurrentLocationLatitude = (double)location.Latitude;
        CurrentLocationLongitude = (double)location.Longitude;
        HasCurrentLocation = true;
        MapCenterLatitude = CurrentLocationLatitude;
        MapCenterLongitude = CurrentLocationLongitude;
        CurrentArea = FormattableString.Invariant($"현재 위치 {location.Latitude:0.0000}, {location.Longitude:0.0000}");
        if (sendToServer)
        {
            await _api.UpdateLocationAsync(new 기사위치갱신요청
            {
                AppKey = _profile.AppKey,
                위도 = location.Latitude,
                경도 = location.Longitude,
                정확도_m = location.AccuracyMeters,
                상차접근허용반경Km = 3m,
                운행상태 = DrivingStatus,
                기록시각 = location.RecordedAtUtc
            });
            _lastLocationSentAtUtc = DateTime.UtcNow;
            LocationSyncText = $"기사 위치 전송 {_lastLocationSentAtUtc.Value.ToLocalTime():HH:mm:ss}";
        }

        return location;
    }

    private async Task RefreshRouteAsync()
    {
        var routeStops = BuildRouteStops();
        if (routeStops.Count == 0)
        {
            SelectedRouteOverlays = [];
            RouteStatusText = "표시할 경로가 없습니다.";
            RefreshRouteLabels();
            return;
        }

        var startLatitude = HasCurrentLocation
            ? (decimal)CurrentLocationLatitude
            : routeStops[0].Latitude;
        var startLongitude = HasCurrentLocation
            ? (decimal)CurrentLocationLongitude
            : routeStops[0].Longitude;
        var stops = HasCurrentLocation ? routeStops : routeStops.Skip(1).ToArray();
        if (stops.Count == 0)
        {
            SelectedRouteOverlays = [];
            return;
        }

        try
        {
            var route = await _api.GetRouteAsync(new FoodDeliveryDriverRouteRequestDto
            {
                StartLatitude = startLatitude,
                StartLongitude = startLongitude,
                Stops = stops
            });
            SelectedRouteOverlays = route.Points.Count < 2
                ? []
                :
                [
                    new DriverMapRouteOverlay(
                        ActiveDelivery?.OfferId ?? SelectedTicket?.TicketId ?? "food-route",
                        "음식 배달 경로",
                        route.Points.Select((x, index) => new DriverMapRoutePoint(
                            (double)x.Latitude,
                            (double)x.Longitude,
                            index == 0 ? "현재 위치" : "배달 경로")).ToArray(),
                        StrokeColor: route.IsEstimated ? "#64748B" : "#2563EB")
                ];
            RouteStatusText = $"{(route.IsEstimated ? "추정 경로" : "실시간 도로 경로")} · {route.DistanceKm:0.0}km · 약 {route.DurationMinutes}분";
        }
        catch (FDriverApiException ex)
        {
            RouteStatusText = $"경로 조회 실패 · {ShortMessage(ex.Message)}";
            SelectedRouteOverlays = [];
        }

        RefreshRouteLabels();
    }

    private IReadOnlyList<FoodDeliveryDriverRouteStopDto> BuildRouteStops()
    {
        if (ActiveDeliveryItems.Count > 0)
        {
            var pickups = ActiveDeliveryItems
                .Where(x => x.WorkStatus == DriverWorkOfferStatus.MovingToPickup)
                .Select(x => x.Pickup)
                .Where(HasCoordinates)
                .Select(x => ToRouteStop($"픽업 · {x.Label}", x));
            var dropoffs = ActiveDeliveryItems
                .Select(x => x.Dropoff)
                .Where(HasCoordinates)
                .Select(x => ToRouteStop($"전달 · {x.Label}", x));
            return pickups.Concat(dropoffs).Take(6).ToArray();
        }

        if (SelectedTicket is null)
        {
            return [];
        }

        return new[] { SelectedTicket.Pickup, SelectedTicket.Dropoff }
            .Where(HasCoordinates)
            .Select(x => ToRouteStop(x.Label, x))
            .ToArray();
    }

    private async Task RunApiAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        NotifyCommandState();
        try
        {
            await action();
        }
        catch (FDriverApiException ex)
        {
            StatusMessage = ShortMessage(ex.Message);
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                await StopMonitoringAsync();
                await _authSession.ClearAsync();
                IsAuthenticated = false;
                ClearWorkspace();
            }
        }
        finally
        {
            IsBusy = false;
            NotifyCommandState();
        }
    }

    private void SetWorkStage()
    {
        WorkStage = ActiveDelivery?.WorkStatus switch
        {
            DriverWorkOfferStatus.MovingToPickup when ActiveDeliveryItems.Count > 1 => $"묶음 픽업 {ActiveDeliveryItems.Count}건",
            DriverWorkOfferStatus.MovingToPickup => "음식점 이동 중",
            DriverWorkOfferStatus.MovingToDropoff when ActiveDeliveryItems.Count > 1 => $"묶음 전달 {ActiveDeliveryItems.Count}건",
            DriverWorkOfferStatus.MovingToDropoff => "고객 주소 이동 중",
            _ => "추천 대기"
        };
        NextActionGuide = ActiveDelivery?.WorkStatus switch
        {
            DriverWorkOfferStatus.MovingToPickup => "음식점에서 주문을 받은 뒤 픽업 확인을 눌러 주세요.",
            DriverWorkOfferStatus.MovingToDropoff => "고객에게 전달한 뒤 전달 완료를 눌러 주세요.",
            _ when IsOnDuty => "지도에서 추천 배달권을 선택하세요.",
            _ => "운행 시작을 눌러 현재 위치 추천을 활성화하세요."
        };
    }

    private void ClearWorkspace()
    {
        RecommendedTicketItems.Clear();
        ActiveDeliveryItems.Clear();
        BundleCandidateItems.Clear();
        SelectedTicket = null;
        ActiveDelivery = null;
        MapMarkers = [];
        SelectedRouteOverlays = [];
        PendingDeliveryTickets = 0;
        RecommendedTickets = 0;
        TodayExpectedPayout = 0m;
        SettlementText = "이번 달 정산 조회 전";
        WorkspaceSyncText = "업무 자동 갱신 대기 · 30초 주기";
        RealtimeConnectionText = "실시간 배차 연결 대기 · 30초 자동 조회 보조";
        LocationSyncText = "기사 위치 전송 대기";
        DispatchAutomationEnabled = false;
        DispatchAutomationNotice = "자동 배차 상태 확인 전";
        HasNewRecommendations = false;
        _knownRecommendedTicketIds.Clear();
        _lastLocationSentAtUtc = null;
        NotifyWorkspaceState();
    }

    partial void OnIsAuthenticatedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(SignedInUserText));
    }

    partial void OnIsOnDutyChanged(bool value) => NotifyWorkState();
    partial void OnSelectedTicketChanged(DeliveryTicketPreview? value) => RefreshRouteLabels();
    partial void OnActiveDeliveryChanged(ActiveDeliveryPreview? value)
    {
        RefreshRouteLabels();
        NotifyWorkspaceState();
    }

    partial void OnWorkStageChanged(string value) => OnPropertyChanged(nameof(ActiveWorkSummary));

    private void NotifyWorkState()
    {
        OnPropertyChanged(nameof(WorkToggleText));
        OnPropertyChanged(nameof(WorkToggleColor));
    }

    private void NotifyWorkspaceState()
    {
        OnPropertyChanged(nameof(ActiveWorkSummary));
        OnPropertyChanged(nameof(ActiveWorkRouteText));
        OnPropertyChanged(nameof(ActiveWorkPayoutText));
        OnPropertyChanged(nameof(HasActiveWork));
        OnPropertyChanged(nameof(HasBundleCandidates));
        OnPropertyChanged(nameof(HasMultipleActiveDeliveries));
        NotifyCommandState();
    }

    private void NotifyCommandState()
    {
        OnPropertyChanged(nameof(CanConfirmPickup));
        OnPropertyChanged(nameof(CanCompleteDelivery));
        OnPropertyChanged(nameof(CanAcceptSelectedTicket));
    }

    private void RefreshRouteLabels()
    {
        OnPropertyChanged(nameof(PickupPointText));
        OnPropertyChanged(nameof(DropoffPointText));
        OnPropertyChanged(nameof(SelectedRouteText));
    }

    private async Task MonitorWorkspaceAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var networkElapsed = TimeSpan.Zero;
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                networkElapsed += TimeSpan.FromSeconds(1);
                await MainThread.InvokeOnMainThreadAsync(UpdateRecommendationCountdowns);
                if (networkElapsed < WorkspaceRefreshInterval)
                {
                    continue;
                }

                networkElapsed = TimeSpan.Zero;
                await MainThread.InvokeOnMainThreadAsync(
                    () => RefreshWorkspaceFromBackgroundAsync("30초 자동 갱신"));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RefreshWorkspaceFromBackgroundAsync(string sourceLabel)
    {
        if (!IsAuthenticated || IsBusy || _monitorRefreshInProgress)
        {
            return;
        }

        _monitorRefreshInProgress = true;
        try
        {
            if (IsOnDuty
                && (!_lastLocationSentAtUtc.HasValue
                    || DateTime.UtcNow - _lastLocationSentAtUtc.Value >= LocationHeartbeatInterval))
            {
                await CaptureLocationAsync(sendToServer: true);
            }

            await LoadWorkspaceAsync(refreshRoute: false);
            if (string.Equals(sourceLabel, "실시간 배차", StringComparison.Ordinal))
            {
                RealtimeConnectionText = "실시간 배차 반영됨 · 30초 자동 조회 보조";
            }
        }
        catch (FDriverApiException ex)
        {
            WorkspaceSyncText = $"{sourceLabel} 지연 · {ShortMessage(ex.Message)}";
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                await StopMonitoringAsync();
                await _authSession.ClearAsync();
                IsAuthenticated = false;
                IsOnDuty = false;
                ClearWorkspace();
                StatusMessage = "로그인이 만료되었습니다. 다시 로그인해 주세요.";
            }
        }
        finally
        {
            _monitorRefreshInProgress = false;
        }
    }

    private Task OnRealtimeRecommendationsReceivedAsync(int _)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            RealtimeConnectionText = "실시간 배차 알림 수신 · 업무 화면 동기화 중";
            await RefreshWorkspaceFromBackgroundAsync("실시간 배차");
        });

    private void OnRealtimeStatusChanged(string status)
        => MainThread.BeginInvokeOnMainThread(() => RealtimeConnectionText = status);

    private void UpdateRecommendationCountdowns()
    {
        var now = DateTime.UtcNow;
        foreach (var item in RecommendedTicketItems)
        {
            item.UpdateCountdown(now);
        }

        NotifyCommandState();
    }

    private static DriverMapMarkerItem ToMapMarker(DeliveryTicketPreview ticket)
        => new(
            ticket.TicketId,
            ticket.Pickup.Latitude,
            ticket.Pickup.Longitude,
            ticket.Dropoff.Latitude,
            ticket.Dropoff.Longitude,
            ticket.RestaurantName,
            $"{ticket.OrderSummary} · {ticket.DriverPayoutText}",
            ticket.Pickup.Address,
            ticket.Dropoff.Address,
            ticket.PickupActionLabel,
            ticket.CompletionActionLabel);

    private static DriverMapMarkerItem ToMapMarker(ActiveDeliveryPreview delivery)
        => new(
            delivery.OfferId,
            delivery.Pickup.Latitude,
            delivery.Pickup.Longitude,
            delivery.Dropoff.Latitude,
            delivery.Dropoff.Longitude,
            delivery.RestaurantName,
            $"진행 중 · {delivery.OrderSummary}",
            delivery.Pickup.Address,
            delivery.Dropoff.Address,
            delivery.PickupActionLabel,
            delivery.CompletionActionLabel);

    private static bool HasCoordinates(DriverWorkStopDto stop)
        => stop.Latitude != 0d && stop.Longitude != 0d;

    private static FoodDeliveryDriverRouteStopDto ToRouteStop(string label, DriverWorkStopDto stop)
        => new()
        {
            Label = label,
            Latitude = (decimal)stop.Latitude,
            Longitude = (decimal)stop.Longitude
        };

    private static string ShortMessage(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 180 ? clean : $"{clean[..180]}…";
    }
}

public sealed class DeliveryTicketPreview : ObservableObject
{
    private string _remainingText = "응답시간 확인 중";
    private bool _isExpired;

    public DeliveryTicketPreview(
        string ticketId,
        string orderSummary,
        string restaurantName,
        DriverWorkStopDto pickup,
        DriverWorkStopDto dropoff,
        double distanceKm,
        decimal driverPayout,
        decimal weatherSurcharge,
        bool weatherSurchargeApplied,
        string recommendationReason,
        DateTime? expiresAtUtc,
        운송실행프로필Dto executionProfile)
    {
        TicketId = ticketId;
        OrderSummary = orderSummary;
        RestaurantName = restaurantName;
        Pickup = pickup;
        Dropoff = dropoff;
        DistanceKm = distanceKm;
        DriverPayout = driverPayout;
        WeatherSurcharge = weatherSurcharge;
        IsWeatherSurchargeApplied = weatherSurchargeApplied;
        RecommendationReason = recommendationReason;
        ExpiresAtUtc = expiresAtUtc;
        ExecutionProfile = executionProfile;
        UpdateCountdown(DateTime.UtcNow);
    }

    public string TicketId { get; }
    public string OrderSummary { get; }
    public string RestaurantName { get; }
    public DriverWorkStopDto Pickup { get; }
    public DriverWorkStopDto Dropoff { get; }
    public double DistanceKm { get; }
    public decimal DriverPayout { get; }
    public decimal WeatherSurcharge { get; }
    public bool IsWeatherSurchargeApplied { get; }
    public string RecommendationReason { get; }
    public DateTime? ExpiresAtUtc { get; }
    public 운송실행프로필Dto ExecutionProfile { get; }
    public string RestaurantAddress => Pickup.Address;
    public string DropoffAddress => Dropoff.Address;
    public string PickupActionLabel => ActionLabel(ExecutionProfile.픽업행동명, "음식점 픽업");
    public string CompletionActionLabel => ActionLabel(ExecutionProfile.완료행동명, "고객 전달");
    public string DistanceText => $"{DistanceKm.ToString("0.0", CultureInfo.CurrentCulture)}km";
    public string DriverPayoutText => $"{DriverPayout.ToString("N0", CultureInfo.CurrentCulture)}원";
    public string WeatherSurchargeText => $"기상 할증 +{WeatherSurcharge.ToString("N0", CultureInfo.CurrentCulture)}원";
    public string RemainingText
    {
        get => _remainingText;
        private set => SetProperty(ref _remainingText, value);
    }
    public bool IsExpired
    {
        get => _isExpired;
        private set
        {
            if (SetProperty(ref _isExpired, value))
            {
                OnPropertyChanged(nameof(CanAccept));
            }
        }
    }
    public bool CanAccept => !IsExpired;

    public void UpdateCountdown(DateTime utcNow)
    {
        if (!ExpiresAtUtc.HasValue)
        {
            IsExpired = false;
            RemainingText = "응답시간 확인 중";
            return;
        }

        var expiresAtUtc = DateTime.SpecifyKind(ExpiresAtUtc.Value, DateTimeKind.Utc);
        var remaining = expiresAtUtc - utcNow;
        IsExpired = remaining <= TimeSpan.Zero;
        RemainingText = IsExpired
            ? "응답시간 만료"
            : $"응답 {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))}초 남음";
    }

    public static DeliveryTicketPreview From(FoodDeliveryDriverOfferDto item)
        => new(
            item.OfferId,
            item.OrderSummary,
            item.RestaurantName,
            ToStop(item.Pickup),
            ToStop(item.Dropoff),
            (double)(item.DistanceKm ?? 0m),
            item.DriverPayout,
            item.WeatherSurcharge,
            item.WeatherSurchargeApplied,
            item.RecommendationReason,
            item.ExpiresAtUtc,
            item.ExecutionProfile);

    public DriverWorkOfferDto ToDriverWorkOffer(FDriverAppProfile profile)
        => new(
            TicketId,
            profile.AppKey,
            profile.DriverDomain,
            profile.PrimaryWorkType,
            OrderSummary,
            $"{PickupActionLabel} · {CompletionActionLabel}",
            Pickup,
            Dropoff,
            DriverPayout,
            DistanceKm,
            RecommendationReason,
            ExecutionProfile: ExecutionProfile,
            WeatherSurcharge: WeatherSurcharge,
            WeatherSurchargeApplied: IsWeatherSurchargeApplied);

    internal static string ActionLabel(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    internal static DriverWorkStopDto ToStop(FoodDeliveryDriverStopDto stop)
        => new(
            stop.Label,
            stop.Address,
            (double)(stop.Latitude ?? 0m),
            (double)(stop.Longitude ?? 0m),
            stop.TargetAtUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(stop.TargetAtUtc.Value, DateTimeKind.Utc))
                : null);
}

public sealed record ActiveDeliveryPreview(
    long TransportId,
    string OfferId,
    string OrderSummary,
    string RestaurantName,
    DriverWorkStopDto Pickup,
    DriverWorkStopDto Dropoff,
    decimal DriverPayout,
    decimal WeatherSurcharge,
    bool WeatherSurchargeApplied,
    string TransportStatus,
    string WorkStatus,
    운송실행프로필Dto ExecutionProfile,
    FoodDeliveryDriverRecipientDto Recipient)
{
    public string PickupActionLabel => DeliveryTicketPreview.ActionLabel(ExecutionProfile.픽업행동명, "음식점 픽업");
    public string CompletionActionLabel => DeliveryTicketPreview.ActionLabel(ExecutionProfile.완료행동명, "고객 전달");
    public bool IsWeatherSurchargeApplied => WeatherSurchargeApplied;
    public string WeatherSurchargeText => $"기상 할증 +{WeatherSurcharge.ToString("N0", CultureInfo.CurrentCulture)}원";
    public bool HasRecipient => !string.IsNullOrWhiteSpace(Recipient.DisplayName)
                                || !string.IsNullOrWhiteSpace(Recipient.ContactPhone)
                                || !string.IsNullOrWhiteSpace(Recipient.DeliveryInstructions);
    public string RecipientNameText => string.IsNullOrWhiteSpace(Recipient.DisplayName)
        ? "수령자 이름 미등록"
        : Recipient.DisplayName;
    public string RecipientContactText => string.IsNullOrWhiteSpace(Recipient.ContactPhone)
        ? "연락처 미등록"
        : Recipient.ContactPhone;
    public string DeliveryInstructionsText => string.IsNullOrWhiteSpace(Recipient.DeliveryInstructions)
        ? "별도 전달 요청 없음"
        : Recipient.DeliveryInstructions;
    public string RecipientRelationshipText => Recipient.OrdererIsRecipient
        ? "주문자 본인 수령"
        : "지정 수령자";

    public static ActiveDeliveryPreview From(FoodDeliveryDriverActiveDeliveryDto item)
        => new(
            item.TransportId,
            item.OfferId,
            item.OrderSummary,
            item.RestaurantName,
            DeliveryTicketPreview.ToStop(item.Pickup),
            DeliveryTicketPreview.ToStop(item.Dropoff),
            item.DriverPayout,
            item.WeatherSurcharge,
            item.WeatherSurchargeApplied,
            item.TransportStatus,
            item.WorkStatus,
            item.ExecutionProfile,
            item.Recipient);

    public DriverWorkOfferDto ToDriverWorkOffer(FDriverAppProfile profile)
        => new(
            OfferId,
            profile.AppKey,
            profile.DriverDomain,
            profile.PrimaryWorkType,
            OrderSummary,
            $"{PickupActionLabel} · {CompletionActionLabel}",
            Pickup,
            Dropoff,
            DriverPayout,
            null,
            "진행 중인 음식 배달",
            Status: WorkStatus,
            ExecutionProfile: ExecutionProfile,
            Recipient: new DriverWorkRecipientDto(
                Recipient.DisplayName,
                Recipient.ContactPhone,
                Recipient.DeliveryInstructions,
                Recipient.OrdererIsRecipient),
            WeatherSurcharge: WeatherSurcharge,
            WeatherSurchargeApplied: WeatherSurchargeApplied);
}

public sealed record FoodDeliveryBundlePreview(
    string BundleId,
    IReadOnlyList<string> OfferIds,
    string Title,
    string Reason,
    decimal TotalPayout,
    decimal EstimatedRouteKm)
{
    public string CountText => $"{OfferIds.Count}건 묶음";
    public string PayoutText => $"{TotalPayout:N0}원";
    public string RouteText => $"예상 {EstimatedRouteKm:0.0}km";

    public static FoodDeliveryBundlePreview From(FoodDeliveryBundleCandidateDto item)
        => new(item.BundleId, item.OfferIds, item.Title, item.Reason, item.TotalPayout, item.EstimatedRouteKm);
}
