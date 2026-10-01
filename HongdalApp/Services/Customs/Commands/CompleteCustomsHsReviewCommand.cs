using HongdalApp.Services.Application;

namespace HongdalApp.Services.Customs.Commands;

public sealed record CompleteCustomsHsReviewCommand(long ReviewId, string HsCode, string Comment)
    : IAppCommand<bool>;
