using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Community;

public sealed class EfCommunityPostOpportunityStore : ICommunityPostOpportunityStore
{
    private readonly HongdalContext _db;

    public EfCommunityPostOpportunityStore(HongdalContext db)
    {
        _db = db;
    }

    public Task<CommunityPostOpportunitySource?> GetAsync(
        long postId,
        CancellationToken cancellationToken = default)
        => _db.PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => post.Id == postId && !post.IsDeleted)
            .Select(post => new CommunityPostOpportunitySource(
                post.Id,
                post.AppKey,
                post.Title,
                post.Body,
                post.AuthorUserId,
                post.커뮤니티원장Id,
                post.IsReportBoardPost,
                post.SalesOfferJson,
                post.CreatedAtUtc,
                post.Category,
                post.WorkflowTag))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CommunityPostLedgerLinkResult> LinkLedgerAsync(
        long postId,
        string actorUserId,
        string ledgerId,
        CancellationToken cancellationToken = default)
    {
        var actor = actorUserId.Trim();
        var updated = await _db.PlatformCommunityPosts
            .Where(post => post.Id == postId
                           && !post.IsDeleted
                           && !post.IsReportBoardPost
                           && post.AuthorUserId == actor
                           && post.커뮤니티원장Id == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(post => post.커뮤니티원장Id, ledgerId)
                    .SetProperty(post => post.UpdatedAtUtc, DateTime.UtcNow),
                cancellationToken);
        if (updated == 1)
        {
            return CommunityPostLedgerLinkResult.Linked;
        }

        var current = await GetAsync(postId, cancellationToken);
        if (current is null)
        {
            return CommunityPostLedgerLinkResult.NotFound;
        }

        if (!string.Equals(current.AuthorUserId, actor, StringComparison.OrdinalIgnoreCase))
        {
            return CommunityPostLedgerLinkResult.NotOwner;
        }

        return string.Equals(current.LinkedLedgerId, ledgerId, StringComparison.OrdinalIgnoreCase)
            ? CommunityPostLedgerLinkResult.AlreadyLinked
            : CommunityPostLedgerLinkResult.ConflictingLedger;
    }

    public async Task<CommunityPostMomentumUpdateResult> SetMomentumPromotionAsync(
        long postId,
        string ledgerId,
        string momentumCode,
        string momentumMessage,
        int roleParticipantCount,
        CancellationToken cancellationToken = default)
    {
        var normalizedLedgerId = ledgerId.Trim();
        var normalizedCode = momentumCode.Trim();
        var normalizedMessage = momentumMessage.Trim();
        var participantCount = Math.Max(0, roleParticipantCount);
        var now = DateTime.UtcNow;
        var updated = await _db.PlatformCommunityPosts
            .Where(post => post.Id == postId
                           && !post.IsDeleted
                           && !post.IsReportBoardPost
                           && post.커뮤니티원장Id == normalizedLedgerId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(post => post.IsCommunityMomentumPromoted, true)
                    .SetProperty(post => post.CommunityMomentumCode, normalizedCode)
                    .SetProperty(post => post.CommunityMomentumMessage, normalizedMessage)
                    .SetProperty(post => post.CommunityMomentumRoleParticipantCount, participantCount)
                    .SetProperty(post => post.CommunityMomentumUpdatedAtUtc, now)
                    .SetProperty(post => post.UpdatedAtUtc, now),
                cancellationToken);
        if (updated == 1)
        {
            return CommunityPostMomentumUpdateResult.Updated;
        }

        var current = await GetAsync(postId, cancellationToken);
        return current is null
            ? CommunityPostMomentumUpdateResult.NotFound
            : CommunityPostMomentumUpdateResult.ConflictingLedger;
    }
}
