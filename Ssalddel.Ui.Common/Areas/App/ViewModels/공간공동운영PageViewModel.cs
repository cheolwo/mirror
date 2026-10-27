using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>공동운영의 참여·합의를 조립합니다. 실제 재고·배송·정산 상태는 전문 원장의 조회 결과만 표시합니다.</summary>
public sealed class 공간공동운영PageViewModel : ObservableObject, IDisposable
{
    private readonly I공간공동운영Client _client;
    private readonly ISsalddel현재사용자Context _user;
    private readonly IWorkflowRecoveryContext _recovery;
    private readonly 공간공동운영DraftSession _draft;
    private readonly CancellationTokenSource _lifetime = new();
    private long _generation;
    private string? _owner;
    private bool _disposed;
    public 공간공동운영PageViewModel(I공간공동운영Client client, ISsalddel현재사용자Context user,
        공간공동운영DraftSession draft, IWorkflowRecoveryContext recovery)
    {
        _client = client; _user = user; _draft = draft; _recovery = recovery;
        _owner = CurrentOwner; _draft.Bind(_owner);
        if (_user is ISsalddel현재사용자변경알림 changes) changes.Changed += AuthenticationChanged;
    }
    private string? CurrentOwner => _user.현재사용자.인증됨 ? _user.현재사용자.UserId : null;
    public bool IsAuthenticated => CurrentOwner is not null && !RequiresLogin;
    public bool RequiresLogin { get; private set; }
    public bool NeedsReload { get; private set; } = true;
    public bool IsLoading { get; private set; }
    public bool IsSending { get; private set; }
    public bool MineLoaded { get; private set; }
    public bool HasPending => _draft.HasPending;
    public bool CanRetryPending { get; private set; }
    public string? Error { get; private set; }
    public string? PublicError { get; private set; }
    public string? RegionError { get; private set; }
    public string? CandidateError { get; private set; }
    public string? Notice { get; private set; }
    public string? RegionKey { get; set; }
    public int Page { get; private set; } = 1;
    public 공간공동운영공개Response? Public { get; private set; }
    public IReadOnlyList<NeighborhoodPublicRegionDto> Regions { get; private set; } = [];
    public IReadOnlyList<공간공동운영상세Response> Mine { get; private set; } = [];
    public 공간공동운영상세Response? Detail { get; private set; }
    public 공간공동운영공개제안Dto? SelectedPublic { get; private set; }
    public 공간공동운영생성Request Form => _draft.Form;
    public 공간공동운영조건Dto TermsDraft { get; private set; } = new();
    public string SelectedRoleCode { get; set; } = 공간공동운영역할Codes.SpaceProvider;
    public string PublicDisplayName { get; set; } = "";
    public bool ParticipationConfirmed { get; set; }
    public bool IntroductionConsented { get; set; }
    public bool TermsConfirmed { get; set; }
    public bool OperatorConfirmed { get; set; }
    public string? TargetOperatorUserId { get; set; }
    public bool OperatorHandoverConfirmed { get; set; }
    public IReadOnlyList<공간공동운영연결후보Dto> ReferenceCandidates { get; private set; } = [];
    public int SelectedReferenceIndex { get; set; } = -1;
    public string ResultNote { get; set; } = "";
    public 공간공동운영회차Dto? CurrentCycle => Detail?.Cycles.OrderByDescending(value => value.Number).FirstOrDefault();
    public bool Allows(string action) => IsAuthenticated && !HasPending && Detail?.AllowedActions.Contains(action) == true;
    public string RegionLabel(string key) => Regions.FirstOrDefault(value => value.RegionKey == key)?.DisplayName ?? "동네 확인 필요";

