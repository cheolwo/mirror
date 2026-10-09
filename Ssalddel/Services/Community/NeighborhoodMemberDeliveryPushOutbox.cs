using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Domain.Community;
using 살뜰.Data;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Community;

/// <summary>제안 저장과 분리된 at-least-once 발송. 앱은 OfferId로 중복·계정·만료를 다시 검사한다.</summary>
public sealed class NeighborhoodMemberDeliveryPushOutbox(SsalddelContext db, NeighborhoodMemberDeliveryUseCase service,
    IFcmPushService push, TimeProvider clock, ISsalddelExecutionModePolicy execution)
{
    public async Task<int> SendPendingAsync(CancellationToken ct = default)
    {
        if (!service.Enabled || !execution.IsOperational) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.Set<NeighborhoodMemberOffer>().AsNoTracking().Where(x => x.StateCode == "Pending"
            && x.ExpiresAtUtc > now && x.PushStateCode == "Pending" && x.PushAttempts < 5
            && (x.PushClaimUntilUtc == null || x.PushClaimUntilUtc < now)
            && (x.PushLastAttemptUtc == null || x.PushLastAttemptUtc < now.AddSeconds(-15)))
            .OrderBy(x => x.CreatedAtUtc).Take(50).Select(x => x.OfferId).ToListAsync(ct);
        var sent = 0;
        foreach (var id in ids)
        {
            now = clock.GetUtcNow().UtcDateTime;
            var offer = await db.Set<NeighborhoodMemberOffer>().AsNoTracking().SingleAsync(x => x.OfferId == id, ct);
            var claim = await db.Set<NeighborhoodMemberOffer>().Where(x => x.OfferId == id && x.PushClaimRevision == offer.PushClaimRevision
                && x.StateCode == "Pending" && x.ExpiresAtUtc > now && x.PushStateCode == "Pending"
                && (x.PushClaimUntilUtc == null || x.PushClaimUntilUtc < now))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PushClaimRevision, x => x.PushClaimRevision + 1)
                    .SetProperty(x => x.PushClaimUntilUtc, now.AddSeconds(30)).SetProperty(x => x.PushLastAttemptUtc, now)
                    .SetProperty(x => x.PushAttempts, x => x.PushAttempts + 1), ct);
            if (claim != 1) continue;
            var participation = await db.Set<NeighborhoodMemberParticipation>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == offer.MemberUserId, ct);
            var source = await db.Set<NeighborhoodMemberDelivery>().AsNoTracking().SingleAsync(x => x.RequestId == offer.RequestId, ct);
            if (participation?.ReceiveOffers != true || participation.Revision != offer.ParticipationRevision
                || source.StateCode != "Pending" || source.Revision != offer.DeliveryRevision)
            {
                await db.Set<NeighborhoodMemberOffer>().Where(x => x.OfferId == id && x.PushClaimRevision == offer.PushClaimRevision + 1)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PushStateCode, "Suppressed").SetProperty(x => x.PushClaimUntilUtc, (DateTime?)null), ct);
                continue;
            }
            var tokens = await db.SsalddelMobilePushInstallations.AsNoTracking().Where(x => x.IsActive && x.UserId == offer.MemberUserId
                && x.AppKey == "ssalddel.shipper").Select(x => x.PushToken).Distinct().ToListAsync(ct);
            var success = false;
            foreach (var token in tokens)
            {
                var stillValid = await db.Set<NeighborhoodMemberOffer>().AsNoTracking().AnyAsync(x => x.OfferId == id
                    && x.StateCode == "Pending" && x.ExpiresAtUtc > clock.GetUtcNow().UtcDateTime, ct);
                var stillOpen = await db.화주운송의뢰.AsNoTracking().AnyAsync(x => x.의뢰Id == offer.RequestId
                    && x.상태 == 살뜰.도메인.공통.상태값.의뢰상태.생성됨, ct);
                var optedIn = await db.Set<NeighborhoodMemberParticipation>().AsNoTracking().AnyAsync(x => x.UserId == offer.MemberUserId
                    && x.ReceiveOffers && x.Revision == offer.ParticipationRevision, ct);
                var unassigned = await db.Set<NeighborhoodMemberDelivery>().AsNoTracking().AnyAsync(x => x.RequestId == offer.RequestId
                    && x.StateCode == "Pending" && x.Revision == offer.DeliveryRevision, ct);
                if (!service.Enabled || !stillValid || !stillOpen || !optedIn || !unassigned) break;
                var data = new Dictionary<string,string> { ["type"] = "NeighborhoodMemberDeliveryOffer", ["appKey"] = "ssalddel.shipper",
                    ["notificationId"] = "member:" + offer.OfferId, ["offerId"] = offer.OfferId, ["requestId"] = offer.RequestId,
                    ["userId"] = offer.MemberUserId, ["expiresAtUtc"] = DateTime.SpecifyKind(offer.ExpiresAtUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) };
                try { success |= await push.SendAsync(new(token, "살뜰 · 새 배송 제안", "앱에서 최신 요청을 확인하세요.", data, HighPriority:true, DataOnly:true), ct); }
                catch (Exception ex) when (ex is HttpRequestException or TimeoutException) { }
            }
            await db.Set<NeighborhoodMemberOffer>().Where(x => x.OfferId == id && x.PushClaimRevision == offer.PushClaimRevision + 1)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PushStateCode, success ? "Sent" : "Pending")
                    .SetProperty(x => x.PushClaimUntilUtc, (DateTime?)null), ct);
            if (success) sent++;
        }
        return sent;
    }
}

public sealed class NeighborhoodMemberDeliveryWorker(IServiceScopeFactory scopes, IOptions<NeighborhoodMemberDeliveryOptions> options,
    ILogger<NeighborhoodMemberDeliveryWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.Value.Enabled)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
                    var service = scope.ServiceProvider.GetRequiredService<NeighborhoodMemberDeliveryUseCase>();
                    if (service.Enabled)
                    {
                        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                        var ids = await db.Set<NeighborhoodMemberDelivery>().AsNoTracking().Where(x => x.StateCode == "Pending" && x.WindowEndUtc > now)
                            .Take(50).Select(x => x.RequestId).ToListAsync(stoppingToken);
                        foreach (var id in ids) await service.FanoutAsync(id, stoppingToken);
                        var mirrors = await db.운송원장.AsNoTracking().Where(x => x.일반회원배송전용
                            && (x.커뮤니티원장동기화시각Utc == null || x.커뮤니티원장동기화시각Utc < x.UpdatedAt))
                            .Take(50).Select(x => x.의뢰Id).ToListAsync(stoppingToken);
                        foreach (var id in mirrors) await service.SyncLedgerAsync(id, "member-delivery-worker", stoppingToken);
                        await scope.ServiceProvider.GetRequiredService<NeighborhoodMemberDeliveryPushOutbox>().SendPendingAsync(stoppingToken);
                        var cases = scope.ServiceProvider.GetService<Ssalddel.Services.PrivacySupport.I보호지원Store>();
                        if (cases is not null) await service.SweepPhotosAsync(cases, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { log.LogWarning("일반 회원 배송 작업을 재시도합니다. 오류 종류: {ErrorType}", ex.GetType().Name); }
            }
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
