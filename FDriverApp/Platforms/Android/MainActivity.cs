using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FDriverApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int NotificationPermissionRequest = 3108;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RequestNotificationPermissionIfNeeded();
            ReceiveNotificationTarget(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            ReceiveNotificationTarget(intent);
        }

        private void ReceiveNotificationTarget(Intent? intent)
        {
            if (intent?.Action != FDriverFoodNotificationService.OpenAction) return;
            var userId = intent.GetStringExtra(FDriverFoodNotificationService.UserExtra);
            var offerId = intent.GetStringExtra(FDriverFoodNotificationService.OfferExtra);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(offerId)) return;
            var services = IPlatformApplication.Current?.Services;
            if (services is null) return;
            services.GetRequiredService<IFDriverFoodNotificationService>().SetOpenTarget(new(userId, offerId));
            // 외부 식별자는 힌트로만 받고 MainPage가 로그인 계정과 서버 정본을 다시 확인한다.
            MainThread.BeginInvokeOnMainThread(async () =>
                await services.GetRequiredService<MainPageModel>().ResumeFoodNotificationWorkspaceAsync());
        }

        private void RequestNotificationPermissionIfNeeded()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
                && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
                RequestPermissions([Android.Manifest.Permission.PostNotifications], NotificationPermissionRequest);
        }
    }
}
