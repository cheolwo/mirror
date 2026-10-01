using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.AgriculturalFisheries;
using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using Hongdal.Services.Content;
using Microsoft.EntityFrameworkCore;

namespace Hongdal.Tests.Services.Content;

public sealed class CommunityUsdaNassInformationCandidateSourceTests
{
    [Fact]
    public async Task ReadAsync_MapsStoredMonthlyPricesToOneStableMetricSeries()
    {
        await using var context = CreateContext();
        context.PriceObservations.AddRange(
            Observation("corn-jun", "JUN", 4.25m),
            Observation("corn-jul", "JUL", 4.50m),
            Observation("corn-suppressed", "JUL", null, isSuppressed: true));
        await context.SaveChangesAsync();
        var source = new CommunityUsdaNassInformationCandidateSource(context);

        var items = await source.ReadAsync(new CommunityInformationCollectionQuery
        {
            CountryCode = "US",
            SearchText = "CORN",
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2026, 7, 10),
            Take = 20
        });

        Assert.Equal(2, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal(CommunityInformationSourceKeys.UsdaNassPriceObservations, item.SourceKey);
            Assert.Equal("US", item.CountryCode);
            Assert.Equal("USD", item.CurrencyCode);
            Assert.Equal("BU", item.Unit);
            Assert.Equal("생산자가격", item.MetricLabel);
            Assert.Contains("CORN", item.MetricSeriesLabel, StringComparison.Ordinal);
            Assert.Contains("소매가격", item.Limitations, StringComparison.Ordinal);
        });
        Assert.Single(items.Select(item => item.MetricSeriesKey).Distinct(StringComparer.Ordinal));
        Assert.Contains(items, item => item.ReferenceDate == new DateOnly(2026, 6, 1)
                                      && item.ReferencePeriodEndDate == new DateOnly(2026, 6, 30));
        Assert.Contains(items, item => item.ReferenceDate == new DateOnly(2026, 7, 1)
                                      && item.ReferencePeriodEndDate == new DateOnly(2026, 7, 31));
    }

    [Fact]
    public async Task ReadAsync_DoesNotExposeArchiveForAnotherCountry()
    {
        await using var context = CreateContext();
        context.PriceObservations.Add(Observation("corn-jul", "JUL", 4.50m));
        await context.SaveChangesAsync();
        var source = new CommunityUsdaNassInformationCandidateSource(context);

        var items = await source.ReadAsync(new CommunityInformationCollectionQuery
        {
            CountryCode = "KR"
        });

        Assert.Empty(items);
    }

    private static UsdaNassPriceObservation Observation(
        string recordKey,
        string referencePeriod,
        decimal? value,
        bool isSuppressed = false)
        => new()
        {
            RecordKey = recordKey,
            SourceDesc = "SURVEY",
            SectorDesc = "CROPS",
            GroupDesc = "FIELD CROPS",
            CommodityDesc = "CORN",
            ClassDesc = "GRAIN",
            StatisticCategoryDesc = "PRICE RECEIVED",
            UnitDesc = "$ / BU",
            ShortDesc = "CORN, GRAIN - PRICE RECEIVED, MEASURED IN $ / BU",
            DomainDesc = "TOTAL",
            AggregationLevelDesc = "NATIONAL",
            CountryCode = "9000",
            CountryName = "UNITED STATES",
            Year = 2026,
            FrequencyDesc = "MONTHLY",
            ReferencePeriodDesc = referencePeriod,
            ValueRaw = value?.ToString() ?? "(D)",
            NumericValue = value,
            IsSuppressed = isSuppressed,
            SourceUrl = "https://quickstats.nass.usda.gov/api",
            LastSeenAtUtc = new DateTime(2026, 7, 19, 1, 0, 0, DateTimeKind.Utc)
        };

    private static AgriculturalFisheriesDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AgriculturalFisheriesDbContext>()
            .UseInMemoryDatabase($"community-usda-{Guid.NewGuid():N}")
            .Options;
        return new AgriculturalFisheriesDbContext(options);
    }
}
