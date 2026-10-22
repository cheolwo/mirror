using Ssalddel.Contracts.Common.Notifications;

namespace Ssalddel.Client.Infrastructure.Notifications;

/// <summary>기존 설치의 계정 결속을 재사용한다. 배송 제안 동의와 업무 진행 알림은 다른 목적이다.</summary>
public sealed class WorkflowProgressPushCoordinator(IMemberDeliveryPushStateStore store,
    Func<MemberDeliveryPushSession> session, Func<CancellationToken, Task> restoreSession,
    Func<WorkflowProgressHint, NotificationAlertPlan, bool> publish, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    public async Task<bool> ReceiveAsync(IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        if (!WorkflowProgressNotification.TryParse(data, out var hint) || hint is null || !Timely(hint)) return false;
        var before = session();
        await restoreSession(cancellationToken);
        var current = session();
        if (!current.IsAuthenticated || current.UserId != hint.RecipientUserId
            || before.UserId is not null && before != current) return false;
        var accepted = false;
        await store.UpdateAsync(state =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session() != current || !Timely(hint) || state.RevocationPending
                || state.RegisteredOwnerId != current.UserId) return state;
            var recent = state.ProgressPublished.Where(x => x.RecipientUserId == current.UserId
                && x.OccurredAtUtc >= _clock.GetUtcNow().AddDays(-7)).TakeLast(63).ToArray();
            if (recent.Any(x => x.EventId == hint.EventId)) { accepted = true; return state; }
            // 지연 재전송이 더 최근에 수신한 진행 단계 위에 덮이지 않게 한다.
            if (recent.Any(x => x.WorkflowKey == hint.WorkflowKey && x.SourceId == hint.SourceId
                && x.OccurredAtUtc > hint.OccurredAtUtc)) return state;
            // 배송 제안 OFF가 업무 상태 확인까지 막지는 않지만 소리·진동을 새로 켜지는 않는다.
            // 소유자에게 확인된 설정이 없을 때는 조용한 진행 알림만 게시한다.
            var preferences = state.PreferencesOwnerId == current.UserId
                ? state.Preferences with { Enabled = true,
                    Sound = state.Preferences.Enabled && state.Preferences.Sound,
                    Vibrate = state.Preferences.Enabled && state.Preferences.Vibrate }
                : new NotificationAlertPreferences(Enabled: true, Sound: false, Vibrate: false, QuietAtNight: true);
            var plan = NotificationAlertPolicy.Evaluate(preferences, _clock.GetUtcNow());
            if (!publish(hint, plan)) return state;
            accepted = true;
            return state with { ProgressPublished = recent.Append(new WorkflowProgressPublishedHint(
                hint.EventId, hint.RecipientUserId, hint.WorkflowKey, hint.SourceId, hint.OccurredAtUtc)).ToArray() };
        }, cancellationToken);
        return accepted;
    }

    public async Task<bool> QueueOpenTargetAsync(IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        if (!WorkflowProgressNotification.TryParse(data, out var hint) || hint is null || !Timely(hint)) return false;
        var current = session();
        if (current.IsAuthenticated && current.UserId != hint.RecipientUserId) return false;
        var accepted = false;
        await store.UpdateAsync(state =>
        {
            if (session() != current || state.RevocationPending || state.RegisteredOwnerId != hint.RecipientUserId) return state;
            accepted = true;
            return state with { ProgressOpenTarget = hint };
        }, cancellationToken);
        return accepted;
    }

    public async Task<WorkflowProgressHint?> TakeOpenTargetAsync(CancellationToken cancellationToken = default)
    {
        WorkflowProgressHint? result = null;
        var current = session();
        await store.UpdateAsync(state =>
        {
            if (state.ProgressOpenTarget is not { } target) return state;
            if (session() == current && !current.IsAuthenticated && !state.RevocationPending
                && state.RegisteredOwnerId == target.RecipientUserId && Timely(target)) return state;
            if (session() == current && current.IsAuthenticated && !state.RevocationPending
                && state.RegisteredOwnerId == current.UserId && target.RecipientUserId == current.UserId
                && Timely(target) && WorkflowProgressNotification.IsSafeRoute(target.Route)) result = target;
            return state with { ProgressOpenTarget = null };
        }, cancellationToken);
        return result;
    }

    public async Task<string?> SignInTargetAsync(CancellationToken cancellationToken = default)
    {
        var current = session();
        if (current.IsAuthenticated) return null;
        var state = await store.LoadAsync(cancellationToken);
        return session() == current && !state.RevocationPending && state.ProgressOpenTarget is { } target
            && state.RegisteredOwnerId == target.RecipientUserId && Timely(target)
            && WorkflowProgressNotification.IsSafeRoute(target.Route) ? target.Route : null;
    }

    public Task ClearAccountAsync(string priorOwner, CancellationToken cancellationToken = default)
        => store.UpdateAsync(state => state with
        {
            ProgressOpenTarget = state.ProgressOpenTarget?.RecipientUserId == priorOwner ? null : state.ProgressOpenTarget,
            ProgressPublished = state.ProgressPublished.Where(x => x.RecipientUserId != priorOwner).ToArray()
        }, cancellationToken);

    private bool Timely(WorkflowProgressHint hint) => hint.OccurredAtUtc >= _clock.GetUtcNow().AddDays(-7)
        && hint.OccurredAtUtc <= _clock.GetUtcNow().AddMinutes(5);
}
