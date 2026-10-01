using Hongdal.Contracts.Common.Community;
using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityAutomatedPostServiceTests
{
    [Fact]
    public async Task Publisher_UsesDeterministicAuthorKeyToAvoidDuplicatePost()
    {
        await using var context = CreateContext();
        var publisher = new RecordingPublisher();
        var service = new EfCommunityAutomatedPostPublisher(
            context,
            new 커뮤니티게시글음성작업예약Service(),
            new CommunityKeywordNotificationQueue(),
            publisher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero)),
            NullLogger<EfCommunityAutomatedPostPublisher>.Instance);
        var draft = new CommunityAutomatedPostDraft(
            CommunityAutomatedPostSourceKeys.Reflection,
            "20260717",
            CommunityBoardCatalog.FreeLife.DisplayName,
            "공동체 성찰",
            "자동 정보",
            "자동 성찰",
            "자동 작성 안내",
            "홍달 성찰봇");

        var first = await service.PublishIfMissingAsync(draft);
        var second = await service.PublishIfMissingAsync(draft);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.PostId, second.PostId);
        Assert.Single(await context.PlatformCommunityPosts.ToListAsync());
        Assert.Single(publisher.Notifications);
    }

    [Fact]
    public void SystemAuthorKey_UsesSourceAndPeriodAndResolvesSystemKind()
    {
        var post = new PlatformCommunityPost
        {
            AuthorUserId = CommunityAutomatedPostPublication.BuildSystemAuthorKey(
                CommunityAutomatedPostSourceKeys.KamisPriceBrief,
                "20260716")
        };

        Assert.Equal(
            "system:community-editorial:kamis-price-brief:20260716",
            post.AuthorUserId);
        Assert.Equal(
            PlatformCommunitySystemPostKinds.KamisPriceBrief,
            CommunityAutomatedPostPublication.GetSystemPostKind(post));
    }

    [Theory]
    [InlineData(CommunityAutomatedPostSourceKeys.PrajnaCard)]
    [InlineData(CommunityAutomatedPostSourceKeys.PrajnaVideo)]
    public void PrajnaSource_ResolvesAdminSelectedSystemKind(string sourceKey)
    {
        var post = new PlatformCommunityPost
        {
            AuthorUserId = CommunityAutomatedPostPublication.BuildSystemAuthorKey(sourceKey, "item-1")
        };

        Assert.Equal(
            PlatformCommunitySystemPostKinds.PrajnaContent,
            CommunityAutomatedPostPublication.GetSystemPostKind(post));
        Assert.Contains(
            "제휴를 뜻하지 않습니다",
            CommunityAutomatedPostPublication.GetPrivacyNotice(PlatformCommunitySystemPostKinds.PrajnaContent));
    }

    [Fact]
    public void KamisBody_IncludesSourceDateUnitAndComparisonBoundary()
    {
        var body = CommunityKamisPriceBriefSource.BuildBody(
            new DateOnly(2026, 7, 16),
            [
                new KamisPriceBriefItem(
                    "소매",
                    "채소류",
                    "배추",
                    "여름배추",
                    "상품",
                    "1포기",
                    4_000m,
                    3_800m)
            ]);

        Assert.Contains("2026-07-16", body);
        Assert.Contains("4,000원/1포기", body);
        Assert.Contains("전일 대비 +200원", body);
        Assert.Contains("출처: KAMIS", body);
        Assert.Contains("전체 시장 평균이나 판매 권고가 아니라", body);
    }

    [Fact]
    public async Task Reflection_DoesNotPretendToBeARealPersonQuote()
    {
        var source = new CommunityReflectionSource();

        var draft = await source.BuildAsync(
            new DateOnly(2026, 7, 17),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));

        Assert.NotNull(draft);
        Assert.Equal(CommunityBoardCatalog.FreeLife.DisplayName, draft.Category);
        Assert.Contains("[자동 성찰]", draft.Title);
        Assert.Contains("특정 사상가의 실제 인용문이 아니며", draft.Body);
    }

    [Fact]
    public async Task ActivityDigest_CountsOnlyPrivacySafeLedgerCompletionPosts()
    {
        await using var context = CreateContext();
        context.PlatformCommunityPosts.AddRange(
            CompletionPost("공동구매", "완료한 사람 이름", new DateTime(2026, 7, 16, 1, 0, 0, DateTimeKind.Utc)),
            CompletionPost("공동구매", "다른 완료 기록", new DateTime(2026, 7, 16, 2, 0, 0, DateTimeKind.Utc)),
            new PlatformCommunityPost
            {
                AppKey = "platform",
                Category = "자유·생활",
                WorkflowTag = "공동구매",
                RoleTag = "구매자",
                Title = "사용자 글",
                Body = "집계하면 안 됩니다.",
                AuthorUserId = "user-1",
                Nickname = "사용자",
                PasswordHash = "hash",
                CreatedAtUtc = new DateTime(2026, 7, 16, 3, 0, 0, DateTimeKind.Utc)
            });
        await context.SaveChangesAsync();
        var source = new CommunityActivityDigestSource(context);

        var draft = await source.BuildAsync(
            new DateOnly(2026, 7, 17),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));

        Assert.NotNull(draft);
        Assert.Equal(CommunityBoardCatalog.CompletionReview.DisplayName, draft.Category);
        Assert.Contains("공개 완료 기록: 2건", draft.Body);
        Assert.Contains("공동구매: 2건", draft.Body);
        Assert.DoesNotContain("완료한 사람 이름", draft.Body);
        Assert.DoesNotContain("사용자 글", draft.Body);
        Assert.Contains("거래액이나 매출", draft.Body);
    }

    private static PlatformCommunityPost CompletionPost(
        string workflowTag,
        string title,
        DateTime createdAtUtc)
        => new()
        {
            AppKey = "platform",
            Category = CommunityBoardCatalog.CompletionReview.DisplayName,
            WorkflowTag = workflowTag,
            RoleTag = "시스템 기록",
            Title = title,
            Body = "비식별 완료 기록",
            AuthorUserId = CommunityLedgerCompletionPublication.SystemAuthorKey,
            Nickname = "홍달 시스템",
            PasswordHash = "hash",
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"community-automated-post-{Guid.NewGuid():N}")
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Notifications { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
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
