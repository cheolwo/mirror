using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.App.ViewModels.Transport;
using Ssalddel.WebApp.Services;
using SubmissionContext = Ssalddel.Ui.Common.Areas.App.ViewModels.Transport.ShipperSubmissionResultSession.Context;

namespace Ssalddel.WebApp.ViewModels;

/// <summary>
/// Web route adapter가 자동 저장, 서버 기능 경계, 커뮤니티 글 가져오기를 조율합니다.
/// 실제 입력 draft와 validation은 Ui.Common의 <see cref="운송의뢰작성ViewModel"/>이 소유합니다.
/// </summary>
public sealed class ShipperRequestAuthoringPageViewModel : IDisposable
{
    private const string DomesticTransportFeatureKey = "DomesticTransportWorkflow";

    private readonly 운송의뢰자동저장Service _draftStorage;
    private readonly 커뮤니티화물글초안가져오기Service _sourcePostImporter;
    private readonly I화주운송의뢰등록Service _registration;
    private readonly WebAuthSessionService _authSession;
    private readonly ICommunityPostClient _communityPosts;
    private readonly ICommunityProcurementClient _workflowMetadata;
    private readonly NavigationManager _navigation;
    private readonly ShipperSubmissionResultSession _submissionResults;
    private bool _initialized;
    private bool _autoSaveReady;
    private long? _loadedSourcePostId;
    private ShipperRequestNavigationContext _navigationContext = new();
    private 화주웹접수기록? _submission;
    private string? _owner, _token;
    private bool _recoveryUnavailable;

    public ShipperRequestAuthoringPageViewModel(
        운송의뢰작성ViewModel state,
        운송의뢰자동저장Service draftStorage,
        커뮤니티화물글초안가져오기Service sourcePostImporter,
        I화주운송의뢰등록Service registration,
        WebAuthSessionService authSession,
        ICommunityPostClient communityPosts,
        ICommunityProcurementClient workflowMetadata,
        NavigationManager navigation)
    {
        State = state;
        _draftStorage = draftStorage;
        _sourcePostImporter = sourcePostImporter;
        _registration = registration;
        _authSession = authSession;
        _communityPosts = communityPosts;
        _workflowMetadata = workflowMetadata;
        _navigation = navigation;
        _submissionResults = new(
            () => new(_authSession.UserId, _authSession.AccessToken, _authSession.IsLoggedIn),
            "로그인한 계정으로 운송 의뢰를 등록해 주세요.");
        _owner = authSession.UserId;
        _token = authSession.AccessToken;
        authSession.Changed += OnAuthenticationChanged;
    }

    public 운송의뢰작성ViewModel State { get; }
    public IReadOnlyList<string> VehicleTypes => State.추천차량종류목록;
    public 운송모델작성Draft? LastDraft { get; private set; }
    public string StatusMessage { get; private set; } = string.Empty;
    public Severity StatusSeverity { get; private set; } = Severity.Info;
    public string AutoSaveMessage { get; private set; } = string.Empty;
    public string? CreatedRequestId { get; private set; }
    public bool IsBusy { get; private set; }
    public PlatformCommunityPostResponse? SourcePost { get; private set; }
    public string? SourcePostError { get; private set; }
    public bool IsSourcePostLoading { get; private set; }
    public bool IsSourcePostApplying { get; private set; }
    public bool SourcePostApplied { get; private set; }
    public bool? RegistrationEnabled { get; private set; }
    public bool IsRegistrationAvailabilityLoading { get; private set; }
    public Guid? ApplicationPrivacyConsentEvidenceId { get; private set; }
    public string ApplicationSourceCode { get; private set; } = string.Empty;
    public bool HasSubmission => _submission is not null || _recoveryUnavailable;
    public bool CanStartNewRequest => !IsBusy && _submission?.ConfirmedRequestId is not null;
    public string? SubmissionNumber => _submission?.ClientRequestId;
    public bool SubmissionNotFound { get; private set; }
    public int? SubmissionRejectedStatus => _submission?.RejectedStatus;
    public string SubmissionState { get; private set; } = "작성 중";

