using Ssalddel.Contracts.Admin.Operations;

namespace SsalddelAdminApp.Services;

public sealed class FollowUpRecoveryMobileService(AdminAuthenticatedApiClient apiClient)
{
    public Task<운영후속처리복구목록Dto> 목록조회Async(CancellationToken cancellationToken = default)
        => apiClient.GetAsync<운영후속처리복구목록Dto>(
            "api/v1/admin/operations/follow-up-recoveries",
            cancellationToken);

    public Task<운영후속처리재시도응답Dto> 재시도예약Async(
        운영후속처리복구항목Dto item,
        CancellationToken cancellationToken = default)
        => apiClient.PutAsync<운영후속처리재시도요청Dto, 운영후속처리재시도응답Dto>(
            $"api/v1/admin/operations/follow-up-recoveries/{Uri.EscapeDataString(item.원천Code)}/{item.원천항목Id}/retry",
            new 운영후속처리재시도요청Dto { 예상처리시도수 = item.처리시도수 },
            cancellationToken);
}
