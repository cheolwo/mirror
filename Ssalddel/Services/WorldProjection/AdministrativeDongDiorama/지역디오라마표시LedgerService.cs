using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData.Korea;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;

public sealed record 지역사업장표시ClaimRequest(
    string ClaimStableId,
    Guid PublicBusinessRecordId,
    string AdministrativeRegionStableId,
    string SemanticPlaceStableId,
    string RequestedDisplayName,
    string CategoryCode,
    string RequestedByUserStableId,
    DateTimeOffset RequestedAtUtc);

public sealed record 지역디오라마후원CampaignRequest(
    string CampaignStableId,
    string ClaimStableId,
    string BadgeText,
    string DetailCardText,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string RequestedByUserStableId,
    DateTimeOffset RequestedAtUtc);

public interface I지역디오라마표시LedgerService
{
    Task<지역사업장표시Claim> SubmitClaimAsync(지역사업장표시ClaimRequest request, CancellationToken cancellationToken);
    Task<지역사업장표시Claim> ReviewClaimAsync(string claimStableId, long expectedRevision, bool approve,
        string reviewerUserStableId, string reasonCode, DateTimeOffset reviewedAtUtc, CancellationToken cancellationToken);
    Task<지역디오라마후원Campaign> SubmitCampaignAsync(지역디오라마후원CampaignRequest request, CancellationToken cancellationToken);
    Task<지역디오라마후원Campaign> ReviewCampaignAsync(string campaignStableId, long expectedRevision, bool approve,
        string reviewerUserStableId, string reasonCode, DateTimeOffset reviewedAtUtc, CancellationToken cancellationToken);
}

