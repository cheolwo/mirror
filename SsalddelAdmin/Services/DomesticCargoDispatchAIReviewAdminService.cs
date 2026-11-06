using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Contracts.Admin.Dispatch;

namespace SsalddelAdmin.Services;

public sealed class DomesticCargoDispatchAIReviewAdminService
{
    private const string Endpoint = "api/v1/admin/dispatch/domestic-cargo-ai-review";

    private readonly HttpClient _httpClient;
    private readonly 관리자인증세션Service _session;
    private readonly bool _useMemoryFallback;

    public DomesticCargoDispatchAIReviewAdminService(HttpClient httpClient, 관리자인증세션Service session, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _session = session;
        _useMemoryFallback = configuration.GetValue("AdminData:UseMemory", false);
    }

    public async Task<DomesticCargoDispatchAIReviewWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        if (_useMemoryFallback)
        {
            return BuildMemoryWorkspace();
        }

        using var request = CreateRequest(HttpMethod.Get, Endpoint);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DomesticCargoDispatchAIReviewWorkspaceDto>(cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("배차 검토 응답에 작업공간이 없습니다.");
    }

    public async Task<DomesticCargoDispatchAIReviewDecisionResponse> RecordDecisionAsync(
        DomesticCargoDispatchAIReviewDecisionRequest decisionRequest,
        CancellationToken cancellationToken = default)
    {
        if (_useMemoryFallback)
        {
            return new DomesticCargoDispatchAIReviewDecisionResponse
            {
                CaseId = $"MEM-DOMESTIC-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                Message = $"{decisionRequest.DecisionType} 판단을 메모리 판단 사례로 저장했습니다."
            };
        }

        using var request = CreateRequest(HttpMethod.Post, $"{Endpoint}/decisions");
        request.Content = JsonContent.Create(decisionRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DomesticCargoDispatchAIReviewDecisionResponse>(cancellationToken: cancellationToken)
               ?? new DomesticCargoDispatchAIReviewDecisionResponse();
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

    private static DomesticCargoDispatchAIReviewWorkspaceDto BuildMemoryWorkspace()
    {
        var now = DateTimeOffset.UtcNow;
        return new DomesticCargoDispatchAIReviewWorkspaceDto
        {
            GeneratedAt = now,
            Source = "memory",
            Notes =
            [
                "AdminData 메모리 모드 샘플입니다.",
                "국내화물 AI 배차 검토 화면 캡처와 오프라인 검증에 사용합니다."
            ],
            Requests =
            [
                new()
                {
                    QueueId = 1,
                    RequestId = "REQ-AI-001",
                    SourceType = "DomesticCargo",
                    CargoType = "전자부품 12박스",
                    PickupAddress = "서울 강남구 테헤란로",
                    PickupLatitude = 37.503m,
                    PickupLongitude = 127.049m,
                    DropoffAddress = "경기 성남시 분당구",
                    DropoffLatitude = 37.382m,
                    DropoffLongitude = 127.118m,
                    DeliveryScopeKey = "seoul-gyeonggi",
                    DeliveryScopeName = "서울/경기 남부",
                    Fare = 45000m,
                    PickupWindowEndUtc = now.UtcDateTime.AddHours(2)
                },
                new()
                {
                    QueueId = 2,
                    RequestId = "REQ-AI-002",
                    SourceType = "DomesticCargo",
                    CargoType = "생활용품 25박스",
                    PickupAddress = "서울 송파구 문정동",
                    PickupLatitude = 37.485m,
                    PickupLongitude = 127.122m,
                    DropoffAddress = "경기 용인시 수지구",
                    DropoffLatitude = 37.322m,
                    DropoffLongitude = 127.095m,
                    DeliveryScopeKey = "seoul-gyeonggi",
                    DeliveryScopeName = "서울/경기 남부",
                    Fare = 62000m,
                    PickupWindowEndUtc = now.UtcDateTime.AddHours(3)
                }
            ],
            Drivers =
            [
                new()
                {
                    DriverId = "DRV-001",
                    DriverName = "홍기사",
                    VehicleType = "1톤 트럭",
                    DrivingStatus = "운행중",
                    Latitude = 37.462m,
                    Longitude = 127.074m,
                    DeliveryScopeKey = "seoul-gyeonggi",
                    DeliveryScopeName = "서울/경기 남부",
                    CurrentAcceptedTransportCount = 1,
                    LastLocationReceivedAtUtc = now.UtcDateTime.AddMinutes(-4)
                },
                new()
                {
                    DriverId = "DRV-002",
                    DriverName = "달기사",
                    VehicleType = "라보",
                    DrivingStatus = "대기",
                    Latitude = 37.395m,
                    Longitude = 127.102m,
                    DeliveryScopeKey = "gyeonggi-south",
                    DeliveryScopeName = "경기 남부",
                    CurrentAcceptedTransportCount = 0,
                    LastLocationReceivedAtUtc = now.UtcDateTime.AddMinutes(-12)
                }
            ],
            Bundles =
            [
                new()
                {
                    BundleKey = "DOM-BUNDLE-001",
                    BundleType = "상차지 근접 묶음",
                    RequestIds = ["REQ-AI-001", "REQ-AI-002"],
                    BundleSize = 2,
                    IsBundleAvailable = true,
                    IsAISuggested = true,
                    SuggestedDriverId = "DRV-001",
                    Score = 91.4m,
                    ExpectedFare = 107000m,
                    ExpectedCost = 64000m,
                    ExpectedProfit = 43000m,
                    ExpectedProfitPerRequest = 21500m,
                    Badges = ["상차권역 일치", "하차권역 연결", "운행중 기사 근접"],
                    Reason = "두 의뢰의 상차지가 서울 동남권에 있고 하차지가 경기 남부로 이어져 같은 기사에게 묶어 검토할 수 있습니다."
                }
            ],
            Assignments =
            [
                new()
                {
                    RequestId = "REQ-AI-001",
                    DriverId = "DRV-001",
                    Order = 1,
                    Score = 92m,
                    ExpectedCost = 28000m,
                    ExpectedFare = 45000m,
                    ExpectedProfit = 17000m,
                    Reason = "기사 현재 위치가 상차지와 가깝고 기존 운행 방향과 맞습니다.",
                    Badges = ["근접", "방향 일치"]
                }
            ]
        };
    }
}
