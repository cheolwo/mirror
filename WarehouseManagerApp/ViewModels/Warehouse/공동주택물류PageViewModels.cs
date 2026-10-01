using Hongdal.Ui.Common.Areas.App.ViewModels;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public sealed class 공동주택반입예정PageViewModel : 창고PageViewModelBase
{
    public 공동주택반입예정PageViewModel(
        창고작업세션상태ViewModel 세션,
        공동수입원장물류ViewModel 원장물류,
        입고예정조회ViewModel 입고예정조회)
        : base(
            세션,
            창고PageCodes.공동주택반입예정,
            "공동주택 반입 예정",
            창고운영ProfileCodes.공동주택물류)
    {
        this.원장물류 = 구성요소등록(원장물류);
        this.입고예정조회 = 구성요소등록(입고예정조회);
    }

    public 공동수입원장물류ViewModel 원장물류 { get; }
    public 입고예정조회ViewModel 입고예정조회 { get; }
    public bool 처리중 => 원장물류.처리중 || 입고예정조회.처리중;

    public Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => 입고예정조회.조회Async(new 목록조회요청(), cancellationToken);
}

public sealed class 공동주택입고확인PageViewModel : 창고PageViewModelBase
{
    public 공동주택입고확인PageViewModel(
        창고작업세션상태ViewModel 세션,
        입고ViewModel 입고,
        IInboundReceivingWorkflowService 입고Workflow)
        : base(
            세션,
            창고PageCodes.공동주택입고확인,
            "공동주택 입고 확인",
            창고운영ProfileCodes.공동주택물류)
    {
        this.입고 = 구성요소등록(입고);
        this.입고Workflow = 입고Workflow;
    }

    public 입고ViewModel 입고 { get; }
    public IInboundReceivingWorkflowService 입고Workflow { get; }
    public bool 처리중 => 입고.처리중;

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        var inboundLoaded = await 입고.목록조회Async(cancellationToken);
        var inventoryLoaded = await 입고.재고목록조회Async(cancellationToken);
        return inboundLoaded && inventoryLoaded;
    }
}

public sealed class 공동주택세대배분PageViewModel : 창고PageViewModelBase
{
    public 공동주택세대배분PageViewModel(
        창고작업세션상태ViewModel 세션,
        공동구매재고배분ViewModel 재고배분,
        출고재고조회ViewModel 재고조회)
        : base(
            세션,
            창고PageCodes.공동주택세대배분,
            "공동주택 세대별 배분",
            창고운영ProfileCodes.공동주택물류)
    {
        this.재고배분 = 구성요소등록(재고배분);
        this.재고조회 = 구성요소등록(재고조회);
    }

    public 공동구매주문집계ViewModel 주문집계 => 재고배분.주문집계;
    public 공동구매재고배분ViewModel 재고배분 { get; }
    public 출고재고조회ViewModel 재고조회 { get; }
    public bool 처리중 => 재고조회.처리중;

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        var inventoryLoaded = await 재고조회.조회Async(cancellationToken);
        재고배분.초안재구성();
        return inventoryLoaded;
    }
}

public sealed class 공동주택수령인계PageViewModel : 창고PageViewModelBase
{
    public 공동주택수령인계PageViewModel(
        창고작업세션상태ViewModel 세션,
        공동구매재고배분ViewModel 재고배분)
        : base(
            세션,
            창고PageCodes.공동주택수령인계,
            "공동주택 입주민 수령 인계",
            창고운영ProfileCodes.공동주택물류)
    {
        this.재고배분 = 구성요소등록(재고배분);
    }

    public 공동구매주문집계ViewModel 주문집계 => 재고배분.주문집계;
    public 공동구매재고배분ViewModel 재고배분 { get; }
}

public sealed class 공동주택미수령관리PageViewModel : 창고PageViewModelBase
{
    public 공동주택미수령관리PageViewModel(
        창고작업세션상태ViewModel 세션,
        공동구매재고배분ViewModel 재고배분)
        : base(
            세션,
            창고PageCodes.공동주택미수령관리,
            "공동주택 미수령 관리",
            창고운영ProfileCodes.공동주택물류)
    {
        this.재고배분 = 구성요소등록(재고배분);
    }

    public 공동구매주문집계ViewModel 주문집계 => 재고배분.주문집계;
    public 공동구매재고배분ViewModel 재고배분 { get; }
}
