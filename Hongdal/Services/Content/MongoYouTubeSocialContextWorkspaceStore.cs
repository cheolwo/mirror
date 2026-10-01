using Hongdal.Contracts.Common.Content;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using 홍달.Services.Options;

namespace Hongdal.Services.Content;

public interface IYouTubeSocialContextWorkspaceStore
{
    Task<YouTubeSocialContextWorkspaceDto> SaveResearchAsync(
        YouTubeSocialContextResearchRequest request,
        YouTubeSocialContextResearchResponse research,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken);

    Task<YouTubeSocialContextWorkspaceDto?> GetAsync(
        string workspaceId,
        CancellationToken cancellationToken);

    Task<YouTubeSocialContextWorkspaceDto?> GetByVideoIdAsync(
        string videoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>> ListAsync(
        string? status,
        int take,
        CancellationToken cancellationToken);

    Task<YouTubeSocialContextWorkspaceDto> UpdateDraftAsync(
        string workspaceId,
        YouTubeSocialContextWorkspaceDraftUpdateRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken);

    Task<YouTubeSocialContextWorkspaceDto> LinkPublicationAsync(
        string workspaceId,
        YouTubeSocialContextPublicationLinkRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken);
}

public sealed class YouTubeSocialContextWorkspaceConcurrencyException(string workspaceId)
    : InvalidOperationException($"YouTube 글쓰기 작업공간이 다른 요청에서 먼저 변경되었습니다: {workspaceId}");

public sealed partial class MongoYouTubeSocialContextWorkspaceStore : IYouTubeSocialContextWorkspaceStore
{
    internal const string CollectionName = "community_youtube_post_workspaces";
    private const int SchemaVersion = 2;
    private readonly IMongoCollection<YouTubeSocialContextWorkspaceDocument> _collection;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public MongoYouTubeSocialContextWorkspaceStore(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options,
        TimeProvider timeProvider)
    {
        var databaseName = options.Value.Database?.Trim();
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        _collection = mongoClient
            .GetDatabase(databaseName)
            .GetCollection<YouTubeSocialContextWorkspaceDocument>(CollectionName);
        _timeProvider = timeProvider;
    }

    public async Task<YouTubeSocialContextWorkspaceDto> SaveResearchAsync(
        YouTubeSocialContextResearchRequest request,
        YouTubeSocialContextResearchResponse research,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(research);
        await EnsureIndexesAsync(cancellationToken);

        var videoId = NormalizeVideoId(research.Video.VideoId);
        var workspaceId = CreateWorkspaceId(videoId);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var existing = await _collection
                .Find(document => document.Id == workspaceId)
                .FirstOrDefaultAsync(cancellationToken);
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var generatedDraft = ToDraftDocument(
                research.Draft,
                research.Video.OriginalUrl,
                now,
                isManuallyEdited: false);
            var currentDraft = existing?.Draft?.IsManuallyEdited == true
                ? existing.Draft
                : generatedDraft;
            var status = existing?.Status == YouTubeSocialContextWorkspaceStatusCodes.Published
                ? YouTubeSocialContextWorkspaceStatusCodes.Published
                : currentDraft.IsManuallyEdited
                    ? YouTubeSocialContextWorkspaceStatusCodes.DraftEdited
                    : YouTubeSocialContextWorkspaceStatusCodes.ResearchReady;
            var document = new YouTubeSocialContextWorkspaceDocument
            {
                Id = workspaceId,
                SchemaVersion = SchemaVersion,
                Revision = (existing?.Revision ?? 0) + 1,
                Status = status,
                Video = ToDocument(research.Video),
                SearchTerms = NormalizeList(research.SearchTerms, 20, 160),
                AdjacentTopics = NormalizeList(research.AdjacentTopics, 20, 120),
                SourceTargets = BuildTargets(request.SourceTargets),
                TakePerSource = Math.Clamp(request.TakePerSource, 1, 50),
                SocialContextSources = BuildSourceGroups(research),
                Failures = research.Failures.Select(ToDocument).ToList(),
                GeneratedDraft = generatedDraft,
                Draft = currentDraft,
                ImportJourney = existing?.ImportJourney ?? new YouTubeImportJourneyDraftDocument(),
                PublishedPostId = existing?.PublishedPostId,
                PublicationLinks = existing?.PublicationLinks ?? [],
                LastResearchedAtUtc = EnsureUtc(research.GeneratedAtUtc),
                CreatedAtUtc = existing?.CreatedAtUtc ?? now,
                UpdatedAtUtc = now,
                CreatedByUserId = existing?.CreatedByUserId
                                  ?? Normalize(updatedByUserId, "server-admin", 200),
                UpdatedByUserId = Normalize(updatedByUserId, "server-admin", 200),
                UpdatedByDisplayName = Normalize(updatedByDisplayName, "홍달 운영자", 100)
            };

            try
            {
                if (existing is null)
                {
                    await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
                    return ToDto(document);
                }

                var result = await _collection.ReplaceOneAsync(
                    item => item.Id == workspaceId && item.Revision == existing.Revision,
                    document,
                    cancellationToken: cancellationToken);
                if (result.MatchedCount == 1)
                {
                    return ToDto(document);
                }
            }
            catch (MongoWriteException exception)
                when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey && attempt < 2)
            {
                continue;
            }
        }

