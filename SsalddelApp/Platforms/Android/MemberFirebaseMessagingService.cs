#if SSALDDEL_MEMBER_FIREBASE
using Android.App;
using Android.Content;
using Firebase.Messaging;
using Microsoft.Extensions.DependencyInjection;
using SsalddelApp.Services;

#pragma warning disable CS0618, CS0672
namespace SsalddelApp.Platforms.Android;

[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class MemberFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        CompleteLocalCallback((services, cancellationToken) =>
            services.GetRequiredService<MauiMemberPushTokenProvider>().CacheTokenAsync(token, cancellationToken));
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);
        // Data-only 전경·배경 모두 같은 계정·만료·동의·소리 정책으로 직접 게시합니다.
        var data = new Dictionary<string, string>(message.Data);
        CompleteLocalCallback(async (services, cancellationToken) =>
        {
            await services.GetRequiredService<MemberDeliveryPushService>().ReceiveAsync(data, cancellationToken);
        });
    }

    private static void CompleteLocalCallback(Func<IServiceProvider, CancellationToken, Task> action)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is not null)
                Task.Run(() => action(services, cancellation.Token), cancellation.Token)
                    .WaitAsync(cancellation.Token).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // 토큰·계정·알림 데이터와 예외 전문은 로그에 넣지 않습니다.
            global::Android.Util.Log.Warn("SsalddelMemberPush", "Member push callback could not complete.");
        }
    }
}
#pragma warning restore CS0618, CS0672
#endif
