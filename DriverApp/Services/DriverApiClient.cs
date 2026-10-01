using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Common.Operations;

namespace DriverApp.Services;

/// <summary>
/// 기사 앱의 타입드 API 서비스들이 공통으로 사용하는 인증 HTTP 클라이언트입니다.
/// 컨트롤러 경로와 DTO 선택은 각 기능 서비스가 담당하고, 인증/오류/직렬화만 이곳에서 처리합니다.
/// </summary>
public interface IDriverApiClient
{
    Task<TResponse?> GetAsync<TResponse>(
        string path,
        string operationName,
        CancellationToken cancellationToken = default);

    Task<TResponse?> PostAsync<TResponse>(
        string path,
        string operationName,
        CancellationToken cancellationToken = default);

    Task<TResponse?> PostAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        string operationName,
        CancellationToken cancellationToken = default);

    Task PostAsync(
        string path,
        string operationName,
        CancellationToken cancellationToken = default);

    Task PostAsync<TRequest>(
        string path,
        TRequest payload,
        string operationName,
        CancellationToken cancellationToken = default);

    Task<TResponse?> PutAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        string operationName,
        CancellationToken cancellationToken = default);

    Task PutAsync<TRequest>(
        string path,
        TRequest payload,
        string operationName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string path,
        string operationName,
        CancellationToken cancellationToken = default);
}

public sealed class DriverApiClient : IDriverApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;
    private readonly DriverOperatingProfileService _operatingProfileService;

    public DriverApiClient(
        HttpClient httpClient,
        IAuthSession authSession,
        DriverOperatingProfileService operatingProfileService)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _operatingProfileService = operatingProfileService;
    }

    public Task<TResponse?> GetAsync<TResponse>(
        string path,
        string operationName,
        CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(
            new HttpRequestMessage(HttpMethod.Get, path),
            operationName,
            allowNotFound: true,
            cancellationToken);

    public Task<TResponse?> PostAsync<TResponse>(
        string path,
        string operationName,
        CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(
            new HttpRequestMessage(HttpMethod.Post, path),
            operationName,
            allowNotFound: false,
            cancellationToken);

    public Task<TResponse?> PostAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        string operationName,
        CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(
            CreateJsonRequest(HttpMethod.Post, path, payload),
            operationName,
            allowNotFound: false,
            cancellationToken);

    public Task PostAsync(
        string path,
        string operationName,
        CancellationToken cancellationToken = default)
        => SendAsync(
            new HttpRequestMessage(HttpMethod.Post, path),
            operationName,
            cancellationToken);

    public Task PostAsync<TRequest>(
        string path,
        TRequest payload,
        string operationName,
        CancellationToken cancellationToken = default)
        => SendAsync(
            CreateJsonRequest(HttpMethod.Post, path, payload),
            operationName,
            cancellationToken);

    public Task<TResponse?> PutAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        string operationName,
        CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(
            CreateJsonRequest(HttpMethod.Put, path, payload),
            operationName,
            allowNotFound: false,
            cancellationToken);

    public Task PutAsync<TRequest>(
        string path,
        TRequest payload,
        string operationName,
        CancellationToken cancellationToken = default)
        => SendAsync(
            CreateJsonRequest(HttpMethod.Put, path, payload),
            operationName,
            cancellationToken);

    public Task DeleteAsync(
        string path,
        string operationName,
        CancellationToken cancellationToken = default)
        => SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, path),
            operationName,
            cancellationToken);

    private static HttpRequestMessage CreateJsonRequest<TRequest>(
        HttpMethod method,
        string path,
        TRequest payload)
        => new(method, path)
        {
            Content = JsonContent.Create(payload)
        };

    private async Task<TResponse?> SendAsync<TResponse>(
        HttpRequestMessage request,
        string operationName,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await SendCoreAsync(request, operationName, cancellationToken))
        {
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            await EnsureSuccessAsync(response, operationName, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return default;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
        }
    }

    private async Task SendAsync(
        HttpRequestMessage request,
        string operationName,
        CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await SendCoreAsync(request, operationName, cancellationToken))
        {
            await EnsureSuccessAsync(response, operationName, cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage request,
        string operationName,
        CancellationToken cancellationToken)
    {
        await _authSession.RestoreAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        }

        if (!request.Headers.Contains(OperatingMarketContextKeys.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(
                OperatingMarketContextKeys.HeaderName,
                _operatingProfileService.Current.MarketCode);
        }

        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"{operationName} API에 연결할 수 없습니다.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"{operationName} API 응답 시간이 초과되었습니다.", ex);
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operationName,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = string.IsNullOrWhiteSpace(body) ? string.Empty : $": {body}";
        throw new InvalidOperationException(
            $"{operationName} API 실패: HTTP {(int)response.StatusCode}{detail}");
    }
}
