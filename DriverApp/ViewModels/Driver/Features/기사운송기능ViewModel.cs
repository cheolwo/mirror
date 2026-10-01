using DriverApp.Services;
using Hongdal.Contracts.Driver.Transport;

namespace DriverApp.ViewModels.Driver.Features;

public abstract class 기사운송업무ViewModelBase(
    string 업무코드,
    string 업무명) : 조립ViewModelBase
{
    public string 업무코드 { get; } = 업무코드;
    public string 업무명 { get; } = 업무명;
}

/// <summary>배정된 운송의 목록·현재 운송·상세를 조회하는 기본 업무입니다.</summary>
public sealed class 기사운송조회ViewModel : 기사운송업무ViewModelBase
{
    public 기사운송조회ViewModel(IDriverTransportApiService api)
        : base("driver-transport-query", "운송 조회")
    {
        목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<기사운송요약응답>>(api.목록조회Async));
        현재조회 = 하위ViewModel등록(new Api작업ViewModel<기사운송요약응답?>(api.현재조회Async));
        상세조회 = 하위ViewModel등록(
            new Api작업ViewModel<long, 기사운송상세응답?>(api.상세조회Async));
    }

    public Api작업ViewModel<IReadOnlyList<기사운송요약응답>> 목록조회 { get; }
    public Api작업ViewModel<기사운송요약응답?> 현재조회 { get; }
    public Api작업ViewModel<long, 기사운송상세응답?> 상세조회 { get; }
}

/// <summary>상차지 도착과 상차 완료를 담당하는 기본 업무입니다.</summary>
public sealed class 기사상차업무ViewModel : 기사운송업무ViewModelBase
{
    public 기사상차업무ViewModel(IDriverTransportApiService api)
        : base("driver-loading", "상차")
    {
        상차지도착 = 하위ViewModel등록(
            new Api작업ViewModel<long, 기사운송상태변경응답?>(api.상차지도착Async));
        상차완료 = 하위ViewModel등록(
            new Api작업ViewModel<기사운송상차완료조건, 기사운송상태변경응답?>(
                (condition, cancellationToken) => api.상차완료Async(
                    condition.운송Id,
                    condition.요청,
                    cancellationToken)));
    }

    public Api작업ViewModel<long, 기사운송상태변경응답?> 상차지도착 { get; }
    public Api작업ViewModel<기사운송상차완료조건, 기사운송상태변경응답?> 상차완료 { get; }
}

/// <summary>하차지 도착과 하차 완료를 담당하는 기본 업무입니다.</summary>
public sealed class 기사하차업무ViewModel : 기사운송업무ViewModelBase
{
    public 기사하차업무ViewModel(IDriverTransportApiService api)
        : base("driver-unloading", "하차")
    {
        하차지도착 = 하위ViewModel등록(
            new Api작업ViewModel<long, 기사운송상태변경응답?>(api.하차지도착Async));
        하차완료 = 하위ViewModel등록(
            new Api작업ViewModel<기사운송하차완료조건, 기사운송상태변경응답?>(
                (condition, cancellationToken) => api.하차완료Async(
                    condition.운송Id,
                    condition.요청,
                    cancellationToken)));
    }

    public Api작업ViewModel<long, 기사운송상태변경응답?> 하차지도착 { get; }
    public Api작업ViewModel<기사운송하차완료조건, 기사운송상태변경응답?> 하차완료 { get; }
}

/// <summary>운송 중 예외와 문제 신고를 담당하는 기본 업무입니다.</summary>
public sealed class 기사운송예외업무ViewModel : 기사운송업무ViewModelBase
{
    public 기사운송예외업무ViewModel(IDriverTransportApiService api)
        : base("driver-transport-exception", "운송 예외")
    {
        예외신고 = 하위ViewModel등록(
            new Api작업ViewModel<기사운송예외신고조건, 기사운송요약응답?>(
                (condition, cancellationToken) => api.예외신고Async(
                    condition.운송Id,
                    condition.요청,
                    cancellationToken)));
        문제신고 = 하위ViewModel등록(
            new Api작업ViewModel<기사운송예외신고조건, 기사운송요약응답?>(
                (condition, cancellationToken) => api.문제신고Async(
                    condition.운송Id,
                    condition.요청,
                    cancellationToken)));
    }

    public Api작업ViewModel<기사운송예외신고조건, 기사운송요약응답?> 문제신고 { get; }
    public Api작업ViewModel<기사운송예외신고조건, 기사운송요약응답?> 예외신고 { get; }
}

/// <summary>기사 운송을 조회·상차·하차·예외 업무로 조립한 역할별 루트입니다.</summary>
public sealed class 기사운송기능ViewModel : 조립ViewModelBase
{
    public 기사운송기능ViewModel(
        기사운송조회ViewModel 조회,
        기사상차업무ViewModel 상차,
        기사하차업무ViewModel 하차,
        기사운송예외업무ViewModel 예외)
    {
        this.조회 = 하위ViewModel등록(조회);
        this.상차 = 하위ViewModel등록(상차);
        this.하차 = 하위ViewModel등록(하차);
        this.예외 = 하위ViewModel등록(예외);
    }

    public 기사운송조회ViewModel 조회 { get; }
    public 기사상차업무ViewModel 상차 { get; }
    public 기사하차업무ViewModel 하차 { get; }
    public 기사운송예외업무ViewModel 예외 { get; }

    public Api작업ViewModel<IReadOnlyList<기사운송요약응답>> 목록조회 => 조회.목록조회;
    public Api작업ViewModel<기사운송요약응답?> 현재조회 => 조회.현재조회;
    public Api작업ViewModel<long, 기사운송상세응답?> 상세조회 => 조회.상세조회;
    public Api작업ViewModel<long, 기사운송상태변경응답?> 상차지도착 => 상차.상차지도착;
    public Api작업ViewModel<기사운송상차완료조건, 기사운송상태변경응답?> 상차완료 => 상차.상차완료;
    public Api작업ViewModel<long, 기사운송상태변경응답?> 하차지도착 => 하차.하차지도착;
    public Api작업ViewModel<기사운송하차완료조건, 기사운송상태변경응답?> 하차완료 => 하차.하차완료;
    public Api작업ViewModel<기사운송예외신고조건, 기사운송요약응답?> 문제신고 => 예외.문제신고;
    public Api작업ViewModel<기사운송예외신고조건, 기사운송요약응답?> 예외신고 => 예외.예외신고;
}

public sealed record 기사운송상차완료조건(long 운송Id, 기사운송상차완료요청 요청);
public sealed record 기사운송하차완료조건(long 운송Id, 기사운송하차완료요청 요청);
public sealed record 기사운송예외신고조건(long 운송Id, 기사운송문제신고요청 요청);
