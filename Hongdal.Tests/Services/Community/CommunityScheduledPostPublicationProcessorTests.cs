using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Infrastructure.Security;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityScheduledPostPublicationProcessorTests
{
    [Fact]
    public async Task DuePost_IsHiddenUntilProcessorPublishesItOnce()
    {
        var now = new DateTimeOffset(2026, 7, 18, 3, 0, 0, TimeSpan.Zero);
        await using var database = await TestDatabase.CreateAsync();
        var post = ScheduledPost(now.UtcDateTime.AddMinutes(-1));
        database.Context.PlatformCommunityPosts.Add(post);
        await database.Context.SaveChangesAsync();

        Assert.False(await database.Context.PlatformCommunityPosts.AnyAsync(item => item.Id == post.Id));

        var publisher = new RecordingPublisher();
        var processor = CreateProcessor(database.Context, now, publisher);
        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
        Assert.False(await processor.ProcessNextAsync(CancellationToken.None));

        database.Context.ChangeTracker.Clear();
        var published = await database.Context.PlatformCommunityPosts
            .Include(item => item.Audio)
            .Include(item => item.KeywordNotificationScan)
            .SingleAsync(item => item.Id == post.Id);
        Assert.Equal(PlatformCommunityPostPublicationStatusCodes.Published, published.PublicationStatusCode);
        Assert.Equal(now.UtcDateTime, published.PublishedAtUtc);
        Assert.Equal(1, published.PublicationAttemptCount);
        Assert.NotNull(published.Audio);
        Assert.NotNull(published.KeywordNotificationScan);
        Assert.Single(publisher.Notifications);
    }

    [Fact]
    public async Task FutureAndCancelledPosts_AreNotPublished()
    {
        var now = new DateTimeOffset(2026, 7, 18, 3, 0, 0, TimeSpan.Zero);
        await using var database = await TestDatabase.CreateAsync();
        var future = ScheduledPost(now.UtcDateTime.AddHours(1));
        var cancelled = ScheduledPost(now.UtcDateTime.AddMinutes(-1));
        cancelled.PublicationStatusCode = PlatformCommunityPostPublicationStatusCodes.Cancelled;
        cancelled.PublicationNextAttemptAtUtc = null;
        database.Context.PlatformCommunityPosts.AddRange(future, cancelled);
        await database.Context.SaveChangesAsync();

        var publisher = new RecordingPublisher();
        var processor = CreateProcessor(database.Context, now, publisher);

        Assert.False(await processor.ProcessNextAsync(CancellationToken.None));
        Assert.Empty(publisher.Notifications);
        Assert.Empty(await database.Context.PlatformCommunityPosts.ToListAsync());
    }

    [Fact]
    public async Task StalePublishingLease_IsRecoveredAfterWorkerRestart()
    {
        var now = new DateTimeOffset(2026, 7, 18, 3, 0, 0, TimeSpan.Zero);
        await using var database = await TestDatabase.CreateAsync();
        var stale = ScheduledPost(now.UtcDateTime.AddMinutes(-10));
        stale.PublicationStatusCode = PlatformCommunityPostPublicationStatusCodes.Publishing;
        stale.PublicationAttemptCount = 5;
        stale.PublicationClaimedAtUtc = now.UtcDateTime.AddMinutes(-10);
        database.Context.PlatformCommunityPosts.Add(stale);
        await database.Context.SaveChangesAsync();

        var publisher = new RecordingPublisher();
        var processor = CreateProcessor(database.Context, now, publisher);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        database.Context.ChangeTracker.Clear();
        var published = await database.Context.PlatformCommunityPosts.SingleAsync(item => item.Id == stale.Id);
        Assert.Equal(PlatformCommunityPostPublicationStatusCodes.Published, published.PublicationStatusCode);
        Assert.Equal(6, published.PublicationAttemptCount);
        Assert.Single(publisher.Notifications);
    }

    private static CommunityScheduledPostPublicationProcessor CreateProcessor(
        HongdalContext context,
        DateTimeOffset now,
        IPublisher publisher)
        => new(
            context,
            new 커뮤니티게시글음성작업예약Service(),
            new CommunityKeywordNotificationQueue(),
            publisher,
            new StaticOptionsMonitor<CommunityPostPublicationOptions>(new CommunityPostPublicationOptions()),
            new FixedTimeProvider(now),
            NullLogger<CommunityScheduledPostPublicationProcessor>.Instance);

    private static PlatformCommunityPost ScheduledPost(DateTime scheduledAtUtc)
        => new()
        {
            AppKey = "platform",
            Category = "서원",
            WorkflowTag = "커뮤니티 신뢰",
            RoleTag = "운영자 서원 기록",
            Title = "예약 발행 테스트",
            Body = "정해 둔 시각이 되기 전에는 공개하지 않습니다.",
            OriginalLanguageCode = "ko",
            Nickname = "운영자",
            PasswordHash = "hash",
            PublicationStatusCode = PlatformCommunityPostPublicationStatusCodes.Scheduled,
            ScheduledPublishAtUtc = scheduledAtUtc,
            PublicationNextAttemptAtUtc = scheduledAtUtc,
            CreatedAtUtc = scheduledAtUtc.AddHours(-1),
            UpdatedAtUtc = scheduledAtUtc.AddHours(-1)
        };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, HongdalContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
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

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
