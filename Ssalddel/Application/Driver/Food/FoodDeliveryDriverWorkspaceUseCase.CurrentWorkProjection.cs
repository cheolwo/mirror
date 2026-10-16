using Ssalddel.Contracts.Common.Metadata;

using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Application.Food;
using Ssalddel.Services.Food;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;

namespace Ssalddel.Application.Driver.Food;

/// <summary>DB와 client 상태를 변경하지 않는 현재 업무 표시 변환입니다.</summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "확인된 제안·운송·시도 자료를 현재 업무 응답 값으로 변환한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 54,
    StepKey = "application.current-work-projector",
    DependsOnStepKeys = ["contract.offer", "contract.active-delivery"],
    ExecutionStage = SsalddelCodeExecutionStage.Projection,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "이 projection은 입력 사본만 변환한다. DB 조회나 상태 변경·외부 호출을 실행하지 않으며 전달·수령·실지급 상태를 합치지 않는다.")]
internal static class FoodDeliveryCurrentWorkProjector
{
    public static FoodDeliveryDriverOfferDto ToOffer(DriverWorkOfferDto offer)
        => new()
        {
            RecommendationRound = offer.RecommendationRound,
            WorkKind = offer.WorkType,
            AssignedActivities = offer.AssignedActivities,
            Pricing = offer.Pricing,
            OfferId = offer.OfferId,
            OrderSummary = offer.Title,
            RestaurantName = offer.Pickup.Label,
            Pickup = ToStop(offer.Pickup),
            Dropoff = ToStop(offer.Dropoff),
            DriverPayout = offer.DriverPayout,
            WeatherSurcharge = offer.WeatherSurcharge,
            WeatherSurchargeApplied = offer.WeatherSurchargeApplied,
            PricingPolicyRevision = offer.PricingPolicyRevision ?? string.Empty,
            WeatherObservedAtUtc = offer.WeatherObservedAtUtc?.UtcDateTime,
            DistanceKm = offer.DistanceKm.HasValue ? (decimal)offer.DistanceKm.Value : null,
            RecommendationReason = offer.RecommendationReason,
            ExpiresAtUtc = offer.ExpiresAtUtc?.UtcDateTime,
            AvailableActions = 음식배달가능행동Projector.기사제안용(offer.ExpiresAtUtc?.UtcDateTime),
            ExecutionProfile = offer.ExecutionProfile
                               ?? 운송실행프로필Factory.Create(
                                   살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.음식점주문,
                                   상태값.배차업무유형.음식배달)
        };

    public static FoodDeliveryDriverActiveDeliveryDto ToActiveDelivery(
        살뜰.도메인.운송.운송원장 transport,
        DriverWorkOfferDto offer,
        살뜰.도메인.음식.음식배달시도? attempt,
        음식주문현재조리상태? preparation)
        => new()
        {
            WorkKind = offer.WorkType,
            AssignedActivities = offer.AssignedActivities,
            Pricing = offer.Pricing,
            TransportId = transport.Id,
            ExperienceSummaries = offer.ExperienceSummaries,
            OrderNo = !string.IsNullOrWhiteSpace(attempt?.주문번호) ? attempt.주문번호 : ResolveOrderNo(transport),
            OfferId = offer.OfferId,
            OrderSummary = offer.Title,
            RestaurantName = offer.Pickup.Label,
            Pickup = ToStop(offer.Pickup),
            Dropoff = ToStop(offer.Dropoff),
            DriverPayout = offer.DriverPayout,
            WeatherSurcharge = offer.WeatherSurcharge,
            WeatherSurchargeApplied = offer.WeatherSurchargeApplied,
            PricingPolicyRevision = offer.PricingPolicyRevision ?? string.Empty,
            WeatherObservedAtUtc = offer.WeatherObservedAtUtc?.UtcDateTime,
            TransportStatus = transport.상태,
            WorkStatus = offer.Status,
            DeliveryAttemptId = attempt?.시도StableId ?? string.Empty,
            AttemptRevision = attempt?.Revision ?? 0,
            RestaurantArrivedAtUtc = attempt?.가게도착시각Utc,
            DisplayedPreparationReadyAtUtc = attempt?.표시준비예정시각Utc,
            PreparationDelayEligibleAtUtc = attempt?.표시준비예정시각Utc?.AddMinutes(10),
            IsPreparationDelayRedispatch = attempt?.조리지연재배차여부 ?? false,
            CurrentPreparationRound = preparation?.Round ?? 1,
            CurrentPickupReadyAtUtc = preparation?.ReadyAtUtc,
            RecookingRequestedAtUtc = preparation?.RecookingRequestedAtUtc,
            ExecutionProfile = offer.ExecutionProfile ?? 운송실행프로필Factory.Create(transport),
            Recipient = ToRecipient(offer.Recipient),
            AvailableActions = 음식배달가능행동Projector.기사배달용(
                offer.Status,
                attempt?.Revision ?? 0,
                attempt?.가게도착시각Utc,
                pickupReady: preparation?.ReadyAtUtc.HasValue == true),
            UpdatedAtUtc = transport.UpdatedAt
        };

    private static string ResolveOrderNo(살뜰.도메인.운송.운송원장 transport)
        => string.IsNullOrWhiteSpace(transport.원본의뢰Id) ? transport.의뢰Id : transport.원본의뢰Id;

    private static FoodDeliveryDriverRecipientDto ToRecipient(DriverWorkRecipientDto? recipient)
        => recipient is null
            ? new FoodDeliveryDriverRecipientDto()
            : new FoodDeliveryDriverRecipientDto
            {
                DisplayName = recipient.DisplayName,
                ContactPhone = recipient.ContactPhone,
                DeliveryInstructions = recipient.DeliveryInstructions,
                OrdererIsRecipient = recipient.OrdererIsRecipient
            };

    private static FoodDeliveryDriverStopDto ToStop(DriverWorkStopDto stop)
        => new()
        {
            Label = stop.Label,
            Address = stop.Address,
            Latitude = (decimal)stop.Latitude,
            Longitude = (decimal)stop.Longitude,
            TargetAtUtc = stop.TargetTime?.UtcDateTime
        };

}
