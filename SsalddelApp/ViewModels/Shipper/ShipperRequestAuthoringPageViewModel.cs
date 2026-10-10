using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;

namespace SsalddelApp.ViewModels.Shipper;

/// <summary>같은 접수의 원문·요청 번호를 보존하고 불명확한 응답은 본인 결과 조회부터 복구합니다.</summary>
public sealed class ShipperRequestAuthoringPageViewModel : IDisposable
{
    public const string RecoveryScope = "shipper-request-create.v1";
    private readonly IShipperOperationsService _operations;
    private readonly NavigationManager _navigation;
    private readonly IAuthSession? _auth;
    private readonly IWorkflowRecoveryStore? _recovery;
    private bool _initialized;
    private bool _initializing;
    private bool _disposed;
    private long _generation;
    private long _sessionRevision;
    private WorkflowRecoveryRecord? _pending;
    private ShipperRequestItem? _pendingRequest;
    private bool _pendingNotFound;

    public ShipperRequestAuthoringPageViewModel(운송의뢰작성ViewModel state,
        IShipperOperationsService operations, NavigationManager navigation,
        IAuthSession? auth = null, IWorkflowRecoveryStore? recovery = null)
    {
        State = state;
        _operations = operations;
        _navigation = navigation;
        _auth = auth;
        _recovery = recovery;
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
    public bool CanRetryPendingRequest => _pendingNotFound && _pendingRequest is not null && !IsBusy;
    public bool PendingInputExpired => _pending is not null && _pendingRequest is null;
    public string RegistrationBoundaryMessage { get; private set; }
        = "등록 준비를 확인하고 있습니다. 등록만으로 자동 배차·계약·결제가 확정되지는 않습니다.";

    public async Task InitializeAsync()
    {
        if (_disposed || _initialized || _initializing) return;
        _initializing = true;
        var generation = _generation;
        try
        {
            if (_auth is not null) await _auth.RestoreAsync();
            generation = _generation;
            var context = CurrentContext();
            if (_auth is not null && _recovery is null)
                throw new InvalidOperationException("이전 접수를 보호할 저장소를 확인하지 못했습니다.");
            var vehicles = await _operations.GetVehicleTypesAsync();
            if (!IsCurrent(context)) return;
            VehicleTypes = vehicles;
            if (_recovery is not null)
            {
                var pending = await _recovery.LoadAsync(context.Owner, RecoveryScope, CancellationToken.None);
                if (!IsCurrent(context)) return;
                ApplyPending(pending);
            }
            RegistrationEnabled = true;
            _initialized = true;
            RegistrationBoundaryMessage = "등록만으로 자동 배차·계약·결제를 확정하지 않습니다. 응답을 확인하지 못한 접수는 같은 번호로 결과를 먼저 확인합니다.";
            if (_pending is not null) await RecoverPendingCoreAsync(context);
            else { StatusMessage = string.Empty; StatusSeverity = Severity.Info; }
        }
        catch (Exception ex)
        {
            if (_disposed || generation != _generation) return;
            VehicleTypes = [];
            RegistrationEnabled = false;
            StatusSeverity = Severity.Warning;
            StatusMessage = $"등록 준비를 확인하지 못했습니다. 입력을 유지했으니 다시 확인해 주세요. {ex.Message}";
            RegistrationBoundaryMessage = "등록 준비를 다시 확인한 뒤 계속할 수 있습니다. 기존 접수 결과가 있으면 먼저 조회합니다.";
        }
        finally { if (generation == _generation) _initializing = false; }
    }

    public Task SaveAsync()
    {
        StatusSeverity = State.서버등록가능 ? Severity.Success : Severity.Warning;
        StatusMessage = State.서버등록가능
            ? "현재 앱 세션에 초안을 유지했습니다. 등록 전 실행 경계를 다시 확인해 주세요."
            : "현재 앱 세션에 초안을 유지했습니다. 등록 전 필수 입력을 보완해 주세요.";
        return Task.CompletedTask;
    }
    public Task AutoSaveAsync() => Task.CompletedTask;
    public Task ResetAsync()
    {
        if (IsBusy || HasPendingRequest)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "이전에 보낸 접수 결과를 먼저 확인해 주세요. 새 요청으로 바꾸지 않았습니다.";
            return Task.CompletedTask;
        }
        State.Reset();
        StatusSeverity = Severity.Info;
        StatusMessage = "운송 의뢰 입력값을 초기화했습니다.";
        return Task.CompletedTask;
    }
    public async Task<decimal?> EstimateFareAsync(string vehicleType, decimal distanceKm)
        => await _operations.EstimateFareAsync(vehicleType, distanceKm);

