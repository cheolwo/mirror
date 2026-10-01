using HongdalApp.Services.Application;

namespace HongdalApp.Services.Customs.Events;

public sealed record CustomsHsReviewCompletedEvent(
    long ReviewId,
    string HsCode,
    DateTime OccurredAt) : IAppEvent;