    public void NotifyAuthenticationChanged() => AuthenticationChanged();
    private void AuthenticationChanged()
    {
        if (_disposed) return;
        ++_generation; _owner = CurrentOwner; _draft.Bind(_owner);
        IsLoading = false; IsSending = false; RequiresLogin = false; NeedsReload = true;
        Mine = []; MineLoaded = false; Detail = null; Error = null; Notice = null; CanRetryPending = false;
        TermsConfirmed = false; OperatorConfirmed = false; ParticipationConfirmed = false;
        PublicDisplayName = ""; IntroductionConsented = false;
        TermsDraft = new(); ResultNote = ""; ReferenceCandidates = []; SelectedReferenceIndex = -1;
        TargetOperatorUserId = null; OperatorHandoverConfirmed = false; CandidateError = null;
        Changed();
    }
    private void SynchronizeOwner()
    { if (_owner != CurrentOwner) AuthenticationChanged(); }
    private bool Current(long generation)
    {
        if (_disposed) return false;
        SynchronizeOwner();
        return generation == _generation;
    }
    public async Task LoadAsync(string? ledgerId = null, int page = 1)
    {
        SynchronizeOwner();
        if (_disposed || IsSending) return;
        var generation = ++_generation;
        IsLoading = true; NeedsReload = false; Error = null; PublicError = null; RegionError = null;
        Detail = null; SelectedPublic = null; Mine = []; MineLoaded = false; Public = null; Page = Math.Clamp(page, 1, 10000); Changed();
        try
        {
            try
            {
                var regions = await _client.RegionsAsync(_lifetime.Token);
                if (!Current(generation)) return;
                Regions = regions.Items;
            }
            catch (Exception) when (Current(generation)) { RegionError = "동네 목록을 불러오지 못했습니다."; }
            if (!Current(generation)) return;
            try
            {
                var value = await _client.PublicAsync(RegionKey, Page, _lifetime.Token);
                if (!Current(generation)) return;
                Public = value;
                SelectedPublic = value.Items.FirstOrDefault(item => item.LedgerId == ledgerId);
                if (!string.IsNullOrWhiteSpace(ledgerId) && SelectedPublic is null)
                {
                    var invitation = await _client.InvitationAsync(ledgerId, _lifetime.Token);
                    if (!Current(generation)) return;
                    if (invitation?.LedgerId == ledgerId) SelectedPublic = invitation;
                }
            }
            catch (Exception) when (Current(generation)) { PublicError = "공개 모집 현황을 불러오지 못했습니다."; }
            if (!Current(generation)) return;
            if (IsAuthenticated)
            {
                var mine = await _client.MineAsync(_lifetime.Token);
                if (!Current(generation)) return;
                Mine = mine.Items; MineLoaded = true;
                if (!string.IsNullOrWhiteSpace(ledgerId))
                {
                    // 공개 제안의 비참여자는 먼저 참여하고 나서 당사자 상세를 조회합니다.
                    if (Mine.Any(item => item.LedgerId == ledgerId))
                    {
                        var detail = await _client.ReadAsync(ledgerId, _lifetime.Token);
                        if (!Current(generation)) return;
                        SetDetail(detail, ledgerId);
                        await LoadCandidatesAsync(generation);
                    }
                    else if (SelectedPublic is null) Error = "참여 중인 제안이나 공개 모집을 찾지 못했습니다.";
                }
            }
            else if (!string.IsNullOrWhiteSpace(ledgerId) && SelectedPublic is null && PublicError is null)
                Error = "현재 공개된 제안을 찾지 못했습니다. 목록에서 다시 선택해 주세요.";
        }
        catch (Exception exception) { if (Current(generation)) ApplyError(exception, false); }
        finally { if (Current(generation)) { IsLoading = false; Changed(); } }
    }

    public async Task<string?> CreateAsync()
    {
        if (!CanWrite()) return null;
        if (!ValidTerms(Form.Terms)) return null;
        if (Form.IntroductionConsented && (string.IsNullOrWhiteSpace(Form.PublicDisplayName) || Form.PublicDisplayName.Length > 40))
        { SetError("공개할 표시 이름을 40자 이내로 입력해 주세요."); return null; }
        if (!Regions.Any(value => value.RegionKey == Form.Terms.PublicNeighborhoodRegionKey))
        { SetError("목록에서 제안할 동네를 선택해 주세요."); return null; }
        if (!Form.ConfirmNonBinding) { SetError("제안과 실제 공간 사용·계약이 별도임을 확인해 주세요."); return null; }
        var scope = Capture(); if (scope is null) return null;
        _draft.PendingCreate = Clone(Form); _draft.PendingScope = scope;
        return await SendPendingAsync(scope);
    }

