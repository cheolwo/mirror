using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace WarehouseManagerApp.ViewModels;

public abstract class 창고업무단위ViewModelBase(
    string 업무코드,
    string 업무명) : 조립ViewModelBase
{
    protected const string BasePath = "api/v1/warehouse-operations";

    public string 업무코드 { get; } = 업무코드;
    public string 업무명 { get; } = 업무명;
}

/// <summary>창고와 작업 사용자를 관리하는 기준정보 업무입니다.</summary>
public sealed class 창고기준정보업무ViewModel : 창고업무단위ViewModelBase
{
    public 창고기준정보업무ViewModel(IHongdalJsonApiClient api)
        : base("warehouse-master", "창고 기준정보")
    {
        창고목록조회 = 하위ViewModel등록(new Api작업ViewModel<창고목록응답?>(
            cancellationToken => api.GetAsync<창고목록응답>($"{BasePath}/warehouses", "창고 목록 조회", cancellationToken: cancellationToken)));
        창고생성 = 하위ViewModel등록(new Api작업ViewModel<창고저장요청, 창고요약응답?>(
            (request, cancellationToken) => api.SendAsync<창고저장요청, 창고요약응답>(
                HttpMethod.Post, $"{BasePath}/warehouses", request, "창고 생성", cancellationToken: cancellationToken)));
        창고사용자목록조회 = 하위ViewModel등록(new Api작업ViewModel<long, 창고사용자목록응답?>(
            (warehouseId, cancellationToken) => api.GetAsync<창고사용자목록응답>(
                $"{BasePath}/warehouses/{warehouseId}/users", "창고 사용자 목록 조회", cancellationToken: cancellationToken)));
        창고사용자추가 = 하위ViewModel등록(new Api작업ViewModel<창고사용자추가조건, 창고사용자항목응답?>(
            (condition, cancellationToken) => api.SendAsync<창고사용자저장요청, 창고사용자항목응답>(
                HttpMethod.Post,
                $"{BasePath}/warehouses/{condition.창고Id}/users",
                condition.요청,
                "창고 사용자 추가",
                cancellationToken: cancellationToken)));
    }

    public Api작업ViewModel<창고목록응답?> 창고목록조회 { get; }
    public Api작업ViewModel<창고저장요청, 창고요약응답?> 창고생성 { get; }
    public Api작업ViewModel<long, 창고사용자목록응답?> 창고사용자목록조회 { get; }
    public Api작업ViewModel<창고사용자추가조건, 창고사용자항목응답?> 창고사용자추가 { get; }
}

/// <summary>입고 요청의 조회·생성·완료를 담당하는 입고 업무입니다.</summary>
public sealed class 창고입고업무ViewModel : 창고업무단위ViewModelBase
{
    public 창고입고업무ViewModel(IHongdalJsonApiClient api)
        : base("warehouse-inbound", "입고")
    {
        입고목록조회 = 하위ViewModel등록(new Api작업ViewModel<입고요청목록응답?>(
            cancellationToken => api.GetAsync<입고요청목록응답>($"{BasePath}/inbounds", "입고 목록 조회", cancellationToken: cancellationToken)));
        입고생성 = 하위ViewModel등록(new Api작업ViewModel<입고요청저장요청, 입고요청항목응답?>(
            (request, cancellationToken) => api.SendAsync<입고요청저장요청, 입고요청항목응답>(
                HttpMethod.Post, $"{BasePath}/inbounds", request, "입고 생성", cancellationToken: cancellationToken)));
        입고완료 = 하위ViewModel등록(new Api작업ViewModel<입고완료조건, 입고상품목록응답?>(
            (condition, cancellationToken) => api.SendAsync<입고완료요청, 입고상품목록응답>(
                HttpMethod.Post,
                $"{BasePath}/inbounds/{condition.입고Id}/complete",
                condition.요청,
                "입고 완료",
                cancellationToken: cancellationToken)));
    }

    public Api작업ViewModel<입고요청목록응답?> 입고목록조회 { get; }
    public Api작업ViewModel<입고요청저장요청, 입고요청항목응답?> 입고생성 { get; }
    public Api작업ViewModel<입고완료조건, 입고상품목록응답?> 입고완료 { get; }
}

/// <summary>재고 조회·검수·적재·포장을 담당하는 재고 및 출고 준비 업무입니다.</summary>
public sealed class 창고재고출고업무ViewModel : 창고업무단위ViewModelBase
{
    public 창고재고출고업무ViewModel(IHongdalJsonApiClient api)
        : base("warehouse-inventory-outbound", "재고·출고 준비")
    {
        재고목록조회 = 하위ViewModel등록(new Api작업ViewModel<재고목록응답?>(
            cancellationToken => api.GetAsync<재고목록응답>($"{BasePath}/inventory", "재고 목록 조회", cancellationToken: cancellationToken)));
        입고검수 = 하위ViewModel등록(new Api작업ViewModel<입고검수조건, 창고작업결과응답?>(
            (condition, cancellationToken) => api.SendAsync<입고검수요청, 창고작업결과응답>(
                HttpMethod.Post,
                $"{BasePath}/inventory/{condition.입고항목Id}/inspect",
                condition.요청,
                "입고 검수",
                cancellationToken: cancellationToken)));
        적재위치배정 = 하위ViewModel등록(new Api작업ViewModel<적재위치배정조건, 창고작업결과응답?>(
            (condition, cancellationToken) => api.SendAsync<적재위치배정요청, 창고작업결과응답>(
                HttpMethod.Post,
                $"{BasePath}/inventory/{condition.입고항목Id}/put-away",
                condition.요청,
                "적재 위치 배정",
                cancellationToken: cancellationToken)));
        포장작업 = 하위ViewModel등록(new Api작업ViewModel<포장작업조건, 창고작업결과응답?>(
            (condition, cancellationToken) => api.SendAsync<포장작업요청, 창고작업결과응답>(
                HttpMethod.Post,
                $"{BasePath}/inventory/{condition.입고항목Id}/pack",
                condition.요청,
                "포장 작업",
                cancellationToken: cancellationToken)));
    }