    public string RegistrationBoundaryMessage { get; private set; }
        = "서버 등록 가능 여부를 확인하고 있습니다. 초안은 계속 작성할 수 있습니다.";

    public string SourcePostPreview => BuildSourcePostPreview(SourcePost?.Body);

    public async Task<차량추천응답> RecommendVehiclesAsync(차량추천요청 request, CancellationToken cancellationToken)
    {
        var context = CurrentContext();
        if (!IsCurrent(context)) throw new OperationCanceledException();
        var response = await _registration.RecommendVehiclesAsync(request, cancellationToken);
        if (!IsCurrent(context)) throw new OperationCanceledException();
        return response;
    }

    public async Task InitializeAsync(ShipperRequestNavigationContext context)
    {
        if (_submissionResults.IsDisposed || IsBusy) return;
        _navigationContext = context;
        await _authSession.RestoreAsync();
        if (!_initialized)
        {
            _initialized = true;
            await RestoreDraftAsync();
            await LoadTransportRegistrationAvailabilityAsync();
            if (_authSession.IsLoggedIn) await RecoverSubmissionAsync(navigate: false);
        }

        if (context.SourcePostId is > 0 && context.SourcePostId != _loadedSourcePostId)
        {
            await LoadSourcePostAsync(context.SourcePostId.Value);
        }
    }

    public async Task LoadTransportRegistrationAvailabilityAsync()
    {
        if (IsRegistrationAvailabilityLoading)
        {
            return;
        }

        IsRegistrationAvailabilityLoading = true;
        RegistrationBoundaryMessage = "서버 등록 가능 여부를 확인하고 있습니다. 초안은 계속 작성할 수 있습니다.";

        try
        {
            var metadata = await _workflowMetadata.GetVersionWorkflowMetadataAsync();
            RegistrationEnabled = metadata.Flags.TryGetValue(DomesticTransportFeatureKey, out var enabled)
                                  && enabled;
            RegistrationBoundaryMessage = RegistrationEnabled == true
                ? "서버에 운송 의뢰 원장을 등록할 수 있습니다. 추천·자동 배차·계약·결제는 이 등록만으로 확정되지 않습니다."
                : "현재 환경에서는 국내 화물 운송의 서버 등록이 비활성화되어 있습니다. 초안 작성·저장은 가능하며 배차나 계약은 시작되지 않습니다.";
        }
        catch
        {
            RegistrationEnabled = false;
            RegistrationBoundaryMessage = "서버 등록 가능 여부를 확인하지 못해 등록을 안전하게 보류했습니다. 초안은 계속 저장할 수 있습니다.";
        }
        finally
        {
            IsRegistrationAvailabilityLoading = false;
        }
    }

    public async Task ApplySourcePostAsync()
    {
        if (SourcePost is null || IsSourcePostApplying || HasSubmission || IsBusy)
        {
            return;
        }

        IsSourcePostApplying = true;
        try
        {
            var result = _sourcePostImporter.가져오기(State, SourcePost);
            LastDraft = State.ToDraft();
            await _draftStorage.SaveForOwnerAsync(LastDraft, CurrentContext().Owner);
            CreatedRequestId = null;
            SourcePostApplied = true;
            StatusSeverity = result.변경됨 ? Severity.Success : Severity.Info;
            StatusMessage = result.변경됨
                ? "화물 글의 제목과 본문을 초안에 반영했습니다. 주소·연락처·수량·운임을 확인해 주세요."
                : "이 화물 글은 이미 현재 초안에 반영되어 있습니다.";
            AutoSaveMessage = $"가져온 초안을 저장했습니다. 저장 시각: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusSeverity = Severity.Error;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsSourcePostApplying = false;
        }
    }

