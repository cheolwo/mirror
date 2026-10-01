using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Shipper.Request;

namespace HongdalApp.Services;

public sealed class 화주운송의뢰BulkApiService : IShipperBulkRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;

    public 화주운송의뢰BulkApiService(HttpClient httpClient, IAuthSession authSession)
    {
        _httpClient = httpClient;
        _authSession = authSession;
    }

    public async Task<화주운송의뢰일괄미리보기응답?> 미리보기Async(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/shipper/requests/bulk/preview");
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(ResolveContentType(fileName));
        form.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "shipper-requests.csv" : fileName);
        request.Content = form;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, "api/v1/shipper/requests/bulk/preview", cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<화주운송의뢰일괄미리보기응답>(cancellationToken);
    }

    public async Task<화주운송의뢰일괄등록결과응답?> 등록Async(화주운송의뢰일괄확정등록요청 confirmRequest, CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/shipper/requests/bulk/confirm-preview");
        request.Content = JsonContent.Create(confirmRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, "api/v1/shipper/requests/bulk/confirm-preview", cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<화주운송의뢰일괄등록결과응답>(cancellationToken);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            throw new InvalidOperationException("서버 인증 정보가 없어 화주 일괄등록 API를 호출할 수 없습니다.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        return request;
    }

    private static string ResolveContentType(string fileName)
    {
        return Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? "text/csv"
            : "application/octet-stream";
    }

    private static async Task<string> BuildFailureMessageAsync(HttpResponseMessage response, string path, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"화주 일괄등록 API 요청에 실패했습니다. path={path}, HTTP {(int)response.StatusCode}";
        }

        return $"화주 일괄등록 API 요청에 실패했습니다. path={path}, HTTP {(int)response.StatusCode}: {body}";
    }
}
