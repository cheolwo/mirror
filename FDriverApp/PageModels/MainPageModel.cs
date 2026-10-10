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
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

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
    private readonly TimeProvider _time;
    private readonly IFDriverLocationService _locationService;
    private readonly 역할앱생명주기State _appLifecycle;
    private bool _initialized;
    private bool _workspaceEntered;
    private bool _workspaceActive;
    private bool _workspacePageVisible = true;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private Task? _workspaceResumeTask;
    private CancellationTokenSource? _workspaceResumeCancellation;
    private bool _monitorRefreshInProgress;
    private long _workspaceSnapshotGeneration;
    private WorkspaceRefreshFailure? _workspaceRefreshFailure;
    private CancellationTokenSource _workspaceCancellation = new();
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private DateTime? _lastLocationSentAtUtc;

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanLogin))] private bool _isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsSignedOut), nameof(CanLogin))] private bool _isRestoringSession;
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
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLocationWarning), nameof(HasMapLocationWarning))] private bool _hasCurrentLocation;
    [ObservableProperty] private bool _hasNewRecommendations;
    [ObservableProperty] private string _newRecommendationNotice = "새 추천 배차가 도착했습니다.";

    public MainPageModel(
        FDriverAppProfile profile,
        IFDriverAuthSession authSession,
        FDriverAuthApiService authApi,
        IFoodDeliveryDriverApiService api,
        IFDriverLocationService locationService,
        역할앱생명주기State appLifecycle,
        IFDriverFoodNotificationService? notifications = null,
        IFDriverPendingOperationStore? pendingOperationStore = null,
        FDriverPushRegistrationService? foodPushRegistration = null, TimeProvider? clock = null)
    {
        _profile = profile;
        _authSession = authSession;
        _authApi = authApi;
        _api = api;
        _time = clock ?? TimeProvider.System;
        _locationService = locationService;
        _appLifecycle = appLifecycle;
        _pendingOperationStore = pendingOperationStore ?? new FDriverMemoryPendingOperationStore();
        // The optional compatibility store is newly created and known empty. The
        // production secure store must still restore and reconcile before actions.
        _pendingRecoveryReady = pendingOperationStore is null;
        _foodPushRegistration = foodPushRegistration;
        InitializeSessionBoundary();
        InitializeFoodNotifications(notifications);
        InitializeDeliveryExperience();
        InitializeFoodMap();
        ExceptionEditor.PropertyChanged += ExceptionEditorChanged;
    }

    private void InitializeSessionBoundary()
    {
        var observedAccount = _authSession.UserId;
        var observedDriverAccess = HasDriverSession();
        _authSession.SessionChanged += (_, _) =>
        {
            var previous = observedAccount;
            var previousAccess = observedDriverAccess;
            var account = _authSession.UserId;
            var driverAccess = HasDriverSession();
            observedAccount = account;
            observedDriverAccess = driverAccess;
            // A same-account token refresh does not replace the foreground work owner.
            // Initial login/restore is owned by InitializeAsync or Login, not this event.
            if (previous is null || (previous == account && previousAccess == driverAccess && driverAccess)) return;
            _ = MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (account != _authSession.UserId || driverAccess != HasDriverSession()) return;
                CancelWorkspaceLifetime();
                ClearWorkspace();
                Navigation.Reset();
                IsOnDuty = false;
                IsAuthenticated = driverAccess;
                _initialized = false;
                StatusMessage = IsAuthenticated
                    ? "기사 계정이 변경됐습니다. 수행 화면으로 돌아오면 현재 계정의 배달을 확인합니다."
                    : "기사 권한이 있는 계정으로 로그인해 주세요.";
            });
        };
    }

    private bool HasDriverSession() => !string.IsNullOrWhiteSpace(_authSession.UserId)
        && _authSession.CurrentState != Ssalddel.Client.Infrastructure.Security.ClientAuthSessionRestoreState.Anonymous
        && _authSession.Roles.Any(role => string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase) || role == "기사");

    // A route change only ends map presentation. Foreground work, location and
    // recommendation polling remain owned by the app until it pauses or signs out.
    public void SetWorkspacePageVisible(bool visible)
    {
        _workspacePageVisible = visible;
        if (!visible)
        {
            ExceptionEditor.Hide();
            _foodMapRoutes.Clear();
            SelectedRouteOverlays = [];
        }
    }

    public void PauseFoodWorkspace()
    {
        _workspaceActive = false;
        _foodMapForeground = false;
        ExceptionEditor.Hide();
        CancelWorkspaceLifetime();
    }

    public async Task ActivateWorkspacePageAsync()
    {
        SetWorkspacePageVisible(true);
        _workspaceEntered = true;
        _workspaceActive = true;
        EnsureWorkspaceLifetime();
        var lifetime = _workspaceCancellation;
        await InitializeAsync();
        if (IsWorkspaceLifetimeCurrent(lifetime)) await StartMonitoringAsync();
    }

    public Task ResumeFoodWorkspaceAsync()
    {
        if (!_workspaceEntered) return Task.CompletedTask;
        if (_workspaceResumeTask is { IsCompleted: false }
            && _workspaceResumeCancellation is { } running && IsWorkspaceLifetimeCurrent(running))
            return _workspaceResumeTask;
        _workspaceActive = true;
        _foodMapForeground = true;
        EnsureWorkspaceLifetime();
        _workspaceResumeCancellation = _workspaceCancellation;
        return _workspaceResumeTask = ResumeForegroundWorkspaceAsync(_workspaceCancellation);
    }

    private async Task ResumeForegroundWorkspaceAsync(CancellationTokenSource lifetime)
    {
        await InitializeAsync();
        if (IsWorkspaceLifetimeCurrent(lifetime)) await StartMonitoringAsync();
    }

    private bool IsWorkspaceLifetimeCurrent(CancellationTokenSource lifetime)
        => ReferenceEquals(_workspaceCancellation, lifetime) && !lifetime.IsCancellationRequested && _workspaceActive;

    public string AppName => _profile.DisplayName;
    public string DriverRole => _profile.DriverRole;
    public 역할앱생명주기State 앱생명주기 => _appLifecycle;
    public FDriverWorkspaceNavigationState Navigation { get; } = new();
    public bool IsSignedOut => !IsAuthenticated && !IsRestoringSession;
    public bool CanLogin => _workspaceActive && !IsBusy && !IsRestoringSession;
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
    private bool CanUseWorkspaceState => IsAuthenticated && _pendingRecoveryReady && !IsBusy
        && _workspaceRefreshFailure is null;
    private bool CanAcceptNewDispatch => CanUseWorkspaceState && DispatchIntentKnown && ReceivesNewDispatches
        && !ExceptionEditor.IsOpen && !ExceptionEditor.HasPendingRequest;
    private bool CanRespondToTicket => CanUseWorkspaceState && !ExceptionEditor.IsOpen && !ExceptionEditor.HasPendingRequest;
    public bool CanConfirmPickup => CanUseWorkspaceState && !ExceptionEditor.IsOpen && !ExceptionEditor.HasPendingRequest
        && IsPickupPrimaryAction
        && ActiveDelivery?.Can(음식배달가능행동Ids.기사픽업확인) == true;
    public bool CanCompleteDelivery => CanUseWorkspaceState && !ExceptionEditor.IsOpen && !ExceptionEditor.HasPendingRequest
        && ActiveDelivery?.Can(음식배달가능행동Ids.기사전달완료) == true;
    public bool CanAcceptSelectedTicket => CanAcceptNewDispatch
                                           && SelectedTicket?.CanAccept == true
                                           && MaxActiveDeliveries > 0
                                           && ActiveDeliveryItems.Count < MaxActiveDeliveries;
    public bool HasBundleCandidates => BundleCandidateItems.Count > 0;
    public bool HasMultipleActiveDeliveries => ActiveDeliveryItems.Count > 1;

    private DriverWorkOfferDto? CurrentRouteOffer
        => ActiveDelivery?.ToDriverWorkOffer(_profile) ?? SelectedTicket?.ToDriverWorkOffer(_profile);

    public async Task InitializeAsync()
    {
        _workspaceEntered = true;
        _workspaceActive = true;
        EnsureWorkspaceLifetime();
        var cancellationToken = _workspaceCancellation.Token;
        var entered = false;
        try
        {
            await _initializationGate.WaitAsync(cancellationToken);
            entered = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (_initialized && IsAuthenticated)
            {
                await ReloadAsync(updateLocation: IsOnDuty);
                return;
            }

            IsRestoringSession = true;
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
            IsAuthenticated = HasDriverSession();
            if (IsAuthenticated)
            {
                await ReloadAsync(updateLocation: true);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (entered) IsRestoringSession = false;
            if (entered) _initializationGate.Release();
        }
    }

    public async Task StartMonitoringAsync()
    {
        if (!_workspaceActive || !_initialized || !IsAuthenticated || !HasDriverSession())
        {
            return;
        }

        EnsureWorkspaceLifetime();
        var workspaceCancellation = _workspaceCancellation;
        if (_foodMapForeground) await ResumeFoodMapAsync();
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
        if (_foodPushRegistration is null)
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
        StopFoodMap();
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
        if (!CanLogin)
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
            IsAuthenticated = HasDriverSession();
            IsBusy = false;
            if (!IsAuthenticated)
            {
                StatusMessage = "기사 권한이 있는 계정으로 로그인해 주세요.";
                return;
            }
            StatusMessage = "배달 업무를 불러오고 있습니다.";
            await ReloadAsync(updateLocation: true);
            await StartMonitoringAsync();
        }
        catch (OperationCanceledException)
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

        var logoutOwner = _authSession.UserId;
        var logoutLifetime = _workspaceCancellation;
        var cancellationToken = logoutLifetime.Token;
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
            EnsureLogoutOwner();
            await ClearPendingAccountAsync(CancellationToken.None);
            var pushNotice = await DeactivateFoodPushAsync();
            EnsureLogoutOwner();
            await ClearAuthenticationAsync(_authSession.SessionRevision, logoutOwner);
            if (!IsAuthenticated) StatusMessage = pushNotice ?? "운행을 종료하고 로그아웃했습니다.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (FDriverApiException ex) when (!cancellationToken.IsCancellationRequested)
        {
            StatusMessage = $"운행을 종료하지 못해 로그아웃을 보류했습니다. {ShortMessage(ex.Message)}";
        }
        catch (FDriverPendingStorageException ex)
        {
            // 보안 저장소 정리에 실패한 로그아웃을 성공으로 표시하지 않습니다.
            if (logoutOwner != _authSession.UserId || !ReferenceEquals(_workspaceCancellation, logoutLifetime)) return;
            await DeactivateFoodPushAsync();
            if (logoutOwner == _authSession.UserId && ReferenceEquals(_workspaceCancellation, logoutLifetime))
            {
                await ClearAuthenticationAsync(_authSession.SessionRevision, logoutOwner);
                if (!IsAuthenticated) StatusMessage = ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
            NotifyCommandState();
        }

        void EnsureLogoutOwner()
        {
            // StopMonitoringAsync intentionally cancelled this lifetime and joined
            // its work. Only a replacement lifetime or account can veto cleanup.
            if (logoutOwner != _authSession.UserId || !ReferenceEquals(_workspaceCancellation, logoutLifetime))
                throw new OperationCanceledException("기사 계정이 변경되어 이전 로그아웃을 적용하지 않습니다.", cancellationToken);
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
                await ClearAutomaticStartReceptionAsync(cancellationToken);
                StatusMessage = "운행을 종료했습니다. 진행 중 배달은 계속 확인할 수 있습니다.";
            }
            else
            {
                await StartAutomaticWorkAsync(cancellationToken);
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
        await InitializeAsync();
        await StartMonitoringAsync();
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

    [RelayCommand(CanExecute = nameof(CanAcceptNewDispatch))]
    private async Task AcceptTicket(DeliveryTicketPreview ticket)
    {
        if (!CanAcceptNewDispatch) return;
        if (!ticket.CanAccept)
        {
            StatusMessage = "현재 상태에서는 이 배달권을 수락할 수 없습니다. 새 추천을 확인해 주세요.";
            return;
        }

        SelectedTicket = ticket;
        await AcceptOfferAsync(ticket.TicketId);
    }

    [RelayCommand(CanExecute = nameof(CanRespondToTicket))]
    private async Task RejectTicket(DeliveryTicketPreview ticket)
    {
        if (!CanRespondToTicket) return;
        if (!ticket.CanReject)
        {
            StatusMessage = "현재 상태에서는 이 배달권을 거절할 수 없습니다. 새 추천을 확인해 주세요.";
            return;
        }

        await RunDeliveryActionAsync(cancellationToken => _api.RejectAsync(ticket.TicketId, cancellationToken));
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

    [RelayCommand(CanExecute = nameof(CanAcceptNewDispatch))]
    private async Task AcceptBundle(FoodDeliveryBundlePreview bundle)
    {
        if (!CanAcceptNewDispatch) return;
        await RunDeliveryActionAsync(cancellationToken => _api.AcceptBundleAsync(bundle.OfferIds, cancellationToken));
    }

    [RelayCommand]
    private async Task ConfirmPickup()
    {
        if (!CanUseWorkspaceState) return;
        if (!CanConfirmPickup || ActiveDelivery is null)
        {
            StatusMessage = "진행 중인 배달이 없습니다.";
            return;
        }

        var offerId = ActiveDelivery.OfferId;
        await RunDeliveryActionAsync(cancellationToken => _api.ConfirmPickupAsync(offerId, cancellationToken));
    }

    [RelayCommand]
    private async Task CompleteDelivery()
    {
        if (!CanUseWorkspaceState) return;
        if (!CanCompleteDelivery || ActiveDelivery is null)
        {
            StatusMessage = "진행 중인 배달이 없습니다.";
            return;
        }

        var offerId = ActiveDelivery.OfferId;
        await RunDeliveryActionAsync(cancellationToken => _api.CompleteAsync(offerId, cancellationToken));
    }

    private async Task AcceptOfferAsync(string offerId)
    {
        var ticket = RecommendedTicketItems.FirstOrDefault(x => x.TicketId == offerId);
        if (ticket?.IsExpired == true)
        {
            StatusMessage = "응답 시간이 지난 배달권입니다. 새 추천을 확인해 주세요.";
            return;
        }

        await RunDeliveryActionAsync(cancellationToken => _api.AcceptAsync(offerId, cancellationToken));
    }

    private async Task ReloadAsync(bool updateLocation)
    {
        await RunApiAsync(async cancellationToken =>
        {
            _workStatusKnown = false;
            NotifyCommandState();
            var workStatus = await _api.GetWorkStatusAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ApplyCanonicalWorkStatus(workStatus?.Status))
                StatusMessage = "운행 상태를 자동으로 확인하고 있습니다.";
            if (updateLocation && _workStatusKnown && IsOnDuty)
            {
                await CaptureLocationAsync(sendToServer: true, cancellationToken);
            }

            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
            await RefreshFoodPushRegistrationAsync(cancellationToken);
        });
    }

    private async Task LoadWorkspaceAsync(bool refreshRoute = true, CancellationToken cancellationToken = default)
    {
        await RestorePendingOperationsAsync(cancellationToken);
        var owner = _authSession.UserId;
        var lifetime = _workspaceCancellation;
        var generation = Interlocked.Increment(ref _workspaceSnapshotGeneration);
        try
        {
            var workspace = await _api.GetCurrentWorkAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Interlocked.Read(ref _workspaceSnapshotGeneration)) return;
            await LoadDispatchAvailabilityAsync(cancellationToken, generation);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Interlocked.Read(ref _workspaceSnapshotGeneration)) return;
            var selectedTicketId = SelectedTicket?.TicketId;
            var activeOfferId = ActiveDelivery?.OfferId ?? _pendingArrivalOfferId ?? _restoredInterruption?.OfferId;
            await RefreshNotificationSettingsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Interlocked.Read(ref _workspaceSnapshotGeneration) || owner != _authSession.UserId) return;
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
            SetDeliveryExperienceSnapshotCurrent(true);
            SelectedTicket = selectedTicketId is null
                ? RecommendedTicketItems.FirstOrDefault()
                : RecommendedTicketItems.FirstOrDefault(x => x.TicketId == selectedTicketId)
                  ?? RecommendedTicketItems.FirstOrDefault();
            PendingDeliveryTickets = RecommendedTicketItems.Count + ActiveDeliveryItems.Count;
            RecommendedTickets = RecommendedTicketItems.Count;
            TodayExpectedPayout = RecommendedTicketItems.Sum(x => x.DriverPayout);
            DispatchAutomationEnabled = workspace.DispatchAutomationEnabled;
            MaxActiveDeliveries = workspace.MaxActiveDeliveries;
            DispatchAutomationNotice = workspace.DispatchAutomationNotice;
            WorkspaceWarningText = string.Empty;
            WorkspaceSyncText = $"업무 동기화 {workspace.UpdatedAtUtc.ToLocalTime():HH:mm:ss} · 다음 자동 갱신 10초 이내";
            MapMarkers = RecommendedTicketItems.Select(ToMapMarker)
                .Concat(ActiveDeliveryItems.Select(ToMapMarker))
                .ToArray();
            SetWorkStage();
            await ReconcileExceptionWorkspaceAsync(cancellationToken);
            if (generation != Interlocked.Read(ref _workspaceSnapshotGeneration)) return;
            _pendingRecoveryReady = true;
            NotifyExceptionState();
            NotifyWorkspaceState();
            UpdateRecommendationCountdowns();
            await RestoreFoodNotificationTargetAsync(cancellationToken);
            if (generation != Interlocked.Read(ref _workspaceSnapshotGeneration)) return;
            UpdateFoodMapContext();
            if (_workspacePageVisible) _ = _foodMapRoutes.TickAsync();
            if (refreshRoute)
            {
                await RefreshRouteAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (generation != Interlocked.Read(ref _workspaceSnapshotGeneration)) return;
            }

            if (_workspaceRefreshFailure is { } failure && failure.Owner == owner
                && failure.Generation < generation && owner == _authSession.UserId
                && ReferenceEquals(lifetime, _workspaceCancellation))
            {
                // Only the notice produced by this owner's failed read is obsolete.
                // A newer command or storage failure still owns its message.
                if (StatusMessage == failure.Message) StatusMessage = string.Empty;
                _workspaceRefreshFailure = null;
            }
            if (string.IsNullOrWhiteSpace(StatusMessage)
                || StatusMessage == "기사 로그인 후 배달 업무를 시작할 수 있습니다."
                || StatusMessage.Contains("불러오", StringComparison.Ordinal))
            {
                StatusMessage = HasActiveWork ? "현재 배달을 확인해 주세요."
                    : HasRecommendationContent ? "배달 요청을 확인해 주세요."
                    : IsOnDuty ? "배달 요청을 기다리고 있습니다." : "운행 시작을 눌러 배달을 시작하세요.";
            }
            NotifyCommandState();
        }
        catch (Exception) when (generation != Interlocked.Read(ref _workspaceSnapshotGeneration)
            && !cancellationToken.IsCancellationRequested)
        {
            // A completed command/newer read owns the screen; an older poll cannot undo it.
        }
        catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized
            && !cancellationToken.IsCancellationRequested)
        {
            if (owner == _authSession.UserId && ReferenceEquals(lifetime, _workspaceCancellation))
            {
                SetDeliveryExperienceSnapshotCurrent(false);
                var message = WorkspaceRefreshErrorMessage(ex);
                _workspaceRefreshFailure = new(owner, generation, message, ex);
                StatusMessage = message;
                WorkspaceWarningText = "배달 상태를 자동으로 다시 확인하고 있습니다. 확인될 때까지 이전 정보일 수 있습니다.";
                NotifyCommandState();
            }
            throw;
        }
        catch
        {
            if (generation == Interlocked.Read(ref _workspaceSnapshotGeneration)
                && owner == _authSession.UserId && ReferenceEquals(lifetime, _workspaceCancellation))
                SetDeliveryExperienceSnapshotCurrent(false);
            throw;
        }
    }

    private Task RefreshRouteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken = cancellationToken == default ? _workspaceCancellation.Token : cancellationToken;
        if (cancellationToken.IsCancellationRequested || !_workspacePageVisible) return Task.CompletedTask;
        UpdateFoodMapContext();
        _ = _foodMapRoutes.RefreshAsync();
        ApplyFoodMapSnapshot(_foodMapRoutes.Snapshot);
        RefreshRouteLabels();
        return Task.CompletedTask;
    }
    private Task RunDeliveryActionAsync(Func<CancellationToken, Task<FoodDeliveryDriverActionResponse>> action)
        => RunApiAsync(async cancellationToken =>
        {
            try
            {
                var result = await action(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                StatusMessage = result.Message;
            }
            catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized
                && !cancellationToken.IsCancellationRequested)
            {
                // An uncertain response never causes an automatic second POST.
                // The canonical read chooses the next step and its allowed actions.
                StatusMessage = ex.StatusCode is null || (int)ex.StatusCode.Value >= 500
                    ? "처리 응답을 확인하지 못했습니다. 현재 배달 상태를 다시 확인합니다."
                    : $"{ShortMessage(ex.Message)} 현재 배달 상태를 다시 확인합니다.";
            }
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
        });

    private async Task RunApiAsync(Func<CancellationToken, Task> action)
    {
        if (!_workspaceActive || IsBusy)
        {
            return;
        }

        var lifetime = _workspaceCancellation;
        var owner = _authSession.UserId;
        var cancellationToken = lifetime.Token;
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        IsBusy = true;
        Interlocked.Increment(ref _workspaceSnapshotGeneration);
        NotifyCommandState();
        try
        {
            await action(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (FDriverApiException ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (!IsWorkspaceLifetimeCurrent(lifetime) || owner != _authSession.UserId) return;
            if (_workspaceRefreshFailure is not { } failure || failure.Owner != owner
                || !ReferenceEquals(failure.Error, ex))
                StatusMessage = ShortMessage(ex.Message);
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                var revision = _authSession.SessionRevision;
                await StopMonitoringAsync(deactivateWorkspace: false);
                if (owner == _authSession.UserId && ReferenceEquals(_workspaceCancellation, lifetime))
                    await ClearAuthenticationAsync(revision, owner);
            }
        }
        catch (FDriverPendingStorageException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _pendingRecoveryReady = false;
            StatusMessage = ex.Message;
            NotifyExceptionState();
        }
        finally
        {
            IsBusy = false;
            NotifyCommandState();
        }
    }

    private void SetWorkStage()
    {
        WorkStage = CurrentDeliveryStageText;
        NextActionGuide = ActiveDelivery is not null ? CurrentDeliveryServerGuide : IsOnDuty switch
        {
            true => "지도에서 추천 배달권을 선택하세요.",
            _ => "운행 시작을 눌러 현재 위치 추천을 활성화하세요."
        };
    }

    private async Task ClearAuthenticationAsync(long? expectedRevision = null, string? expectedOwner = null)
    {
        var cleared = false;
        try
        {
            if (expectedRevision is { } revision)
            {
                if (expectedOwner != _authSession.UserId) return;
                cleared = await _authSession.TryClearAsync(revision);
                if (!cleared) return;
            }
            else { await _authSession.ClearAsync(); cleared = true; }
        }
        catch (Exception ex) when (ex is not OperationCanceledException
            && _authSession.CurrentState == Ssalddel.Client.Infrastructure.Security.ClientAuthSessionRestoreState.Anonymous)
        {
            // The session clears memory before removing device storage. Never retain private UI on removal failure.
        }
        finally
        {
            if (cleared || _authSession.CurrentState == Ssalddel.Client.Infrastructure.Security.ClientAuthSessionRestoreState.Anonymous)
            {
                IsAuthenticated = false;
                IsOnDuty = false;
                Password = string.Empty;
                ClearWorkspace();
            }
        }
    }

    private void ClearWorkspace()
    {
        _workspaceRefreshFailure = null;
        _workStatusKnown = false;
        StopFoodMap();
        IsFollowingCurrentLocation = true;
        MapZoom = 14d;
        ResetFoodPresentationState();
        ClearFoodNotifications();
        ResetDeliveryExperienceState();
        ClearDailySettlementValues();
        ClearProfileSettlementValues();
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
        NotifyDeliveryExperience();
        if (!value)
        {
            Navigation.Reset();
        }
        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(HasMapLocationWarning));
        OnPropertyChanged(nameof(SignedInUserText));
        NotifyExceptionState();
    }

    partial void OnIsOnDutyChanged(bool value)
    {
        if (!value) CancelLocationTransmissions();
        NotifyWorkState();
    }
    partial void OnSelectedTicketChanged(DeliveryTicketPreview? value) => RefreshRouteLabels();
    partial void OnActiveDeliveryChanged(ActiveDeliveryPreview? value)
    {
        RefreshRouteLabels();
        NotifyWorkspaceState();
    }

    partial void OnWorkStageChanged(string value)
    {
        OnPropertyChanged(nameof(ActiveWorkSummary));
        NotifyFoodPresentationState();
    }

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
        NotifyFoodPresentationState();
        OnPropertyChanged(nameof(CanConfirmPickup));
        OnPropertyChanged(nameof(CanCompleteDelivery));
        OnPropertyChanged(nameof(CanAcceptSelectedTicket));
        NotifyExceptionState();
    }

    private void RefreshRouteLabels()
    {
        OnPropertyChanged(nameof(FocusedMapRequestId));
        NotifyFoodPresentationState();
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

        var owner = _authSession.UserId;
        var lifetime = _workspaceCancellation;
        _monitorRefreshInProgress = true;
        try
        {
            try { await ReconcileAutomaticWorkAsync(cancellationToken); }
            catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !cancellationToken.IsCancellationRequested)
            {
                if (owner != _authSession.UserId || !IsWorkspaceLifetimeCurrent(lifetime)) return;
                StatusMessage = "운행·배차 수신 결과를 자동으로 다시 확인하고 있습니다.";
            }
            if (IsOnDuty
                && (!_lastLocationSentAtUtc.HasValue
                    || DateTime.UtcNow - _lastLocationSentAtUtc.Value >= LocationHeartbeatInterval))
            {
                try { await CaptureLocationAsync(sendToServer: true, cancellationToken); }
                catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !cancellationToken.IsCancellationRequested)
                {
                    if (owner != _authSession.UserId || !IsWorkspaceLifetimeCurrent(lifetime)) return;
                    LocationSyncText = "기사 위치 전송 지연 · 자동으로 다시 전송합니다.";
                }
            }

            await LoadWorkspaceAsync(refreshRoute: false, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (FDriverPendingStorageException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _pendingRecoveryReady = false;
            StatusMessage = ex.Message;
            WorkspaceWarningText = ex.Message;
            NotifyExceptionState();
            NotifyCommandState();
        }
        catch (FDriverApiException ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (owner != _authSession.UserId || !IsWorkspaceLifetimeCurrent(lifetime)) return;
            WorkspaceWarningText = "배달 상태를 자동으로 다시 확인하고 있습니다. 확인될 때까지 이전 정보일 수 있습니다.";
            WorkspaceSyncText = $"{sourceLabel} 지연 · {WorkspaceRefreshErrorMessage(ex)}";
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                var revision = _authSession.SessionRevision;
                CancelWorkspaceLifetime();
                await ClearAuthenticationAsync(revision, owner);
                if (!IsAuthenticated) StatusMessage = "로그인이 만료되었습니다. 다시 로그인해 주세요.";
            }
        }
        finally
        {
            _monitorRefreshInProgress = false;
        }
    }

    private void UpdateRecommendationCountdowns()
    {
        var now = _time.GetUtcNow().UtcDateTime;
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

    private static string WorkspaceRefreshErrorMessage(FDriverApiException error)
        => error.StatusCode is null || (int)error.StatusCode.Value >= 500
            ? "배달 상태를 불러오지 못했습니다. 자동으로 다시 확인합니다."
            : ShortMessage(error.Message);

    private sealed record WorkspaceRefreshFailure(string? Owner, long Generation, string Message, FDriverApiException Error);
}

