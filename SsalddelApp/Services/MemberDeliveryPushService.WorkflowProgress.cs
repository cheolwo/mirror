using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Contracts.Common.Notifications;

namespace SsalddelApp.Services;

public sealed partial class MemberDeliveryPushService
{
    public const string ProgressOpenAction = "com.companyname.ssalddelapp.OPEN_WORKFLOW_PROGRESS";
    private const string ProgressChannelPrefix = "workflow_progress_alerts.v1";
    private readonly WorkflowProgressPushCoordinator _progress;

    public Task<WorkflowProgressHint?> TakeProgressOpenTargetAsync(CancellationToken cancellationToken = default)
        => _progress.TakeOpenTargetAsync(cancellationToken);
    public Task<string?> ProgressSignInTargetAsync(CancellationToken cancellationToken = default)
        => _progress.SignInTargetAsync(cancellationToken);

    private static bool PublishProgress(WorkflowProgressHint hint, NotificationAlertPlan plan)
    {
#if ANDROID
        if (!AppNotificationsAllowed) return false;
        var context = global::Android.App.Application.Context;
        var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
        if (manager is null) return false;
        var modes = new List<NotificationChannelState>();
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            foreach (var channel in manager.NotificationChannels ?? [])
                if (channel.Id?.StartsWith(ProgressChannelPrefix, StringComparison.Ordinal) == true)
                    modes.Add(new(channel.Id!, channel.Importance == global::Android.App.NotificationImportance.None,
                        channel.Sound is not null, channel.ShouldVibrate()));
        }
        plan = NotificationAlertPolicy.PreserveLegacy(plan, null, modes);
        if (!plan.Enabled) return false;
        var channelId = NotificationAlertPolicy.ChannelId(ProgressChannelPrefix, plan);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            using var channel = new global::Android.App.NotificationChannel(channelId, "업무 진행", global::Android.App.NotificationImportance.Default);
            using var audio = new global::Android.Media.AudioAttributes.Builder().SetUsage(global::Android.Media.AudioUsageKind.Notification)!
                .SetContentType(global::Android.Media.AudioContentType.Sonification)!.Build();
            channel.SetSound(plan.Sound ? global::Android.Media.RingtoneManager.GetDefaultUri(global::Android.Media.RingtoneType.Notification) : null, audio);
            channel.EnableVibration(plan.Vibrate); channel.LockscreenVisibility = global::Android.App.NotificationVisibility.Private;
            manager.CreateNotificationChannel(channel);
        }
        var intent = new global::Android.Content.Intent(context, typeof(MainActivity));
        intent.SetAction(ProgressOpenAction);
        foreach (var pair in new Dictionary<string, string>
        {
            ["type"] = WorkflowProgressNotification.MessageType, ["eventId"] = hint.EventId,
            ["workflowKey"] = hint.WorkflowKey, ["stageCode"] = hint.StageCode,
            ["sourceId"] = hint.SourceId, ["userId"] = hint.RecipientUserId,
            ["occurredAtUtc"] = hint.OccurredAtUtc.ToString("O"), ["deepLink"] = hint.Route
        }) intent.PutExtra(pair.Key, pair.Value);
        intent.AddFlags(global::Android.Content.ActivityFlags.SingleTop | global::Android.Content.ActivityFlags.ClearTop);
        intent.SetData(global::Android.Net.Uri.Parse($"ssalddel-progress://notification/{hint.EventId}"));
        var pending = global::Android.App.PendingIntent.GetActivity(context, 42092, intent,
            global::Android.App.PendingIntentFlags.Immutable | global::Android.App.PendingIntentFlags.UpdateCurrent);
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new global::Android.App.Notification.Builder(context, channelId) : new global::Android.App.Notification.Builder(context);
        builder.SetSmallIcon(Resource.Drawable.ic_member_delivery_notification)
            .SetContentTitle(hint.Stage.Title).SetContentText(hint.Stage.Body)
            .SetContentIntent(pending).SetAutoCancel(true).SetVisibility(global::Android.App.NotificationVisibility.Private);
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            if (plan.Sound) builder.SetSound(global::Android.Media.RingtoneManager.GetDefaultUri(global::Android.Media.RingtoneType.Notification));
            if (plan.Vibrate) builder.SetVibrate([0L, 180L]);
        }
        try { manager.Notify(hint.EventId, 42092, builder.Build()); return true; }
        catch (global::Java.Lang.SecurityException) { return false; }
#else
        return false;
#endif
    }

    private static void CancelProgress()
    {
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            foreach (var notification in manager?.GetActiveNotifications() ?? [])
                if (notification.Id == 42092) manager?.Cancel(notification.Tag, notification.Id);
        }
        catch (global::Java.Lang.SecurityException) { }
#endif
    }
}
