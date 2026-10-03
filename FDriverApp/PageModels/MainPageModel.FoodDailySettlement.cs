using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Contracts.Food;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private CancellationTokenSource? _dailySettlementCancellation;
    private long _dailySettlementRevision;
    [ObservableProperty] private DateTime _selectedSettlementDate = DateTime.UtcNow.AddHours(9).Date;
    [ObservableProperty] private bool _isDailySettlementLoading;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNoDailySettlementItems))] private bool _isDailySettlementLoaded;
    [ObservableProperty] private string _dailySettlementStatus = "날짜를 선택하면 완료한 배달을 조회합니다.";
    [ObservableProperty] private string _dailySettlementCountText = "완료 내역 조회 전";
    [ObservableProperty] private string _dailySettlementGrossText = "조회 전";
    [ObservableProperty] private string _dailySettlementDeductionText = "조회 전";
    [ObservableProperty] private string _dailySettlementNetText = "조회 전";
    [ObservableProperty] private string _dailySettlementPaymentText = "입금 확인 전";
    public ObservableCollection<FDriverDailySettlementItem> DailySettlementItems { get; } = [];
    public bool HasNoDailySettlementItems => IsDailySettlementLoaded && DailySettlementItems.Count == 0;

    partial void OnSelectedSettlementDateChanged(DateTime value)
    {
        ClearDailySettlementValues();
        if (_workspaceActive && IsAuthenticated)
            _ = RefreshDailySettlementAsync();
    }

    [RelayCommand]
    private Task RefreshDailySettlement() => RefreshDailySettlementAsync();

    public async Task RefreshDailySettlementAsync()
    {
        if (!_workspaceActive || !IsAuthenticated || _workspaceCancellation.IsCancellationRequested)
            return;
        _dailySettlementCancellation?.Cancel();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_workspaceCancellation.Token);
        _dailySettlementCancellation = lifetime;
        var revision = ++_dailySettlementRevision;
        var date = DateOnly.FromDateTime(SelectedSettlementDate);
        var owner = _authSession.UserId;
        IsDailySettlementLoading = true;
        DailySettlementStatus = "완료 내역을 불러오고 있습니다.";
        try
        {
            var daily = await _api.GetDailySettlementAsync(date, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            if (revision != _dailySettlementRevision || !IsAuthenticated || owner != _authSession.UserId)
                return;
            if (daily.DriverId != owner || daily.CompletionDateKst != date || !daily.IsFullDayQuery
                || daily.CompletedOrderCount != daily.OrderSettlements.Count
                || daily.OrderSettlements.Any(row => row.DriverId != owner))
                throw new FDriverApiException("해당 날짜의 전체 정산을 확인하지 못했습니다. 다시 조회해 주세요.", null);
            DailySettlementItems.Clear();
            foreach (var item in daily.OrderSettlements)
                DailySettlementItems.Add(FDriverDailySettlementItem.From(item));
            IsDailySettlementLoaded = true;
            OnPropertyChanged(nameof(HasNoDailySettlementItems));
            DailySettlementCountText = $"완료 {daily.CompletedOrderCount:N0}건 · 수령 확인 {daily.ReceiptConfirmedOrderCount:N0}건";
            DailySettlementGrossText = daily.GrossAmountTotal.HasValue
                ? Amount(daily.GrossAmountTotal)
                : $"확인된 배달료 {Amount(daily.KnownGrossAmountTotal)} · {daily.MissingGrossAmountCount:N0}건 확인 중";
            DailySettlementDeductionText = daily.DeductionAmountTotal.HasValue
                ? Amount(daily.DeductionAmountTotal) : $"{daily.UnconfirmedDeductionCount:N0}건 공제 확인 중";
            DailySettlementNetText = daily.NetAmountTotal.HasValue
                ? Amount(daily.NetAmountTotal) : $"{daily.UnconfirmedNetAmountCount:N0}건 수령액 확인 중";
            DailySettlementPaymentText = daily.ActualTransferAmountTotal.HasValue
                ? $"입금 확인 {Amount(daily.ActualTransferAmountTotal)}"
                : daily.SimulationSucceededOrderCount > 0
                    ? $"지급 테스트 완료 {daily.SimulationSucceededOrderCount:N0}건 · 실제 입금 확인 전"
                    : "실제 입금 확인 전";
            DailySettlementStatus = $"한국 시간 전달 완료일 기준 · {daily.UpdatedAtUtc.AddHours(9):HH:mm} 갱신";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (FDriverApiException ex) when (!lifetime.IsCancellationRequested)
        {
            if (revision != _dailySettlementRevision || owner != _authSession.UserId) return;
            DailySettlementStatus = $"조회하지 못했습니다. {ShortMessage(ex.Message)}"
                + (IsDailySettlementLoaded ? " 이전 조회 결과입니다." : string.Empty);
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                CancelWorkspaceLifetime();
                await ClearAuthenticationAsync();
            }
        }
        finally
        {
            if (revision == _dailySettlementRevision)
                IsDailySettlementLoading = false;
            if (ReferenceEquals(_dailySettlementCancellation, lifetime))
                _dailySettlementCancellation = null;
        }
    }

    private void ClearDailySettlementValues()
    {
        _dailySettlementRevision++;
        _dailySettlementCancellation?.Cancel();
        DailySettlementItems.Clear();
        IsDailySettlementLoaded = false;
        OnPropertyChanged(nameof(HasNoDailySettlementItems));
        IsDailySettlementLoading = false;
        DailySettlementCountText = "완료 내역 조회 전";
        DailySettlementGrossText = "조회 전";
        DailySettlementDeductionText = "조회 전";
        DailySettlementNetText = "조회 전";
        DailySettlementPaymentText = "입금 확인 전";
        DailySettlementStatus = "날짜를 선택하면 완료한 배달을 조회합니다.";
    }

    private static string Amount(decimal? value) => value.HasValue ? $"{value.Value:#,0.##}원" : "확인 전";
}
