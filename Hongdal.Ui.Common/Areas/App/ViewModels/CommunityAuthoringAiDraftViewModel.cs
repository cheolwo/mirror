using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Content;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class CommunityAuthoringAiDraftViewModel(
    ICommunityInformationReviewClient client) : ObservableObject
{
    private IReadOnlyList<CommunityInformationSourceDto> _sources = [];
    private string _objective = "공개 자료를 바탕으로 사실과 미확정 조건을 구분하고, 사람들이 비구속적으로 의견을 모을 수 있는 서원 글을 작성한다.";
    private string _topic = string.Empty;
    private string _sourceKey = string.Empty;
    private string _countryCode = string.Empty;
    private string _searchText = string.Empty;
    private DateTime? _startDate = DateTime.Today.AddDays(-29);
    private DateTime? _endDate = DateTime.Today;
    private bool _includeInformationCollection = true;
    private bool _includeYouTubeSocialResearch;
    private int _maxEvidenceItems = 10;
    private bool _isLoading;
    private CommunityAuthoringAiDraftResponse? _result;
    private string? _statusMessage;
    private CommunityComposerMessageKind _statusKind = CommunityComposerMessageKind.Info;

    public IReadOnlyList<CommunityInformationSourceDto> Sources
    {
        get => _sources;
        private set => SetProperty(ref _sources, value);
    }

    public string Objective
    {
        get => _objective;
        set => SetInput(ref _objective, value ?? string.Empty);
    }

    public string Topic
    {
        get => _topic;
        set => SetInput(ref _topic, value ?? string.Empty);
    }

    public string SourceKey
    {
        get => _sourceKey;
        set => SetInput(ref _sourceKey, value ?? string.Empty);
    }

    public string CountryCode
    {
        get => _countryCode;
        set => SetInput(ref _countryCode, value ?? string.Empty);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetInput(ref _searchText, value ?? string.Empty);
    }

    public DateTime? StartDate
    {
        get => _startDate;
        set => SetInput(ref _startDate, value?.Date);
    }

    public DateTime? EndDate
    {
        get => _endDate;
        set => SetInput(ref _endDate, value?.Date);
    }

    public bool IncludeInformationCollection
    {
        get => _includeInformationCollection;
        set => SetInput(ref _includeInformationCollection, value);
    }

    public bool IncludeYouTubeSocialResearch
    {
        get => _includeYouTubeSocialResearch;
        set => SetInput(ref _includeYouTubeSocialResearch, value);
    }

    public int MaxEvidenceItems
    {
        get => _maxEvidenceItems;
        set => SetInput(ref _maxEvidenceItems, Math.Clamp(value, 1, 20));
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public CommunityAuthoringAiDraftResponse? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
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

    public bool CanApply => Result is { Success: true, Draft: not null, CanPublish: false };

    public void SetAvailableSources(IReadOnlyList<CommunityInformationSourceDto> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        Sources = sources
            .OrderBy(source => source.SourceType)
            .ThenBy(source => source.DisplayName)
            .ToArray();
    }

    public void PrepareFilters(
        string? sourceKey,
        string? countryCode,
        string? searchText,
        string? draftTitle)
    {
        if (string.IsNullOrWhiteSpace(SourceKey))
        {
            _sourceKey = sourceKey?.Trim() ?? string.Empty;
            OnPropertyChanged(nameof(SourceKey));
        }

        if (string.IsNullOrWhiteSpace(CountryCode))
        {
            _countryCode = countryCode?.Trim() ?? string.Empty;
            OnPropertyChanged(nameof(CountryCode));
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            _searchText = searchText?.Trim() ?? string.Empty;
            OnPropertyChanged(nameof(SearchText));
        }

        if (string.IsNullOrWhiteSpace(Topic) && !string.IsNullOrWhiteSpace(draftTitle))
        {
            _topic = draftTitle.Trim();
            OnPropertyChanged(nameof(Topic));
        }
    }

    public async Task<bool> GenerateAsync(
        IReadOnlyList<CommunityAuthoringAiContextSectionDto> contextSections,
        YouTubeSocialContextResearchRequest? socialResearchRequest,
        CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return false;
        }

        var toolKeys = new List<string>();
        if (IncludeInformationCollection)
        {
            toolKeys.Add(CommunityAuthoringAiToolKeys.InformationCollection);
        }

        if (IncludeYouTubeSocialResearch)
        {
            if (socialResearchRequest is null)
            {
                SetStatus(
                    "YouTube·SNS 탭에서 영상과 조사 원천을 먼저 입력해 주세요.",
                    CommunityComposerMessageKind.Warning);
                return false;
            }

            toolKeys.Add(CommunityAuthoringAiToolKeys.YouTubeSocialContext);
        }

        if (toolKeys.Count == 0)
        {
            SetStatus("자료 조회 도구를 하나 이상 선택해 주세요.", CommunityComposerMessageKind.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(Objective))
        {
            SetStatus("작성할 글의 목적을 입력해 주세요.", CommunityComposerMessageKind.Warning);
            return false;
        }

        IsLoading = true;
        Result = null;
        SetStatus("선택한 자료를 조회하고 근거 기반 초안을 만들고 있습니다.", CommunityComposerMessageKind.Info);
        try
        {
            Result = await client.GenerateAiDraftAsync(
                new CommunityAuthoringAiDraftRequest
                {
                    Objective = Objective.Trim(),
                    Topic = NormalizeOptional(Topic),
                    SourceKey = NormalizeOptional(SourceKey),
                    CountryCode = NormalizeOptional(CountryCode),
                    SearchText = NormalizeOptional(SearchText),
                    StartDate = StartDate.HasValue ? DateOnly.FromDateTime(StartDate.Value) : null,
                    EndDate = EndDate.HasValue ? DateOnly.FromDateTime(EndDate.Value) : null,
                    MaxEvidenceItems = MaxEvidenceItems,
                    ToolKeys = toolKeys,
                    YouTubeSocialContext = socialResearchRequest,
                    ContextSections = contextSections
                },
                cancellationToken);
            SetStatus(
                Result.Message,
                Result.Success
                    ? CommunityComposerMessageKind.Success
                    : Result.StatusCode == CommunityAuthoringAiDraftStatusCodes.LlmBlocked
                        ? CommunityComposerMessageKind.Warning
                        : CommunityComposerMessageKind.Error);
            return Result.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SetStatus($"LLM 글 초안을 만들지 못했습니다: {exception.Message}", CommunityComposerMessageKind.Error);
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SetInputError(string message)
        => SetStatus(message, CommunityComposerMessageKind.Warning);

    public void ResetResult()
    {
        Result = null;
        SetStatus(null, CommunityComposerMessageKind.Info);
    }

    private void SetInput<T>(ref T field, T value)
    {
        if (SetProperty(ref field, value))
        {
            ResetResult();
        }
    }

    private void SetStatus(string? message, CommunityComposerMessageKind kind)
    {
        StatusMessage = message;
        StatusKind = kind;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
