using Ssalddel.Client.Infrastructure.Notifications;

namespace SsalddelApp.Services;

public sealed class MauiMemberPushTokenProvider(IMemberDeliveryPushStateStore store) : ISsalddelMobilePushTokenProvider
{
#if ANDROID && SSALDDEL_MEMBER_FIREBASE
    public bool IsConfigured => true;
#else
    public bool IsConfigured => false;
#endif

    public async Task<SsalddelMobilePushTokenSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || !MemberDeliveryPushService.AppNotificationsAllowed) return null;
        var state = await store.LoadAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(state.PushToken) ? null : new(state.DeviceInstallationId,
            MemberDeliveryPushCoordinator.AppKey, "android", state.PushToken, AppInfo.Current.VersionString,
            DeviceInfo.Current.Model);
    }

    // Firebase callback는 짧게 토큰만 저장합니다. 서버 등록은 앱 복귀·로그인에서 재시도합니다.
    public Task CacheTokenAsync(string token, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(token) || token.Length > 2000 ? Task.CompletedTask
            : store.UpdateAsync(state => state with { PushToken = token.Trim() }, cancellationToken);
}
