namespace HongdalApp.Services.Application;

public interface IAppEvent
{
    DateTime OccurredAt { get; }
}