    public async Task<string?> JoinAsync(공간공동운영공개제안Dto proposal)
    {
        if (!CanWrite()) return null;
        if (!ParticipationConfirmed || !공간공동운영역할Codes.IsKnown(SelectedRoleCode))
        { SetError("참여 역할과 관심 참여 안내를 확인해 주세요."); return null; }
        var scope = Capture(); if (scope is null) return null;
        _draft.PendingLedgerId = proposal.LedgerId; _draft.PendingScope = scope;
        _draft.PendingCommand = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = proposal.Revision,
            ExpectedTermsRevision = proposal.TermsRevision, Action = 공간공동운영명령Codes.Join,
            RoleCode = SelectedRoleCode, ConfirmNonBinding = true };
        return await SendPendingAsync(scope);
    }

    public async Task<string?> ExecuteAsync(string action, string? roleCode = null, bool? consented = null, 공간공동운영전문참조Dto? reference = null)
    {
        if (!CanWrite() || Detail is not { } detail) return null;
        if (!detail.AllowedActions.Contains(action)) { SetError("현재 가능한 행동이 바뀌었습니다. 새로고침으로 확인해 주세요."); return null; }
        if (action == 공간공동운영명령Codes.AgreeTerms && !TermsConfirmed)
        { SetError("현재 판본의 운영 조건을 읽고 동의를 선택해 주세요."); return null; }
        if (action == 공간공동운영명령Codes.AcceptOperator && !OperatorConfirmed)
        { SetError("운영 담당의 역할과 책임을 확인해 주세요."); return null; }
        if (action is 공간공동운영명령Codes.AcceptRole or 공간공동운영명령Codes.Join && !ParticipationConfirmed)
        { SetError("할 수 있는 역할과 책임을 확인해 주세요."); return null; }
        if (action == 공간공동운영명령Codes.UpdateTerms && !ValidTerms(TermsDraft)) return null;
        if (action is 공간공동운영명령Codes.ProposeOperatorHandover or 공간공동운영명령Codes.AcceptOperatorHandover && !OperatorHandoverConfirmed)
        { SetError("운영 담당 인계와 진행 중인 업무를 확인해 주세요."); return null; }
        if (action == 공간공동운영명령Codes.ProposeOperatorHandover
            && !detail.Participants.Any(value => value.UserId == TargetOperatorUserId && !value.IsMe && value.ParticipationStateCode == "accepted"
                && value.RoleCode is 공간공동운영역할Codes.SpaceProvider or 공간공동운영역할Codes.Coordinator))
        { SetError("인계를 제안할 참여자를 선택해 주세요."); return null; }
        if (action == 공간공동운영명령Codes.IntroductionConsent && consented == true && (string.IsNullOrWhiteSpace(PublicDisplayName) || PublicDisplayName.Length > 40))
        { SetError("공개할 표시 이름을 40자 이내로 입력해 주세요."); return null; }
        if (action == 공간공동운영명령Codes.ProposalVisibility && consented is null)
        { SetError("공개 모집 여부를 직접 선택해 주세요."); return null; }
        if (action == 공간공동운영명령Codes.LinkReference && (SelectedReferenceIndex < 0 || SelectedReferenceIndex >= ReferenceCandidates.Count))
        { SetError("목록에서 연결할 업무를 선택해 주세요."); return null; }
        if (action == 공간공동운영명령Codes.UnlinkReference && (reference is null || CurrentCycle?.References.Any(value => value.KindCode == reference.KindCode && value.ReferenceId == reference.ReferenceId) != true))
        { SetError("현재 준비 회차에 연결된 업무를 선택해 주세요."); return null; }
        if (action == 공간공동운영명령Codes.CompleteCycle && ResultNote.Length > 1000)
        { SetError("회차 결과는 1,000자 이내로 입력해 주세요."); return null; }
        var scope = Capture(); if (scope is null) return null;
        _draft.PendingLedgerId = detail.LedgerId; _draft.PendingScope = scope;
        _draft.PendingCommand = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = detail.Revision,
            ExpectedTermsRevision = detail.TermsRevision, Action = action,
            RoleCode = action is 공간공동운영명령Codes.Join or 공간공동운영명령Codes.AcceptRole or 공간공동운영명령Codes.AgreeTerms or 공간공동운영명령Codes.IntroductionConsent or 공간공동운영명령Codes.Withdraw ? roleCode ?? SelectedRoleCode : roleCode,
            ConfirmNonBinding = true, Consented = action is 공간공동운영명령Codes.AgreeTerms or 공간공동운영명령Codes.AcceptOperator or 공간공동운영명령Codes.ProposeOperatorHandover or 공간공동운영명령Codes.AcceptOperatorHandover ? true : consented,
            TargetUserId = action == 공간공동운영명령Codes.ProposeOperatorHandover ? TargetOperatorUserId : null,
            PublicDisplayName = action == 공간공동운영명령Codes.IntroductionConsent && consented == true ? PublicDisplayName.Trim() : null,
            Terms = action == 공간공동운영명령Codes.UpdateTerms ? Clone(TermsDraft) : null,
            CycleId = CurrentCycle?.CycleId,
            Reference = action == 공간공동운영명령Codes.LinkReference ? Clone(ReferenceCandidates[SelectedReferenceIndex].Reference)
                : action == 공간공동운영명령Codes.UnlinkReference ? Clone(reference!) : null,
            ResultNote = action == 공간공동운영명령Codes.CompleteCycle ? ResultNote : null };
        return await SendPendingAsync(scope);
    }

    public async Task<string?> RecoverAsync()
    {
        SynchronizeOwner();
        if (_disposed || IsSending || !_draft.HasPending || _draft.PendingRequestId is not { } requestId) return null;
        var scope = Capture(); if (scope is null || !SamePendingScope(scope)) return null;
        var generation = ++_generation; IsSending = true; Error = null; CanRetryPending = false; Changed();
        try
        {
            var receipt = await _client.ReceiptAsync(requestId, _draft.PendingLedgerId, _lifetime.Token);
            if (!Current(generation) || !_recovery.IsCurrent(scope)) return null;
            if (receipt is null) { Notice = "처리 기록이 아직 확인되지 않았습니다. 원래 요청을 직접 다시 보내거나 결과를 다시 확인할 수 있습니다."; CanRetryPending = true; return null; }
            return await ReadCanonicalAsync(receipt, scope, generation);
        }
        catch (Exception exception) { if (Current(generation)) ApplyError(exception, false); return null; }
        finally { if (Current(generation)) { IsSending = false; Changed(); } }
    }
    public Task<string?> RetryPendingAsync()
    {
        if (!CanRetryPending || IsSending) return Task.FromResult<string?>(null);
        var scope = Capture();
        return scope is null || !SamePendingScope(scope) ? Task.FromResult<string?>(null) : SendPendingAsync(scope);
    }
    private async Task<string?> SendPendingAsync(WorkflowRecoverySnapshot scope)
    {
        var generation = ++_generation; IsSending = true; Error = null; Notice = null; CanRetryPending = false; Changed();
        try
        {
            var result = _draft.PendingCreate is { } create ? await _client.CreateAsync(Clone(create), _lifetime.Token)
                : await _client.CommandAsync(_draft.PendingLedgerId!, Clone(_draft.PendingCommand!), _lifetime.Token);
            if (!Current(generation) || !_recovery.IsCurrent(scope)) return null;
            if (result is null) { ApplyUnknown(); return null; }
            return await ReadCanonicalAsync(result, scope, generation);
        }
        catch (Exception exception)
        {
            if (!Current(generation)) return null;
            if (exception is SsalddelApiException { StatusCode: 400 or 403 or 409 or 422 }) _draft.ClearPending();
            ApplyError(exception, true); return null;
        }
        finally { if (Current(generation)) { IsSending = false; Changed(); } }
    }
    private async Task<string?> ReadCanonicalAsync(공간공동운영상세Response result, WorkflowRecoverySnapshot scope, long generation)
    {
        if (string.IsNullOrWhiteSpace(result.LedgerId) || _draft.PendingLedgerId is { } expected && result.LedgerId != expected)
        { ApplyUnknown(); return null; }
        var canonical = await _client.ReadAsync(result.LedgerId, _lifetime.Token);
        if (!Current(generation) || !_recovery.IsCurrent(scope)) return null;
        if (canonical is null || canonical.LedgerId != result.LedgerId || canonical.Revision < result.Revision)
        { ApplyUnknown(); return null; }
        SetDetail(canonical, result.LedgerId);
        var created = _draft.PendingCreate is not null;
        _draft.ClearPending(); if (created) _draft.ResetForm();
        Notice = "저장한 내용을 다시 확인했습니다.";
        CanRetryPending = false;
        await LoadCandidatesAsync(generation);
        if (!Current(generation) || !_recovery.IsCurrent(scope)) return null;
        return canonical.LedgerId;
    }
    private void SetDetail(공간공동운영상세Response? detail, string expected)
    {
        if (detail is null || detail.LedgerId != expected) { SetError("참여 중인 공동운영 내용을 찾지 못했습니다."); return; }
        Detail = detail; TermsDraft = Clone(detail.Terms);
        var me = detail.Participants.FirstOrDefault(value => value.IsMe && value.RoleCode == SelectedRoleCode && value.ParticipationStateCode != "withdrawn")
            ?? detail.Participants.FirstOrDefault(value => value.IsMe && value.ParticipationStateCode != "withdrawn");
        SelectedRoleCode = me?.RoleCode ?? 공간공동운영역할Codes.SpaceProvider;
        PublicDisplayName = me?.PublicDisplayName ?? ""; IntroductionConsented = me?.IntroductionConsented == true;
        TermsConfirmed = false; OperatorConfirmed = false; ParticipationConfirmed = false;
        ResultNote = CurrentCycle?.ResultNote ?? "";
        OperatorHandoverConfirmed = false; TargetOperatorUserId = null;
    }
    private async Task LoadCandidatesAsync(long generation)
    {
        ReferenceCandidates = []; SelectedReferenceIndex = -1; CandidateError = null;
        if (Detail is not { } detail || CurrentCycle is not { } cycle || !detail.AllowedActions.Contains(공간공동운영명령Codes.LinkReference)) return;
        try
        {
            var values = await _client.CandidatesAsync(detail.LedgerId, cycle.CycleId, _lifetime.Token);
            if (Current(generation)) ReferenceCandidates = values;
        }
        catch (Exception) { if (Current(generation)) CandidateError = "연결할 업무를 불러오지 못했습니다. 새로고침으로 다시 확인해 주세요."; }
    }
    private bool CanWrite()
    {
        SynchronizeOwner();
        if (_disposed || IsSending || IsLoading) return false;
        if (!IsAuthenticated) { RequiresLogin = true; SetError("로그인 후 돌아와 직접 선택해 주세요."); return false; }
        if (HasPending) { SetError("이전 요청의 처리 결과를 먼저 확인해 주세요."); return false; }
        return true;
    }
    private bool ValidTerms(공간공동운영조건Dto terms)
    {
        if (string.IsNullOrWhiteSpace(terms.Title) || terms.Title.Length > 100 || string.IsNullOrWhiteSpace(terms.Purpose) || terms.Purpose.Length > 300)
        { SetError("제목은 100자, 제안 내용은 300자 이내로 입력해 주세요."); return false; }
        if (!terms.RequiredRoleCodes.Contains(공간공동운영역할Codes.SpaceProvider))
        { SetError("공간 제공 역할을 포함해 필요한 역할을 선택해 주세요."); return false; }
        return true;
    }
    private WorkflowRecoverySnapshot? Capture()
    {
        try { var scope = _recovery.Capture(); if (scope.Owner != CurrentOwner) throw new InvalidOperationException(); return scope; }
        catch (Exception) { RequiresLogin = true; SetError("로그인한 계정과 서버를 확인한 뒤 다시 선택해 주세요."); return null; }
    }
    private bool SamePendingScope(WorkflowRecoverySnapshot scope)
    {
        if (_draft.PendingScope is { } pending && pending.Owner == scope.Owner && pending.EndpointKey == scope.EndpointKey) return true;
        SetError("원래 요청을 보낸 계정과 서버에서 처리 결과를 확인해 주세요."); return false;
    }
    private void ApplyUnknown() => SetError("처리 결과를 확인하지 못했습니다. 원래 요청의 결과를 먼저 조회해 주세요.");
    private void ApplyError(Exception exception, bool writing)
    {
        Error = exception is SsalddelApiException { StatusCode: 401 } ? "로그인이 만료되었습니다. 로그인 후 원래 요청의 결과를 확인해 주세요."
            : exception is SsalddelApiException { StatusCode: 403 } ? "이 업무를 확인하거나 변경할 권한이 없습니다."
            : exception is SsalddelApiException { StatusCode: 409 } ? "운영 조건이나 진행 상태가 바뀌었습니다. 새로고침해 확인해 주세요."
            : exception is SsalddelApiException { StatusCode: 400 or 422 } ? "입력과 동의 항목을 확인해 주세요."
            : writing ? "처리 결과를 확인하지 못했습니다. 원래 요청의 결과를 먼저 조회해 주세요."
            : "정보를 불러오지 못했습니다. 연결을 확인한 뒤 다시 불러와 주세요.";
        if (exception is SsalddelApiException { StatusCode: 401 }) RequiresLogin = true;
    }
    private void SetError(string value) { Error = value; Changed(); }
    private void Changed() { if (!_disposed) OnPropertyChanged(string.Empty); }
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; ++_generation;
        if (_user is ISsalddel현재사용자변경알림 changes) changes.Changed -= AuthenticationChanged;
        _lifetime.Cancel(); _lifetime.Dispose();
    }
}
