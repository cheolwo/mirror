using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Services.Community;

public sealed class CommunityPostPublishingUseCaseTests
{
    [Fact]
    public async Task 즉시_발행은_음성과_키워드_복구큐와_등록_Event를_함께_기록한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingPublisher();
        var creationService = CreateCreationService(database.Context, publisher);

        var result = await creationService.CreateAsync(
            CreateRequest("즉시 발행"),
            null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlatformCommunityPostPublicationStatusCodes.Published, result.Value.PublicationStatusCode);
        var post = await database.Context.PlatformCommunityPosts
            .Include(item => item.Audio)
            .Include(item => item.KeywordNotificationScan)
            .SingleAsync(item => item.Id == result.Value.Id);
        Assert.NotNull(post.Audio);
        Assert.NotNull(post.KeywordNotificationScan);
        Assert.Single(publisher.Notifications);
    }

    [Fact]
    public async Task 예약_발행은_후속큐를_만들지_않고_예약_상태로_저장한_뒤_취소할_수_있다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingPublisher();
        var creationService = CreateCreationService(database.Context, publisher);
        var useCase = new 커뮤니티게시글예약발행UseCase(creationService, database.Context);

        var scheduled = await useCase.예약Async(
            new PlatformCommunityPostScheduleCreateRequest
            {
                Post = CreateRequest("예약 발행"),
                ScheduledPublishAtUtc = DateTime.UtcNow.AddMinutes(10)
            },
            CancellationToken.None);

        Assert.True(scheduled.IsSuccess);
        Assert.Equal(
            PlatformCommunityPostPublicationStatusCodes.Scheduled,
            scheduled.Value.PublicationStatusCode);
        Assert.Empty(publisher.Notifications);
        var stored = await database.Context.PlatformCommunityPosts
            .IgnoreQueryFilters()
            .Include(item => item.Audio)
            .Include(item => item.KeywordNotificationScan)
            .SingleAsync(item => item.Id == scheduled.Value.Id);
        Assert.Null(stored.Audio);
        Assert.Null(stored.KeywordNotificationScan);

        var cancelled = await useCase.예약취소Async(stored.Id, CancellationToken.None);

        Assert.True(cancelled.IsSuccess);
        Assert.Equal(
            PlatformCommunityPostPublicationStatusCodes.Cancelled,
            cancelled.Value.PublicationStatusCode);
    }

    [Theory]
    [InlineData("모집·함께하기", true)]
    [InlineData("서원", false)]
    public async Task 마음모으기_선택은_공동구매모집글에만_저장한다(
        string category,
        bool expected)
    {
        await using var database = await TestDatabase.CreateAsync();
        var request = CreateRequest("마음 모으기 정책");
        request.Category = category;
        request.Nickname = "작성자";
        request.IsInterestGatheringEnabled = true;

        var result = await CreateCreationService(database.Context, new RecordingPublisher())
            .CreateAsync(request, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.IsInterestGatheringEnabled);
        var stored = await database.Context.PlatformCommunityPosts.SingleAsync(post => post.Id == result.Value.Id);
        Assert.Equal(expected, stored.IsInterestGatheringEnabled);
    }

    [Fact]
    public async Task 지원하지_않는_예약_상태_필터는_저장소를_조회하지_않고_거절한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var useCase = new 커뮤니티게시글예약발행UseCase(null!, database.Context);

        var result = await useCase.예약목록Async("unknown", 50, CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains("지원하지 않는", result.Errors.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 로그인한_원작성자는_비밀번호없이_자신의_글을_삭제한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var post = CreateStoredPost("author-1");
        database.Context.PlatformCommunityPosts.Add(post);
        await database.Context.SaveChangesAsync();
        var useCase = CreatePublishingUseCase(
            database.Context,
            new TestUserAccessor("author-1", 역할명.커뮤니티회원));

        var result = await useCase.삭제Async(
            post.Id,
            new PlatformCommunityPostPasswordRequest(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(post.IsDeleted);
    }

    [Fact]
    public async Task 서버관리자는_다른_작성자의_글을_비밀번호없이_삭제한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var post = CreateStoredPost("author-1");
        database.Context.PlatformCommunityPosts.Add(post);
        await database.Context.SaveChangesAsync();
        var useCase = CreatePublishingUseCase(
            database.Context,
            new TestUserAccessor("admin-1", 역할명.서버관리자));

        var result = await useCase.삭제Async(
            post.Id,
            new PlatformCommunityPostPasswordRequest(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(post.IsDeleted);
    }

    [Fact]
    public async Task 로그인한_비작성자는_글비밀번호를_알아도_등록회원_글을_삭제하거나_수정할수없다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var post = CreateStoredPost("author-1");
        database.Context.PlatformCommunityPosts.Add(post);
        await database.Context.SaveChangesAsync();
        var useCase = CreatePublishingUseCase(
            database.Context,
            new TestUserAccessor("other-user", 역할명.커뮤니티회원));

        var deleteResult = await useCase.삭제Async(
            post.Id,
            new PlatformCommunityPostPasswordRequest { Password = "post-password" },
            CancellationToken.None);
        var updateResult = await useCase.수정Async(
            post.Id,
            CreateUpdateRequest("다른 사용자의 수정"),
            CancellationToken.None);

        Assert.True(deleteResult.IsFailed);
        Assert.Equal(403, deleteResult.Errors.Single().Metadata["StatusCode"]);
        Assert.True(updateResult.IsFailed);
        Assert.Equal(403, updateResult.Errors.Single().Metadata["StatusCode"]);
        Assert.False(post.IsDeleted);
        Assert.NotEqual("다른 사용자의 수정", post.Title);
    }

    [Fact]
    public async Task 익명_글은_작성할때_입력한_비밀번호가_맞아야_수정하고_삭제한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var post = CreateStoredPost(authorUserId: null);
        database.Context.PlatformCommunityPosts.Add(post);
        await database.Context.SaveChangesAsync();
        var useCase = CreatePublishingUseCase(database.Context, new AnonymousUserAccessor());

        var wrongDelete = await useCase.삭제Async(
            post.Id,
            new PlatformCommunityPostPasswordRequest { Password = "wrong-password" },
            CancellationToken.None);
        var updateResult = await useCase.수정Async(
            post.Id,
            CreateUpdateRequest("익명 작성자 수정"),
            CancellationToken.None);
        var deleteResult = await useCase.삭제Async(
            post.Id,
            new PlatformCommunityPostPasswordRequest { Password = "post-password" },
            CancellationToken.None);

        Assert.True(wrongDelete.IsFailed);
        Assert.Equal(403, wrongDelete.Errors.Single().Metadata["StatusCode"]);
        Assert.True(updateResult.IsSuccess);
        Assert.Equal("익명 작성자 수정", post.Title);
        Assert.True(deleteResult.IsSuccess);
        Assert.True(post.IsDeleted);
    }

    [Fact]
    public async Task 생활교류_익명글과_공개문의는_기존_DB에_저장되며_비밀번호로_철회한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var request = CreateRequest("이번 주말 동네 배송을 도와드려요");
        request.WorkflowTag = NeighborhoodExchange.WorkflowTag;
        request.RoleTag = NeighborhoodExchange.Offer;
        var created = await CreateCreationService(database.Context, new RecordingPublisher())
            .CreateAsync(request, null, CancellationToken.None);
        Assert.True(created.IsSuccess);
        var id = created.Value.Id;
        database.Context.ChangeTracker.Clear();
        var stored = await database.Context.PlatformCommunityPosts.SingleAsync(p => p.Id == id);
        Assert.Equal(NeighborhoodExchange.WorkflowTag, stored.WorkflowTag);
        Assert.Null(stored.AuthorUserId);
        Assert.Null(stored.커뮤니티원장Id);
        Assert.Null(stored.SalesOfferJson);

        var participation = new 커뮤니티게시글참여UseCase(database.Context, new AnonymousUserAccessor());
        var comment = await participation.댓글작성Async(id, new()
        { Body = "토요일 오전에도 가능한가요?", Nickname = "이웃", Password = "comment-password" }, CancellationToken.None);
        Assert.True(comment.IsSuccess);
        database.Context.ChangeTracker.Clear();
        var comments = await participation.댓글목록Async(id, CancellationToken.None);
        Assert.True(comments.IsSuccess);
        Assert.Equal(comment.Value.Id, Assert.Single(comments.Value).Id);
        Assert.True((await participation.댓글삭제Async(id, comment.Value.Id, new()
        { Password = "comment-password" }, CancellationToken.None)).IsSuccess);

        var publisher = CreatePublishingUseCase(database.Context, new AnonymousUserAccessor());
        Assert.True((await publisher.삭제Async(id, new()
        { Password = request.Password }, CancellationToken.None)).IsSuccess);
        database.Context.ChangeTracker.Clear();
        Assert.True((await database.Context.PlatformCommunityPosts.SingleAsync(p => p.Id == id)).IsDeleted);
        var reader = new 커뮤니티게시글조회UseCase(database.Context, new EmptyLedgerDisplayService(), new AnonymousUserAccessor());
        Assert.True((await reader.상세Async(id, CancellationToken.None)).IsFailed);
        var publicList = await reader.목록Async("platform", PlatformCommunityPostCategories.General, null,
            NeighborhoodExchange.WorkflowTag, null, 1, 20, CancellationToken.None);
        Assert.DoesNotContain(publicList.Value.Items, p => p.Id == id);
    }

    private static 커뮤니티게시글생성Service CreateCreationService(
        SsalddelContext db,
        IPublisher publisher)
        => new(
            db,
            new 커뮤니티게시글음성작업예약Service(),
            new CommunityKeywordNotificationQueue(),
            null!,
            null!,
            new AllowAllBoardWritePolicy(),
            new AnonymousUserAccessor(),
            publisher,
            NullLogger<커뮤니티게시글생성Service>.Instance);

    private static 커뮤니티게시글발행UseCase CreatePublishingUseCase(
        SsalddelContext db,
        ICurrentUserAccessor currentUserAccessor)
        => new(
            null!,
            db,
            null!,
            new EmptyLedgerDisplayService(),
            new AllowAllBoardWritePolicy(),
            currentUserAccessor);

    private static PlatformCommunityPostCreateRequest CreateRequest(string title)
        => new()
        {
            AppKey = "platform",
            Category = CommunityBoardCatalog.FreeLife.DisplayName,
            WorkflowTag = "커뮤니티 신뢰",
            RoleTag = "구성원",
            Title = title,
            Body = "발행 파이프라인을 검증합니다.",
            Nickname = string.Empty,
            Password = "post-password"
        };

    private static PlatformCommunityPostUpdateRequest CreateUpdateRequest(string title)
        => new()
        {
            Category = CommunityBoardCatalog.FreeLife.DisplayName,
            WorkflowTag = "커뮤니티 신뢰",
            RoleTag = "구성원",
            Title = title,
            Body = "수정된 본문입니다.",
            Nickname = "작성자",
            Password = "post-password"
        };

    private static PlatformCommunityPost CreateStoredPost(string? authorUserId)
        => new()
        {
            AppKey = "platform",
            Category = CommunityBoardCatalog.FreeLife.DisplayName,
            WorkflowTag = "커뮤니티 신뢰",
            RoleTag = "구성원",
            Title = "권한 테스트 글",
            Body = "권한을 검증합니다.",
            OriginalLanguageCode = "ko",
            AuthorUserId = authorUserId,
            Nickname = "작성자",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("post-password"),
            PublicationStatusCode = PlatformCommunityPostPublicationStatusCodes.Published,
            PublishedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private sealed class AllowAllBoardWritePolicy : ICommunityBoardWritePolicy
    {
        public Task<bool> CanWriteAsync(
            string? appKey,
            string? category,
            string? userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class AnonymousUserAccessor : ICurrentUserAccessor
    {
        public string? UserId => null;
        public string? Role => null;
    }

    private sealed record TestUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;

    private sealed class EmptyLedgerDisplayService : I게시글원장표시ContextService
    {
        public Task<PlatformCommunityPostLedgerContextResponse?> 조회Async(
            string? 원장Id,
            string? 사용자UserId,
            CancellationToken cancellationToken)
            => Task.FromResult<PlatformCommunityPostLedgerContextResponse?>(null);

        public Task<PlatformCommunityPostLedgerContextResponse?> 비식별성립사례조회Async(
            string? 원장Id,
            CancellationToken cancellationToken)
            => Task.FromResult<PlatformCommunityPostLedgerContextResponse?>(null);
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

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, SsalddelContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }
        public SsalddelContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SsalddelContext>()
                .UseSqlite(connection)
                .Options;
            var context = new SsalddelContext(options, new DummyPersonalDataEncryptionService());
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
