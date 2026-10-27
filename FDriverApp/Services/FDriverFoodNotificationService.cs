using System.Text.Json;
using Microsoft.Maui.Storage;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Driver.Notification;
using DriverWorkKinds = Ssalddel.Contracts.Driver.Settings.DriverWorkKinds;

namespace FDriverApp.Services;

public sealed class FDriverFoodNotificationService : IFDriverFoodNotificationService
{
    public const string OpenAction = "kr.ssalddel.fdriver.OPEN_RECOMMENDATION";
    public const string UserExtra = "food_notification_user";
    public const string OfferExtra = "food_notification_offer";
    private const string ReceiptKey = "ssalddel.fdriver.foodNotification.receipt.v1";
    private const string TargetKey = "ssalddel.fdriver.foodNotification.target.v1";
    private const int NotificationId = 31081;
    internal const string ChannelId = "food_dispatch_recommendations";
    internal const string ChannelPrefix = "food_dispatch_alerts.v2";
    private const string SettingsKey = "ssalddel.fdriver.alert.settings.v1";
    private const string SettingsFenceKey = "ssalddel.fdriver.alert.settings-fence.v1";
    private const string PublishedKey = "ssalddel.fdriver.alert.published.v1";
    private readonly object _publishGate = new();
    private string? _pendingSettingsOwner;
    private sealed record SettingsSnapshot(string OwnerId, 기사알림설정응답 Settings, DateTime CheckedAtUtc);
    private sealed record PublishedSnapshot(string OwnerId, IReadOnlyList<FDriverPublishedAlert> Alerts, string? WorkKind = null);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FDriverFoodNotificationReceipt? ReadReceipt() => Read<FDriverFoodNotificationReceipt>(ReceiptKey);
    public void WriteReceipt(FDriverFoodNotificationReceipt receipt)
        => Preferences.Default.Set(ReceiptKey, JsonSerializer.Serialize(receipt, JsonOptions));
    public FDriverFoodNotificationTarget? ReadOpenTarget() => Read<FDriverFoodNotificationTarget>(TargetKey);
    public void SetOpenTarget(FDriverFoodNotificationTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.UserId) || target.UserId.Length > 200
            || string.IsNullOrWhiteSpace(target.OfferId) || target.OfferId.Length > 200) return;
        Preferences.Default.Set(TargetKey, JsonSerializer.Serialize(target, JsonOptions));
    }
    public void ClearOpenTarget() => Preferences.Default.Remove(TargetKey);
    public void ClearAccount()
    {
        lock (_publishGate)
        {
            Cancel(); ClearOpenTarget(); Preferences.Default.Remove(ReceiptKey);
            Preferences.Default.Remove(SettingsKey); Preferences.Default.Remove(PublishedKey);
        }
    }

    public void ApplySettings(string ownerId, 기사알림설정응답 settings, DateTime checkedAtUtc)
        => ApplySettingsCore(ownerId, settings, checkedAtUtc, explicitConfirmation: false);

    public void ConfirmSettings(string ownerId, 기사알림설정응답 settings, DateTime readStartedAtUtc)
        => ApplySettingsCore(ownerId, settings, readStartedAtUtc, explicitConfirmation: true);

    public void BeginSettingsChange(string ownerId, DateTime beganAtUtc)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        // Also stop an already-running publication before waiting for its lock.
        Volatile.Write(ref _pendingSettingsOwner, ownerId);
        lock (_publishGate)
        {
            Cancel();
            Preferences.Default.Set(SettingsFenceKey, JsonSerializer.Serialize(new FDriverNotificationSettingsFence(ownerId, beganAtUtc), JsonOptions));
            if (Read<SettingsSnapshot>(SettingsKey)?.OwnerId == ownerId) Preferences.Default.Remove(SettingsKey);
        }
    }

    private void ApplySettingsCore(string ownerId, 기사알림설정응답 settings, DateTime checkedAtUtc, bool explicitConfirmation)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || settings.DriverId != ownerId) return;
        lock (_publishGate)
        {
            var fence = Read<FDriverNotificationSettingsFence>(SettingsFenceKey);
            if (!FDriverNotificationPublicationGate.CanApplySettings(fence, ownerId, checkedAtUtc, explicitConfirmation)) return;
            var prior = Read<SettingsSnapshot>(SettingsKey);
            if (prior?.OwnerId == ownerId && prior.CheckedAtUtc > checkedAtUtc) return;
            Preferences.Default.Set(SettingsKey, JsonSerializer.Serialize(new SettingsSnapshot(ownerId, settings, checkedAtUtc), JsonOptions));
            if (fence?.OwnerId == ownerId && fence.ConfirmedReadAtUtc is null && explicitConfirmation)
            {
                Preferences.Default.Set(SettingsFenceKey, JsonSerializer.Serialize(fence with { ConfirmedReadAtUtc = checkedAtUtc }, JsonOptions));
                Interlocked.CompareExchange(ref _pendingSettingsOwner, null, ownerId);
            }
            var published = Read<PublishedSnapshot>(PublishedKey);
            var visibleKind = published?.OwnerId == ownerId ? published.WorkKind ?? DriverWorkKinds.FoodDelivery : DriverWorkKinds.FoodDelivery;
            if (!FDriverNotificationPublicationGate.AllowsAlert(settings, visibleKind)) Cancel();
        }
    }

    public bool Publish(string userId, string offerId, DateTime expiresAtUtc)
        => PublishForWorkKind(userId, offerId, expiresAtUtc, DriverWorkKinds.FoodDelivery);

    public bool PublishForWorkKind(string userId, string offerId, DateTime expiresAtUtc, string? workKind)
        => PublishChecked(userId, offerId, expiresAtUtc, workKind, CancellationToken.None, static () => true);

    public bool PublishChecked(string userId, string offerId, DateTime expiresAtUtc, string? workKind,
        CancellationToken cancellationToken, Func<bool> isCurrent)
    {
#if ANDROID
        lock (_publishGate)
        try
        {
            if (!FDriverNotificationPublicationGate.CanContinuePublication(cancellationToken, isCurrent)
                || SettingsChangePending(userId)) return false;
            var now = DateTime.UtcNow;
            var settings = Read<SettingsSnapshot>(SettingsKey);
            if (expiresAtUtc <= now || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(offerId)
                || settings?.OwnerId != userId || settings.Settings.DriverId != userId
                || settings.CheckedAtUtc > now.AddMinutes(5) || settings.CheckedAtUtc < now.AddHours(-24)) return false;
            var plan = NotificationAlertPolicy.Evaluate(new(FDriverNotificationPublicationGate.AllowsAlert(settings.Settings, workKind),
                settings.Settings.소리사용, settings.Settings.진동사용, settings.Settings.야간알림제한), new(now));
            if (!plan.Enabled) return false;
            var published = Read<PublishedSnapshot>(PublishedKey);
            var alerts = published?.OwnerId == userId ? published.Alerts : [];
            if (FDriverNotificationPublicationGate.IsDuplicate(alerts, offerId, now)) return true;
            var context = global::Android.App.Application.Context;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Tiramisu
                && context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications)
                    != global::Android.Content.PM.Permission.Granted) return false;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            if (manager is null || !manager.AreNotificationsEnabled()) return false;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                var legacy = manager.GetNotificationChannel(ChannelId);
                var modes = (manager.NotificationChannels ?? []).Where(x => x.Id?.StartsWith(ChannelPrefix, StringComparison.Ordinal) == true).ToArray();
                plan = NotificationAlertPolicy.PreserveLegacy(plan, ToState(legacy), modes.Select(x => ToState(x)!));
                if (!plan.Enabled) return false;
                using var channel = new global::Android.App.NotificationChannel(NotificationAlertPolicy.ChannelId(ChannelPrefix, plan),
                    "살뜰 배달 요청", global::Android.App.NotificationImportance.High);
                using var attributes = new global::Android.Media.AudioAttributes.Builder()
                    .SetUsage(global::Android.Media.AudioUsageKind.Notification)!.SetContentType(global::Android.Media.AudioContentType.Sonification)!.Build();
                channel.SetSound(plan.Sound ? global::Android.Net.Uri.Parse($"android.resource://{context.PackageName}/raw/ssalddel_delivery") : null, attributes);
                channel.EnableVibration(plan.Vibrate);
                channel.LockscreenVisibility = global::Android.App.NotificationVisibility.Private;
                manager.CreateNotificationChannel(channel);
            }
            var remaining = expiresAtUtc - now;
            if (remaining <= TimeSpan.Zero) return false;
            var intent = new global::Android.Content.Intent(context, typeof(global::FDriverApp.MainActivity));
            intent.SetAction(OpenAction);
            intent.PutExtra(UserExtra, userId); intent.PutExtra(OfferExtra, offerId);
            intent.AddFlags(global::Android.Content.ActivityFlags.SingleTop | global::Android.Content.ActivityFlags.ClearTop);
            var pending = global::Android.App.PendingIntent.GetActivity(context, NotificationId, intent,
                global::Android.App.PendingIntentFlags.Immutable | global::Android.App.PendingIntentFlags.UpdateCurrent);
            var builder = global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O
                ? new global::Android.App.Notification.Builder(context, NotificationAlertPolicy.ChannelId(ChannelPrefix, plan))
                : new global::Android.App.Notification.Builder(context);
            var presentation = DriverRecommendationAlertPresentation.Resolve(workKind)!;
            var iconResource = workKind == DriverWorkKinds.FoodDelivery
                ? global::FDriverApp.Resource.Drawable.ic_driver_food : global::FDriverApp.Resource.Drawable.ic_driver_cargo;
            // Keep the bitmap alive through Build/Notify; Dispose releases the peer, never Recycle its shared pixels.
            using var largeIcon = CreateWorkLargeIcon(context, iconResource, presentation.AccentColor);
            builder.SetSmallIcon(iconResource)
                .SetColor(global::Android.Graphics.Color.ParseColor(presentation.AccentColor).ToArgb())
                .SetLargeIcon(largeIcon)
                .SetContentTitle(presentation.Title)
                .SetContentText(presentation.Body)
                .SetContentIntent(pending).SetAutoCancel(true)
                .SetVisibility(global::Android.App.NotificationVisibility.Private);
            if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.O)
            {
                builder.SetSound(plan.Sound ? global::Android.Net.Uri.Parse($"android.resource://{context.PackageName}/raw/ssalddel_delivery") : null);
                if (plan.Vibrate) builder.SetVibrate([0L, 180L]);
            }
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                builder.SetTimeoutAfter(Math.Max(1, (long)remaining.TotalMilliseconds));
            if (!FDriverNotificationPublicationGate.CanContinuePublication(cancellationToken, isCurrent) || SettingsChangePending(userId)) return false;
            manager.Notify(NotificationId, builder.Build());
            Preferences.Default.Set(PublishedKey, JsonSerializer.Serialize(new PublishedSnapshot(userId,
                FDriverNotificationPublicationGate.Record(alerts, offerId, expiresAtUtc, now), workKind), JsonOptions));
            return true;
        }
        catch (global::Java.Lang.SecurityException)
        {
            // 권한 확인과 알림 게시 사이에 사용자가 권한을 철회할 수 있다.
            return false;
        }
