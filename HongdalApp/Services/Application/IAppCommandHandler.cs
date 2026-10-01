namespace HongdalApp.Services.Application;

public interface IAppCommandHandler<in TCommand, TResult>
    where TCommand : IAppCommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
