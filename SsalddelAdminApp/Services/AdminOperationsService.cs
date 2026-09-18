using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Contracts.Admin.Restaurants;

namespace SsalddelAdminApp.Services;

public sealed class AdminOperationsService
{
    private readonly AdminAuthenticatedApiClient apiClient;

    public AdminOperationsService(AdminAuthenticatedApiClient apiClient)
    {
        this.apiClient = apiClient;
    }

    public async Task<AdminMobileOperationsSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var transportsTask = apiClient.GetAsync<IReadOnlyList<운송진행응답>>(
            "api/v1/admin/transports",
            cancellationToken);
        var driversTask = apiClient.GetAsync<IReadOnlyList<현재운행기사응답>>(
            "api/v1/admin/drivers/operating",
            cancellationToken);
        var surchargeTask = apiClient.GetAsync<음식배달한시수요할증응답>(
            "api/v1/admin/food-delivery-pricing-policy/temporary-demand-surcharge",
            cancellationToken);

        await Task.WhenAll(transportsTask, driversTask, surchargeTask);
        return new AdminMobileOperationsSnapshot(
            await transportsTask,
            await driversTask,
            await surchargeTask,
            DateTime.UtcNow);
    }

    public Task<음식배달한시수요할증응답> ApplyTemporaryDemandSurchargeAsync(
        음식배달한시수요할증적용요청 request,
        CancellationToken cancellationToken = default)
        => apiClient.PutAsync<음식배달한시수요할증적용요청, 음식배달한시수요할증응답>(
            "api/v1/admin/food-delivery-pricing-policy/temporary-demand-surcharge",
            request,
            cancellationToken);
}

public sealed record AdminMobileOperationsSnapshot(
    IReadOnlyList<운송진행응답> Transports,
    IReadOnlyList<현재운행기사응답> OperatingDrivers,
    음식배달한시수요할증응답 TemporaryDemandSurcharge,
    DateTime UpdatedAtUtc);