    public async Task SaveAsync()
    {
        if (HasSubmission || IsBusy) { PreserveSubmission(); return; }
        if (!_authSession.IsLoggedIn) { StatusMessage = "로그인한 뒤 이 계정의 초안을 저장해 주세요."; return; }
        var context = CurrentContext();
        LastDraft = State.ToDraft();
        try { await _draftStorage.SaveForOwnerAsync(LastDraft, context.Owner); }
        catch
        {
            if (!IsCurrent(context)) return;
            _recoveryUnavailable = true;
            StatusSeverity = Severity.Warning;
            StatusMessage = "초안 보관 상태를 확인하지 못했습니다. 입력은 화면에 유지했으며 등록은 보내지 않았습니다. 접수 보관을 다시 확인해 주세요.";
            return;
        }
        if (!IsCurrent(context)) return;
        AutoSaveMessage = $"초안을 저장했습니다. 저장 시각: {DateTime.Now:HH:mm:ss}";
        StatusSeverity = State.서버등록가능 ? Severity.Success : Severity.Warning;
        StatusMessage = State.서버등록가능
            ? "서버 등록에 필요한 기본 입력이 채워졌습니다."
            : "초안을 저장했지만 서버 등록 전 보완할 입력이 남아 있습니다.";
    }

    public async Task AutoSaveAsync()
    {
        if (!_autoSaveReady || IsBusy || HasSubmission || !_authSession.IsLoggedIn)
        {
            return;
        }

        var context = CurrentContext();
        LastDraft = State.ToDraft();
        try { await _draftStorage.SaveForOwnerAsync(LastDraft, context.Owner); }
        catch
        {
            if (!IsCurrent(context)) return;
            _recoveryUnavailable = true;
            StatusSeverity = Severity.Warning;
            StatusMessage = "초안을 자동 저장하지 못했습니다. 화면 입력은 유지합니다. 접수 보관을 다시 확인해 주세요.";
            return;
        }
        if (!IsCurrent(context)) return;
        AutoSaveMessage = $"자동 저장됨: {DateTime.Now:HH:mm:ss}";
    }

    public async Task ResetAsync()
    {
        if (_submissionResults.IsDisposed || IsBusy) return;
        if (HasSubmission && !CanStartNewRequest) { PreserveSubmission(); return; }
        if (!_authSession.IsLoggedIn) return;
        var context = CurrentContext();
        try
        {
            if (_submission is not null) await _registration.접수기록종료Async(_submission);
            else await _draftStorage.ClearForOwnerAsync(context.Owner);
        }
        catch
        {
            if (!IsCurrent(context)) return;
            StatusSeverity = Severity.Warning;
            StatusMessage = "기존 접수 보관을 닫지 못했습니다. 새 의뢰를 보내지 않았습니다. 결과를 다시 확인해 주세요.";
            return;
        }
        if (!IsCurrent(context)) return;
        _submission = null;
        SubmissionNotFound = false;
        SubmissionState = "작성 중";
        ApplicationPrivacyConsentEvidenceId = null;
        ApplicationSourceCode = string.Empty;
        State.Reset();
        LastDraft = null;
        CreatedRequestId = null;
        SourcePostApplied = false;
        StatusSeverity = Severity.Info;
        StatusMessage = "운송 의뢰 입력값을 초기화했습니다.";
        AutoSaveMessage = string.Empty;
    }

    public Task SubmitAsync()
        => SubmitAsync(null);

