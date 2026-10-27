using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.App.ViewModels.Transport;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SubmissionContext = Ssalddel.Ui.Common.Areas.App.ViewModels.Transport.ShipperSubmissionResultSession.Context;

namespace SsalddelApp.ViewModels.Shipper;

/// <summary>같은 접수의 원문·요청 번호를 보존하고 불명확한 응답은 본인 결과 조회부터 복구합니다.</summary>
public sealed class ShipperRequestAuthoringPageViewModel : IDisposable
{
    public const string RecoveryScope = "shipper-request-create.v1";
    private readonly IShipperOperationsService _operations;
    private readonly NavigationManager _navigation;
    private readonly IAuthSession? _auth;
    private readonly IWorkflowRecoveryStore? _recovery;
    private readonly IWorkflowRecoveryContext? _recoveryContext;
    private WorkflowRecoverySnapshot? _recoveryIdentity;
    private string _recoveryScope = RecoveryScope;
    private string _draftScope = "shipper-request-draft.v1";
    private WorkflowRecoveryRecord? _draftRecord;
    private readonly SemaphoreSlim _draftGate = new(1, 1);
    private bool _unboundLegacyRecovery;
    private readonly ShipperSubmissionResultSession _submissionResults;
    private bool _initialized;
    private bool _initializing;
    private long _sessionRevision;
    private WorkflowRecoveryRecord? _pending;
    private ShipperRequestItem? _pendingRequest;
    private bool _pendingNotFound;
    private Guid _consentRequestId = Guid.NewGuid();
    private long _consentGeneration;
    private bool _collectionConsent;
    private bool _ageConfirmed;

    public ShipperRequestAuthoringPageViewModel(운송의뢰작성ViewModel state,
        IShipperOperationsService operations, NavigationManager navigation,
        IAuthSession? auth = null, IWorkflowRecoveryStore? recovery = null,
        IWorkflowRecoveryContext? recoveryContext = null)
    {
        State = state;
        _operations = operations;
        _navigation = navigation;
        _auth = auth;
        _recovery = recovery;
        _recoveryContext = recoveryContext;
        _submissionResults = new(
            () => _auth is null ? new("local-session", "0", true)
                : new(_auth.UserId, _auth.SessionRevision.ToString(System.Globalization.CultureInfo.InvariantCulture), _auth.IsLoggedIn),
            "로그인 후 접수 결과를 확인해 주세요.");
        _sessionRevision = auth?.SessionRevision ?? 0;
        if (_auth is not null) _auth.Changed += OnAuthenticationChanged;
    }

    public 운송의뢰작성ViewModel State { get; }
    public IReadOnlyList<string> VehicleTypes { get; private set; } = [];
    public bool? RegistrationEnabled { get; private set; }
    public bool IsBusy { get; private set; }
    public string StatusMessage { get; private set; } = string.Empty;
    public Severity StatusSeverity { get; private set; } = Severity.Info;
    public bool HasPendingRequest => _pending is not null;
    public bool CanRetryPendingRequest => _pendingNotFound && PendingConsentBound && !IsBusy;
    private bool PendingConsentBound => _pendingRequest?.신청개인정보동의증적Id is { } id && id != Guid.Empty
        && _pendingRequest.신청출처Code == 신청개인정보출처Codes.커뮤니티지도;
    public Guid? PrivacyConsentEvidenceId { get; private set; }
    public bool CollectionConsent
    {
        get => _collectionConsent;
        set { _collectionConsent = value; if (!value) ClearPrivacyConsent(); }
    }
    public bool AgeConfirmed
    {
        get => _ageConfirmed;
        set { _ageConfirmed = value; if (!value) ClearPrivacyConsent(); }
    }
    public bool HasValidPrivacyConsent => PrivacyConsentEvidenceId is { } id && id != Guid.Empty && CollectionConsent && AgeConfirmed;
    public bool CanRecordPrivacyConsent => !IsBusy && !HasPendingRequest && CollectionConsent && AgeConfirmed && !HasValidPrivacyConsent
        && (_auth is null || _auth.IsLoggedIn && !string.IsNullOrWhiteSpace(_auth.UserId));
    public bool PendingInputExpired => _pending is not null && _pendingRequest is null;
    public string RegistrationBoundaryMessage { get; private set; }
        = "등록 준비를 확인하고 있습니다. 등록만으로 자동 배차·계약·결제가 확정되지는 않습니다.";

