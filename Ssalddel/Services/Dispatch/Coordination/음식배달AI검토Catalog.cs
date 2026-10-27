using Ssalddel.Contracts.Admin.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Recommendation;

namespace 살뜰.Services.Dispatch.Coordination;

/// <summary>검토용 표본·권역과 기존 주문 보강 규칙입니다. 근사 좌표를 실제 위치로 승격하지 않습니다.</summary>
internal static class 음식배달AI검토Catalog
{
    private const decimal DefaultMaxDeliveryMinutesAfterReady = 42m;
    internal static readonly FoodDeliveryReviewScope PrimaryReviewScope = new("bjd-sigungu:11260", "중랑구", "주요 배달권");
    internal static readonly IReadOnlyList<FoodDeliveryReviewScope> AdjacentReviewScopes =
    [
        new("bjd-sigungu:11230", "동대문구", "인접 배달권"),
        new("bjd-sigungu:11215", "광진구", "인접 배달권"),
        new("bjd-sigungu:11350", "노원구", "인접 배달권"),
        new("bjd-sigungu:41310", "구리시", "인접 배달권")
    ];
    private static readonly IReadOnlyList<FoodDeliveryReviewScope> AllReviewScopes =
        [PrimaryReviewScope, ..AdjacentReviewScopes];

    internal static FoodDeliveryOrderReviewItem EnrichOrder(음식주문응답 order)
    {
        var restaurant = ResolveRestaurant(order);
        var customer = ResolveCustomer(order, restaurant.Latitude, restaurant.Longitude);
        var pickupScope = ResolveFoodDeliveryScope(
            restaurant.Address,
            new 배차경로좌표(restaurant.Latitude, restaurant.Longitude));
        var dropoffScope = ResolveFoodDeliveryScope(
            customer.Address,
            new 배차경로좌표(customer.Latitude, customer.Longitude));

        return new FoodDeliveryOrderReviewItem(
            order.주문번호,
            order.음식점Id,
            restaurant.Name,
            FoodOrderSampleData.BuildMenuSummary(order.상품목록),
            order.총주문금액,
            order.상태,
            order.배차상태,
            restaurant.Address,
            restaurant.Latitude,
            restaurant.Longitude,
            customer.Address,
            customer.Latitude,
            customer.Longitude,
            order.조리예상완료시각Utc ?? order.음식점수락시각Utc?.AddMinutes(20) ?? order.CreatedAt.AddMinutes(20),
            DefaultMaxDeliveryMinutesAfterReady,
            pickupScope.Key,
            pickupScope.Name,
            pickupScope.Role,
            dropoffScope.Key,
            dropoffScope.Name,
            dropoffScope.Role);
    }

    private static (string Name, string Address, decimal Latitude, decimal Longitude) ResolveRestaurant(음식주문응답 order)
    {
        var sample = order.음식점Id switch
        {
            101 => ("살뜰분식 면목점", "서울특별시 중랑구 면목로 352", 37.5888m, 127.0874m),
            102 => ("살뜰도시락 상봉점", "서울특별시 중랑구 망우로 353", 37.5969m, 127.0851m),
            _ => ("살뜰음식점 중랑점", "서울특별시 중랑구 봉화산로 179", 37.6066m, 127.0927m)
        };

        return (
            string.IsNullOrWhiteSpace(order.음식점명) ? sample.Item1 : order.음식점명.Trim(),
            string.IsNullOrWhiteSpace(order.음식점주소) ? sample.Item2 : order.음식점주소.Trim(),
            order.음식점위도 ?? sample.Item3,
            order.음식점경도 ?? sample.Item4);
    }

