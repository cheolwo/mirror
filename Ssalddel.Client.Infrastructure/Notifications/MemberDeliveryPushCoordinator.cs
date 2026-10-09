using System.Globalization;
using Ssalddel.Contracts.Common.Notifications;

namespace Ssalddel.Client.Infrastructure.Notifications;

public sealed class MemberDeliveryPushCoordinator(
    IMemberDeliveryPushStateStore store,
    Func<MemberDeliveryPushSession> session,
    Func<CancellationToken, Task> restoreSession,
    Func<MemberDeliveryPushHint, NotificationAlertPlan, bool> publish,
    TimeProvider? timeProvider = null)
{
    public const string MessageType = "NeighborhoodMemberDeliveryOffer";
    public const string AppKey = "ssalddel.shipper";
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    public static MemberDeliveryPushHint? Parse(IReadOnlyDictionary<string, string> data, DateTimeOffset now)
    {
        if (!data.TryGetValue("type", out var type) || type != MessageType
            || !data.TryGetValue("appKey", out var appKey) || appKey != AppKey
            || !Identifier(data, "notificationId", out var notification)
            || !Identifier(data, "offerId", out var offer)
            || !Identifier(data, "requestId", out var request)
            || !Identifier(data, "userId", out var owner)
            || !data.TryGetValue("expiresAtUtc", out var rawExpiry)
            || !DateTimeOffset.TryParseExact(rawExpiry, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var expiry)
            || expiry <= now || expiry > now.AddMinutes(5)) return null;
        return new(notification, offer, request, owner, expiry.ToUniversalTime());
    }

    public async Task<bool> ReceiveAsync(IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        var hint = Parse(data, _clock.GetUtcNow());
        if (hint is null) return false;
        var before = session();
        await restoreSession(cancellationToken);
        var current = session();
        if (!current.IsAuthenticated || current.UserId != hint.RecipientUserId
            || before.UserId is not null && before != current) return false;
        var posted = false;
        await store.UpdateAsync(state =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = _clock.GetUtcNow();
            if (session() != current || hint.ExpiresAtUtc <= now || state.RevocationPending
                || state.RegisteredOwnerId != hint.RecipientUserId
                || state.PreferencesOwnerId != hint.RecipientUserId
                || state.PreferencesCheckedAtUtc is not { } checkedAt
                || checkedAt < now.AddHours(-24) || checkedAt > now.AddMinutes(5)) return state;
            var plan = NotificationAlertPolicy.Evaluate(state.Preferences, now);
            if (!plan.Enabled) return state;
            var recent = state.Published.Where(x => x.ExpiresAtUtc > now).TakeLast(63).ToArray();
            if (recent.Any(x => x.NotificationId == hint.NotificationId)) { posted = true; return state; }
            if (!publish(hint, plan)) return state;
            posted = true;
            return state with { Published = recent.Append(new(hint.NotificationId, hint.ExpiresAtUtc)).ToArray() };
        }, cancellationToken);
        return posted;
    }

    public async Task<bool> QueueOpenTargetAsync(IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        var hint = Parse(data, _clock.GetUtcNow());
        if (hint is null) return false;
        var accepted = false;
        await store.UpdateAsync(state =>
        {
            var current = session();
            if (state.RevocationPending || state.RegisteredOwnerId != hint.RecipientUserId
                || current.IsAuthenticated && current.UserId != hint.RecipientUserId) return state;
            accepted = true;
            return state with { OpenTarget = hint };
        }, cancellationToken);
        return accepted;
    }

    public async Task<MemberDeliveryPushHint?> TakeOpenTargetAsync(CancellationToken cancellationToken = default)
    {
        MemberDeliveryPushHint? target = null;
        await store.UpdateAsync(state =>
        {
            if (state.OpenTarget is not { } hint) return state;
            var current = session();
            if (state.RevocationPending || hint.ExpiresAtUtc <= _clock.GetUtcNow()
                || state.RegisteredOwnerId != hint.RecipientUserId
                || current.IsAuthenticated && current.UserId != hint.RecipientUserId)
                return state with { OpenTarget = null };
            if (!current.IsAuthenticated) return state; // 로그인 화면에서 로그인 후 같은 계정만 소비합니다.
            target = hint;
            return state with { OpenTarget = null };
        }, cancellationToken);
        return target;
    }

    public async Task<bool> IsSignInRequiredAsync(CancellationToken cancellationToken = default)
    {
        var current = session();
        if (current.IsAuthenticated) return false;
        var state = await store.LoadAsync(cancellationToken);
        return session() == current && !state.RevocationPending && state.OpenTarget is { } hint
            && hint.ExpiresAtUtc > _clock.GetUtcNow() && state.RegisteredOwnerId == hint.RecipientUserId;
    }

    public Task ApplyPreferencesAsync(string ownerId, NotificationAlertPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var current = session();
        if (!current.IsAuthenticated || current.UserId != ownerId) return Task.CompletedTask;
        return store.UpdateAsync(state => session() != current ? state : state with
        {
            PreferencesOwnerId = ownerId, Preferences = preferences,
            PreferencesCheckedAtUtc = _clock.GetUtcNow(),
            Published = state.PreferencesOwnerId == ownerId ? state.Published : [],
            OpenTarget = state.OpenTarget?.RecipientUserId == ownerId ? state.OpenTarget : null
        }, cancellationToken);
    }

    public async Task<NotificationAlertPreferences?> ReadPreferencesAsync(string ownerId,
        CancellationToken cancellationToken = default)
    {
        var current = session();
        if (!current.IsAuthenticated || current.UserId != ownerId) return null;
        var state = await store.LoadAsync(cancellationToken);
        return session() == current && state.PreferencesOwnerId == ownerId ? state.Preferences : null;
    }

    public Task ClearAccountAsync(string? priorOwner = null, CancellationToken cancellationToken = default)
        => store.UpdateAsync(state => priorOwner is not null && state.PreferencesOwnerId != priorOwner
            ? state with { OpenTarget = state.OpenTarget?.RecipientUserId == priorOwner ? null : state.OpenTarget }
            : state with { PreferencesOwnerId = null, Preferences = new(Enabled: false),
                PreferencesCheckedAtUtc = null, OpenTarget = null, Published = [] },
            cancellationToken);

    private static bool Identifier(IReadOnlyDictionary<string, string> data, string key, out string value)
    {
        value = string.Empty;
        if (!data.TryGetValue(key, out var candidate) || string.IsNullOrWhiteSpace(candidate)
            || candidate.Length > 200 || candidate != candidate.Trim()
            || candidate.Any(char.IsControl) || candidate.IndexOfAny(['/', '\\', '?', '#', '%']) >= 0) return false;
        value = candidate;
        return true;
    }
}
