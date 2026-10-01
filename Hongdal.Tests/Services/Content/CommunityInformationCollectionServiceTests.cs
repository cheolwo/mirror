using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.AgriculturalFisheries;
using Hongdal.Domain.Content;
using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using Hongdal.Services.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Services.Content;

public sealed class CommunityInformationCollectionServiceTests
{
    [Fact]
    public async Task Collection_MergesSortsAndFiltersCandidates()
    {
        var youtube = Source(
            CommunityInformationSourceKeys.YouTubeChannelVideos,
            Candidate(
                "youtube:1",
                CommunityInformationSourceKeys.YouTubeChannelVideos,
                "영상 속 사과",
                new DateTime(2026, 7, 17, 2, 0, 0, DateTimeKind.Utc),
                "US",
                CommunityInformationReviewStates.PendingReview));
        var kamis = Source(
            CommunityInformationSourceKeys.KamisPriceObservations,
            Candidate(
                "kamis:1",
                CommunityInformationSourceKeys.KamisPriceObservations,
                "사과 가격",
                new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc),
                "KR",
                CommunityInformationReviewStates.OfficialObservation));
        var service = CreateService(youtube, kamis);

        var response = await service.ReadAsync(new CommunityInformationCollectionQuery
        {
            CountryCode = "KR",
            SearchText = "사과",
            Take = 10
        });

