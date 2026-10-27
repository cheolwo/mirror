using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>기존 주문 상세 안의 명시적 협의 입력과 같은 요청 결과 확인만 소유합니다.</summary>
public sealed class 음식주문취소합의ViewModel(I음식주문취소합의Client client) : IDisposable
{
    private CancellationTokenSource _lifetime = new();
    private long _generation;
    private Pending? _pending;
    private sealed record Pending(string OrderNo, string AgreementId, Guid RequestId);
    public string OrderNo { get; private set; } = "";
    public long OrderRevision { get; private set; }
    public decimal? OrdererCostKrw { get; set; }
    public decimal? RestaurantCostKrw { get; set; }
    public string CostBasisReference { get; set; } = "";
    public bool ApplyConfirmed { get; set; }
    public bool IsBusy { get; private set; }
    public bool HasLoaded { get; private set; }
    public bool HasPending => _pending is not null;
    public string? Error { get; private set; }
    public IReadOnlyList<음식주문취소합의응답> Agreements { get; private set; } = [];
    public 음식주문취소합의응답? Current { get; private set; }
    public bool CanPropose => !IsBusy && !HasPending && HasLoaded && OrderRevision > 0 && Current?.StateCode != "Cancelled"
        && OrdererCostKrw is >= 0 && RestaurantCostKrw is >= 0 && !string.IsNullOrWhiteSpace(CostBasisReference);
    public bool CanConsent => !IsBusy && !HasPending && HasLoaded && Current is { CanConsent: true } value && value.FrozenOrderRevision == OrderRevision;
    public bool CanApply => CanConsent && Current?.CanApply == true && ApplyConfirmed;
    public void SetOrder(string orderNo, long revision)
    {
        if (OrderNo == orderNo) { if (OrderRevision != revision) ApplyConfirmed = false; OrderRevision = revision; return; }
        _lifetime.Cancel(); _lifetime.Dispose(); _lifetime = new(); _generation++;
        OrderNo = orderNo; OrderRevision = revision; Agreements = []; Current = null;
        HasLoaded = IsBusy = ApplyConfirmed = false; _pending = null; Error = null;
        OrdererCostKrw = RestaurantCostKrw = null; CostBasisReference = "";
    }
    public void Select(음식주문취소합의응답 value) { if (!IsBusy && !HasPending) { Current = value; ApplyConfirmed = false; } }
    public async Task LoadAsync()
    {
        if (IsBusy || HasPending) return;
        var generation = _generation; IsBusy = true; Error = null;
        try
        {
            var rows = await client.목록Async(OrderNo, _lifetime.Token);
            if (generation != _generation) return;
            Agreements = rows; Current = rows.FirstOrDefault(); HasLoaded = true; ApplyConfirmed = false;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch { if (generation == _generation) { HasLoaded = false; Error = "취소 조건을 확인하지 못했습니다. 다시 조회해 주세요."; } }
        finally { if (generation == _generation) IsBusy = false; }
    }
    public Task ProposeAsync()
    {
        if (!CanPropose) return Task.CompletedTask;
        var request = new 음식주문취소합의제안요청 { ClientRequestId = Guid.NewGuid(), ExpectedOrderRevision = OrderRevision,
            OrdererCostKrw = OrdererCostKrw, RestaurantCostKrw = RestaurantCostKrw, CostBasisReference = CostBasisReference.Trim() };
        return SendAsync(new(OrderNo, request.ClientRequestId.ToString("N"), request.ClientRequestId), token => client.제안Async(OrderNo, request, token));
    }
    public Task ConsentAsync(bool granted)
    {
        if (!CanConsent || Current is not { } current) return Task.CompletedTask;
        var request = Command(current); request.Granted = granted;
        return SendAsync(new(OrderNo, current.AgreementId, request.ClientRequestId), token => client.동의Async(OrderNo, current.AgreementId, request, token));
    }
    public Task ApplyAsync()
    {
        if (!CanApply || Current is not { } current) return Task.CompletedTask;
        var request = Command(current);
        return SendAsync(new(OrderNo, current.AgreementId, request.ClientRequestId), token => client.실행Async(OrderNo, current.AgreementId, request, token));
    }
    private 음식주문취소합의변경요청 Command(음식주문취소합의응답 value) => new()
    { ClientRequestId = Guid.NewGuid(), ExpectedAgreementRevision = value.Revision, ExpectedOrderRevision = OrderRevision, Granted = true };
    private async Task SendAsync(Pending pending, Func<CancellationToken, Task<음식주문취소합의응답>> send)
    {
        var generation = _generation; IsBusy = true; _pending = pending; Error = null;
        try
        {
            var result = await send(_lifetime.Token);
            if (generation != _generation) return;
            AcceptReceipt(pending, result);
        }
        catch (SsalddelApiException ex) when (ex.StatusCode is 400 or 401 or 403 or 404 or 409)
        {
            if (generation == _generation) { _pending = null; HasLoaded = false; ApplyConfirmed = false; Error = "주문 또는 협의가 변경되었거나 처리할 수 없습니다. 최신 상태와 권한을 다시 확인해 주세요."; }
        }
        catch { if (generation == _generation) Error = "처리 결과가 아직 확인되지 않았습니다. 같은 요청의 결과부터 확인해 주세요."; }
        finally { if (generation == _generation) IsBusy = false; }
    }
    public async Task CheckAsync()
    {
        if (IsBusy || _pending is not { } pending) return;
        var generation = _generation; IsBusy = true; Error = null;
        try
        {
            var result = await client.조회Async(pending.OrderNo, pending.AgreementId, pending.RequestId, _lifetime.Token);
            if (generation != _generation) return;
            if (result is null) Error = "이 요청의 결과가 아직 확인되지 않습니다. 새 요청을 보내지 말고 다시 확인하거나 지원 담당자에게 문의해 주세요.";
            else AcceptReceipt(pending, result);
        }
        catch { if (generation == _generation) Error = "요청 결과를 확인하지 못했습니다. 같은 요청을 다시 조회해 주세요."; }
        finally { if (generation == _generation) IsBusy = false; }
    }
    private void AcceptReceipt(Pending pending, 음식주문취소합의응답 result)
    {
        if (result.OrderNo != pending.OrderNo || result.AgreementId != pending.AgreementId || result.ConfirmedClientRequestId != pending.RequestId)
            throw new InvalidDataException("취소 합의 요청 결과가 일치하지 않습니다.");
        Current = result; Agreements = Agreements.Where(x => x.AgreementId != result.AgreementId).Prepend(result).ToArray();
        _pending = null; HasLoaded = true; ApplyConfirmed = false;
    }
    public void Dispose() { _generation++; _lifetime.Cancel(); _lifetime.Dispose(); }
}
