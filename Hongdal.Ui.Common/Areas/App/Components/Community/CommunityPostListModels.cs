using Hongdal.Contracts.Common.Community;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public sealed record CommunitySeedPost(
    string Title,
    string Body,
    string Category,
    string Meta,
    string Icon,
    Color Color,
    string Author,
    int RecommendationCount,
    int CommentCount,
    bool HasDiagramPreview);

public sealed record CommunityBoardNavigationItem(
    string Name,
    string Description,
    string Icon,
    int PostCount,
    string Href,
    bool IsSelected = false,
    string PostingAccessCode = CommunityBoardPostingAccessCodes.Mixed)
{
    public string PostingAccessLabel
        => CommunityBoardPostingAccessCodes.DisplayName(PostingAccessCode);

    public Color PostingAccessColor
        => PostingAccessCode switch
        {
            CommunityBoardPostingAccessCodes.Anonymous => Color.Success,
            CommunityBoardPostingAccessCodes.Authenticated => Color.Info,
            CommunityBoardPostingAccessCodes.OperatorOnly => Color.Warning,
            _ => Color.Default
        };
}

public static class CommunitySystemPostDisplay
{
    public static string ShortLabel(string? kind)
        => kind switch
        {
            PlatformCommunitySystemPostKinds.LedgerCompletion => "성립",
            PlatformCommunitySystemPostKinds.KamisPriceBrief => "정보",
            PlatformCommunitySystemPostKinds.Reflection => "성찰",
            PlatformCommunitySystemPostKinds.ActivityDigest => "요약",
            PlatformCommunitySystemPostKinds.PrajnaContent => "반야",
            _ => "자동"
        };

    public static string BadgeLabel(string? kind)
        => kind switch
        {
            PlatformCommunitySystemPostKinds.LedgerCompletion => "자동 성립 기록",
            PlatformCommunitySystemPostKinds.KamisPriceBrief => "자동 가격 정보",
            PlatformCommunitySystemPostKinds.Reflection => "자동 성찰문",
            PlatformCommunitySystemPostKinds.ActivityDigest => "자동 활동 요약",
            PlatformCommunitySystemPostKinds.PrajnaContent => "관리자 선별 반야 자료",
            _ => "자동 작성 글"
        };

    public static string DisclosureTitle(string? kind)
        => kind switch
        {
            PlatformCommunitySystemPostKinds.LedgerCompletion => "비식별 공개본",
            PlatformCommunitySystemPostKinds.KamisPriceBrief => "출처 기반 자동 정보",
            PlatformCommunitySystemPostKinds.Reflection => "시스템 작성 성찰문",
            PlatformCommunitySystemPostKinds.ActivityDigest => "비식별 자동 집계",
            PlatformCommunitySystemPostKinds.PrajnaContent => "외부 출처·비제휴 안내",
            _ => "자동 작성 안내"
        };
}