    public async Task SubmitAsync()
    {
        if (_disposed || IsBusy) return;
        if (HasPendingRequest) { await RecoverPendingAsync(); return; }
        if (RegistrationEnabled != true || !State.서버등록가능)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = RegistrationEnabled != true ? "등록 준비를 먼저 확인해 주세요."
                : "등록 전 점검 목록의 필수 항목을 보완해 주세요.";
            return;
        }
        var context = CurrentContext();
        var id = Guid.NewGuid();
        _pendingRequest = CreateRequest(ClientRequestId(id));
        _pending = new(context.Owner, RecoveryScope, id,
            JsonSerializer.Serialize(new SubmissionPayload(_pendingRequest, State.ToDraft())), DateTime.UtcNow);
        await SendPendingAsync(context);
    }

    public async Task RecoverPendingAsync()
    {
        if (_disposed || IsBusy || _pending is null) return;
        var context = CurrentContext();
        IsBusy = true;
        try { await RecoverPendingCoreAsync(context); }
        finally { if (IsCurrent(context)) IsBusy = false; }
    }

    public async Task RetryPendingAsync()
    {
        if (_disposed || IsBusy || !CanRetryPendingRequest) return;
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
        if (_pending is null || _pendingRequest is null || !IsCurrent(context)) return;
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
            if (_recovery is not null) await _recovery.SaveAsync(_pending, CancellationToken.None);
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
                    var stored = await _recovery.LoadAsync(context.Owner, RecoveryScope, CancellationToken.None);
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
        if (_pending is null || !IsCurrent(context)) return;
        _pendingNotFound = false;
        try
        {
            var created = await _operations.GetRequestByClientRequestIdAsync(ClientRequestId(_pending.RequestId));
            if (!IsCurrent(context)) return;
            if (created is not null) { await FinishPendingAsync(created, context); return; }
            _pendingNotFound = true;
            StatusSeverity = Severity.Warning;
            StatusMessage = _pendingRequest is null
                ? "이전 접수 결과를 찾지 못했습니다. 보관 기한이 지나 원문은 삭제됐으며 접수 번호는 유지합니다. 새 의뢰로 우회하지 않고 결과를 다시 확인해 주세요."
                : "이전 접수 결과를 아직 찾지 못했습니다. 원문과 번호는 유지했습니다. 같은 요청으로 다시 등록을 선택할 수 있습니다.";
        }
        catch (Exception ex)
        {
            if (!IsCurrent(context)) return;
            StatusSeverity = Severity.Warning;
            StatusMessage = $"이전 접수 결과를 확인하지 못했습니다. 새 등록은 보내지 않았습니다. 결과 확인을 다시 시도해 주세요. {ex.Message}";
        }
    }

    private async Task FinishPendingAsync(ShipperRequestItem created, SubmissionContext context)
    {
        if (string.IsNullOrWhiteSpace(created.의뢰Id)) throw new InvalidDataException("등록된 운송 의뢰 번호를 확인하지 못했습니다.");
        if (_pending is not null && _recovery is not null)
            await _recovery.RemoveAsync(context.Owner, RecoveryScope, _pending.RequestId, CancellationToken.None);
        if (!IsCurrent(context)) return;
        _pending = null;
        _pendingRequest = null;
        _pendingNotFound = false;
        StatusSeverity = Severity.Success;
        StatusMessage = "운송 의뢰 원장 등록을 확인했습니다.";
        var route = ShipperRequestNavigationContext.Parse(_navigation.Uri);
        _navigation.NavigateTo(PageNavigationContext.WithReturnPath(ShipperRoutes.RequestDetailFor(created.의뢰Id), route.ReturnPath));
    }

    private ShipperRequestItem CreateRequest(string clientRequestId)
    {
        var draft = State.ToDraft();
        return new()
        {
            의뢰Id = clientRequestId, 화물종류 = draft.화물종류, 화물적재형태 = draft.화물적재형태,
            의뢰상태 = "접수", 결제상태 = draft.결제예정금액.HasValue ? "결제대기" : "대기", 배차상태 = "매칭중",
            운송방식 = draft.운송방식, 차량종류 = draft.차량종류 ?? "1톤 카고", 결제수단 = draft.결제수단,
            결제예정금액 = draft.결제예정금액, 예상거리Km = draft.예상거리Km, 기준운임 = draft.기준운임,
            기사지급예정운임 = draft.기사지급예정운임, 알선단계 = draft.알선단계, 재알선금지 = draft.재알선금지,
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
    private SubmissionContext CurrentContext()
    {
        if (_auth is not null && (!_auth.IsLoggedIn || string.IsNullOrWhiteSpace(_auth.UserId)))
            throw new InvalidOperationException("로그인 후 접수 결과를 확인해 주세요.");
        return new(_auth?.UserId ?? "local-session", _auth?.SessionRevision ?? 0, _generation);
    }
    private bool IsCurrent(SubmissionContext context) => !_disposed && context.Generation == _generation
        && (_auth is null || _auth.IsLoggedIn && _auth.UserId == context.Owner && _auth.SessionRevision == context.Revision);
    private void OnAuthenticationChanged()
    {
        if (_auth is null || _sessionRevision == _auth.SessionRevision) return;
        _sessionRevision = _auth.SessionRevision;
        ++_generation;
        _initialized = _initializing = IsBusy = false;
        RegistrationEnabled = null;
        VehicleTypes = [];
        _pending = null;
        _pendingRequest = null;
        _pendingNotFound = false;
        State.Reset();
        StatusMessage = "로그인한 계정의 접수 결과를 다시 확인해 주세요.";
        StatusSeverity = Severity.Info;
    }
    public void Dispose()
    {
        _disposed = true;
        ++_generation;
        if (_auth is not null) _auth.Changed -= OnAuthenticationChanged;
        _pending = null;
        _pendingRequest = null;
    }
    private sealed record SubmissionContext(string Owner, long Revision, long Generation);
    private sealed record SubmissionPayload(ShipperRequestItem Request, 운송모델작성Draft Draft);
}
