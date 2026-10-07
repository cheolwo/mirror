using System.Net;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Admin.Restaurants;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace SsalddelAdminApp.Services;

/// <summary>음식 운영 API만 호출합니다. 화물 조회와 성공 조건을 공유하지 않습니다.</summary>
public sealed class AdminFoodOperationsService(AdminAuthenticatedApiClient apiClient)
    : IAdminFoodOperationsClient
{
    public Task<음식배달한시수요할증응답> GetTemporaryDemandSurchargeAsync(CancellationToken token = default)
        => apiClient.GetAsync<음식배달한시수요할증응답>("api/v1/admin/food-delivery-pricing-policy/temporary-demand-surcharge", token);

    public Task<음식배달한시수요할증응답> ApplyTemporaryDemandSurchargeAsync(
        음식배달한시수요할증적용요청 request, CancellationToken token = default)
        => apiClient.PutAsync<음식배달한시수요할증적용요청, 음식배달한시수요할증응답>(
            "api/v1/admin/food-delivery-pricing-policy/temporary-demand-surcharge", request, token);

    public Task<AdminFoodOrderListDto> ListAsync(string query, int page, CancellationToken cancellationToken = default)
        => ReadAsync(() => apiClient.GetAsync<AdminFoodOrderListDto>(
            $"api/v1/admin/food-orders/operations?query={Uri.EscapeDataString(query)}&page={page}&pageSize=20", cancellationToken));

    public Task<음식주문운영추적응답?> 조회Async(string orderNo, CancellationToken cancellationToken = default)
        => ReadTraceAsync(orderNo, cancellationToken);

    private async Task<음식주문운영추적응답?> ReadTraceAsync(string orderNo, CancellationToken token)
    {
        try
        {
            return await ReadAsync(() => apiClient.GetAsync<음식주문운영추적응답>(
                $"api/v1/admin/food-orders/{Uri.EscapeDataString(orderNo.Trim())}/operations-trace", token));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound && ex is not AdminFoodWorkflowUnavailableException) { return null; }
    }

    public Task<음식배달시도운영응답> 중단검토Async(string attemptId, 음식배달중단검토요청 body, CancellationToken cancellationToken = default)
        => ReadAsync(() => apiClient.PutAsync<음식배달중단검토요청, 음식배달시도운영응답>(
            $"api/v1/admin/food-orders/delivery-attempts/{Uri.EscapeDataString(attemptId.Trim())}/interruption-review", body, cancellationToken));

    private static async Task<T> ReadAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (AdminApiException ex) when (ex.ErrorCode == "FeatureDisabled") { throw new AdminFoodWorkflowUnavailableException(); }
        catch (AdminApiException ex) { throw new HttpRequestException(ex.Message, ex, ex.StatusCode); }
    }
}
