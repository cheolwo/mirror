namespace FDriverApp.Services;

// Separate from the canonical unread receipt: a push alert does not acknowledge a recommendation.
public sealed record FDriverPublishedAlert(string OfferId, DateTime ExpiresAtUtc);
public sealed class FDriverNotificationPublicationGate
{
    public static bool IsDuplicate(IEnumerable<FDriverPublishedAlert> alerts, string offerId, DateTime now)
        => alerts.Any(x => x.OfferId == offerId && x.ExpiresAtUtc > now);

    public static IReadOnlyList<FDriverPublishedAlert> Record(IEnumerable<FDriverPublishedAlert> alerts,
        string offerId, DateTime expiresAtUtc, DateTime now)
        => alerts.Where(x => x.ExpiresAtUtc > now && x.OfferId != offerId)
            .Append(new(offerId, expiresAtUtc)).TakeLast(100).ToArray();
}
