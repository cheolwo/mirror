using Hongdal.Contracts.Common.Drivers;

namespace FDriverApp.Services;

public sealed class FDriverAppProfile
{
    public string AppKey { get; } = 기사앱식별자.FoodDeliveryDriverApp;
    public string DisplayName { get; } = "살뜰 배달";
    public string DriverRole { get; } = "음식 배달 기사";
    public string DriverDomain { get; } = 기사도메인구분.음식배달;
    public string PrimaryWorkType { get; } = 기사업무유형코드.음식배달;

    public DriverWorkProfile WorkProfile { get; } = new(
        기사앱식별자.FoodDeliveryDriverApp,
        기사도메인구분.음식배달,
        기사업무유형코드.음식배달,
        "음식 배달 기사",
        "음식점과 고객 주소 사이의 짧은 시간창 배달을 처리합니다.",
        "조리/픽업 시간, 고객 도착 시간, 묶음 배달 가능 여부를 우선 확인합니다.");
}
