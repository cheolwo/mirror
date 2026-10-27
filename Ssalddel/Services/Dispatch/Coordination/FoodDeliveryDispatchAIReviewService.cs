using Ssalddel.Contracts.Admin.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using static 살뜰.Services.Dispatch.Coordination.음식배달AI검토Catalog;

namespace 살뜰.Services.Dispatch.Coordination;

public interface IFoodDeliveryDispatchAIReviewService
{
    Task<FoodDeliveryDispatchAIReviewWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default);

    Task<FoodDeliveryDispatchAIReviewDecisionResponse> RecordDecisionAsync(
        FoodDeliveryDispatchAIReviewDecisionRequest request,
        string? adminUser,
        CancellationToken cancellationToken = default);
}

public sealed class FoodDeliveryDispatchAIReviewService : IFoodDeliveryDispatchAIReviewService
{
    private readonly ISsalddelFoodOrderStore _orderStore;
    private readonly I음식멀티배차조합Service _bundleService;
    private readonly I음식배달권실행공간Store _deliveryScopeStore;
    private readonly IDriverLocationStore _driverLocationStore;
    private readonly I배차AI판단사례LedgerStore _judgmentLedgerStore;

    public FoodDeliveryDispatchAIReviewService(
        ISsalddelFoodOrderStore orderStore,
        I음식멀티배차조합Service bundleService,
        I음식배달권실행공간Store deliveryScopeStore,
        IDriverLocationStore driverLocationStore,
        I배차AI판단사례LedgerStore judgmentLedgerStore)
    {
        _orderStore = orderStore;
        _bundleService = bundleService;
        _deliveryScopeStore = deliveryScopeStore;
        _driverLocationStore = driverLocationStore;
        _judgmentLedgerStore = judgmentLedgerStore;
    }

    public async Task<FoodDeliveryDispatchAIReviewWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var rawOrders = _orderStore.GetOrders().Items
            .Where(IsReviewTarget)
            .OrderBy(x => x.조리예상완료시각Utc ?? x.CreatedAt)
            .Take(12)
            .ToList();

        var useJungnangScenario = rawOrders.Count == 0 || rawOrders.All(IsDefaultFoodOrderSample);
        var orders = useJungnangScenario
            ? CreateJungnangScenarioOrders(DateTime.UtcNow)
            : rawOrders.Select(EnrichOrder).ToList();
        var source = useJungnangScenario ? "jungnang-scope-sample" : "actual";

        var drivers = await BuildDriversAsync(cancellationToken);
        if (drivers.Count == 0)
        {
            drivers = CreateSampleDrivers(DateTime.UtcNow);
            source = source == "actual" ? "actual-with-sample-drivers" : source;
        }

        var bundles = BuildBundles(orders, drivers);
        var assignments = BuildAssignments(bundles);

