using System.Globalization;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>확정된 응답을 현재 단계의 카드로 변환합니다. 조회·명령·장치 상태를 소유하지 않습니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.ViewModel,
    "확정된 현재 배달 응답을 단계별 목적지·요청·주문 카드와 지도 값으로 변환",
    StepKey = "ui.workspace-presentation", FlowOrder = 206, ExecutionStage = SsalddelCodeExecutionStage.Presentation,
    DependsOnStepKeys = new[] { "contract.current-work", "ui.stage-presentation", "ui.food-presentation" },
    ReadsFrom = SsalddelCodeDataScope.ClientPresentation, Effects = SsalddelCodeEffect.None,
    Boundary = "조회·명령·장치·영속 저장을 소유하지 않는다. 미확인 거리와 0을 구분하며 정산을 현재 조회에 섞지 않는다.")]
public static class FoodDriverWorkspacePresentation
{
    public static RoleWorkspaceItem Offer(FoodDeliveryDriverOfferDto offer, DateTime nowUtc)
    {
        var choices = new[] { (음식배달가능행동Ids.기사제안수락, "배달 수락", true), (음식배달가능행동Ids.기사제안거절, "제안 거절", false) };
        var actions = choices.Select(value => FoodWorkspacePresentation.Available(offer.AvailableActions, value.Item1, value.Item2, value.Item3, automaticRefresh: true, nowUtc: nowUtc))
            .OfType<RoleWorkspaceAction>().Select(value => offer.ExpiresAtUtc is { } expiry && expiry <= nowUtc
                ? value with { Enabled = false, DisabledReason = "제안이 만료되었습니다. 최신 제안을 자동으로 확인합니다." } : value).ToArray();
        return new(offer.OfferId, offer.RestaurantName, "배달 제안 · 수락 전", offer.OrderSummary,
            [FoodWorkspacePresentation.Section("픽업할 음식점", ("음식점", offer.RestaurantName), ("픽업 주소", offer.Pickup.Address)),
             FoodWorkspacePresentation.Section("배달 조건", ("배달료", FoodWorkspacePresentation.Money(offer.DriverPayout)),
                ("제안 거리", offer.DistanceKm is { } distance ? distance.ToString("0.###", CultureInfo.InvariantCulture) + "km" : "미확인"),
                ("제안 만료", FoodWorkspacePresentation.Time(offer.ExpiresAtUtc)), ("추천 안내", offer.RecommendationReason))], actions,
            Stops(offer.OfferId, offer.Pickup, offer.Dropoff), Summary: new("픽업지", FoodWorkspacePresentation.Value(offer.Pickup.Address, "미확인"),
                [new("배달료", Payout(offer.DriverPayout)), new("제안 거리", offer.DistanceKm is >= 0
                    ? offer.DistanceKm.Value.ToString("0.###", CultureInfo.InvariantCulture) + " km" : "미확인")]));
    }

