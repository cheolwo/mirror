using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel : ObservableObject
{
    private const string DrivingStatus = "운행중";
    private static readonly TimeSpan WorkspaceRefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LocationHeartbeatInterval = TimeSpan.FromSeconds(10);
    private readonly FDriverAppProfile _profile;
    private readonly IFDriverAuthSession _authSession;
    private readonly FDriverAuthApiService _authApi;
    private readonly IFoodDeliveryDriverApiService _api;
    private readonly IFDriverLocationService _locationService;
    private readonly 역할앱생명주기State _appLifecycle;
    private bool _initialized;
    private bool _workspaceActive;
    private bool _monitorRefreshInProgress;
    private CancellationTokenSource _workspaceCancellation = new();
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private DateTime? _lastLocationSentAtUtc;

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLocationWarning))] private bool _isOnDuty;
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
    [ObservableProperty] private string _settlementText = "이번 달 이용료 조회 전";
    [ObservableProperty] private string _routeStatusText = "경로 조회 전";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasWorkspaceWarning))] private string _workspaceWarningText = string.Empty;
    [ObservableProperty] private string _workspaceSyncText = "업무 자동 갱신 대기 · 10초 주기";
    [ObservableProperty] private string _recommendationNotificationText = "앱 복귀·10초 조회로 새 배달 요청 확인";
    [ObservableProperty] private string _locationSyncText = "기사 위치 전송 대기";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasDispatchNotice))] private bool _dispatchAutomationEnabled;
    [ObservableProperty] private int _maxActiveDeliveries;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasDispatchNotice))] private string _dispatchAutomationNotice = "자동 배차 상태 확인 전";
    [ObservableProperty] private IReadOnlyList<DriverMapMarkerItem> _mapMarkers = [];
    [ObservableProperty] private IReadOnlyList<DriverMapRouteOverlay> _selectedRouteOverlays = [];
    [ObservableProperty] private double _mapCenterLatitude = 37.5665d;
    [ObservableProperty] private double _mapCenterLongitude = 126.9780d;
    [ObservableProperty] private double _currentLocationLatitude;
    [ObservableProperty] private double _currentLocationLongitude;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLocationWarning))] private bool _hasCurrentLocation;
    [ObservableProperty] private bool _hasNewRecommendations;
    [ObservableProperty] private string _newRecommendationNotice = "새 추천 배차가 도착했습니다.";

    public MainPageModel(
        FDriverAppProfile profile,
        IFDriverAuthSession authSession,
        FDriverAuthApiService authApi,
        IFoodDeliveryDriverApiService api,
        IFDriverLocationService locationService,
        역할앱생명주기State appLifecycle,
        IFDriverFoodNotificationService? notifications = null)
    {
        _profile = profile;
        _authSession = authSession;
        _authApi = authApi;
        _api = api;
        _locationService = locationService;
        _appLifecycle = appLifecycle;
        InitializeFoodNotifications(notifications);
        ExceptionEditor.PropertyChanged += ExceptionEditorChanged;
    }

    public string AppName => _profile.DisplayName;
    public string DriverRole => _profile.DriverRole;
    public 역할앱생명주기State 앱생명주기 => _appLifecycle;
    public FDriverWorkspaceNavigationState Navigation { get; } = new();
    public bool IsSignedOut => !IsAuthenticated;
    public string SignedInUserText => string.IsNullOrWhiteSpace(_authSession.UserName)
        ? DriverRole
        : $"{_authSession.UserName} · {DriverRole}";
    public string WorkToggleText => IsOnDuty ? "운행 종료" : "운행 시작";
    public string WorkToggleColor => IsOnDuty ? "#B91C1C" : "#0F766E";
    public ObservableCollection<DeliveryTicketPreview> RecommendedTicketItems { get; } = [];
    public ObservableCollection<ActiveDeliveryPreview> ActiveDeliveryItems { get; } = [];
    public ObservableCollection<FoodDeliveryBundlePreview> BundleCandidateItems { get; } = [];
    public ObservableCollection<FoodDeliverySettlementDisplay> OrderSettlementItems { get; } = [];

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
    public bool HasRouteSelection => CurrentRouteOffer is not null;
    public bool HasRecommendationContent => RecommendedTicketItems.Count > 0 || HasBundleCandidates;
    public bool HasLocationWarning => IsOnDuty && !HasCurrentLocation;
    public bool HasDispatchNotice => !DispatchAutomationEnabled
        && DispatchAutomationNotice != "자동 배차 상태 확인 전";
    public bool HasWorkspaceWarning => !string.IsNullOrWhiteSpace(WorkspaceWarningText);
    public bool HasActiveRecipient => ActiveDelivery?.HasRecipient == true;
    public bool CanConfirmPickup => IsAuthenticated && !IsBusy && !ExceptionEditor.IsOpen && !ExceptionEditor.HasPendingRequest
        && ActiveDelivery?.Can(음식배달가능행동Ids.기사픽업확인) == true;
    public bool CanCompleteDelivery => IsAuthenticated && !IsBusy && !ExceptionEditor.IsOpen && !ExceptionEditor.HasPendingRequest
        && ActiveDelivery?.Can(음식배달가능행동Ids.기사전달완료) == true;
    public bool CanAcceptSelectedTicket => !IsBusy
                                           && IsAuthenticated && DispatchIntentKnown && ReceivesNewDispatches
                                           && !ExceptionEditor.IsOpen && !ExceptionEditor.HasPendingRequest
                                           && SelectedTicket?.CanAccept == true
                                           && MaxActiveDeliveries > 0
                                           && ActiveDeliveryItems.Count < MaxActiveDeliveries;
    public bool HasBundleCandidates => BundleCandidateItems.Count > 0;
    public bool HasMultipleActiveDeliveries => ActiveDeliveryItems.Count > 1;

    private DriverWorkOfferDto? CurrentRouteOffer
        => ActiveDelivery?.ToDriverWorkOffer(_profile) ?? SelectedTicket?.ToDriverWorkOffer(_profile);

    public async Task InitializeAsync()
    {
        _workspaceActive = true;
        EnsureWorkspaceLifetime();
        var cancellationToken = _workspaceCancellation.Token;
        try
        {
            if (_initialized && IsAuthenticated)
            {
                await ReloadAsync(updateLocation: IsOnDuty);
                return;
            }

            var restoreState = await _authSession.RestoreAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _initialized = true;
            if (restoreState == Ssalddel.Client.Infrastructure.Security.ClientAuthSessionRestoreState.RefreshRequired)
            {
                var refresh = await _authApi.EnsureAccessTokenResultAsync(cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!refresh.IsSuccess)
                {
                    StatusMessage = refresh.ErrorMessage!;
                }
            }

            // A recoverable refresh session remains visible during a temporary connection failure.
            // Protected API requests still require a successfully refreshed access token.
            IsAuthenticated = _authSession.CurrentState != Ssalddel.Client.Infrastructure.Security.ClientAuthSessionRestoreState.Anonymous;
            if (IsAuthenticated)
            {
                await ReloadAsync(updateLocation: true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async Task StartMonitoringAsync()
    {
        if (!_workspaceActive || !IsAuthenticated)
        {
            return;
        }

        EnsureWorkspaceLifetime();
        var workspaceCancellation = _workspaceCancellation;
        if (_monitorTask is { IsCompleted: false })
        {
            if (_monitorCancellation?.IsCancellationRequested != true)
            {
                return;
            }
            await _monitorTask;
        }

        if (!_workspaceActive || !IsAuthenticated
            || workspaceCancellation.IsCancellationRequested
            || !ReferenceEquals(_workspaceCancellation, workspaceCancellation))
        {
            return;
        }

        _monitorCancellation?.Dispose();
        _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(workspaceCancellation.Token);
        _monitorTask = MonitorWorkspaceAsync(_monitorCancellation.Token);
        RecommendationNotificationText = "앱 복귀·10초 조회로 새 배달 요청 확인";
    }

    public Task StopMonitoringAsync() => StopMonitoringAsync(deactivateWorkspace: true);

    private async Task StopMonitoringAsync(bool deactivateWorkspace)
    {
        if (deactivateWorkspace)
        {
            _workspaceActive = false;
            ExceptionEditor.Hide();
        }
        var monitorTask = _monitorTask;
        var monitorCancellation = _monitorCancellation;
        CancelWorkspaceLifetime();
        if (monitorTask is not null)
        {
            await monitorTask;
        }

        if (ReferenceEquals(_monitorTask, monitorTask))
        {
            _monitorTask = null;
            _monitorCancellation = null;
            monitorCancellation?.Dispose();
        }
    }

    private void EnsureWorkspaceLifetime()
    {
        if (_workspaceCancellation.IsCancellationRequested)
        {
            _workspaceCancellation.Dispose();
            _workspaceCancellation = new CancellationTokenSource();
        }
    }

    private void CancelWorkspaceLifetime()
    {
        _workspaceCancellation.Cancel();
        _monitorCancellation?.Cancel();
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
            "settlement" => "완료한 배달의 정산 내역을 확인하세요.",
            "workspace" => "음식 배달 업무 공간을 열었습니다. 배차부터 전달 완료까지 한 흐름으로 처리합니다.",
            _ => StatusMessage
        };
    }

    [RelayCommand]
    private async Task Login()
    {
        if (!_workspaceActive || IsBusy)
        {
            return;
        }

        EnsureWorkspaceLifetime();
        var cancellationToken = _workspaceCancellation.Token;
        IsBusy = true;
        StatusMessage = "기사 계정을 확인하고 있습니다.";
        try
        {
            var error = await _authApi.LoginAsync(LoginId, Password, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (error is not null)
            {
                StatusMessage = error;
                return;
            }

            Password = string.Empty;
            IsAuthenticated = true;
            IsBusy = false;
            StatusMessage = "배달 업무를 불러오고 있습니다.";
            await ReloadAsync(updateLocation: true);
            await StartMonitoringAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            IsBusy = false;
            NotifyCommandState();
        }
    }

    [RelayCommand]
    private async Task Logout()
    {
        if (IsBusy)
        {
            return;
        }

        if (HasActiveWork)
        {
            StatusMessage = "진행 중 배달을 완료하거나 현장 중단을 요청한 뒤 로그아웃해 주세요.";
            return;
        }

        var cancellationToken = _workspaceCancellation.Token;
        IsBusy = true;
        NotifyCommandState();
        try
        {
            if (IsOnDuty)
            {
                await _api.StopWorkAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            await StopMonitoringAsync(deactivateWorkspace: false);
            await ClearAuthenticationAsync();
            StatusMessage = "운행을 종료하고 로그아웃했습니다.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (FDriverApiException ex) when (!cancellationToken.IsCancellationRequested)
        {
            StatusMessage = $"운행을 종료하지 못해 로그아웃을 보류했습니다. {ShortMessage(ex.Message)}";
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
        await OpenLatestFoodRecommendationAsync();
    }

    [RelayCommand]
    private async Task ToggleWork()
    {
        if (!CanToggleWork)
        {
            return;
        }

        await RunApiAsync(async cancellationToken =>
        {
            if (IsOnDuty)
            {
                await _api.StopWorkAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                IsOnDuty = false;
                StatusMessage = "운행을 종료했습니다. 진행 중 배달은 계속 확인할 수 있습니다.";
            }
            else
            {
                var location = await CaptureLocationAsync(sendToServer: false, cancellationToken);
                var startLocation = location is null
                    ? "음식 배달 앱 운행 시작"
                    : FormattableString.Invariant($"{location.Latitude:0.000000},{location.Longitude:0.000000}");
                await _api.StartWorkAsync(startLocation, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                IsOnDuty = true;
                await CaptureLocationAsync(sendToServer: true, cancellationToken);
                StatusMessage = "운행을 시작했습니다. 신규 배차 수신은 별도로 선택해 주세요.";
            }

            NotifyWorkState();
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
        });
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (!IsAuthenticated || IsRefreshing || IsBusy)
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
        StatusMessage = $"{ticket.RestaurantName} 배달 요청을 선택했습니다.";
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
        if (!IsAuthenticated || IsBusy || !DispatchIntentKnown || !ReceivesNewDispatches
            || ExceptionEditor.IsOpen || ExceptionEditor.HasPendingRequest) return;
        if (!ticket.CanAccept)
        {
            StatusMessage = "현재 상태에서는 이 배달권을 수락할 수 없습니다. 새 추천을 확인해 주세요.";
            return;
        }

        SelectedTicket = ticket;
        await AcceptOfferAsync(ticket.TicketId);
    }

    [RelayCommand]
    private async Task RejectTicket(DeliveryTicketPreview ticket)
    {
        if (!ticket.CanReject)
        {
            StatusMessage = "현재 상태에서는 이 배달권을 거절할 수 없습니다. 새 추천을 확인해 주세요.";
            return;
        }

        await RunApiAsync(async cancellationToken =>
        {
            var result = await _api.RejectAsync(ticket.TicketId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            StatusMessage = result.Message;
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
        });
    }

    [RelayCommand]
    private async Task SelectActiveDelivery(ActiveDeliveryPreview delivery)
    {
        if (!IsAuthenticated || IsBusy || ExceptionEditor.IsOpen || ExceptionEditor.HasPendingRequest) return;
        ActiveDelivery = delivery;
        StatusMessage = $"{delivery.OrderSummary} 진행 배달을 선택했습니다.";
        SetWorkStage();
        await RefreshRouteAsync();
    }

    [RelayCommand]
    private async Task AcceptSelectedTicket()
    {
        if (!CanAcceptSelectedTicket) return;
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
        if (!IsAuthenticated || IsBusy || !DispatchIntentKnown || !ReceivesNewDispatches
            || ExceptionEditor.IsOpen || ExceptionEditor.HasPendingRequest) return;
        await RunApiAsync(async cancellationToken =>
        {
            var result = await _api.AcceptBundleAsync(bundle.OfferIds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            StatusMessage = result.Message;
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
        });
    }

    [RelayCommand]
    private async Task ConfirmPickup()
    {
        if (!CanConfirmPickup || ActiveDelivery is null)
        {
            StatusMessage = "진행 중인 배달이 없습니다.";
            return;
        }

        await RunApiAsync(async cancellationToken =>
        {
            var result = await _api.ConfirmPickupAsync(ActiveDelivery.OfferId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            StatusMessage = result.Message;
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
        });
    }

    [RelayCommand]
    private async Task CompleteDelivery()
    {
        if (!CanCompleteDelivery || ActiveDelivery is null)
        {
            StatusMessage = "진행 중인 배달이 없습니다.";
            return;
        }

        await RunApiAsync(async cancellationToken =>
        {
            var result = await _api.CompleteAsync(ActiveDelivery.OfferId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            StatusMessage = result.Message;
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
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

        await RunApiAsync(async cancellationToken =>
        {
            var result = await _api.AcceptAsync(offerId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            StatusMessage = result.Message;
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
        });
    }

    private async Task ReloadAsync(bool updateLocation)
    {
        await RunApiAsync(async cancellationToken =>
        {
            var workStatus = await _api.GetWorkStatusAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            IsOnDuty = string.Equals(workStatus?.Status, DrivingStatus, StringComparison.OrdinalIgnoreCase);
            NotifyWorkState();
            if (updateLocation && IsOnDuty)
            {
                await CaptureLocationAsync(sendToServer: true, cancellationToken);
            }

            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
        });
    }

    private async Task LoadWorkspaceAsync(bool refreshRoute = true, CancellationToken cancellationToken = default)
    {
        var workspace = await _api.GetWorkspaceAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await LoadDispatchAvailabilityAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var selectedTicketId = SelectedTicket?.TicketId;
        var activeOfferId = ActiveDelivery?.OfferId;
        ApplyFoodRecommendations(workspace);

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
        OrderSettlementItems.Clear();
        foreach (var item in workspace.OrderSettlements)
        {
            OrderSettlementItems.Add(FoodDeliverySettlementDisplay.From(item));
        }
        SettlementText = $"월 이용료 · {workspace.Settlement.년도}년 {workspace.Settlement.월}월 · "
                         + $"배차 {workspace.Settlement.배차건수:N0}건 · 이용료 {workspace.Settlement.이용료:N0}원"
                          + (workspace.Settlement.결제완료 ? " · 납부 완료" : string.Empty);
        DispatchAutomationEnabled = workspace.DispatchAutomationEnabled;
        MaxActiveDeliveries = workspace.MaxActiveDeliveries;
        DispatchAutomationNotice = workspace.DispatchAutomationNotice;
        WorkspaceWarningText = string.Empty;
        WorkspaceSyncText = $"업무 동기화 {workspace.UpdatedAtUtc.ToLocalTime():HH:mm:ss} · 다음 자동 갱신 10초 이내";
        MapMarkers = RecommendedTicketItems.Select(ToMapMarker)
            .Concat(ActiveDeliveryItems.Select(ToMapMarker))
            .ToArray();
        SetWorkStage();
        ReconcileExceptionWorkspace();
        NotifyWorkspaceState();
        UpdateRecommendationCountdowns();
        await RestoreFoodNotificationTargetAsync(cancellationToken);
        if (refreshRoute)
        {
            await RefreshRouteAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (Navigation.IsSettlement)
            await RefreshDailySettlementAsync();

        if (string.IsNullOrWhiteSpace(StatusMessage)
            || StatusMessage == "기사 로그인 후 배달 업무를 시작할 수 있습니다."
            || StatusMessage.Contains("불러오", StringComparison.Ordinal))
        {
            StatusMessage = HasActiveWork ? "현재 배달을 확인해 주세요."
                : HasRecommendationContent ? "배달 요청을 확인해 주세요."
                : IsOnDuty ? "배달 요청을 기다리고 있습니다." : "운행 시작을 눌러 배달을 시작하세요.";
        }
    }

    private async Task<FDriverLocationSnapshot?> CaptureLocationAsync(bool sendToServer, CancellationToken cancellationToken = default)
    {
        cancellationToken = cancellationToken == default ? _workspaceCancellation.Token : cancellationToken;
        var location = await _locationService.GetCurrentAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (location is null)
        {
            HasCurrentLocation = false;
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
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _lastLocationSentAtUtc = DateTime.UtcNow;
            LocationSyncText = $"기사 위치 전송 {_lastLocationSentAtUtc.Value.ToLocalTime():HH:mm:ss}";
        }

        return location;
    }

    private async Task RefreshRouteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken = cancellationToken == default ? _workspaceCancellation.Token : cancellationToken;
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
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
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (FDriverApiException ex) when (!cancellationToken.IsCancellationRequested)
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

    private async Task RunApiAsync(Func<CancellationToken, Task> action)
    {
        if (!_workspaceActive || IsBusy)
        {
            return;
        }

        var cancellationToken = _workspaceCancellation.Token;
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        IsBusy = true;
        NotifyCommandState();
        try
        {
            await action(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (FDriverApiException ex) when (!cancellationToken.IsCancellationRequested)
        {
            StatusMessage = ShortMessage(ex.Message);
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                await StopMonitoringAsync(deactivateWorkspace: false);
                await ClearAuthenticationAsync();
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

    private async Task ClearAuthenticationAsync()
    {
        try
        {
            await _authSession.ClearAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException
            && _authSession.CurrentState == Ssalddel.Client.Infrastructure.Security.ClientAuthSessionRestoreState.Anonymous)
        {
            // The session clears memory before removing device storage. Never retain private UI on removal failure.
        }
        finally
        {
            IsAuthenticated = false;
            IsOnDuty = false;
            Password = string.Empty;
            ClearWorkspace();
        }
    }

    private void ClearWorkspace()
    {
        ClearFoodNotifications();
        ClearDailySettlementValues();
        ClearExceptionWorkspace();
        RecommendedTicketItems.Clear();
        ActiveDeliveryItems.Clear();
        BundleCandidateItems.Clear();
        OrderSettlementItems.Clear();
        SelectedTicket = null;
        ActiveDelivery = null;
        MapMarkers = [];
        SelectedRouteOverlays = [];
        PendingDeliveryTickets = 0;
        RecommendedTickets = 0;
        MaxActiveDeliveries = 0;
        TodayExpectedPayout = 0m;
        HasCurrentLocation = false;
        CurrentLocationLatitude = 0d;
        CurrentLocationLongitude = 0d;
        CurrentArea = "현재 위치 확인 전";
        MapCenterLatitude = 37.5665d;
        MapCenterLongitude = 126.9780d;
        RouteStatusText = "경로 조회 전";
        SettlementText = "이번 달 이용료 조회 전";
        WorkspaceWarningText = string.Empty;
        WorkspaceSyncText = "업무 자동 갱신 대기 · 10초 주기";
        RecommendationNotificationText = "앱 복귀·10초 조회로 새 배달 요청 확인";
        LocationSyncText = "기사 위치 전송 대기";
        DispatchAutomationEnabled = false;
        DispatchAutomationNotice = "자동 배차 상태 확인 전";
        HasNewRecommendations = false;
        _lastLocationSentAtUtc = null;
        NotifyWorkspaceState();
    }

    partial void OnIsAuthenticatedChanged(bool value)
    {
        if (!value)
        {
            Navigation.Reset();
        }
        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(SignedInUserText));
        NotifyExceptionState();
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
        NotifyExceptionState();
    }

    private void NotifyWorkspaceState()
    {
        OnPropertyChanged(nameof(ActiveWorkSummary));
        OnPropertyChanged(nameof(ActiveWorkRouteText));
        OnPropertyChanged(nameof(ActiveWorkPayoutText));
        OnPropertyChanged(nameof(HasActiveWork));
        OnPropertyChanged(nameof(HasRecommendationContent));
        OnPropertyChanged(nameof(HasActiveRecipient));
        OnPropertyChanged(nameof(HasBundleCandidates));
        OnPropertyChanged(nameof(HasMultipleActiveDeliveries));
        NotifyCommandState();
    }

    private void NotifyCommandState()
    {
        OnPropertyChanged(nameof(CanConfirmPickup));
        OnPropertyChanged(nameof(CanCompleteDelivery));
        OnPropertyChanged(nameof(CanAcceptSelectedTicket));
        NotifyExceptionState();
    }

    private void RefreshRouteLabels()
    {
        OnPropertyChanged(nameof(HasRouteSelection));
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
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        UpdateRecommendationCountdowns();
                    }
                });
                if (networkElapsed < WorkspaceRefreshInterval)
                {
                    continue;
                }

                networkElapsed = TimeSpan.Zero;
                await MainThread.InvokeOnMainThreadAsync(
                    () => RefreshWorkspaceFromBackgroundAsync("10초 자동 갱신", cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RefreshWorkspaceFromBackgroundAsync(string sourceLabel, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || !IsAuthenticated || IsBusy || _monitorRefreshInProgress)
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
                await CaptureLocationAsync(sendToServer: true, cancellationToken);
            }

            await LoadWorkspaceAsync(refreshRoute: false, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (FDriverApiException ex) when (!cancellationToken.IsCancellationRequested)
        {
            WorkspaceWarningText = "갱신하지 못했습니다. 이전 정보일 수 있으니 화면을 아래로 당겨 새로고침해 주세요.";
            WorkspaceSyncText = $"{sourceLabel} 지연 · {ShortMessage(ex.Message)}";
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                CancelWorkspaceLifetime();
                await ClearAuthenticationAsync();
                StatusMessage = "로그인이 만료되었습니다. 다시 로그인해 주세요.";
            }
        }
        finally
        {
            _monitorRefreshInProgress = false;
        }
    }

    private void UpdateRecommendationCountdowns()
    {
        var now = DateTime.UtcNow;
        foreach (var item in RecommendedTicketItems)
        {
            item.UpdateCountdown(now);
        }

        ReconcileFoodNotificationCountdowns(now);

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
        운송실행프로필Dto executionProfile,
        IReadOnlyList<업무가능행동Dto> availableActions)
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
        AvailableActions = availableActions;
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
    public IReadOnlyList<업무가능행동Dto> AvailableActions { get; }
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
                OnPropertyChanged(nameof(CanReject));
            }
        }
    }
    public bool CanAccept => !IsExpired
                             && 업무가능행동목록.포함(AvailableActions, 음식배달가능행동Ids.기사제안수락);
    public bool CanReject => !IsExpired
                             && 업무가능행동목록.포함(AvailableActions, 음식배달가능행동Ids.기사제안거절);

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
            item.ExecutionProfile,
            item.AvailableActions);

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
    FoodDeliveryDriverRecipientDto Recipient,
    IReadOnlyList<업무가능행동Dto> AvailableActions)
{
    public string DeliveryAttemptId { get; init; } = string.Empty;
    public long AttemptRevision { get; init; }
    public DateTime? RestaurantArrivedAtUtc { get; init; }
    public DateTime? DisplayedPreparationReadyAtUtc { get; init; }
    public DateTime? PreparationDelayEligibleAtUtc { get; init; }
    public bool IsPreparationDelayRedispatch { get; init; }
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
    public bool Can(string actionId) => 업무가능행동목록.포함(AvailableActions, actionId);

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
            item.Recipient,
            item.AvailableActions)
        {
            DeliveryAttemptId = item.DeliveryAttemptId, AttemptRevision = item.AttemptRevision,
            RestaurantArrivedAtUtc = item.RestaurantArrivedAtUtc,
            DisplayedPreparationReadyAtUtc = item.DisplayedPreparationReadyAtUtc,
            PreparationDelayEligibleAtUtc = item.PreparationDelayEligibleAtUtc,
            IsPreparationDelayRedispatch = item.IsPreparationDelayRedispatch
        };

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
