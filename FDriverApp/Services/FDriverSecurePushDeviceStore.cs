using System.Text.Json;

namespace FDriverApp.Services;

public sealed class FDriverSecurePushDeviceStore : IFDriverPushDeviceStore
{
    private const string Key = "ssalddel.fdriver.food-push-device.v1";
    private readonly SemaphoreSlim _gate = new(1, 1);
#if ANDROID && FDRIVER_FIREBASE
    public bool IsConfigured => true;
#else
    public bool IsConfigured => false;
#endif
    public bool AreNotificationsEnabled
    {
        get
        {
#if ANDROID
            var context = global::Android.App.Application.Context;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Tiramisu
                && context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications)
                    != global::Android.Content.PM.Permission.Granted) return false;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            if (manager is null || !manager.AreNotificationsEnabled()) return false;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O
                && manager.GetNotificationChannel(FDriverFoodNotificationService.ChannelId)?.Importance == global::Android.App.NotificationImportance.None)
                return false;
            return true;
#else
            return false;
#endif
        }
    }
    public async Task<FDriverPushDeviceState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var json = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(Key);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(json))
            {
                var state = JsonSerializer.Deserialize<FDriverPushDeviceState>(json)
                    ?? throw new InvalidDataException("알림 설치 정보를 확인하지 못했습니다.");
                Validate(state); return state;
            }
            var created = new FDriverPushDeviceState(Guid.NewGuid().ToString("N"));
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(created));
            cancellationToken.ThrowIfCancellationRequested(); return created;
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(FDriverPushDeviceState state, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); Validate(state);
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(state));
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { _gate.Release(); }
    }

    private static void Validate(FDriverPushDeviceState value)
    {
        if (!Guid.TryParseExact(value.DeviceInstallationId, "N", out _)
            || value.PushToken?.Length is > 2000 || value.RegisteredOwnerId?.Length is > 200
            || value.RevocationPending && string.IsNullOrWhiteSpace(value.RegisteredOwnerId))
            throw new InvalidDataException("알림 설치 정보를 확인해 주세요.");
    }
}
