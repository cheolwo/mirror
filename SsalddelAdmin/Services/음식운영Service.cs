using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Contracts.Admin.Restaurants;

namespace SsalddelAdmin.Services;

public sealed class 음식운영Service
{
    private readonly HttpClient _httpClient;
    private readonly 관리자인증세션Service _session;

    public 음식운영Service(
        HttpClient httpClient,
        관리자인증세션Service session)
    {
        _httpClient = httpClient;
        _session = session;
    }

    public async Task<음식점리뷰관리목록응답> 리뷰운영목록조회Async(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/v1/admin/restaurant-reviews");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<음식점리뷰관리목록응답>(cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("리뷰 목록 응답이 비어 있습니다.");
    }

    public async Task<음식점리뷰운영정책응답> 운영정책조회Async(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/v1/admin/restaurant-reviews/policy");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<음식점리뷰운영정책응답>(cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("리뷰 정책 응답이 비어 있습니다.");
    }

    public async Task<음식배달요금정책응답> 배달요금정책조회Async(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/v1/admin/food-delivery-pricing-policy");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<음식배달요금정책응답>(cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("배달 요금 정책 응답이 비어 있습니다.");
    }

    public async Task<음식배달요금정책응답> 배달요금정책수정Async(음식배달요금정책응답 policy, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, "api/v1/admin/food-delivery-pricing-policy");
        request.Content = JsonContent.Create(policy);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<음식배달요금정책응답>(cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("저장된 배달 요금 정책 응답이 비어 있습니다. 다시 조회해 확인해 주세요.");
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
}
