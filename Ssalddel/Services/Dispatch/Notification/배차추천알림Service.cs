using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Drivers;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.Services.Notifications;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Notification
{
    public sealed class 배차추천알림Service : I배차추천알림Service
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private const string 상태_대기 = "Pending";
        private const string 상태_성공 = "Succeeded";
        private const string 상태_실패 = "Failed";

        private readonly SsalddelContext _db;
        private readonly IDriverPushTokenStore _pushTokenStore;
        private readonly IFcmPushService _fcmPushService;
        private readonly ILogger<배차추천알림Service> _logger;

        public 배차추천알림Service(
            SsalddelContext db,
            IDriverPushTokenStore pushTokenStore,
            IFcmPushService fcmPushService,
            ILogger<배차추천알림Service> logger)
        {
            _db = db;
            _pushTokenStore = pushTokenStore;
            _fcmPushService = fcmPushService;
            _logger = logger;
        }

        public async Task 추천알림요청생성Async(long 배차대기Id, string 의뢰Id, string 기사Id, int 추천라운드, CancellationToken cancellationToken = default)
        {
            if (배차대기Id <= 0 || string.IsNullOrWhiteSpace(의뢰Id) || string.IsNullOrWhiteSpace(기사Id))
            {
                return;
            }

            var exists = await _db.배차추천알림Outbox
                .AnyAsync(x => x.배차대기Id == 배차대기Id
                               && x.기사Id == 기사Id
                               && x.추천라운드 == 추천라운드,
                    cancellationToken);
            if (exists)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var queue = await _db.운송원장.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == 배차대기Id && x.의뢰Id == 의뢰Id, cancellationToken);
            var isFood = queue?.배차업무유형 == 상태값.배차업무유형.음식배달;
            var dataJson = JsonSerializer.Serialize(isFood && queue!.추천만료시각.HasValue
                ? FoodRecommendationData(queue, 기사Id)
                : new Dictionary<string, string>
            {
                ["type"] = 기사배차추천알림계약.현재유형,
                ["dispatchWaitingId"] = 배차대기Id.ToString(),
                ["requestId"] = 의뢰Id,
                ["recommendationRound"] = 추천라운드.ToString()
            }, JsonOptions);

            _db.배차추천알림Outbox.Add(new 배차추천알림Outbox
            {
                배차대기Id = 배차대기Id,
                의뢰Id = 의뢰Id,
                기사Id = 기사Id,
                추천라운드 = 추천라운드,
                제목 = "새로운 배차 추천",
                본문 = isFood ? "새 음식 배달 추천을 확인해 주세요." : "근처 운송의뢰가 도착했습니다.",
                DataJson = dataJson,
                발송상태 = 상태_대기,
                CreatedAt = now,
                UpdatedAt = now
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        public Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default)
            => 업무유형별대기알림발송Async(상태값.배차업무유형.용달운송, take, cancellationToken);

        public async Task<int> 업무유형별대기알림발송Async(int 배차업무유형, int take = 100, CancellationToken cancellationToken = default)
        {
            if (배차업무유형 is not (상태값.배차업무유형.용달운송 or 상태값.배차업무유형.음식배달))
                throw new ArgumentOutOfRangeException(nameof(배차업무유형), "음식 배달 또는 용달 운송 유형이 필요합니다.");

            var appKey = 배차업무유형 == 상태값.배차업무유형.용달운송
                ? 기사앱식별자.CargoYongdalDriverApp : 기사앱식별자.FoodDeliveryDriverApp;

            var pendingItems = await _db.배차추천알림Outbox
                .Where(x => x.발송상태 == 상태_대기
                            && _db.운송원장.Any(queue => queue.Id == x.배차대기Id
                                && queue.의뢰Id == x.의뢰Id && queue.배차업무유형 == 배차업무유형))
                .OrderBy(x => x.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);

            if (pendingItems.Count == 0)
            {
                return 0;
            }

            var processed = 0;
            foreach (var item in pendingItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                var now = DateTime.UtcNow;
                item.시도횟수 += 1;
                item.마지막시도시각 = now;
                item.UpdatedAt = now;

                try
                {
                    var token = await _pushTokenStore.GetForAppAsync(item.기사Id, appKey, cancellationToken);
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        item.발송상태 = 상태_실패;
                        _logger.LogWarning("Action={Action} DriverId={DriverId} OutboxId={OutboxId} Result={Result} Reason={Reason}",
                            "DispatchRecommendationPush",
                            item.기사Id,
                            item.Id,
                            "Failed",
                            "No push token");
                        continue;
                    }

                    bool sent;
                    if (배차업무유형 == 상태값.배차업무유형.음식배달)
                    {
                        // A delayed outbox cannot advertise an expired, accepted,
                        // superseded, or reassigned recommendation as current.
                        var queue = await _db.운송원장.AsNoTracking().SingleOrDefaultAsync(
                            x => x.Id == item.배차대기Id && x.의뢰Id == item.의뢰Id
                                && x.배차업무유형 == 상태값.배차업무유형.음식배달
                                && x.상태 == 상태값.배차대기상태.대기
                                && x.배차큐단계 == 상태값.배차큐단계.배차추천
                                && x.배차노출상태 == 상태값.배차노출상태.추천중
                                && x.현재추천대상기사Id == item.기사Id && x.추천라운드 == item.추천라운드
                                && x.추천만료시각.HasValue && x.추천만료시각 > DateTime.UtcNow,
                            cancellationToken);
                        if (queue is null) { item.발송상태 = 상태_실패; continue; }
                        sent = await _fcmPushService.SendAsync(new FcmPushMessage(token, string.Empty, string.Empty,
                            FoodRecommendationData(queue, item.기사Id), HighPriority: true, DataOnly: true), cancellationToken);
                    }
                    else
                    {
                        // Keep the existing cargo event type and external payload.
                        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(item.DataJson, JsonOptions)
                                   ?? new Dictionary<string, string>();
                        sent = await _fcmPushService.SendToTokenAsync(token, item.제목, item.본문, data, cancellationToken);
                    }

                    item.발송상태 = sent ? 상태_성공 : 상태_실패;
                }
                catch (Exception ex)
                {
                    item.발송상태 = 상태_실패;
                    _logger.LogWarning(ex, "배차추천 알림 발송 실패. OutboxId={OutboxId} DriverId={DriverId}", item.Id, item.기사Id);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return processed;
        }

        private static Dictionary<string, string> FoodRecommendationData(운송원장 queue, string driverId)
            => new()
            {
                ["type"] = "FoodDeliveryRecommendation",
                ["appKey"] = 기사앱식별자.FoodDeliveryDriverApp,
                ["userId"] = driverId,
                ["offerId"] = queue.의뢰Id,
                ["expiresAtUtc"] = DateTime.SpecifyKind(queue.추천만료시각!.Value, DateTimeKind.Utc)
                    .ToString("O", CultureInfo.InvariantCulture)
            };
    }
}
