using DriverApp.Services;
using Hongdal.Contracts.Driver.Settings;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사설정기능ViewModel : 조립ViewModelBase
{
    public 기사설정기능ViewModel(IDriverSettingsApiService api)
    {
        콜범위조회 = 하위ViewModel등록(new Api작업ViewModel<기사콜범위응답?>(api.콜범위조회Async));
        콜범위수정 = 하위ViewModel등록(
            new Api작업ViewModel<기사콜범위수정요청, 기사콜범위응답?>(api.콜범위수정Async));
    }

    public Api작업ViewModel<기사콜범위응답?> 콜범위조회 { get; }
    public Api작업ViewModel<기사콜범위수정요청, 기사콜범위응답?> 콜범위수정 { get; }
}
