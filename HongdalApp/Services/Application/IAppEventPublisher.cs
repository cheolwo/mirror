namespace HongdalApp.Services.Application;

public interface IAppEventPublisher
{
    Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IAppEvent;
}