    public Api작업ViewModel<재고목록응답?> 재고목록조회 { get; }
    public Api작업ViewModel<입고검수조건, 창고작업결과응답?> 입고검수 { get; }
    public Api작업ViewModel<적재위치배정조건, 창고작업결과응답?> 적재위치배정 { get; }
    public Api작업ViewModel<포장작업조건, 창고작업결과응답?> 포장작업 { get; }
}

/// <summary>출고 재고를 운송 업무로 인계하는 연계 업무입니다.</summary>
public sealed class 창고운송연계업무ViewModel : 창고업무단위ViewModelBase
{
    public 창고운송연계업무ViewModel(IHongdalJsonApiClient api)
        : base("warehouse-transport-handoff", "운송 인계")
    {
        재위탁운송생성 = 하위ViewModel등록(new Api작업ViewModel<재고운송의뢰생성요청, 화주운송의뢰응답?>(
            (request, cancellationToken) => api.SendAsync<재고운송의뢰생성요청, 화주운송의뢰응답>(
                HttpMethod.Post,
                $"{BasePath}/inventory/reconsignment",
                request,
                "재위탁 운송 생성",
                cancellationToken: cancellationToken)));
    }

    public Api작업ViewModel<재고운송의뢰생성요청, 화주운송의뢰응답?> 재위탁운송생성 { get; }
}

/// <summary>
/// 공통 입출고 ViewModel과 창고 관리자 전용 API 업무를 조립합니다.
/// 기존 평면 속성은 호환성을 위해 각 업무 단위로 전달합니다.
/// </summary>
public sealed class 창고작업기능ViewModel : 조립ViewModelBase
{
    public 창고작업기능ViewModel(
        입출고화면ViewModel 기본입출고,
        창고기준정보업무ViewModel 기준정보,
        창고입고업무ViewModel 입고,
        창고재고출고업무ViewModel 재고출고,
        창고운송연계업무ViewModel 운송연계)
    {
        this.기본입출고 = 하위ViewModel등록(기본입출고);
        this.기준정보 = 하위ViewModel등록(기준정보);
        this.입고 = 하위ViewModel등록(입고);
        this.재고출고 = 하위ViewModel등록(재고출고);
        this.운송연계 = 하위ViewModel등록(운송연계);
    }

    public 입출고화면ViewModel 기본입출고 { get; }
    public 창고기준정보업무ViewModel 기준정보 { get; }
    public 창고입고업무ViewModel 입고 { get; }
    public 창고재고출고업무ViewModel 재고출고 { get; }
    public 창고운송연계업무ViewModel 운송연계 { get; }

    public Api작업ViewModel<창고목록응답?> 창고목록조회 => 기준정보.창고목록조회;
    public Api작업ViewModel<창고저장요청, 창고요약응답?> 창고생성 => 기준정보.창고생성;
    public Api작업ViewModel<long, 창고사용자목록응답?> 창고사용자목록조회 => 기준정보.창고사용자목록조회;
    public Api작업ViewModel<창고사용자추가조건, 창고사용자항목응답?> 창고사용자추가 => 기준정보.창고사용자추가;
    public Api작업ViewModel<입고요청목록응답?> 입고목록조회 => 입고.입고목록조회;
    public Api작업ViewModel<입고요청저장요청, 입고요청항목응답?> 입고생성 => 입고.입고생성;
    public Api작업ViewModel<입고완료조건, 입고상품목록응답?> 입고완료 => 입고.입고완료;
    public Api작업ViewModel<재고목록응답?> 재고목록조회 => 재고출고.재고목록조회;
    public Api작업ViewModel<입고검수조건, 창고작업결과응답?> 입고검수 => 재고출고.입고검수;
    public Api작업ViewModel<적재위치배정조건, 창고작업결과응답?> 적재위치배정 => 재고출고.적재위치배정;
    public Api작업ViewModel<포장작업조건, 창고작업결과응답?> 포장작업 => 재고출고.포장작업;
    public Api작업ViewModel<재고운송의뢰생성요청, 화주운송의뢰응답?> 재위탁운송생성 => 운송연계.재위탁운송생성;
}

public sealed record 창고사용자추가조건(long 창고Id, 창고사용자저장요청 요청);
public sealed record 입고완료조건(long 입고Id, 입고완료요청 요청);
public sealed record 입고검수조건(long 입고항목Id, 입고검수요청 요청);
public sealed record 적재위치배정조건(long 입고항목Id, 적재위치배정요청 요청);
public sealed record 포장작업조건(long 입고항목Id, 포장작업요청 요청);

public sealed class 창고Api기능모음ViewModel : 조립ViewModelBase
{
    public 창고Api기능모음ViewModel(
        창고작업기능ViewModel 작업,
        창고Controller기능모음ViewModel 창고Controllers,
        공통Controller기능모음ViewModel 공통Controllers)
    {
        this.작업 = 하위ViewModel등록(작업);
        this.창고Controllers = 하위ViewModel등록(창고Controllers);
        this.공통Controllers = 하위ViewModel등록(공통Controllers);
    }

    public 창고작업기능ViewModel 작업 { get; }
    public 창고Controller기능모음ViewModel 창고Controllers { get; }
    public 공통Controller기능모음ViewModel 공통Controllers { get; }
}
