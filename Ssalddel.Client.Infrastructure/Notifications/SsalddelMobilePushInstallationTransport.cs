using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Contracts.Common.Notifications;

namespace Ssalddel.Client.Infrastructure.Notifications;

/// <summary>
/// 설치 HTTP 계약만 구성·전송합니다. 계정, 재시도, 상태 저장과 요청/응답 해제는 호출자가 소유합니다.
/// </summary>
public sealed class SsalddelMobilePushInstallationTransport(HttpClient httpClient)
{
    private const string InstallationsPath = "api/v1/mobile/push/installations";

    // 공통 호출은 using 획득 뒤 헤더를 지정해 헤더 예외 때도 요청을 해제합니다.
    public HttpRequestMessage CreateUpsertRequest(SsalddelMobilePushInstallationUpsertRequest payload)
        => new(HttpMethod.Put, InstallationsPath)
        {
            Content = CreateUpsertContent(payload)
        };

    // 음식 기사 호출은 기존처럼 Bearer를 먼저 구성한 뒤 본문을 지정합니다.
    public HttpRequestMessage CreateUpsertRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, InstallationsPath);
        ApplyAuthorization(request, accessToken);
        return request;
    }

    public static JsonContent CreateUpsertContent(SsalddelMobilePushInstallationUpsertRequest payload)
        => JsonContent.Create(payload);

    public HttpRequestMessage CreateDeleteRequest(string installationId)
        => new(HttpMethod.Delete,
            $"{InstallationsPath}/{Uri.EscapeDataString(installationId)}");

    public HttpRequestMessage CreateDeleteRequest(string installationId, string accessToken)
    {
        var request = CreateDeleteRequest(installationId);
        ApplyAuthorization(request, accessToken);
        return request;
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => httpClient.SendAsync(request, cancellationToken);

    public static void ApplyAuthorization(HttpRequestMessage request, string accessToken)
        => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
