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

/// <summary>현재의 두 건 묶음 표시 기준입니다. 실제 수락 검증과 정책을 변경하지 않습니다.</summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "현재 제안 자료에서 표시할 묶음 후보를 순수 계산한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 53,
    StepKey = "application.current-bundle-policy",
    DependsOnStepKeys = ["contract.offer"],
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "후보 생성은 자동 수락·배차 확정·영속 저장이 아니다.")]
internal static class FoodDeliveryCurrentBundlePolicy
{
    private const decimal MaxPickupSeparationKm = 1.5m;
    private const decimal MaxDropoffSeparationKm = 3m;
    private const decimal MaxBundleRouteKm = 6m;
    private static readonly TimeSpan MaxReadyTimeGap = TimeSpan.FromMinutes(12);

    public static IReadOnlyList<FoodDeliveryBundleCandidateDto> Build(
        IReadOnlyList<FoodDeliveryDriverOfferDto> offers)
    {
        var remaining = offers
            .Where(HasCoordinates)
            .OrderBy(x => x.Pickup.TargetAtUtc ?? DateTime.MaxValue)
            .ToList();
        var result = new List<FoodDeliveryBundleCandidateDto>();

        while (remaining.Count > 1)
        {
            var first = remaining[0];
            remaining.RemoveAt(0);
            var second = remaining.FirstOrDefault(candidate => CanBundle(first, candidate));
            if (second is null)
            {
                continue;
            }

            remaining.Remove(second);
            var estimatedRouteKm = EstimateBundleRouteKm(first, second);
            result.Add(new FoodDeliveryBundleCandidateDto
            {
                BundleId = $"bundle:{first.OfferId}:{second.OfferId}",
                OfferIds = [first.OfferId, second.OfferId],
                Title = $"{first.RestaurantName} + {second.RestaurantName}",
                Reason = "조리 완료 시각과 픽업·전달 동선이 가까운 2건 묶음입니다.",
                TotalPayout = first.DriverPayout + second.DriverPayout,
                EstimatedRouteKm = Math.Round(estimatedRouteKm, 1)
            });
        }

        return result;
    }

    private static bool CanBundle(FoodDeliveryDriverOfferDto first, FoodDeliveryDriverOfferDto second)
    {
        var readyGap = first.Pickup.TargetAtUtc.HasValue && second.Pickup.TargetAtUtc.HasValue
            ? (first.Pickup.TargetAtUtc.Value - second.Pickup.TargetAtUtc.Value).Duration()
            : TimeSpan.Zero;
        return readyGap <= MaxReadyTimeGap
               && DistanceKm(first.Pickup, second.Pickup) <= MaxPickupSeparationKm
               && DistanceKm(first.Dropoff, second.Dropoff) <= MaxDropoffSeparationKm
               && EstimateBundleRouteKm(first, second) <= MaxBundleRouteKm;
    }

    private static decimal EstimateBundleRouteKm(
        FoodDeliveryDriverOfferDto first,
        FoodDeliveryDriverOfferDto second)
        => DistanceKm(first.Pickup, second.Pickup)
           + DistanceKm(second.Pickup, first.Dropoff)
           + DistanceKm(first.Dropoff, second.Dropoff);

    private static decimal DistanceKm(FoodDeliveryDriverStopDto first, FoodDeliveryDriverStopDto second)
    {
        var lat1 = DegreesToRadians((double)first.Latitude!.Value);
        var lat2 = DegreesToRadians((double)second.Latitude!.Value);
        var dLat = lat2 - lat1;
        var dLon = DegreesToRadians((double)(second.Longitude!.Value - first.Longitude!.Value));
        var a = Math.Pow(Math.Sin(dLat / 2d), 2d)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2d), 2d);
        return (decimal)(6371d * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a)));
    }

    private static bool HasCoordinates(FoodDeliveryDriverOfferDto offer)
        => offer.Pickup.Latitude.HasValue && offer.Pickup.Longitude.HasValue
           && offer.Dropoff.Latitude.HasValue && offer.Dropoff.Longitude.HasValue;

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
}
