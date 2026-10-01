using Microsoft.Extensions.DependencyInjection;

namespace HongdalApp.Services.Application;

public sealed class AppEventPublisher : IAppEventPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public AppEventPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IAppEvent
    {
        var handlers = _serviceProvider.GetServices<IAppEventHandler<TEvent>>();
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(appEvent, cancellationToken);
        }
    }
}
