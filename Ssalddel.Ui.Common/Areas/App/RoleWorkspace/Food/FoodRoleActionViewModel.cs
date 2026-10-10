using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>입력·확인·동일 요청 재시도를 한 역할 계정 수명에 결속합니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.ViewModel,
    "음식 역할의 별도 행동 입력·확인·동일 요청 복구를 계정 수명에 결속",
    StepKey = "ui.food-action-view-model", FlowOrder = 215, ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    DependsOnStepKeys = new[] { "api.food-driver", "ui.food-presentation" },
    ReadsFrom = SsalddelCodeDataScope.OperationalState | SsalddelCodeDataScope.ClientPresentation,
    WritesTo = SsalddelCodeDataScope.OperationalState | SsalddelCodeDataScope.ClientPresentation,
    Effects = SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.UiStateMutation | SsalddelCodeEffect.StateMutation | SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.ThirdPartyApiCall | SsalddelCodeEffect.MayIncurExternalCost,
    Boundary = "기사 중단 등 확인 입력의 클라이언트 조율이며 서버 권한·상태 원장을 대체하지 않는다. 다른 음식 역할과 공유한다.")]
public sealed class FoodRoleActionViewModel : IDisposable
{
    private readonly IRoleWorkspaceApi api;
    private readonly IRoleWorkspaceAccess access;
    private CancellationTokenSource lifetime = new();
    private RoleWorkspaceIdentity? identity;
    private long generation;
    private bool disposed;
    private object? detail;
    private FoodRolePreparedRequest? pending;
    private 업무가능행동Dto? allowed;
    private bool submitted;
    private bool commandAcknowledged;
    private bool canonicalReadSucceeded;
    private FoodRoleCompletionExpectation? completion;
    public FoodRoleActionViewModel(IRoleWorkspaceApi api, IRoleWorkspaceAccess access)
    {
        this.api = api; this.access = access; access.Changed += AuthenticationChanged;
    }

