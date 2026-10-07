using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>입력·확인·동일 요청 재시도를 한 역할 계정 수명에 결속합니다.</summary>
public sealed class FoodRoleActionViewModel : IDisposable
{
    private readonly IRoleWorkspaceApi api;
    private readonly IRoleWorkspaceAccess access;
    private CancellationTokenSource lifetime = new();
    private RoleWorkspaceIdentity? identity;
    private long generation;
    private bool disposed;
    private object? detail;
    private PendingRequest? pending;
    private 업무가능행동Dto? allowed;
    private bool submitted;
    private bool commandAcknowledged;
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
    public bool IsAwaitingResult => pending is not null && commandAcknowledged;
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
            var workspace = await api.GetAsync<FoodDeliveryDriverWorkspaceDto>(RoleKey, "api/v1/driver/food-deliveries/workspace", token);
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

    private PendingRequest BuildRequest(Guid requestId)
    {
        if (RoleKey != "operator" && (allowed is null || allowed.ExpiresAtUtc is { } expires && expires <= DateTime.UtcNow))
            throw new InvalidOperationException("현재 가능 행동을 다시 조회해 주세요.");
        var cleanReason = Reason.Trim();
        if (cleanReason.Length > 1000) throw new ArgumentException("사유는 1,000자 이내로 입력해 주세요.");
        var id = Uri.EscapeDataString(ItemId);
        if (RoleKey == "orderer")
        {
            if (ReasonCode is not (운영배차주문자취소사유Code.단순변심 or 운영배차주문자취소사유Code.중복주문 or 운영배차주문자취소사유Code.주소문제 or 운영배차주문자취소사유Code.기타)
                || ReasonCode == 운영배차주문자취소사유Code.기타 && cleanReason.Length == 0) throw new ArgumentException("취소 사유를 선택하고 필요한 설명을 입력해 주세요.");
            return new(requestId, $"api/v1/food-orders/{id}/cancellation", new 주문자음식주문취소요청
                { 클라이언트요청Id = requestId, 예상Revision = allowed!.ExpectedRevision, 사유Code = ReasonCode, 사유 = cleanReason },
                BeforeRevision: allowed.ExpectedRevision ?? ((주문자음식주문상세응답)detail!).상태이력.Count);
        }
        if (RoleKey == "restaurant" && ActionKey == 음식배달가능행동Ids.음식점주문수락)
        {
            if (!ImmediatePickup && PreparationMinutes is not (> 0 and <= 240)) throw new ArgumentException("조리 예상 시간을 1~240분으로 입력해 주세요.");
            if (string.IsNullOrWhiteSpace(RestaurantName) || string.IsNullOrWhiteSpace(RestaurantAddress)) throw new ArgumentException("음식점 이름과 픽업 주소를 확인해 주세요.");
            var order = (음식주문응답)detail!;
            var originalAddress = RestaurantAddress.Trim() == order.음식점주소.Trim() && RestaurantDetailAddress.Trim() == order.음식점상세주소.Trim();
            return new(requestId, $"api/v1/food-orders/{id}/restaurant-acceptance", new 음식점주문수락요청
            {
                클라이언트요청Id = requestId, 음식점명 = RestaurantName.Trim(), 음식점주소 = RestaurantAddress.Trim(), 음식점상세주소 = RestaurantDetailAddress.Trim(),
                음식점위도 = originalAddress ? order.음식점위도 : null, 음식점경도 = originalAddress ? order.음식점경도 : null,
                조리예상분 = ImmediatePickup ? 0 : PreparationMinutes, 즉시픽업가능여부 = ImmediatePickup, 수락메모 = cleanReason
            }, BeforeRevision: order.Revision, OriginalDetailAddress: order.음식점상세주소);
        }
        if (RoleKey == "restaurant")
        {
            if (ActionKey == 음식배달가능행동Ids.음식점조리시간변경 && PreparationMinutes is not (> 0 and <= 240)) throw new ArgumentException("조리 예상 시간을 1~240분으로 입력해 주세요.");
            if (ActionKey == 음식배달가능행동Ids.음식점주문거절 && cleanReason.Length == 0) throw new ArgumentException("거절 사유를 입력해 주세요.");
            return new(requestId, $"api/v1/food-orders/{id}/restaurant-progress", new 음식점주문진행변경요청
            {
                클라이언트요청Id = requestId, 예상Revision = allowed!.ExpectedRevision ?? ((음식주문응답)detail!).Revision,
                작업 = ActionKey == 음식배달가능행동Ids.음식점주문거절 ? 음식점주문진행작업코드.거절 : 음식점주문진행작업코드.조리시간변경,
                조리예상분 = ActionKey == 음식배달가능행동Ids.음식점조리시간변경 ? PreparationMinutes : null, 사유 = cleanReason
            }, BeforeRevision: allowed.ExpectedRevision ?? ((음식주문응답)detail!).Revision);
        }
        if (RoleKey == "food-driver")
        {
            var delivery = (FoodDeliveryDriverActiveDeliveryDto)detail!;
            if (string.IsNullOrWhiteSpace(delivery.DeliveryAttemptId)) throw new ArgumentException("현재 배달 시도를 다시 확인해 주세요.");
            return new(requestId, $"api/v1/driver/food-deliveries/offers/{id}/interruption", new 음식배달중단요청
            {
                클라이언트요청Id = requestId, 예상시도Revision = allowed!.ExpectedRevision ?? delivery.AttemptRevision,
                사유Code = 음식배달중단사유Code.정규화(ReasonCode), 메모 = cleanReason
            }, BeforeRevision: allowed.ExpectedRevision ?? delivery.AttemptRevision, AttemptId: delivery.DeliveryAttemptId);
        }
        var attempt = InterruptedAttempts.FirstOrDefault(value => value.시도StableId == AttemptId)
            ?? throw new ArgumentException("검토할 중단 배달을 선택해 주세요.");
        if (DecisionCode is not (음식배달중단검토판정Code.보호 or 음식배달중단검토판정Code.기사책임 or 음식배달중단검토판정Code.음식점책임 or 음식배달중단검토판정Code.플랫폼책임)
            || cleanReason.Length == 0) throw new ArgumentException("판정과 근거를 입력해 주세요.");
        return new(requestId, $"api/v1/admin/food-orders/delivery-attempts/{Uri.EscapeDataString(AttemptId)}/interruption-review",
            new 음식배달중단검토요청 { 클라이언트요청Id = requestId, 예상Revision = attempt.Revision, 판정Code = DecisionCode, 판정사유 = cleanReason, 악용확정여부 = AbuseConfirmed },
            true, attempt.Revision, AttemptId: attempt.시도StableId);
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
        catch (OperationCanceledException) { return false; }
        catch (RoleWorkspaceAccessException ex) when (Current(owner))
        {
            if (ex.StatusCode is 401 or 403) AccessFailure(ex);
            else if (!commandAcknowledged && ex.StatusCode is (400 or 404 or 409 or 422))
            { pending = null; Confirmed = false; IsLoaded = false; Message = "업무 상태가 변경되었습니다. 최신 상태를 다시 확인해 주세요."; }
            else Message = commandAcknowledged ? "요청 응답을 받았지만 최신 결과를 조회하지 못했습니다. 결과를 다시 확인해 주세요."
                : "요청 결과를 확인하지 못했습니다. 같은 내용으로 다시 시도해 주세요.";
            return false;
        }
        catch when (!Current(owner)) { return false; }
        catch { Message = commandAcknowledged ? "요청 응답을 받았지만 최신 결과를 조회하지 못했습니다. 결과를 다시 확인해 주세요."
            : "요청 결과를 확인하지 못했습니다. 같은 내용으로 다시 시도해 주세요."; return false; }
        finally { if (Current(owner)) IsBusy = false; Changed?.Invoke(); }
    }

