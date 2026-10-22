using System.Globalization;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>기존 화물 근무·위치·수신 계약을 연결합니다. 미확정 요청은 화면 수명에만 보존하며 자동 재전송하지 않습니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.ClientAdapter,
    "통합 화물 기사 운행·위치·신규 수신 상태와 미확정 요청의 정본 복구를 연결",
    StepKey = "ui.cargo-driver-work-session", FlowOrder = 211, ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    DependsOnStepKeys = ["contract.workspace-port", "contract.recovery-port", "contract.work-start-request", "contract.operational-dispatch-availability"],
    ReadsFrom = SsalddelCodeDataScope.OperationalState | SsalddelCodeDataScope.ClientPresentation,
    WritesTo = SsalddelCodeDataScope.OperationalState | SsalddelCodeDataScope.ClientPresentation,
    Effects = SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.UiStateMutation | SsalddelCodeEffect.StateMutation | SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    Boundary = "기존 화물 API의 권한·실행 경계를 소비한다. 수신 ON은 명시적 토글에서만 요청하며 미확정 시작 POST를 자동 반복하지 않는다. 복구 요청은 객체 수명이며 기기 영속 저장이나 백그라운드 위치 송신을 보장하지 않는다.")]
public sealed class CargoDriverWorkSession(CargoDriverWorkspaceApiClient client,
    IRoleWorkspaceLocationProvider? locationProvider, TimeProvider? clock = null)
{
    public const string StartWork = "cargo-driver.work-start";
    public const string StopWork = "cargo-driver.work-stop";
    public const string EnableDispatch = "cargo-driver.enable-dispatch";
    public const string DisableDispatch = "cargo-driver.disable-dispatch";
    public const string CheckLocation = "cargo-driver.check-location";
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private 기사운행상태응답? work;
    private 운영배차수신상태Dto? reception;
    private Guid? pendingStart;
    private bool startRejected;
    private string? pendingPreference;
    private string? resolvedAction;
    private string? recoveryNotice;
    private DateTimeOffset nextPositionRead;
    private string? locationNotice;
    public bool StartStateUnconfirmed => pendingStart.HasValue;
    public bool PreferenceStateUnconfirmed => pendingPreference is not null;
    public string? DispatchReceptionNotice => reception?.미응답자동중지여부 == true
        ? FoodDriverDispatchReceptionPolicy.AutomaticStopNotice : null;
    private bool Driving => work?.Status == "운행중" && work.운송실행유형 == 운송실행유형코드.화물운송 && work.ShiftId is > 0;
    private bool Stopped => work?.Status is "대기" or "운행종료";
    private bool Known => Driving || Stopped;
    private bool Receiving => FoodDriverDispatchReceptionPolicy.IsReceiving(reception);

    public string Notice => recoveryNotice ?? (StartStateUnconfirmed ? "운행 시작 결과를 확인하고 있습니다. 새 시작 요청은 보내지 않습니다."
        : !Known ? "현재 운행의 업무 종류와 상태를 확인해 주세요. 다른 업무의 운행은 변경하지 않습니다."
        : Stopped ? "운행을 시작한 뒤 신규 배차 받기를 켜 주세요."
        : reception?.미응답자동중지여부 == true ? FoodDriverDispatchReceptionPolicy.AutomaticStopNotice
        : !Receiving ? "운행 중입니다. 신규 배차 받기를 켜면 운송 요청을 받을 수 있습니다."
        : reception?.실효상태Code switch
        {
            운영배차실효상태Code.서버일시정지 => "신규 배차가 일시 중지되었습니다. 운영 담당자에게 문의해 주세요.",
            운영배차실효상태Code.연결확인불가 => "배차 연결 상태를 확인하지 못했습니다. 현재 상태를 다시 확인해 주세요.",
            운영배차실효상태Code.조건부적합 => "받을 업무와 운행 조건을 확인해 주세요.",
            _ => "새 운송 요청을 기다리고 있습니다."
        }) + (locationNotice is null ? "" : " " + locationNotice);

    public IReadOnlyList<RoleWorkspaceAction> Actions(bool hasActive)
    {
        var blocked = StartStateUnconfirmed || PreferenceStateUnconfirmed;
        var reason = !Known ? "현재 운행 상태를 확인해 주세요." : blocked ? "이전 요청 결과를 확인하고 있습니다."
            : hasActive && Driving ? "진행 중인 운송을 완료한 뒤 운행을 종료해 주세요." : null;
        List<RoleWorkspaceAction> actions =
        [
            new(Driving ? StopWork : StartWork, Driving ? "운행 종료" : "운행 시작",
                IsPrimary: !Driving, Enabled: Known && !blocked && (!Driving || !hasActive),
                DisabledReason: reason, RequiresConfirmation: Driving),
            new(Receiving ? DisableDispatch : EnableDispatch, "신규 배차 받기",
                Enabled: Driving && !blocked && FoodDriverDispatchReceptionPolicy.IsKnown(reception),
                DisabledReason: !Driving ? "운행을 먼저 시작해 주세요." : blocked ? "수신 변경 결과를 확인하고 있습니다." : null,
                RequiresConfirmation: false)
        ];
        if (locationNotice is not null && Driving)
            actions.Add(new(CheckLocation, "위치 다시 확인", RequiresConfirmation: false));
        return actions;
    }

    public bool IsWorkAction(string action) => action is StartWork or StopWork or EnableDispatch or DisableDispatch or CheckLocation;

    public async Task LoadAsync(CancellationToken ct)
    {
        var status = await client.WorkStatusAsync(ct);
        var available = await client.ReceptionAsync(ct);
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(status.DriverId) || status.DriverId != available.주체Id
            || !FoodDriverDispatchReceptionPolicy.IsKnown(available))
            throw new InvalidOperationException("본인의 운행·신규 배차 수신 상태를 확인하지 못했습니다.");
        work = status; reception = available;
        Reconcile();
        if (Driving && time.GetUtcNow() >= nextPositionRead)
        {
            nextPositionRead = time.GetUtcNow().AddSeconds(15);
            try
            {
                var location = await CurrentLocationAsync(false, ct);
                await PublishLocationAsync(location, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode is 401 or 403) { throw; }
            catch { locationNotice = "현재 위치를 갱신하지 못했습니다. 위치 권한과 GPS를 확인해 주세요."; }
        }
        if (!Driving) { nextPositionRead = default; locationNotice = null; }
    }

    public async Task PerformAsync(string action, Guid requestId, bool hasActive, CancellationToken ct)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("업무 확인 식별자가 없습니다.", nameof(requestId));
        if (!IsWorkAction(action)) throw new InvalidOperationException("지원하지 않는 운행 행동입니다.");
        if (BlocksAction(action)) throw new InvalidOperationException("이전 요청의 현재 상태를 먼저 확인해 주세요.");
        await LoadAsync(ct);
        if (!Known) throw new InvalidOperationException("현재 화물 운행 상태를 확인해 주세요.");
        if (action == StartWork)
        {
            if (Driving) return; // 이미 진행 중인 근무를 새로 만들지 않습니다.
            var position = await CurrentLocationAsync(true, ct);
            pendingStart = requestId;
            startRejected = false;
            try
            {
                await client.StartWorkAsync(new 기사운행시작요청
                {
                    ClientRequestId = requestId, 시작모드 = "바로시작", 시작시각 = time.GetUtcNow().UtcDateTime,
                    시작위치 = string.Create(CultureInfo.InvariantCulture, $"{position.Latitude},{position.Longitude}"),
                    커뮤니티운행공개 = false, 커뮤니티구단위위치공개동의 = false
                }, ct);
            }
            catch (RoleWorkspaceAccessException ex) when (ex.RejectedWorkStartRequestId == pendingStart
                && ex.StatusCode is >= 400 and < 500 and not 408 and not 429)
            { startRejected = true; throw; }
            await LoadAsync(ct);
            if (pendingStart is not null) throw new InvalidOperationException("운행 시작 결과를 확인하고 있습니다.");
        }
        else if (action == StopWork)
        {
            if (!Driving) return;
            if (hasActive) throw new InvalidOperationException("진행 중인 운송을 완료한 뒤 운행을 종료해 주세요.");
            pendingPreference = StopWork;
            try { await client.StopWorkAsync(ct); }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode is 400 or 404 or 409 or 422)
            { pendingPreference = null; throw; }
            await LoadAsync(ct);
            if (pendingPreference is not null) throw new InvalidOperationException("운행 종료 결과를 확인하고 있습니다.");
        }
        else if (action == CheckLocation)
        {
            if (!Driving) throw new InvalidOperationException("운행을 먼저 시작해 주세요.");
            await PublishLocationAsync(await CurrentLocationAsync(true, ct), ct);
        }
        else
        {
            if (!Driving) throw new InvalidOperationException("운행을 먼저 시작해 주세요.");
            if (action == EnableDispatch)
                await PublishLocationAsync(await CurrentLocationAsync(true, ct), ct);
            pendingPreference = action;
            try
            {
                await client.ChangeReceptionAsync(new 운영배차수신의사변경요청
                {
                    클라이언트요청Id = requestId,
                    수신의사Code = action == EnableDispatch ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
                    미응답자동중지해제 = action == EnableDispatch
                }, ct);
            }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode is 400 or 404 or 409 or 422)
            { pendingPreference = null; throw; }
            await LoadAsync(ct);
            if (pendingPreference is not null) throw new InvalidOperationException("신규 수신 변경 결과를 확인하고 있습니다.");
        }
    }

    private async Task<RoleWorkspaceLocation> CurrentLocationAsync(bool prompt, CancellationToken ct)
    {
        var position = locationProvider is null ? null : prompt
            ? await locationProvider.GetCurrentAsync(ct) : await locationProvider.GetCurrentWithoutPermissionPromptAsync(ct);
        ct.ThrowIfCancellationRequested();
        if (position is null || !double.IsFinite(position.Latitude) || !double.IsFinite(position.Longitude)
            || position.Latitude is < -90 or > 90 || position.Longitude is < -180 or > 180
            || position.Latitude == 0 && position.Longitude == 0
            || position.MeasuredAt > time.GetUtcNow().AddSeconds(5)
            || time.GetUtcNow() - position.MeasuredAt > TimeSpan.FromSeconds(30)
            || position.AccuracyMeters is { } accuracy && (!double.IsFinite(accuracy) || accuracy < 0 || accuracy > (double)decimal.MaxValue))
        {
            locationNotice = "현재 위치를 확인할 수 없습니다. 위치 권한과 GPS를 확인해 주세요.";
            throw new InvalidOperationException(locationNotice);
        }
        return position;
    }
    private async Task PublishLocationAsync(RoleWorkspaceLocation position, CancellationToken ct)
    {
        var response = await client.LocationAsync(new 기사위치갱신요청
        {
            AppKey = 기사앱식별자.CargoYongdalDriverApp, 위도 = (decimal)position.Latitude, 경도 = (decimal)position.Longitude,
            정확도_m = position.AccuracyMeters is { } accuracy ? (decimal)accuracy : null,
            운행상태 = work?.Status, 기록시각 = position.MeasuredAt.UtcDateTime
        }, ct);
        ct.ThrowIfCancellationRequested();
        if (response is null || response.DriverId != work?.DriverId || response.현재위도 != (decimal)position.Latitude || response.현재경도 != (decimal)position.Longitude)
            throw new InvalidOperationException("현재 위치의 저장 결과를 확인하지 못했습니다.");
        nextPositionRead = time.GetUtcNow().AddSeconds(15); locationNotice = null;
    }
    private void Reconcile()
    {
        if (pendingStart is { } id && Driving)
        {
            recoveryNotice = work!.WorkStartClientRequestId == id ? null
                : "현재 다른 운행을 확인했습니다. 새 운행이나 신규 배차 수신은 자동으로 변경하지 않았습니다.";
            pendingStart = null; startRejected = false; resolvedAction = StartWork;
        }
        else if (pendingStart is not null && startRejected && Stopped)
        {
            pendingStart = null; startRejected = false; resolvedAction = StartWork;
            recoveryNotice = "운행 시작이 거절됐고 대기 상태를 확인했습니다. 입력과 권한을 확인한 뒤 다시 시작해 주세요.";
        }
        if (pendingPreference is { } action)
        {
            if (action == StopWork)
            {
                if (Stopped) { pendingPreference = null; resolvedAction = StopWork; }
                return;
            }
            var desired = action == EnableDispatch ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off;
            if (reception?.수신의사Code == desired && (action != EnableDispatch || !reception.미응답자동중지여부))
            { pendingPreference = null; resolvedAction = action; }
        }
    }
    public bool BlocksAction(string action) => IsWorkAction(action) && (StartStateUnconfirmed || PreferenceStateUnconfirmed);
    public string? RecordUnconfirmedAction(string action) => action == StartWork && StartStateUnconfirmed
        ? "운행 시작 결과를 확인하지 못했습니다. 현재 상태를 다시 조회해 주세요. 새 시작은 보내지 않습니다."
        : pendingPreference == StopWork ? "운행 종료 결과를 다시 조회해 주세요. 종료 요청을 반복하지 않습니다."
        : PreferenceStateUnconfirmed ? "신규 배차 수신 변경 결과를 다시 확인해 주세요." : locationNotice;
    public RoleWorkspaceRecoveryResult ResolveCanonicalActions()
    {
        var action = resolvedAction; resolvedAction = null;
        return new(action is null ? [] : [action], action is not null, recoveryNotice, action);
    }
    public void ClearTransientSnapshot() { work = null; reception = null; nextPositionRead = default; locationNotice = null; }
    public void Clear()
    {
        ClearTransientSnapshot(); pendingStart = null; startRejected = false; pendingPreference = null; resolvedAction = recoveryNotice = null;
    }
}
