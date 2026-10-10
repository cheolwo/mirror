#if SSALDDEL_ADMIN_FIREBASE
using Android.App;
using Android.Content;
using Firebase.Messaging;
using Microsoft.Extensions.DependencyInjection;
using SsalddelAdminApp.Services;
namespace SsalddelAdminApp.Platforms.Android;
[Service(Exported = false), IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class AdminFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token) { base.OnNewToken(token); Complete(s => s.CacheTokenAsync(token)); }
    public override void OnMessageReceived(RemoteMessage message) { base.OnMessageReceived(message); Complete(s => s.ReceiveAsync(new Dictionary<string, string>(message.Data))); }
    private static void Complete(Func<AdminOperationsPushService, Task> action)
    {
        try { if (IPlatformApplication.Current?.Services is { } services) Task.Run(() => action(services.GetRequiredService<AdminOperationsPushService>())).WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult(); }
        catch { global::Android.Util.Log.Warn("MirrorOperations", "NotificationCallbackFailed"); }
    }
}
#endif
