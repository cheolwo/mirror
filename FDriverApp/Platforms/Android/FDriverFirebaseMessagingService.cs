#if FDRIVER_FIREBASE
using Android.App;
using Android.Content;
using Firebase.Messaging;
using FDriverApp.Services;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CS0618, CS0672
namespace FDriverApp.Platforms.Android;

[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class FDriverFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        RunWithinCallback((services, cancellationToken) =>
            services.GetRequiredService<FDriverPushRegistrationService>().CacheTokenAsync(token, cancellationToken));
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);
        try
        {
            var services = IPlatformApplication.Current?.Services;
            services?.GetRequiredService<FDriverFoodPushReceiver>()
                .ReceiveWithinCallback(new Dictionary<string, string>(message.Data));
        }
        catch (Exception) { LogHandlingFailure(); }
    }

    internal static Task RegisterTokenAsync(string? token) => RunAsync(async services =>
    {
        if (!string.IsNullOrWhiteSpace(token))
            await services.GetRequiredService<FDriverPushRegistrationService>().UpdateTokenAsync(token);
    });

    private static void RunWithinCallback(Func<IServiceProvider, CancellationToken, Task> action)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is not null)
                Task.Run(() => action(services, cancellation.Token), cancellation.Token)
                    .WaitAsync(cancellation.Token).GetAwaiter().GetResult();
        }
        catch (Exception) { LogHandlingFailure(); }
    }

    private static async Task RunAsync(Func<IServiceProvider, Task> action)
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is not null) await action(services);
        }
        catch (Exception)
        {
            // 토큰·계정·알림 데이터·예외 전문은 Android 로그에 남기지 않습니다.
            LogHandlingFailure();
        }
    }

    private static void LogHandlingFailure()
        => global::Android.Util.Log.Warn("SsalddelFoodPush", "Food push handling could not complete.");
}
#pragma warning restore CS0618, CS0672
#endif
