using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public partial class CommunityVoteService
{
    private static void EnsureInterestVoteSource(CommunityVoteRecord vote, long sourcePostId)
    {
        if (sourcePostId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePostId));
        }

        if (!string.Equals(
                vote.VoteKind,
                CommunityVoteKindCodes.CollectiveActionInterest,
                StringComparison.OrdinalIgnoreCase)
            || vote.SourcePostId != sourcePostId)
        {
            throw new InvalidOperationException("게시글의 참여 관심 모집과 일치하는 투표가 아닙니다.");
        }
    }

    private static CommunityInterestVotePromotionSnapshot ToPromotionSnapshot(CommunityVoteRecord vote)
    {
        var roleByOptionId = vote.Options.ToDictionary(
            option => option.OptionId,
            option => ParseParticipationRoleCode(option.ProductKey),
            StringComparer.OrdinalIgnoreCase);
        var participants = vote.Votes
            .OrderBy(cast => cast.VotedAtUtc)
            .Select(cast => new CommunityInterestVoteParticipantSnapshot
            {
                ParticipantReference = cast.VoterHash,
                UserId = NormalizeOptional(cast.VoterUserId),
                DisplayName = Normalize(cast.VoterDisplayName, "익명 참여자"),
                RoleCodes = cast.OptionIds
                    .Select(optionId => roleByOptionId.GetValueOrDefault(optionId))
                    .Where(roleCode => !string.IsNullOrWhiteSpace(roleCode))
                    .Select(roleCode => roleCode!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .ToArray();
        var roleCounts = participants
            .SelectMany(participant => participant.RoleCodes)
            .GroupBy(roleCode => roleCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var evidenceLines = participants
            .OrderBy(participant => participant.ParticipantReference, StringComparer.Ordinal)
            .Select(participant => $"{participant.ParticipantReference}:{string.Join(',', participant.RoleCodes)}")
            .Prepend(vote.Id.ToString("D"));

        return new CommunityInterestVotePromotionSnapshot
        {
            VoteId = vote.Id,
            SourcePostId = vote.SourcePostId!.Value,
            Status = vote.Status,
            CommunityLedgerId = vote.CommunityLedgerId,
            ParticipantCount = participants.Length,
            EvidenceSnapshotHash = Hash(string.Join('\n', evidenceLines)),
            Participants = participants,
            RoleCounts = roleCounts
        };
    }

    private static string? ParseParticipationRoleCode(string? productKey)
    {
        const string prefix = "community-role:";
        if (string.IsNullOrWhiteSpace(productKey)
            || !productKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var roleCode = productKey[prefix.Length..].Trim();
        return CommunityPostParticipationRoleCodes.All.FirstOrDefault(candidate =>
            string.Equals(candidate, roleCode, StringComparison.OrdinalIgnoreCase));
    }
}
