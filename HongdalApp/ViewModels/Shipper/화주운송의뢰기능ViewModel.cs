using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using HongdalApp.Models.Shipper;
using HongdalApp.Services;

namespace HongdalApp.ViewModels.Shipper;

public abstract class 화주운송의뢰업무ViewModelBase(
    string 업무코드,
    string 업무명) : 조립ViewModelBase
{
    public string 업무코드 { get; } = 업무코드;
    public string 업무명 { get; } = 업무명;
}

/// <summary>화주의 운송의뢰와 공개 화물·차량 기준정보를 조회합니다.</summary>
public sealed class 화주운송의뢰조회ViewModel : 화주운송의뢰업무ViewModelBase
{
    public 화주운송의뢰조회ViewModel(IShipperOperationsService operations)
        : base("shipper-request-query", "운송의뢰 조회")
    {
        의뢰목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<ShipperRequestItem>>(operations.GetRequestsAsync),
            수명소유: true);
        의뢰상세조회 = 하위ViewModel등록(
            new Api작업ViewModel<string, ShipperRequestItem?>(operations.GetRequestAsync),
            수명소유: true);
        공개화물조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<공개화물요약응답>>(operations.GetPublicCargoAsync),
            수명소유: true);
        차량종류조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<string>>(operations.GetVehicleTypesAsync),
            수명소유: true);
    }

    public Api작업ViewModel<IReadOnlyList<ShipperRequestItem>> 의뢰목록조회 { get; }
    public Api작업ViewModel<string, ShipperRequestItem?> 의뢰상세조회 { get; }
    public Api작업ViewModel<IReadOnlyList<공개화물요약응답>> 공개화물조회 { get; }
    public Api작업ViewModel<IReadOnlyList<string>> 차량종류조회 { get; }
}

/// <summary>예상 운임을 계산하고 단건 운송의뢰를 등록합니다.</summary>
public sealed class 화주운송의뢰작성ViewModel : 화주운송의뢰업무ViewModelBase
{
    public 화주운송의뢰작성ViewModel(IShipperOperationsService operations)
        : base("shipper-request-draft", "운송의뢰 작성")
    {
        운임예상 = 하위ViewModel등록(
            new Api작업ViewModel<화주운임예상조건, decimal>(
                (condition, cancellationToken) => operations.EstimateFareAsync(
                    condition.차량종류,
                    condition.거리Km,
                    cancellationToken)),
            수명소유: true);
        의뢰등록 = 하위ViewModel등록(
            new Api작업ViewModel<ShipperRequestItem, Api작업완료>(async (request, cancellationToken) =>
            {
                await operations.AddRequestAsync(request, cancellationToken);
                return Api작업완료.값;
            }), 수명소유: true);
    }

    public Api작업ViewModel<화주운임예상조건, decimal> 운임예상 { get; }
    public Api작업ViewModel<ShipperRequestItem, Api작업완료> 의뢰등록 { get; }
}

/// <summary>운송의뢰 파일을 미리 검증하고 일괄 등록합니다.</summary>
public sealed class 화주운송의뢰일괄ViewModel : 화주운송의뢰업무ViewModelBase
{
    public 화주운송의뢰일괄ViewModel(IShipperBulkRequestService bulkRequests)
        : base("shipper-request-bulk", "운송의뢰 일괄등록")
    {
        일괄미리보기 = 하위ViewModel등록(
            new Api작업ViewModel<화주일괄파일조건, 화주운송의뢰일괄미리보기응답?>(
                (condition, cancellationToken) => bulkRequests.미리보기Async(
                    condition.스트림,
                    condition.파일명,
                    cancellationToken)),
            수명소유: true);
        일괄등록 = 하위ViewModel등록(
            new Api작업ViewModel<화주운송의뢰일괄확정등록요청, 화주운송의뢰일괄등록결과응답?>(
                bulkRequests.등록Async),
            수명소유: true);
    }

    public Api작업ViewModel<화주일괄파일조건, 화주운송의뢰일괄미리보기응답?> 일괄미리보기 { get; }
    public Api작업ViewModel<화주운송의뢰일괄확정등록요청, 화주운송의뢰일괄등록결과응답?> 일괄등록 { get; }
}

/// <summary>화주 운송의뢰를 조회·작성·일괄등록 업무로 조립합니다.</summary>
public sealed class 화주운송의뢰기능ViewModel : 조립ViewModelBase, ICrudPageViewModel
{
    public 화주운송의뢰기능ViewModel(
        화주운송의뢰상태ViewModel 상태,
        화주운송의뢰CrudViewModel crud,
        화주운송의뢰조회ViewModel 조회,
        화주운송의뢰작성ViewModel 작성,
        화주운송의뢰일괄ViewModel 일괄)
    {
        this.상태 = 하위ViewModel등록(상태, 수명소유: false);
        Crud = 하위ViewModel등록(crud);
        this.조회 = 하위ViewModel등록(조회);
        this.작성 = 하위ViewModel등록(작성);
        this.일괄 = 하위ViewModel등록(일괄);
        Crud업무단위목록 = [Crud];
    }

    public 화주운송의뢰상태ViewModel 상태 { get; }
    public 화주운송의뢰CrudViewModel Crud { get; }
    public 화주운송의뢰조회ViewModel 조회 { get; }
    public 화주운송의뢰작성ViewModel 작성 { get; }
    public 화주운송의뢰일괄ViewModel 일괄 { get; }
    public IReadOnlyList<I업무단위CrudViewModel> Crud업무단위목록 { get; }

    public Api작업ViewModel<IReadOnlyList<ShipperRequestItem>> 의뢰목록조회 => 조회.의뢰목록조회;
    public Api작업ViewModel<string, ShipperRequestItem?> 의뢰상세조회 => 조회.의뢰상세조회;
    public Api작업ViewModel<IReadOnlyList<공개화물요약응답>> 공개화물조회 => 조회.공개화물조회;
    public Api작업ViewModel<IReadOnlyList<string>> 차량종류조회 => 조회.차량종류조회;
    public Api작업ViewModel<화주운임예상조건, decimal> 운임예상 => 작성.운임예상;
    public Api작업ViewModel<ShipperRequestItem, Api작업완료> 의뢰등록 => 작성.의뢰등록;
    public Api작업ViewModel<화주일괄파일조건, 화주운송의뢰일괄미리보기응답?> 일괄미리보기 => 일괄.일괄미리보기;
    public Api작업ViewModel<화주운송의뢰일괄확정등록요청, 화주운송의뢰일괄등록결과응답?> 일괄등록 => 일괄.일괄등록;
}

public sealed record 화주운임예상조건(string 차량종류, decimal 거리Km);

/// <remarks>스트림의 수명은 이 요청을 만든 페이지가 관리합니다.</remarks>
public sealed record 화주일괄파일조건(Stream 스트림, string 파일명);
