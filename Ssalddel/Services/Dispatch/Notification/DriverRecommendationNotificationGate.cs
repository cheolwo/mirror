using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.설정;

namespace 살뜰.Services.Dispatch.Notification;

/// <summary>두 푸시 경로가 현재 추천과 명시적인 종류별 알림 허용을 함께 확인합니다. 배차·수신·권한을 변경하지 않습니다.</summary>
internal sealed class DriverRecommendationNotificationGate(SsalddelContext? db,
    IDriverNotificationSettingsStore? settingsStore, ISharedDriverWorkAdmissionService? sharedAdmission, TimeProvider clock,
    I운영배차공통UseCase? availability = null)
{
    public async Task<운송원장?> CurrentAsync(배차추천알림Outbox item, int workKind, CancellationToken ct)
    {
        if (db is null) return null;
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == item.배차대기Id && value.의뢰Id == item.의뢰Id, ct);
        return queue?.배차업무유형 == workKind && IsCurrent(queue, item.기사Id, item.추천라운드, clock.GetUtcNow().UtcDateTime)
            ? queue : null;
    }

    public async Task<운송원장?> CurrentAsync(string driverId, string requestId, CancellationToken ct)
    {
        if (db is null || string.IsNullOrWhiteSpace(driverId) || string.IsNullOrWhiteSpace(requestId)) return null;
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(
            value => value.의뢰Id == requestId && value.현재추천대상기사Id == driverId, ct);
        return queue is not null && IsCurrent(queue, driverId, queue.추천라운드, clock.GetUtcNow().UtcDateTime) ? queue : null;
    }

    public async Task<bool> AllowedAsync(운송원장 queue, string driverId, CancellationToken ct)
    {
        if (settingsStore is null || !IsCurrent(queue, driverId, queue.추천라운드, clock.GetUtcNow().UtcDateTime)) return false;
        // 조회 오류는 상위 발송의 실패로 남기고, 설정을 임의의 허용 기본값으로 대체하지 않습니다.
        var settings = await settingsStore.GetAsync(driverId, ct);
        if (settings is null || !settings.배차추천알림사용 || !(SharedDriverWorkAdmissionService.ResolveWorkKind(queue) switch
            {
                DriverWorkKinds.FoodDelivery => settings.음식배달추천알림사용,
                DriverWorkKinds.QuickDelivery or DriverWorkKinds.CargoTransport => settings.화물운송추천알림사용,
                _ => false
            })) return false;
        if (availability is not null && !await 운영배차신규수신Guard.허용Async(availability, driverId, ct)) return false;
        if (sharedAdmission?.Enabled == true
            && (await sharedAdmission.CheckRecommendationAsync(driverId, queue, ct)).IsFailed) return false;
        return IsCurrent(queue, driverId, queue.추천라운드, clock.GetUtcNow().UtcDateTime);
    }

    private static bool IsCurrent(운송원장 queue, string driverId, int round, DateTime now)
        => queue.배차업무유형 is 상태값.배차업무유형.음식배달 or 상태값.배차업무유형.용달운송
            && !string.IsNullOrWhiteSpace(driverId) && queue.현재추천대상기사Id == driverId
            && queue.추천라운드 == round && queue.상태 == 상태값.배차대기상태.대기
            && queue.배차큐단계 == 상태값.배차큐단계.배차추천
            && queue.배차노출상태 == 상태값.배차노출상태.추천중
            && queue.추천만료시각.HasValue && queue.추천만료시각 > now;
}
