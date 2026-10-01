namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed record KamisPriceArchiveResult(
    long CollectionRunId,
    int FetchedCount,
    int InsertedCount,
    int UpdatedCount,
    int ExistingCount,
    DateOnly? LatestSurveyDate);

public interface IKamisPriceArchiveService
{
    Task<KamisPriceArchiveResult> CollectDailyPricesAsync(
        DateOnly requestedDate,
        CancellationToken cancellationToken = default);

    Task<KamisPriceArchiveResult> CollectPeriodPricesAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    Task<KamisPriceArchiveResult> CollectMonthlyPricesAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
