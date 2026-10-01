using Hongdal.Infrastructure.BackgroundJobs.AgriculturalFisheries;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Infrastructure.BackgroundJobs.AgriculturalFisheries;

public sealed class AgriculturalFisheriesBatchRunnerTests
{
    [Fact]
    public void GetLocalDate_UsesConfiguredTimeZone()
    {
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 16, 16, 30, 0, TimeSpan.Zero));

        var result = AgriculturalFisheriesBatchSchedule.GetLocalDate(
            timeProvider,
            "Asia/Seoul");

        Assert.Equal(new DateOnly(2026, 7, 17), result);
    }

    [Fact]
    public void GetKamisMonthlyRange_ReturnsLastTwelveCompletedMonths()
    {
        var options = new AgriculturalFisheriesBatchOptions
        {
            KamisMonthlyLookbackMonths = 12
        };

        var result = AgriculturalFisheriesBatchSchedule.GetKamisMonthlyRange(
            new DateOnly(2026, 7, 17),
            options);

        Assert.Equal(new DateOnly(2025, 7, 1), result.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 30), result.EndDate);
    }

    [Fact]
    public async Task RunAll_UsesExpectedDomesticDatesAndUsdaYear()
    {
        var kamis = new RecordingKamisArchiveService();
        var usda = new RecordingUsdaArchiveService();
        var options = new AgriculturalFisheriesBatchOptions
        {
            TimeZoneId = "Asia/Seoul",
            KamisDailyDaysBehind = 1,
            KamisMonthlyLookbackMonths = 12,
            UsdaLookbackYears = 1
        };
        var runner = new AgriculturalFisheriesBatchRunner(
            kamis,
            usda,
            Options.Create(options),
            new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero)),
            NullLogger<AgriculturalFisheriesBatchRunner>.Instance);

        await runner.RunKamisDailyAsync(CancellationToken.None);
        await runner.RunKamisMonthlyAsync(CancellationToken.None);
        await runner.RunUsdaMonthlyAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 16), kamis.DailyDate);
        Assert.Equal(new DateOnly(2025, 7, 1), kamis.MonthlyStartDate);
        Assert.Equal(new DateOnly(2026, 6, 30), kamis.MonthlyEndDate);
        Assert.Equal(2025, usda.YearFrom);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 3, true)]
    [InlineData(3, 3, false)]
    [InlineData(0, 0, false)]
    public void ShouldRetry_StopsAtConfiguredLimit(
        int refireCount,
        int retryLimit,
        bool expected)
    {
        Assert.Equal(
            expected,
            AgriculturalFisheriesBatchJobExecution.ShouldRetry(refireCount, retryLimit));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingKamisArchiveService : IKamisPriceArchiveService
    {
        public DateOnly? DailyDate { get; private set; }

        public DateOnly? MonthlyStartDate { get; private set; }

        public DateOnly? MonthlyEndDate { get; private set; }

        public Task<KamisPriceArchiveResult> CollectDailyPricesAsync(
            DateOnly requestedDate,
            CancellationToken cancellationToken = default)
        {
            DailyDate = requestedDate;
            return Task.FromResult(EmptyResult(requestedDate));
        }

        public Task<KamisPriceArchiveResult> CollectPeriodPricesAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EmptyResult(endDate));

        public Task<KamisPriceArchiveResult> CollectMonthlyPricesAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            MonthlyStartDate = startDate;
            MonthlyEndDate = endDate;
            return Task.FromResult(EmptyResult(endDate));
        }

        private static KamisPriceArchiveResult EmptyResult(DateOnly date)
            => new(1, 0, 0, 0, 0, date);
    }

    private sealed class RecordingUsdaArchiveService : IUsdaNassPriceArchiveService
    {
        public int? YearFrom { get; private set; }

        public Task<UsdaNassPriceArchiveResult> CollectRecentMonthlyPricesAsync(
            int yearFrom,
            CancellationToken cancellationToken = default)
        {
            YearFrom = yearFrom;
            return Task.FromResult(
                new UsdaNassPriceArchiveResult(1, 0, 0, 0, 0, null));
        }
    }
}