public sealed class DeliveryTicketPreview : ObservableObject
{
    private string _remainingText = "응답시간 확인 중";
    private bool _isExpired;
    private DateTime _nowUtc;

    public DeliveryTicketPreview(
        string ticketId,
        string orderSummary,
        string restaurantName,
        DriverWorkStopDto pickup,
        DriverWorkStopDto dropoff,
        double? distanceKm,
        decimal driverPayout,
        decimal weatherSurcharge,
        bool weatherSurchargeApplied,
        string recommendationReason,
        DateTime? expiresAtUtc,
        운송실행프로필Dto executionProfile,
        IReadOnlyList<업무가능행동Dto> availableActions, DateTime? nowUtc = null)
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
        UpdateCountdown(nowUtc ?? DateTime.UtcNow);
    }

    public string TicketId { get; }
    public string OrderSummary { get; }
    public string RestaurantName { get; }
    public DriverWorkStopDto Pickup { get; }
    public DriverWorkStopDto Dropoff { get; }
    public double? DistanceKm { get; }
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
    public string DistanceText => DistanceKm is { } distance ? $"{distance.ToString("0.0", CultureInfo.CurrentCulture)}km" : "미확인";
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
    public bool CanAccept => !IsExpired && Can(음식배달가능행동Ids.기사제안수락);
    public bool CanReject => !IsExpired && Can(음식배달가능행동Ids.기사제안거절);
    private bool Can(string actionId) => FoodWorkspacePresentation.Available(AvailableActions, actionId, actionId,
        automaticRefresh: true, nowUtc: _nowUtc)?.Enabled == true;

    public void UpdateCountdown(DateTime utcNow)
    {
        _nowUtc = utcNow;
        OnPropertyChanged(nameof(CanAccept));
        OnPropertyChanged(nameof(CanReject));
        if (!ExpiresAtUtc.HasValue)
        {
            IsExpired = false;
            RemainingText = "응답시간 확인 중";
            return;
        }

        var expiresAtUtc = DateTime.SpecifyKind(ExpiresAtUtc.Value, DateTimeKind.Utc);
        var remaining = expiresAtUtc - utcNow;
        IsExpired = !FoodDriverWorkRecoveryPolicy.OfferCurrent(expiresAtUtc, utcNow);
        RemainingText = IsExpired
            ? "응답시간 만료"
            : $"응답 {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))}초 남음";
    }

    public static DeliveryTicketPreview From(FoodDeliveryDriverOfferDto item, DateTime? nowUtc = null)
        => new(
            item.OfferId,
            item.OrderSummary,
            item.RestaurantName,
            ToStop(item.Pickup),
            ToStop(item.Dropoff),
            item.DistanceKm is { } distance ? (double)distance : null,
            item.DriverPayout,
            item.WeatherSurcharge,
            item.WeatherSurchargeApplied,
            item.RecommendationReason,
            item.ExpiresAtUtc,
            item.ExecutionProfile,
            item.AvailableActions, nowUtc);

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
    public IReadOnlyList<DeliveryExperienceSummaryDto> ExperienceSummaries { get; init; } = [];
    public string OrderNo { get; init; } = string.Empty;
    public bool HasSupportSource => !string.IsNullOrWhiteSpace(OrderNo);
    public string DeliveryAttemptId { get; init; } = string.Empty;
    public long AttemptRevision { get; init; }
    public DateTime? RestaurantArrivedAtUtc { get; init; }
    public DateTime? DisplayedPreparationReadyAtUtc { get; init; }
    public DateTime? PreparationDelayEligibleAtUtc { get; init; }
    public bool IsPreparationDelayRedispatch { get; init; }
    public int CurrentPreparationRound { get; init; } = 1;
    public DateTime? CurrentPickupReadyAtUtc { get; init; }
    public DateTime? RecookingRequestedAtUtc { get; init; }
    public bool IsRecooking => CurrentPreparationRound > 1 || RecookingRequestedAtUtc.HasValue;
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
            OrderNo = item.OrderNo,
            RestaurantArrivedAtUtc = item.RestaurantArrivedAtUtc,
            DisplayedPreparationReadyAtUtc = item.DisplayedPreparationReadyAtUtc,
            PreparationDelayEligibleAtUtc = item.PreparationDelayEligibleAtUtc,
            IsPreparationDelayRedispatch = item.IsPreparationDelayRedispatch,
            CurrentPreparationRound = item.CurrentPreparationRound,
            CurrentPickupReadyAtUtc = item.CurrentPickupReadyAtUtc,
            RecookingRequestedAtUtc = item.RecookingRequestedAtUtc,
            ExperienceSummaries = item.ExperienceSummaries ?? []
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