    private static (string Address, decimal Latitude, decimal Longitude) ResolveCustomer(
        음식주문응답 order,
        decimal restaurantLatitude,
        decimal restaurantLongitude)
    {
        var address = string.IsNullOrWhiteSpace(order.수령인정보.주소)
            ? "고객 주소 미정"
            : order.수령인정보.주소.Trim();

        if (IsDefaultFoodOrderSample(order))
        {
            return order.주문번호 switch
            {
                "FOOD-20260701-001" => ("서울특별시 중랑구 면목동 15-1", 37.5867m, 127.0885m),
                "FOOD-20260701-002" => ("서울특별시 동대문구 장안동 335-1", 37.5684m, 127.0717m),
                "FOOD-20260701-003" => ("서울특별시 광진구 중곡동 130-1", 37.5586m, 127.0817m),
                _ => (address, restaurantLatitude, restaurantLongitude)
            };
        }

        if (address.Contains("중랑", StringComparison.Ordinal)
            || address.Contains("면목", StringComparison.Ordinal)
            || address.Contains("상봉", StringComparison.Ordinal)
            || address.Contains("망우", StringComparison.Ordinal)
            || address.Contains("신내", StringComparison.Ordinal))
        {
            return (address, 37.5928m, 127.0879m);
        }

        if (address.Contains("동대문", StringComparison.Ordinal)
            || address.Contains("장안", StringComparison.Ordinal))
        {
            return (address, 37.5684m, 127.0717m);
        }

        if (address.Contains("광진", StringComparison.Ordinal)
            || address.Contains("중곡", StringComparison.Ordinal))
        {
            return (address, 37.5586m, 127.0817m);
        }

        if (address.Contains("노원", StringComparison.Ordinal)
            || address.Contains("공릉", StringComparison.Ordinal))
        {
            return (address, 37.6246m, 127.0730m);
        }

        if (address.Contains("구리", StringComparison.Ordinal))
        {
            return (address, 37.5943m, 127.1296m);
        }

        var offset = StableOffset(order.주문번호);
        return (address, restaurantLatitude + offset.Latitude, restaurantLongitude + offset.Longitude);
    }

    private static (decimal Latitude, decimal Longitude) StableOffset(string value)
    {
        var sum = value.Sum(ch => ch);
        return ((sum % 7 + 1) * 0.002m, (sum % 5 + 1) * 0.002m);
    }

    internal static List<FoodDeliveryOrderReviewItem> CreateJungnangScenarioOrders(DateTime now)
    {
        return
        [
            CreateScenarioOrder(
                "FOOD-JN-001",
                101,
                "살뜰분식 면목점",
                "제육덮밥 2",
                19000m,
                "서울특별시 중랑구 면목로 352",
                37.5888m,
                127.0874m,
                "서울특별시 중랑구 면목동 15-1",
                37.5867m,
                127.0885m,
                now.AddMinutes(14)),
            CreateScenarioOrder(
                "FOOD-JN-002",
                101,
                "살뜰분식 면목점",
                "비빔밥 1, 만두 1",
                16500m,
                "서울특별시 중랑구 면목로 352",
                37.5888m,
                127.0874m,
                "서울특별시 동대문구 장안동 335-1",
                37.5684m,
                127.0717m,
                now.AddMinutes(18)),
            CreateScenarioOrder(
                "FOOD-JN-003",
                102,
                "살뜰도시락 상봉점",
                "돈까스 1, 우동 1",
                19000m,
                "서울특별시 중랑구 망우로 353",
                37.5969m,
                127.0851m,
                "서울특별시 광진구 중곡동 130-1",
                37.5586m,
                127.0817m,
                now.AddMinutes(21)),
            CreateScenarioOrder(
                "FOOD-JN-004",
                103,
                "살뜰치킨 신내점",
                "후라이드치킨 1",
                21000m,
                "서울특별시 중랑구 신내로 128",
                37.6088m,
                127.0961m,
                "서울특별시 노원구 공릉동 375-1",
                37.6246m,
                127.0730m,
                now.AddMinutes(24)),
            CreateScenarioOrder(
                "FOOD-JN-005",
                104,
                "살뜰국밥 망우점",
                "순대국 2",
                22000m,
                "서울특별시 중랑구 망우로 470",
                37.5996m,
                127.1012m,
                "경기도 구리시 교문동 230-1",
                37.5943m,
                127.1296m,
                now.AddMinutes(28))
        ];
    }

    private static FoodDeliveryOrderReviewItem CreateScenarioOrder(
        string orderNo,
        long restaurantId,
        string restaurantName,
        string menuSummary,
        decimal orderAmount,
        string restaurantAddress,
        decimal restaurantLatitude,
        decimal restaurantLongitude,
        string customerAddress,
        decimal customerLatitude,
        decimal customerLongitude,
        DateTime pickupReadyAtUtc)
    {
        var pickupScope = ResolveFoodDeliveryScope(
            restaurantAddress,
            new 배차경로좌표(restaurantLatitude, restaurantLongitude));
        var dropoffScope = ResolveFoodDeliveryScope(
            customerAddress,
            new 배차경로좌표(customerLatitude, customerLongitude));

        return new FoodDeliveryOrderReviewItem(
            orderNo,
            restaurantId,
            restaurantName,
            menuSummary,
            orderAmount,
            음식주문상태코드.픽업대기,
            음식주문배차상태코드.배차대기,
            restaurantAddress,
            restaurantLatitude,
            restaurantLongitude,
            customerAddress,
            customerLatitude,
            customerLongitude,
            pickupReadyAtUtc,
            DefaultMaxDeliveryMinutesAfterReady,
            pickupScope.Key,
            pickupScope.Name,
            pickupScope.Role,
            dropoffScope.Key,
            dropoffScope.Name,
            dropoffScope.Role);
    }

