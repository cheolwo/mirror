using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Contracts.Driver.Settings;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private 기사권역복귀설정? _confirmedReturnAreaSettings;
    private string? _returnAreaOwner;
    private bool _returnAreaSessionObserved;
    private CancellationTokenSource _returnAreaLifetime = new();
    private decimal? _returnAreaLatitude;
    private decimal? _returnAreaLongitude;

    public IReadOnlyList<string> ReturnAreaModes { get; } = ["끄기", "권역 밖에서 자동 복귀", "복귀 우선 켜기"];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEditReturnAreaSettings))] private bool _returnAreaSettingsBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEditReturnAreaSettings))] private bool _returnAreaSettingsLoaded;
    [ObservableProperty] private int _returnAreaModeIndex;
    [ObservableProperty] private string _returnAreaAnchorName = "활동 중심";
    [ObservableProperty] private string _returnAreaAnchorText = "복귀 기준점 미설정";
    [ObservableProperty] private string _returnAreaEntryRadiusText = "7";
    [ObservableProperty] private string _returnAreaReleaseRadiusText = "6";
    [ObservableProperty] private bool _returnAreaRadiusOptionsVisible;
    [ObservableProperty] private string _returnAreaSettingsNotice = "설정을 조회한 뒤 복귀 기준점과 사용 방식을 선택해 주세요.";
    public bool CanEditReturnAreaSettings => ReturnAreaSettingsLoaded && !ReturnAreaSettingsBusy && HasDriverSession();

    private void ObserveReturnAreaSession()
    {
        if (_returnAreaSessionObserved) return;
        _returnAreaSessionObserved = true;
        _authSession.SessionChanged += (_, _) =>
        {
            if (_returnAreaOwner == _authSession.UserId && HasDriverSession()) return;
            // Cancel before dispatching UI cleanup so a late reply cannot race an account change.
            _returnAreaLifetime.Cancel();
            _ = MainThread.InvokeOnMainThreadAsync(ResetReturnAreaSettings);
        };
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IsAuthenticated)) OnPropertyChanged(nameof(CanEditReturnAreaSettings));
        };
    }

    private void ResetReturnAreaSettings()
    {
        _returnAreaLifetime.Cancel(); _returnAreaLifetime.Dispose(); _returnAreaLifetime = new();
        _returnAreaOwner = null; _confirmedReturnAreaSettings = null;
        _returnAreaLatitude = _returnAreaLongitude = null;
        ReturnAreaSettingsLoaded = false; ReturnAreaSettingsBusy = false;
        ReturnAreaModeIndex = 0; ReturnAreaAnchorName = "활동 중심";
        ReturnAreaAnchorText = "복귀 기준점 미설정";
        ReturnAreaEntryRadiusText = "7"; ReturnAreaReleaseRadiusText = "6";
        ReturnAreaRadiusOptionsVisible = false;
        ReturnAreaSettingsNotice = "다시 로그인한 뒤 설정을 조회해 주세요.";
    }

    private bool ReturnAreaCurrent(string? owner, CancellationToken token)
        => !token.IsCancellationRequested && HasDriverSession() && !string.IsNullOrWhiteSpace(owner) && owner == _authSession.UserId;

    [RelayCommand]
    private async Task LoadReturnAreaSettings()
    {
        ObserveReturnAreaSession();
        if (ReturnAreaSettingsBusy) return;
        var owner = _authSession.UserId; var token = _returnAreaLifetime.Token;
        _returnAreaOwner = owner; _confirmedReturnAreaSettings = null; ReturnAreaSettingsLoaded = false;
        if (!ReturnAreaCurrent(owner, token)) { ResetReturnAreaSettings(); return; }
        ReturnAreaSettingsBusy = true; ReturnAreaSettingsNotice = "복귀 설정을 확인하고 있습니다.";
        try
        {
            var result = await _api.GetReturnAreaSettingsAsync(token);
            if (!ReturnAreaCurrent(owner, token)) return;
            ApplyReturnAreaSettings(owner!, result);
            ReturnAreaSettingsNotice = "현재 위치와 복귀 기준점은 별도입니다. 사용 방식을 선택한 뒤 저장해 주세요.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (ReturnAreaCurrent(owner, token)) ReturnAreaSettingsNotice = "설정을 확인하지 못했습니다. 다시 조회해 주세요."; }
        finally { if (_returnAreaLifetime.Token == token) ReturnAreaSettingsBusy = false; }
    }

    [RelayCommand]
    private async Task UseCurrentLocationForReturnArea()
    {
        if (!CanEditReturnAreaSettings) return;
        var owner = _returnAreaOwner; var token = _returnAreaLifetime.Token;
        ReturnAreaSettingsBusy = true; ReturnAreaSettingsNotice = "현재 위치를 확인하고 있습니다.";
        try
        {
            // Explicit capture for a private fixed point; never sends or replaces the driver's live GPS.
            var location = await _locationService.GetCurrentAsync(token);
            if (!ReturnAreaCurrent(owner, token)) return;
            if (FDriverLocationFreshness.Assess(location, DateTime.UtcNow) != FDriverLocationStatus.Ready
                || location!.AccuracyMeters is null or > 100)
            { ReturnAreaSettingsNotice = "정확한 현재 위치를 확인하지 못했습니다. 안전한 곳에서 다시 시도해 주세요."; return; }
            _returnAreaLatitude = location.Latitude; _returnAreaLongitude = location.Longitude;
            ReturnAreaAnchorText = "현재 위치를 기준점으로 선택했습니다. 저장하면 적용됩니다.";
            ReturnAreaSettingsNotice = "집이나 활동 중심에 있을 때 설정해 주세요. 기준점은 본인의 설정에서만 확인합니다.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (ReturnAreaCurrent(owner, token)) ReturnAreaSettingsNotice = "위치를 확인하지 못했습니다. 기준점은 변경하지 않았습니다."; }
        finally { if (_returnAreaLifetime.Token == token) ReturnAreaSettingsBusy = false; }
    }

    [RelayCommand]
    private void ToggleReturnAreaRadiusOptions() => ReturnAreaRadiusOptionsVisible = !ReturnAreaRadiusOptionsVisible;

    [RelayCommand]
    private async Task SaveReturnAreaSettings()
    {
        if (!CanEditReturnAreaSettings || _confirmedReturnAreaSettings is null) return;
        var owner = _returnAreaOwner; var token = _returnAreaLifetime.Token;
        if (!ReturnAreaCurrent(owner, token)) return;
        if (ReturnAreaModeIndex is < 0 or > 2 || !TryRadius(ReturnAreaEntryRadiusText, out var entry)
            || !TryRadius(ReturnAreaReleaseRadiusText, out var release) || release <= 0 || entry <= release || entry > 100
            || (ReturnAreaAnchorName?.Length ?? 0) > 80 || ReturnAreaModeIndex != 0 && (_returnAreaLatitude is null || _returnAreaLongitude is null))
        { ReturnAreaSettingsNotice = "기준점을 선택하고 진입 반경보다 작은 해제 반경을 입력해 주세요."; return; }
        var expected = _confirmedReturnAreaSettings.Revision;
        var request = new 기사권역복귀설정수정요청
        {
            ExpectedRevision = expected,
            설정 = new() { Revision = expected, Mode = ReturnAreaModeIndex switch { 1 => "automatic", 2 => "on", _ => "off" },
                기준위치명 = ReturnAreaAnchorName, 기준위도 = _returnAreaLatitude, 기준경도 = _returnAreaLongitude,
                진입반경Km = entry, 해제반경Km = release }
        };
        ReturnAreaSettingsBusy = true; ReturnAreaSettingsNotice = "복귀 설정을 저장하고 있습니다.";
        try
        {
            var written = await _api.UpdateReturnAreaSettingsAsync(request, token);
            if (!ReturnAreaCurrent(owner, token)) return;
            if (written.DriverId != owner || written.설정 is null || written.설정.Revision != expected + 1)
                throw new InvalidDataException("저장한 설정의 계정 또는 판본이 일치하지 않습니다.");
            var confirmed = await _api.GetReturnAreaSettingsAsync(token);
            if (!ReturnAreaCurrent(owner, token)) return;
            if (confirmed.설정 is null || confirmed.설정.Revision < written.설정.Revision)
                throw new InvalidDataException("저장 결과를 확인하지 못했습니다.");
            ApplyReturnAreaSettings(owner!, confirmed);
            ReturnAreaSettingsNotice = confirmed.설정.Revision == written.설정.Revision
                ? "저장한 복귀 설정을 확인했습니다. 운행과 배차 수신은 그대로 유지됩니다."
                : "다른 화면에서 다시 변경된 최신 설정을 확인했습니다.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch
        {
            if (ReturnAreaCurrent(owner, token))
            { _confirmedReturnAreaSettings = null; ReturnAreaSettingsLoaded = false; ReturnAreaSettingsNotice = "저장 결과를 확인하지 못했습니다. 다시 조회한 뒤 확인해 주세요."; }
        }
        finally { if (_returnAreaLifetime.Token == token) ReturnAreaSettingsBusy = false; }
    }

    private void ApplyReturnAreaSettings(string owner, 기사권역복귀설정응답 result)
    {
        var value = result.설정;
        if (result.DriverId != owner || value is null || value.Revision < 0 || (value.기준위치명?.Length ?? 0) > 80 || value.Mode is not ("off" or "automatic" or "on")
            || value.기준위도.HasValue != value.기준경도.HasValue
            || value.기준위도 is < -90 or > 90 || value.기준경도 is < -180 or > 180
            || value.Mode != "off" && value.기준위도 is null || value.해제반경Km <= 0 || value.진입반경Km <= value.해제반경Km || value.진입반경Km > 100)
            throw new InvalidDataException("복귀 설정 응답이 올바르지 않습니다.");
        _confirmedReturnAreaSettings = value;
        ReturnAreaModeIndex = value.Mode switch { "automatic" => 1, "on" => 2, _ => 0 };
        ReturnAreaAnchorName = value.기준위치명 ?? "활동 중심";
        _returnAreaLatitude = value.기준위도; _returnAreaLongitude = value.기준경도;
        ReturnAreaAnchorText = value.기준위도.HasValue ? "저장한 기준점 있음" : "복귀 기준점 미설정";
        ReturnAreaEntryRadiusText = value.진입반경Km.ToString("0.###", CultureInfo.CurrentCulture);
        ReturnAreaReleaseRadiusText = value.해제반경Km.ToString("0.###", CultureInfo.CurrentCulture);
        ReturnAreaSettingsLoaded = true;
    }

    private static bool TryRadius(string text, out decimal value)
        => decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out value)
           || decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
}
