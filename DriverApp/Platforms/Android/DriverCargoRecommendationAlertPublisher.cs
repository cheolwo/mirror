using System.Globalization;
using DriverApp.Services;
using Microsoft.Maui.Storage;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Notifications;

#pragma warning disable CA1416, CS0618

namespace DriverApp.Platforms.Android;

/// <summary>이미 확인한 알림만 Android에 표시합니다. 네트워크·추천 조회·수락을 수행하지 않습니다.</summary>
public sealed class DriverCargoRecommendationAlertPublisher : IDriverCargoRecommendationAlertPublisher
{
    private const int NotificationId = 31082;
    private const string ChannelPrefix = "cargo_dispatch_alerts.v2";
    private const string PreviousChannelPrefix = "cargo_dispatch_alerts.v1";
    private const string LegacyChannelId = "fcm_fallback_notification_channel";
    private const string PublishedKey = "ssalddel.driver.cargoAlerts.published.v1";
    private readonly object _gate = new();
    private readonly DriverCargoAlertPublicationHistory _history = new(new PreferencesPublicationStorage());

    // A restarted process has not yet verified who owns any previously displayed OS alert.
    public DriverCargoRecommendationAlertPublisher() => Cancel();

    public bool Publish(DriverCargoRecommendationAlert alert, Func<bool> canPublish, CancellationToken cancellationToken)
    {
        lock (_gate)
        try
        {
            if (cancellationToken.IsCancellationRequested || !canPublish()) return false;
            var now = DateTimeOffset.UtcNow;
            var presentation = DriverRecommendationAlertPresentation.Resolve(alert.WorkKind);
            if (presentation is null || !alert.Plan.Enabled || alert.ExpiresAtUtc <= now) return false;
            var context = global::Android.App.Application.Context;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Tiramisu
                && context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications)
                    != global::Android.Content.PM.Permission.Granted) return false;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            if (manager is null || !global::AndroidX.Core.App.NotificationManagerCompat.From(context).AreNotificationsEnabled()) return false;
            var plan = alert.Plan;
            var sound = global::Android.Net.Uri.Parse($"android.resource://{context.PackageName}/raw/hongdal_dispatch");
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                var modes = (manager.NotificationChannels ?? []).Where(x =>
                    x.Id?.StartsWith(PreviousChannelPrefix + ".", StringComparison.Ordinal) == true
                    || x.Id?.StartsWith(ChannelPrefix + ".", StringComparison.Ordinal) == true);
                var legacy = manager.GetNotificationChannel(LegacyChannelId);
                plan = NotificationAlertPolicy.PreserveLegacy(plan, legacy is null ? null : ToState(legacy), modes.Select(ToState));
                if (!plan.Enabled) return false;
                using var channel = new global::Android.App.NotificationChannel(NotificationAlertPolicy.ChannelId(ChannelPrefix, plan),
                    presentation.Label + " 추천", global::Android.App.NotificationImportance.High);
                using var attributes = new global::Android.Media.AudioAttributes.Builder()
                    .SetUsage(global::Android.Media.AudioUsageKind.Notification)!.SetContentType(global::Android.Media.AudioContentType.Sonification)!.Build();
                channel.SetSound(plan.Sound ? sound : null, attributes);
                channel.SetVibrationPattern(plan.Vibrate ? [0L, 180L] : [0L]);
                channel.EnableVibration(plan.Vibrate);
                channel.LockscreenVisibility = global::Android.App.NotificationVisibility.Private;
                manager.CreateNotificationChannel(channel);
            }
            var intent = new global::Android.Content.Intent(context, typeof(global::DriverApp.MainActivity));
            intent.SetAction("kr.ssalddel.driver.OPEN_RECOMMENDATION");
            intent.PutExtra("type", "DriverDispatchRecommendation");
            intent.PutExtra("appKey", 기사앱식별자.CargoYongdalDriverApp);
            intent.PutExtra("driverId", alert.OwnerId); intent.PutExtra("requestId", alert.RequestId);
            intent.PutExtra("recommendationRound", alert.RecommendationRound.ToString(CultureInfo.InvariantCulture));
            intent.PutExtra("expiresAtUtc", alert.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
            intent.AddFlags(global::Android.Content.ActivityFlags.SingleTop | global::Android.Content.ActivityFlags.ClearTop);
            var pending = global::Android.App.PendingIntent.GetActivity(context, NotificationId, intent,
                global::Android.App.PendingIntentFlags.Immutable | global::Android.App.PendingIntentFlags.UpdateCurrent);
            var builder = global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O
                ? new global::Android.App.Notification.Builder(context, NotificationAlertPolicy.ChannelId(ChannelPrefix, plan))
                : new global::Android.App.Notification.Builder(context);
            // Keep the bitmap alive through Build/Notify; Dispose releases the peer, never Recycle its shared pixels.
            using var largeIcon = CreateWorkLargeIcon(context, global::DriverApp.Resource.Drawable.ic_driver_cargo, presentation.AccentColor);
            builder.SetSmallIcon(global::DriverApp.Resource.Drawable.ic_driver_cargo)
                .SetColor(global::Android.Graphics.Color.ParseColor(presentation.AccentColor).ToArgb())
                .SetLargeIcon(largeIcon)
                .SetContentTitle(presentation.Title).SetContentText(presentation.Body)
                .SetContentIntent(pending).SetAutoCancel(true)
                .SetVisibility(global::Android.App.NotificationVisibility.Private);
            if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.O)
            {
                builder.SetSound(plan.Sound ? sound : null);
                if (plan.Vibrate) builder.SetVibrate([0L, 180L]);
            }
            var remaining = alert.ExpiresAtUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || cancellationToken.IsCancellationRequested || !canPublish()) return false;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                builder.SetTimeoutAfter(Math.Max(1, (long)remaining.TotalMilliseconds));
            // The Firebase callback may already have timed out while a transport ignored its cancellation token.
            if (cancellationToken.IsCancellationRequested || !canPublish()) return false;
            var reservation = _history.TryReserve(alert.OwnerId, alert.RequestId, alert.RecommendationRound,
                alert.ExpiresAtUtc, DateTimeOffset.UtcNow);
            if (reservation == DriverCargoAlertReservationResult.Unavailable) return false;
            if (reservation == DriverCargoAlertReservationResult.Duplicate) return true;
            if (alert.ExpiresAtUtc <= DateTimeOffset.UtcNow || cancellationToken.IsCancellationRequested || !canPublish()) return false;
            // The reservation remains consumed even if Notify fails or the process stops after this point.
            // Do not set OnlyAlertOnce on this shared ID: it would silence the next distinct recommendation.
            manager.Notify(NotificationId, builder.Build());
            return true;
        }
        catch (Exception) { return false; }
    }

    public void Cancel()
    {
        lock (_gate)
        try
        {
            var context = global::Android.App.Application.Context;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            manager?.Cancel(NotificationId);
        }
        catch (global::Java.Lang.SecurityException) { }
    }

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

    private static NotificationChannelState ToState(global::Android.App.NotificationChannel channel)
        => new(channel.Id!, channel.Importance == global::Android.App.NotificationImportance.None,
            (int)channel.Importance >= (int)global::Android.App.NotificationImportance.Default && channel.Sound is not null,
            (int)channel.Importance >= (int)global::Android.App.NotificationImportance.Default && channel.ShouldVibrate());

    private sealed class PreferencesPublicationStorage : IDriverCargoAlertPublicationStorage
    {
        public string? Read() => Preferences.Default.Get(PublishedKey, string.Empty);
        public void Write(string value) => Preferences.Default.Set(PublishedKey, value);
    }
}
