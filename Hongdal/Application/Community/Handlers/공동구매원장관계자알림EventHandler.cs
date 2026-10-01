using Hongdal.Application.Community.Events;
using Hongdal.Services.Community;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Application.Community.Handlers;

public sealed class 공동구매원장관계자알림EventHandler : INotificationHandler<커뮤니티원장변경됨Event>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<공동구매원장관계자알림EventHandler> _logger;

    public 공동구매원장관계자알림EventHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<공동구매원장관계자알림EventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Handle(커뮤니티원장변경됨Event notification, CancellationToken cancellationToken)
    {
        if (!공동구매원장관계자알림Policy.ShouldQueue(notification.변경유형, notification.원장))
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<I공동구매원장관계자알림Service>();
            await service.변경알림적재Async(
                notification.원장,
                notification.변경유형,
                notification.변경자,
                notification.EventId,
                notification.발생시각Utc,
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                ex,
                "공동구매 원장 변경 관계자 알림 적재에 실패했습니다. EventId={EventId}, LedgerId={LedgerId}",
                notification.EventId,
                notification.원장.원장Id);
            throw;
        }
    }
}
