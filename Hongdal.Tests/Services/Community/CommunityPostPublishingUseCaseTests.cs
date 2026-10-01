using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Community;
using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Services.Community;

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

    [Fact]
    public async Task 지원하지_않는_예약_상태_필터는_저장소를_조회하지_않고_거절한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var useCase = new 커뮤니티게시글예약발행UseCase(null!, database.Context);

        var result = await useCase.예약목록Async("unknown", 50, CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains("지원하지 않는", result.Errors.Single().Message, StringComparison.Ordinal);
    }

    private static 커뮤니티게시글생성Service CreateCreationService(
        HongdalContext db,
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
        private TestDatabase(SqliteConnection connection, HongdalContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }
        public HongdalContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<HongdalContext>()
                .UseSqlite(connection)
                .Options;
            var context = new HongdalContext(options, new DummyPersonalDataEncryptionService());
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
