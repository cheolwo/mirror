using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Services.Community;

public sealed class CommunityPostReadUseCaseTests
{
    [Fact]
    public async Task 목록은_보호_게시글을_제외하고_페이지_범위를_보정한다()
    {
        await using var db = CreateContext();
        var publicPost = CreatePost("공개 글", CommunityBoardCatalog.FreeLife.DisplayName);
        publicPost.ViewCount = 12;
        db.PlatformCommunityPosts.AddRange(
            publicPost,
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
        Assert.Equal(12, item.ViewCount);
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

    [Fact]
    public async Task 목록은_익명_원작성자_관리자와_다른회원의_권한을_구분한다()
    {
        await using var db = CreateContext();
        var anonymousPost = CreatePost("익명 글", CommunityBoardCatalog.FreeLife.DisplayName);
        var memberPost = CreatePost("회원 글", CommunityBoardCatalog.FreeLife.DisplayName);
        memberPost.AuthorUserId = "author-1";
        db.PlatformCommunityPosts.AddRange(anonymousPost, memberPost);
        await db.SaveChangesAsync();

        var anonymousView = await ListAsync(db, new AnonymousUserAccessor());
        var ownerView = await ListAsync(db, new TestUserAccessor("author-1", 역할명.커뮤니티회원));
        var administratorView = await ListAsync(db, new TestUserAccessor("admin-1", 역할명.서버관리자));
        var otherMemberView = await ListAsync(db, new TestUserAccessor("other-1", 역할명.커뮤니티회원));

        var anonymousItem = anonymousView.Single(item => item.Id == anonymousPost.Id);
        Assert.True(anonymousItem.CanEdit);
        Assert.True(anonymousItem.EditRequiresPassword);
        Assert.True(anonymousItem.CanDelete);
        Assert.True(anonymousItem.DeleteRequiresPassword);

        var ownerItem = ownerView.Single(item => item.Id == memberPost.Id);
        Assert.True(ownerItem.CanEdit);
        Assert.True(ownerItem.EditRequiresPassword);
        Assert.True(ownerItem.CanDelete);
        Assert.False(ownerItem.DeleteRequiresPassword);

        var administratorItem = administratorView.Single(item => item.Id == memberPost.Id);
        Assert.False(administratorItem.CanEdit);
        Assert.True(administratorItem.CanDelete);
        Assert.False(administratorItem.DeleteRequiresPassword);

        var otherMemberItem = otherMemberView.Single(item => item.Id == memberPost.Id);
        Assert.False(otherMemberItem.CanEdit);
        Assert.False(otherMemberItem.CanDelete);
    }

    [Fact]
    public async Task 목록은_주기성_서버글을_별도_주제로_포함하거나_제외한다()
    {
        await using var db = CreateContext();
        var general = CreatePost(
            "일반 업무 글",
            CommunityActivityBoardCatalog.FindBundle(
                CommunityActivityBoardKeys.FoundationEvidence)!.Board.DisplayName);
        var periodic = CreatePost(
            "정기 공공데이터",
            CommunityActivityBoardCatalog.FindBundle(
                CommunityActivityBoardKeys.FoundationEvidence)!.Board.DisplayName);
        periodic.AuthorUserId = CommunityAutomatedPostPublication.BuildSystemAuthorKey(
            "public-data-test",
            "2026-07-24");
        db.PlatformCommunityPosts.AddRange(general, periodic);
        await db.SaveChangesAsync();
        var useCase = CreateUseCase(db);

        var onlyPeriodic = await useCase.목록Async(
            "platform",
            null,
            CommunityActivityBoardKeys.FoundationEvidence,
            null,
            null,
            1,
            50,
            CancellationToken.None,
            CommunityPeriodicPostVisibilityModes.Only);
        var excludePeriodic = await useCase.목록Async(
            "platform",
            null,
            CommunityActivityBoardKeys.FoundationEvidence,
            null,
            null,
            1,
            50,
            CancellationToken.None,
            CommunityPeriodicPostVisibilityModes.Exclude);

        Assert.True(onlyPeriodic.IsSuccess);
        var periodicResponse = Assert.Single(onlyPeriodic.Value.Items);
        Assert.Equal("정기 공공데이터", periodicResponse.Title);
        Assert.True(periodicResponse.IsPeriodic);
        Assert.Equal(
            CommunityPostTopicClassificationCodes.Periodic,
            periodicResponse.TopicClassificationCode);
        Assert.Equal("주기성", periodicResponse.TopicClassificationName);

        Assert.True(excludePeriodic.IsSuccess);
        var generalResponse = Assert.Single(excludePeriodic.Value.Items);
        Assert.Equal("일반 업무 글", generalResponse.Title);
        Assert.False(generalResponse.IsPeriodic);
        Assert.Equal(
            CommunityPostTopicClassificationCodes.General,
            generalResponse.TopicClassificationCode);
    }

    [Fact]
    public async Task 생활교류_제공필터는_페이지_처리전_적용하고_다른글과_삭제글을_제외한다()
    {
        await using var db = CreateContext();
        foreach (var n in Enumerable.Range(1, 23))
        {
            var post = CreatePost($"제공 {n}", CommunityBoardCatalog.FreeLife.DisplayName);
            post.WorkflowTag = NeighborhoodExchange.WorkflowTag; post.RoleTag = NeighborhoodExchange.Offer;
            db.PlatformCommunityPosts.Add(post);
        }
        var need = CreatePost("필요 글", CommunityBoardCatalog.FreeLife.DisplayName);
        need.WorkflowTag = NeighborhoodExchange.WorkflowTag; need.RoleTag = NeighborhoodExchange.Need;
        var deleted = CreatePost("삭제 글", CommunityBoardCatalog.FreeLife.DisplayName);
        deleted.WorkflowTag = NeighborhoodExchange.WorkflowTag; deleted.RoleTag = NeighborhoodExchange.Offer; deleted.IsDeleted = true;
        db.PlatformCommunityPosts.AddRange(need, deleted, CreatePost("일반 글", CommunityBoardCatalog.FreeLife.DisplayName));
        await db.SaveChangesAsync();
        var page = await CreateUseCase(db).목록Async("platform", PlatformCommunityPostCategories.General,
            null, NeighborhoodExchange.WorkflowTag, NeighborhoodExchange.Offer, 2, NeighborhoodExchange.PageSize, CancellationToken.None);
        Assert.True(page.IsSuccess);
        Assert.Equal(23, page.Value.TotalCount);
        Assert.Equal(3, page.Value.Items.Count);
        Assert.All(page.Value.Items, p => Assert.True(NeighborhoodExchange.IsExchange(p)));
        Assert.All(page.Value.Items, p => Assert.Equal(NeighborhoodExchange.Offer, p.RoleTag));
    }

    private static 커뮤니티게시글조회UseCase CreateUseCase(
        SsalddelContext db,
        ICurrentUserAccessor? currentUserAccessor = null)
        => new(db, null!, currentUserAccessor ?? new AnonymousUserAccessor());

    private static async Task<IReadOnlyList<PlatformCommunityPostResponse>> ListAsync(
        SsalddelContext db,
        ICurrentUserAccessor currentUserAccessor)
    {
        var result = await CreateUseCase(db, currentUserAccessor).목록Async(
            "platform",
            null,
            CommunityBoardKeys.FreeLife,
            null,
            null,
            1,
            50,
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value.Items;
    }

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

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class AnonymousUserAccessor : ICurrentUserAccessor
    {
        public string? UserId => null;
        public string? Role => null;
    }

    private sealed record TestUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
