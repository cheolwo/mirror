using System.Text.Json;
using Microsoft.Maui.Storage;

namespace FDriverApp.Services;

public sealed class FDriverFoodNotificationService : IFDriverFoodNotificationService
{
    public const string OpenAction = "kr.ssalddel.fdriver.OPEN_RECOMMENDATION";
    public const string UserExtra = "food_notification_user";
    public const string OfferExtra = "food_notification_offer";
    private const string ReceiptKey = "ssalddel.fdriver.foodNotification.receipt.v1";
    private const string TargetKey = "ssalddel.fdriver.foodNotification.target.v1";
    private const int NotificationId = 31081;
    private const string ChannelId = "food_dispatch_recommendations";
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
        Cancel(); ClearOpenTarget(); Preferences.Default.Remove(ReceiptKey);
    }

    public bool Publish(string userId, string offerId, DateTime expiresAtUtc)
    {
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Tiramisu
                && context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications)
                    != global::Android.Content.PM.Permission.Granted) return false;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            if (manager is null || !manager.AreNotificationsEnabled()) return false;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                manager.CreateNotificationChannel(new global::Android.App.NotificationChannel(
                    ChannelId, "새 배달 요청", global::Android.App.NotificationImportance.High));
            }
            var remaining = expiresAtUtc - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) return false;
            var intent = new global::Android.Content.Intent(context, typeof(global::FDriverApp.MainActivity));
            intent.SetAction(OpenAction);
            intent.PutExtra(UserExtra, userId); intent.PutExtra(OfferExtra, offerId);
            intent.AddFlags(global::Android.Content.ActivityFlags.SingleTop | global::Android.Content.ActivityFlags.ClearTop);
            var pending = global::Android.App.PendingIntent.GetActivity(context, NotificationId, intent,
                global::Android.App.PendingIntentFlags.Immutable | global::Android.App.PendingIntentFlags.UpdateCurrent);
            var builder = global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O
                ? new global::Android.App.Notification.Builder(context, ChannelId)
                : new global::Android.App.Notification.Builder(context);
            builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentTitle("새 배달 요청")
                .SetContentText("앱에서 최신 요청과 배달료를 확인하세요.")
                .SetContentIntent(pending).SetAutoCancel(true)
                .SetVisibility(global::Android.App.NotificationVisibility.Private);
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                builder.SetTimeoutAfter(Math.Max(1, (long)remaining.TotalMilliseconds));
            manager.Notify(NotificationId, builder.Build());
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
