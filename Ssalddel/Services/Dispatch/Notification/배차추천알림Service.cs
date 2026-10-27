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
using Ssalddel.Services.Dispatch.Compliance;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Services.Dispatch.Common;

namespace 살뜰.Services.Dispatch.Notification
{
    public sealed partial class 배차추천알림Service : I배차추천알림Service
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private const string 상태_대기 = "Pending";
        private const string 상태_성공 = "Succeeded";
        private const string 상태_실패 = "Failed";

        private readonly SsalddelContext _db;
        private readonly IDriverPushTokenStore _pushTokenStore;
        private readonly IFcmPushService _fcmPushService;
        private readonly ILogger<배차추천알림Service> _logger;
        private readonly TimeProvider _clock;
        private readonly I신규화물업무자격Guard? _신규업무자격;
        private readonly DriverRecommendationNotificationGate _알림허용;

        public 배차추천알림Service(
            SsalddelContext db,
            IDriverPushTokenStore pushTokenStore,
            IFcmPushService fcmPushService,
            ILogger<배차추천알림Service> logger,
            TimeProvider? clock = null,
            I신규화물업무자격Guard? 신규업무자격 = null,
            IDriverNotificationSettingsStore? notificationSettings = null,
            ISharedDriverWorkAdmissionService? sharedAdmission = null,
            I운영배차공통UseCase? availability = null)
        {
            _db = db;
            _pushTokenStore = pushTokenStore;
            _fcmPushService = fcmPushService;
            _logger = logger;
            _clock = clock ?? TimeProvider.System;
            _신규업무자격 = 신규업무자격;
            _알림허용 = new(db, notificationSettings, sharedAdmission, _clock, availability);
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

            var now = _clock.GetUtcNow().UtcDateTime;
            var queue = await _db.운송원장.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == 배차대기Id && x.의뢰Id == 의뢰Id, cancellationToken);
            var isFood = queue?.배차업무유형 == 상태값.배차업무유형.음식배달;
            if (!isFood && !await 신규화물알림가능Async(기사Id, 의뢰Id, cancellationToken)) return;
            if (isFood && !FoodRecommendationNotificationRecoveryPolicy.IsCurrentRecommendation(queue!,
                new 배차추천알림Outbox { 배차대기Id = 배차대기Id, 의뢰Id = 의뢰Id, 기사Id = 기사Id, 추천라운드 = 추천라운드 }, now))
                return;
            var dataJson = JsonSerializer.Serialize(isFood && queue!.추천만료시각.HasValue
                ? FoodRecommendationData(queue, 기사Id)
                : new Dictionary<string, string>
            {
                ["type"] = 기사배차추천알림계약.현재유형,
                ["dispatchWaitingId"] = 배차대기Id.ToString(),
                ["requestId"] = 의뢰Id,
                ["recommendationRound"] = 추천라운드.ToString()
            }, JsonOptions);

            var item = new 배차추천알림Outbox
            {
                배차대기Id = 배차대기Id,
                의뢰Id = 의뢰Id,
                기사Id = 기사Id,
                추천라운드 = 추천라운드,
                제목 = isFood ? "음식 배달 · 새 배차 추천" : "화물 운송 · 새 배차 추천",
                본문 = isFood ? "앱에서 새 음식 배달 추천을 확인해 주세요." : "앱에서 새 화물 운송 추천을 확인해 주세요.",
                DataJson = dataJson,
                발송상태 = 상태_대기,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.배차추천알림Outbox.Add(item);
            try { await _db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException ex) when (isFood && ex.Entries.Count == 1 && ReferenceEquals(ex.Entries[0].Entity, item))
            {
                // The existing unique key also protects concurrent repair workers.
                _db.Entry(item).State = EntityState.Detached;
                if (!await _db.배차추천알림Outbox.AsNoTracking().AnyAsync(x => x.배차대기Id == 배차대기Id
                    && x.기사Id == 기사Id && x.추천라운드 == 추천라운드 && x.의뢰Id == 의뢰Id, cancellationToken)) throw;
            }
        }