    public async Task SubmitAsync(Func<string, Task<string?>>? afterCreated)
    {
        if (_submissionResults.IsDisposed || IsBusy) return;
        if (HasSubmission) { await RecoverSubmissionAsync(afterCreated: afterCreated); return; }
        if (!_authSession.IsLoggedIn)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "로그인한 계정으로 운송 의뢰를 등록해 주세요.";
            return;
        }
        if (RegistrationEnabled != true)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "현재 환경에서는 국내 화물 운송 서버 등록이 비활성화되어 있습니다. 초안은 브라우저에 저장했습니다.";
            return;
        }

        if (!State.서버등록가능)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "서버 등록 전에 등록 전 점검 목록의 필수 항목을 보완해 주세요.";
            return;
        }

        IsBusy = true;
        var context = CurrentContext();
        SubmissionState = "접수 번호 보관 중";
        StatusSeverity = Severity.Info;
        StatusMessage = "서버에 운송 의뢰를 등록하는 중입니다.";

        try
        {
            // 번호·원문 초안의 내구성 저장을 확인한 최초 화면만 POST할 수 있습니다.
            var draft = State.ToDraft();
            var frozen = State.접수입력고정복사();
            var started = await _registration.접수시작Async(context.Owner, draft);
            if (!IsCurrent(context)) return;
            _submission = started.Record;
            if (!started.Created)
            {
                await RecoverSubmissionCoreAsync(context, true, afterCreated);
                return;
            }
            SubmissionState = "등록 요청 중";
            var created = await _registration.등록Async(
                frozen,
                _submission,
                ApplicationPrivacyConsentEvidenceId,
                ApplicationSourceCode);
            if (!IsCurrent(context)) return;
            await ConfirmSubmissionAsync(created, context, true, afterCreated);
        }
        catch (Exception ex)
        {
            if (!IsCurrent(context)) return;
            if (ex is 화주웹접수거절Exception rejected && _submission is not null)
            {
                _submission = _submission with { RejectedStatus = rejected.StatusCode };
                try
                {
                    var saved = await _registration.접수결과저장Async(_submission);
                    if (!IsCurrent(context)) return;
                    _submission = saved;
                }
                catch { /* 번호는 최초 저장되어 있으며 거절 응답은 미저장 확정 근거가 아닙니다. */ }
            }
            if (!IsCurrent(context)) return;
            if (_submission is not null)
            {
                StatusSeverity = Severity.Warning;
                SubmissionState = ex is 화주웹접수거절Exception ? "서버 요청 거절" : "등록 결과 미확인";
                await RecoverSubmissionCoreAsync(context, true, afterCreated);
            }
            else
            {
                // 저장·잠금 실패 시 POST를 보내지 않습니다. 기존 번호를 읽기 전 새 번호도 만들지 않습니다.
                _recoveryUnavailable = true;
                SubmissionState = "접수 보관 확인 필요";
                StatusSeverity = Severity.Warning;
                StatusMessage = $"등록 요청을 보내지 않았습니다. 접수 보관 상태를 다시 확인해 주세요. {ex.Message}";
            }
        }
        finally
        {
            if (IsCurrent(context)) IsBusy = false;
        }
    }

    public async Task RecoverSubmissionAsync(bool navigate = true, Func<string, Task<string?>>? afterCreated = null)
    {
        if (_submissionResults.IsDisposed || IsBusy || !_authSession.IsLoggedIn) return;
        var context = CurrentContext();
        IsBusy = true;
        try
        {
            var previous = _submission;
            var stored = await _registration.접수기록조회Async(context.Owner);
            if (!IsCurrent(context)) return;
            if (stored is null && previous is not null)
            {
                _recoveryUnavailable = true;
                SubmissionState = "접수 보관 확인 필요";
                StatusSeverity = Severity.Warning;
                StatusMessage = "보관 중이던 접수 번호가 다른 화면에서 변경됐거나 삭제됐습니다. 원래 번호는 화면에 유지했습니다. 운영자에게 접수 번호로 확인을 요청해 주세요.";
                return;
            }
            _submission = stored;
            _recoveryUnavailable = false;
            if (_submission is null) { SubmissionState = "작성 중"; return; }
            await RecoverSubmissionCoreAsync(context, navigate, afterCreated);
        }
        catch (Exception)
        {
            if (!IsCurrent(context)) return;
            _recoveryUnavailable = true;
            SubmissionState = "접수 보관 확인 필요";
            StatusSeverity = Severity.Warning;
            StatusMessage = "이전 접수 보관을 확인하지 못했습니다. 입력은 유지하며 새 등록은 보내지 않았습니다. 다시 확인해 주세요.";
        }
        finally { if (IsCurrent(context)) IsBusy = false; }
    }

    private async Task RecoverSubmissionCoreAsync(SubmissionContext context, bool navigate, Func<string, Task<string?>>? afterCreated)
    {
        if (_submission is null || !IsCurrent(context)) return;
        SubmissionState = "등록 결과 조회 중";
        SubmissionNotFound = false;
        var result = await _submissionResults.ReconcileAsync(context,
            () => _registration.접수결과조회Async(_submission),
            found => ConfirmSubmissionAsync(found, context, navigate, afterCreated));
        if (!IsCurrent(context)) return;
        if (result.Kind == ShipperSubmissionResultKind.NotFound)
        {
            SubmissionNotFound = true;
            SubmissionState = _submission.RejectedStatus is not null ? "서버 요청 거절 · 정본 조회 미발견" : "정본 조회 미발견";
            StatusSeverity = Severity.Warning;
            StatusMessage = "이 접수 번호의 의뢰가 아직 조회되지 않습니다. 저장 중이거나 반영이 늦을 수 있어 미접수로 확정하지 않았습니다. 초안과 번호를 유지하고 결과를 다시 확인해 주세요.";
        }
        else if (result.Kind == ShipperSubmissionResultKind.Failed)
        {
            SubmissionState = "등록 결과 조회 실패";
            StatusSeverity = Severity.Warning;
            StatusMessage = "등록 결과를 확인하지 못했습니다. 새 등록을 보내지 않고 초안과 접수 번호를 유지했습니다. 결과 확인을 다시 시도해 주세요.";
        }
    }

    private async Task ConfirmSubmissionAsync(화주운송의뢰응답 created, SubmissionContext context, bool navigate,
        Func<string, Task<string?>>? afterCreated)
    {
        if (_submission is null || !IsCurrent(context)) return;
        if (created.의뢰Id != _submission.ExpectedRequestId
            || !(created.주문자UserId == context.Owner || string.IsNullOrEmpty(created.주문자UserId) && created.화주Id == context.Owner))
            throw new InvalidDataException("현재 계정과 접수 번호의 운송 의뢰를 확인하지 못했습니다.");
        var confirmed = await _registration.접수결과저장Async(_submission with { ConfirmedRequestId = created.의뢰Id });
        if (!IsCurrent(context)) return;
        _submission = confirmed;
        CreatedRequestId = created.의뢰Id;
        SubmissionNotFound = false;
        SubmissionState = "등록 정본 확인";
        StatusSeverity = Severity.Success;
        var requiresPrepayment = created.정산시점?.ToString() == "선결제" && created.결제상태 == "결제대기";
        StatusMessage = requiresPrepayment ? "접수 준비 완료 · 결제 후 배차 접수" : "운송 의뢰 원장 등록을 확인했습니다. 이 접수를 다시 보내지 않습니다.";
        if (!navigate) return;
        if (afterCreated is not null)
        {
            var returnPath = await afterCreated(created.의뢰Id);
            if (!IsCurrent(context)) return;
            if (!string.IsNullOrWhiteSpace(returnPath)) { _navigation.NavigateTo(returnPath); return; }
        }
        var detailPath = requiresPrepayment
            ? new ShipperRequestDetailNavigationContext { ReturnPath = _navigationContext.ReturnPath }.PathFor(ShipperRequestDetailScreenKind.Payment, created.의뢰Id)
            : ShipperRoutes.CreatedRequestDetailFor(created.의뢰Id);
        if (string.Equals(_navigationContext.Source, CommunityMapApplicationRoutes.SourceCode, StringComparison.OrdinalIgnoreCase))
            detailPath += $"&source={Uri.EscapeDataString(CommunityMapApplicationRoutes.SourceCode)}";
        _navigation.NavigateTo(detailPath);
    }

    private void PreserveSubmission()
    {
        StatusSeverity = Severity.Warning;
        StatusMessage = "이전 접수의 입력과 번호를 유지했습니다. 결과를 먼저 확인해 주세요. 확인된 접수 뒤에만 새 의뢰를 작성할 수 있습니다.";
    }

    private SubmissionContext CurrentContext() => _submissionResults.Capture();
    private bool IsCurrent(SubmissionContext context) => _submissionResults.IsCurrent(context);
    private void OnAuthenticationChanged()
    {
        if (_owner == _authSession.UserId && _token == _authSession.AccessToken) return;
        _owner = _authSession.UserId; _token = _authSession.AccessToken; _submissionResults.Invalidate();
        _initialized = _autoSaveReady = IsBusy = _recoveryUnavailable = false;
        _submission = null; CreatedRequestId = null; LastDraft = null; SubmissionNotFound = false;
        RegistrationEnabled = null;
        SubmissionState = "작성 중"; StatusMessage = AutoSaveMessage = string.Empty;
        ApplicationPrivacyConsentEvidenceId = null; ApplicationSourceCode = string.Empty;
        SourcePost = null; SourcePostError = null; _loadedSourcePostId = null;
        State.Reset();
    }
    public void Dispose() { _submissionResults.Dispose(); State.차량추천초기화(); _authSession.Changed -= OnAuthenticationChanged; }

    public void SetApplicationPrivacyConsent(신청개인정보동의증적Response evidence)
    {
        ApplicationPrivacyConsentEvidenceId = evidence.증적Id;
        ApplicationSourceCode = evidence.출처Code;
    }

    private async Task RestoreDraftAsync()
    {
        try
        {
            if (!_authSession.IsLoggedIn) return;
            var context = CurrentContext();
            var restored = await _draftStorage.LoadForOwnerAsync(context.Owner);
            if (!IsCurrent(context)) return;
            if (restored is not null)
            {
                State.ApplyDraft(restored);
                LastDraft = restored;
                AutoSaveMessage = $"자동 저장 초안을 복구했습니다. 저장 시각: {restored.작성일시:MM-dd HH:mm}";
            }
        }
        catch (Exception ex)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = $"브라우저 초안을 복구하지 못했습니다: {ex.Message}";
        }
        finally
        {
            _autoSaveReady = true;
        }
    }

    private async Task LoadSourcePostAsync(long postId)
    {
        _loadedSourcePostId = postId;
        IsSourcePostLoading = true;
        SourcePost = null;
        SourcePostError = null;

        try
        {
            var post = await _communityPosts.GetPostAsync(postId);
            if (post is null)
            {
                SourcePostError = "가져올 커뮤니티 글을 찾을 수 없습니다.";
                return;
            }

            if (!_sourcePostImporter.가져올수있음(post))
            {
                SourcePostError = "화물 게시판 글만 운송 의뢰 초안으로 가져올 수 있습니다.";
                return;
            }

            SourcePost = post;
            SourcePostApplied = _sourcePostImporter.이미반영됨(State, post.Id);
        }
        catch (Exception ex)
        {
            SourcePostError = $"화물 글을 불러오지 못했습니다: {ex.Message}";
        }
        finally
        {
            IsSourcePostLoading = false;
        }
    }

    private static string BuildSourcePostPreview(string? body)
    {
        const int maximumLength = 240;
        var normalized = string.Join(
            " ",
            (body ?? string.Empty).Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maximumLength
            ? normalized
            : $"{normalized[..maximumLength].TrimEnd()}…";
    }
}
