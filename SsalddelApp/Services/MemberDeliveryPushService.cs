using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Common.Community;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SsalddelApp.Services;

public sealed partial class MemberDeliveryPushService : IMemberDeliveryPushPreferencesClient
{
    public const string OpenAction = "com.companyname.ssalddelapp.OPEN_MEMBER_DELIVERY";
    public const string ChannelPrefix = "member_delivery_alerts.v1";
    private readonly IAuthSession _session;
    private readonly MauiMemberPushTokenProvider _tokens;
    private readonly SsalddelMobilePushInstallationClient _installations;
    private readonly MemberDeliveryPushCoordinator _coordinator;
    private readonly IMemberDeliveryPushStateStore _store;
    private readonly IServiceProvider _services;
    private readonly HttpClient _http;
    private string? _priorOwner;
    public event Action? Changed;

    public MemberDeliveryPushService(IAuthSession session, IMemberDeliveryPushStateStore store,
        MauiMemberPushTokenProvider tokens, SsalddelMobilePushInstallationClient installations,
        IServiceProvider services, HttpClient http)
    {
        _session = session; _tokens = tokens; _installations = installations;
        _store = store; _services = services; _http = http;
        _priorOwner = session.UserId;
        _coordinator = new(store, Session, session.RestoreAsync, Publish);
        _progress = new(store, Session, session.RestoreAsync, PublishProgress);
        _session.Changed += AccountChanged;
    }

    public MemberDeliveryPushStatus GetStatus() => new(_tokens.IsConfigured, NotificationsAllowed,
        !_tokens.IsConfigured ? "휴대폰 알림 설정 전 · 앱의 배송 제안 목록에서 확인하세요."
            : !NotificationsAllowed ? "휴대폰 알림이 꺼져 있습니다. 알림 권한과 기기 설정을 확인하세요."
            : "휴대폰 알림 사용 가능 · 연결 결과와 배송 제안은 앱에서 확인하세요.");

    public async Task RequestPermissionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if ANDROID
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Platform.CurrentActivity is MainActivity activity) activity.RequestMemberNotificationPermission();
        });
