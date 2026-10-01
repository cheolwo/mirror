using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace HongdalApp.ViewModels.Shipper;

/// <summary>
/// 화주 페이지가 필요한 하위 기능만 고르거나, 이 모음을 한 번에 주입해 조립할 수 있습니다.
/// </summary>
public sealed class 화주Api기능모음ViewModel : 조립ViewModelBase
{
    public 화주Api기능모음ViewModel(
        화주운송의뢰기능ViewModel 운송의뢰,
        화주창고기능ViewModel 창고,
        화주판매기능ViewModel 판매,
        화주Controller기능모음ViewModel 화주Controllers,
        공통Controller기능모음ViewModel 공통Controllers)
    {
        this.운송의뢰 = 하위ViewModel등록(운송의뢰);
        this.창고 = 하위ViewModel등록(창고);
        this.판매 = 하위ViewModel등록(판매);
        this.화주Controllers = 하위ViewModel등록(화주Controllers);
        this.공통Controllers = 하위ViewModel등록(공통Controllers);
    }

    public 화주운송의뢰기능ViewModel 운송의뢰 { get; }
    public 화주창고기능ViewModel 창고 { get; }
    public 화주판매기능ViewModel 판매 { get; }
    public 화주Controller기능모음ViewModel 화주Controllers { get; }
    public 공통Controller기능모음ViewModel 공통Controllers { get; }
}
