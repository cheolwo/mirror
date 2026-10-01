using DriverApp.Services;
using Hongdal.Contracts.Driver.Home;
using Hongdal.Contracts.Driver.Profile;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사프로필기능ViewModel : 조립ViewModelBase
{
    public 기사프로필기능ViewModel(IDriverProfileApiService api)
    {
        홈조회 = 하위ViewModel등록(new Api작업ViewModel<기사홈요약응답?>(api.홈조회Async));
        내프로필조회 = 하위ViewModel등록(new Api작업ViewModel<용달기사등록응답?>(api.내프로필조회Async));
        프로필등록 = 하위ViewModel등록(
            new Api작업ViewModel<용달기사등록요청, 용달기사등록응답?>(api.등록Async));
    }

    public Api작업ViewModel<기사홈요약응답?> 홈조회 { get; }
    public Api작업ViewModel<용달기사등록응답?> 내프로필조회 { get; }
    public Api작업ViewModel<용달기사등록요청, 용달기사등록응답?> 프로필등록 { get; }
}
