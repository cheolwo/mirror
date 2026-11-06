using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.WebApp.Services;

public sealed class 기사운송증빙Service
{
    private readonly HttpClient _httpClient;
    private readonly WebAuthSessionService _authSession;
    private readonly ITransportRequestLedgerObserver _ledgerObserver;

    public 기사운송증빙Service(
        HttpClient httpClient,
        WebAuthSessionService authSession,
        ITransportRequestLedgerObserver ledgerObserver)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _ledgerObserver = ledgerObserver;
    }

    public async Task<IReadOnlyList<기사운송요약응답>> 목록조회Async(CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/v1/driver/transports", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "운송 목록 조회", cancellationToken);
        var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<기사운송요약응답>>(cancellationToken) ?? [];
        foreach (var item in items)
        {
            Observe(item, "Ssalddel.WebApp.DriverTransportList");
        }

        return items;
    }

    public async Task<기사운송요약응답?> 현재운송조회Async(CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/v1/driver/transports/current", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var problem = SsalddelApiProblemParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.ErrorCode == "CargoTransportNotFound")
            {
                return null;
            }
        }

        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<기사운송요약응답>(cancellationToken)
                   ?? throw new InvalidOperationException("현재 운송 조회 응답을 읽을 수 없습니다.");
        Observe(item, "Ssalddel.WebApp.DriverCurrentTransport");
        return item;
    }

    public async Task<기사운송상세응답> 상세조회Async(long transportId, CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/v1/driver/transports/{transportId}", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "운송 상세 조회", cancellationToken);
        var item = await response.Content.ReadFromJsonAsync<기사운송상세응답>(cancellationToken)
                   ?? throw new InvalidOperationException("운송 상세 조회 응답을 읽을 수 없습니다.");
        Observe(item, "Ssalddel.WebApp.DriverTransportDetail");
        return item;
    }

    public Task<기사운송상태변경응답> 상차지도착Async(long transportId, CancellationToken cancellationToken = default)
        => PostEmptyAsync($"api/v1/driver/transports/{transportId}/arrive-pickup", "상차지 도착", cancellationToken);

    public Task<기사운송상태변경응답> 하차지도착Async(long transportId, CancellationToken cancellationToken = default)
        => PostEmptyAsync($"api/v1/driver/transports/{transportId}/arrive-dropoff", "하차지 도착", cancellationToken);

    public async Task<기사운송사진업로드결과> 사진업로드Async(
        long transportId,
        운송증빙단계 단계,
        string fileName,
        string contentType,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        if (transportId <= 0)
        {
            throw new InvalidOperationException("운송 ID가 필요합니다.");
        }

        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("업로드할 사진 파일이 비어 있습니다.");
        }

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(ResolveCommandName(단계)), "commandName");
        content.Add(new StringContent(transportId.ToString()), "referenceId");

        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "api/v1/files/upload", cancellationToken);
        request.Content = content;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "사진 업로드", cancellationToken);

        return await response.Content.ReadFromJsonAsync<기사운송사진업로드결과>(cancellationToken)
               ?? throw new InvalidOperationException("사진 업로드 응답을 읽을 수 없습니다.");
    }

    public Task<기사운송상태변경응답> 상차완료Async(
        long transportId,
        기사운송사진업로드결과 upload,
        기사상차인수증입력 receipt,
        CancellationToken cancellationToken = default)
    {
        return PostJsonAsync<기사운송상차완료요청, 기사운송상태변경응답>(
            $"api/v1/driver/transports/{transportId}/pickup-complete",
            new 기사운송상차완료요청
            {
                상차사진ObjectName = upload.ObjectName,
                상차사진Url = upload.Url,
                인수증증빙방식 = receipt.인수증증빙방식,
                인수자명 = receipt.인수자명,
                인수자소속 = receipt.인수자소속,
                인수자서명 = receipt.인수자서명,
                기사서명 = receipt.기사서명,
                인수증확인완료 = receipt.인수증확인완료,
                인수증서명생략확인 = receipt.인수증서명생략확인,
                인수증서명생략사유 = receipt.인수증서명생략사유
            },
            "상차 완료",
            cancellationToken);
    }

    public Task<기사운송상태변경응답> 하차완료Async(
        long transportId,
        기사운송사진업로드결과 upload,
        CancellationToken cancellationToken = default)
    {
        return PostJsonAsync<기사운송하차완료요청, 기사운송상태변경응답>(
            $"api/v1/driver/transports/{transportId}/complete",
            new 기사운송하차완료요청
            {
                하차사진ObjectName = upload.ObjectName,
                하차사진Url = upload.Url
            },
            "하차 완료",
            cancellationToken);
    }

    public Task<기사운송상태변경응답> 예외신고Async(
        long transportId,
        기사운송문제신고요청 payload,
        CancellationToken cancellationToken = default)
    {
        return PostJsonAsync<기사운송문제신고요청, 기사운송상태변경응답>(
            $"api/v1/driver/transports/{transportId}/report-issue",
            payload,
            "운송 예외 신고",
            cancellationToken);
    }

    private async Task<기사운송상태변경응답> PostEmptyAsync(
        string path,
        string actionName,
        CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, path, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, actionName, cancellationToken);
        var item = await response.Content.ReadFromJsonAsync<기사운송상태변경응답>(cancellationToken)
                   ?? throw new InvalidOperationException($"{actionName} 응답을 읽을 수 없습니다.");
        Observe(item, $"Ssalddel.WebApp.{actionName}");
        return item;
    }

    private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        string actionName,
        CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, path, cancellationToken);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, actionName, cancellationToken);
        var item = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken)
                   ?? throw new InvalidOperationException($"{actionName} 응답을 읽을 수 없습니다.");
        if (item is 기사운송상태변경응답 state)
        {
            Observe(state, $"Ssalddel.WebApp.{actionName}");
        }

        return item;
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        await _authSession.RestoreAsync(cancellationToken);
        if (!_authSession.IsLoggedIn || string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            throw new SsalddelApiException(
                "이 작업은 서버 인증이 필요합니다. 먼저 웹 로그인에서 기사 계정으로 로그인해 주세요.",
                (int)HttpStatusCode.Unauthorized, "기사 운송", string.Empty, null);
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        return request;
    }

    private static string ResolveCommandName(운송증빙단계 단계)
        => 단계 switch
        {
            운송증빙단계.상차 => "TransportPickupComplete",
            운송증빙단계.하차 => "TransportDropoffComplete",
            _ => "TransportIssueEvidence"
        };

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string actionName, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
            ? $"{actionName} 실패: HTTP {(int)response.StatusCode}"
            : $"{actionName} 실패: HTTP {(int)response.StatusCode}: {body}");
    }

    private void Observe(기사운송요약응답 item, string source)
    {
        if (string.IsNullOrWhiteSpace(item.운송번호))
        {
            return;
        }

        _ledgerObserver.Observe(
            new TransportRequestLedgerSnapshot(
                item.운송번호,
                item.상태,
                null,
                item.상태,
                null,
                DateTimeOffset.UtcNow,
                source),
            source);
    }

    private void Observe(기사운송상태변경응답 item, string source)
    {
        if (string.IsNullOrWhiteSpace(item.운송번호))
        {
            return;
        }

        _ledgerObserver.Observe(
            new TransportRequestLedgerSnapshot(
                item.운송번호,
                item.상태,
                null,
                item.상태,
                null,
                DateTimeOffset.UtcNow,
                source),
            source);
        _ledgerObserver.RequestRefresh(item.운송번호, source);
    }
}

public enum 운송증빙단계
{
    상차,
    하차,
    예외
}

public sealed class 기사운송사진업로드결과
{
    public string BucketName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class 기사상차인수증입력
{
    public string? 인수증증빙방식 { get; set; }
    public string? 인수자명 { get; set; }
    public string? 인수자소속 { get; set; }
    public string? 인수자서명 { get; set; }
    public string? 기사서명 { get; set; }
    public bool 인수증확인완료 { get; set; }
    public bool 인수증서명생략확인 { get; set; }
    public string? 인수증서명생략사유 { get; set; }
}
