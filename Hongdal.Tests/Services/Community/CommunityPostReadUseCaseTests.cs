using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Community;
using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityPostReadUseCaseTests
{
    [Fact]
    public async Task 목록은_보호_게시글을_제외하고_페이지_범위를_보정한다()
    {
        await using var db = CreateContext();
        db.PlatformCommunityPosts.AddRange(
            CreatePost("공개 글", CommunityBoardCatalog.FreeLife.DisplayName),
            CreatePost("신고 글", CommunityBoardCatalog.SafetyReport.DisplayName, isReport: true),
            CreatePost("다른 앱 글", CommunityBoardCatalog.FreeLife.DisplayName, appKey: "another-app"));
        await db.SaveChangesAsync();
        var useCase = CreateUseCase(db);

        var result = await useCase.목록Async(
            "platform",
            null,
            CommunityBoardKeys.FreeLife,
            null,
            null,
            0,
            100,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(50, result.Value.PageSize);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("공개 글", item.Title);
        Assert.False(item.IsReportBoardPost);
    }

    [Fact]
    public async Task 승인된_사용자_게시판은_요약과_게시글_수를_제공한다()
    {
        await using var db = CreateContext();
        db.PlatformCommunityBoardRequests.Add(new PlatformCommunityBoardRequest
        {
            AppKey = "platform",
            BoardKey = "local-tools",
            Title = "동네 도구",
            Description = "함께 쓰는 도구 정보",
            RequestedByUserId = "user-1",
            RequestedBy = "사용자",
            RequestReason = "정보 공유",
            Status = PlatformCommunityBoardRequestStatuses.Approved,
            ApprovedAtUtc = DateTime.UtcNow
        });
        db.PlatformCommunityPosts.Add(CreatePost("공구함 정보", "동네 도구"));
        await db.SaveChangesAsync();
        var useCase = CreateUseCase(db);

        var result = await useCase.게시판요약목록Async("platform", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var customBoard = Assert.Single(result.Value, board => board.BoardKey == "local-tools");
        Assert.True(customBoard.IsCustom);
        Assert.Equal(1, customBoard.PostCount);
        Assert.False(customBoard.AllowsAnonymousPosting);
    }

    [Fact]
    public async Task 없는_상세_게시글은_404_의미를_반환한다()
    {
        await using var db = CreateContext();
        var result = await CreateUseCase(db).상세Async(999, CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(404, result.Errors.Single().Metadata["StatusCode"]);
    }

    private static 커뮤니티게시글조회UseCase CreateUseCase(HongdalContext db)
        => new(db, null!, new AnonymousUserAccessor());

    private static PlatformCommunityPost CreatePost(
        string title,
        string category,
        bool isReport = false,
        string appKey = "platform")
        => new()
        {
            AppKey = appKey,
            Category = category,
            WorkflowTag = "커뮤니티 신뢰",
            RoleTag = "구성원",
            Title = title,
            Body = "조회 테스트 본문",
            OriginalLanguageCode = "ko",
            Nickname = "작성자",
            PasswordHash = "hash",
            IsReportBoardPost = isReport,
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

    private sealed class AnonymousUserAccessor : ICurrentUserAccessor
    {
        public string? UserId => null;
        public string? Role => null;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
