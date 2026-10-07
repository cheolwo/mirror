using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

public enum FoodDriverStage { Waiting, PickupTravel, PickupWaiting, Delivery }

/// <summary>네이티브 기사와 공유 화면이 같은 서버 상태로 수행 단계와 주 행동을 표시합니다.</summary>
public static class FoodDriverStagePresentation
{
    public static bool IsDropoff(bool canComplete, string? workStatus)
        => canComplete || workStatus is DriverWorkOfferStatus.MovingToDropoff or DriverWorkOfferStatus.PickupConfirmed;

    public static FoodDriverStage Resolve(bool hasActive, bool isDropoff, bool arrived, bool canPickup, bool hasAllowedArrival)
        => !hasActive ? FoodDriverStage.Waiting : isDropoff ? FoodDriverStage.Delivery
            : arrived || (canPickup && !hasAllowedArrival) ? FoodDriverStage.PickupWaiting : FoodDriverStage.PickupTravel;

    public static FoodDriverStage Resolve(FoodDeliveryDriverActiveDeliveryDto? delivery)
    {
        if (delivery is null) return FoodDriverStage.Waiting;
        var dropoff = IsDropoff(Can(delivery, 음식배달가능행동Ids.기사전달완료), delivery.WorkStatus);
        var arrival = Can(delivery, 음식배달가능행동Ids.기사가게도착) && !dropoff
            && !delivery.RestaurantArrivedAtUtc.HasValue && !string.IsNullOrWhiteSpace(delivery.DeliveryAttemptId);
        return Resolve(true, dropoff, delivery.RestaurantArrivedAtUtc.HasValue,
            Can(delivery, 음식배달가능행동Ids.기사픽업확인), arrival);
    }

    public static string Text(FoodDriverStage stage, bool arrived = false) => stage switch
    {
        FoodDriverStage.PickupTravel => "음식점 이동 중",
        FoodDriverStage.PickupWaiting => arrived ? "음식점 도착 · 픽업 대기" : "픽업 대기",
        FoodDriverStage.Delivery => "고객에게 전달 중",
        _ => "배차 대기"
    };

    // 주 행동의 종류는 상태가 정하고 활성 여부는 서버 가능 행동이 정합니다.
    public static string PrimaryAction(FoodDriverStage stage) => stage switch
    {
        FoodDriverStage.PickupTravel => 음식배달가능행동Ids.기사가게도착,
        FoodDriverStage.PickupWaiting => 음식배달가능행동Ids.기사픽업확인,
        FoodDriverStage.Delivery => 음식배달가능행동Ids.기사전달완료,
        _ => string.Empty
    };

    public static string PickupGuide(FoodDeliveryDriverActiveDeliveryDto delivery, FoodDriverStage stage)
    {
        var action = PrimaryAction(stage);
        var allowed = FoodWorkspacePresentation.Available(delivery.AvailableActions, action, string.Empty);
        if (stage == FoodDriverStage.PickupTravel)
            return allowed?.Enabled == true && !string.IsNullOrWhiteSpace(delivery.DeliveryAttemptId)
                ? "음식점에 도착한 뒤 도착을 확인해 주세요."
                : "현재 배달 시도와 도착 가능 여부를 새로고침해 주세요.";
        if (stage != FoodDriverStage.PickupWaiting) return "최신 배달 상태를 확인해 주세요.";
        var recooking = delivery.CurrentPreparationRound > 1 || delivery.RecookingRequestedAtUtc.HasValue;
        if (allowed?.Enabled == true)
            return recooking ? "재조리 음식을 픽업해 주세요." : "준비된 음식을 픽업해 주세요.";
        if (allowed is { Enabled: false }) return allowed.DisabledReason!;
        if (delivery.CurrentPickupReadyAtUtc is null || delivery.CurrentPickupReadyAtUtc == default(DateTime))
            return recooking ? "이번 재조리 음식이 아직 준비되지 않았습니다. 준비 완료를 기다려 주세요."
                : "음식이 아직 준비되지 않았습니다. 준비 완료를 기다려 주세요.";
        return "음식은 준비됐습니다. 현재 배달 시도와 픽업 가능 여부를 새로고침해 주세요.";
    }

    private static bool Can(FoodDeliveryDriverActiveDeliveryDto delivery, string action)
        => FoodWorkspacePresentation.Available(delivery.AvailableActions, action, string.Empty)?.Enabled == true;
}
