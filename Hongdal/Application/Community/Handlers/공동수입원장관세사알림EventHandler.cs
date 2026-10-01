using Hongdal.Application.Community.Events;
using Hongdal.Services.Community;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Application.Community.Handlers;

public sealed class 공동수입원장관세사알림EventHandler : INotificationHandler<커뮤니티원장변경됨Event>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<공동수입원장관세사알림EventHandler> _logger;

    public 공동수입원장관세사알림EventHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<공동수입원장관세사알림EventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Handle(커뮤니티원장변경됨Event notification, CancellationToken cancellationToken)
    {
        if (!공동수입원장관세사알림Policy.ShouldQueue(notification.변경유형, notification.원장))
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<I공동수입원장관세사알림Service>();
            await service.등록알림적재Async(
                notification.원장,
                notification.EventId,
                notification.발생시각Utc,
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                ex,
                "공동수입 원장 등록 관세사 알림 적재에 실패했습니다. EventId={EventId}, LedgerId={LedgerId}",
                notification.EventId,
                notification.원장.원장Id);
            throw;
        }
    }
}
