using System.Text.Json;
using Microsoft.Maui.Storage;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Driver.Notification;

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
    private const string PublishedKey = "ssalddel.fdriver.alert.published.v1";
    private readonly object _publishGate = new();
    private sealed record SettingsSnapshot(string OwnerId, 기사알림설정응답 Settings, DateTime CheckedAtUtc);
    private sealed record PublishedSnapshot(string OwnerId, IReadOnlyList<FDriverPublishedAlert> Alerts);
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
    {
        if (string.IsNullOrWhiteSpace(ownerId) || settings.DriverId != ownerId) return;
        lock (_publishGate)
        {
            var prior = Read<SettingsSnapshot>(SettingsKey);
            if (prior?.OwnerId == ownerId && prior.CheckedAtUtc > checkedAtUtc) return;
            Preferences.Default.Set(SettingsKey, JsonSerializer.Serialize(new SettingsSnapshot(ownerId, settings, checkedAtUtc), JsonOptions));
            if (!settings.배차추천알림사용) Cancel();
        }
    }

    public bool Publish(string userId, string offerId, DateTime expiresAtUtc)
    {
#if ANDROID
        lock (_publishGate)
        try
        {
            var now = DateTime.UtcNow;
            var settings = Read<SettingsSnapshot>(SettingsKey);
            if (expiresAtUtc <= now || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(offerId)
                || settings?.OwnerId != userId || settings.Settings.DriverId != userId
                || settings.CheckedAtUtc > now.AddMinutes(5) || settings.CheckedAtUtc < now.AddHours(-24)) return false;
            var plan = NotificationAlertPolicy.Evaluate(new(settings.Settings.배차추천알림사용,
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
            builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentTitle("살뜰 배달 · 새 배달 요청")
                .SetContentText("앱에서 최신 요청과 배달료를 확인하세요.")
                .SetContentIntent(pending).SetAutoCancel(true)
                .SetVisibility(global::Android.App.NotificationVisibility.Private);
            if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.O)
            {
                builder.SetSound(plan.Sound ? global::Android.Net.Uri.Parse($"android.resource://{context.PackageName}/raw/ssalddel_delivery") : null);
                if (plan.Vibrate) builder.SetVibrate([0L, 180L]);
            }
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                builder.SetTimeoutAfter(Math.Max(1, (long)remaining.TotalMilliseconds));
            manager.Notify(NotificationId, builder.Build());
            Preferences.Default.Set(PublishedKey, JsonSerializer.Serialize(new PublishedSnapshot(userId,
                FDriverNotificationPublicationGate.Record(alerts, offerId, expiresAtUtc, now)), JsonOptions));
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

#if ANDROID
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
