using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum CommunityPostViewMode
{
    List,
    Cards
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Ui,
    HongdalModuleKind.ClientFeature,
    "선택한 게시판의 글을 목록·카드 형식으로 검색하고 상세 선택 상태를 관리",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "특정 거래 상대의 적합성이나 성사 가능성으로 게시글을 추천·순위화하지 않습니다.")]
public sealed class CommunityPostListPageViewModel(
    ICommunityPostClient communityService) : PageViewModelBase
{
    private string _appKey = string.Empty;
    private readonly List<PlatformCommunityPostResponse> _items = [];
    private string _selectedBoard = "전체";
    private string _selectedListFilter = "전체글";
    private CommunityPostViewMode _viewMode;
    private string _searchText = string.Empty;
    private long? _selectedPostId;

    public IReadOnlyList<PlatformCommunityPostResponse> Items => _items;

    public IReadOnlyList<PlatformCommunityPostResponse> VisibleItems
        => _items
            .Where(post => string.Equals(SelectedBoard, "전체", StringComparison.OrdinalIgnoreCase)
                ? !CommunityBoardCatalog.IsProtectedCategory(post.Category)
                : CommunityBoardCatalog.MatchesCategory(SelectedBoard, post.Category))
            .Where(MatchesListFilter)
            .Where(MatchesSearch)
            .OrderByDescending(post => post.IsOperatorPinned)
            .ThenByDescending(post => post.IsTrending)
            .ThenByDescending(post => post.LastEngagedAtUtc ?? post.CreatedAtUtc)
            .ToArray();

    public string SelectedBoard
    {
        get => _selectedBoard;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "전체" : value.Trim();
            if (SetProperty(ref _selectedBoard, normalized))
            {
                SelectedPostId = null;
                OnPropertyChanged(nameof(VisibleItems));
            }
        }
    }

    public string SelectedListFilter
    {
        get => _selectedListFilter;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "전체글" : value.Trim();
            if (SetProperty(ref _selectedListFilter, normalized))
            {
                SelectedPostId = null;
                OnPropertyChanged(nameof(VisibleItems));
            }
        }
    }

    public CommunityPostViewMode ViewMode
    {
        get => _viewMode;
        set => SetProperty(ref _viewMode, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            var normalized = value ?? string.Empty;
            if (SetProperty(ref _searchText, normalized))
            {
                SelectedPostId = null;
                OnPropertyChanged(nameof(VisibleItems));
            }
        }
    }

    public long? SelectedPostId
    {
        get => _selectedPostId;
        set => SetProperty(ref _selectedPostId, value);
    }

    public void Configure(string appKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appKey);
        _appKey = appKey.Trim();
    }

    public void Replace(PlatformCommunityPostResponse post)
    {
        var index = _items.FindIndex(item => item.Id == post.Id);
        if (index < 0)
        {
            return;
        }

        _items[index] = post;
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(VisibleItems));
    }

    public async Task<PlatformCommunityPostResponse?> RefreshItemAsync(
        long postId,
        CancellationToken cancellationToken = default)
    {
        var detail = await communityService.GetPostAsync(postId, cancellationToken);
        if (detail is not null)
        {
            Replace(detail);
        }

        return detail;
    }

    protected override async Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_appKey))
        {
            throw new InvalidOperationException("커뮤니티 게시글 목록 AppKey가 설정되지 않았습니다.");
        }

        var result = await communityService.GetPostsAsync(_appKey, cancellationToken);
        _items.Clear();
        _items.AddRange(result.Items);
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(VisibleItems));
    }

    private bool MatchesListFilter(PlatformCommunityPostResponse post)
        => SelectedListFilter switch
        {
            "공지" => post.IsOperatorPinned,
            "추천글" => post.IsTrending || post.RecommendationCount >= 5,
            _ => true
        };

    private bool MatchesSearch(PlatformCommunityPostResponse post)
    {
        var searchText = SearchText.Trim();
        return searchText.Length == 0
               || ContainsSearchText(post.Title, searchText)
               || ContainsSearchText(post.Body, searchText)
               || ContainsSearchText(post.Nickname, searchText)
               || ContainsSearchText(post.AuthorDisplayCountryCode, searchText)
               || ContainsSearchText(post.AuthorDisplayCountryName, searchText)
               || ContainsSearchText(post.Category, searchText)
               || ContainsSearchText(post.WorkflowTag, searchText)
               || ContainsSearchText(post.RoleTag, searchText);
    }

    private static bool ContainsSearchText(string? value, string searchText)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(searchText, StringComparison.OrdinalIgnoreCase);
}
