using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record CommunityPostComposerSaveResult(
    bool Succeeded,
    bool WasEditing,
    PlatformCommunityPostResponse? Post,
    string Message)
{
    public CommunityPostComposerSnapshot? SubmittedDraft { get; init; }
    public string? SubmissionPassword { get; init; }
    public bool WasScheduled { get; init; }
    public DateTime? ScheduledPublishAtUtc { get; init; }
    public CommunityComposerMessageKind MessageKind { get; init; } = CommunityComposerMessageKind.Success;
    public int AttachmentUploadAttemptedCount { get; init; }
    public int AttachmentUploadSucceededCount { get; init; }
    public IReadOnlyList<string> AttachmentUploadFailedFileNames { get; init; } = [];
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Ui,
    HongdalModuleKind.ClientFeature,
    "커뮤니티 게시글 등록·수정·예약·첨부와 browser 초안 상태를 조율",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "글쓰기는 참여 의사를 공개하는 시작점이며 계약·배차·정산을 자동 확정하지 않습니다.")]
public sealed class CommunityPostComposerViewModel : 조립ViewModelBase
{
    private const long MaxUploadFileBytes = 5 * 1024 * 1024;
    private const int MaxUploadFileCount = 5;
    private static readonly HashSet<string> AllowedUploadContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };
    private readonly ICommunityPostClient _communityService;
    private readonly ICommunityPostComposerDraftStore _draftStore;
    private readonly SemaphoreSlim _draftStoreGate = new(1, 1);
    private long _draftGeneration;
    private string _appKey = string.Empty;
    private string _defaultRoleTag = "플랫폼 구성원";
    private string? _loadedDraftAppKey;
    private bool _isOpen;
    private bool _isSettingsOpen;
    private bool _isSaving;
    private long? _editingPostId;
    private CommunityPostComposerSnapshot? _localDraft;
    private DateTime? _localDraftSavedAtUtc;
    private string? _statusMessage;
    private CommunityComposerMessageKind _statusKind = CommunityComposerMessageKind.Info;
    private bool _allowScheduledPublication;
    private bool _isScheduledPublication;
    private DateTime? _scheduledPublishDateLocal;
    private TimeSpan? _scheduledPublishTimeLocal;

    public CommunityPostComposerViewModel(
        ICommunityPostClient communityService,
        ICommunityPostComposerDraftStore draftStore)
    {
        _communityService = communityService;
        _draftStore = draftStore;
        Draft = 하위ViewModel등록(new CommunityPostComposerDraftViewModel());
    }

    public CommunityPostComposerDraftViewModel Draft { get; }
    public List<IBrowserFile> SelectedFiles { get; } = [];

    public bool IsOpen { get => _isOpen; internal set => SetProperty(ref _isOpen, value); }
    public bool IsSettingsOpen { get => _isSettingsOpen; internal set => SetProperty(ref _isSettingsOpen, value); }
    public bool IsSaving { get => _isSaving; private set => SetProperty(ref _isSaving, value); }
    public long? EditingPostId { get => _editingPostId; internal set => SetProperty(ref _editingPostId, value); }
    public DateTime? LocalDraftSavedAtUtc { get => _localDraftSavedAtUtc; private set => SetProperty(ref _localDraftSavedAtUtc, value); }
    public string? StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public CommunityComposerMessageKind StatusKind { get => _statusKind; private set => SetProperty(ref _statusKind, value); }
    public bool AllowScheduledPublication
    {
        get => _allowScheduledPublication;
        private set => SetProperty(ref _allowScheduledPublication, value);
    }

    public bool IsScheduledPublication
    {
        get => _isScheduledPublication;
        set
        {
            var normalized = AllowScheduledPublication && EditingPostId is null && value;
            if (!SetProperty(ref _isScheduledPublication, normalized))
            {
                return;
            }

            if (normalized)
            {
                EnsureScheduledPublicationDefaults();
            }

            OnPropertyChanged(nameof(ScheduledPublishAtUtc));
        }
    }

    public DateTime? ScheduledPublishDateLocal
    {
        get => _scheduledPublishDateLocal;
        set
        {
            if (SetProperty(ref _scheduledPublishDateLocal, value?.Date))
            {
                OnPropertyChanged(nameof(ScheduledPublishAtUtc));
            }
        }
    }

    public TimeSpan? ScheduledPublishTimeLocal
    {
        get => _scheduledPublishTimeLocal;
        set
        {
            if (SetProperty(ref _scheduledPublishTimeLocal, value))
            {
                OnPropertyChanged(nameof(ScheduledPublishAtUtc));
            }
        }
    }

    public DateTime? ScheduledPublishAtUtc
        => BuildScheduledPublishAtUtc();

    public string LocalTimeZoneDisplayName
        => TimeZoneInfo.Local.DisplayName;

    public void Configure(
        string appKey,
        string defaultRoleTag,
        bool allowScheduledPublication = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultRoleTag);

        if (!string.Equals(_appKey, appKey, StringComparison.OrdinalIgnoreCase))
        {
            _appKey = appKey.Trim();
            _loadedDraftAppKey = null;
            _localDraft = null;
            LocalDraftSavedAtUtc = null;
        }

        _defaultRoleTag = defaultRoleTag.Trim();
        AllowScheduledPublication = allowScheduledPublication;
        if (!allowScheduledPublication)
        {
            IsScheduledPublication = false;
        }

        if (string.IsNullOrWhiteSpace(Draft.RoleTag))
        {
            Draft.RoleTag = _defaultRoleTag;
        }
    }

    public async Task LoadLocalDraftAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (string.Equals(_loadedDraftAppKey, _appKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _loadedDraftAppKey = _appKey;
        try
        {
            _localDraft = await _draftStore.LoadAsync(_appKey, cancellationToken);
            LocalDraftSavedAtUtc = _localDraft?.SavedAtUtc;
            if (IsOpen)
            {
                RestoreLocalDraftIfNeeded();
            }
        }
        catch (Exception)
        {
            _localDraft = null;
            LocalDraftSavedAtUtc = null;
        }
    }

    public void Open()
    {
        RestoreLocalDraftIfNeeded();
        IsOpen = true;
    }

    public void Close()
        => IsOpen = false;

    public void ToggleSettings()
        => IsSettingsOpen = !IsSettingsOpen;

    public void OpenSettings()
        => IsSettingsOpen = true;

    public void SelectCategory(string category)
    {
        if (Draft.IsSalesPost
            && !string.Equals(category, PlatformCommunityPostCategories.Sales, StringComparison.OrdinalIgnoreCase))
        {
            Draft.Category = PlatformCommunityPostCategories.Sales;
            SetStatus(
                "판매 정보가 붙은 글은 판매 게시판에 자동으로 등록됩니다.",
                CommunityComposerMessageKind.Info);
            return;
        }

        Draft.Category = category;
        ClearStatus();
    }

    public void SetFiles(IEnumerable<IBrowserFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var candidates = files.ToArray();
        var accepted = candidates
            .Where(file => file.Size <= MaxUploadFileBytes
                           && AllowedUploadContentTypes.Contains(file.ContentType))
            .Take(MaxUploadFileCount)
            .ToArray();
        SelectedFiles.Clear();
        SelectedFiles.AddRange(accepted);
        OnPropertyChanged(nameof(SelectedFiles));

        var rejectedCount = candidates.Length - accepted.Length;
        if (rejectedCount > 0)
        {
            SetStatus(
                $"사진 {accepted.Length}개를 선택했습니다. {rejectedCount}개는 5개 제한, 5MB 크기 또는 지원 형식(JPG·PNG·WebP·GIF)을 확인해 주세요.",
                CommunityComposerMessageKind.Warning);
        }
    }

    public void RemoveFile(IBrowserFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!SelectedFiles.Remove(file))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedFiles));
        SetStatus("선택한 사진을 첨부 목록에서 뺐습니다.", CommunityComposerMessageKind.Info);
    }

    public void ClearFiles()
    {
        if (SelectedFiles.Count == 0)
        {
            return;
        }

        SelectedFiles.Clear();
        OnPropertyChanged(nameof(SelectedFiles));
        SetStatus("첨부할 사진을 모두 비웠습니다.", CommunityComposerMessageKind.Info);
    }

    public void BeginEdit(PlatformCommunityPostResponse post)
    {
        ArgumentNullException.ThrowIfNull(post);
        EditingPostId = post.Id;
        IsScheduledPublication = false;
        SelectedFiles.Clear();
        Draft.Apply(post);
        SetStatus(
            "작성할 때 입력한 비밀번호를 넣고 수정 저장을 누르세요.",
            CommunityComposerMessageKind.Info);
        Open();
        IsSettingsOpen = true;
    }

    public async Task CancelEditAsync(CancellationToken cancellationToken = default)
    {
        await DiscardDraftAsync(cancellationToken);
        IsOpen = false;
    }

    public async Task<bool> DiscardDraftAsync(CancellationToken cancellationToken = default)
    {
        Reset();
        _localDraft = null;
        LocalDraftSavedAtUtc = null;
        Interlocked.Increment(ref _draftGeneration);
        try
        {
            EnsureConfigured();
            await _draftStoreGate.WaitAsync(cancellationToken);
            try
            {
                await _draftStore.ClearAsync(_appKey, cancellationToken);
                _localDraft = null;
                LocalDraftSavedAtUtc = null;
            }
            finally
            {
                _draftStoreGate.Release();
            }

            return true;
        }
        catch (Exception)
        {
            SetStatus(
                "현재 화면의 초안은 비웠지만 browser 임시 저장을 제거하지 못했습니다.",
                CommunityComposerMessageKind.Warning);
            return false;
        }
    }

    public void Reset()
    {
        EditingPostId = null;
        IsScheduledPublication = false;
        ScheduledPublishDateLocal = null;
        ScheduledPublishTimeLocal = null;
        IsSettingsOpen = false;
        SelectedFiles.Clear();
        Draft.Reset(_defaultRoleTag);
        ClearStatus();
        OnPropertyChanged(nameof(SelectedFiles));
    }

    public void SetStatus(string message, CommunityComposerMessageKind kind)
    {
        StatusKind = kind;
        StatusMessage = message;
    }

    public void ClearStatus()
        => StatusMessage = null;

    public Task SaveLocalDraftAsync(CancellationToken cancellationToken = default)
        => SaveLocalDraftCoreAsync(announce: true, cancellationToken);

    public Task SaveLocalDraftSilentlyAsync(CancellationToken cancellationToken = default)
        => SaveLocalDraftCoreAsync(announce: false, cancellationToken);

    private async Task SaveLocalDraftCoreAsync(
        bool announce,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (!Draft.HasContent)
        {
            if (announce)
            {
                SetStatus(
                    "임시 저장할 제목, 내용, 링크 또는 원장 연결이 없습니다.",
                    CommunityComposerMessageKind.Warning);
            }

            return;
        }

        var snapshot = CreateSnapshot(DateTime.UtcNow);
        var generation = Volatile.Read(ref _draftGeneration);
        try
        {
            await _draftStoreGate.WaitAsync(cancellationToken);
            try
            {
                if (generation != Volatile.Read(ref _draftGeneration))
                {
                    return;
                }

                await _draftStore.SaveAsync(_appKey, snapshot, cancellationToken);
                if (generation != Volatile.Read(ref _draftGeneration))
                {
                    return;
                }
            }
            finally
            {
                _draftStoreGate.Release();
            }

            _localDraft = snapshot;
            LocalDraftSavedAtUtc = snapshot.SavedAtUtc;
            if (announce)
            {
                SetStatus(
                    SelectedFiles.Count == 0
                        ? "이 브라우저에 임시 저장했습니다. 글 비밀번호는 저장하지 않습니다."
                        : "이 브라우저에 임시 저장했습니다. 글 비밀번호와 첨부 사진은 저장하지 않습니다.",
                    CommunityComposerMessageKind.Success);
            }
        }
        catch (Exception)
        {
            if (announce)
            {
                SetStatus(
                    "브라우저 임시 저장을 사용할 수 없습니다. 현재 화면의 내용은 닫기 전까지 유지됩니다.",
                    CommunityComposerMessageKind.Warning);
            }
        }
    }

    public async Task<CommunityPostComposerSaveResult> SaveAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (IsSaving)
        {
            return new(false, EditingPostId is not null, null, "이미 글을 저장하고 있습니다.");
        }

        var validationMessage = Draft.Validate();
        if (validationMessage is not null)
        {
            IsSettingsOpen = true;
            SetStatus(validationMessage, CommunityComposerMessageKind.Warning);
            return new(false, EditingPostId is not null, null, validationMessage);
        }

        var scheduleValidationMessage = ValidateScheduledPublication();
        if (scheduleValidationMessage is not null)
        {
            IsSettingsOpen = true;
            SetStatus(scheduleValidationMessage, CommunityComposerMessageKind.Warning);
            return new(false, EditingPostId is not null, null, scheduleValidationMessage);
        }

        IsSaving = true;
        var wasEditing = EditingPostId is not null;
        var scheduledPublishAtUtc = !wasEditing && IsScheduledPublication
            ? ScheduledPublishAtUtc
            : null;
        var submissionPassword = Draft.Password.Trim();
        var submittedDraft = CreateSnapshot(DateTime.UtcNow);
        try
        {
            var saved = EditingPostId is long postId
                ? await _communityService.UpdatePostAsync(
                    postId,
                    Draft.CreateUpdateRequest(),
                    cancellationToken)
                : scheduledPublishAtUtc is DateTime publishAtUtc
                    ? await _communityService.SchedulePostAsync(
                        new PlatformCommunityPostScheduleCreateRequest
                        {
                            Post = Draft.CreateRequest(_appKey),
                            ScheduledPublishAtUtc = publishAtUtc
                        },
                        cancellationToken)
                    : await _communityService.CreatePostAsync(
                        Draft.CreateRequest(_appKey),
                        cancellationToken);

            if (saved is null)
            {
                const string emptyResponseMessage = "글 저장 응답을 확인하지 못했습니다.";
                SetStatus(emptyResponseMessage, CommunityComposerMessageKind.Error);
                return new(false, wasEditing, null, emptyResponseMessage);
            }

            var selectedFiles = SelectedFiles.ToArray();
            var uploadedFileCount = 0;
            var failedFileNames = new List<string>();
            for (var fileIndex = 0; fileIndex < selectedFiles.Length; fileIndex++)
            {
                var file = selectedFiles[fileIndex];
                try
                {
                    await _communityService.UploadAttachmentAsync(
                        saved.Id,
                        Draft.Password,
                        file,
                        MaxUploadFileBytes,
                        cancellationToken);
                    uploadedFileCount++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    failedFileNames.AddRange(selectedFiles
                        .Skip(fileIndex)
                        .Select(pending => pending.Name));
                    break;
                }
                catch (Exception)
                {
                    failedFileNames.Add(file.Name);
                }
            }

            await ClearLocalDraftAsync(CancellationToken.None);
            Reset();
            IsOpen = false;
            var persistenceMessage = wasEditing
                ? "글을 수정했습니다."
                : scheduledPublishAtUtc is DateTime scheduledAtUtc
                    ? $"{scheduledAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}에 발행하도록 예약했습니다."
                    : "글을 등록했습니다.";
            var messageKind = failedFileNames.Count == 0
                ? CommunityComposerMessageKind.Success
                : CommunityComposerMessageKind.Warning;
            var message = failedFileNames.Count == 0
                ? persistenceMessage
                : $"{persistenceMessage} 다만 사진 {failedFileNames.Count}개를 첨부하지 못했습니다. 글 상세에서 저장 여부를 확인한 뒤 수정으로 다시 첨부해 주세요: {string.Join(", ", failedFileNames.Distinct(StringComparer.OrdinalIgnoreCase))}";
            SetStatus(message, messageKind);
            return new(true, wasEditing, saved, message)
            {
                SubmittedDraft = submittedDraft,
                SubmissionPassword = submissionPassword,
                WasScheduled = scheduledPublishAtUtc.HasValue,
                ScheduledPublishAtUtc = scheduledPublishAtUtc,
                MessageKind = messageKind,
                AttachmentUploadAttemptedCount = selectedFiles.Length,
                AttachmentUploadSucceededCount = uploadedFileCount,
                AttachmentUploadFailedFileNames = failedFileNames
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            const string canceledMessage =
                "저장 요청이 취소되었습니다. 다시 등록하기 전에 글 목록에서 이미 저장됐는지 확인해 주세요.";
            SetStatus(canceledMessage, CommunityComposerMessageKind.Warning);
            return new(false, wasEditing, null, canceledMessage);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            const string loginRequiredMessage =
                "선택한 게시판은 로그인한 사용자만 글을 작성할 수 있습니다. 로그인 후 다시 등록해 주세요.";
            SetStatus(loginRequiredMessage, CommunityComposerMessageKind.Warning);
            return new(false, wasEditing, null, loginRequiredMessage);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            const string forbiddenMessage = "비밀번호가 맞지 않아 수정할 수 없습니다.";
            SetStatus(forbiddenMessage, CommunityComposerMessageKind.Error);
            return new(false, wasEditing, null, forbiddenMessage);
        }
        catch (Exception ex)
        {
            var message = $"저장에 실패했습니다: {ex.Message}";
            SetStatus(message, CommunityComposerMessageKind.Error);
            return new(false, wasEditing, null, message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool RestoreLocalDraftIfNeeded()
    {
        if (EditingPostId is not null || _localDraft is null || Draft.HasContent)
        {
            return false;
        }

        EditingPostId = _localDraft.EditingPostId;
        Draft.Apply(_localDraft);
        ScheduledPublishDateLocal = _localDraft.ScheduledPublishDateLocal;
        ScheduledPublishTimeLocal = _localDraft.ScheduledPublishTimeLocal;
        IsScheduledPublication = _localDraft.IsScheduledPublication;
        SetStatus(
            EditingPostId.HasValue
                ? "이 브라우저에 임시 저장한 수정 내용을 불러왔습니다. 글 비밀번호를 다시 입력해 주세요."
                : "이 브라우저에 임시 저장한 글을 불러왔습니다.",
            CommunityComposerMessageKind.Info);
        return true;
    }

    private CommunityPostComposerSnapshot CreateSnapshot(DateTime savedAtUtc)
        => Draft.CreateSnapshot(savedAtUtc) with
        {
            EditingPostId = EditingPostId,
            IsScheduledPublication = IsScheduledPublication,
            ScheduledPublishDateLocal = ScheduledPublishDateLocal,
            ScheduledPublishTimeLocal = ScheduledPublishTimeLocal
        };

    private async Task ClearLocalDraftAsync(CancellationToken cancellationToken)
    {
        _localDraft = null;
        LocalDraftSavedAtUtc = null;
        Interlocked.Increment(ref _draftGeneration);
        try
        {
            await _draftStoreGate.WaitAsync(cancellationToken);
            try
            {
                await _draftStore.ClearAsync(_appKey, cancellationToken);
                _localDraft = null;
                LocalDraftSavedAtUtc = null;
            }
            finally
            {
                _draftStoreGate.Release();
            }
        }
        catch (Exception)
        {
            // A successful post must not fail because browser storage is unavailable.
        }
    }

    private string? ValidateScheduledPublication()
    {
        if (!IsScheduledPublication)
        {
            return null;
        }

        if (!AllowScheduledPublication || EditingPostId is not null)
        {
            return "새 글을 작성할 때만 예약 발행을 사용할 수 있습니다.";
        }

        if (ScheduledPublishAtUtc is not DateTime publishAtUtc)
        {
            return "예약 발행 날짜와 시간을 모두 선택해 주세요.";
        }

        var now = DateTime.UtcNow;
        if (publishAtUtc < now.Add(PlatformCommunityPostSchedulePolicy.MinimumLeadTime))
        {
            return "예약 발행 시각은 현재보다 1분 이후여야 합니다.";
        }

        if (publishAtUtc > now.Add(PlatformCommunityPostSchedulePolicy.MaximumLeadTime))
        {
            return "예약 발행 시각은 현재부터 365일 이내여야 합니다.";
        }

        return null;
    }

    private DateTime? BuildScheduledPublishAtUtc()
    {
        if (ScheduledPublishDateLocal is not DateTime date
            || ScheduledPublishTimeLocal is not TimeSpan time)
        {
            return null;
        }

        var local = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        return local.ToUniversalTime();
    }

    private void EnsureScheduledPublicationDefaults()
    {
        if (ScheduledPublishDateLocal.HasValue && ScheduledPublishTimeLocal.HasValue)
        {
            return;
        }

        var nextHour = DateTime.Now.AddHours(2);
        var defaultLocal = new DateTime(
            nextHour.Year,
            nextHour.Month,
            nextHour.Day,
            nextHour.Hour,
            0,
            0,
            DateTimeKind.Local);
        ScheduledPublishDateLocal = defaultLocal.Date;
        ScheduledPublishTimeLocal = defaultLocal.TimeOfDay;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_appKey))
        {
            throw new InvalidOperationException("커뮤니티 글쓰기 AppKey가 설정되지 않았습니다.");
        }
    }
}
