using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Features;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Ssalddel.Contracts.Driver.Work;

namespace DriverApp.ViewModels.Driver.Work;

public enum 기사Page메시지종류
{
    안내,
    성공,
    주의,
    오류
}

/// <summary>
/// 운행 시작 화면의 권한, 현재 위치, 근무 상태 전이와 위치 송신을 조정합니다.
/// 시작 응답 유실은 같은 요청의 서버 정본을 조회하며, 미확정 요청을 새 시작으로 대체하지 않습니다.
/// </summary>
public sealed partial class 기사운행시작PageViewModel : 기사PageViewModelBase
{
    private readonly IDriverSampleDataService _samples;
    private readonly I기사위치송신Service _위치송신;
    private readonly DriverWorkStartRecoveryCoordinator _startRecovery;
    private readonly IAuthSession _session;
    private readonly IDriverNativeMapNavigator? _mapNavigator;
    private readonly CancellationTokenSource _pageLifetime = new();
    private bool _근무표시미확인;
    private bool _근무표시조회중;
    private bool _지도여는중;
    private string? _지도인계기사Id;
    private long _지도인계세션Revision;

    public 기사운행시작PageViewModel(
        기사근무기능ViewModel 근무기능,
        IDriverSampleDataService samples,
        I기사위치송신Service 위치송신,
        IDriverWorkApiService workApi,
        IAuthSession session,
        IDriverOperationalDispatchApiService? dispatchReception = null,
        IDriverNativeMapNavigator? mapNavigator = null)
    {
        this.근무기능 = 하위ViewModel등록(근무기능);
        _samples = samples;
        _위치송신 = 위치송신;
        _session = session;
        _mapNavigator = mapNavigator;
        _startRecovery = new(workApi, session, new DriverWorkStartRecoveryStore(
            key => SecureStorage.Default.GetAsync(key),
            (key, value) => SecureStorage.Default.SetAsync(key, value),
            key => SecureStorage.Default.Remove(key)), dispatchReception);
        _startRecovery.Changed += 상태연쇄알림;
        _위치송신.Changed += 위치송신상태변경;
    }

    public 기사근무기능ViewModel 근무기능 { get; }
    // 이 화면의 시작 버튼은 즉시 운행 요청이며 조회 상태의 표시 문구를 전송하지 않습니다.
    public string 시작모드 => "바로시작";
    public string 시작위치 => _samples.근무상태.시작위치;
    public string? 복귀지 => _samples.근무상태.복귀지;
    public string 운행상태 => _근무표시미확인 ? "운행 상태 미확인" : _samples.근무상태.운행상태;
    public bool 위치송신중 => _위치송신.IsRunning;
    public bool 운행시작불가 => 처리중 || 위치송신중;
    public bool 운행종료불가 => 처리중 || (!위치송신중 && !_startRecovery.IsServerConfirmed);
    public bool 운행시작결과미확인 => _startRecovery.Pending is not null;
    public bool 지도이동가능 => !_pageLifetime.IsCancellationRequested && _session.IsAuthenticated
        && _지도인계기사Id is not null && _지도인계기사Id == _session.UserId
        && _지도인계세션Revision == _session.SessionRevision
        && _startRecovery.IsServerConfirmed && 위치송신중 && !_근무표시미확인
        && _samples.ReadState.Work.IsCurrent && _samples.근무상태.운행상태 is "운행중" or "운행 중";

    [ObservableProperty]
    public partial bool 커뮤니티운행공개 { get; set; } = true;

    [ObservableProperty]
    public partial bool 커뮤니티구단위위치공개동의 { get; set; }

    [ObservableProperty]
    public partial string? 상태문구 { get; private set; }

    [ObservableProperty]
    public partial 기사Page메시지종류 상태종류 { get; private set; } = 기사Page메시지종류.안내;