    public async Task InitializeAsync()
    {
        if (_recoveryContext is not null && _recoveryIdentity is not null && !_recoveryContext.IsCurrent(_recoveryIdentity))
        {
            _submissionResults.Invalidate();
            _initialized = _initializing = IsBusy = false;
            RegistrationEnabled = null;
            _recoveryIdentity = null;
            _pending = null; _pendingRequest = null; _pendingNotFound = false;
            _draftRecord = null; _unboundLegacyRecovery = false;
            State.Reset(); ResetPrivacyConsent();
        }
        if (_submissionResults.IsDisposed || _initialized || _initializing) return;
        _initializing = true;
        var generation = _submissionResults.Generation;
        try
        {
            if (_auth is not null) await _auth.RestoreAsync();
            generation = _submissionResults.Generation;
            var context = CurrentContext();
            _recoveryIdentity = _recoveryContext?.Capture();
            _recoveryScope = _recoveryIdentity?.Scope(RecoveryScope) ?? RecoveryScope;
            _draftScope = _recoveryIdentity?.Scope("shipper-request-draft.v1") ?? "shipper-request-draft.v1";
            if (_auth is not null && _recovery is null)
                throw new InvalidOperationException("이전 접수를 보호할 저장소를 확인하지 못했습니다.");
            // 화물 조건 없는 초기 추천을 차량 목록이나 적재 적합성 근거로 사용하지 않습니다.
            VehicleTypes = [];
            if (_recovery is not null)
            {
                var pending = await _recovery.LoadAsync(context.Owner, _recoveryScope, CancellationToken.None);
                if (!IsCurrent(context)) return;
                // 이전 비서버별 기록의 서버를 추측해 새 서버로 재전송하거나 조회하지 않습니다.
                if (pending is null && _recoveryIdentity is not null)
                {
                    var legacy = await _recovery.LoadAsync(context.Owner, RecoveryScope, CancellationToken.None);
                    if (!IsCurrent(context)) return;
                    if (legacy is not null)
                    {
                        _unboundLegacyRecovery = true;
                        pending = legacy with { PayloadJson = null };
                    }
                }
                ApplyPending(pending);
                if (pending is null) await RestoreDraftAsync(context);
            }
            RegistrationEnabled = true;
            _initialized = true;
            RegistrationBoundaryMessage = "등록만으로 자동 배차·계약·결제를 확정하지 않습니다. 응답을 확인하지 못한 접수는 같은 번호로 결과를 먼저 확인합니다.";
            if (_unboundLegacyRecovery)
            {
                StatusMessage = "이전 접수의 서버를 확인하지 못했습니다. 원래 서버에서 결과를 확인한 뒤 계속해 주세요. 새 요청은 보내지 않습니다.";
                StatusSeverity = Severity.Warning;
            }
            else if (_pending is not null) await RecoverPendingCoreAsync(context);
            else if (_draftRecord?.PayloadJson is not null)
            {
                StatusMessage = "저장한 입력을 복원했습니다. 주소·조건과 동의를 다시 확인해 주세요. 의뢰는 전송하지 않았습니다.";
                StatusSeverity = Severity.Info;
            }
            else { StatusMessage = string.Empty; StatusSeverity = Severity.Info; }
        }
        catch (Exception ex)
        {
            if (_submissionResults.IsDisposed || generation != _submissionResults.Generation) return;
            VehicleTypes = [];
            RegistrationEnabled = false;
            StatusSeverity = Severity.Warning;
            StatusMessage = $"등록 준비를 확인하지 못했습니다. 입력을 유지했으니 다시 확인해 주세요. {ex.Message}";
            RegistrationBoundaryMessage = "등록 준비를 다시 확인한 뒤 계속할 수 있습니다. 기존 접수 결과가 있으면 먼저 조회합니다.";
        }
        finally { if (generation == _submissionResults.Generation) _initializing = false; }
    }