    public static RoleWorkspaceItem Active(FoodDeliveryDriverActiveDeliveryDto active, DateTime nowUtc)
    {
        var stage = FoodDriverStagePresentation.Resolve(active, nowUtc);
        var primary = FoodDriverStagePresentation.PrimaryAction(stage);
        var primaryLabel = FoodDriverStagePresentation.PrimaryActionLabel(stage);
        var actions = new List<RoleWorkspaceAction>
        {
            FoodWorkspacePresentation.Available(active.AvailableActions, primary, primaryLabel, true, automaticRefresh: true, nowUtc: nowUtc)
                ?? new(primary, primaryLabel, IsPrimary: true, Enabled: false,
                    DisabledReason: FoodDriverStagePresentation.Guide(active, stage, nowUtc))
        };
        if (primary == 음식배달가능행동Ids.기사가게도착 && string.IsNullOrWhiteSpace(active.DeliveryAttemptId))
            actions[0] = actions[0] with { Enabled = false, DisabledReason = "현재 배달 시도를 자동으로 확인하고 있습니다." };
        if (stage != FoodDriverStage.Delivery && primary != 음식배달가능행동Ids.기사가게도착 && !active.RestaurantArrivedAtUtc.HasValue
            && !string.IsNullOrWhiteSpace(active.DeliveryAttemptId)
            && FoodWorkspacePresentation.Available(active.AvailableActions, 음식배달가능행동Ids.기사가게도착,
                FoodDriverStagePresentation.PrimaryActionLabel(FoodDriverStage.PickupTravel), automaticRefresh: true, nowUtc: nowUtc) is { } arrival) actions.Add(arrival);
        if (FoodWorkspacePresentation.Available(active.AvailableActions, 음식배달가능행동Ids.기사배달중단, "배달 중단",
            route: FoodWorkspacePresentation.ActionRoute("food-driver", 음식배달가능행동Ids.기사배달중단, active.OfferId), automaticRefresh: true, nowUtc: nowUtc) is { } interrupt) actions.Add(interrupt);
        IReadOnlyList<RoleWorkspaceSection> sections;
        if (stage == FoodDriverStage.Delivery)
        {
            sections = [FoodWorkspacePresentation.Section("전달지", ("전달 주소", FoodDriverStagePresentation.Address(active.Dropoff.Address, stage))),
                FoodWorkspacePresentation.Section("고객 요청 사항", ("전달 요청", FoodWorkspacePresentation.Value(active.Recipient.DeliveryInstructions, "별도 전달 요청 없음")),
                    ("수령인", active.Recipient.DisplayName), ("수령 관계", active.Recipient.OrdererIsRecipient ? "주문자 본인 수령" : "지정 수령인"),
                    ("연락처", FoodWorkspacePresentation.MaskPhone(active.Recipient.ContactPhone))), Order(active)];
        }
        else
        {
            var recooking = active.CurrentPreparationRound > 1 || active.RecookingRequestedAtUtc.HasValue;
            sections = [FoodWorkspacePresentation.Section("음식점", ("음식점", active.RestaurantName),
                    ("준비 예정", FoodWorkspacePresentation.Time(active.DisplayedPreparationReadyAtUtc)),
                    ("현재 음식", recooking ? "재조리 음식" : "첫 조리 음식"),
                    ("조리 회차", active.CurrentPreparationRound > 0 ? $"{active.CurrentPreparationRound}회차" : "미확인"),
                    ("준비 완료", FoodWorkspacePresentation.Time(active.CurrentPickupReadyAtUtc))),
                FoodWorkspacePresentation.Section("픽업 주소", ("픽업할 곳", FoodDriverStagePresentation.Address(active.Pickup.Address, stage))), Order(active)];
        }
        return new(active.OfferId, active.RestaurantName, FoodDriverStagePresentation.Text(stage, active.RestaurantArrivedAtUtc.HasValue),
            active.OrderSummary, sections, actions, Stops(active.OfferId, active.Pickup, active.Dropoff), IsCurrent: true,
            Summary: CurrentSummary(active, stage, nowUtc));
    }

    private static RoleWorkspaceSummary CurrentSummary(FoodDeliveryDriverActiveDeliveryDto active, FoodDriverStage stage, DateTime nowUtc)
    {
        if (stage == FoodDriverStage.Delivery)
            return new("전달지", FoodDriverStagePresentation.Address(active.Dropoff.Address, stage),
                [new("배달료", Payout(active.DriverPayout))],
                new("전달 요청", FoodWorkspacePresentation.Value(active.Recipient.DeliveryInstructions, "별도 전달 요청 없음")),
                ExperienceLines: DeliveryExperiencePresentation.DriverLines(active.ExperienceSummaries, true));
        return new("픽업지", FoodDriverStagePresentation.Address(active.Pickup.Address, stage),
            [new("배달료", Payout(active.DriverPayout)), new("준비 예정", FoodWorkspacePresentation.Time(active.DisplayedPreparationReadyAtUtc))],
            stage == FoodDriverStage.PickupWaiting ? new("픽업 확인", FoodDriverStagePresentation.Guide(active, stage, nowUtc)) : null,
            ExperienceLines: DeliveryExperiencePresentation.DriverLines(active.ExperienceSummaries, false));
    }

    private static string Payout(decimal payout) => payout > 0 ? FoodWorkspacePresentation.Money(payout) : "미확인";

    private static RoleWorkspaceSection Order(FoodDeliveryDriverActiveDeliveryDto active) => FoodWorkspacePresentation.Section("주문 정보",
        ("주문", active.OrderSummary), ("배달료", FoodWorkspacePresentation.Money(active.DriverPayout)));
    private static IReadOnlyList<NeighborhoodMapMarker> Stops(string id, FoodDeliveryDriverStopDto pickup, FoodDeliveryDriverStopDto dropoff)
        => FoodWorkspacePresentation.Marker(id + ":pickup", "픽업", NeighborhoodMapMarkerKinds.Pickup, pickup.Latitude, pickup.Longitude)
            .Concat(FoodWorkspacePresentation.Marker(id + ":dropoff", "전달", NeighborhoodMapMarkerKinds.Dropoff, dropoff.Latitude, dropoff.Longitude)).ToArray();
}
