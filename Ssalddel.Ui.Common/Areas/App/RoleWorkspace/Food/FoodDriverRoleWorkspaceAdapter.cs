using System.Globalization;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.ClientAdapter,
    "음식 기사 현재 조회·운행 시작·수신 의사·수행 명령과 정본 복구를 조립",
    StepKey = "ui.food-driver-adapter", FlowOrder = 210, ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    DependsOnStepKeys = new[] { "api.food-driver", "contract.workspace-port", "contract.recovery-port", "ui.recovery-policy", "ui.workspace-presentation" },
    ContractType = typeof(FoodDeliveryDriverCurrentWorkDto),
    ReadsFrom = SsalddelCodeDataScope.OperationalState | SsalddelCodeDataScope.ClientPresentation,
    WritesTo = SsalddelCodeDataScope.OperationalState | SsalddelCodeDataScope.ClientPresentation,
    Effects = SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.UiStateMutation | SsalddelCodeEffect.StateMutation | SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.ThirdPartyApiCall | SsalddelCodeEffect.MayIncurExternalCost,
    Boundary = "서버 명령 효과를 위임한다. 미확정 ID는 이 객체 수명에만 보존하며 네이티브 암호화 저장을 수행하지 않는다. 자동 조회는 수락이나 시작 POST 자동 재전송이 아니다.")]