        public Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default)
            => 업무유형별대기알림발송Async(상태값.배차업무유형.용달운송, take, cancellationToken);

        public async Task<int> 업무유형별대기알림발송Async(int 배차업무유형, int take = 100, CancellationToken cancellationToken = default)
        {
            if (배차업무유형 is not (상태값.배차업무유형.용달운송 or 상태값.배차업무유형.음식배달))
                throw new ArgumentOutOfRangeException(nameof(배차업무유형), "음식 배달 또는 용달 운송 유형이 필요합니다.");

            if (배차업무유형 == 상태값.배차업무유형.음식배달)
                return take <= 0 ? 0 : await 음식알림복구발송Async(Math.Min(take, 100), cancellationToken);

            var appKey = 기사앱식별자.CargoYongdalDriverApp;

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

                var now = _clock.GetUtcNow().UtcDateTime;
                item.시도횟수 += 1;
                item.마지막시도시각 = now;
                item.UpdatedAt = now;

                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(FoodRecommendationNotificationRecoveryPolicy.SendTimeoutSeconds));
                    var sendToken = timeout.Token;
                    var queue = await _알림허용.CurrentAsync(item, 상태값.배차업무유형.용달운송, sendToken);
                    if (queue is null) { item.발송상태 = "Obsolete"; continue; }
                    if (!await _알림허용.AllowedAsync(queue, item.기사Id, sendToken).WaitAsync(sendToken))
                    { item.발송상태 = "Suppressed"; continue; }
                    if (!await 신규화물알림가능Async(item.기사Id, item.의뢰Id, sendToken))
                    {
                        item.발송상태 = "Cancelled";
                        continue;
                    }
                    var token = await _pushTokenStore.GetForAppAsync(item.기사Id, appKey, sendToken).WaitAsync(sendToken);
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

                    // 토큰 조회를 기다리는 동안 정지·만료된 자격으로 알리지 않는다.
                    queue = await _알림허용.CurrentAsync(item, 상태값.배차업무유형.용달운송, sendToken);
                    if (queue is null) { item.발송상태 = "Obsolete"; continue; }
                    if (!await 신규화물알림가능Async(item.기사Id, item.의뢰Id, sendToken))
                    {
                        item.발송상태 = "Cancelled";
                        continue;
                    }

                    // Keep the existing cargo event type and external payload.
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(item.DataJson, JsonOptions)
                               ?? new Dictionary<string, string>();
                    if (!기사배차추천알림계약.Is배차추천(data.GetValueOrDefault("type"))
                        || data.GetValueOrDefault("requestId") != item.의뢰Id
                        || data.GetValueOrDefault("dispatchWaitingId") != item.배차대기Id.ToString(CultureInfo.InvariantCulture)
                        || data.GetValueOrDefault("recommendationRound") != item.추천라운드.ToString(CultureInfo.InvariantCulture))
                    { item.발송상태 = "Obsolete"; continue; }
                    data["appKey"] = appKey;
                    data["userId"] = item.기사Id;
                    data["workKind"] = SharedDriverWorkAdmissionService.ResolveWorkKind(queue);
                    data["expiresAtUtc"] = DateTime.SpecifyKind(queue.추천만료시각!.Value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
                    if (!await _알림허용.AllowedAsync(queue, item.기사Id, sendToken).WaitAsync(sendToken))
                    { item.발송상태 = "Suppressed"; continue; }
                    // 표시·소리·진동은 현재 계정의 앱 설정과 OS 허용을 거친 로컬 게시가 담당합니다.
                    var sent = await _fcmPushService.SendAsync(new FcmPushMessage(token, "", "", data,
                        HighPriority: true, DataOnly: true), sendToken).WaitAsync(sendToken);

                    item.발송상태 = sent ? 상태_성공 : 상태_실패;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    item.발송상태 = 상태_실패;
                    _logger.LogWarning("배차추천 알림 발송 실패. OutboxId={OutboxId}", item.Id);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return processed;
        }

        private async Task<bool> 신규화물알림가능Async(string driverId, string requestId, CancellationToken ct)
        {
            if (!(await 신규화물업무자격검사.평가Async(_신규업무자격, driverId, ct)).CanReceiveNewWork) return false;
            if (_신규업무자격?.RequiresCargoEvidence == false) return true;
            var request = await _db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
            return request is not null && 신규화물업무자격검사.화물정보완료(_신규업무자격, request);
        }

        private static Dictionary<string, string> FoodRecommendationData(운송원장 queue, string driverId)
            => new()
            {
                ["type"] = "FoodDeliveryRecommendation",
                ["appKey"] = 기사앱식별자.FoodDeliveryDriverApp,
                ["userId"] = driverId,
                ["offerId"] = queue.의뢰Id,
                ["workKind"] = SharedDriverWorkAdmissionService.ResolveWorkKind(queue),
                ["expiresAtUtc"] = DateTime.SpecifyKind(queue.추천만료시각!.Value, DateTimeKind.Utc)
                    .ToString("O", CultureInfo.InvariantCulture)
            };
    }
}
