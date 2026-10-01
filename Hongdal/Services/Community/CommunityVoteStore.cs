using System.Text.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

internal interface ICommunityVoteStore
{
    Task AddAsync(CommunityVoteRecord vote, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommunityVoteRecord>> ListAsync(
        string? appKey,
        string? communityScope,
        string? normalizedHsCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CommunityVoteRecord>> ListBySourcePostAsync(
        long sourcePostId,
        CancellationToken cancellationToken);

    Task<CommunityVoteRecord?> GetAsync(Guid voteId, CancellationToken cancellationToken);

    Task<bool> ReplaceAsync(
        CommunityVoteRecord vote,
        long expectedRevision,
        CancellationToken cancellationToken);

    Task<CommunityVoteDemandHandoffWork?> TryClaimDemandHandoffAsync(
        Guid voteId,
        string outboxId,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken);

    Task<CommunityVoteDemandHandoffWork?> TryClaimNextDemandHandoffAsync(
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken);

    Task CompleteDemandHandoffAsync(
        CommunityVoteDemandHandoffWork work,
        CancellationToken cancellationToken);

    Task FailDemandHandoffAsync(
        CommunityVoteDemandHandoffWork work,
        string error,
        int maxAttempts,
        TimeSpan retryBaseDelay,
        CancellationToken cancellationToken);
}

internal sealed class MongoCommunityVoteStore : ICommunityVoteStore
{
    private const string CollectionName = "community_votes";
    private readonly IMongoCollection<CommunityVoteRecord> _collection;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public MongoCommunityVoteStore(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        var databaseName = options.Value.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        _collection = mongoClient
            .GetDatabase(databaseName.Trim())
            .GetCollection<CommunityVoteRecord>(CollectionName);
    }

    public async Task AddAsync(CommunityVoteRecord vote, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        await _collection.InsertOneAsync(vote, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityVoteRecord>> ListAsync(
        string? appKey,
        string? communityScope,
        string? normalizedHsCode,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var filter = Builders<CommunityVoteRecord>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(appKey))
        {
            filter &= Builders<CommunityVoteRecord>.Filter.Eq(x => x.AppKey, appKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(communityScope))
        {
            filter &= Builders<CommunityVoteRecord>.Filter.Eq(x => x.CommunityScope, communityScope.Trim());
        }

        if (!string.IsNullOrWhiteSpace(normalizedHsCode))
        {
            var hsCodeRegex = new BsonRegularExpression(
                CommunityVoteHsCode.PrefixRegex(normalizedHsCode));
            filter &= Builders<CommunityVoteRecord>.Filter.Or(
                Builders<CommunityVoteRecord>.Filter.Regex("GroupPurchase.HsCode", hsCodeRegex),
                Builders<CommunityVoteRecord>.Filter.Regex("Options.HsCode", hsCodeRegex));
        }

        return await _collection
            .Find(filter)
            .SortByDescending(x => x.CreatedAtUtc)
            .Limit(200)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityVoteRecord>> ListBySourcePostAsync(
        long sourcePostId,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        return await _collection
            .Find(x => x.SourcePostId == sourcePostId)
            .SortByDescending(x => x.CreatedAtUtc)
            .Limit(50)
            .ToListAsync(cancellationToken);
    }

    public async Task<CommunityVoteRecord?> GetAsync(Guid voteId, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        return await _collection.Find(x => x.Id == voteId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ReplaceAsync(
        CommunityVoteRecord vote,
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var result = await _collection.ReplaceOneAsync(
            x => x.Id == vote.Id && x.Revision == expectedRevision,
            vote,
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    public async Task<CommunityVoteDemandHandoffWork?> TryClaimDemandHandoffAsync(
        Guid voteId,
        string outboxId,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var vote = await GetAsync(voteId, cancellationToken);
            if (vote is null)
            {
                return null;
            }

            var outbox = vote.DemandHandoffOutbox.FirstOrDefault(x =>
                string.Equals(x.OutboxId, outboxId, StringComparison.Ordinal));
            if (outbox is null || !IsDueForInMemory(outbox, leaseTimeout, DateTime.UtcNow))
            {
                return null;
            }

            var work = Claim(vote, outbox);
            var expectedRevision = vote.Revision++;
            if (await ReplaceAsync(vote, expectedRevision, cancellationToken))
            {
                return work;
            }
        }

        return null;
    }

    public async Task<CommunityVoteDemandHandoffWork?> TryClaimNextDemandHandoffAsync(
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var leaseExpiredAt = now.Subtract(
            leaseTimeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseTimeout);
        var outboxFilter = Builders<CommunityVoteDemandHandoffOutboxRecord>.Filter;
        var candidateFilter = Builders<CommunityVoteRecord>.Filter.ElemMatch(
            x => x.DemandHandoffOutbox,
            outboxFilter.Or(
                outboxFilter.Eq(x => x.Status, CommunityVoteDemandHandoffStatusCodes.Pending),
                outboxFilter.And(
                    outboxFilter.Eq(x => x.Status, CommunityVoteDemandHandoffStatusCodes.RetryPending),
                    outboxFilter.Or(
                        outboxFilter.Eq(x => x.NextAttemptAtUtc, null),
                        outboxFilter.Lte(x => x.NextAttemptAtUtc, now))),
                outboxFilter.And(
                    outboxFilter.Eq(x => x.Status, CommunityVoteDemandHandoffStatusCodes.Processing),
                    outboxFilter.Or(
                        outboxFilter.Eq(x => x.ProcessingStartedAtUtc, null),
                        outboxFilter.Lte(x => x.ProcessingStartedAtUtc, leaseExpiredAt)))));
        var candidates = await _collection.Find(candidateFilter)
            .SortBy(x => x.CreatedAtUtc)
            .Limit(100)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var outbox = candidate.DemandHandoffOutbox
                .Where(x => IsDueForInMemory(x, leaseTimeout, now))
                .OrderBy(x => x.NextAttemptAtUtc ?? x.UpdatedAtUtc)
                .FirstOrDefault();
            if (outbox is null)
            {
                continue;
            }

            var claimed = await TryClaimDemandHandoffAsync(
                candidate.Id,
                outbox.OutboxId,
                leaseTimeout,
                cancellationToken);
            if (claimed is not null)
            {
                return claimed;
            }
        }

        return null;
    }

    public Task CompleteDemandHandoffAsync(
        CommunityVoteDemandHandoffWork work,
        CancellationToken cancellationToken) =>
        UpdateClaimedOutboxAsync(work, outbox =>
        {
            outbox.Status = CommunityVoteDemandHandoffStatusCodes.Completed;
            outbox.CompletedAtUtc = DateTime.UtcNow;
            outbox.NextAttemptAtUtc = null;
            outbox.ProcessingToken = null;
            outbox.ProcessingStartedAtUtc = null;
            outbox.LastError = null;
            outbox.UpdatedAtUtc = DateTime.UtcNow;
        }, cancellationToken);

    public Task FailDemandHandoffAsync(
        CommunityVoteDemandHandoffWork work,
        string error,
        int maxAttempts,
        TimeSpan retryBaseDelay,
        CancellationToken cancellationToken) =>
        UpdateClaimedOutboxAsync(work, outbox => MarkFailed(
            outbox,
            error,
            maxAttempts,
            retryBaseDelay), cancellationToken);

    private async Task UpdateClaimedOutboxAsync(
        CommunityVoteDemandHandoffWork work,
        Action<CommunityVoteDemandHandoffOutboxRecord> update,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var vote = await GetAsync(work.VoteId, cancellationToken);
            var outbox = vote?.DemandHandoffOutbox.FirstOrDefault(x =>
                string.Equals(x.OutboxId, work.OutboxId, StringComparison.Ordinal)
                && string.Equals(x.ProcessingToken, work.ProcessingToken, StringComparison.Ordinal));
            if (vote is null || outbox is null)
            {
                return;
            }

            update(outbox);
            var expectedRevision = vote.Revision++;
            if (await ReplaceAsync(vote, expectedRevision, cancellationToken))
            {
                return;
            }
        }
    }

    internal static bool IsDueForInMemory(
        CommunityVoteDemandHandoffOutboxRecord outbox,
        TimeSpan leaseTimeout,
        DateTime now)
    {
        if (outbox.Status is CommunityVoteDemandHandoffStatusCodes.Pending)
        {
            return true;
        }

        if (outbox.Status is CommunityVoteDemandHandoffStatusCodes.RetryPending)
        {
            return outbox.NextAttemptAtUtc is null || outbox.NextAttemptAtUtc <= now;
        }

        return outbox.Status is CommunityVoteDemandHandoffStatusCodes.Processing
            && (outbox.ProcessingStartedAtUtc is null
                || outbox.ProcessingStartedAtUtc <= now.Subtract(
                    leaseTimeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseTimeout));
    }

    private static CommunityVoteDemandHandoffWork Claim(
        CommunityVoteRecord vote,
        CommunityVoteDemandHandoffOutboxRecord outbox)
    {
        var now = DateTime.UtcNow;
        var token = Guid.NewGuid().ToString("N");
        outbox.Status = CommunityVoteDemandHandoffStatusCodes.Processing;
        outbox.ProcessingToken = token;
        outbox.ProcessingStartedAtUtc = now;
        outbox.NextAttemptAtUtc = null;
        outbox.LastError = null;
        outbox.AttemptCount++;
        outbox.UpdatedAtUtc = now;
        return new CommunityVoteDemandHandoffWork(
            vote.Id,
            outbox.OutboxId,
            token,
            outbox.Request,
            outbox.AttemptCount);
    }

    private static void MarkFailed(
        CommunityVoteDemandHandoffOutboxRecord outbox,
        string error,
        int maxAttempts,
        TimeSpan retryBaseDelay)
    {
        var terminal = outbox.AttemptCount >= Math.Max(1, maxAttempts);
        var baseSeconds = Math.Max(0, retryBaseDelay.TotalSeconds);
        var delaySeconds = Math.Min(
            3600,
            baseSeconds * Math.Pow(2, Math.Max(0, outbox.AttemptCount - 1)));
        var now = DateTime.UtcNow;
        outbox.Status = terminal
            ? CommunityVoteDemandHandoffStatusCodes.Failed
            : CommunityVoteDemandHandoffStatusCodes.RetryPending;
        outbox.NextAttemptAtUtc = terminal ? null : now.AddSeconds(delaySeconds);
        outbox.ProcessingToken = null;
        outbox.ProcessingStartedAtUtc = null;
        outbox.LastError = error.Length <= 2000 ? error : error[..2000];
        outbox.UpdatedAtUtc = now;
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

            var models = new[]
            {
                new CreateIndexModel<CommunityVoteRecord>(
                    Builders<CommunityVoteRecord>.IndexKeys
                        .Ascending(x => x.AppKey)
                        .Ascending(x => x.CommunityScope)
                        .Descending(x => x.CreatedAtUtc)),
                new CreateIndexModel<CommunityVoteRecord>(
                    Builders<CommunityVoteRecord>.IndexKeys.Ascending(x => x.SourcePostId),
                    new CreateIndexOptions { Sparse = true }),
                new CreateIndexModel<CommunityVoteRecord>(
                    Builders<CommunityVoteRecord>.IndexKeys.Ascending("GroupPurchase.HsCode")),
                new CreateIndexModel<CommunityVoteRecord>(
                    Builders<CommunityVoteRecord>.IndexKeys.Ascending("Options.HsCode")),
                new CreateIndexModel<CommunityVoteRecord>(
                    Builders<CommunityVoteRecord>.IndexKeys
                        .Ascending("DemandHandoffOutbox.Status")
                        .Ascending("DemandHandoffOutbox.NextAttemptAtUtc"))
            };
            await _collection.Indexes.CreateManyAsync(models, cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }
}

internal sealed class InMemoryCommunityVoteStore : ICommunityVoteStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, CommunityVoteRecord> _votes = [];

    public Task AddAsync(CommunityVoteRecord vote, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _votes.Add(vote.Id, Clone(vote));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CommunityVoteRecord>> ListAsync(
        string? appKey,
        string? communityScope,
        string? normalizedHsCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var items = _votes.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(appKey))
            {
                items = items.Where(x => string.Equals(x.AppKey, appKey.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(communityScope))
            {
                items = items.Where(x => string.Equals(x.CommunityScope, communityScope.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(normalizedHsCode))
            {
                items = items.Where(x =>
                    CommunityVoteHsCode.MatchesPrefix(x.GroupPurchase?.HsCode, normalizedHsCode)
                    || x.Options.Any(option =>
                        CommunityVoteHsCode.MatchesPrefix(option.HsCode, normalizedHsCode)));
            }

            return Task.FromResult<IReadOnlyList<CommunityVoteRecord>>(
                items.OrderByDescending(x => x.CreatedAtUtc).Select(Clone).ToArray());
        }
    }

    public Task<IReadOnlyList<CommunityVoteRecord>> ListBySourcePostAsync(
        long sourcePostId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<CommunityVoteRecord>>(
                _votes.Values
                    .Where(x => x.SourcePostId == sourcePostId)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(Clone)
                    .ToArray());
        }
    }

    public Task<CommunityVoteRecord?> GetAsync(Guid voteId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_votes.TryGetValue(voteId, out var vote) ? Clone(vote) : null);
        }
    }

    public Task<bool> ReplaceAsync(
        CommunityVoteRecord vote,
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_votes.TryGetValue(vote.Id, out var existing) || existing.Revision != expectedRevision)
            {
                return Task.FromResult(false);
            }

            _votes[vote.Id] = Clone(vote);
            return Task.FromResult(true);
        }
    }

    public Task<CommunityVoteDemandHandoffWork?> TryClaimDemandHandoffAsync(
        Guid voteId,
        string outboxId,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_votes.TryGetValue(voteId, out var vote))
            {
                return Task.FromResult<CommunityVoteDemandHandoffWork?>(null);
            }

            var outbox = vote.DemandHandoffOutbox.FirstOrDefault(x =>
                string.Equals(x.OutboxId, outboxId, StringComparison.Ordinal));
            if (outbox is null || !MongoCommunityVoteStore.IsDueForInMemory(outbox, leaseTimeout, DateTime.UtcNow))
            {
                return Task.FromResult<CommunityVoteDemandHandoffWork?>(null);
            }

            var work = Claim(vote, outbox);
            vote.Revision++;
            return Task.FromResult<CommunityVoteDemandHandoffWork?>(work);
        }
    }

    public Task<CommunityVoteDemandHandoffWork?> TryClaimNextDemandHandoffAsync(
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            foreach (var vote in _votes.Values.OrderBy(x => x.CreatedAtUtc))
            {
                var outbox = vote.DemandHandoffOutbox
                    .Where(x => MongoCommunityVoteStore.IsDueForInMemory(x, leaseTimeout, now))
                    .OrderBy(x => x.NextAttemptAtUtc ?? x.UpdatedAtUtc)
                    .FirstOrDefault();
                if (outbox is null)
                {
                    continue;
                }

                var work = Claim(vote, outbox);
                vote.Revision++;
                return Task.FromResult<CommunityVoteDemandHandoffWork?>(work);
            }

            return Task.FromResult<CommunityVoteDemandHandoffWork?>(null);
        }
    }

    public Task CompleteDemandHandoffAsync(
        CommunityVoteDemandHandoffWork work,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var outbox = FindClaimed(work);
            if (outbox is not null)
            {
                var now = DateTime.UtcNow;
                outbox.Status = CommunityVoteDemandHandoffStatusCodes.Completed;
                outbox.CompletedAtUtc = now;
                outbox.NextAttemptAtUtc = null;
                outbox.ProcessingToken = null;
                outbox.ProcessingStartedAtUtc = null;
                outbox.LastError = null;
                outbox.UpdatedAtUtc = now;
                _votes[work.VoteId].Revision++;
            }
        }

        return Task.CompletedTask;
    }

