using DriverApp.Services;
using Hongdal.Contracts.Driver.Reservation;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사예약기능ViewModel : 조립ViewModelBase
{
    public 기사예약기능ViewModel(IDriverReservationApiService api)
    {
        목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<기사예약목록응답>>(api.목록조회Async));
        생성 = 하위ViewModel등록(
            new Api작업ViewModel<기사예약요청, 기사예약응답?>(api.생성Async));
        취소 = 하위ViewModel등록(
            new Api작업ViewModel<long, 기사예약취소응답?>(api.취소Async));
        상세조회 = 하위ViewModel등록(
            new Api작업ViewModel<long, 기사예약응답?>(api.상세조회Async));
    }

    public Api작업ViewModel<IReadOnlyList<기사예약목록응답>> 목록조회 { get; }
    public Api작업ViewModel<기사예약요청, 기사예약응답?> 생성 { get; }
    public Api작업ViewModel<long, 기사예약취소응답?> 취소 { get; }
    public Api작업ViewModel<long, 기사예약응답?> 상세조회 { get; }
}
