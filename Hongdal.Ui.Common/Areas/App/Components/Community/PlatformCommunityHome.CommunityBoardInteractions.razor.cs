using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private static string ResolveCommunityBoardIcon(string board)
        => board switch
        {
            "전체" => Icons.Material.Filled.DynamicFeed,
            PlatformCommunityPostCategories.Sales => Icons.Material.Filled.Storefront,
            "시스템 다이어그램" => Icons.Material.Filled.AccountTree,
            "운송 실무" => Icons.Material.Filled.LocalShipping,
            "업무 질문" => Icons.Material.Filled.HelpOutline,
            "업무 기록" => Icons.Material.Filled.TaskAlt,
            "생활 원장" => Icons.Material.Filled.Assignment,
            "개선 제안" => Icons.Material.Filled.Lightbulb,
            "신고/분쟁" => Icons.Material.Filled.ReportProblem,
            _ => Icons.Material.Filled.Forum
        };

    private static string ResolveCommunityBoardDescription(string board)
        => board switch
        {
            "전체" => "모든 커뮤니티 글",
            PlatformCommunityPostCategories.Sales => "상품과 거래 조건을 댓글로 확인",
            "시스템 다이어그램" => "README형 흐름 공유",
            "운송 실무" => "배차·운송 경험",
            "업무 질문" => "함께 답을 찾는 공간",
            "업무 기록" => "처리 과정과 결과",
            "생활 원장" => "사람 중심 요청과 약속",
            "개선 제안" => "서비스를 바꾸는 의견",
            "신고/분쟁" => "보호가 필요한 이야기",
            _ => "구성원이 만든 게시판"
        };

    private int GetCommunityBoardPostCount(string board)
    {
        if (string.Equals(board, "전체", StringComparison.OrdinalIgnoreCase))
        {
            return posts.Count + SeedPosts.Count;
        }

        return posts.Count(post => string.Equals(post.Category, board, StringComparison.OrdinalIgnoreCase))
             + SeedPosts.Count(post => string.Equals(post.Category, board, StringComparison.OrdinalIgnoreCase));
    }

    private CommunityBoardNavigationItem AllBoardNavigationItem
        => BuildCommunityBoardNavigationItem("전체");

    private IReadOnlyList<CommunityBoardNavigationItem> VisibleBoardIndexItems
        => VisibleBoardIndexOptions
            .Select(BuildCommunityBoardNavigationItem)
            .ToArray();

    private IReadOnlyList<CommunityBoardNavigationItem> PostIndexBoardItems
        => CommunityBoardOptions
            .Select(BuildCommunityBoardNavigationItem)
            .ToArray();

    private CommunityBoardNavigationItem BuildCommunityBoardNavigationItem(string board)
        => new(
            board,
            string.Equals(board, "전체", StringComparison.OrdinalIgnoreCase)
                ? "모든 게시판에서 올라온 글을 한 번에 봅니다."
                : ResolveCommunityBoardDescription(board),
            ResolveCommunityBoardIcon(board),
            GetCommunityBoardPostCount(board),
            BuildCommunityBoardHref(board),
            string.Equals(selectedBoardFilter, board, StringComparison.OrdinalIgnoreCase),
            ResolvePostingAccessCode(board));

    private static string ResolvePostingAccessCode(string board)
        => string.Equals(board, "전체", StringComparison.OrdinalIgnoreCase)
            ? CommunityBoardPostingAccessCodes.Mixed
            : CommunityBoardCatalog.Find(board)?.PostingAccessCode
              ?? CommunityBoardPostingAccessCodes.Authenticated;

    private void SelectBoardFilter(string board)
    {
        selectedBoardFilter = board;
        selectedForumPostId = null;
        selectedForumSeedPostTitle = null;
        if (!string.Equals(board, "전체", StringComparison.OrdinalIgnoreCase))
        {
            form.Category = board;
        }
    }

    private static string BuildCommunityBoardHref(string board)
        => $"/community/boards?board={Uri.EscapeDataString(board)}";

    private void HandleBoardIndexSearchChanged(string value)
        => boardIndexSearchText = value;

    private void OpenCommunityPostPage(PlatformCommunityPostResponse post)
        => Navigation.NavigateTo($"/community/posts/{post.Id}");

    private void OpenCommunitySeedPostPage(CommunitySeedPost post)
        => Navigation.NavigateTo(
            $"/community/workspace?seed={Uri.EscapeDataString(post.Title)}");

    private void OpenCommunityComposePage()
        => Navigation.NavigateTo(
            string.Equals(selectedBoardFilter, "전체", StringComparison.OrdinalIgnoreCase)
                ? "/community/write"
                : $"/community/write?board={Uri.EscapeDataString(selectedBoardFilter)}");

    private bool MatchesForumListFilter(CommunitySeedPost post)
        => selectedForumListFilter switch
        {
            "공지" => false,
            "추천글" => post.RecommendationCount >= 5,
            _ => true
        };

    private bool MatchesCommunityPostSearch(CommunitySeedPost post)
    {
        var searchText = communityPostSearchText.Trim();
        return searchText.Length == 0
            || ContainsCommunitySearchText(post.Title, searchText)
            || ContainsCommunitySearchText(post.Body, searchText)
            || ContainsCommunitySearchText(post.Author, searchText)
            || ContainsCommunitySearchText(post.Category, searchText);
    }

    private static bool ContainsCommunitySearchText(string? value, string searchText)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private void SelectForumListFilter(string filter)
    {
        selectedForumListFilter = filter;
        selectedForumPostId = null;
        selectedForumSeedPostTitle = null;
    }

    private void SelectCommunityPostViewMode(CommunityPostViewMode viewMode)
        => communityPostViewMode = viewMode;

    private async Task ToggleForumPostAsync(PlatformCommunityPostResponse post)
    {
        if (selectedForumPostId == post.Id)
        {
            selectedForumPostId = null;
            return;
        }

        selectedForumPostId = post.Id;
        selectedForumSeedPostTitle = null;

        try
        {
            await LoadPostDetailAsync(post.Id);
        }
        catch (HttpRequestException)
        {
            statusSeverity = Severity.Warning;
            statusMessage = "게시글의 원장 정보를 불러오지 못했습니다.";
        }
    }

    private void 실시간베스트글열기(공통홈베스트글요약 베스트글)
    {
        selectedBoardFilter = 베스트글.분류;
        selectedForumListFilter = "전체글";
        communityPostSearchText = string.Empty;
        selectedForumPostId = 베스트글.게시글Id;
        selectedForumSeedPostTitle = 베스트글.추천글제목;
    }

    private void ToggleForumSeedPost(CommunitySeedPost post)
    {
        selectedForumSeedPostTitle = string.Equals(selectedForumSeedPostTitle, post.Title, StringComparison.Ordinal)
            ? null
            : post.Title;
        selectedForumPostId = null;
    }

    private bool IsForumPostSelected(PlatformCommunityPostResponse post)
        => selectedForumPostId == post.Id;

    private bool IsForumSeedPostSelected(CommunitySeedPost post)
        => string.Equals(selectedForumSeedPostTitle, post.Title, StringComparison.Ordinal);

}
