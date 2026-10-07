using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private string? _currentDeliveryPresentationKey;
    private string _waitingEffectiveDispatchState = string.Empty;
    private string _waitingEffectiveDispatchReason = string.Empty;
    private DateTimeOffset? _waitingEffectiveDispatchChangedAtUtc;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoodCardExpanded), nameof(IsCurrentDeliveryContentVisible),
        nameof(IsCurrentDeliverySummaryVisible), nameof(CurrentDeliveryToggleText))]
    private bool _isCurrentDeliveryExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoodCardExpanded), nameof(DeliveryDetailsToggleText), nameof(IsDeliveryDetailsVisible))]
    private bool _isDeliveryDetailsExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoodCardExpanded), nameof(WorkControlsToggleText))]
    private bool _isWorkControlsExpanded;

    public bool IsFoodCardExpanded => (HasActiveWork && IsCurrentDeliveryExpanded)
        || (HasRecommendationContent && IsDeliveryDetailsExpanded) || IsWorkControlsExpanded;
    public bool IsCurrentDeliveryContentVisible => HasActiveWork && IsCurrentDeliveryExpanded;
    public bool IsCurrentDeliverySummaryVisible => HasActiveWork && !IsCurrentDeliveryExpanded;
    public string CurrentDeliveryToggleText => IsCurrentDeliveryExpanded ? "접기" : "펼치기";
    public string? CurrentDeliveryPresentationKey => _currentDeliveryPresentationKey;
    public FDriverFoodStage CurrentDeliveryStage => FoodDriverStagePresentation.Resolve(
        ActiveDelivery is not null, IsCurrentTargetDropoff, ActiveDelivery?.RestaurantArrivedAtUtc.HasValue == true,
        ActiveDelivery?.Can(음식배달가능행동Ids.기사픽업확인) == true, HasAllowedArrival) switch
    {
        FoodDriverStage.PickupTravel => FDriverFoodStage.PickupTravel,
        FoodDriverStage.PickupWaiting => FDriverFoodStage.PickupWaiting,
        FoodDriverStage.Delivery => FDriverFoodStage.Delivery,
        _ => FDriverFoodStage.Waiting
    };
    public bool HasPickupStage => CurrentDeliveryStage is FDriverFoodStage.PickupTravel or FDriverFoodStage.PickupWaiting;
    public bool HasDeliveryStage => CurrentDeliveryStage == FDriverFoodStage.Delivery;
    public bool HasDeliveryRecipient => HasDeliveryStage && HasActiveRecipient;
    public bool HasPickupPreparationTime => HasPickupStage && ActiveDelivery is { } delivery
        && (delivery.DisplayedPreparationReadyAtUtc.HasValue || delivery.CurrentPickupReadyAtUtc.HasValue || delivery.IsRecooking);
    public string PickupPreparationTimeText
    {
        get
        {
            if (!HasPickupPreparationTime || ActiveDelivery is not { } delivery) return string.Empty;
            var expected = delivery.DisplayedPreparationReadyAtUtc is { } expectedAt
                ? $"준비 예정 {expectedAt.ToLocalTime():HH:mm}" : string.Empty;
            var current = delivery.CurrentPickupReadyAtUtc is { } readyAt
                ? $"{(delivery.IsRecooking ? "재조리 음식" : "음식")} · 준비 완료 {readyAt.ToLocalTime():HH:mm}"
                : delivery.IsRecooking ? "재조리 음식 · 준비 중" : string.Empty;
            return string.IsNullOrEmpty(current) ? expected
                : string.IsNullOrEmpty(expected) ? current : $"{current}\n{expected}";
        }
    }
    public string CurrentDeliveryStageText => CurrentDeliveryStage switch
    {
        FDriverFoodStage.PickupTravel => "음식점 이동 중",
        FDriverFoodStage.PickupWaiting => ActiveDelivery?.RestaurantArrivedAtUtc.HasValue == true
            ? "음식점 도착 · 픽업 대기" : "픽업 대기",
        FDriverFoodStage.Delivery => "고객에게 전달 중",
        _ => "배차 대기"
    };
    public string DeliveryDetailsToggleText => IsDeliveryDetailsExpanded ? "상세 접기" : "상세 보기";
    public string WorkControlsToggleText => IsWorkControlsExpanded ? "설정 접기" : "운행·수신 설정";

    public bool IsWaitingForDelivery => IsAuthenticated && !HasActiveWork
        && !HasRecommendationContent && SelectedTicket is null;
    public bool HasDeliveryDetails => HasActiveWork || HasRecommendationContent;
    public bool IsDeliveryDetailsVisible => HasDeliveryDetails && IsDeliveryDetailsExpanded;
    public bool HasSelectedRecommendationSummary => !HasActiveWork && SelectedTicket is not null;
    public bool HasDeliveryStatusNotice => !HasActiveWork && !string.IsNullOrWhiteSpace(StatusMessage)
        && (!IsWaitingForDelivery || StatusMessage is not (
            "기사 로그인 후 배달 업무를 시작할 수 있습니다." or "운행 시작을 눌러 배달을 시작하세요."
            or "배달 요청을 기다리고 있습니다." or "운행을 시작했습니다. 신규 배차 수신은 별도로 선택해 주세요."
            or "운행을 종료했습니다. 진행 중 배달은 계속 확인할 수 있습니다."));
    public bool HasTaskLocationWarning => HasMapLocationWarning && !IsWaitingForDelivery;
    public bool HasTaskDispatchNotice => HasDispatchNotice && !IsWaitingForDelivery;
    public bool IsStartWorkPrimaryAction => IsWaitingForDelivery && !IsOnDuty;
    public bool IsEnableDispatchPrimaryAction => IsWaitingForDelivery && IsOnDuty
        && DispatchIntentKnown && !ReceivesNewDispatches;
    public bool IsWaitingLocationPrimaryAction => IsWaitingForDelivery && IsOnDuty
        && DispatchIntentKnown && ReceivesNewDispatches && !HasServerDispatchRestriction && !HasCurrentLocation;
    public bool HasServerDispatchRestriction => DispatchIntentKnown && ReceivesNewDispatches
        && (_waitingEffectiveDispatchState is 운영배차실효상태Code.서버일시정지 or 운영배차실효상태Code.연결확인불가
            || (_waitingEffectiveDispatchState == 운영배차실효상태Code.조건부적합
                && _waitingEffectiveDispatchReason != "NoEffectiveEligibilityRecorded"
                && (_waitingEffectiveDispatchChangedAtUtc.HasValue || !string.IsNullOrWhiteSpace(_waitingEffectiveDispatchReason))));
    public string WaitingTitle => IsOnDuty ? "배차 대기" : "운행 시작 전";
    public string WaitingNotice => !IsOnDuty ? "운행을 시작하면 배달 요청을 받을 준비를 합니다."
        : !DispatchIntentKnown ? DispatchIntentNotice
        : !ReceivesNewDispatches ? "신규 배차 받기를 켜면 배달 요청을 받을 수 있습니다."
        : HasServerDispatchRestriction ? _waitingEffectiveDispatchState switch
        {
            운영배차실효상태Code.서버일시정지 => "신규 배차가 일시 중지되었습니다. 운영자에게 문의해 주세요.",
            운영배차실효상태Code.연결확인불가 => "배차 연결 상태를 확인할 수 없습니다. 연결을 확인한 뒤 새로고침해 주세요.",
            _ => "신규 배차 조건을 충족하지 못했습니다. 운행·배차 조건을 확인해 주세요."
        }
        : !HasCurrentLocation ? "배차를 받으려면 위치 권한과 GPS를 확인해 주세요."
        : HasDispatchNotice ? "현재 자동 배차가 중지되어 있습니다. 운영자에게 문의해 주세요."
        : _waitingEffectiveDispatchState != 운영배차실효상태Code.배차가능 ? "신규 배차 가능 여부를 확인하고 있습니다."
        : "새로운 배달 요청을 기다리고 있습니다.";

    // The server snapshot chooses the stage; permissions enable its action.
    // Busy/auth/editor guards must not replace it while a response is in flight.
    public bool IsCompletionPrimaryAction => HasDeliveryStage;
    public bool IsPickupPrimaryAction => CurrentDeliveryStage == FDriverFoodStage.PickupWaiting;
    private bool HasAllowedArrival => ActiveDelivery?.Can(음식배달가능행동Ids.기사가게도착) == true
        && !IsCurrentTargetDropoff && !ActiveDelivery.RestaurantArrivedAtUtc.HasValue
        && !string.IsNullOrWhiteSpace(ActiveDelivery.DeliveryAttemptId);
    public bool IsArrivalPrimaryAction => !IsCompletionPrimaryAction && !IsPickupPrimaryAction && HasAllowedArrival;
    public bool IsArrivalSecondaryAction => HasAllowedArrival && !IsArrivalPrimaryAction;
    public bool IsRecommendationPrimaryAction => ActiveDelivery is null && SelectedTicket is not null;
    public bool HasDeliveryPrimaryAction => IsCompletionPrimaryAction || IsPickupPrimaryAction
        || IsArrivalPrimaryAction || IsRecommendationPrimaryAction || IsStartWorkPrimaryAction
        || IsEnableDispatchPrimaryAction || IsWaitingLocationPrimaryAction;
    public bool HasDeliveryFooter => HasActiveWork || HasDeliveryPrimaryAction;

    private bool IsCurrentTargetDropoff => ActiveDelivery is not null
        && FoodDriverStagePresentation.IsDropoff(ActiveDelivery.Can(음식배달가능행동Ids.기사전달완료), ActiveDelivery.WorkStatus);
    public bool IsCurrentTargetPickup => HasRouteSelection && !IsCurrentTargetDropoff;
    public string CurrentDeliveryTargetLabel => !HasRouteSelection ? "현재 배달"
        : IsCurrentTargetDropoff ? "전달할 곳" : "픽업할 음식점";
    public string CurrentDeliveryTargetText
    {
        get
        {
            var stop = ActiveDelivery is not null
                ? IsCurrentTargetDropoff ? ActiveDelivery.Dropoff : ActiveDelivery.Pickup
                : SelectedTicket?.Pickup;
            return stop is null ? "지도에서 배달을 선택해 주세요."
                : string.IsNullOrWhiteSpace(stop.Label) ? stop.Address
                : string.IsNullOrWhiteSpace(stop.Address) ? stop.Label : $"{stop.Label}\n{stop.Address}";
        }
    }
    public string CurrentDeliveryFeeText => (ActiveDelivery?.DriverPayout ?? SelectedTicket?.DriverPayout) is decimal fee
        ? $"배달료 {fee.ToString("N0", CultureInfo.CurrentCulture)}원" : string.Empty;
    public string CurrentDeliveryStatusText => ActiveDelivery is not null ? WorkStage
        : SelectedTicket is not null ? "선택한 추천 · 수락 전" : IsOnDuty ? "추천 배달 대기" : "운행 시작 전";

    [RelayCommand]
    private void ToggleCurrentDeliveryCard()
    {
        if (HasActiveWork) IsCurrentDeliveryExpanded = !IsCurrentDeliveryExpanded;
    }

    [RelayCommand]
    private void ToggleDeliveryDetails()
    {
        if (HasDeliveryDetails) IsDeliveryDetailsExpanded = !IsDeliveryDetailsExpanded;
    }

    [RelayCommand]
    private void ToggleWorkControls() => IsWorkControlsExpanded = !IsWorkControlsExpanded;

    private void ResetFoodPresentationState()
    {
        _currentDeliveryPresentationKey = null;
        IsCurrentDeliveryExpanded = false;
        IsDeliveryDetailsExpanded = false;
        IsWorkControlsExpanded = false;
        NotifyFoodPresentationState();
    }

    private void NotifyFoodPresentationState()
    {
        var key = ActiveDelivery is null ? null
            : $"{ActiveDelivery.OfferId}|{ActiveDelivery.DeliveryAttemptId}|{CurrentDeliveryStage}";
        var contextChanged = _currentDeliveryPresentationKey != key;
        if (contextChanged)
        {
            _currentDeliveryPresentationKey = key;
            IsCurrentDeliveryExpanded = HasActiveWork;
        }
        OnPropertyChanged(nameof(IsFoodCardExpanded));
        OnPropertyChanged(nameof(CurrentDeliveryStage));
        OnPropertyChanged(nameof(CurrentDeliveryStageText));
        OnPropertyChanged(nameof(IsCurrentDeliveryContentVisible));
        OnPropertyChanged(nameof(IsCurrentDeliverySummaryVisible));
        OnPropertyChanged(nameof(HasPickupStage));
        OnPropertyChanged(nameof(HasDeliveryStage));
        OnPropertyChanged(nameof(HasDeliveryRecipient));
        OnPropertyChanged(nameof(HasPickupPreparationTime));
        OnPropertyChanged(nameof(PickupPreparationTimeText));
        OnPropertyChanged(nameof(CurrentDeliveryTargetLabel));
        OnPropertyChanged(nameof(IsCurrentTargetPickup));
        OnPropertyChanged(nameof(CurrentDeliveryTargetText));
        OnPropertyChanged(nameof(CurrentDeliveryFeeText));
        OnPropertyChanged(nameof(CurrentDeliveryStatusText));
        OnPropertyChanged(nameof(IsCompletionPrimaryAction));
        OnPropertyChanged(nameof(IsPickupPrimaryAction));
        OnPropertyChanged(nameof(IsArrivalPrimaryAction));
        OnPropertyChanged(nameof(IsArrivalSecondaryAction));
        OnPropertyChanged(nameof(IsRecommendationPrimaryAction));
        OnPropertyChanged(nameof(HasDeliveryPrimaryAction));
        OnPropertyChanged(nameof(HasDeliveryFooter));
        NotifyWaitingPresentationState();
        if (contextChanged) OnPropertyChanged(nameof(CurrentDeliveryPresentationKey));
    }

    partial void OnHasCurrentLocationChanged(bool value)
    {
        NotifyWaitingPresentationState();
        OnPropertyChanged(nameof(HasDeliveryPrimaryAction));
        OnPropertyChanged(nameof(HasDeliveryFooter));
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasDeliveryStatusNotice));

    private void ApplyWaitingDispatchAvailability(운영배차수신상태Dto? status)
    {
        _waitingEffectiveDispatchState = status?.실효상태Code ?? string.Empty;
        _waitingEffectiveDispatchReason = status?.실효사유Code ?? string.Empty;
        _waitingEffectiveDispatchChangedAtUtc = status?.실효상태변경시각Utc;
        NotifyWaitingPresentationState();
        OnPropertyChanged(nameof(HasDeliveryPrimaryAction));
        OnPropertyChanged(nameof(HasDeliveryFooter));
    }

    private void NotifyWaitingPresentationState()
    {
        OnPropertyChanged(nameof(IsWaitingForDelivery));
        OnPropertyChanged(nameof(HasDeliveryDetails));
        OnPropertyChanged(nameof(IsDeliveryDetailsVisible));
        OnPropertyChanged(nameof(HasSelectedRecommendationSummary));
        OnPropertyChanged(nameof(HasDeliveryStatusNotice));
        OnPropertyChanged(nameof(HasTaskLocationWarning));
        OnPropertyChanged(nameof(HasTaskDispatchNotice));
        OnPropertyChanged(nameof(IsStartWorkPrimaryAction));
        OnPropertyChanged(nameof(IsEnableDispatchPrimaryAction));
        OnPropertyChanged(nameof(IsWaitingLocationPrimaryAction));
        OnPropertyChanged(nameof(HasServerDispatchRestriction));
        OnPropertyChanged(nameof(WaitingTitle));
        OnPropertyChanged(nameof(WaitingNotice));
    }
}

public enum FDriverFoodStage { Waiting, PickupTravel, PickupWaiting, Delivery }

// The measured content may grow with font scaling. Cap the whole card, including
// its fixed action footer, so the remaining workspace still contains the map.
public static class FDriverFoodPresentationLayout
{
    public static double CardHeight(double usableHeight, double bodyHeight, double footerHeight, bool expanded,
        double minimumMapHeight = 0)
    {
        if (!double.IsFinite(usableHeight) || usableHeight <= 0) return 0;
        var contentHeight = Math.Max(0, double.IsFinite(bodyHeight) ? bodyHeight : 0)
            + Math.Max(0, double.IsFinite(footerHeight) ? footerHeight : 0) + 40;
        var mapReserve = Math.Max(0, double.IsFinite(minimumMapHeight) ? minimumMapHeight : 0);
        var remainingHeight = Math.Max(0, usableHeight - mapReserve);
        return Math.Min(contentHeight, Math.Min(usableHeight * (expanded ? 0.70 : 0.40), remainingHeight));
    }
}