public sealed class FoodDriverRoleWorkspaceAdapter(IRoleWorkspaceApi api, IRoleWorkspaceLocationProvider locationProvider,
    TimeProvider? clock = null) : IRoleWorkspaceAdapter, IRoleWorkspaceRecovery, IRoleWorkspaceDispatchReceptionState, IRoleWorkspacePresentationObserver
{
    private FoodDeliveryDriverCurrentWorkDto? workspace;
    private 기사운행상태응답? work;
    private 운영배차수신상태Dto? dispatch;
    private RoleWorkspaceLocation? currentLocation;
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private DateTimeOffset nextAutomaticLocationRead;
    private bool locationRecoveryNeeded;
    private string? automaticLocationNotice;
    private Guid? pendingStartReceptionRequestId;
    private Guid? pendingStartRequestId;
    private string? unconfirmedPreferenceAction;
    private bool resolvedStart;
    private string? rejectedStartNotice;
    private long presentationGeneration;
    private readonly Dictionary<RoleWorkspaceItem, FoodDeliveryDriverOfferDto> presentationOffers = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<(string OfferId, int Round)> acknowledgedPresentations = [];
    private readonly HashSet<(string OfferId, int Round)> presentationAttempts = [];
    public bool StartReceptionPending => pendingStartReceptionRequestId.HasValue;
    public bool StartReceptionReady => FoodDriverWorkRecoveryPolicy.WorkState(work?.Status) == FoodDriverCanonicalWorkState.Driving
        && dispatch?.수신의사Code == 운영배차수신의사Code.On;
    public bool StartStateUnconfirmed => StartReceptionPending;
    public bool PreferenceStateUnconfirmed => unconfirmedPreferenceAction is not null;
    public string? DispatchReceptionNotice => FoodDriverDispatchReceptionPolicy.IsKnown(dispatch)
        && dispatch!.미응답자동중지여부 ? FoodDriverDispatchReceptionPolicy.AutomaticStopNotice : null;
    public string RoleKey => "food-driver";
    public const string StartWork = "food-driver.work-start";
    public const string StopWork = "food-driver.work-stop";
    public const string EnableDispatch = "food-driver.enable-dispatch";
    public const string DisableDispatch = "food-driver.disable-dispatch";
    public const string UpdateLocation = "food-driver.update-location";

    public async Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
    {
        var value = await api.GetAsync<FoodDeliveryDriverCurrentWorkDto>(RoleKey, "api/v1/driver/food-deliveries/workspace/current", cancellationToken);
        var status = await api.GetAsync<기사운행상태응답>(RoleKey, "api/v1/driver/food-deliveries/work/status", cancellationToken);
        var availability = await api.GetAsync<운영배차수신상태Dto>(RoleKey, "api/v1/driver/operational-dispatch/availability", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        workspace = value; work = status; dispatch = availability;
        availability = await ReconcileStartedReceptionAsync(status, availability, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        dispatch = availability;
        var onDuty = string.Equals(status.Status, "운행중", StringComparison.OrdinalIgnoreCase);
        var workStatusKnown = KnownWorkStatus(status.Status);
        var receives = FoodDriverDispatchReceptionPolicy.IsReceiving(availability);
        var receivingKnown = availability.수신의사Code is 운영배차수신의사Code.On or 운영배차수신의사Code.Off;
        await RefreshLocationWithoutPromptAsync(onDuty && (receives || value.ActiveDeliveries.Count > 0), cancellationToken);
        var nowUtc = time.GetUtcNow().UtcDateTime;
        var active = value.ActiveDeliveries.Select(item => FoodDriverWorkspacePresentation.Active(item, nowUtc)).ToArray();
        var items = active.Concat(value.Recommendations.Select(item => FoodDriverWorkspacePresentation.Offer(item, nowUtc))).ToArray();
        var selected = active.FirstOrDefault(item => item.Id == selectedId)?.Id ?? active.FirstOrDefault()?.Id
            ?? items.FirstOrDefault(item => item.Id == selectedId)?.Id ?? items.FirstOrDefault()?.Id;
        var (routes, routeNotice) = await ReadRoutesAsync(selected, value, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (selected is not null && currentLocation is { } location && Fresh(location))
            items = items.Select(item => item.Id == selected ? item with
            {
                Markers = (item.Markers ?? []).Concat([new NeighborhoodMapMarker(item.Id + ":driver", "최근 확인한 내 위치", NeighborhoodMapMarkerKinds.Driver,
                    location.Latitude, location.Longitude, MeasuredAt: location.MeasuredAt, ExpiresAt: location.MeasuredAt.AddSeconds(30))]).ToArray(),
                Routes = routes
            } : item).ToArray();
        var controls = new List<RoleWorkspaceAction>
        {
            new(onDuty ? StopWork : StartWork, onDuty ? "운행 종료" : "운행 시작", IsPrimary: !onDuty,
                Enabled: workStatusKnown && !StartReceptionPending && (!onDuty || active.Length == 0), DisabledReason: !workStatusKnown
                    ? "운행 상태를 자동으로 확인하고 있습니다." : StartReceptionPending
                    ? "운행 시작과 배차 수신 상태를 자동으로 확인하고 있습니다."
                    : onDuty && active.Length > 0 ? "진행 중인 배달을 완료한 뒤 운행을 종료해 주세요." : null, RequiresConfirmation: onDuty),
            new(receives ? DisableDispatch : EnableDispatch, "신규 배차 받기", Enabled: workStatusKnown && receivingKnown && !StartReceptionPending,
                DisabledReason: workStatusKnown && receivingKnown && !StartReceptionPending ? null : "수신 상태를 자동으로 확인하고 있습니다.", RequiresConfirmation: false)
        };
        if (locationRecoveryNeeded) controls.Add(new(UpdateLocation, "위치 확인", RequiresConfirmation: false));
        var notice = StartReceptionPending ? "운행 시작과 신규 배차 수신 상태를 자동으로 확인하고 있습니다."
            : !workStatusKnown ? "운행 상태를 자동으로 확인하고 있습니다."
            : !onDuty ? "운행을 시작하면 배달 요청을 받을 준비를 합니다."
            : !receivingKnown ? "신규 배차 수신 상태를 자동으로 확인하고 있습니다."
            : availability.미응답자동중지여부 ? FoodDriverDispatchReceptionPolicy.AutomaticStopNotice
            : !receives ? "신규 배차 받기를 켜면 배달 요청을 받을 수 있습니다."
            : availability.실효상태Code switch
            {
                운영배차실효상태Code.서버일시정지 => "신규 배차가 일시 중지되었습니다. 운영자에게 문의해 주세요.",
                운영배차실효상태Code.연결확인불가 => "배차 연결 상태를 확인할 수 없습니다. 연결을 자동으로 확인하고 있습니다.",
                운영배차실효상태Code.조건부적합 => "신규 배차 조건을 확인해 주세요.",
                _ => active.Length > 0 ? "현재 배달의 진행 상태와 가능한 행동을 확인해 주세요."
                    : value.Recommendations.Count > 0 ? "새로운 배달 제안을 확인해 주세요."
                    : "새로운 배달 요청을 기다리고 있습니다."
            };
        if (!string.IsNullOrWhiteSpace(value.DispatchAutomationNotice)) notice += " " + value.DispatchAutomationNotice;
        if (!string.IsNullOrWhiteSpace(automaticLocationNotice)) notice += " " + automaticLocationNotice;
        if (!string.IsNullOrWhiteSpace(routeNotice)) notice += " " + routeNotice;
        presentationGeneration++;
        presentationOffers.Clear(); presentationAttempts.Clear();
        foreach (var item in items.Where(item => !item.IsCurrent))
        {
            var offer = value.Recommendations.FirstOrDefault(offer => offer.OfferId == item.Id);
            if (offer is not null) presentationOffers[item] = offer;
        }
        return new(RoleKey, items, notice, selected, controls);
    }

    public async Task AcknowledgePresentedAsync(RoleWorkspaceItem displayedItem, CancellationToken cancellationToken)
    {
        if (!FoodDriverDispatchReceptionPolicy.IsReceiving(dispatch)
            || !presentationOffers.TryGetValue(displayedItem, out var offer)
            || offer.RecommendationRound <= 0 || string.IsNullOrWhiteSpace(offer.OfferId)
            || offer.ExpiresAtUtc is not { } expiry || expiry <= time.GetUtcNow().UtcDateTime) return;
        var key = (offer.OfferId, offer.RecommendationRound);
        if (acknowledgedPresentations.Contains(key) || !presentationAttempts.Add(key)) return;
        var generation = presentationGeneration;
        try
        {
            await api.AcknowledgeOfferPresentationAsync(offer.OfferId, offer.RecommendationRound, cancellationToken);
            if (generation == presentationGeneration && !cancellationToken.IsCancellationRequested)
                acknowledgedPresentations.Add(key);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception) { /* 표시 수신 확인 실패는 제안 표시를 막지 않습니다. 다음 정본 조회에서 같은 차수를 확인합니다. */ }
    }

    // 화면의 기존 15초 전경 재조회에 결속합니다. 배차 OFF여도 진행 배달의 위치는 유지합니다.
    private async Task RefreshLocationWithoutPromptAsync(bool required, CancellationToken token)
    {
        if (!required)
        {
            currentLocation = null; nextAutomaticLocationRead = default;
            locationRecoveryNeeded = false; automaticLocationNotice = null;
            return;
        }
        if (time.GetUtcNow() < nextAutomaticLocationRead) return;
        nextAutomaticLocationRead = time.GetUtcNow().AddSeconds(15);
        try
        {
            var observed = await locationProvider.GetCurrentWithoutPermissionPromptAsync(token);
            token.ThrowIfCancellationRequested();
            var location = ValidateLocation(observed);
            await PublishLocationAsync(token, location);
            locationRecoveryNeeded = false; automaticLocationNotice = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (RoleWorkspaceAccessException exception) when (exception.StatusCode is 401 or 403) { throw; }
        catch (Exception)
        {
            token.ThrowIfCancellationRequested();
            currentLocation = null; locationRecoveryNeeded = true;
            automaticLocationNotice = "현재 위치를 확인하지 못했습니다. 위치 확인에서 권한과 GPS를 확인해 주세요.";
        }
    }

    private async Task<(IReadOnlyList<NeighborhoodMapRoute> Routes, string? Notice)> ReadRoutesAsync(string? selected,
        FoodDeliveryDriverCurrentWorkDto value, CancellationToken token)
    {
        if (selected is null) return ([], null);
        if (currentLocation is not { } location || !Fresh(location)) return ([], locationRecoveryNeeded ? null : "현재 위치를 확인하고 있습니다.");
        var active = value.ActiveDeliveries.FirstOrDefault(item => item.OfferId == selected);
        var offer = value.Recommendations.FirstOrDefault(item => item.OfferId == selected);
        if (active is null && offer is null) return ([], null);
        var pickup = active?.Pickup ?? offer!.Pickup;
        var dropoff = active?.Dropoff ?? offer!.Dropoff;
        var isDelivery = active is not null && FoodDriverStagePresentation.Resolve(active, time.GetUtcNow().UtcDateTime) == FoodDriverStage.Delivery;
        var legs = new List<(decimal Latitude, decimal Longitude, FoodDeliveryDriverStopDto Stop, string Color, string Key)>();
        if (isDelivery)
            legs.Add(((decimal)location.Latitude, (decimal)location.Longitude, dropoff, "#2563eb", "delivery"));
        else
        {
            legs.Add(((decimal)location.Latitude, (decimal)location.Longitude, pickup, "#f97316", "pickup"));
            if (FoodWorkspacePresentation.Coordinates(pickup.Latitude, pickup.Longitude))
                legs.Add((pickup.Latitude!.Value, pickup.Longitude!.Value, dropoff, "#6b7280", "delivery"));
        }
        var routes = new List<NeighborhoodMapRoute>();
        var incomplete = false;
        foreach (var leg in legs)
        {
            if (!FoodWorkspacePresentation.Coordinates(leg.Stop.Latitude, leg.Stop.Longitude)) { incomplete = true; continue; }
            try
            {
                var route = await api.PostAsync<FoodDeliveryDriverRouteResponseDto>(RoleKey, "api/v1/driver/food-deliveries/route", new FoodDeliveryDriverRouteRequestDto
                {
                    StartLatitude = leg.Latitude, StartLongitude = leg.Longitude,
                    Stops = [new() { Label = leg.Stop.Label, Latitude = leg.Stop.Latitude!.Value, Longitude = leg.Stop.Longitude!.Value }]
                }, token);
                token.ThrowIfCancellationRequested();
                if (route is null || route.Source != "NaverDirections5" || route.IsEstimated || route.Points.Count < 2
                    || route.Points.Any(point => !FoodWorkspacePresentation.Coordinates(point.Latitude, point.Longitude)))
                { incomplete = true; continue; }
                routes.Add(new(selected + ":" + leg.Key, route.Points.Select(point => new NeighborhoodMapPoint((double)point.Latitude, (double)point.Longitude)).ToArray(),
                    leg.Color, SourceCode: route.Source, ExpiresAt: location.MeasuredAt.AddSeconds(30)));
            }
            catch (OperationCanceledException) { throw; }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode is 401 or 403) { throw; }
            catch { incomplete = true; }
        }
        return (routes, incomplete ? "일부 실제 이동 경로를 확인하지 못했습니다. 목적지 핀과 업무 내용을 확인해 주세요." : null);
    }

    public async Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
    {
        if (workspace is null) throw new InvalidOperationException("기사 업무를 확인하고 있습니다. 잠시 후 다시 시도해 주세요.");
        if (actionKey is StartWork or StopWork or EnableDispatch or DisableDispatch or UpdateLocation)
        {
            await PerformWorkAsync(actionKey, requestId, cancellationToken); return;
        }
        var active = workspace.ActiveDeliveries.FirstOrDefault(value => value.OfferId == itemId);
        var offer = workspace.Recommendations.FirstOrDefault(value => value.OfferId == itemId);
        var actions = active?.AvailableActions ?? offer?.AvailableActions ?? [];
        var allowed = FoodWorkspacePresentation.Require(actions, actionKey, automaticRefresh: true, nowUtc: time.GetUtcNow().UtcDateTime);
        if (offer?.ExpiresAtUtc is { } expiry && expiry <= time.GetUtcNow().UtcDateTime)
            throw new InvalidOperationException("배달 제안이 만료되었습니다. 최신 제안을 자동으로 확인합니다.");
        if (actionKey == 음식배달가능행동Ids.기사배달중단)
            throw new InvalidOperationException("중단 사유를 입력해 주세요.");
        var endpoint = actionKey switch
        {
            음식배달가능행동Ids.기사제안수락 when offer is not null => "accept",
            음식배달가능행동Ids.기사제안거절 when offer is not null => "reject",
            음식배달가능행동Ids.기사가게도착 when active is not null => "restaurant-arrival",
            음식배달가능행동Ids.기사픽업확인 when active is not null => "pickup-complete",
            음식배달가능행동Ids.기사전달완료 when active is not null => "delivery-complete",
            _ => throw new InvalidOperationException("최신 배달 단계와 가능 행동을 확인해 주세요.")
        };
        if (endpoint == "restaurant-arrival" && (active is null || string.IsNullOrWhiteSpace(active.DeliveryAttemptId)
            || active.RestaurantArrivedAtUtc.HasValue || FoodDriverStagePresentation.Resolve(active, time.GetUtcNow().UtcDateTime) == FoodDriverStage.Delivery))
            throw new InvalidOperationException("현재 배달 시도와 음식점 도착 상태를 확인해 주세요.");
        if (actionKey != 음식배달가능행동Ids.기사제안거절) await PublishLocationAsync(cancellationToken);
        object body = active is not null && endpoint == "restaurant-arrival"
            ? new 음식배달가게도착요청 { 클라이언트요청Id = requestId, 예상시도Revision = allowed.ExpectedRevision ?? active.AttemptRevision }
            : new { };
        await api.PostAsync<FoodDeliveryDriverActionResponse>(RoleKey,
            $"api/v1/driver/food-deliveries/offers/{Uri.EscapeDataString(itemId)}/{endpoint}", body, cancellationToken);
    }

    private async Task PerformWorkAsync(string action, Guid requestId, CancellationToken token)
    {
        if (work is null || dispatch is null) throw new InvalidOperationException("운행·배차 상태를 확인해 주세요.");
        if (!KnownWorkStatus(work.Status) && action is StartWork or StopWork or EnableDispatch or DisableDispatch)
            throw new InvalidOperationException("운행 상태를 자동으로 확인하고 있습니다.");
        if (StartReceptionPending && action is StartWork or StopWork or EnableDispatch or DisableDispatch)
            throw new InvalidOperationException("운행 시작과 배차 수신 상태를 자동으로 확인하고 있습니다.");
        if (action is EnableDispatch or DisableDispatch)
        {
            await api.PutAsync<운영배차수신상태Dto>(RoleKey, "api/v1/driver/operational-dispatch/availability/intent",
                new 운영배차수신의사변경요청 { 클라이언트요청Id = requestId,
                    수신의사Code = action == EnableDispatch ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
                    미응답자동중지해제 = action == EnableDispatch }, token);
        }
        else if (action == StartWork)
        {
            var location = await RequireLocationAsync(token);
            // 시작과 수신 ON은 각각의 요청 ID를 소유합니다. 응답 유실은 정본 조회로만 확인합니다.
            pendingStartRequestId = requestId;
            pendingStartReceptionRequestId = Guid.NewGuid();
            기사운행시작응답? started;
            try
            {
                started = await api.PostAsync<기사운행시작응답>(RoleKey, "api/v1/driver/food-deliveries/work/start", new 기사운행시작요청
                {
                    ClientRequestId = pendingStartRequestId, 시작모드 = "바로시작", 시작시각 = time.GetUtcNow().UtcDateTime,
                    시작위치 = string.Create(CultureInfo.InvariantCulture, $"{location.Latitude},{location.Longitude}"),
                    커뮤니티운행공개 = false, 커뮤니티구단위위치공개동의 = false
                }, token);
            }
            catch (RoleWorkspaceAccessException ex) when (FoodDriverWorkRecoveryPolicy.StartWasRejected(ex.StatusCode))
            {
                pendingStartRequestId = null; pendingStartReceptionRequestId = null;
                throw;
            }
            token.ThrowIfCancellationRequested();
            if (started is null || started.Status != "운행중") throw new InvalidOperationException("운행 시작 결과를 확인하지 못했습니다. 현재 운행 상태를 자동으로 확인합니다.");
            var canonical = await api.GetAsync<기사운행상태응답>(RoleKey, "api/v1/driver/food-deliveries/work/status", token);
            token.ThrowIfCancellationRequested();
            if (!KnownWorkStatus(canonical.Status)) throw new InvalidOperationException("운행 시작 결과를 자동으로 확인하고 있습니다.");
            work = canonical;
            if (FoodDriverWorkRecoveryPolicy.WorkState(canonical.Status) != FoodDriverCanonicalWorkState.Driving
                || !FoodDriverWorkRecoveryPolicy.StartOwnershipMatches(pendingStartRequestId,
                    canonical.WorkStartClientRequestId, canonical.운송실행유형))
            {
                RejectPendingStart();
                throw new InvalidOperationException(FoodDriverWorkRecoveryPolicy.StartOwnershipNotice);
            }
            await PublishLocationAsync(token, location);
            await EnableStartedReceptionAsync(pendingStartReceptionRequestId.Value, token);
        }
        else if (action == StopWork)
        {
            if (workspace?.ActiveDeliveries.Count > 0) throw new InvalidOperationException("진행 중인 배달을 완료한 뒤 운행을 종료해 주세요.");
            await api.PostAsync<object>(RoleKey, "api/v1/driver/food-deliveries/work/stop", new { }, token);
        }
        else await PublishLocationAsync(token);
    }

    private async Task<운영배차수신상태Dto> ReconcileStartedReceptionAsync(기사운행상태응답 status,
        운영배차수신상태Dto availability, CancellationToken token)
    {
        var decision = FoodDriverWorkRecoveryPolicy.ResolveStart(StartReceptionPending, status.Status, availability.수신의사Code);
        if (pendingStartReceptionRequestId is not { } requestId) return availability;
        if (decision is FoodDriverStartRecoveryDecision.AwaitCanonical or FoodDriverStartRecoveryDecision.ClearStopped)
        {
            if (decision == FoodDriverStartRecoveryDecision.ClearStopped) CompletePendingStart();
            return availability;
        }
        if (!FoodDriverWorkRecoveryPolicy.StartOwnershipMatches(pendingStartRequestId,
            status.WorkStartClientRequestId, status.운송실행유형))
        {
            RejectPendingStart();
            return availability;
        }
        if (decision == FoodDriverStartRecoveryDecision.EnableReception)
        {
            await EnableStartedReceptionAsync(requestId, token);
            availability = await api.GetAsync<운영배차수신상태Dto>(RoleKey, "api/v1/driver/operational-dispatch/availability", token);
            token.ThrowIfCancellationRequested();
        }
        if (availability.수신의사Code == 운영배차수신의사Code.On) CompletePendingStart();
        return availability;
    }

    private async Task EnableStartedReceptionAsync(Guid requestId, CancellationToken token)
    {
        await api.PutAsync<운영배차수신상태Dto>(RoleKey, "api/v1/driver/operational-dispatch/availability/intent",
            new 운영배차수신의사변경요청 { 클라이언트요청Id = requestId, 수신의사Code = 운영배차수신의사Code.On }, token);
        token.ThrowIfCancellationRequested();
    }

    private static bool KnownWorkStatus(string? status) => FoodDriverWorkRecoveryPolicy.WorkState(status) != FoodDriverCanonicalWorkState.Unknown;

    private void CompletePendingStart()
    {
        pendingStartReceptionRequestId = null; pendingStartRequestId = null; resolvedStart = true;
    }
    private void RejectPendingStart()
    {
        CompletePendingStart(); rejectedStartNotice = FoodDriverWorkRecoveryPolicy.StartOwnershipNotice;
    }

    public bool BlocksAction(string actionKey) => StartReceptionPending && actionKey is StartWork or StopWork or EnableDispatch or DisableDispatch
        || PreferenceStateUnconfirmed && actionKey is EnableDispatch or DisableDispatch;

    public string? RecordUnconfirmedAction(string actionKey)
    {
        if (actionKey is EnableDispatch or DisableDispatch)
        {
            unconfirmedPreferenceAction = actionKey;
            return "신규 배차 수신 변경 결과를 확인하지 못했습니다. 현재 수신 상태를 자동으로 확인하고 있습니다.";
        }
        return actionKey == StartWork && StartReceptionPending ? "운행 시작 결과와 신규 배차 수신 상태를 자동으로 확인하고 있습니다." : null;
    }

    public RoleWorkspaceRecoveryResult ResolveCanonicalActions()
    {
        var completed = new List<string>(); var confirmed = resolvedStart;
        var actionKey = resolvedStart ? StartWork : unconfirmedPreferenceAction;
        if (resolvedStart) { completed.Add(StartWork); resolvedStart = false; }
        if (unconfirmedPreferenceAction is { } action && dispatch?.수신의사Code is 운영배차수신의사Code.On or 운영배차수신의사Code.Off)
        {
            var desired = action == EnableDispatch ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off;
            if (FoodDriverDispatchReceptionPolicy.Confirms(new 운영배차수신의사변경요청
                { 수신의사Code = desired, 미응답자동중지해제 = action == EnableDispatch }, dispatch)) completed.Add(action);
            unconfirmedPreferenceAction = null; confirmed = true;
        }
        var notice = rejectedStartNotice; rejectedStartNotice = null;
        return new(completed, confirmed, notice, actionKey);
    }

    private async Task<RoleWorkspaceLocation> RequireLocationAsync(CancellationToken token)
    {
        var location = await locationProvider.GetCurrentAsync(token);
        token.ThrowIfCancellationRequested();
        return ValidateLocation(location);
    }

    private RoleWorkspaceLocation ValidateLocation(RoleWorkspaceLocation? location)
    {
        if (location is null || !Fresh(location) || !double.IsFinite(location.Latitude) || !double.IsFinite(location.Longitude)
            || location.Latitude is < -90 or > 90 || location.Longitude is < -180 or > 180
            || location.Latitude == 0 && location.Longitude == 0
            || location.AccuracyMeters is { } accuracy && (!double.IsFinite(accuracy) || accuracy < 0 || accuracy > (double)decimal.MaxValue))
            throw new InvalidOperationException("현재 위치를 확인할 수 없습니다. 위치 권한과 GPS를 확인해 주세요.");
        return location;
    }

    private bool Fresh(RoleWorkspaceLocation location)
    {
        var now = time.GetUtcNow();
        return now - location.MeasuredAt <= TimeSpan.FromSeconds(30) && location.MeasuredAt <= now.AddSeconds(5);
    }

    private async Task PublishLocationAsync(CancellationToken token, RoleWorkspaceLocation? captured = null)
    {
        var location = captured ?? await RequireLocationAsync(token);
        var response = await api.PostAsync<기사위치갱신응답>(RoleKey, "api/v1/driver/food-deliveries/work/location", new 기사위치갱신요청
        {
            AppKey = 기사앱식별자.FoodDeliveryDriverApp, 위도 = (decimal)location.Latitude, 경도 = (decimal)location.Longitude,
            정확도_m = location.AccuracyMeters is { } accuracy ? (decimal)accuracy : null,
            상차접근허용반경Km = 3m, 운행상태 = work?.Status, 기록시각 = location.MeasuredAt.UtcDateTime
        }, token);
        token.ThrowIfCancellationRequested();
        if (response is null || !FoodWorkspacePresentation.Coordinates(response.현재위도, response.현재경도)
            || response.현재위도 != (decimal)location.Latitude || response.현재경도 != (decimal)location.Longitude)
            throw new InvalidOperationException("현재 위치 전송 결과를 확인하지 못했습니다. 위치 확인에서 권한과 GPS를 확인해 주세요.");
        currentLocation = location;
        nextAutomaticLocationRead = time.GetUtcNow().AddSeconds(15);
        locationRecoveryNeeded = false; automaticLocationNotice = null;
    }

    public void Clear()
    {
        ClearTransientSnapshot();
        pendingStartReceptionRequestId = null;
        pendingStartRequestId = null; unconfirmedPreferenceAction = null; resolvedStart = false; rejectedStartNotice = null;
        acknowledgedPresentations.Clear();
    }

    // 같은 계정의 일시 숨김/조회 실패에서는 진행 중인 시작→수신 ON 요청만 보존합니다.
    public void ClearTransientSnapshot()
    {
        presentationGeneration++; presentationOffers.Clear(); presentationAttempts.Clear();
        workspace = null; work = null; dispatch = null; currentLocation = null;
        nextAutomaticLocationRead = default; locationRecoveryNeeded = false; automaticLocationNotice = null;
    }
}