    public Task FailDemandHandoffAsync(
        CommunityVoteDemandHandoffWork work,
        string error,
        int maxAttempts,
        TimeSpan retryBaseDelay,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var outbox = FindClaimed(work);
            if (outbox is not null)
            {
                MarkFailed(outbox, error, maxAttempts, retryBaseDelay);
                _votes[work.VoteId].Revision++;
            }
        }

        return Task.CompletedTask;
    }

    private CommunityVoteDemandHandoffOutboxRecord? FindClaimed(CommunityVoteDemandHandoffWork work) =>
        _votes.TryGetValue(work.VoteId, out var vote)
            ? vote.DemandHandoffOutbox.FirstOrDefault(x =>
                string.Equals(x.OutboxId, work.OutboxId, StringComparison.Ordinal)
                && string.Equals(x.ProcessingToken, work.ProcessingToken, StringComparison.Ordinal))
            : null;

    private static CommunityVoteDemandHandoffWork Claim(
        CommunityVoteRecord vote,
        CommunityVoteDemandHandoffOutboxRecord outbox)
    {
        var now = DateTime.UtcNow;
        var token = Guid.NewGuid().ToString("N");
        outbox.Status = CommunityVoteDemandHandoffStatusCodes.Processing;
        outbox.ProcessingToken = token;
        outbox.ProcessingStartedAtUtc = now;
        outbox.NextAttemptAtUtc = null;
        outbox.LastError = null;
        outbox.AttemptCount++;
        outbox.UpdatedAtUtc = now;
        return new CommunityVoteDemandHandoffWork(
            vote.Id,
            outbox.OutboxId,
            token,
            CloneRequest(outbox.Request),
            outbox.AttemptCount);
    }

    private static void MarkFailed(
        CommunityVoteDemandHandoffOutboxRecord outbox,
        string error,
        int maxAttempts,
        TimeSpan retryBaseDelay)
    {
        var terminal = outbox.AttemptCount >= Math.Max(1, maxAttempts);
        var baseSeconds = Math.Max(0, retryBaseDelay.TotalSeconds);
        var delaySeconds = Math.Min(3600, baseSeconds * Math.Pow(2, Math.Max(0, outbox.AttemptCount - 1)));
        var now = DateTime.UtcNow;
        outbox.Status = terminal
            ? CommunityVoteDemandHandoffStatusCodes.Failed
            : CommunityVoteDemandHandoffStatusCodes.RetryPending;
        outbox.NextAttemptAtUtc = terminal ? null : now.AddSeconds(delaySeconds);
        outbox.ProcessingToken = null;
        outbox.ProcessingStartedAtUtc = null;
        outbox.LastError = error.Length <= 2000 ? error : error[..2000];
        outbox.UpdatedAtUtc = now;
    }

    private static CommunityGroupPurchaseDemandHandoffRequest CloneRequest(
        CommunityGroupPurchaseDemandHandoffRequest source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<CommunityGroupPurchaseDemandHandoffRequest>(json)
            ?? throw new InvalidOperationException("공동구매 수요 전달 요청을 복제할 수 없습니다.");
    }

    private static CommunityVoteRecord Clone(CommunityVoteRecord source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<CommunityVoteRecord>(json)
            ?? throw new InvalidOperationException("커뮤니티 투표 상태를 복제할 수 없습니다.");
    }
}

