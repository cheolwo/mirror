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

public interface IFoodDeliveryDriverWorkspaceUseCase
{
    Task<FoodDeliveryDriverWorkspaceDto> GetAsync(string driverId, CancellationToken cancellationToken);
    Task<FoodDeliveryDailySettlementDto> GetDailySettlementAsync(
        string driverId, DateOnly? completionDateKst, CancellationToken cancellationToken);
    Task<FoodDeliveryCompletedDeliveryDetailDto?> GetCompletedDeliveryDetailAsync(
        string driverId, string settlementId, CancellationToken cancellationToken);
}

public sealed partial class FoodDeliveryDriverWorkspaceUseCase : IFoodDeliveryDriverWorkspaceUseCase
{
    private const decimal MaxPickupSeparationKm = 1.5m;
    private const decimal MaxDropoffSeparationKm = 3m;
    private const decimal MaxBundleRouteKm = 6m;
    private static readonly TimeSpan MaxReadyTimeGap = TimeSpan.FromMinutes(12);

    private readonly SsalddelContext _db;
    private readonly I음식배달기사업무Service _driverWork;
    private readonly I배달기사월정산UseCase _settlements;
    private readonly ISsalddelExecutionModePolicy _executionMode;
    private readonly IVersionFeatureFlagService _featureFlags;
    private readonly TimeProvider _timeProvider;
    private readonly FoodDeliveryCompletedDetailAccessOptions _completedDetailAccessOptions;

    public FoodDeliveryDriverWorkspaceUseCase(
        SsalddelContext db,
        I음식배달기사업무Service driverWork,
        I배달기사월정산UseCase settlements,
        ISsalddelExecutionModePolicy executionMode,
        IVersionFeatureFlagService featureFlags,
        TimeProvider? timeProvider = null,
        Microsoft.Extensions.Options.IOptions<FoodDeliveryCompletedDetailAccessOptions>? completedDetailAccessOptions = null)
    {
        _db = db;
        _driverWork = driverWork;
        _settlements = settlements;
        _executionMode = executionMode;
        _featureFlags = featureFlags;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _completedDetailAccessOptions = completedDetailAccessOptions?.Value ?? new FoodDeliveryCompletedDetailAccessOptions();
    }

    public async Task<FoodDeliveryDriverWorkspaceDto> GetAsync(
        string driverId,
        CancellationToken cancellationToken)
    {
        var workItems = await _driverWork.제안조회Async(driverId, cancellationToken);
        var offers = workItems
            .Where(x => x.Status == DriverWorkOfferStatus.Recommended)
            .Select(ToOffer)
            .ToArray();
        var activeWork = workItems
            .Where(x => x.Status is DriverWorkOfferStatus.Accepted
                or DriverWorkOfferStatus.MovingToPickup
                or DriverWorkOfferStatus.MovingToDropoff)
            .ToDictionary(x => x.OfferId, StringComparer.Ordinal);
        var activeIds = activeWork.Keys.ToArray();
        var activeQueues = activeIds.Length == 0
            ? []
            : await _db.운송원장
                .AsNoTracking()
                .Where(x => Enumerable.Contains(activeIds, x.의뢰Id)
                            && x.배차업무유형 == 상태값.배차업무유형.음식배달
                            && (x.기사_운송자 == driverId || x.확정기사Id == driverId))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
        var activeAttempts = activeIds.Length == 0
            ? new Dictionary<string, 살뜰.도메인.음식.음식배달시도>(StringComparer.Ordinal)
            : (await _db.음식배달시도
                .AsNoTracking()
                .Where(x => Enumerable.Contains(activeIds, x.제안Id)
                            && x.기사Id == driverId
                            && x.중단시각Utc == null
                            && x.전달완료시각Utc == null)
                .OrderByDescending(x => x.시도순번)
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.제안Id, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var activeOrderNumbers = activeQueues.Select(ResolveOrderNo).Distinct().ToArray();
        var preparationByOrder = activeOrderNumbers.Length == 0
            ? new Dictionary<string, 음식주문현재조리상태>(StringComparer.Ordinal)
            : (await _db.음식주문.AsNoTracking().Include(x => x.상태이력)
                .Where(x => Enumerable.Contains(activeOrderNumbers, x.주문번호))
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.주문번호, 음식주문현재조리Policy.계산, StringComparer.Ordinal);
        var active = activeQueues
            .Where(x => activeWork.ContainsKey(x.의뢰Id))
            .Select(x => ToActiveDelivery(
                x,
                activeWork[x.의뢰Id],
                activeAttempts.GetValueOrDefault(x.의뢰Id),
                preparationByOrder.GetValueOrDefault(ResolveOrderNo(x))))
            .ToArray();

        var settlementResult = await _settlements.당월조회Async(driverId, driverId, cancellationToken);
        var settlement = settlementResult.IsSuccess
            ? settlementResult.Value
            : new 배달기사월정산응답
            {
                기사Id = driverId,
                년도 = DateTime.UtcNow.Year,
                월 = DateTime.UtcNow.Month
            };
        var dispatchAutomationEnabled = _executionMode.IsOperational
                                        && _featureFlags.IsEnabled(
                                            VersionFeatureFlagKeys.FoodDeliveryWorkflow);
        var orderSettlements = await _db.음식주문기사정산
            .AsNoTracking()
            .Include(x => x.지급검증목록)
            .Where(x => x.기사Id == driverId)
            .OrderByDescending(x => x.전달완료시각Utc)
            .Take(40)
            .ToListAsync(cancellationToken);

        return new FoodDeliveryDriverWorkspaceDto
        {
            DriverId = driverId,
            Recommendations = offers,
            ActiveDeliveries = active,
            BundleCandidates = BuildBundleCandidates(offers),
            MaxActiveDeliveries = 음식배달기사활성업무Policy.MaxActiveDeliveries,
            Settlement = settlement,
            OrderSettlements = orderSettlements.Select(x => 음식주문기사정산Recorder.ToDto(
                x, currentExecutionModeCode: _executionMode.Mode.ToString())).ToArray(),
            DispatchAutomationEnabled = dispatchAutomationEnabled,
            DispatchAutomationNotice = ResolveDispatchAutomationNotice(dispatchAutomationEnabled),
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private string ResolveDispatchAutomationNotice(bool enabled)
    {
        if (enabled)
        {
            return "자동 기사 추천이 활성화되어 있습니다.";
        }

        return !_executionMode.IsOperational
            ? "시뮬레이션 모드에서는 자동 기사 추천이 실행되지 않습니다."
            : "음식 배달 기능이 비활성화되어 자동 기사 추천이 실행되지 않습니다.";
    }

    private static FoodDeliveryDriverOfferDto ToOffer(DriverWorkOfferDto offer)
        => new()
        {
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

    private static FoodDeliveryDriverActiveDeliveryDto ToActiveDelivery(
        살뜰.도메인.운송.운송원장 transport,
        DriverWorkOfferDto offer,
        살뜰.도메인.음식.음식배달시도? attempt,
        음식주문현재조리상태? preparation)
        => new()
        {
            TransportId = transport.Id,
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

    private static IReadOnlyList<FoodDeliveryBundleCandidateDto> BuildBundleCandidates(
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
