using MediatR;
using Microsoft.Extensions.Logging;
using Ssalddel.Services.Operations;

namespace Ssalddel.Application.Driver.Transport;

public sealed class 운송완료화주인수인계EventHandler : INotificationHandler<운송인수완료됨Event>
{
    private readonly I화물운송완료화주인수인계Service _handoffService;
    private readonly ILogger<운송완료화주인수인계EventHandler> _logger;

    public 운송완료화주인수인계EventHandler(
        I화물운송완료화주인수인계Service handoffService,
        ILogger<운송완료화주인수인계EventHandler> logger)
    {
        _handoffService = handoffService;
        _logger = logger;
    }

    public async Task Handle(운송인수완료됨Event notification, CancellationToken cancellationToken)
    {
        try
        {
            var handoff = await _handoffService.완료결과요청Async(
                notification.운송Id,
                notification.하차완료증빙 is not null,
                cancellationToken);
            _logger.LogInformation(
                "화물 운송 완료 결과를 화주 인수 단계로 반환했습니다. TransportId={TransportId} HandoffStableId={HandoffStableId} Status={Status}",
                notification.운송Id,
                handoff.인계StableId,
                handoff.상태Code);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "화물 운송 완료 결과의 화주 인수 인계 중 예외가 발생했습니다. TransportId={TransportId}",
                notification.운송Id);
        }
    }
}
