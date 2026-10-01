using System.Net.Http.Json;
using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface IDiagramOrganizationDirectoryClient
{
    Task<ThirdPartyLogisticsProviderDirectoryResponse> SearchThirdPartyLogisticsAsync(
        string? searchText,
        int pageSize = 12,
        CancellationToken cancellationToken = default);
}

public sealed class DiagramOrganizationDirectoryClient(HttpClient httpClient)
    : IDiagramOrganizationDirectoryClient
{
    public async Task<ThirdPartyLogisticsProviderDirectoryResponse> SearchThirdPartyLogisticsAsync(
        string? searchText,
        int pageSize = 12,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            "page=1",
            $"pageSize={Math.Clamp(pageSize, 1, 50)}"
        };
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query.Add($"q={Uri.EscapeDataString(searchText.Trim())}");
        }

        using var response = await httpClient.GetAsync(
            $"api/v1/operations/third-party-logistics/providers?{string.Join("&", query)}",
            cancellationToken);
        var result = await response.Content
            .ReadFromJsonAsync<ThirdPartyLogisticsProviderDirectoryResponse>(
                cancellationToken: cancellationToken);

        return result ?? new ThirdPartyLogisticsProviderDirectoryResponse
        {
            Success = false,
            ErrorMessage = "업체 디렉터리 응답을 확인할 수 없습니다."
        };
    }
}

internal sealed class NoopDiagramOrganizationDirectoryClient
    : IDiagramOrganizationDirectoryClient
{
    public static NoopDiagramOrganizationDirectoryClient Instance { get; } = new();

    private NoopDiagramOrganizationDirectoryClient()
    {
    }

    public Task<ThirdPartyLogisticsProviderDirectoryResponse> SearchThirdPartyLogisticsAsync(
        string? searchText,
        int pageSize = 12,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ThirdPartyLogisticsProviderDirectoryResponse
        {
            Success = false,
            ErrorMessage = "업체 디렉터리 클라이언트가 연결되지 않았습니다."
        });
}
