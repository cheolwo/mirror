using MediatR;
using Microsoft.Extensions.Logging;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Community;

namespace Hongdal.Application.Driver.DispatchAction;

public sealed partial class 배차수락사후처리EventHandler : INotificationHandler<배차수락됨Event>
{
    private readonly HongdalContext _db;
    private readonly IDispatchAcceptanceLogStore _acceptanceLogStore;
    private readonly 홍달.Services.Dispatch.Queue.I배차대기원장전환Service _원장전환Service;
    private readonly I운송원장Mongo동기화Service _원장동기화Service;
    private readonly ILogger<배차수락사후처리EventHandler> _logger;

    public 배차수락사후처리EventHandler(
        HongdalContext db,
        IDispatchAcceptanceLogStore acceptanceLogStore,
        홍달.Services.Dispatch.Queue.I배차대기원장전환Service 원장전환Service,
        I운송원장Mongo동기화Service 원장동기화Service,
        ILogger<배차수락사후처리EventHandler> logger)
    {
        _db = db;
        _acceptanceLogStore = acceptanceLogStore;
        _원장전환Service = 원장전환Service;
        _원장동기화Service = 원장동기화Service;
        _logger = logger;
    }

    public async Task Handle(배차수락됨Event notification, CancellationToken cancellationToken)
    {
        try
        {
            await _acceptanceLogStore.AppendAsync(new DispatchAcceptanceLogEntry(
                notification.기사Id,
                notification.화주Id,
                notification.의뢰Id,
                notification.발생시각Utc,
                notification.배차대기상태,
                notification.의뢰배차상태,
                notification.의뢰결제상태), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "배차수락 사후처리 중 수락로그 적재 예외가 발생했습니다. RequestId={RequestId}", notification.의뢰Id);
        }

        try
        {
            await _원장전환Service.실행주체확정결과동기화Async(
                notification.의뢰Id,
                DispatchConfirmationBoundaryRequest.ForDriverSelfAcceptance(
                    notification.기사Id,
                    notification.기사Id),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "배차수락 사후처리 중 큐전환 예외가 발생했습니다. RequestId={RequestId}", notification.의뢰Id);
        }

        try
        {
            await 운송진행건생성또는보정Async(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "배차수락 사후처리 중 운송 진행 건 생성 예외가 발생했습니다. RequestId={RequestId}", notification.의뢰Id);
        }

        try
        {
            await 화주수락알림의도적재Async(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "배차수락 사후처리 중 화주 알림 의도 적재 예외가 발생했습니다. RequestId={RequestId}", notification.의뢰Id);
        }

        _logger.LogDebug(
            "Action={Action} DriverId={DriverId} RequestId={RequestId} AfterStatus={AfterStatus} Result={Result} TraceId={TraceId} OccurredAt={OccurredAt}",
            "DispatchAccepted",
            notification.기사Id,
            notification.의뢰Id,
            notification.배차대기상태,
            "Success",
            notification.TraceId,
            notification.발생시각Utc);
    }

}
