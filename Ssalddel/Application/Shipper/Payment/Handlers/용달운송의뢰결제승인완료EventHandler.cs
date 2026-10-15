using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Community;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Options;
using 살뜰.도메인.결제;
using Microsoft.Extensions.Options;

namespace Ssalddel.Application.Shipper.Payment.Handlers;

public sealed class 용달운송의뢰결제승인완료EventHandler : INotificationHandler<결제승인완료Event>
{
    private readonly SsalddelContext _db;
    private readonly I운송의뢰배차대기Service _dispatchQueueService;
    private readonly I운송원장Mongo동기화Service _transportLedgerSync;
    private readonly ILogger<용달운송의뢰결제승인완료EventHandler> _logger;
    private readonly IOptions<TransportPrepaymentOptions> _prepaymentOptions;

    public 용달운송의뢰결제승인완료EventHandler(
        SsalddelContext db,
        I운송의뢰배차대기Service dispatchQueueService,
        I운송원장Mongo동기화Service transportLedgerSync,
        ILogger<용달운송의뢰결제승인완료EventHandler> logger,
        IOptions<TransportPrepaymentOptions>? prepaymentOptions = null)
    {
        _db = db;
        _dispatchQueueService = dispatchQueueService;
        _transportLedgerSync = transportLedgerSync;
        _logger = logger;
        _prepaymentOptions = prepaymentOptions ?? Microsoft.Extensions.Options.Options.Create(new TransportPrepaymentOptions());
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

        if (shipperRequest.모의처리처수행)
        {
            // 지연된 결제 이벤트로 모의 수행의 정산 상태나 자체 배차 큐를 만들지 않습니다.
            return;
        }

        var payment = await _db.결제.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == notification.결제레코드Id && x.결제Id == notification.결제Id, cancellationToken);
        if (payment is null || payment.결제대상유형 != notification.결제대상유형
            || payment.대상Id != notification.대상Id || payment.의뢰Id != shipperRequest.의뢰Id
            || payment.결제제공자 != notification.결제제공자 || payment.결제금액 != notification.결제금액
            || payment.통화 != notification.통화 || payment.결제상태 != 상태값.결제상태.결제완료
            || payment.공통결제상태 != 결제공통정의.결제상태.승인완료)
            throw new InvalidOperationException("결제 승인 후속의 금액·대상·완료 근거가 원장과 일치하지 않습니다.");

        // 이미 종결된 의뢰에는 지연 승인 이벤트가 업무 상태를 재대입하지 않습니다.
        if (shipperRequest.상태 == 상태값.의뢰상태.취소 || shipperRequest.배차상태 == 상태값.배차상태.취소)
            return;
        if (payment.결제제공자 == 결제공통정의.결제제공자.TossPayments)
        {
            if (!TossPaymentCollectionEvidencePolicy.수납확인됨(payment))
                throw new InvalidOperationException("토스 정본 수납 완료 근거를 확인할 수 없습니다.");
            var evidence = 배송료결제준비근거Policy.현재근거검증(payment, shipperRequest);
            if (evidence.IsFailed) throw new InvalidOperationException(evidence.Errors[0].Message);
        }

        shipperRequest.결제상태 = 상태값.결제상태.결제완료;
        var hasOpenAbnormalTransportIncident = await _db.비정상운송사건
            .AsNoTracking()
            .AnyAsync(
                x => x.운송의뢰Id == shipperRequest.의뢰Id
                     && (x.정산보류적용여부
                         || x.상태Code == 비정상운송사건상태Codes.운영검토대기),
                cancellationToken);
        // 지연·중복 이벤트가 지급 준비/완료나 이미 보류된 정산을 결제완료로 되돌리지 않습니다.
        if (hasOpenAbnormalTransportIncident)
            shipperRequest.정산상태 = 운임정산상태.비정상운송검토보류.ToString();
        else if (shipperRequest.정산상태 is "결제대기" or "결제확보대기" or "운임확정" or "결제완료"
                 or "청구대기" or "인수증대기" or "후불승인대기" or "현장수금예정")
            shipperRequest.정산상태 = 운임정산상태.결제완료.ToString();
        shipperRequest.UpdatedAt = DateTime.UtcNow;

        var createDispatchQueue = _prepaymentOptions.Value.Enabled
            && 화주운송결제진행정책.선결제초기상태인가(shipperRequest)
            && !await _db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == shipperRequest.의뢰Id, cancellationToken);
        if (createDispatchQueue)
        {
            var queued = await _dispatchQueueService.생성또는조회Async(
                화주운송의뢰출고예정정규화.To출고예정운송대상(shipperRequest),
                new 운송의뢰배차대기생성옵션
                {
                    픽업상세주소 = shipperRequest.픽업_상세주소,
                    하차상세주소 = shipperRequest.하차_상세주소
                },
                cancellationToken);
            if (queued.상태 is 상태값.배차대기상태.대기 or 상태값.배차상태.매칭중)
                shipperRequest.배차상태 = 상태값.배차상태.매칭중;
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

}
