using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Contracts.Admin.Operations;

namespace SsalddelAdmin.Services;

public sealed class FollowUpRecoveryAdminService(
    HttpClient httpClient,
    관리자인증세션Service session)
{
    public Task<운영후속처리복구목록Dto?> 목록조회Async(CancellationToken cancellationToken = default)
        => SendAsync<운영후속처리복구목록Dto>(
            HttpMethod.Get,
            "api/v1/admin/operations/follow-up-recoveries",
            null,
            cancellationToken);

    public Task<운영후속처리재시도응답Dto?> 재시도예약Async(
        운영후속처리복구항목Dto item,
        CancellationToken cancellationToken = default)
        => SendAsync<운영후속처리재시도응답Dto>(
            HttpMethod.Put,
            $"api/v1/admin/operations/follow-up-recoveries/{Uri.EscapeDataString(item.원천Code)}/{item.원천항목Id}/retry",
            new 운영후속처리재시도요청Dto { 예상처리시도수 = item.처리시도수 },
            cancellationToken);

    private async Task<T?> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (!string.IsNullOrWhiteSpace(session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }
}