    private async Task<object?> SendCommandAsync(PendingRequest request, CancellationToken token)
    {
        if (request.UsePut) return await api.PutAsync<음식배달시도운영응답>(RoleKey, request.Path, request.Body, token);
        if (request.Body is 음식배달중단요청) return await api.PostAsync<FoodDeliveryDriverActionResponse>(RoleKey, request.Path, request.Body, token);
        return await api.PostAsync<음식주문응답>(RoleKey, request.Path, request.Body, token);
    }

    public async Task<bool> CheckResultAsync()
    {
        if (disposed || IsBusy || !IsAwaitingResult || !Current(generation)) return false;
        var owner = generation; var token = lifetime.Token; IsBusy = true; Message = null; Changed?.Invoke();
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
        if (completion?.Matches(current) != true)
        {
            Message = "요청 응답을 받았지만 반영 상태를 아직 확인하지 못했습니다. 결과를 다시 확인해 주세요.";
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
        commandAcknowledged = false; completion = null;
        IsBusy = false; IsLoaded = false; IsCompleted = false; Confirmed = false; PreparationMinutes = null; ImmediatePickup = false;
        RestaurantName = RestaurantAddress = RestaurantDetailAddress = ReasonCode = Reason = AttemptId = DecisionCode = string.Empty; AbuseConfirmed = false;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; access.Changed -= AuthenticationChanged; generation++; lifetime.Cancel(); lifetime.Dispose();
        detail = null; pending = null; completion = null; commandAcknowledged = false;
    }
    private sealed record PendingRequest(Guid RequestId, string Path, object Body, bool UsePut = false,
        long BeforeRevision = 0, string? AttemptId = null, string? OriginalDetailAddress = null);
}
