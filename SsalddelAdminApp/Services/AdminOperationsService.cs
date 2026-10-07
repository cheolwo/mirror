using Ssalddel.Contracts.Admin.Progress;

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
        await Task.WhenAll(transportsTask, driversTask);
        return new AdminMobileOperationsSnapshot(
            await transportsTask,
            await driversTask,
            DateTime.UtcNow);
    }

}

public sealed record AdminMobileOperationsSnapshot(
    IReadOnlyList<운송진행응답> Transports,
    IReadOnlyList<현재운행기사응답> OperatingDrivers,
    DateTime UpdatedAtUtc);