/// <summary>
/// 공공 사업장 사실과 사용자 Claim, 후원 표시 승인을 분리합니다. 결제·노출 순위·게임 상태는 변경하지 않습니다.
/// </summary>
public sealed class 지역디오라마표시LedgerService(PublicDataIngestionDbContext db)
    : I지역디오라마표시LedgerService, I행정동디오라마DisplayOverlaySource
{
    public async Task<지역사업장표시Claim> SubmitClaimAsync(
        지역사업장표시ClaimRequest request,
        CancellationToken cancellationToken)
    {
        ValidateClaimRequest(request);
        var stableId = request.ClaimStableId.Trim();
        var existing = await db.지역사업장표시Claims.SingleOrDefaultAsync(
            item => item.ClaimStableId == stableId, cancellationToken);
        if (existing is not null)
        {
            if (existing.PublicBusinessRecordId != request.PublicBusinessRecordId
                || !string.Equals(existing.RequestedByUserStableId, request.RequestedByUserStableId.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("RegionalBusinessDisplayClaimIdempotencyConflict");
            return existing;
        }
        if (!await db.공개인허가사업장Records.AnyAsync(
                item => item.Id == request.PublicBusinessRecordId && item.ClosureDate == null,
                cancellationToken))
            throw new InvalidOperationException("PublicBusinessRecordNotActive");
        var claim = new 지역사업장표시Claim
        {
            Id = Guid.NewGuid(),
            ClaimStableId = stableId,
            PublicBusinessRecordId = request.PublicBusinessRecordId,
            AdministrativeRegionStableId = request.AdministrativeRegionStableId.Trim(),
            SemanticPlaceStableId = request.SemanticPlaceStableId.Trim(),
            RequestedDisplayName = request.RequestedDisplayName.Trim(),
            CategoryCode = request.CategoryCode.Trim(),
            RequestedByUserStableId = request.RequestedByUserStableId.Trim(),
            RequestedAtUtc = request.RequestedAtUtc.ToUniversalTime()
        };
        db.지역사업장표시Claims.Add(claim);
        await db.SaveChangesAsync(cancellationToken);
        return claim;
    }

    public async Task<지역사업장표시Claim> ReviewClaimAsync(
        string claimStableId,
        long expectedRevision,
        bool approve,
        string reviewerUserStableId,
        string reasonCode,
        DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        var claim = await db.지역사업장표시Claims.SingleOrDefaultAsync(
            item => item.ClaimStableId == claimStableId.Trim(), cancellationToken)
            ?? throw new KeyNotFoundException("RegionalBusinessDisplayClaimNotFound");
        if (claim.Revision != expectedRevision)
            throw new InvalidOperationException("RegionalBusinessDisplayClaimRevisionConflict");
        if (!string.Equals(claim.StatusCode, 지역사업장표시ClaimStatusCodes.PendingReview, StringComparison.Ordinal))
            throw new InvalidOperationException("RegionalBusinessDisplayClaimNotPendingReview");
        claim.StatusCode = approve ? 지역사업장표시ClaimStatusCodes.Verified : 지역사업장표시ClaimStatusCodes.Rejected;
        claim.Revision++;
        claim.ReviewedByUserStableId = Required(reviewerUserStableId, "RegionalBusinessDisplayClaimReviewerRequired", 160);
        claim.ReviewReasonCode = Required(reasonCode, "RegionalBusinessDisplayClaimReviewReasonRequired", 80);
        claim.ReviewedAtUtc = reviewedAtUtc.ToUniversalTime();
        await db.SaveChangesAsync(cancellationToken);
        return claim;
    }

    public async Task<지역디오라마후원Campaign> SubmitCampaignAsync(
        지역디오라마후원CampaignRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCampaignRequest(request);
        var stableId = request.CampaignStableId.Trim();
        var existing = await db.지역디오라마후원Campaigns.SingleOrDefaultAsync(
            item => item.CampaignStableId == stableId, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestedByUserStableId, request.RequestedByUserStableId.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("RegionalDioramaSponsorshipIdempotencyConflict");
            return existing;
        }
        var claim = await db.지역사업장표시Claims.SingleOrDefaultAsync(
            item => item.ClaimStableId == request.ClaimStableId.Trim(), cancellationToken)
            ?? throw new KeyNotFoundException("RegionalBusinessDisplayClaimNotFound");
        if (!string.Equals(claim.StatusCode, 지역사업장표시ClaimStatusCodes.Verified, StringComparison.Ordinal))
            throw new InvalidOperationException("RegionalBusinessDisplayClaimNotVerified");
        if (!string.Equals(claim.RequestedByUserStableId, request.RequestedByUserStableId.Trim(), StringComparison.Ordinal))
            throw new UnauthorizedAccessException("RegionalDioramaSponsorshipClaimOwnerRequired");
        var campaign = new 지역디오라마후원Campaign
        {
            Id = Guid.NewGuid(),
            CampaignStableId = stableId,
            BusinessDisplayClaimId = claim.Id,
            BadgeText = request.BadgeText.Trim(),
            DetailCardText = request.DetailCardText.Trim(),
            StartsAtUtc = request.StartsAtUtc.ToUniversalTime(),
            EndsAtUtc = request.EndsAtUtc.ToUniversalTime(),
            RequestedByUserStableId = request.RequestedByUserStableId.Trim(),
            RequestedAtUtc = request.RequestedAtUtc.ToUniversalTime()
        };
        db.지역디오라마후원Campaigns.Add(campaign);
        await db.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    public async Task<지역디오라마후원Campaign> ReviewCampaignAsync(
        string campaignStableId,
        long expectedRevision,
        bool approve,
        string reviewerUserStableId,
        string reasonCode,
        DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        var campaign = await db.지역디오라마후원Campaigns.SingleOrDefaultAsync(
            item => item.CampaignStableId == campaignStableId.Trim(), cancellationToken)
            ?? throw new KeyNotFoundException("RegionalDioramaSponsorshipCampaignNotFound");
        if (campaign.Revision != expectedRevision)
            throw new InvalidOperationException("RegionalDioramaSponsorshipRevisionConflict");
        if (!string.Equals(campaign.StatusCode, 지역디오라마후원CampaignStatusCodes.PendingReview, StringComparison.Ordinal))
            throw new InvalidOperationException("RegionalDioramaSponsorshipNotPendingReview");
        campaign.StatusCode = approve
            ? 지역디오라마후원CampaignStatusCodes.Approved
            : 지역디오라마후원CampaignStatusCodes.Rejected;
        campaign.Revision++;
        campaign.ReviewedByUserStableId = Required(reviewerUserStableId, "RegionalDioramaSponsorshipReviewerRequired", 160);
        campaign.ReviewReasonCode = Required(reasonCode, "RegionalDioramaSponsorshipReviewReasonRequired", 80);
        campaign.ReviewedAtUtc = reviewedAtUtc.ToUniversalTime();
        await db.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    public async Task<IReadOnlyList<AdministrativeDongDisplayOverlay>> FindActiveAsync(
        string administrativeAreaStableId,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        var normalizedUtc = asOfUtc.Kind == DateTimeKind.Utc
            ? asOfUtc
            : asOfUtc.ToUniversalTime();
        var moment = new DateTimeOffset(normalizedUtc);
        return await db.지역디오라마후원Campaigns.AsNoTracking()
            .Where(campaign => campaign.StatusCode == 지역디오라마후원CampaignStatusCodes.Approved
                               && campaign.StartsAtUtc <= moment
                               && campaign.EndsAtUtc > moment
                               && campaign.BusinessDisplayClaim.StatusCode == 지역사업장표시ClaimStatusCodes.Verified
                               && campaign.BusinessDisplayClaim.AdministrativeRegionStableId == administrativeAreaStableId)
            .OrderBy(campaign => campaign.CampaignStableId)
            .Select(campaign => new AdministrativeDongDisplayOverlay
            {
                OverlayStableId = "sponsorship:" + campaign.CampaignStableId,
                OverlayKindCode = AdministrativeDongDisplayOverlayKinds.Sponsorship,
                DisplayLabel = campaign.BusinessDisplayClaim.RequestedDisplayName,
                CategoryCode = campaign.BusinessDisplayClaim.CategoryCode,
                SemanticPlaceStableId = campaign.BusinessDisplayClaim.SemanticPlaceStableId,
                BadgeText = campaign.BadgeText,
                DetailCardText = campaign.DetailCardText,
                AdvertisementDisclosureRequired = true,
                StartsAtUtc = campaign.StartsAtUtc.UtcDateTime,
                EndsAtUtc = campaign.EndsAtUtc.UtcDateTime
            })
            .ToArrayAsync(cancellationToken);
    }

    private static void ValidateClaimRequest(지역사업장표시ClaimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Required(request.ClaimStableId, "RegionalBusinessDisplayClaimStableIdRequired", 160);
        if (request.PublicBusinessRecordId == Guid.Empty) throw new ArgumentException("PublicBusinessRecordIdRequired");
        if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(request.AdministrativeRegionStableId))
            throw new ArgumentException("AdministrativeDongStableIdInvalid");
        Required(request.SemanticPlaceStableId, "SemanticPlaceStableIdRequired", 200);
        Required(request.RequestedDisplayName, "RegionalBusinessDisplayNameRequired", 120);
        Required(request.CategoryCode, "RegionalBusinessCategoryRequired", 80);
        Required(request.RequestedByUserStableId, "RegionalBusinessDisplayClaimRequesterRequired", 160);
    }

    private static void ValidateCampaignRequest(지역디오라마후원CampaignRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Required(request.CampaignStableId, "RegionalDioramaSponsorshipStableIdRequired", 160);
        Required(request.ClaimStableId, "RegionalBusinessDisplayClaimStableIdRequired", 160);
        var badge = Required(request.BadgeText, "RegionalDioramaSponsorshipDisclosureRequired", 40);
        if (!badge.Contains("광고", StringComparison.Ordinal) && !badge.Contains("후원", StringComparison.Ordinal))
            throw new ArgumentException("RegionalDioramaSponsorshipDisclosureRequired");
        Required(request.DetailCardText, "RegionalDioramaSponsorshipDetailRequired", 500);
        Required(request.RequestedByUserStableId, "RegionalDioramaSponsorshipRequesterRequired", 160);
        if (request.StartsAtUtc >= request.EndsAtUtc)
            throw new ArgumentException("RegionalDioramaSponsorshipPeriodInvalid");
    }

    private static string Required(string? value, string error, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
            throw new ArgumentException(error);
        return value.Trim();
    }
}
