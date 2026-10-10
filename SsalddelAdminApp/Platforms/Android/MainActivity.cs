using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using SsalddelAdminApp.Services;

namespace SsalddelAdminApp;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize
                           | ConfigChanges.Orientation
                           | ConfigChanges.UiMode
                           | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize
                           | ConfigChanges.Density)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "mirror-operations", DataHost = "alerts")]
public sealed class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? state) { base.OnCreate(state); ReceiveTarget(Intent); }
    protected override void OnNewIntent(Intent? intent) { base.OnNewIntent(intent); ReceiveTarget(intent); }
    protected override void OnResume()
    {
        base.OnResume(); _ = ResumeAsync();
#if SSALDDEL_ADMIN_FIREBASE
        try { Firebase.FirebaseApp.InitializeApp(this); Firebase.Messaging.FirebaseMessaging.Instance.GetToken().AddOnCompleteListener(new TokenListener()); } catch { }
#endif
    }
    private static async Task ResumeAsync()
    { try { if (IPlatformApplication.Current?.Services is { } s) await s.GetRequiredService<AdminOperationsPushService>().RegisterAsync(); } catch { } }
    public void RequestOperationsNotificationPermission()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            RequestPermissions([Android.Manifest.Permission.PostNotifications], 4510);
    }
    private static void ReceiveTarget(Intent? intent)
    {
        if (intent?.Action == Intent.ActionView)
        {
            IPlatformApplication.Current?.Services.GetRequiredService<AdminOperationsPushService>().QueueEmailLink(intent.DataString);
            return;
        }
        if (intent?.Action != AdminOperationsPushService.OpenAction) return;
        var data = new Dictionary<string, string>();
        foreach (var key in new[] { "type", "appKey", "notificationId", "alertId", "userId", "createdAtUtc" })
        { var v = intent.GetStringExtra(key); if (v is null) return; data[key] = v; }
        _ = OpenAsync(data);
    }
    private static async Task OpenAsync(IReadOnlyDictionary<string, string> data)
    { try { if (IPlatformApplication.Current?.Services is { } s) await s.GetRequiredService<AdminOperationsPushService>().ReceiveAsync(data, true); } catch { } }
#if SSALDDEL_ADMIN_FIREBASE
    private sealed class TokenListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnCompleteListener
    {
        public void OnComplete(global::Android.Gms.Tasks.Task task) { if (task.IsSuccessful && task.Result?.ToString() is { } token) _ = SaveAsync(token); }
        private static async Task SaveAsync(string token)
        { try { if (IPlatformApplication.Current?.Services is { } s) { var push=s.GetRequiredService<AdminOperationsPushService>(); await push.CacheTokenAsync(token); await push.RegisterAsync(); } } catch { } }
    }
#endif
}
