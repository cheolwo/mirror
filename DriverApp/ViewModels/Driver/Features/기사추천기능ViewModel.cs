using DriverApp.Services;
using Hongdal.Contracts.Driver.Recommendation;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사추천기능ViewModel : 조립ViewModelBase
{
    public 기사추천기능ViewModel(IDriverRecommendationApiService api)
    {
        전체조회 = 하위ViewModel등록(CreateListOperation(api.전체조회Async));
        비운행중조회 = 하위ViewModel등록(CreateListOperation(api.비운행중조회Async));
        운행중조회 = 하위ViewModel등록(CreateListOperation(api.운행중조회Async));
        전국콜조회 = 하위ViewModel등록(CreateListOperation(api.전국콜조회Async));
        공개배차조회 = 하위ViewModel등록(CreateListOperation(api.공개배차조회Async));
        위치검색 = 하위ViewModel등록(
            new Api작업ViewModel<기사추천위치검색조건, IReadOnlyList<기사배차추천항목응답>>(
                (condition, cancellationToken) => api.위치검색Async(
                    condition.위도,
                    condition.경도,
                    condition.반경Km,
                    cancellationToken)));
        요약조회 = 하위ViewModel등록(new Api작업ViewModel<기사배차추천요약응답?>(api.요약조회Async));
        운송의뢰상세조회 = 하위ViewModel등록(
            new Api작업ViewModel<string, 기사운송의뢰상세응답?>(api.운송의뢰상세조회Async));
    }

    public Api작업ViewModel<IReadOnlyList<기사배차추천항목응답>> 전체조회 { get; }
    public Api작업ViewModel<IReadOnlyList<기사배차추천항목응답>> 비운행중조회 { get; }
    public Api작업ViewModel<IReadOnlyList<기사배차추천항목응답>> 운행중조회 { get; }
    public Api작업ViewModel<기사추천위치검색조건, IReadOnlyList<기사배차추천항목응답>> 위치검색 { get; }
    public Api작업ViewModel<IReadOnlyList<기사배차추천항목응답>> 전국콜조회 { get; }
    public Api작업ViewModel<IReadOnlyList<기사배차추천항목응답>> 공개배차조회 { get; }
    public Api작업ViewModel<기사배차추천요약응답?> 요약조회 { get; }
    public Api작업ViewModel<string, 기사운송의뢰상세응답?> 운송의뢰상세조회 { get; }

    private static Api작업ViewModel<IReadOnlyList<기사배차추천항목응답>> CreateListOperation(
        Func<CancellationToken, Task<IReadOnlyList<기사배차추천항목응답>>> operation)
        => new(operation);
}

public sealed record 기사추천위치검색조건(decimal 위도, decimal 경도, decimal 반경Km);
