using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;

namespace SsalddelAdmin.Services;

public interface IFoodOrderInterruptionReviewClient
{
    Task<음식주문운영추적응답?> 조회Async(string orderNo, CancellationToken cancellationToken = default);
    Task<음식배달시도운영응답> 중단검토Async(string attemptId, 음식배달중단검토요청 body, CancellationToken cancellationToken = default);
}

public sealed class FoodOrderOperationsTraceAdminService(
    HttpClient httpClient,
    관리자인증세션Service session) : IFoodOrderInterruptionReviewClient
{
    public async Task<음식배달시도운영응답> 중단검토Async(
        string attemptId,
        음식배달중단검토요청 body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(attemptId))
            throw new ArgumentException("검토할 배달 시도를 선택해 주세요.", nameof(attemptId));
        ArgumentNullException.ThrowIfNull(body);
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"api/v1/admin/food-orders/delivery-attempts/{Uri.EscapeDataString(attemptId.Trim())}/interruption-review")
        {
            Content = JsonContent.Create(body)
        };
        if (!string.IsNullOrWhiteSpace(session.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("배달 중단 검토를 저장하지 못했습니다. 최신 시도를 다시 확인해 주세요.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<음식배달시도운영응답>(cancellationToken)
            ?? throw new InvalidOperationException("검토 응답을 확인할 수 없습니다. 최신 시도를 다시 조회해 주세요.");
    }

    public async Task<FoodDeliveryOrderSettlementDto> 모의지급검증Async(
        string orderNo,
        FoodDeliverySimulatedPayoutRequest body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNo))
            throw new ArgumentException("주문번호를 입력해 주세요.", nameof(orderNo));
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/admin/food-orders/{Uri.EscapeDataString(orderNo.Trim())}/simulate-driver-payout")
        {
            Content = JsonContent.Create(body)
        };
        if (!string.IsNullOrWhiteSpace(session.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"모의 지급 검증 실패({(int)response.StatusCode}). 최신 정산을 조회하고 공제액·근거를 확인해 주세요.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<FoodDeliveryOrderSettlementDto>(cancellationToken)
            ?? throw new InvalidOperationException("모의 지급 응답이 없습니다. 최신 정산을 다시 조회해 주세요.");
    }

    public async Task<음식주문운영추적응답?> 조회Async(
        string orderNo,
        CancellationToken cancellationToken = default)
    {
        var normalized = orderNo?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("주문번호를 입력해 주세요.", nameof(orderNo));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/admin/food-orders/{Uri.EscapeDataString(normalized)}/operations-trace");
        if (!string.IsNullOrWhiteSpace(session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<음식주문운영추적응답>(
            cancellationToken: cancellationToken);
    }
}