    protected override async Task 불러오기Async(bool 새로고침, CancellationToken cancellationToken)
    {
        await _samples.RefreshAsync(cancellationToken, force: 새로고침);
        _근무표시미확인 = !_samples.ReadState.Work.IsCurrent;
        await _startRecovery.RestoreAsync(cancellationToken);
        if (_startRecovery.Pending is { } pending)
        {
            커뮤니티운행공개 = pending.CommunityPublicationRequested;
            커뮤니티구단위위치공개동의 = pending.CoarseLocationConsent;
            메시지설정(기사Page메시지종류.주의, "이전 운행 시작 결과가 미확인입니다. 현재 운행 확인으로 같은 요청을 조회해 주세요.");
        }
        else if (!string.IsNullOrWhiteSpace(_위치송신.Notice))
            메시지설정(기사Page메시지종류.주의, _위치송신.Notice);
        상태연쇄알림();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task 운행시작Async(CancellationToken cancellationToken)
    {
        상태문구 = null;
        _지도인계기사Id = null;
        var owner = _session.UserId; var revision = _session.SessionRevision;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _pageLifetime.Token);
        var token = lifetime.Token;
        try
        {
            var result = await _startRecovery.StartOrRecoverAsync(async activeToken =>
            {
                if (!await 위치권한확보Async())
                {
                    메시지설정(기사Page메시지종류.주의, "위치 권한이 필요합니다. 권한을 허용한 뒤 다시 운행을 시작해 주세요.");
                    return null;
                }
                await 알림권한확보Async();
                activeToken.ThrowIfCancellationRequested();
                var location = await 현재위치조회Async(activeToken);
                activeToken.ThrowIfCancellationRequested();
                if (location is null) return null;
                return new 기사운행시작요청
                {
                    시작모드 = 시작모드, 시작시각 = DateTime.UtcNow,
                    시작위치 = 현재위치문구(location), 복귀지 = 복귀지,
                    커뮤니티운행공개 = 커뮤니티운행공개,
                    커뮤니티구단위위치공개동의 = 커뮤니티운행공개 && 커뮤니티구단위위치공개동의
                };
            }, async (status, activeToken) =>
            {
                if (!await 위치권한확보Async()) throw new PermissionException("위치 권한이 필요합니다.");
                await 알림권한확보Async();
                activeToken.ThrowIfCancellationRequested();
                await _위치송신.StartAsync(new 기사위치송신시작요청
                {
                    권장위치전송간격초 = 30, 상차접근허용반경Km = 10m, 운행상태 = status
                }, activeToken);
            }, token);
            현재계정확인(owner, revision, token);
            var summaryConfirmed = !result.ServerConfirmed || await 최신근무표시확인Async(owner, revision, token);
            현재계정확인(owner, revision, token);
            메시지설정(summaryConfirmed && result.LocationStarted ? 기사Page메시지종류.성공 : 기사Page메시지종류.주의,
                summaryConfirmed ? result.Message : result.Message + " 최신 근무 상태 표시를 확인하지 못했습니다. 다시 조회해 주세요.");
            if (result.ServerConfirmed && result.LocationStarted && summaryConfirmed)
            {
                _지도인계기사Id = owner;
                _지도인계세션Revision = revision;
                if (지도이동가능) await 지도열기Async(token);
            }
        }
        catch (OperationCanceledException) { /* A cancelled/departed page must not publish success or another user's result. */ }
        catch (PermissionException)
        {
            메시지설정(기사Page메시지종류.주의, "위치 권한이 필요합니다. 앱 설정에서 위치 권한을 허용해 주세요.");
        }
        catch (Exception ex)
        {
            메시지설정(기사Page메시지종류.오류, $"운행 시작 실패: {ex.Message}");
        }
        finally
        {
            상태연쇄알림();
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task 운행종료Async(CancellationToken cancellationToken)
    {
        상태문구 = null;
        _지도인계기사Id = null;
        var owner = _session.UserId; var revision = _session.SessionRevision;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _pageLifetime.Token);
        var token = lifetime.Token;

        try
        {
            await 근무기능.운행종료.실행Async(token);
            현재계정확인(owner, revision, token);
            if (근무기능.운행종료.오류발생)
            {
                throw new InvalidOperationException(근무기능.운행종료.오류메시지 ?? "운행 종료에 실패했습니다.");
            }
            try
            {
                await _위치송신.StopAsync(token);
            }
            catch (Exception ex)
            {
                현재계정확인(owner, revision, token);
                var summaryConfirmed = await 최신근무표시확인Async(owner, revision, token);
                현재계정확인(owner, revision, token);
                메시지설정(
                    기사Page메시지종류.주의,
                    $"서버 운행은 종료됐지만 단말의 위치 송신 중지 확인에 실패했습니다. 앱을 다시 열어 상태를 확인해 주세요. ({ex.Message})"
                    + (summaryConfirmed ? "" : " 최신 근무 상태 표시도 확인하지 못했습니다."));
                return;
            }

            현재계정확인(owner, revision, token);
            var stoppedSummaryConfirmed = await 최신근무표시확인Async(owner, revision, token);
            현재계정확인(owner, revision, token);

            // 복구 기록 정리 실패가 확정된 운행의 단말 송신 중지를 막지 않도록 중지 다음에 정리합니다.
            try
            {
                await _startRecovery.ClearAfterStopAsync(token);
                현재계정확인(owner, revision, token);
                메시지설정(stoppedSummaryConfirmed ? 기사Page메시지종류.안내 : 기사Page메시지종류.주의,
                    "운행을 종료했고, 위치 송신을 중지했습니다."
                    + (stoppedSummaryConfirmed ? "" : " 최신 근무 상태 표시는 확인하지 못했습니다. 다시 조회해 주세요."));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                현재계정확인(owner, revision, token);
                메시지설정(기사Page메시지종류.주의,
                    "운행과 위치 송신은 종료됐지만 확인 기록 정리는 완료하지 못했습니다. 현재 운행 확인으로 다시 조회해 주세요."
                    + (stoppedSummaryConfirmed ? "" : " 최신 근무 상태 표시도 확인하지 못했습니다."));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            메시지설정(기사Page메시지종류.오류, $"운행 종료 실패: {ex.Message}");
        }
        finally
        {
            상태연쇄알림();
        }
    }

    private void 현재계정확인(string? owner, long revision, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!_session.IsAuthenticated || revision != _session.SessionRevision
            || !string.Equals(owner, _session.UserId, StringComparison.Ordinal))
            throw new OperationCanceledException("로그인 정보가 변경되었습니다.", token);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task 지도열기Async(CancellationToken cancellationToken)
    {
        if (!지도이동가능 || _지도여는중) return;
        var owner = _지도인계기사Id; var revision = _지도인계세션Revision;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _pageLifetime.Token);
        var token = lifetime.Token;
        _지도여는중 = true;
        상태연쇄알림();
        try
        {
            현재계정확인(owner, revision, token);
            if (_mapNavigator is null) throw new InvalidOperationException("지도를 열 수 없습니다.");
            await _mapNavigator.OpenAsync(token);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (token.IsCancellationRequested || !_session.IsAuthenticated
                || _session.UserId != owner || _session.SessionRevision != revision) return;
            // 화면 인계 실패로 서버에서 확정된 운행과 위치 송신을 실패로 바꾸지 않습니다.
            메시지설정(기사Page메시지종류.주의, "운행과 위치 송신은 시작됐지만 지도를 열지 못했습니다. ‘운행 지도 열기’로 다시 시도해 주세요.");
        }
        finally
        {
            _지도여는중 = false;
            상태연쇄알림();
        }
    }

    private async Task<bool> 최신근무표시확인Async(string? owner, long revision, CancellationToken token)
    {
        현재계정확인(owner, revision, token);
        _근무표시미확인 = true; _근무표시조회중 = true;
        상태연쇄알림();
        try
        {
            // 시작·종료의 확정 결과와 요약 캐시의 최신 여부는 별도로 확인합니다.
            await _samples.RefreshAsync(token, force: true);
            현재계정확인(owner, revision, token);
            _근무표시미확인 = !_samples.ReadState.Work.IsCurrent;
            return !_근무표시미확인;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || !_session.IsAuthenticated
            || revision != _session.SessionRevision || !string.Equals(owner, _session.UserId, StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception)
        {
            현재계정확인(owner, revision, token);
            // 표시 조회 실패로 이미 확정한 운행·단말 중지 결과를 실패로 뒤집지 않습니다.
            return false;
        }
        finally
        {
            _근무표시조회중 = false;
            상태연쇄알림();
        }
    }

    protected override bool 하위ViewModel처리중
        => _startRecovery.IsBusy || 근무기능.운행종료.처리중 || _근무표시조회중 || _지도여는중;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pageLifetime.Cancel();
            _startRecovery.Changed -= 상태연쇄알림;
            _startRecovery.Dispose();
            _위치송신.Changed -= 위치송신상태변경;
        }

        base.Dispose(disposing);
    }

