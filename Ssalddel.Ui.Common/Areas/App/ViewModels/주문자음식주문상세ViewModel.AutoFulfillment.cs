using Ssalddel.Contracts.Common.Fulfillment;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed partial class 주문자음식주문상세ViewModel
{
    private AutoFulfillmentQueryViewModel? _autoRouting;
    public AutoFulfillmentQueryViewModel AutoRouting => _autoRouting ??= CreateAutoRouting();
    private AutoFulfillmentQueryViewModel CreateAutoRouting()
    {
        var model = new AutoFulfillmentQueryViewModel(service.자동동의Async, service.자동진행확인Async, service.자동수행취소Async);
        model.PropertyChanged += (_, _) => OnPropertyChanged(string.Empty);
        model.Conditions.PropertyChanged += (_, _) => OnPropertyChanged(string.Empty);
        return model;
    }
    private void 자동배송결속()
    {
        if (상세 is { } value) AutoRouting.Bind(FulfillmentSourceKinds.FoodOrder, value.주문.주문번호, _selectionGeneration.ToString(), value.AutoFulfillment);
        else _autoRouting?.Clear();
    }
    public Task 자동배송확인Async() => 자동배송작업Async(() => AutoRouting.RefreshAsync());
    public Task 자동배송취소Async() => 자동배송작업Async(() => AutoRouting.CancelAsync());
    public Task 자동배송동의철회Async() => 자동배송작업Async(() => AutoRouting.RevokeAsync());
    public Task 자동배송동의갱신Async() => 자동배송작업Async(() => AutoRouting.RenewAsync());
    private async Task 자동배송작업Async(Func<Task> action)
    {
        if (처리중 || AutoRouting.IsSending || 상세?.AutoFulfillment is null || 요청OrderNo is null) return;
        var generation = _selectionGeneration; var order = 요청OrderNo;
        await action();
        if (generation != _selectionGeneration || order != 요청OrderNo) return;
        if (상세 is not null) 상세.AutoFulfillment = AutoRouting.Value;
        if (!AutoRouting.HasPending) await 조회Async(order);
        OnPropertyChanged(string.Empty);
    }
}
