using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

public enum FoodDriverStage { Waiting, PickupTravel, PickupWaiting, Delivery }

/// <summary>네이티브 기사와 공유 화면이 같은 서버 상태로 수행 단계와 주 행동을 표시합니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.ViewModel,
    "확인된 배달 상태·가능 행동으로 현재 수행 단계와 주 행동 표시를 선택",
    StepKey = "ui.stage-presentation", FlowOrder = 205, ExecutionStage = SsalddelCodeExecutionStage.Presentation,
    DependsOnStepKeys = new[] { "contract.active-delivery", "ui.food-presentation" },
    ReadsFrom = SsalddelCodeDataScope.ClientPresentation, Effects = SsalddelCodeEffect.None,
    Boundary = "순수 표시 판정이며 실제 픽업·전달 가능 권한을 생성하거나 서버 상태를 변경하지 않는다.")]
public static class FoodDriverStagePresentation
{
    public static bool IsDropoff(bool canComplete, string? workStatus)
        => canComplete || workStatus is DriverWorkOfferStatus.MovingToDropoff or DriverWorkOfferStatus.PickupConfirmed;

    public static FoodDriverStage Resolve(bool hasActive, bool isDropoff, bool arrived, bool canPickup, bool hasAllowedArrival)
        => !hasActive ? FoodDriverStage.Waiting : isDropoff ? FoodDriverStage.Delivery
            : arrived || (canPickup && !hasAllowedArrival) ? FoodDriverStage.PickupWaiting : FoodDriverStage.PickupTravel;

    public static FoodDriverStage Resolve(FoodDeliveryDriverActiveDeliveryDto? delivery, DateTime? nowUtc = null)
    {
        if (delivery is null) return FoodDriverStage.Waiting;
        var dropoff = IsDropoff(Can(delivery, 음식배달가능행동Ids.기사전달완료, nowUtc), delivery.WorkStatus);
        var arrival = Can(delivery, 음식배달가능행동Ids.기사가게도착, nowUtc) && !dropoff
            && !delivery.RestaurantArrivedAtUtc.HasValue && !string.IsNullOrWhiteSpace(delivery.DeliveryAttemptId);
        return Resolve(true, dropoff, delivery.RestaurantArrivedAtUtc.HasValue,
            Can(delivery, 음식배달가능행동Ids.기사픽업확인, nowUtc), arrival);
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

    public static string PrimaryActionLabel(FoodDriverStage stage) => stage switch
    {
        FoodDriverStage.PickupTravel => "가게 도착",
        FoodDriverStage.PickupWaiting => "픽업 완료",
        FoodDriverStage.Delivery => "전달 완료",
        _ => string.Empty
    };

    public static string Address(string? address, FoodDriverStage stage)
        => !string.IsNullOrWhiteSpace(address) ? address
            : stage == FoodDriverStage.Delivery ? "전달 주소 미확인" : "픽업 주소 미확인";

    public static string PickupGuide(FoodDeliveryDriverActiveDeliveryDto delivery, FoodDriverStage stage, DateTime? nowUtc = null)
        => Guide(delivery, stage, nowUtc);

    public static string Guide(FoodDeliveryDriverActiveDeliveryDto delivery, FoodDriverStage stage, DateTime? nowUtc = null)
        => Guide(stage, delivery.AvailableActions, !string.IsNullOrWhiteSpace(delivery.DeliveryAttemptId),
            delivery.CurrentPreparationRound > 1 || delivery.RecookingRequestedAtUtc.HasValue,
            delivery.CurrentPickupReadyAtUtc is { } readyAt && readyAt != default, nowUtc);

    public static string Guide(FoodDriverStage stage, IReadOnlyList<업무가능행동Dto> actions,
        bool hasValidAttempt, bool recooking, bool pickupReady, DateTime? nowUtc = null)
    {
        var action = PrimaryAction(stage);
        var allowed = FoodWorkspacePresentation.Available(actions, action, string.Empty, automaticRefresh: true, nowUtc: nowUtc);
        if (stage == FoodDriverStage.PickupTravel)
            return allowed?.Enabled == true && hasValidAttempt
                ? "음식점에 도착하면 가게 도착을 눌러 주세요."
                : allowed?.DisabledReason ?? "현재 배달 시도와 도착 가능 여부를 자동으로 확인하고 있습니다.";
        if (stage == FoodDriverStage.Delivery)
            return allowed?.Enabled == true ? "고객에게 전달한 뒤 전달 완료를 눌러 주세요."
                : allowed?.DisabledReason ?? "전달 가능 여부를 자동으로 확인하고 있습니다.";
        if (stage != FoodDriverStage.PickupWaiting) return "최신 배달 상태를 확인해 주세요.";
        if (allowed?.Enabled == true)
            return recooking ? "재조리 음식을 받은 뒤 픽업 완료를 눌러 주세요." : "음식을 받은 뒤 픽업 완료를 눌러 주세요.";
        if (allowed is { Enabled: false }) return allowed.DisabledReason!;
        if (!pickupReady)
            return recooking ? "이번 재조리 음식이 아직 준비되지 않았습니다. 준비 완료를 기다려 주세요."
                : "음식이 아직 준비되지 않았습니다. 준비 완료를 기다려 주세요.";
        return "음식은 준비됐습니다. 현재 배달 시도와 픽업 가능 여부를 자동으로 확인하고 있습니다.";
    }

    private static bool Can(FoodDeliveryDriverActiveDeliveryDto delivery, string action, DateTime? nowUtc)
        => FoodWorkspacePresentation.Available(delivery.AvailableActions, action, string.Empty, automaticRefresh: true, nowUtc: nowUtc)?.Enabled == true;
}
