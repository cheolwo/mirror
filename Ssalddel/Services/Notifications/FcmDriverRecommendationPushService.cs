using System.Globalization;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Hubs;
using Microsoft.Extensions.Options;
using 살뜰.Services.Options;
using 살뜰.Data;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Dispatch.Notification;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.설정;

namespace 살뜰.Services.Notifications
{
    public sealed class FcmDriverRecommendationPushService : IDriverRecommendationPushService
    {
        private readonly IDriverPushTokenStore _tokenStore;
        private readonly IDriverRecommendationPushStateStore _pushStateStore;
        private readonly IFcmPushService _fcmPushService;
        private readonly ILogger<FcmDriverRecommendationPushService> _logger;
        private readonly DriverRecommendationNotificationGate _알림허용;

        public FcmDriverRecommendationPushService(
            IDriverPushTokenStore tokenStore,
            IDriverRecommendationPushStateStore pushStateStore,
            IFcmPushService fcmPushService,
            IOptions<PushNotificationsOptions> options,
            ILogger<FcmDriverRecommendationPushService> logger,
            SsalddelContext? db = null,
            IDriverNotificationSettingsStore? notificationSettings = null,
            ISharedDriverWorkAdmissionService? sharedAdmission = null,
            TimeProvider? clock = null,
            I운영배차공통UseCase? availability = null)
        {
            _tokenStore = tokenStore;
            _pushStateStore = pushStateStore;
            _fcmPushService = fcmPushService;
            _logger = logger;
            _알림허용 = new(db, notificationSettings, sharedAdmission, clock ?? TimeProvider.System, availability);
        }

        public async Task<bool> SendAsync(
            string driverId,
            IReadOnlyList<DispatchRecommendationDto> recommendations,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(driverId) || recommendations.Count == 0)
            {
                _logger.LogDebug(
                    "Action={Action} DriverId={DriverId} Result={Result} Reason={Reason} TraceId={TraceId} OccurredAt={OccurredAt}",
                    "NotificationSkipped",
                    driverId,
                    "Skipped",
                    "Empty driverId or recommendations",
                    System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                    DateTime.UtcNow);
                return false;
            }

            try
            {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(FoodRecommendationNotificationRecoveryPolicy.SendTimeoutSeconds));
            var ct = timeout.Token;
            var top = recommendations[0];
            var queue = await _알림허용.CurrentAsync(driverId, top.의뢰Id, ct);
            if (queue is null || !await _알림허용.AllowedAsync(queue, driverId, ct).WaitAsync(ct)) return false;
            var expected = new 배차추천알림Outbox { 배차대기Id = queue.Id, 의뢰Id = queue.의뢰Id,
                기사Id = driverId, 추천라운드 = queue.추천라운드 };
            var kind = queue.배차업무유형;
            var isFood = kind == 상태값.배차업무유형.음식배달;
            var appKey = isFood ? 기사앱식별자.FoodDeliveryDriverApp : 기사앱식별자.CargoYongdalDriverApp;
            var token = await _tokenStore.GetForAppAsync(driverId, appKey, ct).WaitAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogDebug(
                    "Action={Action} DriverId={DriverId} Result={Result} Reason={Reason} TraceId={TraceId} OccurredAt={OccurredAt}",
                    "NotificationSkipped",
                    driverId,
                    "Skipped",
                    "No push token registered",
                    System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                    DateTime.UtcNow);
                return false;
            }

            queue = await _알림허용.CurrentAsync(expected, kind, ct);
            if (queue is null || !await _알림허용.AllowedAsync(queue, driverId, ct).WaitAsync(ct)) return false;
            // 목록 전체의 유효성을 과거 추천 DTO로 추정하지 않고 현재 확인한 한 건만 알립니다.
            var ids = new[] { queue.의뢰Id };
            if (!await _pushStateStore.HasChangedAsync(driverId, ids, ct).WaitAsync(ct).ConfigureAwait(false))
            {
                _logger.LogDebug(
                    "Action={Action} DriverId={DriverId} Result={Result} Reason={Reason} TraceId={TraceId} OccurredAt={OccurredAt}",
                    "NotificationSkipped",
                    driverId,
                    "Skipped",
                    "Recommendation set unchanged",
                    System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                    DateTime.UtcNow);
                return false;
            }

            queue = await _알림허용.CurrentAsync(expected, kind, ct);
            if (queue is null || !await _알림허용.AllowedAsync(queue, driverId, ct).WaitAsync(ct)) return false;
            var data = new Dictionary<string, string>
                {
                    ["type"] = isFood ? "FoodDeliveryRecommendation" : 기사배차추천알림계약.현재유형,
                    ["driverId"] = driverId,
                    ["recommendationCount"] = "1",
                    ["topRequestId"] = queue.의뢰Id,
                    ["requestIds"] = string.Join(",", ids),
                    ["dispatchWaitingId"] = queue.Id.ToString(CultureInfo.InvariantCulture),
                    ["requestId"] = queue.의뢰Id,
                    ["recommendationRound"] = queue.추천라운드.ToString(CultureInfo.InvariantCulture),
                    ["appKey"] = appKey,
                    ["userId"] = driverId,
                    ["workKind"] = SharedDriverWorkAdmissionService.ResolveWorkKind(queue),
                    ["expiresAtUtc"] = DateTime.SpecifyKind(queue.추천만료시각!.Value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture)
                };
            if (isFood) data["offerId"] = queue.의뢰Id;
            // 주소·배달료·표시 문구는 알림에서 복제하지 않고 앱의 현재 계정 정본과 표시 정책을 사용합니다.
            var sent = await _fcmPushService.SendAsync(new FcmPushMessage(token, "", "", data,
                HighPriority: true, DataOnly: true), ct).WaitAsync(ct).ConfigureAwait(false);

            if (!sent)
            {
                _logger.LogWarning(
                    "Action={Action} DriverId={DriverId} Result={Result} RecommendationCount={RecommendationCount} TraceId={TraceId} OccurredAt={OccurredAt}",
                    "NotificationSent",
                    driverId,
                    "Failed",
                    recommendations.Count,
                    System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                    DateTime.UtcNow);
                return false;
            }

            _logger.LogInformation(
                "Action={Action} DriverId={DriverId} Result={Result} RecommendationCount={RecommendationCount} TraceId={TraceId} OccurredAt={OccurredAt}",
                "NotificationSent",
                driverId,
                "Success",
                recommendations.Count,
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                DateTime.UtcNow);

            return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                _logger.LogWarning("Recommendation notification was not sent. Settings or current recommendation could not be verified.");
                return false;
            }
        }
    }
}
