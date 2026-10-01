using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class PlatformCommunityBoardForm : ObservableObject
{
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _requestedBy = string.Empty;
    private string _requestReason = string.Empty;

    public string Title { get => _title; set => SetProperty(ref _title, value ?? string.Empty); }
    public string Description { get => _description; set => SetProperty(ref _description, value ?? string.Empty); }
    public string RequestedBy { get => _requestedBy; set => SetProperty(ref _requestedBy, value ?? string.Empty); }
    public string RequestReason { get => _requestReason; set => SetProperty(ref _requestReason, value ?? string.Empty); }

    public bool IsValid
        => !string.IsNullOrWhiteSpace(Title)
           && !string.IsNullOrWhiteSpace(RequestedBy)
           && !string.IsNullOrWhiteSpace(RequestReason);

    public void ResetAfterSubmit()
    {
        Title = string.Empty;
        Description = string.Empty;
        RequestReason = string.Empty;
    }
}

/// <summary>
/// 승인 게시판, 개설 신청과 운영자 검토 상태를 소유합니다.
/// </summary>
public sealed class PlatformCommunityBoardWorkspaceViewModel : 조립ViewModelBase
{
    private readonly PlatformCommunityService _communityService;
    private bool _isLoading;
    private bool _isSavingRequest;
    private string _indexSearchText = string.Empty;

    public PlatformCommunityBoardWorkspaceViewModel(PlatformCommunityService communityService)
    {
        _communityService = communityService;
        Form = 하위ViewModel등록(new PlatformCommunityBoardForm());
    }

    public List<PlatformCommunityBoardResponse> ApprovedBoards { get; } = [];
    public List<PlatformCommunityBoardResponse> PendingBoardRequests { get; } = [];
    public Dictionary<long, string> ReviewMemos { get; } = [];
    public PlatformCommunityBoardForm Form { get; }
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public bool IsSavingRequest { get => _isSavingRequest; set => SetProperty(ref _isSavingRequest, value); }
    public string IndexSearchText
    {
        get => _indexSearchText;
        set => SetProperty(ref _indexSearchText, value ?? string.Empty);
    }

    public void ReplaceApproved(IEnumerable<PlatformCommunityBoardResponse> boards)
    {
        ApprovedBoards.Clear();
        ApprovedBoards.AddRange(boards);
        OnPropertyChanged(nameof(ApprovedBoards));
    }

    public void ReplacePending(IEnumerable<PlatformCommunityBoardResponse> boards)
    {
        PendingBoardRequests.Clear();
        PendingBoardRequests.AddRange(boards);
        foreach (var board in PendingBoardRequests)
        {
            ReviewMemos.TryAdd(board.Id, string.Empty);
        }

        OnPropertyChanged(nameof(PendingBoardRequests));
        OnPropertyChanged(nameof(ReviewMemos));
    }

    public async Task LoadAsync(
        string appKey,
        bool canManageCommunityPosts,
        CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var approved = await _communityService.GetBoardsAsync(
                appKey,
                PlatformCommunityBoardRequestStatuses.Approved,
                cancellationToken);
            ReplaceApproved(approved.Items);

            if (canManageCommunityPosts)
            {
                var pending = await _communityService.GetBoardsAsync(
                    appKey,
                    PlatformCommunityBoardRequestStatuses.Pending,
                    cancellationToken);
                ReplacePending(pending.Items);
            }
            else
            {
                ReplacePending([]);
            }
        }
        catch
        {
            // 게시판 신청 기능이 연결되지 않아도 기본 게시글 목록은 계속 사용합니다.
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<PlatformCommunityCommandResult> SubmitRequestAsync(
        string appKey,
        bool canManageCommunityPosts,
        CancellationToken cancellationToken = default)
    {
        if (!Form.IsValid)
        {
            return new(
                false,
                "게시판 이름, 신청자, 개설 이유를 입력하세요.",
                CommunityComposerMessageKind.Warning);
        }

        IsSavingRequest = true;
        try
        {
            await _communityService.CreateBoardRequestAsync(
                new PlatformCommunityBoardCreateRequest
                {
                    AppKey = appKey,
                    Title = Form.Title,
                    Description = Form.Description,
                    RequestedBy = Form.RequestedBy,
                    RequestReason = Form.RequestReason
                },
                cancellationToken);
            Form.ResetAfterSubmit();
            await LoadAsync(appKey, canManageCommunityPosts, cancellationToken);
            return new(
                true,
                "게시판 개설 신청을 접수했습니다. 운영자 승인 후 게시판 목록에 표시됩니다.",
                CommunityComposerMessageKind.Success);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"게시판 개설 신청에 실패했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
        finally
        {
            IsSavingRequest = false;
        }
    }

    public async Task<PlatformCommunityCommandResult> ReviewAsync(
        string appKey,
        PlatformCommunityBoardResponse board,
        bool approve,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var memo = ReviewMemos.TryGetValue(board.Id, out var value) ? value : string.Empty;
            if (approve)
            {
                await _communityService.ApproveBoardAsync(board.Id, memo, cancellationToken);
            }
            else
            {
                await _communityService.RejectBoardAsync(board.Id, memo, cancellationToken);
            }

            await LoadAsync(appKey, canManageCommunityPosts: true, cancellationToken);
            return new(
                true,
                approve
                    ? $"'{board.Title}' 게시판을 승인했습니다."
                    : $"'{board.Title}' 게시판 신청을 반려했습니다.",
                CommunityComposerMessageKind.Success);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"게시판 검토 처리에 실패했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }
}
