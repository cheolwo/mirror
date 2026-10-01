using DriverApp.Services;
using Hongdal.Contracts.Common.Exploration;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사탐색캠페인기능ViewModel : 조립ViewModelBase
{
    public 기사탐색캠페인기능ViewModel(IDriverExplorationCampaignApiService api)
    {
        목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<탐색캠페인목록항목응답>>(api.목록조회Async));
        생성 = 하위ViewModel등록(
            new Api작업ViewModel<탐색캠페인생성요청, 탐색캠페인응답?>(api.생성Async));
        상세조회 = 하위ViewModel등록(
            new Api작업ViewModel<long, 탐색캠페인상세응답?>(api.상세조회Async));
        추천대상조회 = 하위ViewModel등록(
            new Api작업ViewModel<long, IReadOnlyList<탐색캠페인추천대상응답>>(api.추천대상조회Async));
        발송 = 하위ViewModel등록(
            new Api작업ViewModel<기사탐색캠페인발송조건, 탐색캠페인상세응답?>(
                (condition, cancellationToken) => api.발송Async(
                    condition.캠페인Id,
                    condition.요청,
                    cancellationToken)));
    }

    public Api작업ViewModel<IReadOnlyList<탐색캠페인목록항목응답>> 목록조회 { get; }
    public Api작업ViewModel<탐색캠페인생성요청, 탐색캠페인응답?> 생성 { get; }
    public Api작업ViewModel<long, 탐색캠페인상세응답?> 상세조회 { get; }
    public Api작업ViewModel<long, IReadOnlyList<탐색캠페인추천대상응답>> 추천대상조회 { get; }
    public Api작업ViewModel<기사탐색캠페인발송조건, 탐색캠페인상세응답?> 발송 { get; }
}

public sealed record 기사탐색캠페인발송조건(long 캠페인Id, 탐색캠페인발송요청 요청);