    private void 위치송신상태변경()
    {
        if (!string.IsNullOrWhiteSpace(_위치송신.Notice))
            메시지설정(기사Page메시지종류.주의, _위치송신.Notice);
        상태연쇄알림();
    }

    private void 상태연쇄알림()
    {
        OnPropertyChanged(nameof(위치송신중));
        OnPropertyChanged(nameof(운행상태));
        OnPropertyChanged(nameof(처리중));
        OnPropertyChanged(nameof(운행시작불가));
        OnPropertyChanged(nameof(운행종료불가));
        OnPropertyChanged(nameof(운행시작결과미확인));
        OnPropertyChanged(nameof(지도이동가능));
    }

    private void 메시지설정(기사Page메시지종류 종류, string message)
    {
        상태종류 = 종류;
        상태문구 = message;
    }

    private static async Task<bool> 위치권한확보Async()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        return status == PermissionStatus.Granted;
    }

    private static async Task 알림권한확보Async()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
        }
#else
        await Task.CompletedTask;
#endif
    }

    private static async Task<Location?> 현재위치조회Async(CancellationToken token)
    {
        var location = await Geolocation.Default.GetLocationAsync(
            new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(20)), token);
        return location is not null && DriverLocationTransmissionSession.IsValid(new(
            location.Latitude, location.Longitude, location.Accuracy, location.Timestamp,
            location.IsFromMockProvider), DateTimeOffset.UtcNow) ? location : null;
    }

    private static string 현재위치문구(Location location)
        => FormattableString.Invariant($"{location.Latitude:F6},{location.Longitude:F6}");
}
