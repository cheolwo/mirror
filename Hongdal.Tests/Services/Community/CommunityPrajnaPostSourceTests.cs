using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.Community;
using Hongdal.Domain.Content;
using Hongdal.Services.Community;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityPrajnaPostSourceTests
{
    private const string HongikChannelId = "hongik-channel";

    [Fact]
    public async Task 카드내부검토상태와별도로_반야게시승인된카드만선택한다()
    {
        await using var context = CreateContext();
        AddCard(context, approved: false, "미승인 카드");
        var approved = AddCard(context, approved: true, "승인 카드");
        await context.SaveChangesAsync();
        var source = CreateSource(context);

        var draft = await source.BuildAsync(
            new DateOnly(2026, 7, 18),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));

        Assert.NotNull(draft);
        Assert.Equal(CommunityAutomatedPostSourceKeys.PrajnaCard, draft.SourceKey);
        Assert.Equal(approved.Id.ToString(), draft.PeriodKey);
        Assert.Equal(CommunityBoardCatalog.Prajna.DisplayName, draft.Category);
        Assert.Contains("승인 카드", draft.Title);
        Assert.DoesNotContain("미승인 카드", draft.Body);
        Assert.Contains("협력기관이 아닙니다", draft.Body);
    }

    [Fact]
    public async Task 직전카드게시후에는_공개승인된영상한건을선택하고_중복을피한다()
    {
        await using var context = CreateContext();
        var card = AddCard(context, approved: true, "먼저 게시할 카드");
        AddVideo(context, "hidden-video", YouTube채널영상.숨김상태, "숨김 영상");
        AddVideo(context, "blocked-channel-video", YouTube채널영상.공개상태, "채널 미승인 영상", "other-channel", false);
        AddVideo(context, "approved-video", YouTube채널영상.공개상태, "승인 영상");
        await context.SaveChangesAsync();
        context.PlatformCommunityPosts.Add(SystemPost(
            CommunityAutomatedPostSourceKeys.PrajnaCard,
            card.Id.ToString(),
            new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
        var source = CreateSource(context);

        var videoDraft = await source.BuildAsync(
            new DateOnly(2026, 7, 18),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));

        Assert.NotNull(videoDraft);
        Assert.Equal(CommunityAutomatedPostSourceKeys.PrajnaVideo, videoDraft.SourceKey);
        Assert.Equal("approved-video", videoDraft.PeriodKey);
        Assert.Contains("승인 영상", videoDraft.Title);
        Assert.Equal("https://www.youtube.com/watch?v=approved-video", videoDraft.SharedLinkUrl);

        context.PlatformCommunityPosts.Add(SystemPost(
            CommunityAutomatedPostSourceKeys.PrajnaVideo,
            videoDraft.PeriodKey,
            new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();

        Assert.Null(await source.BuildAsync(
            new DateOnly(2026, 7, 18),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul")));
    }

    private static HongikHakdangCard AddCard(
        HongdalContext context,
        bool approved,
        string title)
    {
        var card = new HongikHakdangCard
        {
            SourceKey = Guid.NewGuid().ToString("N"),
            Title = title,
            Description = "짧은 카드 소개",
            OriginalImageUrl = "https://example.test/card.jpg",
            RelatedUrl = "https://example.test/card",
            IsActive = true,
            IsAdminEnabled = true,
            IsCommunityPublicationApproved = approved,
            LastSeenAtUtc = DateTime.UtcNow
        };
        var collection = new HongikHakdangCardCollection
        {
            SourceKey = Guid.NewGuid().ToString("N"),
            Name = "테스트 묶음",
            IsActive = true,
            IsAdminEnabled = true,
            LastSeenAtUtc = DateTime.UtcNow
        };
        var item = new HongikHakdangCardCollectionItem
        {
            Card = card,
            Collection = collection,
            IsActive = true,
            LastSeenAtUtc = DateTime.UtcNow
        };
        card.Collections.Add(item);
        collection.Items.Add(item);
        context.Add(collection);
        return card;
    }

    private static void AddVideo(
        HongdalContext context,
        string videoId,
        string publicationState,
        string title,
        string channelId = HongikChannelId,
        bool prajnaAllowed = true)
    {
        var channel = context.YouTube감시채널.Local
            .FirstOrDefault(existing => existing.ChannelId == channelId);
        if (channel is null)
        {
            channel = new YouTube감시채널
            {
                ChannelId = channelId,
                채널명 = channelId == HongikChannelId ? "홍익학당" : "다른 성찰 채널",
                UploadsPlaylistId = $"uploads-{channelId}",
                활성화여부 = true,
                지식성찰채널여부 = true,
                반야게시허용여부 = prajnaAllowed,
                지식성찰분류 = YouTube지식성찰주제코드.철학,
                관점표시 = "홍익·양심 공부"
            };
            context.Add(channel);
        }

        channel.영상.Add(new YouTube채널영상
        {
            감시채널 = channel,
            ChannelId = channelId,
            VideoId = videoId,
            제목 = title,
            설명 = "짧은 영상 소개",
            게시일시Utc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            공유상태 = publicationState
        });
    }

    private static PlatformCommunityPost SystemPost(
        string sourceKey,
        string periodKey,
        DateTime createdAtUtc)
        => new()
        {
            AppKey = "platform",
            Category = CommunityBoardCatalog.Prajna.DisplayName,
            WorkflowTag = "배움·성찰",
            RoleTag = "관리자 선별 콘텐츠",
            Title = "반야 게시글",
            Body = "출처 안내",
            AuthorUserId = CommunityAutomatedPostPublication.BuildSystemAuthorKey(sourceKey, periodKey),
            Nickname = "홍달 반야지기",
            PasswordHash = "hash",
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };

    private static CommunityPrajnaPostSource CreateSource(HongdalContext context)
        => new(context);

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"community-prajna-source-{Guid.NewGuid():N}")
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
