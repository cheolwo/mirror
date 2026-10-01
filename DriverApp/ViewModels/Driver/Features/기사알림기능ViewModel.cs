using DriverApp.Services;
using Hongdal.Contracts.Driver.Notification;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사알림기능ViewModel : 조립ViewModelBase
{
    public 기사알림기능ViewModel(IDriverNotificationApiService api)
    {
        푸시토큰조회 = 하위ViewModel등록(new Api작업ViewModel<기사푸시토큰응답?>(api.푸시토큰조회Async));
        푸시토큰등록 = 하위ViewModel등록(
            new Api작업ViewModel<기사푸시토큰등록요청, 기사푸시토큰응답?>(api.푸시토큰등록Async));
        푸시토큰삭제 = 하위ViewModel등록(
            new Api작업ViewModel<Api작업완료>(async cancellationToken =>
            {
                await api.푸시토큰삭제Async(cancellationToken);
                return Api작업완료.값;
            }));
        설정조회 = 하위ViewModel등록(new Api작업ViewModel<기사알림설정응답?>(api.설정조회Async));
        설정수정 = 하위ViewModel등록(
            new Api작업ViewModel<기사알림설정수정요청, 기사알림설정응답?>(api.설정수정Async));
    }

    public Api작업ViewModel<기사푸시토큰응답?> 푸시토큰조회 { get; }
    public Api작업ViewModel<기사푸시토큰등록요청, 기사푸시토큰응답?> 푸시토큰등록 { get; }
    public Api작업ViewModel<Api작업완료> 푸시토큰삭제 { get; }
    public Api작업ViewModel<기사알림설정응답?> 설정조회 { get; }
    public Api작업ViewModel<기사알림설정수정요청, 기사알림설정응답?> 설정수정 { get; }
}
