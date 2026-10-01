using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Models;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
private static readonly IReadOnlyList<string> ForumListFilterOptions = ["전체글", "추천글", "공지"];

    private bool IsDiagramMode => DiagramPalette.IsDiagramMode;

    private bool IsCompactHomeSummaryVisible
        => UseCompactHomeSummary && isCompactHomeSummary && !isWorkMode && !IsDiagramMode;

    private string HomeRootClass => isLedgerPickerOpen || isLedgerDetailOpen || isOrderLedgerHierarchyOpen
        ? "py-4 platform-community-home platform-home--ledger-picker"
        : IsDiagramMode
        ? "py-4 platform-community-home platform-home--diagram"
        : isWorkMode
        ? "py-4 platform-community-home platform-home--work"
        : IsCompactHomeSummaryVisible
        ? "py-4 platform-community-home platform-home--summary"
        : "py-4 platform-community-home platform-home--community";

    private string CommunityGridClass => IsDiagramMode
        ? "platform-community-main-grid platform-community-main-grid--diagram"
        : isWorkMode
        ? "platform-community-main-grid platform-home-section-hidden"
        : "platform-community-main-grid";

    private string WorkPanelClass => isWorkMode && !IsDiagramMode
        ? "pa-4 platform-work-panel"
        : "pa-4 platform-work-panel platform-home-section-hidden";

    private string CurrentModeLabel => IsDiagramMode ? "다이어그램 모드" : isWorkMode ? "업무 모드" : "커뮤니티 모드";

    private Color CurrentModeColor => IsDiagramMode ? Color.Secondary : isWorkMode ? Color.Success : Color.Primary;

    private IReadOnlyList<string> CommunityBoardOptions
        => new[] { "전체" }
            .Concat(BoardCategoryOptions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private IReadOnlyList<string> VisibleBoardIndexOptions
        => CommunityBoardOptions
            .Where(board => !string.Equals(board, "전체", StringComparison.OrdinalIgnoreCase))
            .Where(board => string.IsNullOrWhiteSpace(boardIndexSearchText)
                || board.Contains(boardIndexSearchText.Trim(), StringComparison.OrdinalIgnoreCase)
                || ResolveCommunityBoardDescription(board).Contains(
                    boardIndexSearchText.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private IReadOnlyList<PlatformCommunityPostResponse> VisiblePosts
        => ViewModel.PostList.VisibleItems;

    private IReadOnlyList<CommunitySeedPost> VisibleSeedPosts
        => SeedPosts
            .Where(post => string.Equals(selectedBoardFilter, "전체", StringComparison.OrdinalIgnoreCase)
                || string.Equals(post.Category, selectedBoardFilter, StringComparison.OrdinalIgnoreCase))
            .Where(MatchesForumListFilter)
            .Where(MatchesCommunityPostSearch)
            .ToArray();

    private int ForumVisiblePostCount => VisiblePosts.Count + VisibleSeedPosts.Count;

    private IReadOnlyList<공통홈베스트글요약> 실시간베스트글
        => posts
            .Select(post => new 공통홈베스트글요약(
                post.Id,
                null,
                post.Title,
                post.Category,
                DisplayPostNickname(post),
                post.RecommendationCount,
                post.CommentCount,
                post.IsTrending,
                post.LastEngagedAtUtc ?? post.CreatedAtUtc))
            .Concat(SeedPosts.Select(post => new 공통홈베스트글요약(
                null,
                post.Title,
                post.Title,
                post.Category,
                post.Author,
                post.RecommendationCount,
                post.CommentCount,
                false,
                DateTime.MinValue)))
            .OrderByDescending(post => post.실시간인기)
            .ThenByDescending(post => (post.추천수 * 3) + (post.댓글수 * 2))
            .ThenByDescending(post => post.최근활동일시)
            .Take(3)
            .ToArray();

    private int 공통홈전체글수 => posts.Count + SeedPosts.Count;

    private IReadOnlyList<string> 공통홈게시판명목록
        => CommunityBoardOptions
            .Where(board => !string.Equals(board, "전체", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToArray();

    private int 공통홈보유상품수
        => DecorationState.Products.Count(DecorationState.IsProductOwned);

    private string 공통홈현재테마명
        => DecorationState.Products.FirstOrDefault(product =>
               string.Equals(
                   product.PackKey,
                   DecorationState.ActiveHomeThemePackKey,
                   StringComparison.OrdinalIgnoreCase))?.Title
           ?? "홍달 기본 홈";

    private IReadOnlyList<string> 공통홈추천상품명목록
        => DecorationState.Products
            .OrderByDescending(DecorationState.IsProductActive)
            .ThenByDescending(product => !DecorationState.IsProductOwned(product))
            .Select(product => product.Title)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();

    private string CurrentCommunityBoardTitle
        => string.Equals(selectedBoardFilter, "전체", StringComparison.OrdinalIgnoreCase)
            ? "살뜰 게시판"
            : $"{selectedBoardFilter} 게시판";

    private string CurrentCommunityBoardDescription
        => string.Equals(selectedBoardFilter, "전체", StringComparison.OrdinalIgnoreCase)
            ? "질문과 경험을 나누고, 필요한 일은 원장과 업무 흐름으로 이어가는 공간입니다."
            : ResolveCommunityBoardDescription(selectedBoardFilter);
}
