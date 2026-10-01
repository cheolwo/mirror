using DriverApp.Services;
using Hongdal.Contracts.Driver.Settlement;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사정산기능ViewModel : 조립ViewModelBase
{
    public 기사정산기능ViewModel(IDriverSettlementApiService api)
    {
        목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<기사정산월요약응답>>(api.목록조회Async));
        월별조회 = 하위ViewModel등록(
            new Api작업ViewModel<기사정산월조건, 기사정산응답?>(
                (condition, cancellationToken) => api.월별조회Async(
                    condition.연도,
                    condition.월,
                    cancellationToken)));
        현재월조회 = 하위ViewModel등록(new Api작업ViewModel<기사정산응답?>(api.현재월조회Async));
    }

    public Api작업ViewModel<IReadOnlyList<기사정산월요약응답>> 목록조회 { get; }
    public Api작업ViewModel<기사정산월조건, 기사정산응답?> 월별조회 { get; }
    public Api작업ViewModel<기사정산응답?> 현재월조회 { get; }
}

public sealed record 기사정산월조건(int 연도, int 월);
