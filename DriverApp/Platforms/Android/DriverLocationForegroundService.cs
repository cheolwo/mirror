using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

#pragma warning disable CA1416, CA1422

namespace DriverApp.Services;

[Service(
    Name = "kr.ssalddel.driver.DriverLocationForegroundService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class DriverLocationForegroundService : Service
{
    public const string ActionStart = "kr.ssalddel.driver.action.START_LOCATION_STREAM";
    public const string ActionStop = "kr.ssalddel.driver.action.STOP_LOCATION_STREAM";
    public const string ExtraLeaseId = "locationLeaseId";

    private const int NotificationId = 21001;
    private const string ChannelId = "driver-location-stream";
    private Task? _loopTask;
    private Guid? _ownedLeaseId;
    private DriverLocationTransmissionSession Session
        => DriverAppServiceProvider.Services.GetRequiredService<DriverLocationTransmissionSession>();

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action != ActionStart || !Guid.TryParse(intent.GetStringExtra(ExtraLeaseId), out var leaseId)
            || !Session.IsCurrent(leaseId))
        {
            StopLocationStream(stopSelf: true);
            return StartCommandResult.NotSticky;
        }

        if (_ownedLeaseId == leaseId && _loopTask is { IsCompleted: false }) return StartCommandResult.NotSticky;
        var previous = _loopTask;
        _ownedLeaseId = leaseId;
        StartForeground(NotificationId, BuildNotification());
        // 이전 작업이 실제 끝난 뒤 다음 작업을 시작하여 취소가 늦어져도 측정 루프가 겹치지 않습니다.
        _loopTask = Task.Run(async () =>
        {
            if (previous is not null)
            {
                try { await previous; } catch (Exception) { /* 이전 송신 권한은 이미 취소됐습니다. */ }
            }
            await RunLocationLoopAsync(leaseId);
        });
        // 프로세스가 종료되면 명시적인 운행 확인이 다시 필요하며 null Intent로 추적하지 않습니다.
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        StopLocationStream(stopSelf: false);
        base.OnDestroy();
    }

    private async Task RunLocationLoopAsync(Guid leaseId)
    {
        var session = Session;
        try
        {
            if (!session.TryActivate(leaseId)) return;
            var cancellationToken = session.Lifetime(leaseId);
            while (session.IsCurrent(leaseId) && !cancellationToken.IsCancellationRequested)
            {
                await session.SampleAndSendAsync(leaseId, ReadLocationAsync, cancellationToken);
                var delay = session.NextIntervalSeconds(leaseId);
                // 대기 중에는 새 위치를 얻지 않고 계정과 권한만 확인합니다.
                for (var second = 0; second < delay && session.IsCurrent(leaseId); second++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
                    {
                        session.Stop(leaseId, "위치 권한이 해제되어 송신을 중지했습니다. 권한 확인 후 운행 확인으로 다시 시작해 주세요.");
                        break;
                    }
                }
            }
        }
        catch (System.OperationCanceledException) { }
        catch (Exception)
        {
            session.Stop(leaseId, "위치 송신 서비스를 중지했습니다. 권한과 현재 운행을 다시 확인해 주세요.");
        }
        finally
        {
            // 이전 루프의 늦은 종료가 새 송신이나 알림을 중지하지 않도록 실행 시점에도 대조합니다.
            if (_ownedLeaseId == leaseId)
            {
                session.Stop(leaseId, "위치 송신 서비스가 중지됐습니다. 현재 운행 확인으로 다시 시작할 수 있습니다.");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (_ownedLeaseId == leaseId) StopLocationStream(stopSelf: true);
                });
            }
        }
    }

    private static async Task<DriverGpsObservation?> ReadLocationAsync(CancellationToken token)
    {
        if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
            throw new DriverLocationPermissionRevokedException();
        try
        {
            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(20)), token);
            if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
                throw new DriverLocationPermissionRevokedException();
            return location is null ? null : new(location.Latitude, location.Longitude,
                location.Accuracy, location.Timestamp, location.IsFromMockProvider);
        }
        catch (Exception error) when (error is PermissionException or FeatureNotEnabledException or FeatureNotSupportedException)
        { throw new DriverLocationPermissionRevokedException(); }
    }

    private void StopLocationStream(bool stopSelf)
    {
        if (_ownedLeaseId is { } leaseId) Session.Stop(leaseId);
        _ownedLeaseId = null;

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