#else
        return false;
#endif
    }

    private bool SettingsChangePending(string ownerId)
        => Volatile.Read(ref _pendingSettingsOwner) == ownerId
            || FDriverNotificationPublicationGate.HasPendingSettingsChange(Read<FDriverNotificationSettingsFence>(SettingsFenceKey), ownerId);

#if ANDROID
    private static global::Android.Graphics.Bitmap CreateWorkLargeIcon(global::Android.Content.Context context,
        int iconResource, string accentColor)
    {
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        var size = Math.Clamp((int)Math.Ceiling(64f * density), 96, 192);
        var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(size, size, global::Android.Graphics.Bitmap.Config.Argb8888!)!;
        try
        {
            using var canvas = new global::Android.Graphics.Canvas(bitmap);
            using var paint = new global::Android.Graphics.Paint(global::Android.Graphics.PaintFlags.AntiAlias)
            { Color = global::Android.Graphics.Color.ParseColor(accentColor) };
            canvas.DrawCircle(size / 2f, size / 2f, size / 2f - 1f, paint);
            using var glyph = context.GetDrawable(iconResource)!;
            var inset = (int)Math.Round(size * .2);
            glyph.SetBounds(inset, inset, size - inset, size - inset);
            glyph.Draw(canvas);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static NotificationChannelState? ToState(global::Android.App.NotificationChannel? channel)
        => channel is null ? null : new(channel.Id!, channel.Importance == global::Android.App.NotificationImportance.None,
            (int)channel.Importance >= (int)global::Android.App.NotificationImportance.Default && channel.Sound is not null,
            (int)channel.Importance >= (int)global::Android.App.NotificationImportance.Default && channel.ShouldVibrate());
#endif

    public void Cancel()
    {
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            manager?.Cancel(NotificationId);
        }
        catch (global::Java.Lang.SecurityException) { }
#endif
    }

    private static T? Read<T>(string key)
    {
        try
        {
            var json = Preferences.Default.Get(key, string.Empty);
            return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            Preferences.Default.Remove(key);
            return default;
        }
    }
}
