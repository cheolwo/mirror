using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace FDriverApp.PageModels;

/// <summary>완료일별 배달 목록과 목록의 페이지 위치만 소유합니다. 주문·고객 상세는 가져오거나 보관하지 않습니다.</summary>
public sealed partial class FDriverCompletedDeliveryListPageModel : ObservableObject
{
    private const int PageSize = 20;
    private readonly IFoodDeliveryDriverApiService _api;
    private readonly IFDriverAuthSession _session;
    private readonly IFDriverCompletedDeliveryNavigator _navigator;
    private IReadOnlyList<FDriverDailySettlementItem> _allItems = [];
    private CancellationTokenSource? _requestCancellation;
    private long _revision;
    private string? _loadedOwner;
    private string? _observedOwner;
    private DateOnly _selectedDay;
    private bool _active;
    private bool _foreground = true;
    private bool _navigationInProgress;
    private bool _sessionSubscribed;

    [ObservableProperty] private DateTime _selectedDate = DateTime.UtcNow.AddHours(9).Date;
    [ObservableProperty] private int _pageNumber = 1;
    [ObservableProperty] private string _totalCountText = "완료 내역 조회 전";
    [ObservableProperty] private string _grossTotalText = "배달료 합계 조회 전";
    [ObservableProperty] private string _statusText = "날짜를 선택하면 완료한 배달을 조회합니다.";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isAuthenticationRequired;
    [ObservableProperty] private bool _isLoaded;
    public ObservableCollection<FDriverDailySettlementItem> Items { get; } = [];
    public int PageCount => Math.Max(1, (_allItems.Count + PageSize - 1) / PageSize);
    public string PageText => $"{PageNumber} / {PageCount}페이지";
    public bool HasPreviousPage => IsLoaded && !IsLoading && PageNumber > 1;
    public bool HasNextPage => IsLoaded && !IsLoading && PageNumber < PageCount;
    public bool HasNoItems => IsLoaded && !IsLoading && Items.Count == 0;

    public FDriverCompletedDeliveryListPageModel(IFoodDeliveryDriverApiService api,
        IFDriverAuthSession session, IFDriverCompletedDeliveryNavigator navigator)
    {
        _api = api;
        _session = session;
        _navigator = navigator;
        _selectedDay = DateOnly.FromDateTime(_selectedDate);
        _observedOwner = session.UserId;
    }

    public void SetDate(DateOnly date) => SelectedDate = date.ToDateTime(TimeOnly.MinValue);

    public async Task ActivateAsync()
    {
        _active = true;
        _foreground = true;
        if (!_sessionSubscribed)
        {
            _observedOwner = _session.UserId;
            _session.SessionChanged += OnSessionChanged;
            _sessionSubscribed = true;
        }
        if (!CheckSession()) return;
        // 상세에서 돌아올 때 동일 계정·날짜의 금융 목록과 페이지 위치를 보존합니다.
        if (!IsLoaded) await RefreshAsync();
    }

    public void Deactivate()
    {
        _active = false;
        if (_sessionSubscribed)
        {
            _session.SessionChanged -= OnSessionChanged;
            _sessionSubscribed = false;
        }
        CancelRequests();
        IsLoading = false;
    }

    public void Pause()
    {
        _foreground = false;
        CancelRequests();
        IsLoading = false;
    }

