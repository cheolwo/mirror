using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;
using 홍달.Services.External.PublicData;

namespace Hongdal.Tests.Services.Content;

public sealed class CommunityFishCooperativeInformationCandidateSourceTests
{
    [Fact]
    public async Task ReadAsync_달력범위의기준월을조회하고동일조합시계열후보로변환한다()
    {
        var client = new StubClient(month =>
        [
            Item(month, "001", "통영수산업협동조합", 100m + month.Month),
            Item(month, "002", "부산시수산업협동조합", 200m + month.Month)
        ]);
        var source = new CommunityFishCooperativeInformationCandidateSource(
            client,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 19, 3, 0, 0, TimeSpan.Zero)));

        var result = await source.ReadAsync(new CommunityInformationCollectionQuery
        {
            StartDate = new DateOnly(2026, 5, 15),
            EndDate = new DateOnly(2026, 7, 10),
            SearchText = "통영",
            CountryCode = "KR",
            ReviewState = CommunityInformationReviewStates.OfficialObservation,
            Take = 100
        });

        Assert.Equal(
            [new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1)],
            client.RequestedMonths);
        Assert.Equal(3, result.Count);
        var may = Assert.Single(result, item => item.ReferenceDate == new DateOnly(2026, 5, 1));
        Assert.Equal(new DateOnly(2026, 5, 31), may.ReferencePeriodEndDate);
        Assert.Equal(105m, may.NumericValue);
        Assert.Equal("명", may.Unit);
        Assert.Equal("총임직원", may.MetricLabel);
        Assert.Equal("fish-coop|001|TOTAL|employee-count", may.MetricSeriesKey);
        Assert.Equal(CommunityInformationReviewStates.OfficialObservation, may.ReviewState);
        Assert.Contains("현재 인력", may.Limitations, StringComparison.Ordinal);
        Assert.All(result, item => Assert.Contains("통영", item.Title, StringComparison.Ordinal));
        Assert.Equal(
            CommunityInformationCollectionModes.OnDemandPublicDataQuery,
            source.Source.CollectionMode);
    }

    [Fact]
    public async Task ReadAsync_최대조회월수를넘기면외부호출하지않는다()
    {
        var client = new StubClient(_ => []);
        var source = new CommunityFishCooperativeInformationCandidateSource(
            client,
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => source.ReadAsync(
            new CommunityInformationCollectionQuery
            {
                StartDate = new DateOnly(2025, 1, 1),
                EndDate = new DateOnly(2026, 7, 31)
            }));

        Assert.Contains("13개월", exception.Message, StringComparison.Ordinal);
        Assert.Empty(client.RequestedMonths);
    }

    private static FishCooperativeGeneralStatisticsItem Item(
        DateOnly month,
        string code,
        string name,
        decimal count)
        => new()
        {
            BaseYearMonth = month.ToString("yyyyMM"),
            Title = "수협_일반현황_임직원현황",
            FinancialCompanyCode = code,
            FinancialCompanyName = name,
            EmployeeCount = count,
            EmployeeClassificationCode = "TOTAL",
            EmployeeClassificationName = "총임직원"
        };

    private sealed class StubClient(
        Func<DateOnly, IReadOnlyList<FishCooperativeGeneralStatisticsItem>> factory)
        : IFishCooperativeStatisticsClient
    {
        public List<DateOnly> RequestedMonths { get; } = [];

        public Task<IReadOnlyList<FishCooperativeGeneralStatisticsItem>> FetchGeneralStatisticsAsync(
            DateOnly baseMonth,
            CancellationToken cancellationToken = default)
        {
            RequestedMonths.Add(baseMonth);
            return Task.FromResult(factory(baseMonth));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
