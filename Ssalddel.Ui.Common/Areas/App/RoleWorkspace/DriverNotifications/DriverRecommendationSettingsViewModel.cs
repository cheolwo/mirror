using Ssalddel.Contracts.Driver.Notification;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverNotifications;

public sealed class DriverRecommendationSettingsViewModel : IDisposable
{
    private readonly IDriverRecommendationSettingsClient _client;
    private CancellationTokenSource _lifetime = new();
    private RoleWorkspaceIdentity? _identity;
    private 기사알림설정응답? _confirmed;
    private bool _disposed;
    public event Action? Changed;
    public bool IsBusy { get; private set; }
    public bool IsLoaded => _confirmed is not null;
    public bool CanEdit => !IsBusy && IsLoaded;
    public bool RequiresLogin { get; private set; }
    public bool Enabled { get; set; }
    public bool FoodEnabled { get; set; }
    public bool CargoEnabled { get; set; }
    public bool Sound { get; set; }
    public bool Vibrate { get; set; }
    public bool QuietAtNight { get; set; }
    public bool DrivingPushOnly { get; set; }
    public bool CollectWhenStopped { get; set; }
    public string Status { get; private set; } = "알림 설정을 확인해 주세요.";

    public DriverRecommendationSettingsViewModel(IDriverRecommendationSettingsClient client)
    {
        _client = client;
        _client.Changed += SessionChanged;
    }

    private bool Current(CancellationToken ct) => !_disposed && !ct.IsCancellationRequested
        && _identity is { IsAuthenticated: true } && _client.Identity == _identity;
    private void SessionChanged()
    {
        if (_identity is null || _identity == _client.Identity) return;
        _lifetime.Cancel(); _confirmed = null; IsBusy = false;
        Enabled = FoodEnabled = CargoEnabled = Sound = Vibrate = QuietAtNight = DrivingPushOnly = CollectWhenStopped = false;
        RequiresLogin = !_client.Identity.IsAuthenticated;
        Status = "로그인 상태가 바뀌었습니다. 설정을 다시 조회해 주세요.";
        Changed?.Invoke();
    }

    public async Task LoadAsync()
    {
        if (_disposed || IsBusy) return;
        _lifetime.Cancel(); _lifetime.Dispose(); _lifetime = new();
        var ct = _lifetime.Token;
        _identity = null; _confirmed = null; IsBusy = true; RequiresLogin = false;
        Status = "알림 설정을 확인하고 있습니다."; Changed?.Invoke();
        try
        {
            await _client.InitializeAsync(ct);
            ct.ThrowIfCancellationRequested();
            _identity = _client.Identity;
            if (!_identity.IsAuthenticated || string.IsNullOrWhiteSpace(_identity.OwnerId))
            { RequiresLogin = true; Status = "기사 로그인 후 알림 설정을 확인해 주세요."; return; }
            var result = await _client.LoadAsync(ct);
            if (!Current(ct)) return;
            Apply(result);
            Status = "받을 추천을 선택한 뒤 저장해 주세요.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch { if (!_disposed && !ct.IsCancellationRequested) Status = "설정을 확인하지 못했습니다. 다시 조회해 주세요."; }
        finally { if (!_disposed && _lifetime.Token == ct) { IsBusy = false; Changed?.Invoke(); } }
    }

    public async Task SaveAsync()
    {
        if (!CanEdit || _confirmed is null) return;
        var ct = _lifetime.Token;
        if (!Current(ct)) { SessionChanged(); return; }
        var request = new 기사알림설정수정요청
        {
            배차추천알림사용 = Enabled, 음식배달추천알림사용 = FoodEnabled, 화물운송추천알림사용 = CargoEnabled,
            소리사용 = Sound, 진동사용 = Vibrate, 야간알림제한 = QuietAtNight,
            운전중푸시만사용 = DrivingPushOnly, 정차후모아보기 = CollectWhenStopped
        };
        IsBusy = true; _confirmed = null; Status = "알림 설정을 저장하고 있습니다."; Changed?.Invoke();
        try
        {
            await _client.SaveAsync(request, ct);
            if (!Current(ct)) return;
            var result = await _client.LoadAsync(ct);
            if (!Current(ct)) return;
            Apply(result);
            Status = "저장 후 확인한 설정입니다.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch { if (Current(ct)) Status = "저장 결과를 확인하지 못했습니다. 다시 조회한 뒤 확인해 주세요."; }
        finally { if (!_disposed && _lifetime.Token == ct) { IsBusy = false; Changed?.Invoke(); } }
    }

    private void Apply(기사알림설정응답? settings)
    {
        if (settings is null || settings.DriverId != _identity?.OwnerId) throw new InvalidDataException();
        _confirmed = settings; Enabled = settings.배차추천알림사용;
        FoodEnabled = settings.음식배달추천알림사용; CargoEnabled = settings.화물운송추천알림사용;
        Sound = settings.소리사용; Vibrate = settings.진동사용; QuietAtNight = settings.야간알림제한;
        DrivingPushOnly = settings.운전중푸시만사용; CollectWhenStopped = settings.정차후모아보기;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _client.Changed -= SessionChanged; _lifetime.Cancel(); _lifetime.Dispose();
    }
}
