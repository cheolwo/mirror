using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

public sealed class 보호지원관리ViewModel(I보호지원관리Client client, Func<string?> adminOwner) : ObservableObject, IDisposable
{
    private string? _owner;
    private long _generation;
    private CancellationTokenSource _operation = new();
    private bool _disposed;
    private string? _caseId;
    private 보호지원CommandRequest? _pending;
    private string? _pendingCaseId;
    private IReadOnlyList<string> _recipientIds = [];
    public IReadOnlyList<보호지원CaseResponse> Items { get; private set; } = [];
    public 보호지원CaseResponse? Detail { get; private set; }
    public string Kind { get; private set; } = 보호지원종류Codes.분쟁;
    public int Page { get; private set; } = 1;
    public bool HasMore { get; private set; }
    public bool Loaded { get; private set; }
    public bool IsBusy { get; private set; }
    public bool RequiresLogin { get; private set; }
    public bool RequiresRefresh { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public bool HasPending => _pending is not null;
    public bool IsAuthenticated => !_disposed && !string.IsNullOrWhiteSpace(adminOwner());
    public int EvidenceCount { get; private set; }
    public int RecipientCount => _recipientIds.Count;
    /// <summary>명시적 감사 열람을 통과한 담당자에게만 한시적으로 표시하며 저장·로그·일반 상태로 복사하지 않습니다.</summary>
    public IReadOnlyList<보호지원증거Item> PrivateRecords { get; private set; } = [];
    public IReadOnlyList<string> PrivateRecipientIds => _recipientIds;
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
    public string PrivateEvidence { get; set; } = "";
    public string LegalBasis { get; set; } = "";
    public string LegalReasonCode { get; set; } = "statutory-retention";
    public string DeliveryEvidenceRef { get; set; } = "";
    public string LocalOccurredAt { get; set; } = "";
    public string LocalReviewAt { get; set; } = "";
    public int RecipientIndex { get; set; } = -1;
    public bool ActualDeliveryConfirmed { get; set; }
    public string OccurrenceCode { get; set; } = "unknown";
    public string AffectedPersonCount { get; set; } = "";
    public bool SensitiveAffected { get; set; }
    public bool ExternalIllegalAccess { get; set; }
    public string IndividualNoticeRequired { get; set; } = "unknown";
    public string AuthorityReportRequired { get; set; } = "unknown";
    private static readonly HashSet<string> SupportedActions = new(StringComparer.Ordinal)
    {
        "assign-self", "start-review", "add-evidence", "prepare-progress", "prepare-result",
        "record-progress-notified", "record-result-notified", "deny", "complete-rights", "close",
        "hold-record", "release-hold", "assess-incident", "prepare-incident-notice",
        "record-individual-notified", "record-authority-reported", "record-followup"
    };
    public IReadOnlyList<string> Actions => Detail is { RetentionPending: false } row
        ? row.AllowedActions.Where(SupportedActions.Contains).ToArray() : [];
    public bool IsNoticeAction => Action is "record-progress-notified" or "record-result-notified"
        or "record-individual-notified" or "record-authority-reported";
    public bool NeedsSummary => Action is "prepare-progress" or "prepare-result" or "prepare-incident-notice" or "deny";
    public bool NeedsPrivateEvidence => Action is "add-evidence" or "record-followup";
    public bool NeedsLegalBasis => Action is "deny" or "hold-record" or "assess-incident";
    public bool NeedsReviewAt => Action == "hold-record" || Action == "deny" && LegalReasonCode is "statutory-retention" or "ongoing-dispute";
    public bool CanSubmit => !IsBusy && !HasPending && !RequiresRefresh && IsAuthenticated
        && Detail is not null && Actions.Contains(Action) && ValidInput();

    public void SynchronizeOwner()
    {
        var owner = adminOwner();
        if (_owner == owner || _disposed) return;
        Cancel(); _owner = owner; Items = []; Detail = null; Loaded = false;
        _pending = null; _pendingCaseId = null; ClearPrivateRecords();
        Error = Notice = null; RequiresRefresh = false; RequiresLogin = string.IsNullOrWhiteSpace(owner);
        ClearDraft(); Changed();
    }
    public async Task LoadAsync(string? caseId = null, string? kind = null, int page = 1)
    {
        SynchronizeOwner();
        var selected = string.IsNullOrWhiteSpace(caseId) ? null : caseId;
        if (_caseId != selected)
        {
            Cancel(); Detail = null; ClearPrivateRecords();
            _pending = null; _pendingCaseId = null; ClearDraft();
        }
        _caseId = selected;
        if (!IsAuthenticated || HasPending || _disposed) return;
        if (kind is 보호지원종류Codes.분쟁 or 보호지원종류Codes.권리 or 보호지원종류Codes.사고) Kind = kind;
        var op = Begin(); ClearPrivateRecords(); IsBusy = true; Error = null; RequiresRefresh = false; Changed();
        try
        {
            if (selected is null)
            {
                var list = await client.목록Async(Kind, page, _operation.Token);
                if (!Current(op)) return;
                Items = list.Items; Page = list.Page; HasMore = list.HasMore;
            }
            else
            {
                var row = await client.상세Async(selected, _operation.Token);
                if (!Current(op)) return;
                if (row.CaseId != selected) throw new InvalidOperationException();
                Detail = row; SelectAllowedAction();
            }
            Loaded = true; RequiresLogin = false;
        }
        catch (Exception ex) { if (Current(op)) ApplyError(ex); }
        finally { if (Current(op)) { IsBusy = false; Changed(); } }
    }
    /// <summary>열람은 감사 기록과 Revision 갱신을 유발하며 담당자 전용 한시 표시만 허용합니다.</summary>
    public async Task VerifyRecipientsAsync()
    {
        SynchronizeOwner();
        if (!IsAuthenticated || IsBusy || HasPending || Detail is null
            || !Actions.Any(x => x != "assign-self")) return;
        var id = Detail.CaseId; var op = Begin(); ClearPrivateRecords(); IsBusy = true; Error = null; Changed();
        try
        {
            var evidence = await client.비공개확인Async(id, _operation.Token);
            if (!Current(op)) return;
            if (evidence.CaseId != id) throw new InvalidOperationException();
            var current = await client.상세Async(id, _operation.Token);
            if (!Current(op)) return;
            if (current.CaseId != id) throw new InvalidOperationException();
            _recipientIds = evidence.AuthorizedRecipientIds.Distinct(StringComparer.Ordinal).ToArray();
            PrivateRecords = evidence.Items; EvidenceCount = evidence.Items.Count; Detail = current; RecipientIndex = -1;
            RequiresRefresh = false; SelectAllowedAction();
            Notice = "감사 열람으로 비공개 기록을 확인했습니다. 담당 업무에만 사용하고 확인 후 닫아 주세요.";
        }
        catch (Exception ex) { if (Current(op)) { ClearPrivateRecords(); RequiresRefresh = true; ApplyError(ex); } }
        finally { if (Current(op)) { IsBusy = false; Changed(); } }
    }
    public async Task SubmitAsync()
    {
        SynchronizeOwner();
        if (!CanSubmit) return;
        _pending = BuildRequest(); _pendingCaseId = Detail!.CaseId;
        await SendPendingAsync();
    }
    public Task RetryAsync() => SendPendingAsync();
    private async Task SendPendingAsync()
    {
        SynchronizeOwner();
        if (!IsAuthenticated || IsBusy || _pending is null || _pendingCaseId is null) return;
        var request = _pending; var id = _pendingCaseId; var op = Begin(); IsBusy = true; Error = Notice = null; Changed();
        try
        {
            var row = await client.변경Async(id, request, _operation.Token);
            if (!Current(op)) return;
            if (row.CaseId != id) throw new InvalidOperationException();
            Detail = row; _pending = null; _pendingCaseId = null; ClearPrivateRecords();
            ClearDraft(); SelectAllowedAction(); RequiresRefresh = false;
            Notice = IsNotification(request.Action) ? "실제 전달 증빙을 기록했습니다. 전체 대상의 안내 완료 여부는 서버 상태에서 확인하세요."
                : request.Action.StartsWith("prepare-", StringComparison.Ordinal) ? "안내문을 준비했습니다. 실제 전달은 별도로 진행하고 증빙을 기록하세요."
                : request.Action == "complete-rights" ? "서버가 확인한 실제 처리 결과를 기록했습니다." : "처리 결과를 저장했습니다.";
        }
        catch (Exception ex)
        {
            if (!Current(op)) return;
            ClearPrivateRecords(); ApplyError(ex);
            // 보존 의도는 먼저 영속될 수 있습니다. 같은 요청으로 재확인할 수 있게 유지합니다.
            if (ex is SsalddelApiException { StatusCode: 400 or 403 or 404 or 409 or 422 } api
                && api.ErrorCode is not ("RetentionPending" or "RetentionSourceNotFound"))
            {
                _pending = null; _pendingCaseId = null; RequiresRefresh = true;
            }
        }
        finally { if (Current(op)) { IsBusy = false; Changed(); } }
    }
    private bool ValidInput()
    {
        if (NeedsSummary && string.IsNullOrWhiteSpace(Summary) || Summary.Length > 2000
            || NeedsPrivateEvidence && string.IsNullOrWhiteSpace(PrivateEvidence) || PrivateEvidence.Length > 8000
            || NeedsLegalBasis && string.IsNullOrWhiteSpace(LegalBasis) || LegalBasis.Length > 1000) return false;
        if (NeedsReviewAt && (!TryKstUtc(LocalReviewAt, out var review) || review <= Detail!.ServerNowUtc
            || Action == "hold-record" && review > Detail.ServerNowUtc.AddDays(90))) return false;
        if (IsNoticeAction)
        {
            if (!ActualDeliveryConfirmed || string.IsNullOrWhiteSpace(DeliveryEvidenceRef) || DeliveryEvidenceRef.Length > 200
                || !TryKstUtc(LocalOccurredAt, out var when)
                || when < (Detail!.KnownAtUtc ?? Detail.CreatedAtUtc)) return false;
            // 조회 이후 실제 안내가 진행될 수 있으므로 미래 시각은 명령을 받는 서버의 현재 시각으로 판정합니다.
            if (Action is "record-progress-notified" or "record-result-notified"
                && (RecipientIndex < 0 || RecipientIndex >= _recipientIds.Count)) return false;
        }
        if (Action == "complete-rights" && string.IsNullOrWhiteSpace(DeliveryEvidenceRef)) return false;
        if (Action == "assess-incident" && (!string.IsNullOrWhiteSpace(AffectedPersonCount)
            && (!int.TryParse(AffectedPersonCount, out var count) || count < 0))) return false;
        return true;
    }
    private 보호지원CommandRequest BuildRequest() => new()
    {
        ClientRequestId = Guid.NewGuid(), ExpectedRevision = Detail!.Revision, Action = Action,
        Summary = NeedsSummary ? Summary : "", PrivateEvidence = NeedsPrivateEvidence ? PrivateEvidence : "",
        LegalBasis = NeedsLegalBasis ? LegalBasis : null, LegalReasonCode = Action == "deny" ? LegalReasonCode : null,
        RetainUntilUtc = Action == "deny" && NeedsReviewAt && TryKstUtc(LocalReviewAt, out var retain) ? retain : null,
        HoldReviewAtUtc = Action == "hold-record" && TryKstUtc(LocalReviewAt, out var hold) ? hold : null,
        DeliveryEvidenceRef = IsNoticeAction || Action == "complete-rights" ? DeliveryEvidenceRef : null,
        OccurredAtUtc = IsNoticeAction && TryKstUtc(LocalOccurredAt, out var occurred) ? occurred : null,
        RecipientUserId = Action is "record-progress-notified" or "record-result-notified" ? _recipientIds[RecipientIndex]
            : Action == "record-individual-notified" ? "affected-individual" : Action == "record-authority-reported" ? "authority" : null,
        IncidentAssessment = Action == "assess-incident" ? new()
        {
            OccurrenceCode = OccurrenceCode, AffectedPersonCount = int.TryParse(AffectedPersonCount, out var count) ? count : null,
            SensitiveOrUniqueIdentifierAffected = SensitiveAffected, ExternalIllegalAccess = ExternalIllegalAccess,
            IndividualNoticeRequired = Bool(IndividualNoticeRequired), AuthorityReportRequired = Bool(AuthorityReportRequired), LegalBasis = LegalBasis
        } : null
    };
    private static bool? Bool(string value) => value == "yes" ? true : value == "no" ? false : null;
    private static bool IsNotification(string action) => action.StartsWith("record-", StringComparison.Ordinal) && action != "record-followup";
    public static bool TryKstUtc(string value, out DateTime utc)
    {
        utc = default;
        if (!DateTime.TryParseExact(value, ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var local) || local < DateTime.MinValue.AddHours(9)) return false;
        utc = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime;
        return true;
    }
    private void SelectAllowedAction() { if (!Actions.Contains(Action)) Action = Actions.FirstOrDefault() ?? ""; }
    private void ClearDraft()
    {
        Action = Summary = PrivateEvidence = LegalBasis = DeliveryEvidenceRef = LocalOccurredAt = LocalReviewAt = "";
        RecipientIndex = -1; ActualDeliveryConfirmed = false; LegalReasonCode = "statutory-retention";
        OccurrenceCode = "unknown"; AffectedPersonCount = ""; SensitiveAffected = ExternalIllegalAccess = false;
        IndividualNoticeRequired = AuthorityReportRequired = "unknown";
    }
    public void ClearPrivateRecords() { PrivateRecords = []; _recipientIds = []; EvidenceCount = 0; RecipientIndex = -1; Changed(); }
    private (long Generation, string? Owner) Begin() { Cancel(); return (_generation, _owner); }
    private void Cancel() { ++_generation; _operation.Cancel(); _operation.Dispose(); _operation = new(); IsBusy = false; }
    private bool Current((long Generation, string? Owner) op)
    {
        if (_disposed) return false;
        if (_owner != adminOwner()) { SynchronizeOwner(); return false; }
        return op.Generation == _generation && op.Owner == _owner;
    }
    private void ApplyError(Exception ex)
    {
        if (ex is SsalddelApiException { StatusCode: 401 or 403 })
        {
            ClearPrivateRecords(); Detail = null; Items = []; Loaded = false;
            _pending = null; _pendingCaseId = null; ClearDraft(); RequiresRefresh = true;
        }
        Error = ex is SsalddelApiException { StatusCode: 401 } ? "운영자 로그인이 만료되었습니다. 다시 로그인해 주세요."
            : ex is SsalddelApiException { StatusCode: 403 } ? "이 사건의 담당 권한이 없습니다. 배정 상태를 다시 확인해 주세요."
            : ex is SsalddelApiException { StatusCode: 409 } ? "사건 상태나 처리 조건이 바뀌었습니다. 최신 정보를 확인해 주세요."
            : ex is SsalddelApiException { StatusCode: 400 or 422 } ? "입력한 내용과 필수 증빙을 확인해 주세요."
            : "처리 결과를 확인하지 못했습니다. 연결을 확인하고 다시 시도해 주세요.";
        if (ex is SsalddelApiException { StatusCode: 401 }) RequiresLogin = true;
    }
    private void Changed() { if (!_disposed) OnPropertyChanged(string.Empty); }
    public void Dispose() { if (_disposed) return; _disposed = true; Cancel(); _operation.Dispose(); ClearPrivateRecords(); _pending = null; Items = []; Detail = null; ClearDraft(); }
}
