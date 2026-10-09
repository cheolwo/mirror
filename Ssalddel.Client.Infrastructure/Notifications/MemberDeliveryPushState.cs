using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Client.Infrastructure.Notifications;

// 고객 주소·연락처·물품 정보는 설치 또는 알림 복구 저장소에 저장하지 않습니다.
public sealed record MemberDeliveryPushState(string DeviceInstallationId)
{
    public string? PushToken { get; init; }
    public string? RegisteredOwnerId { get; init; }
    public string? RegisteredTokenHash { get; init; }
    public DateTimeOffset? RegisteredAtUtc { get; init; }
    public bool RevocationPending { get; init; }
    public string? PreferencesOwnerId { get; init; }
    public NotificationAlertPreferences Preferences { get; init; } = new(Enabled: false);
    public DateTimeOffset? PreferencesCheckedAtUtc { get; init; }
    public MemberDeliveryPushHint? OpenTarget { get; init; }
    public IReadOnlyList<MemberDeliveryPublishedHint> Published { get; init; } = [];
}

public sealed record MemberDeliveryPublishedHint(string NotificationId, DateTimeOffset ExpiresAtUtc);

public interface IMemberDeliveryPushStateStore
{
    Task<MemberDeliveryPushState> LoadAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(Func<MemberDeliveryPushState, MemberDeliveryPushState> update,
        CancellationToken cancellationToken = default);
}

public sealed record MemberDeliveryPushSession(string? UserId, bool IsAuthenticated, long Revision);

public sealed record MemberDeliveryPushHint(string NotificationId, string OfferId, string RequestId,
    string RecipientUserId, DateTimeOffset ExpiresAtUtc)
{
    // 외부 알림은 route·URL을 지정할 수 없습니다. 서버 상세를 다시 조회할 내부 경로만 생성합니다.
    public string Route => NeighborhoodMemberDeliveryRoutes.Offer(OfferId);
}
