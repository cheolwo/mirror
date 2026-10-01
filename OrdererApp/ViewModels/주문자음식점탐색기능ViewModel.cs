using Hongdal.Ui.Common.Areas.App.ViewModels;
using OrdererApp.Services;

namespace OrdererApp.ViewModels;

public sealed class 주문자음식점탐색기능ViewModel : 조립ViewModelBase
{
    public 주문자음식점탐색기능ViewModel(IRestaurantSearchPolicyService service)
    {
        탐색정책조회 = 하위ViewModel등록(
            new Api작업ViewModel<RestaurantSearchPolicy>(service.GetPolicyAsync));
    }

    public Api작업ViewModel<RestaurantSearchPolicy> 탐색정책조회 { get; }
}