        throw new YouTubeSocialContextWorkspaceConcurrencyException(workspaceId);
    }

    public async Task<YouTubeSocialContextWorkspaceDto?> GetAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var normalizedId = Normalize(workspaceId, string.Empty, 140);
        if (normalizedId.Length == 0)
        {
            return null;
        }

        var document = await _collection
            .Find(item => item.Id == normalizedId)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToDto(document);
    }

    public Task<YouTubeSocialContextWorkspaceDto?> GetByVideoIdAsync(
        string videoId,
        CancellationToken cancellationToken)
        => GetAsync(CreateWorkspaceId(NormalizeVideoId(videoId)), cancellationToken);

    public async Task<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>> ListAsync(
        string? status,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        if (normalizedStatus is not null
            && !YouTubeSocialContextWorkspaceStatusCodes.IsSupported(normalizedStatus))
        {
            throw new ArgumentException("지원하지 않는 YouTube 글쓰기 작업공간 상태입니다.", nameof(status));
        }

        var filter = normalizedStatus is null
            ? Builders<YouTubeSocialContextWorkspaceDocument>.Filter.Empty
            : Builders<YouTubeSocialContextWorkspaceDocument>.Filter.Eq(item => item.Status, normalizedStatus);
        var documents = await _collection
            .Find(filter)
            .SortByDescending(item => item.UpdatedAtUtc)
            .Limit(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
        return documents.Select(ToSummaryDto).ToArray();
    }

    public async Task<YouTubeSocialContextWorkspaceDto> UpdateDraftAsync(
        string workspaceId,
        YouTubeSocialContextWorkspaceDraftUpdateRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var existing = await FindRequiredAsync(workspaceId, cancellationToken);
        EnsureRevision(existing, request.ExpectedRevision);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        existing.Draft = new YouTubeSocialContextWorkspaceDraftDocument
        {
            Nickname = Normalize(request.Nickname, string.Empty, 40),
            Category = Normalize(request.Category, string.Empty, 40),
            WorkflowTag = Normalize(request.WorkflowTag, string.Empty, 100),
            RoleTag = Normalize(request.RoleTag, string.Empty, 100),
            Title = NormalizeRequired(request.Title, nameof(request.Title), 160),
            Body = NormalizeRequired(request.Body, nameof(request.Body), 4_000),
            SharedLinkUrl = NormalizeUrl(request.SharedLinkUrl),
            CollectiveAction = existing.GeneratedDraft.CollectiveAction,
            IsManuallyEdited = true,
            UpdatedAtUtc = now
        };
        if (request.ImportJourney is not null)
        {
            existing.ImportJourney = ToDocument(request.ImportJourney, now);
        }

        existing.Revision++;
        existing.Status = YouTubeSocialContextWorkspaceStatusCodes.DraftEdited;
        existing.UpdatedAtUtc = now;
        existing.UpdatedByUserId = Normalize(updatedByUserId, "server-admin", 200);
        existing.UpdatedByDisplayName = Normalize(updatedByDisplayName, "홍달 운영자", 100);
        await ReplaceExpectedAsync(existing, request.ExpectedRevision, cancellationToken);
        return ToDto(existing);
    }

    public async Task<YouTubeSocialContextWorkspaceDto> LinkPublicationAsync(
        string workspaceId,
        YouTubeSocialContextPublicationLinkRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PostId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.PostId), "연결할 게시글 ID가 필요합니다.");
        }

        var existing = await FindRequiredAsync(workspaceId, cancellationToken);
        EnsureRevision(existing, request.ExpectedRevision);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (existing.PublicationLinks.All(link => link.PostId != request.PostId))
        {
            existing.PublicationLinks.Add(new YouTubeSocialContextPublicationLinkDocument
            {
                PostId = request.PostId,
                LinkedAtUtc = now,
                LinkedByDisplayName = Normalize(updatedByDisplayName, "홍달 운영자", 100)
            });
        }

        existing.PublishedPostId = request.PostId;
        existing.Status = YouTubeSocialContextWorkspaceStatusCodes.Published;
        existing.Revision++;
        existing.UpdatedAtUtc = now;
        existing.UpdatedByUserId = Normalize(updatedByUserId, "server-admin", 200);
        existing.UpdatedByDisplayName = Normalize(updatedByDisplayName, "홍달 운영자", 100);
        await ReplaceExpectedAsync(existing, request.ExpectedRevision, cancellationToken);
        return ToDto(existing);
    }

    private async Task<YouTubeSocialContextWorkspaceDocument> FindRequiredAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeRequired(workspaceId, nameof(workspaceId), 140);
        return await _collection
                   .Find(item => item.Id == normalizedId)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new KeyNotFoundException($"YouTube 글쓰기 작업공간을 찾을 수 없습니다: {normalizedId}");
    }

    private async Task ReplaceExpectedAsync(
        YouTubeSocialContextWorkspaceDocument document,
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        var result = await _collection.ReplaceOneAsync(
            item => item.Id == document.Id && item.Revision == expectedRevision,
            document,
            cancellationToken: cancellationToken);
        if (result.MatchedCount == 0)
        {
            throw new YouTubeSocialContextWorkspaceConcurrencyException(document.Id);
        }
    }

    private static void EnsureRevision(
        YouTubeSocialContextWorkspaceDocument document,
        long expectedRevision)
    {
        if (expectedRevision <= 0 || document.Revision != expectedRevision)
        {
            throw new YouTubeSocialContextWorkspaceConcurrencyException(document.Id);
        }
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexesReady)
        {
            return;
        }

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexesReady)
            {
                return;
            }

            await _collection.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<YouTubeSocialContextWorkspaceDocument>(
                        Builders<YouTubeSocialContextWorkspaceDocument>.IndexKeys
                            .Descending(item => item.UpdatedAtUtc)),
                    new CreateIndexModel<YouTubeSocialContextWorkspaceDocument>(
                        Builders<YouTubeSocialContextWorkspaceDocument>.IndexKeys
                            .Ascending(item => item.Status)
                            .Descending(item => item.UpdatedAtUtc))
                ],
                cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }
}
