using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Admin.Restaurants;

namespace HongdalAdmin.Services;

public sealed class 음식운영Service
{
    private readonly HttpClient _httpClient;
    private readonly 관리자인증세션Service _session;
    private readonly ILogger<음식운영Service> _logger;
    private readonly bool _useMemoryFallback;

    public 음식운영Service(
        HttpClient httpClient,
        관리자인증세션Service session,
        IConfiguration configuration,
        ILogger<음식운영Service> logger)
    {
        _httpClient = httpClient;
        _session = session;
        _logger = logger;
        _useMemoryFallback = configuration.GetValue("AdminData:UseMemory", false);
    }

    public async Task<음식점리뷰관리목록응답> 리뷰운영목록조회Async(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "api/v1/admin/restaurant-reviews");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<음식점리뷰관리목록응답>(cancellationToken: cancellationToken)
                   ?? new 음식점리뷰관리목록응답();
        }
        catch (Exception ex) when (CanUseMemoryFallback(ex))
        {
            LogMemoryFallback(ex);
            return CreateSampleReviewItems();
        }
    }

    public async Task<음식점리뷰운영정책응답> 운영정책조회Async(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "api/v1/admin/restaurant-reviews/policy");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<음식점리뷰운영정책응답>(cancellationToken: cancellationToken)
                   ?? new 음식점리뷰운영정책응답();
        }
        catch (Exception ex) when (CanUseMemoryFallback(ex))
        {
            LogMemoryFallback(ex);
            return CreateSampleReviewPolicy();
        }
    }

    public async Task<음식배달요금정책응답> 배달요금정책조회Async(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "api/v1/admin/food-delivery-pricing-policy");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<음식배달요금정책응답>(cancellationToken: cancellationToken)
                   ?? new 음식배달요금정책응답();
        }
        catch (Exception ex) when (CanUseMemoryFallback(ex))
        {
            LogMemoryFallback(ex);
            return new 음식배달요금정책응답();
        }
    }

    public async Task<음식배달요금정책응답> 배달요금정책수정Async(음식배달요금정책응답 policy, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Put, "api/v1/admin/food-delivery-pricing-policy");
            request.Content = JsonContent.Create(policy);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<음식배달요금정책응답>(cancellationToken: cancellationToken)
                   ?? policy;
        }
        catch (Exception ex) when (CanUseMemoryFallback(ex))
        {
            LogMemoryFallback(ex);
            return policy;
        }
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

    private bool CanUseMemoryFallback(Exception exception)
        => _useMemoryFallback && exception is HttpRequestException or TaskCanceledException;

    private void LogMemoryFallback(Exception exception)
        => _logger.LogInformation(
            "AdminData 메모리 모드에서 음식 운영 API를 사용할 수 없어 샘플 데이터를 사용합니다. 사유: {Message}",
            exception.Message);

    private static 음식점리뷰관리목록응답 CreateSampleReviewItems()
        => new()
        {
            Items =
            [
                new()
                {
                    리뷰Id = 1,
                    음식점Id = 101,
                    음식점명 = "살뜰분식 강남점",
                    주문자UserId = "ORDERER-001",
                    주문번호 = "FOOD-ORDER-001",
                    별점 = 2,
                    내용 = "배달 지연으로 관리자 확인이 필요합니다.",
                    사진포함여부 = false,
                    같은음식점기준저평점3회연속여부 = true,
                    사장노출허용여부 = false,
                    현재노출여부 = false,
                    CreatedAt = DateTime.UtcNow.AddHours(-3),
                    최근조치사유 = "저평점 연속 발생 샘플"
                },
                new()
                {
                    리뷰Id = 2,
                    음식점Id = 102,
                    음식점명 = "살뜰도시락 마포점",
                    주문자UserId = "ORDERER-002",
                    주문번호 = "FOOD-ORDER-002",
                    별점 = 5,
                    내용 = "포장 상태와 기사 응대가 좋았습니다.",
                    사진포함여부 = true,
                    사장노출허용여부 = true,
                    현재노출여부 = true,
                    관리자게시강제여부 = true,
                    CreatedAt = DateTime.UtcNow.AddHours(-5)
                }
            ]
        };

    private static 음식점리뷰운영정책응답 CreateSampleReviewPolicy()
        => new()
        {
            Id = 1,
            기본저평점게시일수 = 3,
            허용게시일수옵션 = [3, 7],
            UpdatedAt = DateTime.UtcNow
        };
}

public sealed class 음식배달요금정책응답
{
    public decimal BaseFee { get; set; } = 3000m;
    public int IncludedDistanceMeters { get; set; } = 1000;
    public int DistanceUnitMeters { get; set; } = 100;
    public decimal DistanceUnitFee { get; set; } = 120m;
    public decimal MinimumFee { get; set; } = 3000m;
    public decimal DriverBasePayout { get; set; } = 2500m;
    public decimal DriverDistanceUnitPayout { get; set; } = 90m;
    public decimal DriverMinimumPayout { get; set; } = 2500m;
}