    internal static List<FoodDeliveryDispatchAIReviewDriverDto> CreateSampleDrivers(DateTime now)
    {
        return
        [
            new()
            {
                DriverId = "f-driver-sample-a",
                DriverName = "F드라이버 A",
                DrivingStatus = "운행중",
                Latitude = 37.5907m,
                Longitude = 127.0869m,
                DeliveryScopeKey = PrimaryReviewScope.Key,
                DeliveryScopeName = PrimaryReviewScope.Name,
                DeliveryScopeRole = PrimaryReviewScope.Role,
                LastLocationReceivedAtUtc = now.AddMinutes(-2)
            },
            new()
            {
                DriverId = "f-driver-sample-b",
                DriverName = "F드라이버 B",
                DrivingStatus = "운행중",
                Latitude = 37.5712m,
                Longitude = 127.0732m,
                DeliveryScopeKey = "bjd-sigungu:11230",
                DeliveryScopeName = "동대문구",
                DeliveryScopeRole = "인접 배달권",
                CurrentAcceptedDeliveryCount = 1,
                LastLocationReceivedAtUtc = now.AddMinutes(-5)
            },
            new()
            {
                DriverId = "f-driver-sample-c",
                DriverName = "F드라이버 C",
                DrivingStatus = "운행중",
                Latitude = 37.5949m,
                Longitude = 127.1267m,
                DeliveryScopeKey = "bjd-sigungu:41310",
                DeliveryScopeName = "구리시",
                DeliveryScopeRole = "인접 배달권",
                LastLocationReceivedAtUtc = now.AddMinutes(-7)
            }
        ];
    }

    internal static bool IsDefaultFoodOrderSample(음식주문응답 order)
        => order.주문번호.StartsWith("FOOD-20260701-", StringComparison.Ordinal);

    internal static FoodDeliveryReviewScope ResolveFoodDeliveryScope(string? address, 배차경로좌표? point)
    {
        if (!string.IsNullOrWhiteSpace(address))
        {
            var normalized = address.Trim();
            if (normalized.Contains("중랑", StringComparison.Ordinal)
                || normalized.Contains("면목", StringComparison.Ordinal)
                || normalized.Contains("상봉", StringComparison.Ordinal)
                || normalized.Contains("망우", StringComparison.Ordinal)
                || normalized.Contains("신내", StringComparison.Ordinal))
            {
                return PrimaryReviewScope;
            }

            var adjacent = AdjacentReviewScopes.FirstOrDefault(x => normalized.Contains(x.Name, StringComparison.Ordinal));
            if (adjacent is not null)
            {
                return adjacent;
            }
        }

        var catalogScope = 국내화물배달권정책.판정(point, address);
        return ResolveScopeByKey(catalogScope.배달권키)
               ?? new FoodDeliveryReviewScope(catalogScope.배달권키, catalogScope.배달권명, "기타 배달권");
    }

    internal static FoodDeliveryReviewScope? ResolveScopeByKey(string? scopeKey)
        => string.IsNullOrWhiteSpace(scopeKey)
            ? null
            : AllReviewScopes.FirstOrDefault(x => string.Equals(x.Key, scopeKey.Trim(), StringComparison.Ordinal));

    internal static string ResolveScopeDisplay(string? scopeKey)
    {
        var scope = ResolveScopeByKey(scopeKey);
        return scope is null
            ? string.IsNullOrWhiteSpace(scopeKey) ? "미정" : scopeKey.Trim()
            : $"{scope.Name}({scope.Role})";
    }

    internal static 배차경로좌표? CreatePoint(decimal? latitude, decimal? longitude)
        => latitude.HasValue && longitude.HasValue
            ? new 배차경로좌표(latitude.Value, longitude.Value)
            : null;

}

internal sealed record FoodDeliveryOrderReviewItem(
    string OrderNo,
    long RestaurantId,
    string RestaurantName,
    string MenuSummary,
    decimal OrderAmount,
    string OrderStatus,
    string DispatchStatus,
    string RestaurantAddress,
    decimal RestaurantLatitude,
    decimal RestaurantLongitude,
    string CustomerAddress,
    decimal CustomerLatitude,
    decimal CustomerLongitude,
    DateTime? PickupReadyAtUtc,
    decimal MaxDeliveryMinutesAfterReady,
    string PickupScopeKey,
    string PickupScopeName,
    string PickupScopeRole,
    string DropoffScopeKey,
    string DropoffScopeName,
    string DropoffScopeRole);

internal sealed record FoodDeliveryReviewScope(string Key, string Name, string Role);
