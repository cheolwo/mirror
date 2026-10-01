using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace OrdererApp.ViewModels;

public sealed class 주문자Api기능모음ViewModel : 조립ViewModelBase
{
    public 주문자Api기능모음ViewModel(
        주문자공동구매기능ViewModel 공동구매,
        주문자음식점탐색기능ViewModel 음식점탐색,
        주문자Controller기능모음ViewModel 주문자Controllers,
        음식Controller기능모음ViewModel 음식Controllers,
        공통Controller기능모음ViewModel 공통Controllers)
    {
        this.공동구매 = 하위ViewModel등록(공동구매);
        this.음식점탐색 = 하위ViewModel등록(음식점탐색);
        this.주문자Controllers = 하위ViewModel등록(주문자Controllers);
        this.음식Controllers = 하위ViewModel등록(음식Controllers);
        this.공통Controllers = 하위ViewModel등록(공통Controllers);
    }

    public 주문자공동구매기능ViewModel 공동구매 { get; }
    public 주문자음식점탐색기능ViewModel 음식점탐색 { get; }
    public 주문자Controller기능모음ViewModel 주문자Controllers { get; }
    public 음식Controller기능모음ViewModel 음식Controllers { get; }
    public 공통Controller기능모음ViewModel 공통Controllers { get; }
}