        var item = Assert.Single(response.Items);
        Assert.Equal("kamis:1", item.CandidateKey);
        Assert.Equal(2, response.Sources.Count);
        Assert.Empty(response.Failures);
        Assert.Equal(
            new DateTime(2026, 7, 18, 3, 0, 0, DateTimeKind.Utc),
            response.GeneratedAtUtc);
    }

    [Fact]
    public async Task Collection_ReportsFailedSourceWithoutDiscardingOtherItems()
    {
        var healthy = Source(
            CommunityInformationSourceKeys.KamisPriceObservations,
            Candidate(
                "kamis:1",
                CommunityInformationSourceKeys.KamisPriceObservations,
                "배추 가격",
                new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc),
                "KR",
                CommunityInformationReviewStates.OfficialObservation));
        var failed = new StubSource(
            Descriptor(CommunityInformationSourceKeys.YouTubeChannelVideos),
            [],
            new InvalidOperationException("remote store unavailable"));
        var service = CreateService(healthy, failed);

        var response = await service.ReadAsync(new CommunityInformationCollectionQuery());

        Assert.Single(response.Items);
        var failure = Assert.Single(response.Failures);
        Assert.Equal(CommunityInformationSourceKeys.YouTubeChannelVideos, failure.SourceKey);
        Assert.DoesNotContain("remote store", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Collection_DoesNotCallOnDemandPublicDataUntilSourceIsExplicitlySelected()
    {
        var onDemand = new StubSource(
            Descriptor(
                CommunityInformationSourceKeys.FishCooperativeGeneralStatistics,
                CommunityInformationCollectionModes.OnDemandPublicDataQuery),
            [],
            new InvalidOperationException("should only run when selected"));
        var service = CreateService(onDemand);

        var allSourcesResponse = await service.ReadAsync(new CommunityInformationCollectionQuery());
        var selectedSourceResponse = await service.ReadAsync(new CommunityInformationCollectionQuery
        {
            SourceKey = CommunityInformationSourceKeys.FishCooperativeGeneralStatistics
        });

        Assert.Empty(allSourcesResponse.Failures);
        Assert.Contains(
            allSourcesResponse.Sources,
            source => source.SourceKey == CommunityInformationSourceKeys.FishCooperativeGeneralStatistics);
        var failure = Assert.Single(selectedSourceResponse.Failures);
        Assert.Equal(CommunityInformationSourceKeys.FishCooperativeGeneralStatistics, failure.SourceKey);
    }

    [Fact]
    public async Task Collection_FiltersCandidatesByInclusiveReferenceDateRange()
    {
        var source = Source(
            CommunityInformationSourceKeys.KamisPriceObservations,
            Candidate(
                "kamis:before",
                CommunityInformationSourceKeys.KamisPriceObservations,
                "범위 이전",
                new DateTime(2026, 7, 9, 1, 0, 0, DateTimeKind.Utc),
                "KR",
                CommunityInformationReviewStates.OfficialObservation),
            Candidate(
                "kamis:start",
                CommunityInformationSourceKeys.KamisPriceObservations,
                "시작일",
                new DateTime(2026, 7, 10, 1, 0, 0, DateTimeKind.Utc),
                "KR",
                CommunityInformationReviewStates.OfficialObservation),
            Candidate(
                "kamis:end",
                CommunityInformationSourceKeys.KamisPriceObservations,
                "종료일",
                new DateTime(2026, 7, 12, 1, 0, 0, DateTimeKind.Utc),
                "KR",
                CommunityInformationReviewStates.OfficialObservation),
            Candidate(
                "kamis:after",
                CommunityInformationSourceKeys.KamisPriceObservations,
                "범위 이후",
                new DateTime(2026, 7, 13, 1, 0, 0, DateTimeKind.Utc),
                "KR",
                CommunityInformationReviewStates.OfficialObservation));
        var service = CreateService(source);

        var response = await service.ReadAsync(new CommunityInformationCollectionQuery
        {
            StartDate = new DateOnly(2026, 7, 10),
            EndDate = new DateOnly(2026, 7, 12),
            Take = 10
        });

        Assert.Equal(2, response.Items.Count);
        Assert.Contains(response.Items, item => item.CandidateKey == "kamis:start");
        Assert.Contains(response.Items, item => item.CandidateKey == "kamis:end");
    }

    [Fact]
    public async Task Collection_IncludesMonthlyObservationWhenSelectedDatesOverlapItsReferencePeriod()
    {
        var monthly = Candidate(
                "fish-coop:202605:001:A",
                CommunityInformationSourceKeys.FishCooperativeGeneralStatistics,
                "5월 총임직원",
                new DateTime(2026, 5, 1, 1, 0, 0, DateTimeKind.Utc),
                "KR",
                CommunityInformationReviewStates.OfficialObservation)
            with
            {
                ReferencePeriodEndDate = new DateOnly(2026, 5, 31)
            };
        var service = CreateService(Source(
            CommunityInformationSourceKeys.FishCooperativeGeneralStatistics,
            monthly));

        var response = await service.ReadAsync(new CommunityInformationCollectionQuery
        {
            SourceKey = CommunityInformationSourceKeys.FishCooperativeGeneralStatistics,
            StartDate = new DateOnly(2026, 5, 15),
            EndDate = new DateOnly(2026, 5, 20)
        });

        Assert.Equal("fish-coop:202605:001:A", Assert.Single(response.Items).CandidateKey);
    }

    [Fact]
    public async Task YouTubeSource_ReturnsOnlyProjectRelatedChannelsWithSourceBoundary()
    {
        await using var context = CreateHongdalContext();
        var foodChannel = new YouTube감시채널
        {
            ChannelId = "food-channel",
            채널명 = "음식 채널",
            UploadsPlaylistId = "uploads-food",
            국가코드 = "US",
            기본언어코드 = "en",
            음식채널여부 = true,
            음식콘텐츠분류 = "CookingIngredient,FoodIndustry"
        };
        foodChannel.영상.Add(new YouTube채널영상
        {
            VideoId = "food-video",
            ChannelId = foodChannel.ChannelId,
            제목 = "  New apple ingredient video  ",
            설명 = "A public video description",
            게시일시Utc = new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc),
            최초감지일시Utc = new DateTime(2026, 7, 18, 2, 0, 0, DateTimeKind.Utc),
            공유상태 = YouTube채널영상.공유대기상태
        });
        var unrelatedChannel = new YouTube감시채널
        {
            ChannelId = "other-channel",
            채널명 = "기타 채널",
            UploadsPlaylistId = "uploads-other"
        };
        unrelatedChannel.영상.Add(new YouTube채널영상
        {
            VideoId = "other-video",
            ChannelId = unrelatedChannel.ChannelId,
            제목 = "Unrelated",
            게시일시Utc = new DateTime(2026, 7, 18, 3, 0, 0, DateTimeKind.Utc)
        });
        context.YouTube감시채널.AddRange(foodChannel, unrelatedChannel);
        await context.SaveChangesAsync();
        var source = new CommunityYouTubeInformationCandidateSource(
            new EfYouTube채널감시저장소(context));

        var items = await source.ReadAsync(new CommunityInformationCollectionQuery
        {
            CountryCode = "US",
            ReviewState = CommunityInformationReviewStates.PendingReview
        });

        var item = Assert.Single(items);
        Assert.Equal("youtube:food-video", item.CandidateKey);
        Assert.Equal("New apple ingredient video", item.Title);
        Assert.Contains("CookingIngredient", item.TopicTags);
        Assert.Contains("상품 사실", item.Limitations);
        Assert.Equal("https://www.youtube.com/watch?v=food-video", item.OriginalUrl);
    }

    [Fact]
    public async Task KamisSource_PreservesReferenceDateCurrencyUnitAndLimitations()
    {
        await using var context = CreateAgriculturalFisheriesContext();
        context.KamisPriceObservations.Add(new KamisPriceObservation
        {
            RecordKey = "daily-apple",
            ProductClassCode = "01",
            ProductClassName = "소매",
            CategoryCode = "400",
            CategoryName = "과일류",
            RequestedDate = new DateOnly(2026, 7, 18),
            SurveyDate = new DateOnly(2026, 7, 17),
            FrequencyCode = "Daily",
            ItemCode = "411",
            ItemName = "사과",
            KindCode = "01",
            KindName = "후지",
            RankCode = "04",
            RankName = "상품",
            Unit = "10개",
            PriceRaw = "25,000",
            PriceKrw = 25_000m,
            SourceUrl = "https://www.kamis.or.kr/service/price/xml.do",
            LastSeenAtUtc = new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();
        var source = new CommunityKamisInformationCandidateSource(context);

        var items = await source.ReadAsync(new CommunityInformationCollectionQuery
        {
            SearchText = "사과"
        });

        var item = Assert.Single(items);
        Assert.Equal(new DateOnly(2026, 7, 17), item.ReferenceDate);
        Assert.Equal("KRW", item.CurrencyCode);
        Assert.Equal("10개", item.Unit);
        Assert.Equal(25_000m, item.NumericValue);
        Assert.Equal("가격", item.MetricLabel);
        Assert.Equal("01|400|411|01|04|10개|DAILY", item.MetricSeriesKey);
        Assert.Contains("25,000원/10개", item.Summary);
        Assert.Contains("판매 권고", item.Limitations);
    }

    [Fact]
    public async Task KamisSource_IncludesStoredMonthlyAverageWhenCalendarRangeOverlapsMonth()
    {
        await using var context = CreateAgriculturalFisheriesContext();
        context.KamisPriceObservations.Add(new KamisPriceObservation
        {
            RecordKey = "monthly-apple-202606",
            ProductClassCode = "01",
            ProductClassName = "소매",
            CategoryCode = "400",
            CategoryName = "과일류",
            RequestedDate = new DateOnly(2026, 7, 1),
            SurveyDate = new DateOnly(2026, 6, 30),
            FrequencyCode = "Monthly",
            ItemCode = "411",
            ItemName = "사과",
            KindCode = "01",
            KindName = "후지",
            RankCode = "04",
            RankName = "상품",
            Unit = "kg",
            PriceRaw = "7,500",
            PriceKrw = 7_500m,
            SourceUrl = "https://www.kamis.or.kr/service/price/xml.do",
            LastSeenAtUtc = new DateTime(2026, 7, 2, 1, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();
        var source = new CommunityKamisInformationCandidateSource(context);

        var items = await source.ReadAsync(new CommunityInformationCollectionQuery
        {
            StartDate = new DateOnly(2026, 6, 5),
            EndDate = new DateOnly(2026, 6, 10),
            SearchText = "사과"
        });

        var item = Assert.Single(items);
        Assert.Equal(new DateOnly(2026, 6, 1), item.ReferenceDate);
        Assert.Equal(new DateOnly(2026, 6, 30), item.ReferencePeriodEndDate);
        Assert.Equal("월평균 가격", item.MetricLabel);
        Assert.EndsWith("|MONTHLY", item.MetricSeriesKey, StringComparison.Ordinal);
        Assert.Contains("월평균", item.Summary, StringComparison.Ordinal);
    }

    private static CommunityInformationCollectionService CreateService(
        params ICommunityInformationCandidateSource[] sources)
        => new(
            sources,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 18, 3, 0, 0, TimeSpan.Zero)),
            NullLogger<CommunityInformationCollectionService>.Instance);

    private static StubSource Source(
        string sourceKey,
        params CommunityInformationCandidateDto[] candidates)
        => new(Descriptor(sourceKey), candidates, null);

    private static CommunityInformationSourceDto Descriptor(
        string sourceKey,
        string collectionMode = CommunityInformationCollectionModes.ScheduledArchive)
        => new(
            sourceKey,
            CommunityInformationSourceTypes.PublicData,
            sourceKey,
            sourceKey,
            collectionMode,
            "daily",
            "review first",
            "https://example.com/docs",
            true);

    private static CommunityInformationCandidateDto Candidate(
        string candidateKey,
        string sourceKey,
        string title,
        DateTime publishedAtUtc,
        string countryCode,
        string reviewState)
        => new(
            candidateKey,
            sourceKey,
            CommunityInformationSourceTypes.PublicData,
            "provider",
            title,
            title,
            "https://example.com/item",
            null,
            publishedAtUtc,
            DateOnly.FromDateTime(publishedAtUtc),
            publishedAtUtc,
            countryCode,
            "ko",
            null,
            null,
            reviewState,
            ["사과"],
            "source",
            "limitations");

    private static HongdalContext CreateHongdalContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"community-information-youtube-{Guid.NewGuid():N}")
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private static AgriculturalFisheriesDbContext CreateAgriculturalFisheriesContext()
    {
        var options = new DbContextOptionsBuilder<AgriculturalFisheriesDbContext>()
            .UseInMemoryDatabase($"community-information-kamis-{Guid.NewGuid():N}")
            .Options;
        return new AgriculturalFisheriesDbContext(options);
    }

    private sealed class StubSource(
        CommunityInformationSourceDto source,
        IReadOnlyList<CommunityInformationCandidateDto> candidates,
        Exception? exception) : ICommunityInformationCandidateSource
    {
        public CommunityInformationSourceDto Source { get; } = source;

        public Task<IReadOnlyList<CommunityInformationCandidateDto>> ReadAsync(
            CommunityInformationCollectionQuery query,
            CancellationToken cancellationToken = default)
            => exception is null
                ? Task.FromResult(candidates)
                : Task.FromException<IReadOnlyList<CommunityInformationCandidateDto>>(exception);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
