using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Infrastructure.BackgroundJobs.AgriculturalFisheries;

public sealed class AgriculturalFisheriesBatchRunner
{
    private readonly IKamisPriceArchiveService _kamisArchiveService;
    private readonly IUsdaNassPriceArchiveService _usdaArchiveService;
    private readonly AgriculturalFisheriesBatchOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AgriculturalFisheriesBatchRunner> _logger;

    public AgriculturalFisheriesBatchRunner(
        IKamisPriceArchiveService kamisArchiveService,
        IUsdaNassPriceArchiveService usdaArchiveService,
        IOptions<AgriculturalFisheriesBatchOptions> options,
        TimeProvider timeProvider,
        ILogger<AgriculturalFisheriesBatchRunner> logger)
    {
        _kamisArchiveService = kamisArchiveService;
        _usdaArchiveService = usdaArchiveService;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RunKamisDailyAsync(CancellationToken cancellationToken)
    {
        var localDate = AgriculturalFisheriesBatchSchedule.GetLocalDate(
            _timeProvider,
            _options.TimeZoneId);
        var targetDate = AgriculturalFisheriesBatchSchedule.GetKamisDailyTargetDate(
            localDate,
            _options);
        var result = await _kamisArchiveService.CollectDailyPricesAsync(
            targetDate,
            cancellationToken);

        _logger.LogInformation(
            "Action={Action} TargetDate={TargetDate} RunId={RunId} Fetched={Fetched} Inserted={Inserted} Updated={Updated} Existing={Existing} LatestSurveyDate={LatestSurveyDate}",
            "KamisDailyPricesCollected",
            targetDate,
            result.CollectionRunId,
            result.FetchedCount,
            result.InsertedCount,
            result.UpdatedCount,
            result.ExistingCount,
            result.LatestSurveyDate);
    }

    public async Task RunKamisMonthlyAsync(CancellationToken cancellationToken)
    {
        var localDate = AgriculturalFisheriesBatchSchedule.GetLocalDate(
            _timeProvider,
            _options.TimeZoneId);
        var range = AgriculturalFisheriesBatchSchedule.GetKamisMonthlyRange(
            localDate,
            _options);
        var result = await _kamisArchiveService.CollectMonthlyPricesAsync(
            range.StartDate,
            range.EndDate,
            cancellationToken);

        _logger.LogInformation(
            "Action={Action} StartDate={StartDate} EndDate={EndDate} RunId={RunId} Fetched={Fetched} Inserted={Inserted} Updated={Updated} Existing={Existing} LatestSurveyDate={LatestSurveyDate}",
            "KamisMonthlyPricesCollected",
            range.StartDate,
            range.EndDate,
            result.CollectionRunId,
            result.FetchedCount,
            result.InsertedCount,
            result.UpdatedCount,
            result.ExistingCount,
            result.LatestSurveyDate);
    }

    public async Task RunUsdaMonthlyAsync(CancellationToken cancellationToken)
    {
        var localDate = AgriculturalFisheriesBatchSchedule.GetLocalDate(
            _timeProvider,
            _options.TimeZoneId);
        var yearFrom = AgriculturalFisheriesBatchSchedule.GetUsdaYearFrom(
            localDate,
            _options);
        var result = await _usdaArchiveService.CollectRecentMonthlyPricesAsync(
            yearFrom,
            cancellationToken);

        _logger.LogInformation(
            "Action={Action} YearFrom={YearFrom} RunId={RunId} Fetched={Fetched} Inserted={Inserted} Existing={Existing} Mappings={Mappings} LatestSourceLoad={LatestSourceLoad}",
            "UsdaMonthlyPricesCollected",
            yearFrom,
            result.CollectionRunId,
            result.FetchedCount,
            result.InsertedCount,
            result.ExistingCount,
            result.MappingCount,
            result.LatestSourceLoadTimeUtc);
    }
}
