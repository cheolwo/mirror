using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Contracts.Admin.Management;

namespace SsalddelAdmin.Services;

public interface I기사실차적재확인Client
{
    Task<기사실차적재제원응답> 실차조회Async(string driverId, CancellationToken ct = default);
    Task<기사실차적재제원응답> 실차수정Async(string driverId, 기사실차적재제원수정요청 payload, CancellationToken ct = default);
}

public sealed class 차량관리Service : I기사실차적재확인Client
{
    private readonly HttpClient _httpClient;
    private readonly 관리자인증세션Service _session;

    public 차량관리Service(HttpClient httpClient, 관리자인증세션Service session)
    {
        _httpClient = httpClient;
        _session = session;
    }

    public async Task<IReadOnlyList<차량단가응답>> 차량단가목록조회Async(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/v1/vehicle-rates");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<차량단가응답>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<차량단가응답?> 차량단가수정Async(long id, 차량단가요청 payload, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/v1/vehicle-rates/{id}");
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<차량단가응답>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<차량추천기준응답>> 차량추천기준목록조회Async(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/v1/admin/vehicle-recommendations/criteria");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<차량추천기준응답>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<차량추천기준응답?> 차량추천기준수정Async(string vehicleCode, 차량추천기준수정요청 payload, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/v1/admin/vehicle-recommendations/criteria/{Uri.EscapeDataString(vehicleCode)}");
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<차량추천기준응답>(cancellationToken: cancellationToken);
    }

    public async Task<차량추천시뮬레이션응답> 차량추천시뮬레이션Async(차량추천시뮬레이션요청 payload, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/v1/admin/vehicle-recommendations/simulate");
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<차량추천시뮬레이션응답>(cancellationToken: cancellationToken)
               ?? new 차량추천시뮬레이션응답();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }

        return request;
    }

    public async Task<기사실차적재제원응답> 실차조회Async(string driverId, CancellationToken ct = default)
    {
        RequireActualAccess(driverId);
        using var request = CreateRequest(HttpMethod.Get, $"api/v1/admin/vehicle-recommendations/drivers/{Uri.EscapeDataString(driverId)}/actual-loading");
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<기사실차적재제원응답>(cancellationToken: ct) ?? throw new InvalidDataException();
    }

    public async Task<기사실차적재제원응답> 실차수정Async(string driverId, 기사실차적재제원수정요청 payload, CancellationToken ct = default)
    {
        RequireActualAccess(driverId);
        using var request = CreateRequest(HttpMethod.Put, $"api/v1/admin/vehicle-recommendations/drivers/{Uri.EscapeDataString(driverId)}/actual-loading");
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<기사실차적재제원응답>(cancellationToken: ct) ?? throw new InvalidDataException();
    }

    private void RequireActualAccess(string driverId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        if (!_session.로그인됨 || !_session.서버관리자인가)
            throw new HttpRequestException("관리자 로그인이 필요합니다.", null, System.Net.HttpStatusCode.Forbidden);
    }
}
