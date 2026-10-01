using Hongdal.Contracts.Common.Community;
using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityPostModerationUseCaseTests
{
    [Fact]
    public async Task 운영자_고정은_게시글_상태와_응답을_함께_갱신한다()
    {
        await using var db = CreateContext();
        var post = CreatePost();
        db.PlatformCommunityPosts.Add(post);
        await db.SaveChangesAsync();
        var useCase = new 커뮤니티게시글운영UseCase(db);

        var result = await useCase.운영자고정Async(
            post.Id,
            new PlatformCommunityPostOperatorPinRequest { IsOperatorPinned = true },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsOperatorPinned);
        Assert.NotNull(result.Value.OperatorPinnedAtUtc);
        Assert.True((await db.PlatformCommunityPosts.FindAsync(post.Id))!.IsOperatorPinned);
    }

    [Fact]
    public async Task 댓글과_첨부댓글의_신고와_숨김은_각_대상만_변경한다()
    {
        await using var db = CreateContext();
        var post = CreatePost();
        var comment = new PlatformCommunityPostComment
        {
            Post = post,
            Nickname = "참여자",
            Body = "일반 댓글",
            PasswordHash = "hash"
        };
        var attachment = new PlatformCommunityPostAttachment
        {
            Post = post,
            BucketName = "bucket",
            ObjectName = "object",
            Url = "https://storage.test/object",
            OriginalFileName = "sample.png",
            ContentType = "image/png",
            FileSizeBytes = 1
        };
        var attachmentComment = new PlatformCommunityPostAttachmentComment
        {
            Attachment = attachment,
            Nickname = "참여자",
            Body = "첨부 댓글",
            PasswordHash = "hash"
        };
        db.AddRange(post, comment, attachment, attachmentComment);
        await db.SaveChangesAsync();
        var useCase = new 커뮤니티게시글운영UseCase(db);

        Assert.True((await useCase.댓글신고Async(comment.Id, CancellationToken.None)).IsSuccess);
        Assert.True((await useCase.댓글운영자숨김Async(
            comment.Id,
            new PlatformCommunityOperatorHiddenRequest { IsOperatorHidden = true },
            CancellationToken.None)).IsSuccess);
        Assert.True((await useCase.첨부댓글신고Async(
            attachmentComment.Id,
            CancellationToken.None)).IsSuccess);
        Assert.True((await useCase.첨부댓글운영자숨김Async(
            attachmentComment.Id,
            new PlatformCommunityOperatorHiddenRequest { IsOperatorHidden = true },
            CancellationToken.None)).IsSuccess);

        Assert.Equal(1, comment.ReportCount);
        Assert.True(comment.IsOperatorHidden);
        Assert.Equal(1, attachmentComment.ReportCount);
        Assert.True(attachmentComment.IsOperatorHidden);
    }

    private static PlatformCommunityPost CreatePost()
        => new()
        {
            AppKey = "platform",
            Category = "정보",
            WorkflowTag = "커뮤니티 신뢰",
            RoleTag = "구성원",
            Title = "운영 테스트",
            Body = "운영 상태 변경을 검증합니다.",
            OriginalLanguageCode = "ko",
            Nickname = "테스터",
            PasswordHash = "hash",
            PublicationStatusCode = PlatformCommunityPostPublicationStatusCodes.Published,
            PublishedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
