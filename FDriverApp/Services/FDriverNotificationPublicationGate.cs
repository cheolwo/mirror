using Ssalddel.Contracts.Driver.Notification;
using DriverWorkKinds = Ssalddel.Contracts.Driver.Settings.DriverWorkKinds;

namespace FDriverApp.Services;

// Separate from the canonical unread receipt: a push alert does not acknowledge a recommendation.
public sealed record FDriverPublishedAlert(string OfferId, DateTime ExpiresAtUtc);
public sealed record FDriverNotificationSettingsFence(string OwnerId, DateTime BeganAtUtc, DateTime? ConfirmedReadAtUtc = null);
public sealed class FDriverNotificationPublicationGate
{
    public static bool CanApplySettings(FDriverNotificationSettingsFence? fence, string ownerId, DateTime readStartedAtUtc,
        bool explicitConfirmation)
        => fence is null || fence.OwnerId != ownerId || (fence.ConfirmedReadAtUtc is { } confirmed
            ? readStartedAtUtc >= confirmed
            : explicitConfirmation && readStartedAtUtc > fence.BeganAtUtc);

    public static bool HasPendingSettingsChange(FDriverNotificationSettingsFence? fence, string ownerId)
        => fence is { ConfirmedReadAtUtc: null } && fence.OwnerId == ownerId;

    public static bool CanContinuePublication(CancellationToken cancellationToken, Func<bool> isCurrent)
        => !cancellationToken.IsCancellationRequested && isCurrent() && !cancellationToken.IsCancellationRequested;

    public static bool AllowsFoodAlert(기사알림설정응답? settings)
        => AllowsAlert(settings, DriverWorkKinds.FoodDelivery);

    public static bool AllowsAlert(기사알림설정응답? settings, string? workKind)
        => settings?.배차추천알림사용 == true && (workKind switch
        {
            DriverWorkKinds.FoodDelivery => settings.음식배달추천알림사용,
            DriverWorkKinds.QuickDelivery => settings.화물운송추천알림사용,
            _ => false
        });

    public static bool IsDuplicate(IEnumerable<FDriverPublishedAlert> alerts, string offerId, DateTime now)
        => alerts.Any(x => x.OfferId == offerId && x.ExpiresAtUtc > now);

    public static IReadOnlyList<FDriverPublishedAlert> Record(IEnumerable<FDriverPublishedAlert> alerts,
        string offerId, DateTime expiresAtUtc, DateTime now)
        => alerts.Where(x => x.ExpiresAtUtc > now && x.OfferId != offerId)
            .Append(new(offerId, expiresAtUtc)).TakeLast(100).ToArray();
}
