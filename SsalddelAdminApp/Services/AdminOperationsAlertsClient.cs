using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace SsalddelAdminApp.Services;
public sealed class AdminOperationsAlertsClient(AdminAuthenticatedApiClient api) : IOperationsAlertsClient
{
    public Task<OperationsAlertsPage> ListAsync(string status, int page, CancellationToken ct)
        => api.GetAsync<OperationsAlertsPage>($"api/v1/admin/operations-alerts?status={Uri.EscapeDataString(status)}&page={page}", ct);
    public Task<OperationsAlertDto?> DetailAsync(string id, CancellationToken ct)
        => api.GetAsync<OperationsAlertDto?>($"api/v1/admin/operations-alerts/{Uri.EscapeDataString(id)}", ct);
    public Task<OperationsAlertDto> AcknowledgeAsync(string id, OperationsAlertAcknowledgeRequest input, CancellationToken ct)
        => api.PostAsync<OperationsAlertAcknowledgeRequest, OperationsAlertDto>($"api/v1/admin/operations-alerts/{Uri.EscapeDataString(id)}/acknowledge", input, ct);
}