internal sealed class CommunityVoteRecord
{
    [BsonId]
    public Guid Id { get; set; }
    public long Revision { get; set; } = 1;
    public string AppKey { get; set; } = string.Empty;
    public string CommunityScope { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VoteKind { get; set; } = CommunityVoteKindCodes.General;
    public long? SourcePostId { get; set; }
    public string? CommunityLedgerId { get; set; }
    public string Status { get; set; } = CommunityVoteStatusCodes.Open;
    public bool AllowMultipleSelection { get; set; }
    public bool ResolutionDocumentEnabled { get; set; }
    public bool SignatureRequired { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string ClosedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ClosesAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public IReadOnlyList<CommunityVoteOptionRecord> Options { get; set; } = [];
    public List<CommunityVoteCastRecord> Votes { get; set; } = [];
    public List<CommunityVoteDemandHandoffOutboxRecord> DemandHandoffOutbox { get; set; } = [];
    public CommunityGroupPurchaseVoteSettingsRecord? GroupPurchase { get; set; }
    public CommunityVoteResolutionDocumentRecord? ResolutionDocument { get; set; }
}

internal static class CommunityVoteDemandHandoffStatusCodes
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string RetryPending = "retry-pending";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

internal sealed class CommunityVoteDemandHandoffOutboxRecord
{
    public string OutboxId { get; set; } = string.Empty;
    public CommunityGroupPurchaseDemandHandoffRequest Request { get; set; } = new();
    public string Status { get; set; } = CommunityVoteDemandHandoffStatusCodes.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? ProcessingToken { get; set; }
    public DateTime? ProcessingStartedAtUtc { get; set; }
    public string? LastError { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed record CommunityVoteDemandHandoffWork(
    Guid VoteId,
    string OutboxId,
    string ProcessingToken,
    CommunityGroupPurchaseDemandHandoffRequest Request,
    int AttemptCount);

internal sealed class CommunityVoteOptionRecord
{
    public string OptionId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string ProductKey { get; set; } = string.Empty;
    public string HsCode { get; set; } = string.Empty;
    public string TemperatureCode { get; set; } = string.Empty;
    public string LogisticsMode { get; set; } = string.Empty;
    public string QuantityUnit { get; set; } = string.Empty;
}

internal sealed class CommunityVoteCastRecord
{
    public string VoterHash { get; set; } = string.Empty;
    public string? VoterUserId { get; set; }
    public string VoterDisplayName { get; set; } = string.Empty;
    public IReadOnlyList<string> OptionIds { get; set; } = [];
    public int RequestedQuantity { get; set; }
    public string ParticipationMethodCode { get; set; } = string.Empty;
    public string? PickupPointId { get; set; }
    public bool AllowNearbyPickupPointFallback { get; set; }
    public DateTime VotedAtUtc { get; set; }
}

internal sealed class CommunityGroupPurchaseVoteSettingsRecord
{
    public string ProposerRoleCode { get; set; } = CommunityGroupPurchaseProposerRoleCodes.GroupPurchaseRepresentative;
    public string AgreementPolicyCode { get; set; } = CommunityGroupPurchaseAgreementPolicy.PolicyCode;
    public string ProposalOriginLegalEffectNotice { get; set; }
        = CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice;
    public string OperatingMarketCountryCode { get; set; }
        = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode;
    public string SellerCountryCode { get; set; } = string.Empty;
    public string ShipFromCountryCode { get; set; } = string.Empty;
    public string DeliveryCountryCode { get; set; } = string.Empty;
    public string CustomsClearanceStatusCode { get; set; }
        = CommunityGroupPurchaseCustomsClearanceStatusCodes.Unknown;
    public string TradeRouteCode { get; set; } = string.Empty;
    public string ParticipationPolicyCode { get; set; } = string.Empty;
    public string HsCode { get; set; } = string.Empty;
    public string TemperatureCode { get; set; } = "상온";
    public string LogisticsMode { get; set; } = "LCL";
    public string QuantityUnit { get; set; } = "개";
    public decimal? TargetUnitPriceKrwPerKg { get; set; }
    public string ServiceAreaKey { get; set; } = string.Empty;
    public string ServiceAreaLabel { get; set; } = string.Empty;
    public int? RadiusMeters { get; set; }
    public int MinimumParticipantCount { get; set; }
    public int MinimumTotalQuantity { get; set; }
    public IReadOnlyList<CommunityVotePickupPointRecord> PickupPoints { get; set; } = [];
}

internal sealed class CommunityVotePickupPointRecord
{
    public string PickupPointId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressSummary { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string StorageTypeCode { get; set; } = string.Empty;
    public DateTime? PickupStartsAtUtc { get; set; }
    public DateTime? PickupEndsAtUtc { get; set; }
    public int? CapacityQuantity { get; set; }
    public int? MinimumParticipantCount { get; set; }
    public int? MinimumTotalQuantity { get; set; }
    public decimal PickupFee { get; set; }
}

internal sealed class CommunityVoteResolutionDocumentRecord
{
    public Guid Id { get; set; }
    public Guid VoteId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string ResolutionText { get; set; } = string.Empty;
    public string DocumentHash { get; set; } = string.Empty;
    public string Status { get; set; } = CommunityVoteResolutionStatusCodes.LegalReviewRequired;
    public string LegalEffectNotice { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public ContractElectronicSignatureBundle? SignatureBundle { get; set; }
}
