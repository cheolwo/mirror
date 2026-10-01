using DriverApp.Services;
using Hongdal.Contracts.Driver.Development;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사개발도구기능ViewModel : 조립ViewModelBase
{
    public 기사개발도구기능ViewModel(IDriverDevelopmentApiService api)
    {
        스냅샷조회 = 하위ViewModel등록(new Api작업ViewModel<기사개발스냅샷응답?>(api.스냅샷조회Async));
    }

    public Api작업ViewModel<기사개발스냅샷응답?> 스냅샷조회 { get; }
}
