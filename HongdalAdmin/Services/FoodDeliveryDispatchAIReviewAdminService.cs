using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Admin.Dispatch;

namespace HongdalAdmin.Services;

public sealed class FoodDeliveryDispatchAIReviewAdminService
{
    private const string Endpoint = "api/v1/admin/dispatch/food-delivery-ai-review";

    private readonly HttpClient _httpClient;
    private readonly 관리자인증세션Service _session;
    private readonly bool _useMemoryFallback;

    public FoodDeliveryDispatchAIReviewAdminService(HttpClient httpClient, 관리자인증세션Service session, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _session = session;
        _useMemoryFallback = configuration.GetValue("AdminData:UseMemory", false);
    }

    public async Task<FoodDeliveryDispatchAIReviewWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        if (_useMemoryFallback)
        {
            return BuildMemoryWorkspace();
        }

        using var request = CreateRequest(HttpMethod.Get, Endpoint);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FoodDeliveryDispatchAIReviewWorkspaceDto>(cancellationToken: cancellationToken)
               ?? new FoodDeliveryDispatchAIReviewWorkspaceDto();
    }

    public async Task<FoodDeliveryDispatchAIReviewDecisionResponse> RecordDecisionAsync(
        FoodDeliveryDispatchAIReviewDecisionRequest decisionRequest,
        CancellationToken cancellationToken = default)
    {
        if (_useMemoryFallback)
        {
            return new FoodDeliveryDispatchAIReviewDecisionResponse
            {
                CaseId = $"MEM-FOOD-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                Message = $"{decisionRequest.DecisionType} 판단을 음식배달 메모리 사례로 저장했습니다."
            };
        }

        using var request = CreateRequest(HttpMethod.Post, $"{Endpoint}/decisions");
        request.Content = JsonContent.Create(decisionRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FoodDeliveryDispatchAIReviewDecisionResponse>(cancellationToken: cancellationToken)
               ?? new FoodDeliveryDispatchAIReviewDecisionResponse();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }

        return request;
    }

    private static FoodDeliveryDispatchAIReviewWorkspaceDto BuildMemoryWorkspace()
    {
        var now = DateTimeOffset.UtcNow;
        return new FoodDeliveryDispatchAIReviewWorkspaceDto
        {
            GeneratedAt = now,
            Source = "memory",
            PrimaryDeliveryScopeKey = "gangnam-seocho",
            PrimaryDeliveryScopeName = "강남/서초 배달권",
            AdjacentDeliveryScopeKeys = ["songpa", "bundang"],
            AdjacentDeliveryScopeNames = ["송파", "분당"],
            Notes =
            [
                "AdminData 메모리 모드 샘플입니다.",
                "음식배달 AI 배차 검토 화면 캡처와 오프라인 검증에 사용합니다."
            ],
            Orders =
            [
                new()
                {
                    OrderNo = "FOOD-2026-001",
                    RestaurantId = 1001,
                    RestaurantName = "살뜰분식 강남점",
                    MenuSummary = "김밥 3, 떡볶이 2",
                    OrderAmount = 43000m,
                    OrderStatus = "조리중",
                    DispatchStatus = "배차대기",
                    RestaurantAddress = "서울 강남구 역삼동",
                    RestaurantLatitude = 37.500m,
                    RestaurantLongitude = 127.036m,
                    CustomerAddress = "서울 서초구 서초동",
                    CustomerLatitude = 37.492m,
                    CustomerLongitude = 127.025m,
                    PickupReadyAtUtc = now.UtcDateTime.AddMinutes(14),
                    PickupScopeKey = "gangnam",
                    PickupScopeName = "강남",
                    PickupScopeRole = "주 배달권",
                    DropoffScopeKey = "seocho",
                    DropoffScopeName = "서초",
                    DropoffScopeRole = "인접 배달권"
                },
                new()
                {
                    OrderNo = "FOOD-2026-002",
                    RestaurantId = 1002,
                    RestaurantName = "달빛도시락",
                    MenuSummary = "도시락 5",
                    OrderAmount = 58000m,
                    OrderStatus = "조리완료",
                    DispatchStatus = "배차대기",
                    RestaurantAddress = "서울 강남구 논현동",
                    RestaurantLatitude = 37.511m,
                    RestaurantLongitude = 127.028m,
                    CustomerAddress = "서울 강남구 삼성동",
                    CustomerLatitude = 37.508m,
                    CustomerLongitude = 127.062m,
                    PickupReadyAtUtc = now.UtcDateTime.AddMinutes(6),
                    PickupScopeKey = "gangnam",
                    PickupScopeName = "강남",
                    PickupScopeRole = "주 배달권",
                    DropoffScopeKey = "gangnam",
                    DropoffScopeName = "강남",
                    DropoffScopeRole = "주 배달권"
                }
            ],
            Drivers =
            [
                new()
                {
                    DriverId = "FDRV-001",
                    DriverName = "홍F드라이버",
                    DrivingStatus = "대기",
                    Latitude = 37.504m,
                    Longitude = 127.041m,
                    DeliveryScopeKey = "gangnam",
                    DeliveryScopeName = "강남",
                    DeliveryScopeRole = "주 배달권",
                    CurrentAcceptedDeliveryCount = 1,
                    LastLocationReceivedAtUtc = now.UtcDateTime.AddMinutes(-3)
                },
                new()
                {
                    DriverId = "FDRV-002",
                    DriverName = "달F드라이버",
                    DrivingStatus = "운행중",
                    Latitude = 37.494m,
                    Longitude = 127.029m,
                    DeliveryScopeKey = "seocho",
                    DeliveryScopeName = "서초",
                    DeliveryScopeRole = "인접 배달권",
                    CurrentAcceptedDeliveryCount = 2,
                    LastLocationReceivedAtUtc = now.UtcDateTime.AddMinutes(-7)
                }
            ],
            Bundles =
            [
                new()
                {
                    BundleKey = "FOOD-BUNDLE-001",
                    BundleType = "고객권 근접 묶음",
                    OrderNos = ["FOOD-2026-001", "FOOD-2026-002"],
                    BundleSize = 2,
                    IsBundleAvailable = true,
                    IsAISuggested = true,
                    SuggestedDriverId = "FDRV-001",
                    Score = 88.6m,
                    PickupDistanceKm = 1.4m,
                    DropoffDistanceKm = 3.2m,
                    ExpectedRouteDistanceKm = 7.8m,
                    Badges = ["주 배달권", "픽업 준비시간 근접", "F드라이버 근접"],
                    Reason = "음식점 픽업지가 강남권에 모여 있고 조리 완료 시간이 가깝습니다.",
                    BundleDecisionSummary = "2건 묶음 가능, 고객 전달 순서 확인 필요",
                    DriverAssignmentDecisionSummary = "FDRV-001이 현재 위치와 수락 건수 기준으로 우선 후보입니다."
                }
            ],
            Assignments =
            [
                new()
                {
                    OrderNo = "FOOD-2026-001",
                    DriverId = "FDRV-001",
                    Order = 1,
                    Score = 90m,
                    Reason = "픽업지 접근 시간이 짧고 기존 수락 건수와 충돌하지 않습니다.",
                    Badges = ["근접", "주 배달권"]
                }
            ]
        };
    }
}
