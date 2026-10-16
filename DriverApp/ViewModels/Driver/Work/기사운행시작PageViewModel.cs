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
    private readonly CancellationTokenSource _pageLifetime = new();

    public 기사운행시작PageViewModel(
        기사근무기능ViewModel 근무기능,
        IDriverSampleDataService samples,
        I기사위치송신Service 위치송신,
        IDriverWorkApiService workApi,
        IAuthSession session,
        IDriverOperationalDispatchApiService? dispatchReception = null)
    {
        this.근무기능 = 하위ViewModel등록(근무기능);
        _samples = samples;
        _위치송신 = 위치송신;
        _session = session;
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
    public string 운행상태 => _samples.근무상태.운행상태;
    public bool 위치송신중 => _위치송신.IsRunning;
    public bool 운행시작불가 => 처리중 || 위치송신중;
    public bool 운행종료불가 => 처리중 || (!위치송신중 && !_startRecovery.IsServerConfirmed);
    public bool 운행시작결과미확인 => _startRecovery.Pending is not null;

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
        await _startRecovery.RestoreAsync(cancellationToken);
        if (_startRecovery.Pending is { } pending)
        {
            커뮤니티운행공개 = pending.CommunityPublicationRequested;
            커뮤니티구단위위치공개동의 = pending.CoarseLocationConsent;
            메시지설정(기사Page메시지종류.주의, "이전 운행 시작 결과가 미확인입니다. 현재 운행 확인으로 같은 요청을 조회해 주세요.");
        }
        상태연쇄알림();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task 운행시작Async(CancellationToken cancellationToken)
    {
        상태문구 = null;
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
                var location = await 현재위치조회Async();
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
                    권장위치전송간격초 = 300, 상차접근허용반경Km = 10m, 운행상태 = status
                }, activeToken);
            }, token);
            token.ThrowIfCancellationRequested();
            메시지설정(result.LocationStarted ? 기사Page메시지종류.성공 : 기사Page메시지종류.주의, result.Message);
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
        var owner = _session.UserId; var revision = _session.SessionRevision;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _pageLifetime.Token);
        var token = lifetime.Token;

        try
        {
            await 근무기능.운행종료.실행Async(token);
            token.ThrowIfCancellationRequested();
            if (!_session.IsAuthenticated || revision != _session.SessionRevision || !string.Equals(owner, _session.UserId, StringComparison.Ordinal))
                throw new OperationCanceledException("로그인 정보가 변경되었습니다.", token);
            if (근무기능.운행종료.오류발생)
            {
                throw new InvalidOperationException(근무기능.운행종료.오류메시지 ?? "운행 종료에 실패했습니다.");
            }
            await _startRecovery.ClearAfterStopAsync(token);

            try
            {
                await _위치송신.StopAsync(token);
                메시지설정(기사Page메시지종류.안내, "운행을 종료했고, 위치 송신을 중지했습니다.");
            }
            catch (Exception ex)
            {
                메시지설정(
                    기사Page메시지종류.주의,
                    $"서버 운행은 종료됐지만 단말의 위치 송신 중지 확인에 실패했습니다. 앱을 다시 열어 상태를 확인해 주세요. ({ex.Message})");
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

    protected override bool 하위ViewModel처리중
        => _startRecovery.IsBusy || 근무기능.운행종료.처리중;

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

    private void 위치송신상태변경() => 상태연쇄알림();

    private void 상태연쇄알림()
    {
        OnPropertyChanged(nameof(위치송신중));
        OnPropertyChanged(nameof(운행상태));
        OnPropertyChanged(nameof(처리중));
        OnPropertyChanged(nameof(운행시작불가));
        OnPropertyChanged(nameof(운행종료불가));
        OnPropertyChanged(nameof(운행시작결과미확인));
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

    private static Task<Location?> 현재위치조회Async()
        => Geolocation.Default.GetLocationAsync(
            new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(20)));

    private static string 현재위치문구(Location location)
        => FormattableString.Invariant($"{location.Latitude:F6},{location.Longitude:F6}");
}
