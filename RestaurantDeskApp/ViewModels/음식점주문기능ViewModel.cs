using Hongdal.Contracts.Food;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Services;

namespace RestaurantDeskApp.ViewModels;

public abstract class 음식점주문업무ViewModelBase(
    string 업무코드,
    string 업무명) : 조립ViewModelBase
{
    public string 업무코드 { get; } = 업무코드;
    public string 업무명 { get; } = 업무명;
}

/// <summary>서버 주문과 음식점 데스크 주문을 조회하는 기본 업무입니다.</summary>
public sealed class 음식점주문조회ViewModel : 음식점주문업무ViewModelBase
{
    public 음식점주문조회ViewModel(
        I음식주문ApiClient api,
        I음식점주문DeskService desk)
        : base("restaurant-order-query", "주문 조회")
    {
        서버주문목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<음식주문응답>>(api.주문목록조회Async));
        서버주문상세조회 = 하위ViewModel등록(
            new Api작업ViewModel<string, 음식주문응답?>(api.주문상세조회Async));
        데스크주문목록조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<음식점주문DeskItem>>(desk.주문목록조회Async));
    }

    public Api작업ViewModel<IReadOnlyList<음식주문응답>> 서버주문목록조회 { get; }
    public Api작업ViewModel<string, 음식주문응답?> 서버주문상세조회 { get; }
    public Api작업ViewModel<IReadOnlyList<음식점주문DeskItem>> 데스크주문목록조회 { get; }
}

/// <summary>주문 알림을 수신하고 음식점이 주문을 수락하는 기본 업무입니다.</summary>
public sealed class 음식점주문접수ViewModel : 음식점주문업무ViewModelBase
{
    public 음식점주문접수ViewModel(I음식점주문DeskService desk)
        : base("restaurant-order-acceptance", "주문 접수")
    {
        주문알림수신 = 하위ViewModel등록(
            new Api작업ViewModel<음식점주문수신Payload, 음식점주문DeskItem>(desk.주문알림수신Async));
        주문수락및전표준비 = 하위ViewModel등록(
            new Api작업ViewModel<string, 음식점주문수락결과>(desk.주문수락후전표준비Async));
    }

    public Api작업ViewModel<음식점주문수신Payload, 음식점주문DeskItem> 주문알림수신 { get; }
    public Api작업ViewModel<string, 음식점주문수락결과> 주문수락및전표준비 { get; }
}

/// <summary>수락된 주문의 전표 출력 완료를 기록하는 이행 업무입니다.</summary>
public sealed class 음식점주문이행ViewModel : 음식점주문업무ViewModelBase
{
    public 음식점주문이행ViewModel(I음식점주문DeskService desk)
        : base("restaurant-order-fulfillment", "주문 이행")
    {
        전표출력완료 = 하위ViewModel등록(
            new Api작업ViewModel<string, Api작업완료>(async (orderNumber, cancellationToken) =>
            {
                await desk.전표출력완료Async(orderNumber, cancellationToken);
                return Api작업완료.값;
            }));
    }

    public Api작업ViewModel<string, Api작업완료> 전표출력완료 { get; }
}

/// <summary>음식점 주문을 조회·접수·이행 단위로 조립합니다.</summary>
public sealed class 음식점주문기능ViewModel : 조립ViewModelBase
{
    public 음식점주문기능ViewModel(
        음식점주문조회ViewModel 조회,
        음식점주문접수ViewModel 접수,
        음식점주문이행ViewModel 이행)
    {
        this.조회 = 하위ViewModel등록(조회);
        this.접수 = 하위ViewModel등록(접수);
        this.이행 = 하위ViewModel등록(이행);
    }

    public 음식점주문조회ViewModel 조회 { get; }
    public 음식점주문접수ViewModel 접수 { get; }
    public 음식점주문이행ViewModel 이행 { get; }

    public Api작업ViewModel<IReadOnlyList<음식주문응답>> 서버주문목록조회 => 조회.서버주문목록조회;
    public Api작업ViewModel<string, 음식주문응답?> 서버주문상세조회 => 조회.서버주문상세조회;
    public Api작업ViewModel<IReadOnlyList<음식점주문DeskItem>> 데스크주문목록조회 => 조회.데스크주문목록조회;
    public Api작업ViewModel<음식점주문수신Payload, 음식점주문DeskItem> 주문알림수신 => 접수.주문알림수신;
    public Api작업ViewModel<string, 음식점주문수락결과> 주문수락및전표준비 => 접수.주문수락및전표준비;
    public Api작업ViewModel<string, Api작업완료> 전표출력완료 => 이행.전표출력완료;
}

public sealed class 음식점Api기능모음ViewModel : 조립ViewModelBase
{
    public 음식점Api기능모음ViewModel(
        음식점주문기능ViewModel 주문,
        음식Controller기능모음ViewModel 음식Controllers,
        공통Controller기능모음ViewModel 공통Controllers)
    {
        this.주문 = 하위ViewModel등록(주문);
        this.음식Controllers = 하위ViewModel등록(음식Controllers);
        this.공통Controllers = 하위ViewModel등록(공통Controllers);
    }

    public 음식점주문기능ViewModel 주문 { get; }
    public 음식Controller기능모음ViewModel 음식Controllers { get; }
    public 공통Controller기능모음ViewModel 공통Controllers { get; }
}
