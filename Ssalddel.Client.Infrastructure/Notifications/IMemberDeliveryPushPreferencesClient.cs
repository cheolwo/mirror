using Ssalddel.Contracts.Common.Notifications;

namespace Ssalddel.Client.Infrastructure.Notifications;

public sealed record MemberDeliveryPushStatus(bool IsConfigured, bool PermissionGranted, string Message);

public interface IMemberDeliveryPushPreferencesClient
{
    MemberDeliveryPushStatus GetStatus();
    Task RequestPermissionAsync(CancellationToken cancellationToken = default);
    Task ApplyPreferencesAsync(string ownerId, NotificationAlertPreferences preferences,
        CancellationToken cancellationToken = default);
    Task<NotificationAlertPreferences?> ReadPreferencesAsync(string ownerId, CancellationToken cancellationToken = default);
    Task<SsalddelMobilePushRegistrationResult> EnsureRegisteredAsync(CancellationToken cancellationToken = default);
}
