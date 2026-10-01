using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class CommunityScheduledPostListViewModel(
    ICommunityPostClient communityService) : 조립ViewModelBase
{
    private IReadOnlyList<PlatformCommunityPostResponse> _items = [];
    private bool _isLoading;
    private long? _cancellingPostId;
    private string? _statusMessage;
    private CommunityComposerMessageKind _statusKind = CommunityComposerMessageKind.Info;

    public IReadOnlyList<PlatformCommunityPostResponse> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public long? CancellingPostId
    {
        get => _cancellingPostId;
        private set => SetProperty(ref _cancellingPostId, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public CommunityComposerMessageKind StatusKind
    {
        get => _statusKind;
        private set => SetProperty(ref _statusKind, value);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        try
        {
            Items = await communityService.GetScheduledPostsAsync(
                status: null,
                take: 50,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SetStatus(
                $"예약 발행 목록을 불러오지 못했습니다: {exception.Message}",
                CommunityComposerMessageKind.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> CancelAsync(
        long postId,
        CancellationToken cancellationToken = default)
    {
        if (postId <= 0 || CancellingPostId.HasValue)
        {
            return false;
        }

        CancellingPostId = postId;
        StatusMessage = null;
        try
        {
            var cancelled = await communityService.CancelScheduledPostAsync(postId, cancellationToken);
            if (cancelled is null)
            {
                SetStatus("예약 취소 응답을 확인하지 못했습니다.", CommunityComposerMessageKind.Error);
                return false;
            }

            Items = Items
                .Select(item => item.Id == cancelled.Id ? cancelled : item)
                .ToArray();
            SetStatus(
                $"게시글 #{cancelled.Id:N0}의 예약 발행을 취소했습니다.",
                CommunityComposerMessageKind.Success);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SetStatus(
                $"예약 발행을 취소하지 못했습니다: {exception.Message}",
                CommunityComposerMessageKind.Error);
            return false;
        }
        finally
        {
            CancellingPostId = null;
        }
    }

    private void SetStatus(string message, CommunityComposerMessageKind kind)
    {
        StatusKind = kind;
        StatusMessage = message;
    }
}
