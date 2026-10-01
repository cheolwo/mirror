using Hongdal.Application.Community;
using Hongdal.Domain.Community;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

public interface ICommunityScheduledPostPublicationProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}

public sealed class CommunityScheduledPostPublicationProcessor : ICommunityScheduledPostPublicationProcessor
{
    private readonly HongdalContext _db;
    private readonly I커뮤니티게시글음성작업예약Service _audioQueue;
    private readonly ICommunityKeywordNotificationQueue _keywordQueue;
    private readonly IPublisher _publisher;
    private readonly IOptionsMonitor<CommunityPostPublicationOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CommunityScheduledPostPublicationProcessor> _logger;

    public CommunityScheduledPostPublicationProcessor(
        HongdalContext db,
        I커뮤니티게시글음성작업예약Service audioQueue,
        ICommunityKeywordNotificationQueue keywordQueue,
        IPublisher publisher,
        IOptionsMonitor<CommunityPostPublicationOptions> options,
        TimeProvider timeProvider,
        ILogger<CommunityScheduledPostPublicationProcessor> logger)
    {
        _db = db;
        _audioQueue = audioQueue;
        _keywordQueue = keywordQueue;
        _publisher = publisher;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var staleClaim = now.AddMinutes(-Math.Max(1, options.LeaseTimeoutMinutes));
        var candidateId = await _db.PlatformCommunityPosts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(post => !post.IsDeleted
                           && ((post.PublicationStatusCode == PlatformCommunityPostPublicationStatusCodes.Scheduled
                                && post.PublicationAttemptCount < Math.Max(1, options.MaxAttempts)
                                 && post.PublicationNextAttemptAtUtc <= now)
                               || (post.PublicationStatusCode == PlatformCommunityPostPublicationStatusCodes.Publishing
                                   && post.PublicationClaimedAtUtc <= staleClaim)))
            .OrderBy(post => post.PublicationNextAttemptAtUtc)
            .Select(post => (long?)post.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!candidateId.HasValue)
        {
            return false;
        }

        var claimed = await _db.PlatformCommunityPosts
            .IgnoreQueryFilters()
            .Where(post => post.Id == candidateId.Value
                           && !post.IsDeleted
                           && ((post.PublicationStatusCode == PlatformCommunityPostPublicationStatusCodes.Scheduled
                                && post.PublicationAttemptCount < Math.Max(1, options.MaxAttempts)
                                 && post.PublicationNextAttemptAtUtc <= now)
                               || (post.PublicationStatusCode == PlatformCommunityPostPublicationStatusCodes.Publishing
                                   && post.PublicationClaimedAtUtc <= staleClaim)))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        post => post.PublicationStatusCode,
                        PlatformCommunityPostPublicationStatusCodes.Publishing)
                    .SetProperty(post => post.PublicationClaimedAtUtc, now)
                    .SetProperty(post => post.PublicationAttemptCount, post => post.PublicationAttemptCount + 1)
                    .SetProperty(post => post.UpdatedAtUtc, now),
                cancellationToken);
        if (claimed == 0)
        {
            return true;
        }

        try
        {
            var post = await _db.PlatformCommunityPosts
                .IgnoreQueryFilters()
                .Include(item => item.Audio)
                .Include(item => item.KeywordNotificationScan)
                .SingleAsync(item => item.Id == candidateId.Value, cancellationToken);
            _audioQueue.예약(post, now);
            _keywordQueue.Enqueue(post, now);
            post.PublicationStatusCode = PlatformCommunityPostPublicationStatusCodes.Published;
            post.PublishedAtUtc = now;
            post.PublicationNextAttemptAtUtc = null;
            post.PublicationClaimedAtUtc = null;
            post.PublicationLastError = null;
            post.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);

            try
            {
                await _publisher.Publish(new 커뮤니티게시글등록됨Event(post.Id), cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    exception,
                    "예약 게시글 공개 후속 이벤트 발행에 실패했습니다. PostId={PostId}",
                    post.Id);
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _db.ChangeTracker.Clear();
            var attemptCount = await _db.PlatformCommunityPosts
                .IgnoreQueryFilters()
                .Where(post => post.Id == candidateId.Value)
                .Select(post => post.PublicationAttemptCount)
                .SingleAsync(cancellationToken);
            var terminal = attemptCount >= Math.Max(1, options.MaxAttempts);
            var retryAt = now.AddSeconds(Math.Max(5, options.RetryDelaySeconds) * Math.Max(1, attemptCount));
            await _db.PlatformCommunityPosts
                .IgnoreQueryFilters()
                .Where(post => post.Id == candidateId.Value
                               && post.PublicationStatusCode == PlatformCommunityPostPublicationStatusCodes.Publishing)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            post => post.PublicationStatusCode,
                            terminal
                                ? PlatformCommunityPostPublicationStatusCodes.Failed
                                : PlatformCommunityPostPublicationStatusCodes.Scheduled)
                        .SetProperty(
                            post => post.PublicationNextAttemptAtUtc,
                            terminal ? (DateTime?)null : retryAt)
                        .SetProperty(post => post.PublicationClaimedAtUtc, (DateTime?)null)
                        .SetProperty(post => post.PublicationLastError, Limit(exception.Message, 1000))
                        .SetProperty(post => post.UpdatedAtUtc, now),
                    cancellationToken);
            _logger.LogWarning(
                exception,
                "예약 게시글 발행에 실패하여 재시도 상태를 저장했습니다. PostId={PostId}, Attempt={Attempt}",
                candidateId.Value,
                attemptCount);
            return true;
        }
    }

    private static string Limit(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

public sealed class CommunityScheduledPostPublicationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<CommunityPostPublicationOptions> _options;
    private readonly ILogger<CommunityScheduledPostPublicationWorker> _logger;

    public CommunityScheduledPostPublicationWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<CommunityPostPublicationOptions> options,
        ILogger<CommunityScheduledPostPublicationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            var processed = 0;
            if (options.Enabled)
            {
                try
                {
                    for (; processed < Math.Clamp(options.BatchSize, 1, 100); processed++)
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var processor = scope.ServiceProvider
                            .GetRequiredService<ICommunityScheduledPostPublicationProcessor>();
                        if (!await processor.ProcessNextAsync(stoppingToken))
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "예약 게시글 발행 Worker에서 예외가 발생했습니다.");
                }
            }

            if (processed < Math.Clamp(options.BatchSize, 1, 100))
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(5, options.PollingIntervalSeconds)),
                    stoppingToken);
            }
        }
    }
}
