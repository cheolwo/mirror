using HongdalApp.Services.Application;

namespace HongdalApp.Services.Customs.Commands;

public sealed record AssignCustomsBrokerCommand(long ReviewId, string BrokerId)
    : IAppCommand<bool>;
