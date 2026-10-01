namespace HongdalApp.Services.Application;

public interface IAppEventHandler<in TEvent>
    where TEvent : IAppEvent
{
    Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default);
}
