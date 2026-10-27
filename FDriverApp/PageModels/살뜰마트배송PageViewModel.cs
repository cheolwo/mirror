using System.Collections.ObjectModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Contracts.Mart;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;

namespace FDriverApp.PageModels;

/// <summary>마트 수행만 조립합니다. 허용 행동과 상태 확정은 서버가 소유합니다.</summary>
public sealed partial class 살뜰마트배송PageViewModel(I살뜰마트배송ApiClient api, IFDriverAuthSession auth,
    I살뜰마트배송PendingStore store) : ObservableObject
{
    private CancellationTokenSource _lifetime = new();
    private string? _owner;
    private 살뜰마트배송미확인요청? _pending;
    private bool _restored;
    private bool _active;
    private bool _retryVerified;
    public FDriverCompletionFeedbackState Completion { get; } = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _hasLoaded;
    [ObservableProperty] private string _status = "살뜰마트 배송 확인 전";
    [ObservableProperty] private string _notice = string.Empty;
    [ObservableProperty] private string _pickupCode = string.Empty;
    [ObservableProperty] private bool _byNumber = true;
    [ObservableProperty] private 마트배송표시항목? _selectedItem;
    public ObservableCollection<마트배송표시항목> Items { get; } = [];
    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => HasLoaded && IsEnabled && !HasItems;
    public bool HasSelection => SelectedItem is not null;
    public bool HasPendingRequest => _pending is not null;
    public bool NeedsPickupInput => SelectedItem?.Value.CanPickup == true
        || _pending?.Action == 마트배송행동.Pickup;
    public bool CanAccept => CanAct && SelectedItem!.Value.CanAccept;
    public bool CanReject => CanAct && SelectedItem!.Value.CanReject;
    public bool CanPickup => CanAct && SelectedItem!.Value.CanPickup && ValidCode(PickupCode, ByNumber);
    public bool CanDeliver => CanAct && SelectedItem!.Value.CanDeliver;
    public bool CanCheckResult => _active && !IsBusy && _pending is not null && SameOwner(_pending.OwnerId);
    public bool CanRetry => CanCheckResult && _retryVerified && (_pending!.Action != 마트배송행동.Pickup
        || ValidCode(PickupCode, _pending.ByNumber) && HashCode(_pending.RequestId, PickupCode) == _pending.CodeHash);
    public bool CanSelect => !IsBusy && !HasPendingRequest;
    public bool CanChangeInput => !IsBusy && (!HasPendingRequest || _pending?.Action == 마트배송행동.Pickup);
    public bool CanChangeCodeKind => CanChangeInput && !HasPendingRequest;
    public string SelectionState => SelectedItem?.StateText ?? string.Empty;
    public string PickupText => Place(SelectedItem?.Value.Pickup.Label, SelectedItem?.Value.Pickup.Address);
    public string DropoffText => SelectedItem?.Value.CanAccept == true || SelectedItem?.Value.CanReject == true
        ? "수락 후 전달 장소를 확인할 수 있습니다."
        : Place(SelectedItem?.Value.Dropoff.Label, SelectedItem?.Value.Dropoff.Address);
    public string PayoutText => SelectedItem?.Value.DriverPayout is { } amount ? $"배달료 {amount:N0}원" : "배달료 확인 중";

    public async Task ActivateAsync()
    {
        _lifetime.Cancel(); _lifetime.Dispose(); _lifetime = new(); _active = true; IsBusy = false;
        auth.SessionChanged -= SessionChanged; auth.SessionChanged += SessionChanged;
        _owner = auth.UserId;
        if (!SameOwner(_owner)) { ClearMemory(); Status = "기사 로그인 후 배송을 확인해 주세요."; return; }
        await RefreshAsync();
    }
    public void Deactivate()
    {
        Completion.Dismiss();
        _active = false; _lifetime.Cancel(); PickupCode = string.Empty; NotifyState();
        // 계정 변경은 화면 밖에서도 감지합니다. 다음 Activate에서 중복 구독하지 않습니다.
    }
    private async void SessionChanged(object? sender, EventArgs args)
    {
        if (_owner == auth.UserId && auth.IsAuthenticated) return;
        var oldOwner = _owner;
        _lifetime.Cancel(); _restored = false; _owner = auth.UserId; ClearMemory();
        Status = "기사 계정이 변경되었습니다. 다시 들어와 확인해 주세요.";
        if (oldOwner is not null)
        {
            try { await store.ClearAsync(oldOwner); }
            catch { Status = "이전 요청 기록을 정리하지 못했습니다. 다시 들어와 확인해 주세요."; }
        }
    }
    private bool SameOwner(string? owner) => !string.IsNullOrWhiteSpace(owner) && auth.UserId == owner && auth.IsAuthenticated;
    private bool Current(string? owner, CancellationToken token) => _active && !token.IsCancellationRequested && SameOwner(owner);
    private bool CanAct => _active && !IsBusy && IsEnabled && HasLoaded && !HasPendingRequest
        && HasSelection && SameOwner(_owner);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy || !Current(_owner, _lifetime.Token)) return;
        var owner = _owner!; var token = _lifetime.Token; IsBusy = true; NotifyState();
        try
        {
            if (!_restored)
            {
                var saved = await store.LoadAsync(token);
                if (!Current(owner, token)) return;
                if (saved is not null && saved.OwnerId != owner) await store.ClearAsync(saved.OwnerId, token);
                _pending = saved?.OwnerId == owner ? saved : null; _restored = true;
                if (_pending is not null) ByNumber = _pending.ByNumber;
            }
            Status = "배송을 확인하고 있습니다.";
            var list = await api.ListAsync(token);
            if (!Current(owner, token)) return;
            ValidateList(list);
            var selectedId = _pending?.OfferId ?? SelectedItem?.Value.OfferId;
            Items.Clear();
            foreach (var item in list.Items) Items.Add(new(item));
            IsEnabled = list.Enabled; Notice = list.Notice; HasLoaded = true;
            SelectedItem = Items.FirstOrDefault(x => x.Value.OfferId == selectedId) ?? Items.FirstOrDefault();
            Status = list.Enabled ? Items.Count == 0 ? "현재 제안되거나 수행 중인 마트 배송이 없습니다." : "현재 배송을 확인했습니다."
                : "살뜰마트 배송 기능이 비활성 상태입니다.";
            if (_pending is not null) await RecoverCoreAsync(owner, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || !SameOwner(owner)) { }
        catch (FDriverApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            if (Current(owner, token)) PauseForAccess();
        }
        catch
        {
            if (Current(owner, token)) { HasLoaded = false; _retryVerified = false; Status = "배송을 확인하지 못했습니다. 다시 조회해 주세요."; }
        }
        finally { if (_lifetime.Token == token) IsBusy = false; NotifyState(); }
    }

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private Task AcceptAsync() => BeginAsync(마트배송행동.Accept);
    [RelayCommand(CanExecute = nameof(CanReject))]
    private Task RejectAsync() => BeginAsync(마트배송행동.Reject);
    [RelayCommand(CanExecute = nameof(CanPickup))]
    private Task PickupAsync() => BeginAsync(마트배송행동.Pickup);
    [RelayCommand(CanExecute = nameof(CanDeliver))]
    private Task DeliverAsync() => BeginAsync(마트배송행동.Deliver);

    private async Task BeginAsync(마트배송행동 action)
    {
        if (!CanAct || SelectedItem is null || (action switch
            { 마트배송행동.Accept => !CanAccept, 마트배송행동.Reject => !CanReject,
                마트배송행동.Pickup => !CanPickup, _ => !CanDeliver })) return;
        var value = SelectedItem.Value; var id = Guid.NewGuid(); var code = PickupCode.Trim();
        Completion.Dismiss();
        var pending = new 살뜰마트배송미확인요청(_owner!, id, value.OfferId, action, value.Revision,
            value.LabelRevision ?? 0, value.RecommendationRound, value.QueueUpdatedAtUtc,
            action == 마트배송행동.Pickup && ByNumber, action == 마트배송행동.Pickup ? HashCode(id, code) : null);
        await SendAsync(pending, code, true);
    }

    private async Task SendAsync(살뜰마트배송미확인요청 pending, string code, bool persist)
    {
        var token = _lifetime.Token; var owner = pending.OwnerId;
        if (IsBusy || !Current(owner, token)) return;
        IsBusy = true; _retryVerified = false; NotifyState();
        try
        {
            if (persist) await store.SaveAsync(pending, token);
            if (!Current(owner, token)) { await store.ClearAsync(owner); return; }
            _pending = pending; PickupCode = string.Empty; Status = "요청 결과를 확인하고 있습니다."; NotifyState();
            var result = pending.Action switch
            {
                마트배송행동.Accept => await api.AcceptAsync(pending.OfferId, new() { ClientRequestId = pending.RequestId,
                    ExpectedRecommendationRound = pending.RecommendationRound, ExpectedQueueUpdatedAtUtc = pending.QueueUpdatedAtUtc }, token),
                마트배송행동.Reject => await api.RejectAsync(pending.OfferId, new() { ClientRequestId = pending.RequestId,
                    ExpectedRecommendationRound = pending.RecommendationRound, ExpectedQueueUpdatedAtUtc = pending.QueueUpdatedAtUtc }, token),
                마트배송행동.Pickup => await api.PickupAsync(pending.OfferId, new() { ClientRequestId = pending.RequestId,
                    ExpectedRevision = pending.Revision, ExpectedLabelRevision = pending.LabelRevision, Code = code, ByNumber = pending.ByNumber }, token),
                _ => await api.DeliverAsync(pending.OfferId, new() { ClientRequestId = pending.RequestId, ExpectedRevision = pending.Revision }, token)
            };
            if (!Current(owner, token)) return;
            ValidateItem(result, pending.OfferId);
            ValidateCompletionReceipt(pending, result);
            await ConfirmCurrentAsync(pending, owner, token, result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || !SameOwner(owner)) { }
        catch (FDriverApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            if (Current(owner, token)) PauseForAccess();
        }
        catch (FDriverApiException ex) when (살뜰마트배송ApiClient.IsConfirmedCommandRejection(ex))
        {
            try
            {
                _pending = pending with { ServerRejected = true };
                await store.SaveAsync(_pending, token);
                if (!Current(owner, token)) return;
                await RejectedCurrentAsync(_pending, owner, token);
            }
            catch { if (Current(owner, token)) Status = "요청 거절 후 현재 상태를 확인하지 못했습니다. 결과부터 재조회해 주세요."; }
        }
        catch
        {
            if (Current(owner, token)) Status = _pending is null ? "요청을 안전하게 저장하지 못해 전송하지 않았습니다. 다시 시도해 주세요."
                : "요청 결과가 불명확합니다. 다시 보내기 전에 결과를 확인해 주세요.";
        }
        finally { code = string.Empty; if (_lifetime.Token == token) IsBusy = false; NotifyState(); }
    }

    [RelayCommand(CanExecute = nameof(CanCheckResult))]
    private async Task CheckResultAsync()
    {
        if (!CanCheckResult) return;
        var owner = _owner!; var token = _lifetime.Token; IsBusy = true; _retryVerified = false; NotifyState();
        try { await RecoverCoreAsync(owner, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested || !SameOwner(owner)) { }
        catch (FDriverApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { if (Current(owner, token)) PauseForAccess(); }
        catch { if (Current(owner, token)) Status = "요청 결과를 확인하지 못했습니다. 다시 보내지 않고 재조회해 주세요."; }
        finally { if (_lifetime.Token == token) IsBusy = false; NotifyState(); }
    }
    private async Task RecoverCoreAsync(string owner, CancellationToken token)
    {
        var pending = _pending;
        if (pending is null) return;
        _retryVerified = false;
        if (pending.ServerRejected) { await RejectedCurrentAsync(pending, owner, token); return; }
        try
        {
            var result = await api.ResultAsync(pending.RequestId, token);
            if (!Current(owner, token)) return;
            ValidateItem(result, pending.OfferId);
            ValidateCompletionReceipt(pending, result);
            await ConfirmCurrentAsync(pending, owner, token, result);
        }
        catch (FDriverApiException ex) when (살뜰마트배송ApiClient.IsConfirmedCommandRejection(ex))
        {
            if (!Current(owner, token)) return;
            _pending = pending with { ServerRejected = true };
            await store.SaveAsync(_pending, token);
            if (!Current(owner, token)) return;
            await RejectedCurrentAsync(_pending, owner, token);
        }
        catch (FDriverApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var current = await api.GetAsync(pending.OfferId, token);
            if (!Current(owner, token)) return;
            ValidateItem(current, pending.OfferId); Apply(current);
            _retryVerified = Matches(pending, current) && IsEnabled;
            Status = _retryVerified ? "요청 기록이 없고 이전 상태가 유지됩니다. 원하면 같은 요청을 다시 보낼 수 있습니다."
                : "배송 상태가 바뀌었습니다. 요청 결과를 다시 확인해 주세요.";
        }
    }
    private async Task RejectedCurrentAsync(살뜰마트배송미확인요청 pending, string owner, CancellationToken token)
    {
        var removed = false;
        try
        {
            var current = await api.GetAsync(pending.OfferId, token);
            if (!Current(owner, token)) return;
            ValidateItem(current, pending.OfferId); Apply(current);
        }
        catch (FDriverApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var list = await api.ListAsync(token);
            if (!Current(owner, token)) return;
            ValidateList(list);
            if (!list.Enabled || list.Items.Any(x => x.OfferId == pending.OfferId))
                throw new InvalidDataException("변경된 제안 목록을 확인하지 못했습니다.");
            Items.Clear(); foreach (var item in list.Items) Items.Add(new(item));
            SelectedItem = Items.FirstOrDefault(); IsEnabled = list.Enabled; Notice = list.Notice; HasLoaded = true; removed = true;
        }
        await store.ClearAsync(owner, token);
        if (!Current(owner, token)) return;
        _pending = null;
        Status = removed ? "배송 제안이 변경되어 요청이 처리되지 않았습니다. 현재 목록을 확인해 주세요."
            : "요청이 처리되지 않았습니다. 현재 상태와 입력을 확인한 뒤 다시 선택해 주세요.";
    }
    private async Task ConfirmCurrentAsync(살뜰마트배송미확인요청 pending, string owner, CancellationToken token,
        마트기사업무항목Dto receiptResult)
    {
        마트기사업무항목Dto? confirmed = null;
        if (pending.Action == 마트배송행동.Reject)
        {
            // 거절 뒤에는 다음 기사의 업무 조회 권한이 없습니다. 본인 요청 영수증과 목록만 확인합니다.
            var receipt = await api.ResultAsync(pending.RequestId, token);
            if (!Current(owner, token)) return;
            ValidateItem(receipt, pending.OfferId);
            if (receipt.StateCode != "Rejected") throw new InvalidDataException("거절 결과를 확인하지 못했습니다.");
            var list = await api.ListAsync(token);
            if (!Current(owner, token)) return;
            ValidateList(list); Items.Clear();
            foreach (var item in list.Items.Where(x => x.OfferId != pending.OfferId)) Items.Add(new(item));
            IsEnabled = list.Enabled; Notice = list.Notice;
            Apply(new 마트기사업무항목Dto { OfferId = receipt.OfferId, StateCode = "Rejected", IsReplay = true });
        }
        else
        {
            var current = await api.GetAsync(pending.OfferId, token);
            if (!Current(owner, token)) return;
            ValidateItem(current, pending.OfferId);
            if (pending.Action == 마트배송행동.Pickup && (current.StateCode is not ("PickedUp" or "Delivered" or "Completed") || current.Revision <= pending.Revision)
                || pending.Action == 마트배송행동.Deliver && (current.StateCode is not ("Delivered" or "Completed") || current.Revision <= pending.Revision))
                throw new InvalidDataException("완료 결과와 현재 배송 상태가 일치하지 않습니다.");
            if (pending.Action is 마트배송행동.Pickup or 마트배송행동.Deliver
                && (current.AttemptStableId != receiptResult.AttemptStableId || current.Revision < receiptResult.Revision))
                throw new InvalidDataException("완료 기록과 현재 배송 시도가 일치하지 않습니다.");
            Apply(current); confirmed = current;
        }
        await store.ClearAsync(owner, token);
        if (!Current(owner, token)) return;
        _pending = null; _retryVerified = false;
        Status = pending.Action switch { 마트배송행동.Accept => "마트 배송을 수락했습니다.", 마트배송행동.Reject => "배송 제안을 거절했습니다.",
            마트배송행동.Pickup => "픽업을 완료했습니다.", _ => "전달을 완료했습니다. 수령 확인·정산·지급은 별도입니다." };
        if (confirmed is not null && pending.Action is 마트배송행동.Pickup or 마트배송행동.Deliver)
        {
            var final = pending.Action == 마트배송행동.Deliver;
            Completion.Record(new($"mart:{pending.OfferId}:{pending.RequestId:D}:{pending.Action}",
                final ? "전달을 완료했습니다" : "상품을 픽업했습니다",
                final ? "해당 마트 배송의 전달 완료 기록을 확인했습니다." : "해당 마트 배송의 픽업 완료 기록을 확인했습니다.",
                final ? "수령 확인·정산·지급은 각 내역에서 확인해 주세요." : "다음 단계: 전달 장소로 이동",
                final, WorkflowCompletionVisuals.PickingIconPath, "#0F766E"));
        }
    }
    [RelayCommand(CanExecute = nameof(CanRetry))]
    private async Task RetryAsync()
    {
        if (!CanRetry || _pending is null) return;
        var pending = _pending; var code = PickupCode.Trim();
        await SendAsync(pending, code, false);
    }
    private void Apply(마트기사업무항목Dto item)
    {
        var old = Items.FirstOrDefault(x => x.Value.OfferId == item.OfferId);
        if (old is not null && item.Revision < old.Value.Revision) throw new InvalidDataException("이전 배송 판본입니다.");
        var view = new 마트배송표시항목(item);
        if (old is null) Items.Add(view); else Items[Items.IndexOf(old)] = view;
        SelectedItem = view; HasLoaded = true;
    }
    private static void ValidateItem(마트기사업무항목Dto? item, string offerId)
    {
        if (item is null || item.OfferId != offerId || item.Revision < 0) throw new InvalidDataException("배송 대상이 일치하지 않습니다.");
    }
    private static void ValidateCompletionReceipt(살뜰마트배송미확인요청 pending, 마트기사업무항목Dto receipt)
    {
        if (pending.Action is 마트배송행동.Pickup or 마트배송행동.Deliver && string.IsNullOrWhiteSpace(receipt.AttemptStableId))
            throw new InvalidDataException("완료 기록의 배송 시도를 확인하지 못했습니다.");
        if (pending.Action == 마트배송행동.Pickup && (receipt.StateCode is not ("PickedUp" or "Delivered" or "Completed") || receipt.Revision <= pending.Revision)
            || pending.Action == 마트배송행동.Deliver && (receipt.StateCode is not ("Delivered" or "Completed") || receipt.Revision <= pending.Revision))
            throw new InvalidDataException("완료 기록을 확인하지 못했습니다.");
    }
    private static void ValidateList(마트기사업무목록Dto list)
    {
        if (list.Items is null || list.Items.Any(x => string.IsNullOrWhiteSpace(x.OfferId) || x.Revision < 0)
            || list.Items.Select(x => x.OfferId).Distinct(StringComparer.Ordinal).Count() != list.Items.Count)
            throw new InvalidDataException("배송 목록을 확인하지 못했습니다.");
    }
    private static bool Matches(살뜰마트배송미확인요청 request, 마트기사업무항목Dto current) => request.Action switch
    {
        마트배송행동.Accept => current.CanAccept && current.RecommendationRound == request.RecommendationRound && current.QueueUpdatedAtUtc == request.QueueUpdatedAtUtc,
        마트배송행동.Reject => current.CanReject && current.RecommendationRound == request.RecommendationRound && current.QueueUpdatedAtUtc == request.QueueUpdatedAtUtc,
        마트배송행동.Pickup => current.CanPickup && current.Revision == request.Revision && current.LabelRevision == request.LabelRevision,
        _ => current.CanDeliver && current.Revision == request.Revision
    };
    private static string HashCode(Guid requestId, string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestId.ToString("D") + ":" + code.Trim())));
    private static bool ValidCode(string code, bool byNumber) => byNumber ? code.Trim().Length == 4 && code.Trim().All(char.IsAsciiDigit)
        : !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= 512;
    private static string Place(string? label, string? address) => string.IsNullOrWhiteSpace(address) ? label ?? "장소 확인 중" : $"{label}\n{address}";
    private void ClearMemory()
    {
        Completion.Clear();
        Items.Clear(); SelectedItem = null; _pending = null; _retryVerified = false; PickupCode = string.Empty;
        HasLoaded = false; IsEnabled = false; Notice = string.Empty; IsBusy = false; NotifyState();
    }
    private void PauseForAccess()
    {
        Items.Clear(); SelectedItem = null; HasLoaded = false; IsEnabled = false; _retryVerified = false; PickupCode = string.Empty;
        Status = "로그인 또는 배송 권한을 다시 확인해 주세요. 이전 요청은 보존했습니다.";
    }
    partial void OnSelectedItemChanged(마트배송표시항목? oldValue, 마트배송표시항목? newValue)
    {
        if (oldValue?.Value.OfferId != newValue?.Value.OfferId) Completion.Dismiss();
        PickupCode = string.Empty; NotifyState();
    }
    partial void OnPickupCodeChanged(string value) => NotifyState();
    partial void OnByNumberChanged(bool value) { PickupCode = string.Empty; NotifyState(); }
    private void NotifyState()
    {
        foreach (var name in new[] { nameof(HasItems), nameof(IsEmpty), nameof(HasSelection), nameof(HasPendingRequest), nameof(NeedsPickupInput),
            nameof(CanAccept), nameof(CanReject), nameof(CanPickup), nameof(CanDeliver), nameof(CanCheckResult), nameof(CanRetry),
            nameof(CanSelect), nameof(CanChangeInput), nameof(CanChangeCodeKind), nameof(SelectionState), nameof(PickupText), nameof(DropoffText), nameof(PayoutText) })
            OnPropertyChanged(name);
        AcceptCommand.NotifyCanExecuteChanged(); RejectCommand.NotifyCanExecuteChanged(); PickupCommand.NotifyCanExecuteChanged();
        DeliverCommand.NotifyCanExecuteChanged(); CheckResultCommand.NotifyCanExecuteChanged(); RetryCommand.NotifyCanExecuteChanged();
    }
}

public sealed record 마트배송표시항목(마트기사업무항목Dto Value)
{
    public string Title => string.IsNullOrWhiteSpace(Value.Pickup.Label) ? "마트 배송" : Value.Pickup.Label;
    public string StateText => Value.StateCode switch
    {
        "Recommended" or "Offered" or "DispatchWaiting" => "배송 제안", "Accepted" => "픽업 대기", "PickedUp" => "전달 중",
        "Delivered" or "Completed" => "전달 완료", "Rejected" => "제안 거절", _ => "배송 상태 확인"
    };
}