    public async Task SaveAsync()
    {
        if (!await SaveDraftAsync()) return;
        StatusSeverity = State.서버등록가능 ? Severity.Success : Severity.Warning;
        StatusMessage = State.서버등록가능
            ? "보호 저장소에 입력을 저장했습니다. 앱을 다시 열어도 복원할 수 있습니다. 등록 전 내용을 확인해 주세요."
            : "입력을 저장했습니다. 앱을 다시 열어도 복원할 수 있습니다. 등록 전 필수 입력을 보완해 주세요.";
    }
    public async Task AutoSaveAsync() { await SaveDraftAsync(); }
    public async Task ResetAsync()
    {
        if (IsBusy || HasPendingRequest)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "이전에 보낸 접수 결과를 먼저 확인해 주세요. 새 요청으로 바꾸지 않았습니다.";
            return;
        }
        var context = CurrentContext();
        try { await ClearDraftAsync(context); }
        catch (Exception)
        {
            if (IsCurrent(context))
            {
                StatusSeverity = Severity.Warning;
                StatusMessage = "저장한 입력을 정리하지 못해 초기화하지 않았습니다. 다시 시도해 주세요.";
            }
            return;
        }
        if (!IsCurrent(context)) return;
        State.Reset();
        ResetPrivacyConsent();
        StatusSeverity = Severity.Info;
        StatusMessage = "운송 의뢰 입력값을 초기화했습니다.";
    }
    public async Task<decimal?> EstimateFareAsync(string vehicleType, decimal distanceKm)
        => await _operations.EstimateFareAsync(new 화주운송기준운임견적요청
        {
            차량종류 = vehicleType,
            예상거리Km = distanceKm,
            상차위도 = State.상차위도,
            상차경도 = State.상차경도,
            하차위도 = State.하차위도,
            하차경도 = State.하차경도,
            대기료 = State.대기료,
            수작업비 = State.수작업비,
            할증 = State.할증
        });

    public async Task<차량추천응답> RecommendVehiclesAsync(차량추천요청 request, CancellationToken cancellationToken)
    {
        var context = CurrentContext();
        if (!IsCurrent(context)) throw new OperationCanceledException();
        var response = await _operations.RecommendVehiclesAsync(request, cancellationToken);
        if (!IsCurrent(context)) throw new OperationCanceledException();
        return response;
    }

    public async Task RecordPrivacyConsentAsync()
    {
        if (_submissionResults.IsDisposed || IsBusy || HasPendingRequest) return;
        if (!CollectionConsent || !AgeConfirmed)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "개인정보 수집·이용 안내와 본인·연령 확인을 모두 선택해 주세요.";
            return;
        }
        SubmissionContext context;
        try { context = CurrentContext(); }
        catch (InvalidOperationException ex)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = ex.Message;
            return;
        }
        var generation = _consentGeneration;
        var requestId = _consentRequestId;
        if (!IsCurrent(context))
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "계정 또는 연결 서버가 변경되었습니다. 작성 준비를 다시 확인해 주세요.";
            return;
        }
        IsBusy = true;
        try
        {
            var evidence = await _operations.RecordPrivacyConsentAsync(new()
            {
                증적Id = requestId, 업무Code = 신청개인정보업무Codes.운송대행,
                출처Code = 신청개인정보출처Codes.커뮤니티지도, 동의문버전 = 신청개인정보동의정책.현재버전,
                수집이용동의 = CollectionConsent, 연령요건확인 = AgeConfirmed
            });
            if (!IsCurrent(context) || generation != _consentGeneration || !CollectionConsent || !AgeConfirmed) return;
            if (evidence is null || evidence.증적Id != requestId || evidence.상태Code != 신청개인정보동의상태Codes.유효
                || evidence.업무Code != 신청개인정보업무Codes.운송대행 || evidence.출처Code != 신청개인정보출처Codes.커뮤니티지도
                || evidence.동의문버전 != 신청개인정보동의정책.현재버전)
                throw new InvalidOperationException("본인 운송 신청의 유효한 동의 증적을 확인하지 못했습니다.");
            PrivacyConsentEvidenceId = evidence.증적Id;
            StatusSeverity = Severity.Success;
            StatusMessage = "운송 신청의 개인정보 동의 기록을 확인했습니다. 최종 조건을 확인한 뒤 의뢰를 등록해 주세요.";
        }
        catch (Exception ex)
        {
            if (!IsCurrent(context) || generation != _consentGeneration) return;
            PrivacyConsentEvidenceId = null;
            StatusSeverity = Severity.Warning;
            StatusMessage = $"개인정보 동의 기록을 확인하지 못했습니다. 의뢰는 보내지 않았습니다. {ex.Message}";
        }
        finally { if (IsCurrent(context)) IsBusy = false; }
    }

    public async Task SubmitAsync()
    {
        if (_submissionResults.IsDisposed || IsBusy) return;
        if (HasPendingRequest) { await RecoverPendingAsync(); return; }
        if (RegistrationEnabled != true || !State.서버등록가능)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = RegistrationEnabled != true ? "등록 준비를 먼저 확인해 주세요."
                : "등록 전 점검 목록의 필수 항목을 보완해 주세요.";
            return;
        }
        try { _ = ShipperRequestItem.신규결제수단확인(State.결제수단); }
        catch (InvalidOperationException ex)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = ex.Message;
            return;
        }
        if (!HasValidPrivacyConsent)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "본인 신청의 개인정보 수집·이용 동의를 먼저 기록해 주세요.";
            return;
        }
        var context = CurrentContext();
        if (!IsCurrent(context))
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "계정 또는 연결 서버가 변경되었습니다. 작성 준비를 다시 확인해 주세요. 의뢰는 보내지 않았습니다.";
            return;
        }
        var id = Guid.NewGuid();
        _pendingRequest = CreateRequest(ClientRequestId(id));
        _pending = new(context.Owner, _recoveryScope, id,
            JsonSerializer.Serialize(new SubmissionPayload(_pendingRequest, State.ToDraft())), DateTime.UtcNow);
        await SendPendingAsync(context);
    }

    public async Task RecoverPendingAsync()
    {
        if (_submissionResults.IsDisposed || IsBusy || _pending is null) return;
        var context = CurrentContext();
        IsBusy = true;
        try { await RecoverPendingCoreAsync(context); }
        finally { if (IsCurrent(context)) IsBusy = false; }
    }

    public async Task RetryPendingAsync()
    {
        if (_submissionResults.IsDisposed || IsBusy || !CanRetryPendingRequest) return;
        var context = CurrentContext();
        IsBusy = true;
        try
        {
            // 명시적으로 재등록을 선택해도 먼저 조회합니다. 조회 실패·만료 원문은 POST하지 않습니다.
            await RecoverPendingCoreAsync(context);
            if (!IsCurrent(context) || _pending is null || !_pendingNotFound || _pendingRequest is null) return;
        }
        finally { if (IsCurrent(context)) IsBusy = false; }
        if (IsCurrent(context) && _pending is not null && _pendingNotFound && _pendingRequest is not null)
            await SendPendingAsync(context);
    }

    private async Task SendPendingAsync(SubmissionContext context)
    {
        if (_pending is null || _pendingRequest is null || !PendingConsentBound || !IsCurrent(context)) return;
        IsBusy = true;
        _pendingNotFound = false;
        StatusSeverity = Severity.Info;
        StatusMessage = "같은 접수 번호로 운송 의뢰 등록을 요청하고 있습니다.";
        try
        {
            if (WorkflowRecoveryPolicy.Expire(_pending, DateTime.UtcNow).PayloadJson is null)
            {
                ApplyPending(_pending with { PayloadJson = null });
                if (_recovery is not null) await _recovery.SaveAsync(_pending!, CancellationToken.None);
                if (IsCurrent(context)) await RecoverPendingCoreAsync(context);
                return;
            }
            if (_recovery is not null)
            {
                await _recovery.SaveAsync(_pending, CancellationToken.None);
                if (!IsCurrent(context)) return;
                var saved = await _recovery.LoadAsync(context.Owner, _recoveryScope, CancellationToken.None);
                if (!IsCurrent(context)) return;
                if (saved?.RequestId != _pending.RequestId || saved.Owner != context.Owner || saved.Scope != _recoveryScope
                    || saved.Target != _pending.Target || saved.PayloadJson != _pending.PayloadJson)
                    throw new InvalidDataException("원 요청의 보호 저장을 다시 확인하지 못했습니다. 새 등록은 보내지 않았습니다.");
            }
            if (!IsCurrent(context)) return;
            var created = await _operations.AddRequestAsync(_pendingRequest);
            if (!IsCurrent(context)) return;
            await FinishPendingAsync(created, context);
        }
        catch (Exception ex)
        {
            if (!IsCurrent(context)) return;
            if (_recovery is not null)
            {
                try
                {
                    var stored = await _recovery.LoadAsync(context.Owner, _recoveryScope, CancellationToken.None);
                    if (!IsCurrent(context)) return;
                    if (stored is not null && (stored.RequestId != _pending?.RequestId || stored.PayloadJson is null)) ApplyPending(stored);
                }
                catch (Exception) { /* 보호 기록 읽기 실패는 원 요청을 지우거나 POST하는 근거가 아닙니다. */ }
            }
            StatusSeverity = Severity.Warning;
            StatusMessage = $"등록 결과를 아직 확인하지 못했습니다. 접수 번호와 원문을 유지했습니다. {ex.Message}";
            // 응답 유실은 미저장으로 단정하지 않습니다. 여기서는 GET만 수행합니다.
            await RecoverPendingCoreAsync(context);
        }
        finally { if (IsCurrent(context)) IsBusy = false; }
    }

    private async Task RecoverPendingCoreAsync(SubmissionContext context)
    {
        if (_pending is null || !IsCurrent(context) || _unboundLegacyRecovery) return;
        _pendingNotFound = false;
        var result = await _submissionResults.ReconcileAsync(context,
            () => _operations.GetRequestByClientRequestIdAsync(ClientRequestId(_pending.RequestId)),
            created => FinishPendingAsync(created, context));
        if (!IsCurrent(context)) return;
        if (result.Kind == ShipperSubmissionResultKind.NotFound)
        {
            _pendingNotFound = true;
            StatusSeverity = Severity.Warning;
            StatusMessage = _pendingRequest is null
                ? "이전 접수 결과를 찾지 못했습니다. 보관 기한이 지나 원문은 삭제됐으며 접수 번호는 유지합니다. 새 의뢰로 우회하지 않고 결과를 다시 확인해 주세요."
                : !PendingConsentBound
                    ? "이전 접수의 개인정보 동의 증적을 확인할 수 없어 결과 조회만 가능합니다. 기존 접수 번호를 유지하고 결과를 다시 확인해 주세요."
                : "이전 접수 결과를 아직 찾지 못했습니다. 원문과 번호는 유지했습니다. 같은 요청으로 다시 등록을 선택할 수 있습니다.";
        }
        else if (result.Kind == ShipperSubmissionResultKind.Failed)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = $"이전 접수 결과를 확인하지 못했습니다. 새 등록은 보내지 않았습니다. 결과 확인을 다시 시도해 주세요. {result.Error!.Message}";
        }
    }

    private async Task FinishPendingAsync(ShipperRequestItem created, SubmissionContext context)
    {
        if (string.IsNullOrWhiteSpace(created.의뢰Id)) throw new InvalidDataException("등록된 운송 의뢰 번호를 확인하지 못했습니다.");
        await ClearDraftAsync(context);
        if (!IsCurrent(context)) return;
        if (_pending is not null && _recovery is not null)
            await _recovery.RemoveAsync(context.Owner, _recoveryScope, _pending.RequestId, CancellationToken.None);
        if (!IsCurrent(context)) return;
        _pending = null;
        _pendingRequest = null;
        _pendingNotFound = false;
        ResetPrivacyConsent();
        StatusSeverity = Severity.Success;
        var requiresPrepayment = created.정산시점 == "선결제" && created.결제상태 == "결제대기";
        StatusMessage = requiresPrepayment ? "접수 준비 완료 · 결제 후 배차 접수" : "운송 의뢰 원장 등록을 확인했습니다.";
        var route = ShipperRequestNavigationContext.Parse(_navigation.Uri);
        var next = requiresPrepayment
            ? new ShipperRequestDetailNavigationContext().PathFor(ShipperRequestDetailScreenKind.Payment, created.의뢰Id)
            : ShipperRoutes.RequestDetailFor(created.의뢰Id);
        _navigation.NavigateTo(PageNavigationContext.WithReturnPath(next, route.ReturnPath));
    }

    private ShipperRequestItem CreateRequest(string clientRequestId)
    {
        var draft = State.ToDraft();
        return new()
        {
            신청개인정보동의증적Id = PrivacyConsentEvidenceId,
            신청출처Code = 신청개인정보출처Codes.커뮤니티지도,
            의뢰Id = clientRequestId, 화물종류 = draft.화물종류, 화물적재형태 = draft.화물적재형태,
            화물원본 = new CargoDTO
            {
                화물종류 = draft.화물종류, 설명 = draft.화물설명,
                수량 = draft.화물수량, 중량Kg = draft.화물중량Kg,
                길이Mm = draft.화물길이Mm, 폭Mm = draft.화물폭Mm,
                높이Mm = draft.화물높이Mm, 팔레트개수 = draft.팔레트개수,
                부피Cbm = draft.화물부피Cbm, 온도조건 = draft.온도조건,
                바닥회전가능여부 = draft.화물바닥회전가능여부,
                적층가능여부 = draft.화물적층가능여부, 화물파손주의여부 = draft.화물파손주의여부
            },
            의뢰상태 = "접수", 결제상태 = draft.결제예정금액.HasValue ? "결제대기" : "대기", 배차상태 = "매칭중",
            운송방식 = draft.운송방식, 차량종류 = draft.차량종류 ?? "1톤 카고", 결제수단 = draft.결제수단,
            결제예정금액 = draft.결제예정금액, 예상거리Km = draft.예상거리Km, 기준운임 = draft.기준운임,
            기사지급예정운임 = draft.기사지급예정운임, 알선단계 = draft.알선단계, 재알선금지 = draft.재알선금지,
            서비스레벨 = draft.서비스레벨, 요청사항 = draft.요청사항,
            대기료 = draft.대기료, 수작업비 = draft.수작업비, 할증 = draft.할증,
            알선소Id = draft.알선소Id, 절차메모 = draft.절차메모,
            재알선의심 = draft.재알선의심, 정책위반 = draft.정책위반, 정책경고목록 = draft.정책경고목록,
            생성일시 = DateTime.Now, 픽업지 = draft.픽업도로명주소, 하차지 = draft.하차도로명주소,
            픽업정보 = ShipperRequestHandoffMapper.CreatePickup(draft), 하차정보 = ShipperRequestHandoffMapper.CreateDropoff(draft)
        };
    }

    private static string ClientRequestId(Guid id) => $"shipper-app-{id:N}";
    private void ApplyPending(WorkflowRecoveryRecord? pending)
    {
        _pending = pending;
        var payload = pending?.PayloadJson is null ? null
            : JsonSerializer.Deserialize<SubmissionPayload>(pending.PayloadJson)
              ?? throw new InvalidDataException("이전 접수 원문을 확인하지 못했습니다.");
        _pendingRequest = payload?.Request;
        if (_pendingRequest is not null && _pendingRequest.의뢰Id != ClientRequestId(pending!.RequestId))
            throw new InvalidDataException("이전 접수 번호와 원문이 다릅니다.");
        if (payload is not null) State.ApplyDraft(payload.Draft);
    }
    private SubmissionContext CurrentContext() => _submissionResults.Capture();
    private bool IsCurrent(SubmissionContext context) => _submissionResults.IsCurrent(context)
        && (_recoveryContext is null || _recoveryIdentity is not null && _recoveryContext.IsCurrent(_recoveryIdentity));
    private void OnAuthenticationChanged()
    {
        if (_auth is null || _sessionRevision == _auth.SessionRevision) return;
        _sessionRevision = _auth.SessionRevision;
        _submissionResults.Invalidate();
        _initialized = _initializing = IsBusy = false;
        RegistrationEnabled = null;
        VehicleTypes = [];
        _pending = null;
        _pendingRequest = null;
        _pendingNotFound = false;
        _draftRecord = null;
        _recoveryIdentity = null;
        _unboundLegacyRecovery = false;
        State.Reset();
        ResetPrivacyConsent();
        StatusMessage = "로그인한 계정의 접수 결과를 다시 확인해 주세요.";
        StatusSeverity = Severity.Info;
    }
    public void Dispose()
    {
        _submissionResults.Dispose();
        State.차량추천초기화();
        if (_auth is not null) _auth.Changed -= OnAuthenticationChanged;
        _pending = null;
        _pendingRequest = null;
        ResetPrivacyConsent();
    }
    private void ClearPrivacyConsent()
    {
        ++_consentGeneration;
        PrivacyConsentEvidenceId = null;
        _consentRequestId = Guid.NewGuid();
    }
    private void ResetPrivacyConsent()
    {
        _collectionConsent = _ageConfirmed = false;
        ClearPrivacyConsent();
    }
    private sealed record SubmissionPayload(ShipperRequestItem Request, 운송모델작성Draft Draft);

    private async Task RestoreDraftAsync(SubmissionContext context)
    {
        if (_recovery is null) return;
        var record = await _recovery.LoadAsync(context.Owner, _draftScope, CancellationToken.None);
        if (!IsCurrent(context) || record is null) return;
        ValidateDraft(record, context.Owner);
        if (record.PayloadJson is null)
        {
            await _recovery.RemoveAsync(context.Owner, _draftScope, record.RequestId, CancellationToken.None);
            return;
        }
        var draft = JsonSerializer.Deserialize<운송모델작성Draft>(record.PayloadJson)
            ?? throw new InvalidDataException("저장한 입력을 확인하지 못했습니다.");
        State.ApplyDraft(draft);
        ResetPrivacyConsent();
        _draftRecord = record;
    }

    private async Task<bool> SaveDraftAsync()
    {
        if (_submissionResults.IsDisposed || !_initialized || IsBusy || HasPendingRequest) return false;
        if (_recovery is null)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "재실행 복원을 위한 보호 저장소를 확인하지 못했습니다. 현재 화면의 입력은 유지합니다.";
            return false;
        }
        SubmissionContext context;
        try { context = CurrentContext(); }
        catch (InvalidOperationException) { return false; }
        var scope = _draftScope;
        var json = JsonSerializer.Serialize(State.ToDraft());
        await _draftGate.WaitAsync();
        try
        {
            if (!IsCurrent(context) || IsBusy || HasPendingRequest) return false;
            var stored = await _recovery.LoadAsync(context.Owner, scope, CancellationToken.None);
            if (!IsCurrent(context) || IsBusy || HasPendingRequest) return false;
            if (stored is not null) ValidateDraft(stored, context.Owner);
            if (stored?.PayloadJson is null && stored is not null)
            {
                await _recovery.RemoveAsync(context.Owner, scope, stored.RequestId, CancellationToken.None);
                if (!IsCurrent(context)) return false;
                stored = null;
            }
            var record = new WorkflowRecoveryRecord(context.Owner, scope, stored?.RequestId ?? Guid.NewGuid(), json,
                stored?.CreatedAtUtc ?? DateTime.UtcNow);
            await _recovery.SaveAsync(record, CancellationToken.None);
            if (!IsCurrent(context)) return false;
            var saved = await _recovery.LoadAsync(context.Owner, scope, CancellationToken.None);
            if (!IsCurrent(context)) return false;
            if (saved?.RequestId != record.RequestId || saved.Owner != context.Owner || saved.Scope != scope
                || saved.Target is not null || saved.PayloadJson != json)
                throw new InvalidDataException("저장한 입력을 다시 확인하지 못했습니다.");
            _draftRecord = saved;
            return true;
        }
        catch (Exception)
        {
            if (IsCurrent(context))
            {
                StatusSeverity = Severity.Warning;
                StatusMessage = "입력을 보호 저장소에 저장하지 못했습니다. 현재 화면의 입력을 유지했으니 다시 저장해 주세요.";
            }
            return false;
        }
        finally { _draftGate.Release(); }
    }

    private async Task ClearDraftAsync(SubmissionContext context)
    {
        if (_recovery is null) return;
        var scope = _draftScope;
        await _draftGate.WaitAsync();
        try
        {
            if (!IsCurrent(context)) return;
            var record = await _recovery.LoadAsync(context.Owner, scope, CancellationToken.None);
            if (!IsCurrent(context)) return;
            if (record is not null)
            {
                ValidateDraft(record, context.Owner);
                await _recovery.RemoveAsync(context.Owner, scope, record.RequestId, CancellationToken.None);
                if (!IsCurrent(context)) return;
            }
            _draftRecord = null;
        }
        finally { _draftGate.Release(); }
    }

    private void ValidateDraft(WorkflowRecoveryRecord record, string owner)
    {
        WorkflowRecoveryPolicy.Validate(record);
        if (record.Owner != owner || record.Scope != _draftScope || record.Target is not null)
            throw new InvalidDataException("저장한 입력의 계정과 서버가 다릅니다.");
    }
}
