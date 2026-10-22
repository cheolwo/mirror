using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Outbox;
using Ssalddel.Contracts.Common.Notifications;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Notifications;

public sealed class Command알림Outbox발송Service : ICommand알림Outbox발송Service
{
    private readonly SsalddelContext _db;
    private readonly I사용자PushTokenStore _userPushTokenStore;
    private readonly IFcmPushService _fcmPushService;
    private readonly IKakaoAlimTalkService _kakaoAlimTalkService;
    private readonly KakaoAlimTalkOptions _kakaoOptions;
    private readonly ILogger<Command알림Outbox발송Service> _logger;

    public Command알림Outbox발송Service(
        SsalddelContext db,
        I사용자PushTokenStore userPushTokenStore,
        IFcmPushService fcmPushService,
        IKakaoAlimTalkService kakaoAlimTalkService,
        IOptions<KakaoAlimTalkOptions> kakaoOptions,
        ILogger<Command알림Outbox발송Service> logger)
    {
        _db = db;
        _userPushTokenStore = userPushTokenStore;
        _fcmPushService = fcmPushService;
        _kakaoAlimTalkService = kakaoAlimTalkService;
        _kakaoOptions = kakaoOptions.Value;
        _logger = logger;
    }

    public async Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var retryCutoff = now - OutboxProcessingPolicy.RetryDelay;
        var leaseCutoff = now - OutboxProcessingPolicy.LeaseTimeout;
        var items = await _db.Command알림Outbox
            .Where(x => ((x.Status == OutboxProcessingStatuses.Pending
                          && (x.RetryCount == 0 || x.UpdatedAt <= retryCutoff))
                         || (x.Status == OutboxProcessingStatuses.Processing
                             && x.UpdatedAt <= leaseCutoff))
                        && ((x.Target == 업무진행알림Recorder.Target && x.FeatureName == 업무진행알림Recorder.FeatureName)
                            || (x.Target == "Shipper"
                             && (x.FeatureName == Command알림FeatureNames.배차수락
                                 || x.FeatureName == Command알림FeatureNames.상차접근
                                 || x.FeatureName == Command알림FeatureNames.운송완료입금요청
                                 || x.FeatureName == Command알림FeatureNames.운송상차지도착
                                 || x.FeatureName == Command알림FeatureNames.운송상차완료
                                 || x.FeatureName == Command알림FeatureNames.운송하차지도착
                                 || x.FeatureName == Command알림FeatureNames.운송인수완료
                                 || x.FeatureName == Command알림FeatureNames.운송현장예외신고))
                            || (x.Target == "CustomsBroker"
                                && x.FeatureName == Command알림FeatureNames.같이수입원장등록)
                            || (x.Target == Command알림TargetNames.공동구매원장관계자
                                && x.FeatureName == Command알림FeatureNames.공동구매원장변경)))
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return 0;
        }

        var processed = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                item.Status = OutboxProcessingStatuses.Processing;
                item.RetryCount += 1;
                item.UpdatedAt = now;
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _db.Entry(item).State = EntityState.Detached;
                    continue;
                }

                var payload = Command알림Payload.Parse(item.PayloadJson);
                if (payload.IsScheduledForFuture(now))
                {
                    item.Status = OutboxProcessingStatuses.Pending;
                    item.RetryCount = Math.Max(0, item.RetryCount - 1);
                    item.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                processed++;

                var pushRequested = payload.Channels.Contains("Push");
                var alimTalkRequested = payload.Channels.Contains("AlimTalk");

                var pushSent = !pushRequested || await Fcm발송Async(payload, cancellationToken);
                var alimTalkSent = !alimTalkRequested
                                   || !_kakaoOptions.Enabled
                                   || await 알림톡발송Async(item.FeatureName, payload, cancellationToken);

                if (pushSent && alimTalkSent)
                {
                    item.Status = OutboxProcessingStatuses.Succeeded;
                }
                else
                {
                    var retry = OutboxProcessingPolicy.CanRetry(item.RetryCount);
                    item.Status = retry
                        ? OutboxProcessingStatuses.Pending
                        : OutboxProcessingStatuses.Failed;
                    _logger.LogWarning(
                        "Command 알림 발송 실패. OutboxId={OutboxId} FeatureName={FeatureName} TargetUserId={TargetUserId} PushSent={PushSent} AlimTalkSent={AlimTalkSent} Attempt={Attempt} WillRetry={WillRetry}",
                        item.Id,
                        item.FeatureName,
                        payload.TargetUserId,
                        pushSent,
                        alimTalkSent,
                        item.RetryCount,
                        retry);
                }
            }
            catch (JsonException ex)
            {
                item.Status = OutboxProcessingStatuses.Failed;
                _logger.LogWarning(
                    ex,
                    "Command 알림 Outbox payload가 올바르지 않습니다. OutboxId={OutboxId}",
                    item.Id);
            }
            catch (Exception ex)
            {
                var retry = OutboxProcessingPolicy.CanRetry(item.RetryCount);
                item.Status = retry
                    ? OutboxProcessingStatuses.Pending
                    : OutboxProcessingStatuses.Failed;
                _logger.LogWarning(
                    ex,
                    "Command 알림 발송 처리 중 예외가 발생했습니다. OutboxId={OutboxId} Attempt={Attempt} WillRetry={WillRetry}",
                    item.Id,
                    item.RetryCount,
                    retry);
            }

            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }

    private async Task<bool> Fcm발송Async(Command알림Payload payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.TargetUserId))
        {
            return false;
        }

        var installationTokens = await _db.SsalddelMobilePushInstallations
            .AsNoTracking()
            .Where(x => x.IsActive && x.UserId == payload.TargetUserId)
            .Select(x => new { x.UserId, x.PushToken, x.AppKey })
            .Distinct()
            .ToListAsync(cancellationToken);
        if (installationTokens.Count > 0)
        {
            var results = new List<bool>(installationTokens.Count);
            foreach (var installationToken in installationTokens)
            {
                if (!string.Equals(installationToken.UserId, payload.TargetUserId, StringComparison.Ordinal)) continue;
                results.Add(await SendFcmAsync(installationToken.PushToken, payload, cancellationToken,
                    installationToken.AppKey == "ssalddel.shipper"));
            }

            return results.Any(sent => sent);
        }

        var token = await _userPushTokenStore.GetAsync(payload.TargetUserId, cancellationToken);
        return !string.IsNullOrWhiteSpace(token)
               && await SendFcmAsync(token, payload, cancellationToken);
    }

    private Task<bool> SendFcmAsync(
        string token,
        Command알림Payload payload,
        CancellationToken cancellationToken,
        bool integratedApp = false)
    {
        if (payload.NotificationType == WorkflowProgressNotification.MessageType)
        {
            var data = new Dictionary<string, string>
            {
                ["type"] = payload.NotificationType, ["eventId"] = payload.EventId,
                ["workflowKey"] = payload.WorkflowKey, ["stageCode"] = payload.StageCode,
                ["sourceId"] = payload.SourceId, ["userId"] = payload.TargetUserId,
                ["occurredAtUtc"] = payload.OccurredAtUtc, ["deepLink"] = payload.DeepLink
            };
            if (!WorkflowProgressNotification.TryParse(data, out var hint) || hint is null)
                throw new JsonException("업무 진행 알림의 단계 또는 경로가 올바르지 않습니다.");
            return _fcmPushService.SendAsync(new FcmPushMessage(token, hint.Stage.Title, hint.Stage.Body,
                data, HighPriority: true, DataOnly: integratedApp), cancellationToken);
        }
        return _fcmPushService.SendToTokenAsync(
            token,
            payload.Title,
            payload.Body,
            new Dictionary<string, string>
            {
                ["type"] = payload.NotificationType,
                ["requestId"] = payload.RequestId,
                ["driverId"] = payload.DriverId,
                ["paymentId"] = payload.PaymentId,
                ["orderId"] = payload.OrderId,
                ["ledgerId"] = payload.LedgerId,
                ["hsCodes"] = payload.HsCodes,
                ["deepLink"] = payload.DeepLink
            },
            cancellationToken);
    }

    private async Task<bool> 알림톡발송Async(string featureName, Command알림Payload payload, CancellationToken cancellationToken)
    {
        if (payload.NotificationType == WorkflowProgressNotification.MessageType && payload.WorkflowKey == "cargo")
        {
            // 기존 알림톡 채널은 유지하되 연락처/주소를 Outbox 또는 FCM에 복제하지 않는다.
            // 실제 발송 때 같은 원천과 현재 수신 권한을 확인한 뒤 기존 템플릿 변수만 채운다.
            var source = await _db.화주운송의뢰.AsNoTracking()
                .SingleOrDefaultAsync(row => row.의뢰Id == payload.SourceId, cancellationToken);
            if (source is null || !string.Equals(source.의뢰Id, payload.SourceId, StringComparison.Ordinal)
                || !string.Equals(CargoWorkflowProgressNotifications.Recipient(source), payload.TargetUserId, StringComparison.Ordinal)) return false;
            payload = payload with
            {
                CargoType = source.화물종류, PickupAddress = source.픽업_도로명주소, DropoffAddress = source.하차_도로명주소,
                RecipientPhone = source.픽업_연락처_전화번호, PickupContactPhone = source.픽업_연락처_전화번호,
                PickupWindowText = source.픽업_시간창_시작일시 == default && source.픽업_시간창_종료일시 == default
                    ? "상차 시간 협의" : $"{source.픽업_시간창_시작일시:O} ~ {source.픽업_시간창_종료일시:O}"
            };
        }
        var variables = new Dictionary<string, string>
        {
            ["requestId"] = payload.RequestId,
            ["driverId"] = payload.DriverId,
            ["cargoType"] = payload.CargoType,
            ["pickupAddress"] = payload.PickupAddress,
            ["dropoffAddress"] = payload.DropoffAddress,
            ["pickupWindow"] = payload.PickupWindowText,
            ["paymentId"] = payload.PaymentId,
            ["orderId"] = payload.OrderId,
            ["amount"] = payload.AmountText,
            ["paymentFlow"] = payload.PaymentFlow,
            ["reminderDay"] = payload.ReminderDay.ToString()
        };
        var templateCode = featureName switch
        {
            Command알림FeatureNames.상차접근 => _kakaoOptions.DispatchPickupApproachTemplateCode,
            Command알림FeatureNames.운송완료입금요청 => _kakaoOptions.SettlementDepositReminderTemplateCode,
            _ => _kakaoOptions.DispatchAcceptedTemplateCode
        };

        return await _kakaoAlimTalkService.SendAsync(
            new KakaoAlimTalkMessage(
                payload.RecipientPhone,
                templateCode,
                payload.Title,
                payload.Body,
                variables),
            cancellationToken);
    }
}
