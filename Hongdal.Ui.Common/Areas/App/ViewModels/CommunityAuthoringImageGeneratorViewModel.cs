using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.ViewModel,
    "글 초안을 문맥별 이미지 작업으로 계획하고 선택·생성·상태·첨부 UI 상태를 조율",
    ContractType = typeof(ICommunityAuthoringImageClient),
    FlowOrder = 10,
    Effects = HongdalCodeEffect.NetworkCall
              | HongdalCodeEffect.UiStateMutation
              | HongdalCodeEffect.MayIncurExternalCost,
    Boundary = "문맥 계획은 무료이며 사용자가 선택한 항목만 생성 API로 전달합니다.")]
public sealed partial class CommunityAuthoringImageGeneratorViewModel : ObservableObject
{
    private readonly ICommunityAuthoringImageClient _client;
    private string _draftTitle = string.Empty;
    private string _draftBody = string.Empty;
    private int _maxImages = CommunityAuthoringImageLimits.DefaultPlannedImages;
    private string _defaultAspectRatio = CommunityAuthoringImageAspectRatios.Landscape;
    private bool _isBusy;
    private int _sourceSectionCount;
    private string? _promptVersion;
    private string? _guidance;
    private string? _statusMessage;
    private CommunityComposerMessageKind _statusKind = CommunityComposerMessageKind.Info;

    public CommunityAuthoringImageGeneratorViewModel(ICommunityAuthoringImageClient client)
    {
        _client = client;
    }

    public ObservableCollection<CommunityAuthoringImagePromptItemViewModel> Items { get; } = [];

    public int MaxImages
    {
        get => _maxImages;
        set
        {
            var normalized = Math.Clamp(value, 1, CommunityAuthoringImageLimits.MaximumPlannedImages);
            if (SetProperty(ref _maxImages, normalized))
            {
                OnPropertyChanged(nameof(CanPlan));
            }
        }
    }

    public string DefaultAspectRatio
    {
        get => _defaultAspectRatio;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? CommunityAuthoringImageAspectRatios.Landscape
                : value.Trim();
            SetProperty(ref _defaultAspectRatio, normalized);
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

    public int SourceSectionCount
    {
        get => _sourceSectionCount;
        private set => SetProperty(ref _sourceSectionCount, value);
    }

    public string? PromptVersion
    {
        get => _promptVersion;
        private set => SetProperty(ref _promptVersion, value);
    }

    public string? Guidance
    {
        get => _guidance;
        private set => SetProperty(ref _guidance, value);
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

    public bool HasPlan => Items.Count > 0;

    public bool CanPlan
        => !IsBusy
           && (!string.IsNullOrWhiteSpace(_draftTitle) || !string.IsNullOrWhiteSpace(_draftBody));

    public bool CanGenerateSelected
        => !IsBusy && Items.Any(item => item.IsIncluded && item.NeedsGeneration && item.CanGenerate);

    public bool CanRefresh
        => !IsBusy && Items.Any(item => item.CanRefresh);

    public bool HasSelectedImage
        => Items.Any(item => item.IsSelectedForPost);

    public int IncludedCount
        => Items.Count(item => item.IsIncluded);

    public int SelectedForPostCount
        => Items.Count(item => item.IsSelectedForPost);

    public void PrepareFromDraft(string? title, string? body, bool overwrite = false)
    {
        var nextTitle = title?.Trim() ?? string.Empty;
        var nextBody = body?.Trim() ?? string.Empty;
        var sourceChanged = !string.Equals(_draftTitle, nextTitle, StringComparison.Ordinal)
                            || !string.Equals(_draftBody, nextBody, StringComparison.Ordinal);
        _draftTitle = nextTitle;
        _draftBody = nextBody;

        if (overwrite && HasPlan)
        {
            ClearPlan();
        }

        if (sourceChanged && HasPlan)
        {
            SetStatus(
                "글 내용이 바뀌었습니다. 기존 프롬프트를 유지하거나 문맥 나누기를 다시 실행하세요.",
                CommunityComposerMessageKind.Warning);
        }

        OnPropertyChanged(nameof(CanPlan));
    }

    public async Task<bool> PlanAsync(
        string? title,
        string? body,
        CancellationToken cancellationToken = default)
    {
        PrepareFromDraft(title, body);
        if (!CanPlan)
        {
            SetStatus("제목이나 본문을 입력한 뒤 이미지 문맥을 나눠 주세요.", CommunityComposerMessageKind.Warning);
            return false;
        }

        IsBusy = true;
        SetStatus("글의 제목, 소제목과 문단 흐름을 기준으로 이미지 문맥을 나누고 있습니다.", CommunityComposerMessageKind.Info);
        try
        {
            var result = await _client.PlanAuthoringImagePromptsAsync(
                new CommunityAuthoringImagePromptPlanRequest
                {
                    Title = _draftTitle,
                    Body = _draftBody,
                    MaxImages = MaxImages,
                    AspectRatio = DefaultAspectRatio
                },
                cancellationToken);
            ReplaceItems(result.Segments);
            SourceSectionCount = result.SourceSectionCount;
            PromptVersion = result.PromptVersion;
            Guidance = result.Guidance;
            SetStatus(
                $"{result.SourceSectionCount}개 원문 구간을 {result.Segments.Count}개 이미지 문맥으로 정리했습니다.",
                CommunityComposerMessageKind.Success);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SetStatus($"이미지 문맥을 나누지 못했습니다: {exception.Message}", CommunityComposerMessageKind.Error);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Reset()
    {
        _draftTitle = string.Empty;
        _draftBody = string.Empty;
        ClearPlan();
        SetStatus(null, CommunityComposerMessageKind.Info);
        NotifyStateChanged();
    }

    private void ReplaceItems(IReadOnlyList<CommunityAuthoringImagePromptSegmentDto> segments)
    {
        ClearItems();
        foreach (var segment in segments.OrderBy(segment => segment.Sequence))
        {
            var item = new CommunityAuthoringImagePromptItemViewModel(segment);
            item.PropertyChanged += HandleItemPropertyChanged;
            Items.Add(item);
        }

        NotifyStateChanged();
    }

    private void ClearPlan()
    {
        ClearItems();
        SourceSectionCount = 0;
        PromptVersion = null;
        Guidance = null;
    }

    private void ClearItems()
    {
        foreach (var item in Items)
        {
            item.PropertyChanged -= HandleItemPropertyChanged;
        }

        Items.Clear();
        NotifyStateChanged();
    }

    private void HandleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => NotifyStateChanged();

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(CanPlan));
        OnPropertyChanged(nameof(CanGenerateSelected));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(HasSelectedImage));
        OnPropertyChanged(nameof(IncludedCount));
        OnPropertyChanged(nameof(SelectedForPostCount));
    }

    private void SetStatus(string? message, CommunityComposerMessageKind kind)
    {
        StatusMessage = message;
        StatusKind = kind;
    }
}
