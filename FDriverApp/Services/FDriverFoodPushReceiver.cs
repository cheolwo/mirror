using System.Globalization;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Drivers;

namespace FDriverApp.Services;

public sealed class FDriverFoodPushReceiver(IFDriverAuthSession session, IFDriverPushDeviceStore store,
    IFDriverFoodNotificationService notifications)
{
    public const string MessageType = "FoodDeliveryRecommendation";

    // Firebase calls on a background thread. Finish bounded local work before
    // returning its callback; a cancelled late store read cannot publish an alert.
    public bool ReceiveWithinCallback(IReadOnlyDictionary<string, string> data, TimeSpan? budget = null)
    {
        var timeout = budget ?? TimeSpan.FromSeconds(3);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(3))
            throw new ArgumentOutOfRangeException(nameof(budget));
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            return Task.Run(() => ReceiveAsync(data, cancellation.Token), cancellation.Token)
                .WaitAsync(cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
    }

    // FCM는 조회 힌트입니다. 백그라운드 수신은 짧게 끝내고 업무 조회·실행은 앱 복귀가 맡습니다.
    public async Task<bool> ReceiveAsync(IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!data.TryGetValue("type", out var type) || type != MessageType
            || !data.TryGetValue("appKey", out var appKey) || appKey != 기사앱식별자.FoodDeliveryDriverApp
            || !data.TryGetValue("userId", out var owner) || string.IsNullOrWhiteSpace(owner) || owner.Length > 200
            || !data.TryGetValue("offerId", out var offer) || string.IsNullOrWhiteSpace(offer) || offer.Length > 200
            || !data.TryGetValue("expiresAtUtc", out var expiryText)
            || !DateTimeOffset.TryParse(expiryText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry)
            || expiry <= DateTimeOffset.UtcNow || expiry > DateTimeOffset.UtcNow.AddHours(1)) return false;
        var revisionOwner = session.UserId;
        await session.RestoreAsync(cancellationToken);
        var state = await store.LoadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if ((revisionOwner is not null && revisionOwner != session.UserId) || owner != session.UserId
            || session.CurrentState == ClientAuthSessionRestoreState.Anonymous
            || !session.Roles.Any(role => role == "기사" || string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase))
            || state.RegisteredOwnerId != owner || state.RevocationPending) return false;
        return notifications.Publish(owner, offer, expiry.UtcDateTime);
    }
}
