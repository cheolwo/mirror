using FDriverApp.Services;
using Hongdal.Contracts.Common.Drivers;
using Hongdal.Contracts.Driver.Food;
using Hongdal.Contracts.Driver.Work;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace FDriverApp.ViewModels;

public abstract class 음식배달기사업무ViewModelBase(
    string 업무코드,
    string 업무명) : 조립ViewModelBase
{
    public string 업무코드 { get; } = 업무코드;
    public string 업무명 { get; } = 업무명;
}

/// <summary>작업공간, 운행 시작·종료와 위치 갱신을 담당합니다.</summary>
public sealed class 음식배달기사운행ViewModel : 음식배달기사업무ViewModelBase
{
    public 음식배달기사운행ViewModel(IFoodDeliveryDriverApiService api)
        : base("food-driver-work", "운행")
    {
        작업공간조회 = 하위ViewModel등록(
            new Api작업ViewModel<FoodDeliveryDriverWorkspaceDto>(api.GetWorkspaceAsync));
        운행상태조회 = 하위ViewModel등록(
            new Api작업ViewModel<기사운행상태응답?>(api.GetWorkStatusAsync));
        운행시작 = 하위ViewModel등록(
            new Api작업ViewModel<string, Api작업완료>(async (startLocation, cancellationToken) =>
            {
                await api.StartWorkAsync(startLocation, cancellationToken);
                return Api작업완료.값;
            }));
        운행종료 = 하위ViewModel등록(
            new Api작업ViewModel<Api작업완료>(async cancellationToken =>
            {
                await api.StopWorkAsync(cancellationToken);
                return Api작업완료.값;
            }));
        위치갱신 = 하위ViewModel등록(
            new Api작업ViewModel<기사위치갱신요청, 기사위치갱신응답?>(api.UpdateLocationAsync));
    }

    public Api작업ViewModel<FoodDeliveryDriverWorkspaceDto> 작업공간조회 { get; }
    public Api작업ViewModel<기사운행상태응답?> 운행상태조회 { get; }
    public Api작업ViewModel<string, Api작업완료> 운행시작 { get; }
    public Api작업ViewModel<Api작업완료> 운행종료 { get; }
    public Api작업ViewModel<기사위치갱신요청, 기사위치갱신응답?> 위치갱신 { get; }
}

/// <summary>단건·묶음 배달 제안 수락을 담당합니다.</summary>
public sealed class 음식배달수락ViewModel : 음식배달기사업무ViewModelBase
{
    public 음식배달수락ViewModel(IFoodDeliveryDriverApiService api)
        : base("food-delivery-acceptance", "배달 수락")
    {
        배달제안수락 = 하위ViewModel등록(
            new Api작업ViewModel<string, FoodDeliveryDriverActionResponse>(api.AcceptAsync));
        묶음배달수락 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<string>, FoodDeliveryDriverActionResponse>(api.AcceptBundleAsync));
    }

    public Api작업ViewModel<string, FoodDeliveryDriverActionResponse> 배달제안수락 { get; }
    public Api작업ViewModel<IReadOnlyList<string>, FoodDeliveryDriverActionResponse> 묶음배달수락 { get; }
}

/// <summary>음식 픽업과 고객 배달 완료를 담당합니다.</summary>
public sealed class 음식배달이행ViewModel : 음식배달기사업무ViewModelBase
{
    public 음식배달이행ViewModel(IFoodDeliveryDriverApiService api)
        : base("food-delivery-fulfillment", "픽업·배달")
    {
        픽업완료 = 하위ViewModel등록(
            new Api작업ViewModel<string, FoodDeliveryDriverActionResponse>(api.ConfirmPickupAsync));
        배달완료 = 하위ViewModel등록(
            new Api작업ViewModel<string, FoodDeliveryDriverActionResponse>(api.CompleteAsync));
    }

    public Api작업ViewModel<string, FoodDeliveryDriverActionResponse> 픽업완료 { get; }
    public Api작업ViewModel<string, FoodDeliveryDriverActionResponse> 배달완료 { get; }
}

