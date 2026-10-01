using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using OrdererApp.Services;

namespace OrdererApp.ViewModels;

public sealed class 주문자공동구매기능ViewModel : 조립ViewModelBase
{
    public 주문자공동구매기능ViewModel(
        IGroupPurchaseShipmentTrackingService service,
        공동구매화면ViewModel 업무흐름)
    {
        this.업무흐름 = 하위ViewModel등록(업무흐름);
        해외선적조회 = 하위ViewModel등록(
            new Api작업ViewModel<string, 공동구매해외선적공개Dto?>(service.LookupAsync));
        수입단가시뮬레이션 = 하위ViewModel등록(
            new Api작업ViewModel<HsCountryMonthlyTradeUnitPriceRequest, HsCountryImportUnitPriceSimulationResult?>(
                service.SimulateImportUnitPriceAsync));
        수요등록 = 하위ViewModel등록(
            new Api작업ViewModel<공동구매자동수요등록Command, 공동구매자동집단응답?>(service.RegisterDemandAsync));
    }

    public 공동구매화면ViewModel 업무흐름 { get; }
    public Api작업ViewModel<string, 공동구매해외선적공개Dto?> 해외선적조회 { get; }
    public Api작업ViewModel<HsCountryMonthlyTradeUnitPriceRequest, HsCountryImportUnitPriceSimulationResult?> 수입단가시뮬레이션 { get; }
    public Api작업ViewModel<공동구매자동수요등록Command, 공동구매자동집단응답?> 수요등록 { get; }
}
