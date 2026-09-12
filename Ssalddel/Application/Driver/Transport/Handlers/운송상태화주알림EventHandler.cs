using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.Services.Notifications;

namespace Ssalddel.Application.Driver.Transport;

public sealed class 운송상태화주알림EventHandler :
    INotificationHandler<운송상차지도착됨Event>,
    INotificationHandler<운송상차완료됨Event>,
    INotificationHandler<운송하차지도착됨Event>,
    INotificationHandler<운송인수완료됨Event>,
    INotificationHandler<운송문제신고됨Event>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SsalddelContext _db;
    private readonly ILogger<운송상태화주알림EventHandler> _logger;

    public 운송상태화주알림EventHandler(
        SsalddelContext db,
        ILogger<운송상태화주알림EventHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task Handle(운송상차지도착됨Event notification, CancellationToken cancellationToken)
        => 처리Async(new 운송상태화주알림요청(
            nameof(운송상차지도착Command),
            nameof(운송상차지도착됨Event),
            Command알림FeatureNames.운송상차지도착,
            "상차지도착",
            "TransportArrivedPickup",
            notification.기사Id,
            notification.운송Id,
            운송번호: null,
            notification.이전상태,
            notification.현재상태,
            notification.발생시각Utc,
            notification.TraceId,
            "기사님이 상차지에 도착했습니다.",
            "기사님이 상차지에 도착했습니다. 상차 담당자와 물건 준비 상태를 확인해 주세요."),
            cancellationToken);

    public Task Handle(운송상차완료됨Event notification, CancellationToken cancellationToken)
        => 처리Async(new 운송상태화주알림요청(
            nameof(운송상차완료Command),
            nameof(운송상차완료됨Event),
            Command알림FeatureNames.운송상차완료,
            "상차완료",
            "TransportPickupCompleted",
            notification.기사Id,
            notification.운송Id,
            notification.운송번호,
            notification.이전상태,
            notification.현재상태,
            notification.발생시각Utc,
            notification.TraceId,
            "상차가 완료되었습니다.",
            "상차 완료 증빙이 등록되었습니다. 운송 진행 상태를 확인해 주세요.",
            new
            {
                notification.인수증증빙?.서명확보됨,
                notification.인수증증빙?.서명필수여부,
                notification.인수증증빙?.증빙방식,
                notification.인수증증빙?.상차사진ObjectName,
                notification.인수증증빙?.상차사진Url
            }),
            cancellationToken);

    public Task Handle(운송하차지도착됨Event notification, CancellationToken cancellationToken)
        => 처리Async(new 운송상태화주알림요청(
            nameof(운송하차지도착Command),
            nameof(운송하차지도착됨Event),
            Command알림FeatureNames.운송하차지도착,
            "하차지도착",
            "TransportArrivedDropoff",
            notification.기사Id,
            notification.운송Id,
            운송번호: null,
            notification.이전상태,
            notification.현재상태,
            notification.발생시각Utc,
            notification.TraceId,
            "기사님이 하차지에 도착했습니다.",
            "기사님이 하차지에 도착했습니다. 하차 담당자와 인수 준비를 확인해 주세요."),
            cancellationToken);

    public Task Handle(운송인수완료됨Event notification, CancellationToken cancellationToken)
        => 처리Async(new 운송상태화주알림요청(
            nameof(운송인수완료Command),
            nameof(운송인수완료됨Event),
            Command알림FeatureNames.운송인수완료,
            "인수완료",
            "TransportDropoffCompleted",
            notification.기사Id,
            notification.운송Id,
            notification.운송번호,
            이전상태: null,
            notification.상태,
            notification.발생시각Utc,
            notification.TraceId,
            "운송 인수가 완료되었습니다.",
            "하차 완료 증빙이 등록되었습니다. 정산 상태를 확인해 주세요.",
            new
            {
                notification.하차완료증빙?.하차사진ObjectName,
                notification.하차완료증빙?.하차사진Url
            }),
            cancellationToken);

    public Task Handle(운송문제신고됨Event notification, CancellationToken cancellationToken)
        => 처리Async(new 운송상태화주알림요청(
            nameof(운송문제신고Command),
            nameof(운송문제신고됨Event),
            Command알림FeatureNames.운송현장예외신고,
            "운송예외",
            "TransportFieldIssueReported",
            notification.기사Id,
            notification.운송Id,
            notification.운송번호,
            이전상태: null,
            현재상태: null,
            notification.발생시각Utc,
            notification.TraceId,
            "기사님이 운송 현장 문제를 신고했습니다.",
            $"{notification.단계} 단계에서 {notification.사유} 사유가 접수되었습니다.",
            new
            {
                notification.단계,
                notification.예외코드,
                notification.사유,
                notification.메모,
                notification.증빙ObjectName,
                notification.증빙Url,
                notification.관리자확인필요,
                notification.전체수량,
                notification.정상확인수량,
                notification.영향수량,
                notification.현장진행불가,
                notification.업무통제상태Code,
                notification.보류범위Code
            },
            원장상태변경여부: false),
            cancellationToken);

    private async Task 처리Async(운송상태화주알림요청 요청, CancellationToken cancellationToken)
    {
        try
        {
            var context = await 조회Async(요청, cancellationToken);
            if (context is null)
            {
                return;
            }

            if (요청.원장상태변경여부 && !string.IsNullOrWhiteSpace(요청.현재상태))
            {
                context.화주운송의뢰.배차상태 = 화주배차상태값(요청.현재상태);
                context.화주운송의뢰.UpdatedAt = DateTime.UtcNow;
            }

            if (!string.Equals(context.화주운송의뢰.화주Id, 요청.기사Id, StringComparison.Ordinal))
            {
                await 알림의도적재Async(context, 요청, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "운송 상태 화주 알림 처리 중 예외가 발생했습니다. TransportId={TransportId} EventName={EventName}",
                요청.운송Id,
                요청.EventName);
        }
    }

    private async Task<운송상태화주알림Context?> 조회Async(
        운송상태화주알림요청 요청,
        CancellationToken cancellationToken)
    {
        운송원장? transport = null;
        var 운송번호 = 요청.운송번호;
        if (string.IsNullOrWhiteSpace(운송번호))
        {
            transport = await _db.운송원장
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == 요청.운송Id, cancellationToken);
            운송번호 = transport?.운송번호;
        }

        if (string.IsNullOrWhiteSpace(운송번호))
        {
            _logger.LogDebug(
                "화주 알림 생략. 운송번호를 찾을 수 없습니다. TransportId={TransportId} EventName={EventName}",
                요청.운송Id,
                요청.EventName);
            return null;
        }

        var request = await _db.화주운송의뢰
            .FirstOrDefaultAsync(x => x.의뢰Id == 운송번호, cancellationToken);
        if (request is null)
        {
            _logger.LogDebug(
                "화주 알림 생략. 화주 운송 의뢰를 찾을 수 없습니다. TransportId={TransportId} RequestId={RequestId} EventName={EventName}",
                요청.운송Id,
                운송번호,
                요청.EventName);
            return null;
        }

        transport ??= await _db.운송원장
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 요청.운송Id, cancellationToken);

        return new 운송상태화주알림Context(request, transport, 운송번호);
    }

    private async Task 알림의도적재Async(
        운송상태화주알림Context context,
        운송상태화주알림요청 요청,
        CancellationToken cancellationToken)
    {
        if (await 이미생성됨Async(context.의뢰Id, 요청, cancellationToken))
        {
            return;
        }

        var request = context.화주운송의뢰;
        var transport = context.운송;
        var now = DateTime.UtcNow;
        var targetUserId = string.IsNullOrWhiteSpace(request.주문자UserId)
            ? request.화주Id
            : request.주문자UserId;

        _db.Command알림Outbox.Add(new Command알림Outbox
        {
            CommandName = 요청.CommandName,
            EventName = 요청.EventName,
            FeatureName = 요청.FeatureName,
            Target = "Shipper",
            PayloadJson = JsonSerializer.Serialize(new
            {
                알림유형 = 요청.알림유형,
                TargetUserId = targetUserId,
                ShipperUserId = request.화주Id,
                DriverId = 요청.기사Id,
                RequestId = request.의뢰Id,
                TransportId = 요청.운송Id,
                TransportNo = context.의뢰Id,
                CargoType = request.화물종류,
                PickupAddress = request.픽업_도로명주소,
                PickupAddressDetail = request.픽업_상세주소,
                PickupContactName = request.픽업_연락처_이름,
                PickupContactPhone = request.픽업_연락처_전화번호,
                PickupWindowStartUtc = request.픽업_시간창_시작일시,
                PickupWindowEndUtc = request.픽업_시간창_종료일시,
                DropoffAddress = request.하차_도로명주소,
                DropoffAddressDetail = request.하차_상세주소,
                DropoffContactName = request.하차_연락처_이름,
                DropoffContactPhone = request.하차_연락처_전화번호,
                DropoffWindowStartUtc = request.하차_시간창_시작일시,
                DropoffWindowEndUtc = request.하차_시간창_종료일시,
                TransportOrigin = transport?.출발지 ?? request.픽업_도로명주소,
                TransportDestination = transport?.도착지 ?? request.하차_도로명주소,
                BeforeStatus = 요청.이전상태,
                AfterStatus = 요청.현재상태,
                OccurredAtUtc = 요청.발생시각Utc,
                Title = 요청.Title,
                Body = 요청.Body,
                Evidence = 요청.Evidence,
                Channels = new[] { "Push", "AlimTalk" }
            }, JsonOptions),
            Status = "Pending",
            TraceId = 요청.TraceId,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private Task<bool> 이미생성됨Async(
        string 의뢰Id,
        운송상태화주알림요청 요청,
        CancellationToken cancellationToken)
    {
        var requestIdFragment = $"\"requestId\":\"{의뢰Id}\"";
        var transportIdFragment = $"\"transportId\":{요청.운송Id}";
        return _db.Command알림Outbox
            .AsNoTracking()
            .AnyAsync(x => x.FeatureName == 요청.FeatureName
                           && x.EventName == 요청.EventName
                           && x.Target == "Shipper"
                           && x.PayloadJson.Contains(requestIdFragment)
                           && x.PayloadJson.Contains(transportIdFragment),
                cancellationToken);
    }

    private static string 화주배차상태값(string 운송상태)
        => 운송상태 switch
        {
            기사운송상태코드.상차완료 => 상태값.배차상태.상차완료,
            기사운송상태코드.인수완료 => 상태값.배차상태.인수완료,
            _ => 운송상태
        };

    private sealed record 운송상태화주알림요청(
        string CommandName,
        string EventName,
        string FeatureName,
        string 알림유형,
        string ActionCode,
        string 기사Id,
        long 운송Id,
        string? 운송번호,
        string? 이전상태,
        string? 현재상태,
        DateTime 발생시각Utc,
        string TraceId,
        string Title,
        string Body,
        object? Evidence = null,
        bool 원장상태변경여부 = true);

    private sealed record 운송상태화주알림Context(
        화주운송의뢰 화주운송의뢰,
        운송원장? 운송,
        string 의뢰Id);
}
