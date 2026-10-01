using Hongdal.Application.Community.Events;
using Hongdal.Services.Community;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Application.Community.Handlers;

public sealed class 커뮤니티원장성립게시EventHandler : INotificationHandler<커뮤니티원장변경됨Event>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<커뮤니티원장성립게시EventHandler> _logger;

    public 커뮤니티원장성립게시EventHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<커뮤니티원장성립게시EventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Handle(커뮤니티원장변경됨Event notification, CancellationToken cancellationToken)
    {
        if (!CommunityLedgerCompletionPublication.IsCompleted(notification.원장))
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ICommunityLedgerCompletionPostService>();
            await service.PublishIfCompletedAsync(
                notification.원장,
                notification.EventId,
                notification.발생시각Utc,
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                ex,
                "완료 원장의 비식별 성립 사례 게시글 발행에 실패했습니다. EventId={EventId}, LedgerId={LedgerId}",
                notification.EventId,
                notification.원장.원장Id);
            throw;
        }
    }
}
