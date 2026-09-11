using System.Net.Http.Json;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>
/// 운영 서버가 노출한 기능·워크플로·API 경계를 읽어 연결 상태를 확인합니다.
/// 이 Client는 기능을 활성화하거나 운영 상태를 변경하지 않습니다.
/// </summary>
public interface ISsalddelOperationalServerCapabilityClient
{
    Task<VersionFeatureFlagsResponse> GetAsync(
        CancellationToken cancellationToken = default);
}

public sealed class SsalddelOperationalServerCapabilityClient(
    IHttpClientFactory clientFactory) : ISsalddelOperationalServerCapabilityClient
{
    public async Task<VersionFeatureFlagsResponse> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var client = clientFactory.CreateClient(SsalddelHttpClientNames.OperationalApi);
        var response = await client.GetFromJsonAsync<VersionFeatureFlagsResponse>(
            VersionFeatureFlagsRoutes.Metadata,
            cancellationToken);
        return response
            ?? throw new InvalidOperationException(
                "SsalddelOperationalCapabilityResponseEmpty");
    }
}
