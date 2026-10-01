using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

internal sealed record 공동구매원장빈역할알림Context(
    IReadOnlyList<CommunityPostPartyRoleSlotResponse> OpenRoleSlots,
    string DeepLink)
{
    public bool Enabled => OpenRoleSlots.Count > 0;

    public string BuildBody(string ledgerTitle)
    {
        var roleSummary = BuildRoleSummary(OpenRoleSlots);
        return $"{ledgerTitle}에 관심이 모여 비구속적 가원장이 만들어졌습니다. "
               + $"현재 비어 있는 역할은 {roleSummary}입니다. "
               + "기존 참여자는 게시글에서 맡을 수 있는 역할을 확인하고 비구속적 참여 의향을 표시할 수 있습니다. "
               + "이 알림만으로 역할이 배정되거나 확정되지는 않습니다. "
               + "아직 주문·계약·배차·운송 주선은 확정되지 않았습니다.";
    }

    private static string BuildRoleSummary(
        IReadOnlyList<CommunityPostPartyRoleSlotResponse> openRoleSlots)
    {
        const int visibleRoleCount = 3;
        var labels = openRoleSlots
            .Take(visibleRoleCount)
            .Select(slot => slot.Label)
            .ToArray();
        var remainingCount = openRoleSlots.Count - labels.Length;
        return remainingCount > 0
            ? $"{string.Join(", ", labels)} 외 {remainingCount}개"
            : string.Join(", ", labels);
    }
}

internal static class 공동구매원장빈역할알림Policy
{
    private const string SourceCommunityPostIdKey = "SourceCommunityPostId";
    private const string SourcePostIdKey = "SourcePostId";

    public static 공동구매원장빈역할알림Context Resolve(
        커뮤니티원장Dto ledger,
        bool provisionalCreated,
        string fallbackDeepLink)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackDeepLink);

        if (!provisionalCreated)
        {
            return new([], fallbackDeepLink);
        }

        var openRoleSlots = CommunityPostProfessionalParticipationProjection
            .BuildPartyFormationResponse(ledger, CommunityDisplayLanguageCodes.Korean)
            .RoleSlots
            .Where(slot => slot.ConfirmedParticipantCount == 0)
            .OrderByDescending(slot => slot.IsRequired)
            .ThenByDescending(slot => slot.InterestCount)
            .ThenBy(slot => slot.Label, StringComparer.Ordinal)
            .ToArray();
        if (openRoleSlots.Length == 0)
        {
            return new([], fallbackDeepLink);
        }

        return new(openRoleSlots, ResolveParticipationDeepLink(ledger, fallbackDeepLink));
    }

    private static string ResolveParticipationDeepLink(
        커뮤니티원장Dto ledger,
        string fallbackDeepLink)
    {
        if (ledger.외부참조.TryGetValue(SourceCommunityPostIdKey, out var externalSourcePostId)
            && TryBuildCommunityPostDeepLink(externalSourcePostId, out var externalDeepLink))
        {
            return externalDeepLink;
        }

        var blockSourcePostId = ledger.블록목록
            .Select(block => block.Data.GetValueOrDefault(SourcePostIdKey))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return TryBuildCommunityPostDeepLink(blockSourcePostId, out var blockDeepLink)
            ? blockDeepLink
            : fallbackDeepLink;
    }

    private static bool TryBuildCommunityPostDeepLink(string? value, out string deepLink)
    {
        if (long.TryParse(value, out var postId) && postId > 0)
        {
            deepLink = $"/community/posts/{postId}";
            return true;
        }

        deepLink = string.Empty;
        return false;
    }
}