/// <summary>선택한 배달의 이동 경로 조회를 담당합니다.</summary>
public sealed class 음식배달경로ViewModel : 음식배달기사업무ViewModelBase
{
    public 음식배달경로ViewModel(IFoodDeliveryDriverApiService api)
        : base("food-delivery-route", "배달 경로")
    {
        경로조회 = 하위ViewModel등록(
            new Api작업ViewModel<FoodDeliveryDriverRouteRequestDto, FoodDeliveryDriverRouteResponseDto>(api.GetRouteAsync));
    }

    public Api작업ViewModel<FoodDeliveryDriverRouteRequestDto, FoodDeliveryDriverRouteResponseDto> 경로조회 { get; }
}

/// <summary>음식배달 기사 업무를 운행·수락·이행·경로 단위로 조립합니다.</summary>
public sealed class 음식배달기사업무기능ViewModel : 조립ViewModelBase
{
    public 음식배달기사업무기능ViewModel(
        음식배달기사운행ViewModel 운행,
        음식배달수락ViewModel 수락,
        음식배달이행ViewModel 이행,
        음식배달경로ViewModel 경로)
    {
        this.운행 = 하위ViewModel등록(운행);
        this.수락 = 하위ViewModel등록(수락);
        this.이행 = 하위ViewModel등록(이행);
        this.경로 = 하위ViewModel등록(경로);
    }

    public 음식배달기사운행ViewModel 운행 { get; }
    public 음식배달수락ViewModel 수락 { get; }
    public 음식배달이행ViewModel 이행 { get; }
    public 음식배달경로ViewModel 경로 { get; }

    public Api작업ViewModel<FoodDeliveryDriverWorkspaceDto> 작업공간조회 => 운행.작업공간조회;
    public Api작업ViewModel<기사운행상태응답?> 운행상태조회 => 운행.운행상태조회;
    public Api작업ViewModel<string, Api작업완료> 운행시작 => 운행.운행시작;
    public Api작업ViewModel<Api작업완료> 운행종료 => 운행.운행종료;
    public Api작업ViewModel<기사위치갱신요청, 기사위치갱신응답?> 위치갱신 => 운행.위치갱신;
    public Api작업ViewModel<string, FoodDeliveryDriverActionResponse> 배달제안수락 => 수락.배달제안수락;
    public Api작업ViewModel<IReadOnlyList<string>, FoodDeliveryDriverActionResponse> 묶음배달수락 => 수락.묶음배달수락;
    public Api작업ViewModel<string, FoodDeliveryDriverActionResponse> 픽업완료 => 이행.픽업완료;
    public Api작업ViewModel<string, FoodDeliveryDriverActionResponse> 배달완료 => 이행.배달완료;
    public Api작업ViewModel<FoodDeliveryDriverRouteRequestDto, FoodDeliveryDriverRouteResponseDto> 경로조회 => 경로.경로조회;
}

public sealed class 음식배달기사Api기능모음ViewModel : 조립ViewModelBase
{
    public 음식배달기사Api기능모음ViewModel(
        음식배달기사업무기능ViewModel 업무,
        음식배달기사Controller기능모음ViewModel 음식배달Controllers,
        기사Controller기능모음ViewModel 기사Controllers,
        공통Controller기능모음ViewModel 공통Controllers)
    {
        this.업무 = 하위ViewModel등록(업무);
        this.음식배달Controllers = 하위ViewModel등록(음식배달Controllers);
        this.기사Controllers = 하위ViewModel등록(기사Controllers);
        this.공통Controllers = 하위ViewModel등록(공통Controllers);
    }

    public 음식배달기사업무기능ViewModel 업무 { get; }
    public 음식배달기사Controller기능모음ViewModel 음식배달Controllers { get; }
    public 기사Controller기능모음ViewModel 기사Controllers { get; }
    public 공통Controller기능모음ViewModel 공통Controllers { get; }
}
