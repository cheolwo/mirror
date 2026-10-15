using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Drivers;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.Services.Notifications;

namespace 살뜰.Services.Dispatch.Notification;

public sealed partial class 배차추천알림Service
{
    private async Task<int> 음식알림복구발송Async(int take, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        // Reconcile persisted recommendations, including a crash between recommendation and Outbox saves.
        var missing = await _db.운송원장.AsNoTracking().Where(q => q.배차업무유형 == 상태값.배차업무유형.음식배달
            && q.상태 == 상태값.배차대기상태.대기 && q.배차큐단계 == 상태값.배차큐단계.배차추천
            && q.배차노출상태 == 상태값.배차노출상태.추천중 && q.현재추천대상기사Id != null
            && q.현재추천대상기사Id != "" && q.추천만료시각 > now
            && !_db.배차추천알림Outbox.Any(x => x.배차대기Id == q.Id && x.기사Id == q.현재추천대상기사Id
                && x.추천라운드 == q.추천라운드)).OrderBy(q => q.추천시작시각).ThenBy(q => q.Id)
            .Take(take).ToListAsync(ct);
        foreach (var q in missing)
            await 추천알림요청생성Async(q.Id, q.의뢰Id, q.현재추천대상기사Id!, q.추천라운드, ct);

        var leaseCutoff = now.AddSeconds(-FoodRecommendationNotificationRecoveryPolicy.LeaseSeconds);
        var delay1 = now.AddSeconds(-5); var delay2 = now.AddSeconds(-10);
        var delay3 = now.AddSeconds(-20); var delay4 = now.AddSeconds(-30);
        var items = await _db.배차추천알림Outbox.AsNoTracking().Where(x =>
            _db.운송원장.Any(q => q.Id == x.배차대기Id && q.의뢰Id == x.의뢰Id
                && q.배차업무유형 == 상태값.배차업무유형.음식배달)
            && ((x.발송상태 == "Pending" && x.시도횟수 < FoodRecommendationNotificationRecoveryPolicy.MaxAttempts)
                || (x.발송상태 == "Sending" && (x.마지막시도시각 ?? x.UpdatedAt) <= leaseCutoff)
                || (x.발송상태 == "Failed" && x.시도횟수 < FoodRecommendationNotificationRecoveryPolicy.MaxAttempts
                    && (x.마지막시도시각 == null || (x.시도횟수 <= 1 && x.마지막시도시각 <= delay1)
                        || (x.시도횟수 == 2 && x.마지막시도시각 <= delay2)
                        || (x.시도횟수 == 3 && x.마지막시도시각 <= delay3)
                        || (x.시도횟수 == 4 && x.마지막시도시각 <= delay4)))))
            .OrderBy(x => x.마지막시도시각 ?? x.CreatedAt).ThenBy(x => x.Id).Take(take).ToListAsync(ct);

        var processed = 0;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            now = _clock.GetUtcNow().UtcDateTime;
            if (item.시도횟수 >= FoodRecommendationNotificationRecoveryPolicy.MaxAttempts)
            {
                await ReplaceFoodStateAsync(item, "Failed", item.시도횟수, now, ct);
                await RefreshTrackedFoodOutboxAsync(item.Id, ct);
                continue;
            }
            var attempts = item.시도횟수 + 1;
            if (!await ReplaceFoodStateAsync(item, "Sending", attempts, now, ct)) continue;
            processed++;
            item.발송상태 = "Sending";
            item.시도횟수 = attempts;
            item.마지막시도시각 = now;
            item.UpdatedAt = now;
            var status = "Failed";
            try
            {
                var q = await CurrentFoodRecommendationAsync(item, ct);
                if (q is null) status = "Obsolete";
                else
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(FoodRecommendationNotificationRecoveryPolicy.SendTimeoutSeconds));
                    var token = await _pushTokenStore.GetForAppAsync(item.기사Id, 기사앱식별자.FoodDeliveryDriverApp, timeout.Token)
                        .WaitAsync(timeout.Token);
                    // Re-read after asynchronous token lookup before advertising the offer.
                    q = await CurrentFoodRecommendationAsync(item, timeout.Token);
                    if (q is null) status = "Obsolete";
                    else if (!string.IsNullOrWhiteSpace(token))
                    {
                        var sent = await _fcmPushService.SendAsync(new FcmPushMessage(token, "", "",
                            FoodRecommendationData(q, item.기사Id), HighPriority: true, DataOnly: true), timeout.Token)
                            .WaitAsync(timeout.Token);
                        status = sent ? "Succeeded" : "Failed";
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                // Provider exception text may contain credentials or payloads; only log the durable identifier.
                _logger.LogWarning("Food recommendation notification failed. OutboxId={OutboxId} Attempt={Attempt}", item.Id, attempts);
            }
            if (!await ReplaceFoodStateAsync(item, status, attempts, _clock.GetUtcNow().UtcDateTime, ct))
                _logger.LogInformation("Food notification claim changed before completion. OutboxId={OutboxId}", item.Id);
            await RefreshTrackedFoodOutboxAsync(item.Id, ct);
        }
        return processed;
    }

    private async Task<운송원장?> CurrentFoodRecommendationAsync(배차추천알림Outbox item, CancellationToken ct)
    {
        var q = await _db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == item.배차대기Id && x.의뢰Id == item.의뢰Id, ct);
        return q is not null && FoodRecommendationNotificationRecoveryPolicy.IsCurrentRecommendation(q, item, _clock.GetUtcNow().UtcDateTime) ? q : null;
    }

    private async Task<bool> ReplaceFoodStateAsync(배차추천알림Outbox expected, string state, int attempts, DateTime now, CancellationToken ct)
    {
        var rows = _db.배차추천알림Outbox.Where(x => x.Id == expected.Id && x.발송상태 == expected.발송상태
            && x.시도횟수 == expected.시도횟수 && x.마지막시도시각 == expected.마지막시도시각);
        if (!_db.Database.IsRelational())
            throw new InvalidOperationException("Food notification recovery requires a relational store for atomic claims.");
        return await rows.ExecuteUpdateAsync(s => s.SetProperty(x => x.발송상태, state)
            .SetProperty(x => x.시도횟수, attempts).SetProperty(x => x.UpdatedAt, now)
            .SetProperty(x => x.마지막시도시각, state == "Sending" ? now : expected.마지막시도시각), ct) == 1;
    }

    private async Task RefreshTrackedFoodOutboxAsync(long id, CancellationToken ct)
    {
        var tracked = _db.배차추천알림Outbox.Local.FirstOrDefault(x => x.Id == id);
        if (tracked is not null) await _db.Entry(tracked).ReloadAsync(ct);
    }
}
