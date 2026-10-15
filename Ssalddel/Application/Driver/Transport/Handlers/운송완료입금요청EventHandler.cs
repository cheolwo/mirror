using MediatR;
using Microsoft.Extensions.Logging;

namespace Ssalddel.Application.Driver.Transport;

public sealed class 운송완료입금요청EventHandler :
    INotificationHandler<운송상차완료됨Event>,
    INotificationHandler<운송인수완료됨Event>
{
    private readonly I운송완료입금요청Service _입금요청Service;
    private readonly ILogger<운송완료입금요청EventHandler> _logger;
    private readonly ICargoCompletionRecoveryService? _recovery;

    public 운송완료입금요청EventHandler(
        I운송완료입금요청Service 입금요청Service,
        ILogger<운송완료입금요청EventHandler> logger,
        ICargoCompletionRecoveryService? recovery = null)
    {
        _입금요청Service = 입금요청Service;
        _logger = logger;
        _recovery = recovery;
    }

    public async Task Handle(운송상차완료됨Event notification, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _입금요청Service.조기준비Async(notification, cancellationToken);
            if (!result.처리됨)
            {
                _logger.LogDebug(
                    "상차 완료 조기 입금 요청 생략. TransportId={TransportId} RequestId={RequestId} Reason={Reason}",
                    notification.운송Id,
                    notification.운송번호,
                    result.사유);
                return;
            }

            _logger.LogInformation(
                "상차 완료 조기 입금 요청 준비 완료. TransportId={TransportId} RequestId={RequestId} PaymentId={PaymentId} OrderId={OrderId} ReminderCount={ReminderCount}",
                notification.운송Id,
                notification.운송번호,
                result.결제Id,
                result.OrderId,
                result.알림예약건수);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "상차 완료 조기 입금 요청 준비 중 예외가 발생했습니다. TransportId={TransportId} RequestId={RequestId}",
                notification.운송Id,
                notification.운송번호);
        }
    }

    public async Task Handle(운송인수완료됨Event notification, CancellationToken cancellationToken)
    {
        if (_recovery is not null)
        { await _recovery.ProcessTransportAsync(notification.운송Id, cancellationToken); return; }
        try
        {
            var result = await _입금요청Service.준비Async(notification, cancellationToken);
            if (!result.처리됨)
            {
                _logger.LogDebug(
                    "운송 완료 입금 요청 생략. TransportId={TransportId} RequestId={RequestId} Reason={Reason}",
                    notification.운송Id,
                    notification.운송번호,
                    result.사유);
                return;
            }

            _logger.LogInformation(
                "운송 완료 입금 요청 준비 완료. TransportId={TransportId} RequestId={RequestId} PaymentId={PaymentId} OrderId={OrderId} ReminderCount={ReminderCount}",
                notification.운송Id,
                notification.운송번호,
                result.결제Id,
                result.OrderId,
                result.알림예약건수);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "운송 완료 입금 요청 준비 중 예외가 발생했습니다. TransportId={TransportId} RequestId={RequestId}",
                notification.운송Id,
                notification.운송번호);
        }
    }
}
