using Hongdal.Infrastructure.BackgroundJobs.Community;
using Hongdal.Services.Community;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Infrastructure.BackgroundJobs.Community;

public sealed class CommunityEditorialBatchRunnerTests
{
    [Fact]
    public async Task ReflectionRun_UsesLocalDateAndPublishesDraft()
    {
        var source = new RecordingSource(CommunityAutomatedPostSourceKeys.Reflection, hasDraft: true);
        var publisher = new RecordingPublisher();
        var runner = CreateRunner(source, publisher);

        await runner.RunReflectionAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 17), source.PublicationDate);
        Assert.NotNull(publisher.Draft);
        Assert.Equal(CommunityAutomatedPostSourceKeys.Reflection, publisher.Draft.SourceKey);
    }

    [Fact]
    public async Task SourceWithoutVerifiedData_DoesNotPublishEmptyPost()
    {
        var source = new RecordingSource(CommunityAutomatedPostSourceKeys.ActivityDigest, hasDraft: false);
        var publisher = new RecordingPublisher();
        var runner = CreateRunner(source, publisher);

        await runner.RunActivityDigestAsync(CancellationToken.None);

        Assert.Equal(0, publisher.CallCount);
    }

    [Fact]
    public async Task PrajnaRun_PublishesOnlyTheSingleDraftReturnedByTheSource()
    {
        var source = new RecordingSource(CommunityAutomatedPostSourceKeys.Prajna, hasDraft: true);
        var publisher = new RecordingPublisher();
        var runner = CreateRunner(source, publisher);

        await runner.RunPrajnaPublicationAsync(CancellationToken.None);

        Assert.Equal(1, publisher.CallCount);
        Assert.Equal(CommunityAutomatedPostSourceKeys.Prajna, publisher.Draft?.SourceKey);
    }

    private static CommunityEditorialBatchRunner CreateRunner(
        ICommunityAutomatedPostSource targetSource,
        RecordingPublisher publisher)
    {
        var sources = new ICommunityAutomatedPostSource[]
        {
            targetSource,
            new RecordingSource(CommunityAutomatedPostSourceKeys.KamisPriceBrief, hasDraft: false),
            new RecordingSource(CommunityAutomatedPostSourceKeys.Reflection, hasDraft: false),
            new RecordingSource(CommunityAutomatedPostSourceKeys.ActivityDigest, hasDraft: false)
        }
            .GroupBy(source => source.SourceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        return new CommunityEditorialBatchRunner(
            sources,
            publisher,
            Options.Create(new CommunityEditorialBatchOptions { TimeZoneId = "Asia/Seoul" }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero)),
            NullLogger<CommunityEditorialBatchRunner>.Instance);
    }

    private sealed class RecordingSource(string sourceKey, bool hasDraft) : ICommunityAutomatedPostSource
    {
        public string SourceKey { get; } = sourceKey;

        public DateOnly? PublicationDate { get; private set; }

        public Task<CommunityAutomatedPostDraft?> BuildAsync(
            DateOnly publicationDate,
            TimeZoneInfo timeZone,
            CancellationToken cancellationToken = default)
        {
            PublicationDate = publicationDate;
            return Task.FromResult<CommunityAutomatedPostDraft?>(hasDraft
                ? new CommunityAutomatedPostDraft(
                    SourceKey,
                    publicationDate.ToString("yyyyMMdd"),
                    "자유·생활",
                    "테스트",
                    "자동 정보",
                    "자동 글",
                    "자동 작성 안내",
                    "홍달 시스템")
                : null);
        }
    }

    private sealed class RecordingPublisher : ICommunityAutomatedPostPublisher
    {
        public int CallCount { get; private set; }

        public CommunityAutomatedPostDraft? Draft { get; private set; }

        public Task<CommunityAutomatedPostPublishResult> PublishIfMissingAsync(
            CommunityAutomatedPostDraft draft,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Draft = draft;
            return Task.FromResult(new CommunityAutomatedPostPublishResult(1, true));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
