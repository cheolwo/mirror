using DriverApp.Services;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Driver.Work;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사근무기능ViewModel : 조립ViewModelBase
{
    public 기사근무기능ViewModel(IDriverWorkApiService api)
    {
        운행상태조회 = 하위ViewModel등록(new Api작업ViewModel<기사운행상태응답?>(api.운행상태조회Async));
        현재근무조회 = 하위ViewModel등록(new Api작업ViewModel<기사현재근무응답?>(api.현재근무조회Async));
        근무목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<기사근무요약응답>>(api.근무목록조회Async));
        근무상세조회 = 하위ViewModel등록(
            new Api작업ViewModel<long, 기사근무요약응답?>(api.근무상세조회Async));
        기사별근무상세조회 = 하위ViewModel등록(
            new Api작업ViewModel<기사별근무상세조회조건, 기사근무요약응답?>(
                (condition, cancellationToken) => api.기사별근무상세조회Async(
                    condition.기사Id,
                    condition.근무Id,
                    cancellationToken)));
        운행시작 = 하위ViewModel등록(
            new Api작업ViewModel<기사운행시작요청, 기사운행시작응답?>(api.운행시작Async));
        운행종료 = 하위ViewModel등록(
            new Api작업ViewModel<Api작업완료>(async cancellationToken =>
            {
                await api.운행종료Async(cancellationToken);
                return Api작업완료.값;
            }));
        위치갱신 = 하위ViewModel등록(
            new Api작업ViewModel<기사위치갱신요청, 기사위치갱신응답?>(api.위치갱신Async));
        커뮤니티의뢰목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<CommunityDriverInquiryResponse>>(api.커뮤니티의뢰목록Async));
        커뮤니티의뢰답변 = 하위ViewModel등록(
            new Api작업ViewModel<기사커뮤니티의뢰답변조건, CommunityDriverInquiryResponse?>(
                (condition, cancellationToken) => api.커뮤니티의뢰답변Async(
                    condition.문의Id,
                    condition.요청,
                    cancellationToken)));
    }

    public Api작업ViewModel<기사운행상태응답?> 운행상태조회 { get; }
    public Api작업ViewModel<기사현재근무응답?> 현재근무조회 { get; }
    public Api작업ViewModel<IReadOnlyList<기사근무요약응답>> 근무목록조회 { get; }
    public Api작업ViewModel<long, 기사근무요약응답?> 근무상세조회 { get; }
    public Api작업ViewModel<기사별근무상세조회조건, 기사근무요약응답?> 기사별근무상세조회 { get; }
    public Api작업ViewModel<기사운행시작요청, 기사운행시작응답?> 운행시작 { get; }
    public Api작업ViewModel<Api작업완료> 운행종료 { get; }
    public Api작업ViewModel<기사위치갱신요청, 기사위치갱신응답?> 위치갱신 { get; }
    public Api작업ViewModel<IReadOnlyList<CommunityDriverInquiryResponse>> 커뮤니티의뢰목록조회 { get; }
    public Api작업ViewModel<기사커뮤니티의뢰답변조건, CommunityDriverInquiryResponse?> 커뮤니티의뢰답변 { get; }
}

public sealed record 기사커뮤니티의뢰답변조건(
    Guid 문의Id,
    CommunityDriverInquiryDecisionRequest 요청);

public sealed record 기사별근무상세조회조건(string 기사Id, long 근무Id);
