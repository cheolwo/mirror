using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Metadata;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.ViewModel,
    "이미지 문맥 한 건의 편집·생성·선택·오류 상태를 보관",
    FlowOrder = 11,
    Effects = HongdalCodeEffect.UiStateMutation,
    Boundary = "프롬프트나 비율이 바뀌면 이전 생성 결과 선택을 무효화합니다.")]
public sealed class CommunityAuthoringImagePromptItemViewModel : ObservableObject
{
    private string _prompt;
    private string _aspectRatio;
    private bool _isIncluded;
    private bool _isBusy;
    private bool _isSelectedForPost;
    private CommunityAuthoringImageTaskResponse? _task;
    private string? _errorMessage;

    public CommunityAuthoringImagePromptItemViewModel(CommunityAuthoringImagePromptSegmentDto segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        SegmentKey = segment.SegmentKey;
        Sequence = segment.Sequence;
        Title = segment.Title;
        Context = segment.Context;
        _prompt = segment.Prompt;
        _aspectRatio = segment.AspectRatio;
        _isIncluded = segment.IsSelectedByDefault;
    }

    public string SegmentKey { get; }

    public int Sequence { get; }

    public string Title { get; }

    public string Context { get; }

    public string Prompt
    {
        get => _prompt;
        set
        {
            if (SetProperty(ref _prompt, value ?? string.Empty))
            {
                ClearGeneratedState();
                NotifyStateChanged();
            }
        }
    }

    public string AspectRatio
    {
        get => _aspectRatio;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? CommunityAuthoringImageAspectRatios.Landscape
                : value.Trim();
            if (SetProperty(ref _aspectRatio, normalized))
            {
                ClearGeneratedState();
                NotifyStateChanged();
            }
        }
    }

    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (SetProperty(ref _isIncluded, value))
            {
                NotifyStateChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyStateChanged();
            }
        }
    }

    public bool IsSelectedForPost
    {
        get => _isSelectedForPost;
        private set
        {
            if (SetProperty(ref _isSelectedForPost, value))
            {
                NotifyStateChanged();
            }
        }
    }

    public CommunityAuthoringImageTaskResponse? Task
    {
        get => _task;
        private set
        {
            if (SetProperty(ref _task, value))
            {
                NotifyStateChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool CanGenerate
        => !IsBusy && Prompt.Trim().Length >= CommunityAuthoringImageLimits.MinimumPromptLength;

    public bool NeedsGeneration
        => Task is null || Task.StatusCode == CommunityAuthoringImageTaskStatusCodes.Failed;

    public bool CanRefresh => !IsBusy && Task is { IsTerminal: false };

    public bool CanSelectForPost
        => !IsBusy
           && !IsSelectedForPost
           && Task is { IsSuccess: true, ImageUrl: not null };

    internal void SetTask(CommunityAuthoringImageTaskResponse task)
    {
        ArgumentNullException.ThrowIfNull(task);
        Task = task;
        ClearError();
        if (!task.IsSuccess)
        {
            IsSelectedForPost = false;
        }
    }

    internal void SetBusy(bool value)
        => IsBusy = value;

    internal void SetSelectedForPost(bool value)
        => IsSelectedForPost = value && Task is { IsSuccess: true };

    internal void SetError(string message)
        => ErrorMessage = message;

    internal void ClearError()
        => ErrorMessage = null;

    internal void ClearGeneratedState()
    {
        Task = null;
        ClearError();
        IsSelectedForPost = false;
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanGenerate));
        OnPropertyChanged(nameof(NeedsGeneration));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(CanSelectForPost));
    }
}
