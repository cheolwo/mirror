using Ssalddel.Contracts.Common.Drivers;

namespace DriverApp.Services;

public enum DriverRecommendationPushRecoveryStatus
{ Ignored, Captured, LoginRequired, Ready, AlreadyAccepted, ExpiredOrUnavailable, RetryableFailure, SessionChanged }

public sealed record DriverRecommendationPushRecoveryResult(DriverRecommendationPushRecoveryStatus Status,
    string? Route = null, string? OwnerId = null, long? SessionRevision = null, bool OpenRequested = false,
    DriverRecommendationPushTarget? Target = null);

/// <summary>알림의 식별자를 현재 기사 정본과 대조합니다. 수락 명령을 호출하지 않습니다.</summary>
public sealed class DriverRecommendationPushRecovery(IAuthSession auth, IDriverSampleDataService samples,
    IDriverHomeMapService map, IDriverRecommendationNotificationService notifications,
    IDriverRecommendationPushTargetStore store, TimeProvider? timeProvider = null,
    IDriverCargoRecommendationAlertService? cargoAlerts = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private (string Owner, long Revision, string RequestId, int Round, DateTime? ExpiresAt)? _lastPublished;

    public async Task<DriverRecommendationPushRecoveryResult> CaptureAsync(IReadOnlyDictionary<string, string> data,
        bool openRequested, CancellationToken cancellationToken = default)
    {
        var hint = DriverRecommendationPushHintParser.Parse(data, _time.GetUtcNow());
        if (hint is null) return new(DriverRecommendationPushRecoveryStatus.Ignored);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await auth.RestoreAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ClearForeignPublication();
            if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.UserId))
                return new(DriverRecommendationPushRecoveryStatus.LoginRequired,
                    DriverRoutes.LoginFor(DriverRoutes.Recommendations), OpenRequested: openRequested);
            if (!HasDriverRole() || (hint.OwnerId is not null && hint.OwnerId != auth.UserId))
                return new(DriverRecommendationPushRecoveryStatus.Ignored);
            // Owner 없는 구형 메시지도 현재 계정에 묶되 이후 서버의 본인 추천 조회가 필수입니다.
            var prior = await store.LoadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var now = _time.GetUtcNow();
            var priorLive = prior?.OwnerId == auth.UserId && prior.ReceivedAtUtc >= now.AddHours(-1)
                && (prior.Hint.ExpiresAtUtc is null || prior.Hint.ExpiresAtUtc > now);
            // A later background hint must not replace the exact notification the user opened.
            if (priorLive && prior!.OpenRequested && !openRequested)
                return new(DriverRecommendationPushRecoveryStatus.Captured);
            var target = priorLive && prior!.Hint == hint
                ? prior with { OpenRequested = prior.OpenRequested || openRequested }
                : new DriverRecommendationPushTarget(hint, auth.UserId, now, openRequested);
            await store.SaveAsync(target, cancellationToken);
            return new(DriverRecommendationPushRecoveryStatus.Captured);
        }
        finally { _gate.Release(); }
    }

    public async Task<DriverRecommendationPushRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await auth.RestoreAsync(cancellationToken);
            ClearForeignPublication();
            var target = await store.LoadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (target is null) return new(DriverRecommendationPushRecoveryStatus.Ignored);
            var owner = auth.UserId;
            var revision = auth.SessionRevision;
            if (!auth.IsAuthenticated || !HasDriverRole() || owner != target.OwnerId
                || (target.Hint.OwnerId is not null && target.Hint.OwnerId != owner))
            {
                await store.ClearAsync(cancellationToken);
                cargoAlerts?.Cancel();
                _lastPublished = null;
                return new(DriverRecommendationPushRecoveryStatus.SessionChanged);
            }
            var now = _time.GetUtcNow();
            if (target.ReceivedAtUtc > now.AddMinutes(5) || target.ReceivedAtUtc < now.AddHours(-1)
                || target.Hint.ExpiresAtUtc <= now)
                return await DiscardAsync(target, owner!, revision, cancellationToken);

            try
            {
                // Never trust the payload's fee/place or a cached recommendation after a failed read.
                await samples.RefreshAsync(cancellationToken, force: true);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrent(owner!, revision))
                {
                    await store.ClearAsync(cancellationToken);
                    return new(DriverRecommendationPushRecoveryStatus.SessionChanged);
                }
                var request = samples.추천의뢰조회(target.Hint.RequestId);
                var accepted = samples.ReadState.CurrentTransport.IsCurrent
                    ? samples.운송목록.FirstOrDefault(x => x.의뢰Id == target.Hint.RequestId) : null;
                if (accepted is not null)
                {
                    cargoAlerts?.Cancel();
                    notifications.MarkHandled(target.Hint.RequestId);
                    if (!target.OpenRequested) await store.ClearAsync(cancellationToken);
                    return new(DriverRecommendationPushRecoveryStatus.AlreadyAccepted,
                        DriverRoutes.CurrentTransport + "?acceptedRequestId=" + Uri.EscapeDataString(target.Hint.RequestId),
                        owner, revision, target.OpenRequested, target);
                }
                if (!samples.ReadState.Recommendations.IsCurrent
                    || (request is null && !samples.ReadState.CurrentTransport.IsCurrent))
                    return new(DriverRecommendationPushRecoveryStatus.RetryableFailure, DriverRoutes.Recommendations,
                        owner, revision, target.OpenRequested);
                now = _time.GetUtcNow();
                if (request is null || request.의뢰Id != target.Hint.RequestId || !request.서버지정추천
                    || request.추천만료시각!.Value.ToUniversalTime() <= now.UtcDateTime
                    || (target.Hint.RecommendationRound is { } round && request.추천라운드 != round)
                    || target.Hint.ExpiresAtUtc <= now)
                    return await DiscardAsync(target, owner!, revision, cancellationToken);

                if (!target.OpenRequested && cargoAlerts is not null)
                    await cargoAlerts.TryPublishAsync(owner!, revision, request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrent(owner!, revision))
                {
                    await store.ClearAsync(cancellationToken);
                    cargoAlerts?.Cancel();
                    return new(DriverRecommendationPushRecoveryStatus.SessionChanged);
                }
                var publication = (owner!, revision, request.의뢰Id, request.추천라운드, request.추천만료시각);
                if (_lastPublished != publication && map.BuildMarkers([request]).FirstOrDefault() is { } marker)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsCurrent(owner!, revision)) return new(DriverRecommendationPushRecoveryStatus.SessionChanged);
                    notifications.Publish(new DriverIncomingRecommendation(marker, request, now.LocalDateTime,
                        "FCM", target.Hint.PendingCount));
                    _lastPublished = publication;
                }
                if (!target.OpenRequested) await store.ClearAsync(cancellationToken);
                return new(DriverRecommendationPushRecoveryStatus.Ready,
                    DriverRoutes.RecommendationDetail(Uri.EscapeDataString(request.의뢰Id)), owner, revision, target.OpenRequested, target);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch
            {
                if (!IsCurrent(owner!, revision))
                {
                    await store.ClearAsync(cancellationToken);
                    return new(DriverRecommendationPushRecoveryStatus.SessionChanged);
                }
                // Keep the exact pointer for a later foreground retry; never submit/accept on its behalf.
                return new(DriverRecommendationPushRecoveryStatus.RetryableFailure, DriverRoutes.Recommendations,
                    owner, revision, target.OpenRequested);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task AcknowledgeOpenedAsync(DriverRecommendationPushRecoveryResult result,
        CancellationToken cancellationToken = default)
    {
        if (result.Status is not (DriverRecommendationPushRecoveryStatus.Ready or DriverRecommendationPushRecoveryStatus.AlreadyAccepted)
            || result.Target is null || result.OwnerId is null || result.SessionRevision is not { } revision) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsCurrent(result.OwnerId, revision) && await store.LoadAsync(cancellationToken) == result.Target)
                await store.ClearAsync(cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<DriverRecommendationPushRecoveryResult> DiscardAsync(DriverRecommendationPushTarget target,
        string owner, long revision, CancellationToken cancellationToken)
    {
        cargoAlerts?.Cancel();
        notifications.MarkDismissed(target.Hint.RequestId);
        await store.ClearAsync(cancellationToken);
        return new(DriverRecommendationPushRecoveryStatus.ExpiredOrUnavailable, DriverRoutes.Recommendations,
            owner, revision, target.OpenRequested);
    }

    private bool IsCurrent(string owner, long revision) => auth.IsAuthenticated
        && auth.UserId == owner && auth.SessionRevision == revision && HasDriverRole();
    private bool HasDriverRole() => auth.Roles.Any(x => x == "기사" || string.Equals(x, "Driver", StringComparison.OrdinalIgnoreCase));

    private void ClearForeignPublication()
    {
        if (_lastPublished is not { } published || (auth.IsAuthenticated && published.Owner == auth.UserId
            && published.Revision == auth.SessionRevision)) return;
        cargoAlerts?.Cancel();
        if (notifications.GetCurrent() is { } current && current.Request.의뢰Id == published.RequestId
            && current.Request.추천라운드 == published.Round)
            notifications.MarkDismissed(published.RequestId);
        _lastPublished = null;
    }
}
