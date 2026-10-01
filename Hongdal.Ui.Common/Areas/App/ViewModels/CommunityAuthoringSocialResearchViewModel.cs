using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Content;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class CommunitySocialResearchSourceViewModel : ObservableObject
{
    private bool _isSelected;
    private string _startUrlsText = string.Empty;

    public CommunitySocialResearchSourceViewModel(
        SocialMediaResearchSourceDto source,
        bool isSelected)
    {
        Source = source;
        _isSelected = isSelected;
    }

    public SocialMediaResearchSourceDto Source { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string StartUrlsText
    {
        get => _startUrlsText;
        set => SetProperty(ref _startUrlsText, value ?? string.Empty);
    }

    public IReadOnlyList<string> StartUrls
        => CommunityAuthoringSocialResearchViewModel.SplitValues(StartUrlsText);
}

public sealed class CommunityAuthoringSocialResearchViewModel(
    ICommunityInformationReviewClient client) : ObservableObject
{
    private IReadOnlyList<CommunitySocialResearchSourceViewModel> _sources = [];
    private string _videoReference = string.Empty;
    private string _searchTermsText = string.Empty;
    private string _adjacentTopicsText = string.Empty;
    private string _countryCode = string.Empty;
    private string _languageCode = string.Empty;
    private int _takePerSource = 8;
    private bool _isLoading;
    private string? _errorMessage;
    private string? _statusMessage;
    private YouTubeSocialContextResearchResponse? _result;
    private string _workspaceId = string.Empty;
    private long _workspaceRevision;
    private string _workspaceStatus = string.Empty;
    private bool _isSavingWorkspace;
    private YouTubeSocialContextWorkspaceDto? _workspace;
    private IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto> _savedWorkspaces = [];

    public IReadOnlyList<CommunitySocialResearchSourceViewModel> Sources
    {
        get => _sources;
        private set => SetProperty(ref _sources, value);
    }

    public string VideoReference
    {
        get => _videoReference;
        set => SetProperty(ref _videoReference, value ?? string.Empty);
    }

    public string SearchTermsText
    {
        get => _searchTermsText;
        set => SetProperty(ref _searchTermsText, value ?? string.Empty);
    }

    public string AdjacentTopicsText
    {
        get => _adjacentTopicsText;
        set => SetProperty(ref _adjacentTopicsText, value ?? string.Empty);
    }

    public string CountryCode
    {
        get => _countryCode;
        set => SetProperty(ref _countryCode, value ?? string.Empty);
    }

    public string LanguageCode
    {
        get => _languageCode;
        set => SetProperty(ref _languageCode, value ?? string.Empty);
    }

    public int TakePerSource
    {
        get => _takePerSource;
        set => SetProperty(ref _takePerSource, Math.Clamp(value, 1, 20));
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public YouTubeSocialContextResearchResponse? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(HasResult));
            }
        }
    }

    public bool HasResult => Result is not null;

    public string WorkspaceId
    {
        get => _workspaceId;
        private set
        {
            if (SetProperty(ref _workspaceId, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasWorkspace));
            }
        }
    }

    public long WorkspaceRevision
    {
        get => _workspaceRevision;
        private set
        {
            if (SetProperty(ref _workspaceRevision, value))
            {
                OnPropertyChanged(nameof(HasWorkspace));
            }
        }
    }

    public string WorkspaceStatus
    {
        get => _workspaceStatus;
        private set
        {
            if (SetProperty(ref _workspaceStatus, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(WorkspaceStatusLabel));
            }
        }
    }

    public bool IsSavingWorkspace
    {
        get => _isSavingWorkspace;
        private set => SetProperty(ref _isSavingWorkspace, value);
    }

    public bool HasWorkspace => WorkspaceId.Length > 0 && WorkspaceRevision > 0;

    public YouTubeSocialContextWorkspaceDto? Workspace
    {
        get => _workspace;
        private set => SetProperty(ref _workspace, value);
    }

    public IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto> SavedWorkspaces
    {
        get => _savedWorkspaces;
        private set
        {
            if (SetProperty(ref _savedWorkspaces, value))
            {
                OnPropertyChanged(nameof(HasSavedWorkspaces));
            }
        }
    }

    public bool HasSavedWorkspaces => SavedWorkspaces.Count > 0;

    public string WorkspaceStatusLabel
        => WorkspaceStatus switch
        {
            YouTubeSocialContextWorkspaceStatusCodes.ResearchReady => "조사 저장",
            YouTubeSocialContextWorkspaceStatusCodes.DraftEdited => "편집 초안 저장",
            YouTubeSocialContextWorkspaceStatusCodes.Published => "게시글 연결",
            YouTubeSocialContextWorkspaceStatusCodes.Archived => "보관",
            _ => "저장"
        };

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Sources.Count > 0)
        {
            return;
        }

        try
        {
            var sources = await client.GetSocialMediaSourcesAsync(cancellationToken);
            Sources = sources
                .Select(source => new CommunitySocialResearchSourceViewModel(
                    source,
                    source.Enabled && !source.RequiresStartUrl))
                .ToArray();
            await RefreshSavedWorkspacesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"SNS 조사 원천을 불러오지 못했습니다: {exception.Message}";
        }
    }

    public async Task<bool> ResearchAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return false;
        }

        ErrorMessage = null;
        StatusMessage = null;
        Result = null;
        Workspace = null;
        ApplyWorkspaceMetadata(string.Empty, 0, string.Empty);

        YouTubeSocialContextResearchRequest researchRequest;
        try
        {
            researchRequest = CreateResearchRequest();
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }

        IsLoading = true;
        try
        {
            Result = await client.ResearchYouTubeSocialContextAsync(
                researchRequest,
                cancellationToken);
            ApplyWorkspaceMetadata(
                Result.WorkspaceId,
                Result.WorkspaceRevision,
                Result.WorkspaceStatus);
            await RefreshSavedWorkspacesAsync(cancellationToken);
            StatusMessage = $"공개 자료 {Result.Items.Count:N0}건과 글 초안을 저장했습니다.";
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"YouTube·SNS 자료를 조사하지 못했습니다: {exception.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> LoadSavedWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return false;
        }

        ErrorMessage = null;
        StatusMessage = null;
        Result = null;
        ApplyWorkspaceMetadata(string.Empty, 0, string.Empty);
        string videoId;
        try
        {
            videoId = ExtractYouTubeVideoId(VideoReference);
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }

        IsLoading = true;
        try
        {
            var workspace = await client.GetYouTubeSocialContextWorkspaceByVideoAsync(
                videoId,
                cancellationToken);
            if (workspace is null)
            {
                StatusMessage = "이 영상으로 저장한 글쓰기 자료가 아직 없습니다.";
                return false;
            }

            ApplyWorkspace(workspace);
            StatusMessage = $"저장한 초안과 SNS 자료 {workspace.SocialContextSources.Sum(group => group.Items.Count):N0}건을 불러왔습니다.";
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"저장한 YouTube 글쓰기 자료를 불러오지 못했습니다: {exception.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> SaveDraftAsync(
        string nickname,
        string category,
        string workflowTag,
        string roleTag,
        string title,
        string body,
        string sharedLinkUrl,
        YouTubeImportJourneyDraftUpdateRequest? importJourney,
        CancellationToken cancellationToken = default)
    {
        if (!HasWorkspace || IsSavingWorkspace)
        {
            return false;
        }

        IsSavingWorkspace = true;
        ErrorMessage = null;
        try
        {
            var workspace = await client.SaveYouTubeSocialContextWorkspaceDraftAsync(
                WorkspaceId,
                new YouTubeSocialContextWorkspaceDraftUpdateRequest
                {
                    ExpectedRevision = WorkspaceRevision,
                    Nickname = nickname,
                    Category = category,
                    WorkflowTag = workflowTag,
                    RoleTag = roleTag,
                    Title = title,
                    Body = body,
                    SharedLinkUrl = sharedLinkUrl,
                    ImportJourney = importJourney
                },
                cancellationToken);
            Workspace = workspace;
            ApplyWorkspaceMetadata(workspace.WorkspaceId, workspace.Revision, workspace.Status);
            await RefreshSavedWorkspacesAsync(cancellationToken);
            StatusMessage = $"편집 초안을 저장했습니다. 리비전 {workspace.Revision:N0}";
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"YouTube 글 초안을 저장하지 못했습니다: {exception.Message}";
            return false;
        }
        finally
        {
            IsSavingWorkspace = false;
        }
    }

    public async Task<bool> LinkPublicationAsync(
        long postId,
        CancellationToken cancellationToken = default)
    {
        if (!HasWorkspace || postId <= 0 || IsSavingWorkspace)
        {
            return false;
        }

        IsSavingWorkspace = true;
        ErrorMessage = null;
        try
        {
            var workspace = await client.LinkYouTubeSocialContextPublicationAsync(
                WorkspaceId,
                new YouTubeSocialContextPublicationLinkRequest
                {
                    ExpectedRevision = WorkspaceRevision,
                    PostId = postId
                },
                cancellationToken);
            Workspace = workspace;
            ApplyWorkspaceMetadata(workspace.WorkspaceId, workspace.Revision, workspace.Status);
            await RefreshSavedWorkspacesAsync(cancellationToken);
            StatusMessage = $"저장한 YouTube 작업공간을 게시글 #{postId:N0}과 연결했습니다.";
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"게시글은 등록됐지만 YouTube 작업공간 연결에 실패했습니다: {exception.Message}";
            return false;
        }
        finally
        {
            IsSavingWorkspace = false;
        }
    }

    private void ApplyWorkspace(YouTubeSocialContextWorkspaceDto workspace)
    {
        Workspace = workspace;
        ApplyWorkspaceMetadata(workspace.WorkspaceId, workspace.Revision, workspace.Status);
        VideoReference = workspace.Video.OriginalUrl;
        SearchTermsText = string.Join(Environment.NewLine, workspace.SearchTerms);
        AdjacentTopicsText = string.Join(Environment.NewLine, workspace.AdjacentTopics);
        CountryCode = workspace.Video.CountryCode;
        LanguageCode = workspace.Video.LanguageCode;
        TakePerSource = workspace.TakePerSource;

        var selectedSourceKeys = workspace.SocialContextSources
            .Select(group => group.Source.SourceKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targets = workspace.SourceTargets.ToDictionary(
            target => target.SourceKey,
            StringComparer.OrdinalIgnoreCase);
        foreach (var source in Sources)
        {
            source.IsSelected = selectedSourceKeys.Contains(source.Source.SourceKey);
            source.StartUrlsText = targets.TryGetValue(source.Source.SourceKey, out var target)
                ? string.Join(Environment.NewLine, target.StartUrls)
                : string.Empty;
        }

        Result = new YouTubeSocialContextResearchResponse(
            workspace.LastResearchedAtUtc,
            workspace.Video,
            workspace.SearchTerms,
            workspace.AdjacentTopics,
            workspace.SocialContextSources.Select(group => group.Source).ToArray(),
            workspace.SocialContextSources.SelectMany(group => group.Items).ToArray(),
            workspace.Failures,
            new YouTubeSocialContextPostDraftDto(
                workspace.Draft.Title,
                workspace.Draft.Body,
                workspace.Draft.CollectiveAction))
        {
            WorkspaceId = workspace.WorkspaceId,
            WorkspaceRevision = workspace.Revision,
            WorkspaceStatus = workspace.Status
        };
    }

    public async Task<bool> LoadSavedWorkspaceAsync(
        YouTubeSocialContextWorkspaceSummaryDto summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        VideoReference = summary.VideoId;
        return await LoadSavedWorkspaceAsync(cancellationToken);
    }

    public static string ResolveOutreachReadinessLabel(string readinessCode)
        => readinessCode switch
        {
            YouTubeImportOutreachReadinessCodes.ReadyForManualDraft => "수동 이메일 초안 준비 가능",
            YouTubeImportOutreachReadinessCodes.ContactReviewRequired => "연락처 검토 필요",
            _ => "업체 후보 수집 중"
        };

    public YouTubeSocialContextResearchRequest CreateResearchRequest()
    {
        var videoId = ExtractYouTubeVideoId(VideoReference);
        ValidateSelectedSources();
        var selectedSources = Sources.Where(source => source.IsSelected).ToArray();
        return new YouTubeSocialContextResearchRequest
        {
            VideoId = videoId,
            SourceKeys = selectedSources.Select(source => source.Source.SourceKey).ToArray(),
            SearchTerms = SplitValues(SearchTermsText),
            AdjacentTopics = SplitValues(AdjacentTopicsText),
            SourceTargets = selectedSources
                .Where(source => source.StartUrls.Count > 0)
                .Select(source => new SocialMediaResearchTargetDto(
                    source.Source.SourceKey,
                    source.StartUrls))
                .ToArray(),
            TakePerSource = TakePerSource,
            CountryCode = NormalizeOptional(CountryCode),
            LanguageCode = NormalizeOptional(LanguageCode)
        };
    }

    private async Task RefreshSavedWorkspacesAsync(CancellationToken cancellationToken)
    {
        try
        {
            SavedWorkspaces = await client.GetYouTubeSocialContextWorkspacesAsync(50, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Recent-workspace indexing is auxiliary to the active authoring operation.
        }
    }

    private void ApplyWorkspaceMetadata(string workspaceId, long revision, string status)
    {
        WorkspaceId = workspaceId;
        WorkspaceRevision = revision;
        WorkspaceStatus = status;
    }

    internal static IReadOnlyList<string> SplitValues(string? value)
        => (value ?? string.Empty)
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static string ExtractYouTubeVideoId(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("YouTube 영상 주소나 영상 ID를 입력해 주세요.", nameof(value));
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return IsSafeVideoId(normalized)
                ? normalized
                : throw new ArgumentException("YouTube 영상 주소나 영상 ID 형식을 확인해 주세요.", nameof(value));
        }

        var host = uri.Host.TrimStart('.');
        if (host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return RequiredVideoId(uri.AbsolutePath.Trim('/'), value);
        }

        if (!host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("YouTube 도메인의 영상 주소만 사용할 수 있습니다.", nameof(value));
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 2
            && (segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("live", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("embed", StringComparison.OrdinalIgnoreCase)))
        {
            return RequiredVideoId(segments[1], value);
        }

        var videoId = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && parts[0].Equals("v", StringComparison.OrdinalIgnoreCase))
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .FirstOrDefault();
        return RequiredVideoId(videoId, value);
    }

    private void ValidateSelectedSources()
    {
        var selected = Sources.Where(source => source.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            throw new ArgumentException("조사할 SNS 원천을 하나 이상 선택해 주세요.");
        }

        var unavailable = selected.FirstOrDefault(source => !source.Source.Enabled);
        if (unavailable is not null)
        {
            throw new ArgumentException($"{unavailable.Source.DisplayName} 조사 모듈이 비활성화되어 있습니다.");
        }

        var missingTarget = selected.FirstOrDefault(source =>
            source.Source.RequiresStartUrl && source.StartUrls.Count == 0);
        if (missingTarget is not null)
        {
            throw new ArgumentException($"{missingTarget.Source.DisplayName}에서 확인할 공개 게시물 또는 계정 주소를 입력해 주세요.");
        }
    }

    private static string RequiredVideoId(string? value, string? original)
        => !string.IsNullOrWhiteSpace(value) && IsSafeVideoId(value)
            ? value.Trim()
            : throw new ArgumentException("YouTube 영상 주소에서 영상 ID를 확인하지 못했습니다.", nameof(original));

    private static bool IsSafeVideoId(string value)
        => value.Length is > 0 and <= 100
           && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
