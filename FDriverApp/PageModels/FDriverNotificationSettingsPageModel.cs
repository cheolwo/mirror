using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Contracts.Driver.Notification;
using Ssalddel.Contracts.Common.Notifications;
using DriverWorkKinds = Ssalddel.Contracts.Driver.Settings.DriverWorkKinds;

namespace FDriverApp.PageModels;

public sealed partial class FDriverNotificationSettingsPageModel(IFoodDeliveryDriverApiService api,
    IFDriverAuthSession auth, IFDriverFoodNotificationService alerts) : ObservableObject
{
    private CancellationTokenSource _lifetime = new();
    private 기사알림설정응답? _loaded;
    private string? _owner;
    private long _ownerRevision;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))] private bool _isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))] private bool _hasLoaded;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _foodEnabled;
    [ObservableProperty] private bool _cargoEnabled;
    [ObservableProperty] private bool _sound;
    [ObservableProperty] private bool _vibrate;
    [ObservableProperty] private bool _quietAtNight;
    [ObservableProperty] private string _status = "알림 설정 확인 전";
    public bool CanEdit => HasLoaded && !IsBusy;
    public string FoodIconPath => DriverRecommendationAlertPresentation.Resolve(DriverWorkKinds.FoodDelivery)!.IconPath;
    public string CargoIconPath => DriverRecommendationAlertPresentation.Resolve(DriverWorkKinds.CargoTransport)!.IconPath;

    public async Task ActivateAsync()
    {
        _lifetime.Cancel(); _lifetime.Dispose(); _lifetime = new(); IsBusy = false;
        auth.SessionChanged -= SessionChanged; auth.SessionChanged += SessionChanged;
        if (Current(_owner, _lifetime.Token) && HasLoaded && HasUnsavedChanges)
        {
            Status = "저장 전 변경을 유지했습니다. 적용하려면 저장해 주세요.";
            return;
        }
        await LoadAsync();
    }
    public void Deactivate() { auth.SessionChanged -= SessionChanged; _lifetime.Cancel(); }
    private void SessionChanged(object? sender, EventArgs args)
    {
        if (_owner != auth.UserId || _ownerRevision != auth.SessionRevision || !auth.IsAuthenticated)
        { _lifetime.Cancel(); _loaded = null; HasLoaded = false; Enabled = FoodEnabled = CargoEnabled = Sound = Vibrate = QuietAtNight = false; Status = "다시 로그인한 뒤 설정을 확인해 주세요."; }
    }
    private bool Current(string? owner, CancellationToken token)
        => !token.IsCancellationRequested && auth.IsAuthenticated && !string.IsNullOrWhiteSpace(owner) && auth.UserId == owner
            && auth.SessionRevision == _ownerRevision;
    private bool HasUnsavedChanges => _loaded is { } loaded
        && (Enabled != loaded.배차추천알림사용 || FoodEnabled != loaded.음식배달추천알림사용 || CargoEnabled != loaded.화물운송추천알림사용 || Sound != loaded.소리사용
            || Vibrate != loaded.진동사용 || QuietAtNight != loaded.야간알림제한);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        var token = _lifetime.Token; var owner = auth.UserId;
        if (_owner != owner || _ownerRevision != auth.SessionRevision || !auth.IsAuthenticated)
            Enabled = FoodEnabled = CargoEnabled = Sound = Vibrate = QuietAtNight = false;
        _owner = owner; _ownerRevision = auth.SessionRevision; HasLoaded = false; _loaded = null;
        if (!Current(owner, token)) { Status = "로그인한 뒤 알림 설정을 확인해 주세요."; return; }
        IsBusy = true; Status = "알림 설정을 확인하고 있습니다.";
        try
        {
            var requestedAtUtc = DateTime.UtcNow;
            var settings = await api.GetNotificationSettingsAsync(token);
            if (!Current(owner, token)) return;
            Apply(owner!, settings, requestedAtUtc);
            Status = "배차 수신과 알림 허용은 별도입니다. 소리·진동은 기기 설정도 따릅니다.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (Current(owner, token)) Status = "설정을 확인하지 못했습니다. 다시 조회해 주세요."; }
        finally { if (_lifetime.Token == token) IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || !HasLoaded || _loaded is null) return;
        var token = _lifetime.Token; var owner = _owner;
        if (!Current(owner, token)) return;
        var request = new 기사알림설정수정요청
        {
            배차추천알림사용 = Enabled, 소리사용 = Sound, 진동사용 = Vibrate, 야간알림제한 = QuietAtNight,
            음식배달추천알림사용 = FoodEnabled, 화물운송추천알림사용 = CargoEnabled,
            운전중푸시만사용 = _loaded.운전중푸시만사용, 정차후모아보기 = _loaded.정차후모아보기
        };
        IsBusy = true; HasLoaded = false; Status = "알림 설정을 저장하고 있습니다.";
        try
        {
            alerts.BeginSettingsChange(owner!, DateTime.UtcNow);
            await api.UpdateNotificationSettingsAsync(request, token);
            if (!Current(owner, token)) return;
            var requestedAtUtc = DateTime.UtcNow;
            var confirmed = await api.GetNotificationSettingsAsync(token);
            if (!Current(owner, token)) return;
            Apply(owner!, confirmed, requestedAtUtc);
            Status = "저장한 알림 설정을 확인했습니다.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch
        {
            if (Current(owner, token)) { HasLoaded = false; Status = "저장 결과를 확인하지 못했습니다. 다시 조회한 뒤 확인해 주세요."; }
        }
        finally { if (_lifetime.Token == token) IsBusy = false; }
    }

    private void Apply(string owner, 기사알림설정응답? settings, DateTime requestedAtUtc)
    {
        if (settings is null || settings.DriverId != owner) throw new InvalidDataException("설정 계정이 일치하지 않습니다.");
        _loaded = settings; Enabled = settings.배차추천알림사용; FoodEnabled = settings.음식배달추천알림사용;
        CargoEnabled = settings.화물운송추천알림사용; Sound = settings.소리사용;
        Vibrate = settings.진동사용; QuietAtNight = settings.야간알림제한; HasLoaded = true;
        alerts.ConfirmSettings(owner, settings, requestedAtUtc);
    }
}
