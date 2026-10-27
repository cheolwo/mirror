using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

#pragma warning disable CA1416

namespace DriverApp.Services;

public sealed class Android기사위치송신Service : I기사위치송신Service
{
    private readonly DriverLocationTransmissionSession _session;
    private readonly DriverAppProfile _profile;
    private readonly SemaphoreSlim _transition = new(1, 1);

    public Android기사위치송신Service(DriverLocationTransmissionSession session, DriverAppProfile profile)
    {
        _session = session; _profile = profile;
        _session.Changed += () => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
    }

    public bool IsRunning => _session.IsRunning;
    public string? Notice => _session.Notice;
    public event Action? Changed;

    public async Task StartAsync(기사위치송신시작요청 request, CancellationToken cancellationToken = default)
    {
        await _transition.WaitAsync(cancellationToken);
        Guid? leaseId = null;
        try
        {
            leaseId = await _session.PrepareAsync(new(_profile.AppKey,
                request.권장위치전송간격초, request.상차접근허용반경Km), cancellationToken);
            var context = Platform.AppContext;
            var intent = new Intent(context, typeof(DriverLocationForegroundService));
            intent.SetAction(DriverLocationForegroundService.ActionStart);
            intent.PutExtra(DriverLocationForegroundService.ExtraLeaseId, leaseId.Value.ToString("D"));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O) context.StartForegroundService(intent);
            else context.StartService(intent);
            // 시작 Intent 접수만으로 송신 중이라고 표시하지 않고 실제 서비스의 활성 응답을 기다립니다.
            await _session.WaitActivatedAsync(leaseId.Value, cancellationToken);
        }
        catch
        {
            if (leaseId.HasValue) _session.Stop(leaseId, "위치 송신 서비스를 시작하지 못했습니다. 현재 운행 확인으로 다시 시도해 주세요.");
            throw;
        }
        finally { _transition.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        // 시작 응답을 기다리는 중이어도 진행 중인 측정과 HTTP부터 취소합니다.
        _session.Stop(notice: "위치 송신을 중지했습니다.");
        await _transition.WaitAsync(cancellationToken);
        try
        {
            _session.Stop(notice: "위치 송신을 중지했습니다.");
            Platform.AppContext.StopService(new Intent(Platform.AppContext, typeof(DriverLocationForegroundService)));
            await _session.DrainAsync(cancellationToken);
        }
        finally { _transition.Release(); }
    }
}

#pragma warning restore CA1416
