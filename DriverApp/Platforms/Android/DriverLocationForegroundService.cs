using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Hongdal.Contracts.Driver.Work;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Devices.Sensors;

#pragma warning disable CA1416, CA1422

namespace DriverApp.Services;

[Service(
    Name = "kr.hongdal.driver.DriverLocationForegroundService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class DriverLocationForegroundService : Service
{
    public const string ActionStart = "kr.hongdal.driver.action.START_LOCATION_STREAM";
    public const string ActionStop = "kr.hongdal.driver.action.STOP_LOCATION_STREAM";
    public const string ExtraIntervalSeconds = "intervalSeconds";
    public const string ExtraPickupApproachRadiusKm = "pickupApproachRadiusKm";
    public const string ExtraDrivingStatus = "drivingStatus";

    private const int NotificationId = 21001;
    private const string ChannelId = "driver-location-stream";
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private int _intervalSeconds = 300;
    private int _연속실패횟수;
    private decimal _pickupApproachRadiusKm = 10m;
    private string _drivingStatus = "운행중";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            StopLocationStream(stopSelf: true);
            return StartCommandResult.NotSticky;
        }

        _intervalSeconds = Math.Clamp(intent?.GetIntExtra(ExtraIntervalSeconds, 300) ?? 300, 30, 900);
        _pickupApproachRadiusKm = (decimal)(intent?.GetDoubleExtra(ExtraPickupApproachRadiusKm, 10d) ?? 10d);
        _drivingStatus = intent?.GetStringExtra(ExtraDrivingStatus) ?? "운행중";

        StartForeground(NotificationId, BuildNotification());
        StartLocationStream();

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        StopLocationStream(stopSelf: false);
        base.OnDestroy();
    }

    private void StartLocationStream()
    {
        if (_loopTask is { IsCompleted: false })
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLocationLoopAsync(_cts.Token));
    }

    private async Task RunLocationLoopAsync(CancellationToken cancellationToken)
    {
        var authSession = DriverAppServiceProvider.Services.GetRequiredService<IAuthSession>();
        await authSession.RestoreAsync(cancellationToken);
        var api = DriverAppServiceProvider.Services.GetRequiredService<IDriverWorkApiService>();
        var profile = DriverAppServiceProvider.Services.GetRequiredService<DriverAppProfile>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var location = await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(20)),
                    cancellationToken);

                if (location is not null)
                {
                    var response = await api.위치갱신Async(new 기사위치갱신요청
                    {
                        AppKey = profile.AppKey,
                        위도 = (decimal)location.Latitude,
                        경도 = (decimal)location.Longitude,
                        정확도_m = location.Accuracy.HasValue ? (decimal)location.Accuracy.Value : null,
                        상차접근허용반경Km = _pickupApproachRadiusKm,
                        운행상태 = _drivingStatus,
                        기록시각 = DateTime.UtcNow
                    }, cancellationToken);

                    if (response?.권장위치전송간격초 is > 0)
                    {
                        _intervalSeconds = Math.Clamp(response.권장위치전송간격초, 30, 900);
                    }

                    _연속실패횟수 = 0;
                }
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _연속실패횟수++;
                Log.Warn(nameof(DriverLocationForegroundService), $"기사 위치 송신 실패: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(다음송신대기초()), cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
        }
    }

    private int 다음송신대기초()
    {
        if (_연속실패횟수 <= 0)
        {
            return _intervalSeconds;
        }

        var backoffSeconds = _intervalSeconds * (int)Math.Pow(2, Math.Min(_연속실패횟수, 3));
        return Math.Clamp(backoffSeconds, _intervalSeconds, 900);
    }

    private void StopLocationStream(bool stopSelf)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
        {
            StopForeground(StopForegroundFlags.Remove);
        }
        else
        {
#pragma warning disable CS0618
            StopForeground(true);
#pragma warning restore CS0618
        }

        if (stopSelf)
        {
            StopSelf();
        }
    }

    private Notification BuildNotification()
    {
        EnsureNotificationChannel();

        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? string.Empty);
        var pendingIntent = launchIntent is null
            ? null
            : PendingIntent.GetActivity(
                this,
                0,
                launchIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);

        builder
            .SetContentTitle("살뜰 운행 위치 송신")
            .SetContentText("운행 중 서버에 현재 위치를 주기적으로 전송합니다.")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .SetShowWhen(false);

        if (pendingIntent is not null)
        {
            builder.SetContentIntent(pendingIntent);
        }

        return builder.Build();
    }

    private void EnsureNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        var channel = new NotificationChannel(
            ChannelId,
            "기사 위치 송신",
            NotificationImportance.Low)
        {
            Description = "운행 중 기사 위치를 서버에 송신하기 위한 알림"
        };

        manager?.CreateNotificationChannel(channel);
    }
}

#pragma warning restore CA1416, CA1422