#endif
        Changed?.Invoke();
    }

    public async Task ApplyPreferencesAsync(string ownerId, NotificationAlertPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var current = Session();
        await _coordinator.ApplyPreferencesAsync(ownerId, preferences, cancellationToken);
        if (!preferences.Enabled && current.IsAuthenticated && current.UserId == ownerId && Session() == current) Cancel();
    }

    public Task<SsalddelMobilePushRegistrationResult> EnsureRegisteredAsync(CancellationToken cancellationToken = default)
        => _installations.EnsureRegisteredAsync(cancellationToken);

    public Task<NotificationAlertPreferences?> ReadPreferencesAsync(string ownerId, CancellationToken cancellationToken = default)
        => _coordinator.ReadPreferencesAsync(ownerId, cancellationToken);

    public Task<bool> ReceiveAsync(IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
        => data.TryGetValue("type", out var type) && type == WorkflowProgressNotification.MessageType
            ? _progress.ReceiveAsync(data, cancellationToken) : _coordinator.ReceiveAsync(data, cancellationToken);

    public async Task QueueOpenTargetAsync(IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
    {
        await _session.RestoreAsync(cancellationToken);
        if (data.TryGetValue("type", out var type) && type == WorkflowProgressNotification.MessageType)
        {
            if (await _progress.QueueOpenTargetAsync(data, cancellationToken)) Changed?.Invoke();
            return;
        }
        if (await _coordinator.QueueOpenTargetAsync(data, cancellationToken)) Changed?.Invoke();
    }

    public Task<MemberDeliveryPushHint?> TakeOpenTargetAsync(CancellationToken cancellationToken = default)
        => _coordinator.TakeOpenTargetAsync(cancellationToken);

    public Task<bool> IsSignInRequiredAsync(CancellationToken cancellationToken = default)
        => _coordinator.IsSignInRequiredAsync(cancellationToken);

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _session.RestoreAsync(cancellationToken);
        if (_session.UserId is not null)
        {
            var error = await _services.GetRequiredService<AuthApiService>().EnsureAccessTokenAsync(cancellationToken: cancellationToken);
            if (error is null) await RefreshConsentAsync(cancellationToken);
        }
        await EnsureRegisteredAsync(cancellationToken);
        Changed?.Invoke(); // 화면은 서버 알림함을 재조회해 push 누락을 복구합니다.
    }

    private async Task RefreshConsentAsync(CancellationToken cancellationToken)
    {
        var current = Session();
        var accessToken = _session.AccessToken;
        if (!current.IsAuthenticated || current.UserId is null || accessToken is null) return;
        var prior = await _store.LoadAsync(cancellationToken);
        if (Session() != current) return;
        using var request = new HttpRequestMessage(HttpMethod.Get, NeighborhoodMemberDeliveryRoutes.Api.TrimStart('/') + "/participation");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode || Session() != current) return;
        var settings = await response.Content.ReadFromJsonAsync<NeighborhoodMemberParticipationResponse>(cancellationToken);
        if (settings is null || Session() != current) return;
        await _store.UpdateAsync(state =>
        {
            // 앱 복귀 조회 뒤 화면에서 저장한 새 알림 선택은 늦은 응답으로 덮지 않습니다.
            if (Session() != current || state.PreferencesCheckedAtUtc != prior.PreferencesCheckedAtUtc) return state;
            var preferences = state.PreferencesOwnerId == current.UserId ? state.Preferences : new(true, true, true, true);
            return state with { PreferencesOwnerId = current.UserId,
                Preferences = preferences with { Enabled = settings.Enabled && settings.ReceiveOffers },
                PreferencesCheckedAtUtc = DateTimeOffset.UtcNow };
        }, cancellationToken);
        if ((!settings.Enabled || !settings.ReceiveOffers) && Session() == current) Cancel();
    }

    private MemberDeliveryPushSession Session() => new(_session.UserId, _session.IsLoggedIn, _session.SessionRevision);
    private void AccountChanged()
    {
        var owner = _session.UserId;
        if (_priorOwner is not null && _priorOwner != owner)
        {
            Cancel();
            CancelProgress();
            _ = ClearAccountSafelyAsync(_priorOwner);
        }
        _priorOwner = owner;
        Changed?.Invoke();
    }

    private async Task ClearAccountSafelyAsync(string priorOwner)
    {
        try { await _coordinator.ClearAccountAsync(priorOwner); await _progress.ClearAccountAsync(priorOwner); }
        catch (Exception) { /* 저장 오류를 알림 성공으로 표시하지 않으며 이후 로그인·조회가 복구합니다. */ }
    }

    public static bool NotificationsAllowed
    {
        get
        {
#if ANDROID
            var context = global::Android.App.Application.Context;
            if (!AppNotificationsAllowed) return false;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            return manager?.AreNotificationsEnabled() == true
                && (!OperatingSystem.IsAndroidVersionAtLeast(26)
                    || !(manager.NotificationChannels ?? []).Any(channel => channel.Id?.StartsWith(ChannelPrefix, StringComparison.Ordinal) == true
                    && channel.Importance == global::Android.App.NotificationImportance.None));
#else
            return false;
#endif
        }
    }

    public static bool AppNotificationsAllowed
    {
        get
        {
#if ANDROID
            var context = global::Android.App.Application.Context;
            if (OperatingSystem.IsAndroidVersionAtLeast(33)
                && context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications)
                    != global::Android.Content.PM.Permission.Granted) return false;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            return manager?.AreNotificationsEnabled() == true;
#else
            return false;
#endif
        }
    }

    private static bool Publish(MemberDeliveryPushHint hint, NotificationAlertPlan plan)
    {
#if ANDROID
        if (!NotificationsAllowed) return false;
        var context = global::Android.App.Application.Context;
        var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
        if (manager is null) return false;
        var modes = (!OperatingSystem.IsAndroidVersionAtLeast(26) ? [] : manager.NotificationChannels ?? [])
            .Where(channel => channel.Id?.StartsWith(ChannelPrefix, StringComparison.Ordinal) == true)
            .Select(channel => new NotificationChannelState(channel.Id!, channel.Importance == global::Android.App.NotificationImportance.None,
                channel.Sound is not null, channel.ShouldVibrate()));
        plan = NotificationAlertPolicy.PreserveLegacy(plan, null, modes);
        if (!plan.Enabled) return false;
        var channelId = NotificationAlertPolicy.ChannelId(ChannelPrefix, plan);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            using var channel = new global::Android.App.NotificationChannel(channelId, "살뜰 배송 제안", global::Android.App.NotificationImportance.High);
            using var audio = new global::Android.Media.AudioAttributes.Builder().SetUsage(global::Android.Media.AudioUsageKind.Notification)!
                .SetContentType(global::Android.Media.AudioContentType.Sonification)!.Build();
            channel.SetSound(plan.Sound ? global::Android.Net.Uri.Parse($"android.resource://{context.PackageName}/raw/ssalddel_member_delivery") : null, audio);
            channel.EnableVibration(plan.Vibrate); channel.LockscreenVisibility = global::Android.App.NotificationVisibility.Private;
            manager.CreateNotificationChannel(channel);
        }
        var intent = new global::Android.Content.Intent(context, typeof(MainActivity));
        intent.SetAction(OpenAction);
        intent.PutExtra("type", MemberDeliveryPushCoordinator.MessageType);
        intent.PutExtra("appKey", MemberDeliveryPushCoordinator.AppKey);
        intent.PutExtra("notificationId", hint.NotificationId); intent.PutExtra("offerId", hint.OfferId);
        intent.PutExtra("requestId", hint.RequestId); intent.PutExtra("userId", hint.RecipientUserId);
        intent.PutExtra("expiresAtUtc", hint.ExpiresAtUtc.ToString("O"));
        intent.AddFlags(global::Android.Content.ActivityFlags.SingleTop | global::Android.Content.ActivityFlags.ClearTop);
        // 제안별 PendingIntent identity를 분리합니다. 이전 알림이 새 제안을 여는 것을 방지합니다.
        intent.SetData(global::Android.Net.Uri.Parse($"ssalddel-member://notification/{Uri.EscapeDataString(hint.NotificationId)}"));
        var pending = global::Android.App.PendingIntent.GetActivity(context, 42091, intent,
            global::Android.App.PendingIntentFlags.Immutable | global::Android.App.PendingIntentFlags.UpdateCurrent);
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new global::Android.App.Notification.Builder(context, channelId) : new global::Android.App.Notification.Builder(context);
        builder.SetSmallIcon(Resource.Drawable.ic_member_delivery_notification)
            .SetContentTitle("살뜰 · 새 배송 제안").SetContentText("앱에서 물품·거리·배송비를 확인하세요.")
            .SetContentIntent(pending).SetAutoCancel(true).SetVisibility(global::Android.App.NotificationVisibility.Private);
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            builder.SetSound(plan.Sound ? global::Android.Net.Uri.Parse($"android.resource://{context.PackageName}/raw/ssalddel_member_delivery") : null);
            if (plan.Vibrate) builder.SetVibrate([0L, 180L]);
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            builder.SetTimeoutAfter(Math.Max(1L, (long)(hint.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalMilliseconds));
        try { manager.Notify(hint.NotificationId, 42091, builder.Build()); return true; }
        catch (global::Java.Lang.SecurityException) { return false; }
#else
        return false;
#endif
    }

    private static void Cancel()
    {
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            var manager = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
            foreach (var notification in manager?.GetActiveNotifications() ?? [])
                if (notification.Id == 42091) manager?.Cancel(notification.Tag, notification.Id);
        }
        catch (global::Java.Lang.SecurityException) { }
#endif
    }
}
