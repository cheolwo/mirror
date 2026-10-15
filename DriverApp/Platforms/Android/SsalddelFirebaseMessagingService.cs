using Android.App;
using Android.Content;
using Android.Util;
using DriverApp.Services;
using Firebase.Messaging;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CS0618, CS0672

namespace DriverApp.Platforms.Android;

[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class SsalddelFirebaseMessagingService : FirebaseMessagingService
{
    private const string Tag = "SsalddelFcm";

    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        _ = RegisterTokenAsync(token);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);
        // Persist the minimal pointer before returning Firebase's bounded callback.
        // Failed/slow reads are retried on foreground resume, never converted into acceptance.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            var data = new Dictionary<string, string>(message.Data);
            Task.Run(async () =>
            {
                var recovery = DriverAppServiceProvider.Services.GetRequiredService<DriverRecommendationPushRecovery>();
                var captured = await recovery.CaptureAsync(data, openRequested: false, cancellation.Token);
                if (captured.Status == DriverRecommendationPushRecoveryStatus.Captured)
                    await recovery.RecoverAsync(cancellation.Token);
            }, cancellation.Token).WaitAsync(cancellation.Token).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Tokens, addresses, payloads and exception messages never enter Android logs.
            Log.Warn(Tag, "Recommendation hint could not be confirmed; retry on app resume.");
        }
    }

    public static Task RegisterTokenAsync(string? token)
        => RunWithServicesAsync(async services =>
        {
            var tokenService = services.GetRequiredService<I기사푸시토큰등록Service>();
            await tokenService.수신토큰저장및등록Async(token);
        });

    private static async Task RunWithServicesAsync(Func<IServiceProvider, Task> action)
    {
        try { await action(DriverAppServiceProvider.Services); }
        catch (Exception) { Log.Warn(Tag, "Push registration could not complete."); }
    }
}

#pragma warning restore CS0618, CS0672
