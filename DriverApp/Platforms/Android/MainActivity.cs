using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Tasks;
using Android.OS;
using DriverApp.Platforms.Android;
using DriverApp.Services;
using Firebase.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

#pragma warning disable CA1416, CS0618

namespace DriverApp;

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
public class MainActivity : MauiAppCompatActivity
{
    private const int PostNotificationsRequestCode = 3107;
    private readonly SemaphoreSlim _recoveryGate = new(1, 1);
    private IAuthSession? _session;
    private bool _resumed;
    private bool _destroyed;
    private DriverRecommendationPushRecoveryResult? _pendingLoginNavigation;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _session = DriverAppServiceProvider.Services.GetRequiredService<IAuthSession>();
        _session.Changed += OnSessionChanged;
        _ = CaptureRecommendationIntentAsync(Intent);
        RequestPostNotificationsPermissionIfNeeded();
        FirebaseMessaging.Instance.GetToken().AddOnCompleteListener(new FcmTokenCompleteListener());
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        _ = CaptureRecommendationIntentAsync(intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _resumed = true;
        _ = RecoverRecommendationAsync();
    }

    protected override void OnPause()
    {
        _resumed = false;
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        _destroyed = true;
        _resumed = false;
        if (_session is not null) _session.Changed -= OnSessionChanged;
        base.OnDestroy();
    }

    private void OnSessionChanged()
    {
        if (_resumed && !_destroyed) _ = RecoverRecommendationAsync();
    }

    private async Task CaptureRecommendationIntentAsync(Intent? intent)
    {
        try
        {
            if (intent?.Extras is not { } extras) return;
            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in DriverRecommendationPushHintParser.DataKeys)
                if (extras.GetString(key) is { } value) data[key] = value;
            var recovery = DriverAppServiceProvider.Services.GetRequiredService<DriverRecommendationPushRecovery>();
            var result = await recovery.CaptureAsync(data, openRequested: true);
            if (result.Status == DriverRecommendationPushRecoveryStatus.Ignored) return;
            foreach (var key in DriverRecommendationPushHintParser.DataKeys) intent.RemoveExtra(key);
            if (result.Status == DriverRecommendationPushRecoveryStatus.LoginRequired)
            {
                _pendingLoginNavigation = result;
                if (await OpenRecoveryRouteAsync(result)) _pendingLoginNavigation = null;
            }
            else if (_resumed && !_destroyed)
                await RecoverRecommendationAsync();
        }
        catch (Exception) { LogRecoveryFailure(); }
    }

    private async Task RecoverRecommendationAsync()
    {
        await _recoveryGate.WaitAsync();
        try
        {
            if (!_resumed || _destroyed) return;
            if (_pendingLoginNavigation is { } login)
            {
                if (await OpenRecoveryRouteAsync(login)) _pendingLoginNavigation = null;
                return;
            }
            var recovery = DriverAppServiceProvider.Services.GetRequiredService<DriverRecommendationPushRecovery>();
            var result = await recovery.RecoverAsync();
            if (await OpenRecoveryRouteAsync(result))
                await recovery.AcknowledgeOpenedAsync(result);
        }
        catch (Exception) { LogRecoveryFailure(); }
        finally { _recoveryGate.Release(); }
    }

    private Task<bool> OpenRecoveryRouteAsync(DriverRecommendationPushRecoveryResult result)
        => Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (!_resumed || _destroyed || !result.OpenRequested || result.Route is null) return false;
            if (result.OwnerId is not null && (_session?.UserId != result.OwnerId
                || _session.SessionRevision != result.SessionRevision || !_session.IsAuthenticated)) return false;
            if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is not NavigationPage navigation
                || navigation.RootPage is not MainPage page) return false;
            await navigation.PopToRootAsync(animated: false);
            if (!_resumed || _destroyed || (result.OwnerId is not null && (_session?.UserId != result.OwnerId
                || _session.SessionRevision != result.SessionRevision || !_session.IsAuthenticated))) return false;
            await page.OpenRouteAsync(result.Route);
            return true;
        });

    private static void LogRecoveryFailure()
        => global::Android.Util.Log.Warn("SsalddelFcm", "Recommendation could not be reopened; retry on app resume.");

    private void RequestPostNotificationsPermissionIfNeeded()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
        {
            return;
        }

        if (CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Permission.Granted)
        {
            return;
        }

        RequestPermissions([Android.Manifest.Permission.PostNotifications], PostNotificationsRequestCode);
    }

    private sealed class FcmTokenCompleteListener : Java.Lang.Object, IOnCompleteListener
    {
        public void OnComplete(global::Android.Gms.Tasks.Task task)
        {
            if (!task.IsSuccessful)
            {
                return;
            }

            var token = task.Result?.ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            _ = SsalddelFirebaseMessagingService.RegisterTokenAsync(token);
        }
    }
}

#pragma warning restore CA1416, CS0618