        return new FoodDeliveryDispatchAIReviewWorkspaceDto
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Source = source,
            PrimaryDeliveryScopeKey = PrimaryReviewScope.Key,
            PrimaryDeliveryScopeName = PrimaryReviewScope.Name,
            AdjacentDeliveryScopeKeys = AdjacentReviewScopes.Select(x => x.Key).ToList(),
            AdjacentDeliveryScopeNames = AdjacentReviewScopes.Select(x => x.Name).ToList(),
            Orders = orders.Select(ToDto).ToList(),
            Drivers = drivers,
            Bundles = bundles,
            Assignments = assignments,
            Notes =
            [
                "음식점 주문은 화물 운송과 분리된 RAG 출처(admin-food-delivery-ai-review)로 저장합니다.",
                "현재 검토 기준은 중랑구를 주요 배달권으로 두고 동대문구, 광진구, 노원구, 구리시를 인접 배달권으로 둡니다.",
                "AI 판단은 1단계 고객 전달권 묶음 판단, 2단계 F드라이버 배정 판단으로 나눠 표시합니다.",
                "배차 확정 적용은 별도 실행 서비스 연결 단계에서 처리합니다."
            ]
        };
    }

    public async Task<FoodDeliveryDispatchAIReviewDecisionResponse> RecordDecisionAsync(
        FoodDeliveryDispatchAIReviewDecisionRequest request,
        string? adminUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orderNos = request.OrderNos
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (orderNos.Count == 0)
        {
            throw new ArgumentException("판정할 음식 주문을 하나 이상 선택해야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.DriverId))
        {
            throw new ArgumentException("판정할 배달 기사를 선택해야 합니다.");
        }

        var accepted = request.Accepted ? "승인" : "보류";
        var manual = request.ManualBundle ? "수동 묶음" : "AI 제안";
        var bundleLabel = string.IsNullOrWhiteSpace(request.BundleKey)
            ? string.Join("+", orderNos)
            : request.BundleKey.Trim();
        var note = string.IsNullOrWhiteSpace(request.AdminNote)
            ? $"{manual} {bundleLabel}을 {accepted}했습니다."
            : request.AdminNote.Trim();

        var item = await _judgmentLedgerStore.CreateAsync(
            new DispatchAIJudgmentCaseCreateRequest
            {
                Title = $"{manual} 음식배달 배차 판단: {bundleLabel}",
                RelatedOS = "음식 배달 OS",
                Keywords =
                [
                    "음식배달OS",
                    "음식점주문",
                    "F드라이버",
                    "운영자판정",
                    request.ManualBundle ? "수동묶음" : "AI제안",
                    request.Accepted ? "승인" : "보류",
                    "조리완료",
                    "픽업",
                    "고객전달",
                    "음식멀티배차",
                    "중랑구",
                    "인접배달권"
                ],
                SituationSummary = $"운영자가 음식배달 AI 검토 화면에서 중랑구 주요 배달권과 동대문구·광진구·노원구·구리시 인접 배달권 기준으로 주문 {string.Join(", ", orderNos)}와 배달 기사 {request.DriverId.Trim()}의 위치, 묶음, 조리 완료 기준 시간을 확인했습니다.",
                JudgmentSummary = note,
                UserDecision = request.DecisionType,
                BalancedDecision = $"{manual} {accepted}",
                Source = "admin-food-delivery-ai-review",
                Active = true
            },
            adminUser,
            cancellationToken);

        return new FoodDeliveryDispatchAIReviewDecisionResponse
        {
            CaseId = item.CaseId,
            Message = $"{item.CaseId} 음식배달 판단 사례를 RAG 저장소에 저장했습니다."
        };
    }

    private List<FoodDeliveryDispatchAIReviewBundleDto> BuildBundles(
        IReadOnlyList<FoodDeliveryOrderReviewItem> orders,
        IReadOnlyList<FoodDeliveryDispatchAIReviewDriverDto> drivers)
    {
        var jobs = orders.Select(ToJob).ToArray();
        if (jobs.Length == 0)
        {
            return [];
        }

        var candidates = _bundleService.조합생성(
            new 멀티배차조합요청(
                jobs,
                최대묶음크기: 2,
                최대조합수: 24,
                좌표근사총거리상한Km: 6m,
                같은배달권멀티허용: true,
                인접배달권멀티허용: true,
                비인접배달권멀티허용: false));

        var suggestedKey = candidates
            .Where(x => x.조합가능여부)
            .OrderByDescending(x => x.배차묶음유형 == "멀티배차")
            .ThenByDescending(x => x.조합점수)
            .Select(x => x.조합키)
            .FirstOrDefault();

        return candidates
            .Take(24)
            .Select(candidate => ToBundleDto(candidate, drivers, string.Equals(candidate.조합키, suggestedKey, StringComparison.Ordinal)))
            .ToList();
    }

    private static List<FoodDeliveryDispatchAIReviewAssignmentDto> BuildAssignments(
        IReadOnlyList<FoodDeliveryDispatchAIReviewBundleDto> bundles)
    {
        var selected = bundles.FirstOrDefault(x => x.IsAISuggested && !string.IsNullOrWhiteSpace(x.SuggestedDriverId));
        if (selected is null)
        {
            return [];
        }

        return selected.OrderNos
            .Select((orderNo, index) => new FoodDeliveryDispatchAIReviewAssignmentDto
            {
                OrderNo = orderNo,
                DriverId = selected.SuggestedDriverId!,
                Order = index + 1,
                Score = selected.Score,
                Reason = $"{selected.BundleType} 후보를 {selected.SuggestedDriverId} 기사에게 추천합니다.",
                Badges = selected.Badges.ToList()
            })
            .ToList();
    }

    private async Task<List<FoodDeliveryDispatchAIReviewDriverDto>> BuildDriversAsync(CancellationToken cancellationToken)
    {
        var spaces = await _deliveryScopeStore.SnapshotAsync(cancellationToken);
        var drivers = new List<FoodDeliveryDispatchAIReviewDriverDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in spaces.SelectMany(x => x.운행중기사Ids.Select(driverId => (Space: x, DriverId: driverId))))
        {
            if (!seen.Add(item.DriverId))
            {
                continue;
            }

            DriverLocationSnapshot? location = null;
            if (_driverLocationStore.TryGetLatest(item.DriverId, out var snapshot))
            {
                location = snapshot;
            }

            var point = location is null ? null : new 배차경로좌표(location.Latitude, location.Longitude);
            var scope = ResolveScopeByKey(item.Space.배달권키)
                        ?? (point is null
                            ? new FoodDeliveryReviewScope(item.Space.배달권키, item.Space.배달권키, "기타 배달권")
                            : ResolveFoodDeliveryScope(null, point));
            drivers.Add(new FoodDeliveryDispatchAIReviewDriverDto
            {
                DriverId = item.DriverId,
                DriverName = item.DriverId,
                DrivingStatus = location?.DrivingStatus ?? "운행중",
                Latitude = location?.Latitude,
                Longitude = location?.Longitude,
                DeliveryScopeKey = scope.Key,
                DeliveryScopeName = scope.Name,
                DeliveryScopeRole = scope.Role,
                LastLocationReceivedAtUtc = location?.ReceivedAtUtc
            });
        }

        return drivers;
    }

    private static FoodDeliveryDispatchAIReviewBundleDto ToBundleDto(
        멀티배차조합후보 candidate,
        IReadOnlyList<FoodDeliveryDispatchAIReviewDriverDto> drivers,
        bool isSuggested)
    {
        var suggestedDriverId = isSuggested ? ResolveNearestDriver(candidate, drivers) : null;
        return new FoodDeliveryDispatchAIReviewBundleDto
        {
            BundleKey = candidate.조합키,
            BundleType = candidate.배차묶음유형,
            OrderNos = candidate.의뢰Ids.ToList(),
            BundleSize = candidate.의뢰Ids.Count,
            IsBundleAvailable = candidate.조합가능여부,
            IsAISuggested = isSuggested,
            SuggestedDriverId = suggestedDriverId,
            Score = candidate.조합점수,
            PickupDistanceKm = candidate.상차지간거리Km,
            DropoffDistanceKm = candidate.하차지간거리Km,
            ExpectedRouteDistanceKm = candidate.묶음내예상거리Km,
            Badges = candidate.배지.ToList(),
            Warnings = candidate.경고.ToList(),
            ExclusionReasons = candidate.제외사유?.ToList() ?? [],
            Reason = BuildBundleReason(candidate, suggestedDriverId),
            BundleDecisionSummary = BuildBundleDecisionSummary(candidate),
            DriverAssignmentDecisionSummary = BuildDriverAssignmentDecisionSummary(candidate, drivers, suggestedDriverId)
        };
    }

    private static string BuildBundleReason(멀티배차조합후보 candidate, string? suggestedDriverId)
    {
        if (!candidate.조합가능여부)
        {
            return candidate.제외사유 is { Count: > 0 }
                ? $"음식배달 묶음 조건을 만족하지 않습니다. {string.Join(" ", candidate.제외사유)}"
                : "음식배달 묶음 조건을 만족하지 않습니다.";
        }

        var driverText = string.IsNullOrWhiteSpace(suggestedDriverId)
            ? "배달 기사 후보 미정"
            : $"배달 기사 {suggestedDriverId} 후보";
        var distanceText = candidate.묶음내예상거리Km.HasValue
            ? $"예상 묶음 거리 {candidate.묶음내예상거리Km:0.##}km"
            : "예상 묶음 거리 미정";
        return $"{candidate.배차묶음유형} 후보입니다. {distanceText}, 점수 {candidate.조합점수:0.##}, {driverText}.";
    }

    private static string BuildBundleDecisionSummary(멀티배차조합후보 candidate)
    {
        var customerScopes = candidate.작업목록
            .Select(x => ResolveScopeDisplay(x.하차배달권키))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var scopeText = customerScopes.Length == 0 ? "고객 전달권 미정" : string.Join(", ", customerScopes);
        var distanceText = candidate.묶음내예상거리Km.HasValue
            ? $"{candidate.묶음내예상거리Km:0.##}km"
            : "미정";

        return candidate.조합가능여부
            ? $"1단계 AI: 고객 전달권 {scopeText}을 중랑구 주요/인접 배달권 기준으로 검토했고, 묶음 예상거리 {distanceText} 조건에서 {candidate.배차묶음유형} 후보로 판단했습니다."
            : $"1단계 AI: 고객 전달권 {scopeText}은 현재 중랑구 주요/인접 배달권 묶음 조건에 맞지 않아 제외 후보로 판단했습니다.";
    }

    private static string BuildDriverAssignmentDecisionSummary(
        멀티배차조합후보 candidate,
        IReadOnlyList<FoodDeliveryDispatchAIReviewDriverDto> drivers,
        string? suggestedDriverId)
    {
        if (string.IsNullOrWhiteSpace(suggestedDriverId))
        {
            return "2단계 AI: 아직 추천할 F드라이버를 고르지 못했습니다.";
        }

        var driver = drivers.FirstOrDefault(x => string.Equals(x.DriverId, suggestedDriverId, StringComparison.Ordinal));
        var driverName = driver?.DriverName ?? suggestedDriverId;
        var driverScope = string.IsNullOrWhiteSpace(driver?.DeliveryScopeName)
            ? "배달권 미정"
            : $"{driver.DeliveryScopeName}({driver.DeliveryScopeRole})";

        var firstPickup = candidate.작업목록.FirstOrDefault()?.픽업주소 ?? "픽업지 미정";
        return $"2단계 AI: {driverName}의 현재 권역 {driverScope}과 첫 픽업지 {firstPickup} 접근성을 기준으로 이 기사에게 배정하는 판단을 만들었습니다.";
    }

    private static string? ResolveNearestDriver(
        멀티배차조합후보 candidate,
        IReadOnlyList<FoodDeliveryDispatchAIReviewDriverDto> drivers)
    {
        var pickup = candidate.작업목록.FirstOrDefault()?.픽업좌표;
        if (pickup is null)
        {
            return drivers.FirstOrDefault()?.DriverId;
        }

        return drivers
            .Where(x => x.Latitude.HasValue && x.Longitude.HasValue)
            .Select(x => new
            {
                x.DriverId,
                Distance = CalculateDistanceKm(pickup, new 배차경로좌표(x.Latitude!.Value, x.Longitude!.Value))
            })
            .OrderBy(x => x.Distance)
            .Select(x => x.DriverId)
            .FirstOrDefault()
            ?? drivers.FirstOrDefault()?.DriverId;
    }

    private static 픽업하차경로작업 ToJob(FoodDeliveryOrderReviewItem item)
        => new(
            item.OrderNo,
            item.RestaurantAddress,
            CreatePoint(item.RestaurantLatitude, item.RestaurantLongitude),
            item.PickupReadyAtUtc?.AddMinutes(10),
            item.CustomerAddress,
            CreatePoint(item.CustomerLatitude, item.CustomerLongitude),
            item.PickupReadyAtUtc?.AddMinutes((double)item.MaxDeliveryMinutesAfterReady),
            item.PickupReadyAtUtc,
            item.MaxDeliveryMinutesAfterReady,
            item.PickupScopeKey,
            item.DropoffScopeKey);

    private static FoodDeliveryDispatchAIReviewOrderDto ToDto(FoodDeliveryOrderReviewItem item)
        => new()
        {
            OrderNo = item.OrderNo,
            RestaurantId = item.RestaurantId,
            RestaurantName = item.RestaurantName,
            MenuSummary = item.MenuSummary,
            OrderAmount = item.OrderAmount,
            OrderStatus = item.OrderStatus,
            DispatchStatus = item.DispatchStatus,
            RestaurantAddress = item.RestaurantAddress,
            RestaurantLatitude = item.RestaurantLatitude,
            RestaurantLongitude = item.RestaurantLongitude,
            CustomerAddress = item.CustomerAddress,
            CustomerLatitude = item.CustomerLatitude,
            CustomerLongitude = item.CustomerLongitude,
            PickupReadyAtUtc = item.PickupReadyAtUtc,
            MaxDeliveryMinutesAfterReady = item.MaxDeliveryMinutesAfterReady,
            PickupScopeKey = item.PickupScopeKey,
            PickupScopeName = item.PickupScopeName,
            PickupScopeRole = item.PickupScopeRole,
            DropoffScopeKey = item.DropoffScopeKey,
            DropoffScopeName = item.DropoffScopeName,
            DropoffScopeRole = item.DropoffScopeRole
        };

    private static bool IsReviewTarget(음식주문응답 order)
    {
        var status = 음식주문상태코드.Normalize(order.상태);
        return status is not 음식주문상태코드.전달완료
            and not 음식주문상태코드.수령확인
            and not 음식주문상태코드.거절
            and not 음식주문상태코드.취소;
    }

    private static decimal CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
    {
        const double earthRadiusKm = 6371d;
        var sourceLat = ToRadians((double)source.Latitude);
        var targetLat = ToRadians((double)target.Latitude);
        var deltaLat = ToRadians((double)(target.Latitude - source.Latitude));
        var deltaLng = ToRadians((double)(target.Longitude - source.Longitude));

        var a = Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d)
                + Math.Cos(sourceLat) * Math.Cos(targetLat)
                * Math.Sin(deltaLng / 2d) * Math.Sin(deltaLng / 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return Math.Round((decimal)(earthRadiusKm * c), 3);
    }

    private static double ToRadians(double degrees)
        => degrees * Math.PI / 180d;

}