    public event Action? Changed;
    public string RoleKey { get; private set; } = string.Empty;
    public string ItemId { get; private set; } = string.Empty;
    public string ActionKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Message { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool IsConfirming => pending is not null;
    public bool IsAwaitingResult => pending is not null && submitted;
    public bool CanRetrySameRequest => IsAwaitingResult && !commandAcknowledged && canonicalReadSucceeded && !IsBusy;
    public bool IsCompleted { get; private set; }
    public bool RequiresLogin { get; private set; }
    public bool Confirmed { get; set; }
    public Guid? RequestId => pending?.RequestId;
    public int? PreparationMinutes { get; set; }
    public bool ImmediatePickup { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string RestaurantAddress { get; set; } = string.Empty;
    public string RestaurantDetailAddress { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string AttemptId { get; set; } = string.Empty;
    public string DecisionCode { get; set; } = string.Empty;
    public bool AbuseConfirmed { get; set; }
    public IReadOnlyList<음식배달시도운영응답> InterruptedAttempts
        => detail is 음식주문운영추적응답 trace ? trace.배달시도목록.Where(value => value.상태Code == 음식배달시도상태Code.중단).ToArray() : [];

    public async Task InitializeAsync(string roleKey, string itemId, string actionKey, string? attemptId = null)
    {
        if (disposed) return;
        Invalidate(); identity = null; RoleKey = roleKey; ItemId = itemId.Trim(); ActionKey = actionKey;
        var owner = generation;
        Title = actionKey switch
        {
            음식배달가능행동Ids.주문취소 => "주문 취소",
            음식배달가능행동Ids.음식점주문수락 => "음식점 주문 수락",
            음식배달가능행동Ids.음식점조리시간변경 => "조리 시간 변경",
            음식배달가능행동Ids.음식점주문거절 => "주문 거절",
            음식배달가능행동Ids.기사배달중단 => "배달 중단",
            OperatorRoleWorkspaceAdapter.ReviewInterruption => "배달 중단 검토",
            _ => "업무 요청"
        };
        try
        {
            await access.EnsureInitializedAsync(RoleKey, lifetime.Token);
            if (disposed || owner != generation) return;
            identity = access.GetIdentity(RoleKey);
            RequiresLogin = !identity.IsAuthenticated;
            if (RequiresLogin) { Message = "이 역할 계정으로 로그인해 주세요."; return; }
            await LoadAsync(attemptId);
        }
        catch (RoleWorkspaceAccessException ex) when (owner == generation && !disposed) { AccessFailure(ex); }
        catch (OperationCanceledException) { }
        catch when (owner != generation || disposed) { }
        catch { Message = "최신 업무를 확인하지 못했습니다. 다시 조회해 주세요."; }
        finally { Changed?.Invoke(); }
    }

    private async Task LoadAsync(string? attemptId)
    {
        IsBusy = true; IsLoaded = false; IsCompleted = false; Message = null;
        var owner = generation;
        try
        {
            var value = await ReadDetailAsync(lifetime.Token);
            if (!Current(owner)) return;
            detail = value;
            allowed = ResolveAllowed(value);
            if (RoleKey != "operator" && allowed is null) { Message = "현재 요청할 수 없는 행동입니다. 업무로 돌아가 최신 상태를 확인해 주세요."; return; }
            if (value is 음식주문응답 order)
            {
                RestaurantName = order.음식점명; RestaurantAddress = order.음식점주소; RestaurantDetailAddress = order.음식점상세주소;
                PreparationMinutes = order.조리예상분;
            }
            if (RoleKey == "operator")
            {
                AttemptId = InterruptedAttempts.Any(value => value.시도StableId == attemptId)
                    ? attemptId! : InterruptedAttempts.FirstOrDefault()?.시도StableId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(AttemptId)) { Message = "검토할 중단 배달 시도가 없습니다."; return; }
            }
            IsLoaded = true;
        }
        finally { if (Current(owner)) IsBusy = false; }
    }

    private async Task<object> ReadDetailAsync(CancellationToken token, bool afterCommand = false)
    {
        var id = Uri.EscapeDataString(ItemId);
        if (RoleKey == "orderer" && ActionKey == 음식배달가능행동Ids.주문취소)
        {
            var order = await api.GetAsync<주문자음식주문상세응답>(RoleKey, $"api/v1/food-orders/{id}", token);
            if (order.주문.주문번호 != ItemId) throw new InvalidOperationException("주문 응답이 일치하지 않습니다.");
            return order;
        }
        if (RoleKey == "restaurant" && ActionKey is 음식배달가능행동Ids.음식점주문수락 or 음식배달가능행동Ids.음식점조리시간변경 or 음식배달가능행동Ids.음식점주문거절)
        {
            var order = await api.GetAsync<음식주문응답>(RoleKey, $"api/v1/food-orders/restaurant/inbox/{id}", token);
            if (order.주문번호 != ItemId) throw new InvalidOperationException("주문 응답이 일치하지 않습니다.");
            return order;
        }
        if (RoleKey == "food-driver" && ActionKey == 음식배달가능행동Ids.기사배달중단)
        {
            var workspace = await api.GetAsync<FoodDeliveryDriverCurrentWorkDto>(RoleKey, "api/v1/driver/food-deliveries/workspace/current", token);
            if (workspace.DriverId != identity?.OwnerId) throw new InvalidOperationException("기사 업무 응답이 일치하지 않습니다.");
            return afterCommand ? workspace : (object?)workspace.ActiveDeliveries.FirstOrDefault(value => value.OfferId == ItemId)
                ?? throw new InvalidOperationException("현재 수행 배달을 찾지 못했습니다.");
        }
        if (RoleKey == "operator" && ActionKey == OperatorRoleWorkspaceAdapter.ReviewInterruption)
        {
            var trace = await api.GetAsync<음식주문운영추적응답>(RoleKey, $"api/v1/admin/food-orders/{id}/operations-trace", token);
            if (trace.주문번호 != ItemId) throw new InvalidOperationException("주문 응답이 일치하지 않습니다.");
            return trace;
        }
        throw new InvalidOperationException("지원하지 않는 역할 업무 요청입니다.");
    }

    private 업무가능행동Dto? ResolveAllowed(object value)
    {
        IReadOnlyList<업무가능행동Dto> actions = value switch
        {
            주문자음식주문상세응답 order => order.AvailableActions,
            음식주문응답 order => order.AvailableActions,
            FoodDeliveryDriverActiveDeliveryDto delivery => delivery.AvailableActions,
            _ => []
        };
        return actions.FirstOrDefault(value => value.ActionId == ActionKey
            && (!value.ExpiresAtUtc.HasValue || value.ExpiresAtUtc.Value > DateTime.UtcNow));
    }

    public void Prepare()
    {
        if (disposed || IsBusy || !IsLoaded || pending is not null || !Current(generation)) return;
        Message = null;
        try
        {
            var requestId = Guid.NewGuid();
            pending = BuildRequest(requestId); Confirmed = false;
        }
        catch (ArgumentException ex) { Message = ex.Message; }
        catch (InvalidOperationException ex) { Message = ex.Message; }
        Changed?.Invoke();
    }

    private FoodRolePreparedRequest BuildRequest(Guid requestId)
    {
        var selectedAttempt = InterruptedAttempts.FirstOrDefault(value => value.시도StableId == AttemptId);
        var input = new FoodRoleRequestSnapshot(RoleKey, ItemId, ActionKey)
        {
            HasAllowedAction = allowed is not null, ExpectedRevision = allowed?.ExpectedRevision, ActionExpiresAtUtc = allowed?.ExpiresAtUtc,
            OrdererHistoryCount = detail is 주문자음식주문상세응답 orderer ? orderer.상태이력.Count : 0,
            Order = detail is 음식주문응답 order ? new(order.Revision, order.음식점주소, order.음식점상세주소, order.음식점위도, order.음식점경도) : null,
            Delivery = detail is FoodDeliveryDriverActiveDeliveryDto delivery ? new(delivery.DeliveryAttemptId, delivery.AttemptRevision) : null,
            Review = selectedAttempt is null ? null : new(selectedAttempt.시도StableId, selectedAttempt.Revision),
            PreparationMinutes = PreparationMinutes, ImmediatePickup = ImmediatePickup,
            RestaurantName = RestaurantName, RestaurantAddress = RestaurantAddress, RestaurantDetailAddress = RestaurantDetailAddress,
            ReasonCode = ReasonCode, Reason = Reason, AttemptId = AttemptId, DecisionCode = DecisionCode, AbuseConfirmed = AbuseConfirmed
        };
        return FoodRoleRequestPreparation.Prepare(input, requestId, DateTime.UtcNow);
    }

    public async Task<bool> SubmitAsync()
    {
        if (IsAwaitingResult) return await CheckResultAsync();
        if (disposed || IsBusy || pending is null || !Confirmed || !Current(generation)) return false;
        var request = pending; var owner = generation; var token = lifetime.Token; IsBusy = true; submitted = true; Message = null; Changed?.Invoke();
        try
        {
            var response = await SendCommandAsync(request, token);
            if (!Current(owner)) return false;
            // 응답을 받은 명령은 재전송하지 않고 후속 원장 조회로만 결과를 확인합니다.
            commandAcknowledged = true;
            completion = FoodRoleCompletionExpectation.Create(ItemId, request.RequestId, request.Body, response,
                request.BeforeRevision, request.AttemptId, request.OriginalDetailAddress, identity?.OwnerId);
            return await ConfirmResultAsync(owner, token);
        }
        catch (OperationCanceledException) when (Current(owner) && !token.IsCancellationRequested)
        { return await RecoverUnknownResultAsync(owner, token); }
        catch (OperationCanceledException) { return false; }
        catch (RoleWorkspaceAccessException ex) when (Current(owner))
        {
            if (ex.StatusCode is 401 or 403) AccessFailure(ex);
            else if (!commandAcknowledged && ex.StatusCode is (400 or 404 or 409 or 422))
            { pending = null; Confirmed = false; IsLoaded = false; Message = "업무 상태가 변경되었습니다. 최신 상태를 다시 확인해 주세요."; }
            else if (!commandAcknowledged) return await RecoverUnknownResultAsync(owner, token);
            else Message = "요청 응답을 받았지만 최신 결과를 조회하지 못했습니다. 결과를 다시 확인해 주세요.";
            return false;
        }
        catch when (!Current(owner)) { return false; }
        catch
        {
            if (!commandAcknowledged) return await RecoverUnknownResultAsync(owner, token);
            Message = "요청 응답을 받았지만 최신 결과를 조회하지 못했습니다. 결과를 다시 확인해 주세요.";
            return false;
        }
        finally { if (Current(owner)) IsBusy = false; Changed?.Invoke(); }
    }

    private async Task<object?> SendCommandAsync(FoodRolePreparedRequest request, CancellationToken token)
    {
        if (request.UsePut) return await api.PutAsync<음식배달시도운영응답>(RoleKey, request.Path, request.Body, token);
        if (request.Body is 음식배달중단요청) return await api.PostAsync<FoodDeliveryDriverActionResponse>(RoleKey, request.Path, request.Body, token);
        return await api.PostAsync<음식주문응답>(RoleKey, request.Path, request.Body, token);
    }

    /// <summary>조회만으로 확인되지 않은 최초 요청을 사용자가 명시적으로 다시 확인합니다.</summary>
    public async Task<bool> RetrySameRequestAsync()
    {
        if (!CanRetrySameRequest || !Current(generation)) return false;
        // 재확인 직전에도 정본을 먼저 읽습니다. 조회 실패 시 POST하지 않습니다.
        if (await CheckResultAsync()) return true;
        if (!CanRetrySameRequest || pending is null || !Current(generation)) return false;
        submitted = false; Confirmed = true;
        return await SubmitAsync();
    }

    private async Task<bool> RecoverUnknownResultAsync(long owner, CancellationToken token)
    {
        canonicalReadSucceeded = false;
        try { return await ConfirmResultAsync(owner, token); }
        catch (OperationCanceledException) { return false; }
        catch (RoleWorkspaceAccessException ex) when (Current(owner))
        {
            if (ex.StatusCode is 401 or 403) AccessFailure(ex);
            else Message = "요청 결과와 최신 상태를 확인하지 못했습니다. 결과를 다시 확인해 주세요.";
            return false;
        }
        catch when (!Current(owner)) { return false; }
        catch { Message = "요청 결과와 최신 상태를 확인하지 못했습니다. 결과를 다시 확인해 주세요."; return false; }
    }

    public async Task<bool> CheckResultAsync()
    {
        if (disposed || IsBusy || !IsAwaitingResult || !Current(generation)) return false;
        var owner = generation; var token = lifetime.Token; IsBusy = true; canonicalReadSucceeded = false; Message = null; Changed?.Invoke();
        try { return await ConfirmResultAsync(owner, token); }
        catch (OperationCanceledException) { return false; }
        catch (RoleWorkspaceAccessException ex) when (Current(owner))
        {
            if (ex.StatusCode is 401 or 403) AccessFailure(ex);
            else Message = "최신 결과를 조회하지 못했습니다. 결과를 다시 확인해 주세요.";
            return false;
        }
        catch when (!Current(owner)) { return false; }
        catch { Message = "최신 결과를 조회하지 못했습니다. 결과를 다시 확인해 주세요."; return false; }
        finally { if (Current(owner)) IsBusy = false; Changed?.Invoke(); }
    }

    private async Task<bool> ConfirmResultAsync(long owner, CancellationToken token)
    {
        var current = await ReadDetailAsync(token, afterCommand: true);
        if (!Current(owner)) return false;
        canonicalReadSucceeded = true;
        if (!commandAcknowledged && pending is { } request)
            completion = FoodRoleCompletionExpectation.FromCanonical(ItemId, request.RequestId, request.Body, current,
                request.BeforeRevision, request.AttemptId, request.OriginalDetailAddress, identity?.OwnerId);
        if (completion?.Matches(current) != true)
        {
            Message = commandAcknowledged ? "요청 응답을 받았지만 반영 상태를 아직 확인하지 못했습니다. 결과를 다시 확인해 주세요."
                : "현재 업무를 조회했지만 이 요청의 처리 결과는 확인되지 않았습니다. 같은 요청을 다시 확인할 수 있습니다.";
            return false;
        }
        detail = current; pending = null; completion = null; Confirmed = false; IsCompleted = true; IsLoaded = false; Message = null;
        return true;
    }

    public void Edit()
    {
        if (disposed || IsBusy) return;
        // 결과가 미확인인 요청은 같은 본문과 요청 ID로 먼저 확인합니다.
        if (pending is not null && submitted) { Message = "먼저 같은 내용으로 요청 결과를 확인해 주세요."; Changed?.Invoke(); return; }
        pending = null; Confirmed = false; Changed?.Invoke();
    }

    private void AccessFailure(RoleWorkspaceAccessException ex)
    { Invalidate(); RequiresLogin = ex.StatusCode is 401 or 403; Message = ex.StatusCode == 403 ? "이 업무에 접근할 권한이 없습니다. 역할 계정을 확인해 주세요." : "역할 계정으로 다시 로그인해 주세요."; }
    private bool Current(long owner) => !disposed && owner == generation && identity is { IsAuthenticated: true } && identity == access.GetIdentity(RoleKey);
    private void AuthenticationChanged()
    {
        if (identity is null || identity == access.GetIdentity(RoleKey)) return;
        Invalidate(); identity = access.GetIdentity(RoleKey); RequiresLogin = !identity.IsAuthenticated;
        Message = "계정이 변경되었습니다. 최신 업무를 다시 확인해 주세요."; Changed?.Invoke();
    }
    private void Invalidate()
    {
        generation++; lifetime.Cancel(); lifetime.Dispose(); lifetime = new(); detail = null; allowed = null; pending = null; submitted = false;
        commandAcknowledged = false; canonicalReadSucceeded = false; completion = null;
        IsBusy = false; IsLoaded = false; IsCompleted = false; Confirmed = false; PreparationMinutes = null; ImmediatePickup = false;
        RestaurantName = RestaurantAddress = RestaurantDetailAddress = ReasonCode = Reason = AttemptId = DecisionCode = string.Empty; AbuseConfirmed = false;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; access.Changed -= AuthenticationChanged; generation++; lifetime.Cancel(); lifetime.Dispose();
        detail = null; pending = null; completion = null; commandAcknowledged = false;
    }
}
