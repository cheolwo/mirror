using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using HongdalApp.Services;

namespace HongdalApp.ViewModels.Shipper;

public sealed class 화주창고기능ViewModel : 조립ViewModelBase
{
    public 화주창고기능ViewModel(
        IShipperOperationsService operations,
        입출고화면ViewModel 기본입출고)
    {
        this.기본입출고 = 하위ViewModel등록(기본입출고);
        창고목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<창고요약응답>>(operations.GetWarehousesAsync),
            수명소유: true);
        입고목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<입고요청항목응답>>(operations.GetInboundsAsync),
            수명소유: true);
        재고목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<재고항목응답>>(operations.GetInventoryAsync),
            수명소유: true);
    }

    public 입출고화면ViewModel 기본입출고 { get; }
    public Api작업ViewModel<IReadOnlyList<창고요약응답>> 창고목록조회 { get; }
    public Api작업ViewModel<IReadOnlyList<입고요청항목응답>> 입고목록조회 { get; }
    public Api작업ViewModel<IReadOnlyList<재고항목응답>> 재고목록조회 { get; }
}
