using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Community;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Application.Shipper.Payment.Handlers;

public sealed class 용달운송의뢰결제승인완료EventHandler : INotificationHandler<결제승인완료Event>
{
    private readonly SsalddelContext _db;
    private readonly I운송의뢰배차대기Service _dispatchQueueService;
    private readonly I운송원장Mongo동기화Service _transportLedgerSync;
    private readonly ILogger<용달운송의뢰결제승인완료EventHandler> _logger;

    public 용달운송의뢰결제승인완료EventHandler(
        SsalddelContext db,
        I운송의뢰배차대기Service dispatchQueueService,
        I운송원장Mongo동기화Service transportLedgerSync,
        ILogger<용달운송의뢰결제승인완료EventHandler> logger)
    {
        _db = db;
        _dispatchQueueService = dispatchQueueService;
        _transportLedgerSync = transportLedgerSync;
        _logger = logger;
    }

    public async Task Handle(결제승인완료Event notification, CancellationToken cancellationToken)
    {
        if (notification.결제대상유형 != 살뜰.도메인.결제.결제공통정의.결제대상유형.용달운송의뢰)
        {
            return;
        }

        var shipperRequest = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == notification.대상Id, cancellationToken);
        if (shipperRequest is null)
        {
            _logger.LogWarning("결제승인완료 후처리 대상 의뢰를 찾지 못했습니다. 대상Id={대상Id}", notification.대상Id);
            return;
        }

        shipperRequest.결제상태 = 상태값.결제상태.결제완료;
        var hasOpenAbnormalTransportIncident = await _db.비정상운송사건
            .AsNoTracking()
            .AnyAsync(
                x => x.운송의뢰Id == shipperRequest.의뢰Id
                     && (x.정산보류적용여부
                         || x.상태Code == 비정상운송사건상태Codes.운영검토대기),
                cancellationToken);
        shipperRequest.정산상태 = hasOpenAbnormalTransportIncident
            ? 운임정산상태.비정상운송검토보류.ToString()
            : 운임정산상태.결제완료.ToString();
        shipperRequest.UpdatedAt = DateTime.UtcNow;

        var createDispatchQueue = TryParseSettlementTime(shipperRequest.정산시점) == 정산시점.선결제;
        if (createDispatchQueue)
        {
            shipperRequest.배차상태 = 상태값.배차상태.매칭중;

            await _dispatchQueueService.생성또는조회Async(
                화주운송의뢰출고예정정규화.To출고예정운송대상(shipperRequest),
                new 운송의뢰배차대기생성옵션
                {
                    픽업상세주소 = shipperRequest.픽업_상세주소,
                    하차상세주소 = shipperRequest.하차_상세주소
                },
                cancellationToken);
        }
        else
        {
            shipperRequest.배차상태 = 상태값.배차상태.미시작;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _transportLedgerSync.화주운송의뢰동기화Async(shipperRequest, "payment", cancellationToken);

        _logger.LogInformation(
            "결제승인완료 후처리 완료(용달운송의뢰): 결제Id={결제Id}, 대상Id={대상Id}, 금액={금액}{통화}",
            notification.결제Id,
            notification.대상Id,
            notification.결제금액,
            notification.통화);
    }

    private static 정산시점 TryParseSettlementTime(string? value)
    {
        return Enum.TryParse<정산시점>(value, ignoreCase: false, out var parsed)
            ? parsed
            : 정산시점.선결제;
    }
}
