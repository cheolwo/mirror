using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;
using Microsoft.Extensions.DependencyInjection;
using SsalddelApp.Services;

namespace SsalddelApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int NotificationPermissionRequest = 4209;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ReceiveNotificationTarget(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        ReceiveNotificationTarget(intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _ = ResumeSafelyAsync();
#if SSALDDEL_MEMBER_FIREBASE
        try
        {
            Firebase.FirebaseApp.InitializeApp(this);
            Firebase.Messaging.FirebaseMessaging.Instance.GetToken().AddOnCompleteListener(new MemberTokenListener());
        }
        catch (Exception) { LogHandlingFailure(); }
#endif
    }

    public void RequestMemberNotificationPermission()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33)
            && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            RequestPermissions([Android.Manifest.Permission.PostNotifications], NotificationPermissionRequest);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == NotificationPermissionRequest) _ = ResumeSafelyAsync();
    }

    private static async Task ResumeSafelyAsync()
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is not null) await services.GetRequiredService<MemberDeliveryPushService>().ResumeAsync();
        }
        catch (Exception) { LogHandlingFailure(); }
    }

    private static void ReceiveNotificationTarget(Intent? intent)
    {
        if (intent?.Action != MemberDeliveryPushService.OpenAction) return;
        var data = new Dictionary<string, string>();
        foreach (var key in new[] { "type", "appKey", "notificationId", "offerId", "requestId", "userId", "expiresAtUtc" })
        {
            var value = intent.GetStringExtra(key);
            if (value is null) return;
            data[key] = value;
        }
        _ = QueueTargetSafelyAsync(data);
    }

    private static async Task QueueTargetSafelyAsync(IReadOnlyDictionary<string, string> data)
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is not null) await services.GetRequiredService<MemberDeliveryPushService>().QueueOpenTargetAsync(data);
        }
        catch (Exception) { LogHandlingFailure(); }
    }

    private static void LogHandlingFailure()
        => global::Android.Util.Log.Warn("SsalddelMemberPush", "Member notification handling could not complete.");

#if SSALDDEL_MEMBER_FIREBASE
    private sealed class MemberTokenListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnCompleteListener
    {
        public void OnComplete(global::Android.Gms.Tasks.Task task)
        {
            if (task.IsSuccessful) _ = UpdateTokenSafelyAsync(task.Result?.ToString());
        }
    }

    private static async Task UpdateTokenSafelyAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null) return;
            await services.GetRequiredService<MauiMemberPushTokenProvider>().CacheTokenAsync(token);
            await services.GetRequiredService<MemberDeliveryPushService>().EnsureRegisteredAsync();
        }
        catch (Exception) { LogHandlingFailure(); }
    }
#endif
}