    public async Task ResumeAsync()
    {
        _foreground = true;
        if (!_active || !CheckSession()) return;
        if (!IsLoaded) await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync()
    {
        if (!_active || !_foreground || !CheckSession()) return;
        var date = DateOnly.FromDateTime(SelectedDate);
        if (date == DateOnly.MinValue || date == DateOnly.MaxValue)
        {
            ClearCache();
            StatusText = "조회 가능한 날짜를 선택해 주세요.";
            return;
        }
        CancelRequests();
        using var lifetime = new CancellationTokenSource();
        _requestCancellation = lifetime;
        var revision = Interlocked.Increment(ref _revision);
        var owner = _session.UserId!;
        IsLoading = true;
        StatusText = "완료 내역을 불러오고 있습니다.";
        try
        {
            var daily = await _api.GetDailySettlementAsync(date, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            if (!IsCurrent(revision, owner, date)) return;
            if (!IsValidDay(daily, owner, date))
                throw new FDriverApiException("완료 내역을 확인하지 못했습니다.", null);

            // 새 응답 전체를 검사한 뒤 교체합니다. 정산 금액은 서버 값을 그대로 표시합니다.
            _allItems = daily.OrderSettlements.OrderByDescending(row => row.CompletedAtUtc)
                .ThenBy(row => row.SettlementId, StringComparer.Ordinal)
                .Select(row => FDriverDailySettlementItem.From(row, OpenDetailAsync)).ToArray();
            _loadedOwner = owner;
            IsLoaded = true;
            PageNumber = Math.Clamp(PageNumber, 1, PageCount);
            RenderPage();
            TotalCountText = $"완료 {daily.CompletedOrderCount:N0}건";
            GrossTotalText = daily.GrossAmountTotal.HasValue
                ? $"배달료 합계 {daily.GrossAmountTotal.Value:#,0.##}원"
                : $"확인된 배달료 {daily.KnownGrossAmountTotal:#,0.##}원 · 전체 합계 확인 중";
            StatusText = "한국 시간 전달 완료일 기준";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (FDriverApiException ex) when (!lifetime.IsCancellationRequested)
        {
            if (!IsCurrent(revision, owner, date)) return;
            if (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                ClearCache();
                IsAuthenticationRequired = true;
                StatusText = ex.StatusCode == HttpStatusCode.Unauthorized
                    ? "기사 로그인이 필요합니다." : "기사 권한이 있는 계정으로 로그인해 주세요.";
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    try { await _session.ClearAsync(); }
                    catch (Exception clearError) when (clearError is not OperationCanceledException
                        && _session.CurrentState == ClientAuthSessionRestoreState.Anonymous) { }
                }
            }
            else
                // 임의 서버 오류 본문이나 오래된 성공 상태를 정상 빈 목록으로 표시하지 않습니다.
                StatusText = "완료 내역을 조회하지 못했습니다. 다시 조회해 주세요."
                    + (IsLoaded ? " 이전 조회 결과입니다." : string.Empty);
        }
        finally
        {
            if (revision == Volatile.Read(ref _revision)) IsLoading = false;
            Interlocked.CompareExchange(ref _requestCancellation, null, lifetime);
        }
    }

    private bool CanRefresh() => !IsLoading;
    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private void PreviousPage() { if (CheckSession() && HasPreviousPage) PageNumber--; }
    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private void NextPage() { if (CheckSession() && HasNextPage) PageNumber++; }

    partial void OnSelectedDateChanged(DateTime value)
    {
        var date = DateOnly.FromDateTime(value);
        if (date == _selectedDay) return;
        _selectedDay = date;
        ClearCache();
        if (_active && _foreground) _ = RefreshAsync();
    }

    partial void OnPageNumberChanged(int value)
    {
        var bounded = Math.Clamp(value, 1, PageCount);
        if (bounded != value) { PageNumber = bounded; return; }
        RenderPage();
    }

    partial void OnIsLoadingChanged(bool value) => NotifyPaging();
    partial void OnIsLoadedChanged(bool value) => NotifyPaging();

    private void RenderPage()
    {
        Items.Clear();
        foreach (var item in _allItems.Skip((PageNumber - 1) * PageSize).Take(PageSize)) Items.Add(item);
        NotifyPaging();
    }

    private void NotifyPaging()
    {
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasNoItems));
        RefreshCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private async Task OpenDetailAsync(FDriverDailySettlementItem item)
    {
        if (!_active || !_foreground || _navigationInProgress || IsLoading || !CheckSession()
            || !Items.Contains(item) || item.DriverId != _session.UserId) return;
        _navigationInProgress = true;
        try
        {
            await _navigator.OpenDetailAsync(new FDriverCompletedDeliveryNavigationTarget(
                item.SettlementId, DateOnly.FromDateTime(SelectedDate), item.Display.OrderNo, item.DeliveryAttemptId));
        }
        finally { _navigationInProgress = false; }
    }

    private bool CheckSession()
    {
        if (!HasDriverSession())
        {
            ClearCache();
            IsAuthenticationRequired = true;
            StatusText = "기사 로그인이 필요합니다.";
            return false;
        }
        if (_loadedOwner is not null && _loadedOwner != _session.UserId) ClearCache();
        IsAuthenticationRequired = false;
        return true;
    }

    private bool HasDriverSession() => _session.CurrentState != ClientAuthSessionRestoreState.Anonymous
        && !string.IsNullOrWhiteSpace(_session.UserId)
        && (_session.Roles.Contains("Driver", StringComparer.OrdinalIgnoreCase)
            || _session.Roles.Contains("기사", StringComparer.OrdinalIgnoreCase));

    private bool IsCurrent(long revision, string owner, DateOnly date)
        => revision == Volatile.Read(ref _revision) && _active && _foreground && HasDriverSession()
            && owner == _session.UserId && date == DateOnly.FromDateTime(SelectedDate);

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        var previous = _observedOwner;
        var current = _session.UserId;
        _observedOwner = current;
        if (previous == current && HasDriverSession()) return;
        // 늦은 응답은 UI dispatcher가 계정 변경을 처리하기 전에도 차단합니다.
        var revision = CancelRequests();
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (revision != Volatile.Read(ref _revision) || current != _session.UserId) return;
            ClearCache();
            IsAuthenticationRequired = !HasDriverSession();
            StatusText = IsAuthenticationRequired ? "기사 로그인이 필요합니다."
                : "계정이 바뀌었습니다. 새로고침하면 현재 계정의 배달을 확인할 수 있습니다.";
        });
    }

    private long CancelRequests()
    {
        var revision = Interlocked.Increment(ref _revision);
        var cancellation = Interlocked.Exchange(ref _requestCancellation, null);
        try { cancellation?.Cancel(); } catch (ObjectDisposedException) { }
        return revision;
    }

    private void ClearCache()
    {
        CancelRequests();
        _allItems = [];
        _loadedOwner = null;
        IsLoaded = false;
        IsLoading = false;
        PageNumber = 1;
        RenderPage();
        TotalCountText = "완료 내역 조회 전";
        GrossTotalText = "배달료 합계 조회 전";
        StatusText = "날짜를 선택하면 완료한 배달을 조회합니다.";
    }

    private static bool IsValidDay(FoodDeliveryDailySettlementDto daily, string owner, DateOnly date)
    {
        if (daily is null || daily.DriverId != owner || daily.CompletionDateKst != date || !daily.IsFullDayQuery
            || daily.OrderSettlements is null || daily.CompletedOrderCount != daily.OrderSettlements.Count) return false;
        var startUtc = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-9), DateTimeKind.Utc);
        var endUtc = startUtc.AddDays(1);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        return daily.OrderSettlements.All(row => row is not null && row.DriverId == owner
            && !string.IsNullOrWhiteSpace(row.SettlementId) && ids.Add(row.SettlementId)
            && !string.IsNullOrWhiteSpace(row.OrderNo) && !string.IsNullOrWhiteSpace(row.DeliveryAttemptId)
            && row.PricingBreakdown is not null && row.CompletedAtUtc.Kind != DateTimeKind.Local
            && row.CompletedAtUtc >= startUtc && row.CompletedAtUtc < endUtc);
    }
}
