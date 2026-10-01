using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public sealed class 수입화물반입PageViewModel : 창고PageViewModelBase
{
    public 수입화물반입PageViewModel(
        창고작업세션상태ViewModel 세션,
        공동수입원장물류ViewModel 원장물류,
        입고ViewModel 입고,
        입고예정조회ViewModel 입고예정조회)
        : base(
            세션,
            창고PageCodes.수입화물반입,
            "수입 화물 반입",
            창고운영ProfileCodes.보세수입)
    {
        this.원장물류 = 구성요소등록(원장물류);
        this.입고 = 구성요소등록(입고);
        this.입고예정조회 = 구성요소등록(입고예정조회);
    }

    public 공동수입원장물류ViewModel 원장물류 { get; }
    public 입고ViewModel 입고 { get; }
    public 입고예정조회ViewModel 입고예정조회 { get; }
    public bool 처리중 => 원장물류.처리중 || 입고.처리중 || 입고예정조회.처리중;

    public Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => 입고.목록조회Async(cancellationToken);
}

public sealed class 보세통관상태PageViewModel : 창고PageViewModelBase
{
    public 보세통관상태PageViewModel(
        창고작업세션상태ViewModel 세션,
        공동수입원장물류ViewModel 원장물류,
        공동수입선적통관ViewModel 선적통관)
        : base(
            세션,
            창고PageCodes.보세통관상태,
            "보세·통관 상태",
            창고운영ProfileCodes.보세수입)
    {
        this.원장물류 = 구성요소등록(원장물류);
        this.선적통관 = 구성요소등록(선적통관);
        this.선적통관.원장물류연결(this.원장물류);
    }

    public 공동수입원장물류ViewModel 원장물류 { get; }
    public 공동수입선적통관ViewModel 선적통관 { get; }
    public bool 처리중 => 원장물류.처리중 || 선적통관.처리중;
}

public sealed class 수입화물반출PageViewModel : 창고PageViewModelBase
{
    public 수입화물반출PageViewModel(
        창고작업세션상태ViewModel 세션,
        공동수입원장물류ViewModel 원장물류,
        출고재고조회ViewModel 재고조회,
        출고운송인계ViewModel 운송인계)
        : base(
            세션,
            창고PageCodes.수입화물반출,
            "수입 화물 반출",
            창고운영ProfileCodes.보세수입)
    {
        this.원장물류 = 구성요소등록(원장물류);
        this.재고조회 = 구성요소등록(재고조회);
        this.운송인계 = 구성요소등록(운송인계);
    }

    public 공동수입원장물류ViewModel 원장물류 { get; }
    public 출고재고조회ViewModel 재고조회 { get; }
    public 출고운송인계ViewModel 운송인계 { get; }
    public bool 처리중 => 원장물류.처리중 || 재고조회.처리중 || 운송인계.처리중;

    public Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => 재고조회.조회Async(cancellationToken);
}

public sealed class 수입국내운송인계PageViewModel : 창고PageViewModelBase
{
    public 수입국내운송인계PageViewModel(
        창고작업세션상태ViewModel 세션,
        공동수입원장물류ViewModel 원장물류,
        출고재고조회ViewModel 재고조회,
        출고운송인계ViewModel 운송인계)
        : base(
            세션,
            창고PageCodes.수입국내운송인계,
            "수입 국내 운송 인계",
            창고운영ProfileCodes.보세수입)
    {
        this.원장물류 = 구성요소등록(원장물류);
        this.재고조회 = 구성요소등록(재고조회);
        this.운송인계 = 구성요소등록(운송인계);
    }

    public 공동수입원장물류ViewModel 원장물류 { get; }
    public 출고재고조회ViewModel 재고조회 { get; }
    public 출고운송인계ViewModel 운송인계 { get; }
    public bool 처리중 => 원장물류.처리중 || 재고조회.처리중 || 운송인계.처리중;

    public Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => 재고조회.조회Async(cancellationToken);
}
