using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;
using 홍달.Services.Storage.Local;

namespace 홍달.Services.Notifications;

public sealed class Command알림Outbox발송Service : ICommand알림Outbox발송Service
{
    private const string 상태_대기 = "Pending";
    private const string 상태_성공 = "Succeeded";
    private const string 상태_실패 = "Failed";

    private readonly HongdalContext _db;
    private readonly I사용자PushTokenStore _userPushTokenStore;
    private readonly IFcmPushService _fcmPushService;
    private readonly IKakaoAlimTalkService _kakaoAlimTalkService;
    private readonly KakaoAlimTalkOptions _kakaoOptions;
    private readonly ILogger<Command알림Outbox발송Service> _logger;

    public Command알림Outbox발송Service(
        HongdalContext db,
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
        var items = await _db.Command알림Outbox
            .Where(x => x.Status == 상태_대기
                        && ((x.Target == "Shipper"
                             && (x.FeatureName == Command알림FeatureNames.배차수락
                                 || x.FeatureName == Command알림FeatureNames.상차접근
                                 || x.FeatureName == Command알림FeatureNames.운송완료입금요청
                                 || x.FeatureName == Command알림FeatureNames.운송상차지도착
                                 || x.FeatureName == Command알림FeatureNames.운송상차완료
                                 || x.FeatureName == Command알림FeatureNames.운송하차지도착
                                 || x.FeatureName == Command알림FeatureNames.운송인수완료
                                 || x.FeatureName == Command알림FeatureNames.운송현장예외신고))
                            || (x.Target == "CustomsBroker"
                                && x.FeatureName == Command알림FeatureNames.공동수입원장등록)
                            || (x.Target == Command알림TargetNames.공동구매원장관계자
                                && x.FeatureName == Command알림FeatureNames.공동구매원장변경)))
            .OrderBy(x => x.CreatedAt)
            .Take(take)
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
                var payload = Command알림Payload.Parse(item.PayloadJson);
                var now = DateTime.UtcNow;
                if (payload.IsScheduledForFuture(now))
                {
                    continue;
                }

                processed++;
                item.RetryCount += 1;
                item.UpdatedAt = now;

                var pushRequested = payload.Channels.Contains("Push");
                var alimTalkRequested = payload.Channels.Contains("AlimTalk");

                var pushSent = !pushRequested || await Fcm발송Async(payload, cancellationToken);
                var alimTalkSent = !alimTalkRequested
                                   || !_kakaoOptions.Enabled
                                   || await 알림톡발송Async(item.FeatureName, payload, cancellationToken);

                item.Status = pushSent && alimTalkSent ? 상태_성공 : 상태_실패;
                if (item.Status == 상태_실패)
                {
                    _logger.LogWarning(
                        "Command 알림 발송 실패. OutboxId={OutboxId} FeatureName={FeatureName} TargetUserId={TargetUserId} PushSent={PushSent} AlimTalkSent={AlimTalkSent}",
                        item.Id,
                        item.FeatureName,
                        payload.TargetUserId,
                        pushSent,
                        alimTalkSent);
                }
            }
            catch (Exception ex)
            {
                item.Status = 상태_실패;
                _logger.LogWarning(ex, "Command 알림 발송 처리 중 예외가 발생했습니다. OutboxId={OutboxId}", item.Id);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return processed;
    }

    private async Task<bool> Fcm발송Async(Command알림Payload payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.TargetUserId))
        {
            return false;
        }

        var installationTokens = await _db.HongdalMobilePushInstallations
            .AsNoTracking()
            .Where(x => x.IsActive && x.UserId == payload.TargetUserId)
            .Select(x => x.PushToken)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (installationTokens.Count > 0)
        {
            var results = new List<bool>(installationTokens.Count);
            foreach (var installationToken in installationTokens)
            {
                results.Add(await SendFcmAsync(installationToken, payload, cancellationToken));
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
        CancellationToken cancellationToken)
        => _fcmPushService.SendToTokenAsync(
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

    private Task<bool> 알림톡발송Async(string featureName, Command알림Payload payload, CancellationToken cancellationToken)
    {
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

        return _kakaoAlimTalkService.SendAsync(
            new KakaoAlimTalkMessage(
                payload.RecipientPhone,
                templateCode,
                payload.Title,
                payload.Body,
                variables),
            cancellationToken);
    }
}
