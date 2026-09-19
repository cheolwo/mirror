using System.Globalization;
using Ssalddel.Contracts.Admin.Finance;

namespace SsalddelAdminApp.Services;

public sealed class AdminFinanceService(AdminAuthenticatedApiClient apiClient)
{
    public Task<운영현금흐름요약Dto> GetCashFlowSummaryAsync(
        string currencyCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"api/v1/admin/operations/finance/cash-flow-summary?currencyCode={Uri.EscapeDataString(currencyCode)}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        return apiClient.GetAsync<운영현금흐름요약Dto>(path, cancellationToken);
    }
}
