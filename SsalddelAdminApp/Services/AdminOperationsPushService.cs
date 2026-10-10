using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Common.Notifications;

namespace SsalddelAdminApp.Services;
public sealed class AdminOperationsPushService(AdminAuthSession session, IServiceScopeFactory scopes)
{
    public const string OpenAction = "mirror.operations.OPEN";
    private string? pending;
    private string? pendingOwner;
    public event Action? OpenRequested;
    public string? ConsumeHref()
    {
        var href = pendingOwner is null ? pending : session.IsServerAdmin && pendingOwner == session.UserId ? pending : null;
        pending = null; pendingOwner = null;
        return href is not null && !session.IsServerAdmin ? "/login?returnUrl=" + Uri.EscapeDataString(href) : href;
    }
    public void QueueEmailLink(string? uri)
    {
        if (!Uri.TryCreate(uri,UriKind.Absolute,out var link)||link.Scheme!="mirror-operations"||link.Host!="alerts"||link.Query.Length>0) return;
        var id=link.AbsolutePath.Trim('/'); if(id.Length>0&&!Hex(id))return;
        pending="/operations-alerts"+(id.Length==0?"":"/"+id);pendingOwner=null;OpenRequested?.Invoke();
    }
    public async Task DeactivateAsync()
    {
        try
        {
            using var scope=scopes.CreateScope(); using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var id=Preferences.Default.Get("mirror.admin.push.installation","");
            if(session.IsServerAdmin&&id.Length>0) await scope.ServiceProvider.GetRequiredService<AdminAuthenticatedApiClient>()
                .DeleteAsync("api/v1/mobile/push/installations/"+Uri.EscapeDataString(id),timeout.Token);
        }
        catch { }
        finally { ClearLocal(); }
    }
    public Task CacheTokenAsync(string token) => SecureStorage.Default.SetAsync("mirror.admin.push.token", token);
    public async Task RegisterAsync()
    {
        await session.RestoreAsync(); if (!session.IsServerAdmin) return;
        var owner = session.UserId;
        var token = await SecureStorage.Default.GetAsync("mirror.admin.push.token"); if (string.IsNullOrWhiteSpace(token)) return;
        var id = Preferences.Default.Get("mirror.admin.push.installation", "");
        if (id.Length == 0) { id = Guid.NewGuid().ToString("N"); Preferences.Default.Set("mirror.admin.push.installation", id); }
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AdminAuthenticatedApiClient>().PutAsync<SsalddelMobilePushInstallationUpsertRequest, SsalddelMobilePushInstallationResponse>(
                "api/v1/mobile/push/installations", new(id, "ssalddel.admin", "Android", token, AppInfo.Current.VersionString));
            if (session.UserId != owner) ClearLocal();
        }
        catch { /* Resume retries; no credentials or raw errors logged. */ }
    }
    public async Task ReceiveAsync(IReadOnlyDictionary<string, string> data, bool open = false)
    {
        await session.RestoreAsync();
        if (!session.IsServerAdmin || data.GetValueOrDefault("userId") != session.UserId || data.GetValueOrDefault("type") != "operations-alert"
            || data.GetValueOrDefault("appKey") != "ssalddel.admin") return;
        var id = data.GetValueOrDefault("notificationId") ?? ""; var alert = data.GetValueOrDefault("alertId") ?? "";
        if (!Hex(id) || (alert.Length > 0 && !Hex(alert)) || !DateTimeOffset.TryParse(data.GetValueOrDefault("createdAtUtc"), out var at)
            || at > DateTimeOffset.UtcNow.AddMinutes(5) || at < DateTimeOffset.UtcNow.AddDays(-1)) return;
        if (open) { pending = "/operations-alerts" + (alert.Length == 0 ? "" : "/" + alert); pendingOwner = session.UserId; OpenRequested?.Invoke(); return; }
#if ANDROID
        var context = global::Android.App.Application.Context;
        var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
        if (manager is null) return;
        const string channelId = "mirror.operations.v1";
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            using var channel = new global::Android.App.NotificationChannel(channelId, "Mirror 운영 알림", global::Android.App.NotificationImportance.High);
            channel.LockscreenVisibility = global::Android.App.NotificationVisibility.Private; channel.EnableVibration(true); manager.CreateNotificationChannel(channel);
        }
        var intent = new global::Android.Content.Intent(context, typeof(MainActivity)); intent.SetAction(OpenAction);
        intent.SetData(global::Android.Net.Uri.Parse("mirror-operations://notification/" + id));
        intent.AddFlags(global::Android.Content.ActivityFlags.SingleTop | global::Android.Content.ActivityFlags.ClearTop);
        foreach (var key in new[] { "type", "appKey", "notificationId", "alertId", "userId", "createdAtUtc" }) intent.PutExtra(key, data.GetValueOrDefault(key));
        var pi = global::Android.App.PendingIntent.GetActivity(context, 4510, intent, global::Android.App.PendingIntentFlags.Immutable | global::Android.App.PendingIntentFlags.UpdateCurrent);
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26) ? new global::Android.App.Notification.Builder(context, channelId) : new global::Android.App.Notification.Builder(context);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo).SetContentTitle("Mirror · 운영 확인").SetContentText("앱에서 최신 업무 상태를 확인하세요.")
            .SetContentIntent(pi).SetAutoCancel(true).SetVisibility(global::Android.App.NotificationVisibility.Private);
        manager.Notify(id, 4510, builder.Build());
#endif
    }
    public void ClearLocal()
    {
        pending = null; pendingOwner = null;
        ClearLocalNotifications();
    }
    public void ClearLocalNotifications()
    {
#if ANDROID
        var c = global::Android.App.Application.Context;
        ((global::Android.App.NotificationManager?)c.GetSystemService(global::Android.Content.Context.NotificationService))?.CancelAll();
#endif
    }
    private static bool Hex(string id) => id.Length == 64 && id.All(char.IsAsciiHexDigit);
}
