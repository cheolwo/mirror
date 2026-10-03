using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Contracts.Common.Operations;

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
    private readonly AuthApiService _authApiService;
    private readonly DriverOperatingProfileService _operatingProfileService;

    public DriverApiClient(
        HttpClient httpClient,
        IAuthSession authSession,
        AuthApiService authApiService,
        DriverOperatingProfileService operatingProfileService)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _authApiService = authApiService;
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
        var authenticationError = await _authApiService.EnsureAccessTokenAsync(
            cancellationToken: cancellationToken);
        if (authenticationError is not null)
        {
            ThrowAuthenticationFailure(authenticationError);
        }

        var requestSessionVersion = _authSession.Version;
        var requestSessionRevision = _authSession.SessionRevision;
        ApplyRequestHeaders(request);
        EnsureSameSession(requestSessionVersion);
        using var retryRequest = await CloneRequestAsync(request, cancellationToken);

        try
        {
            var response = ValidateResponse(
                await _httpClient.SendAsync(request, cancellationToken), requestSessionRevision, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            response.Dispose();
            EnsureSameSession(requestSessionVersion);
            authenticationError = await _authApiService.EnsureAccessTokenAsync(
                forceRefresh: true,
                cancellationToken: cancellationToken,
                expectedSessionVersion: requestSessionVersion);
            if (authenticationError is not null)
            {
                ThrowAuthenticationFailure(authenticationError);
            }

            // A successful refresh advances exactly this session. A newer login must not
            // receive the old request or be cleared by its delayed unauthorized response.
            var retrySessionVersion = requestSessionVersion + 1;
            EnsureSameSession(retrySessionVersion);
            ApplyRequestHeaders(retryRequest);
            EnsureSameSession(retrySessionVersion);
            var retryResponse = ValidateResponse(
                await _httpClient.SendAsync(retryRequest, cancellationToken), requestSessionRevision, cancellationToken);
            if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                retryResponse.Dispose();
                bool cleared;
                try
                {
                    cleared = await _authSession.TryClearAsync(retrySessionVersion, cancellationToken);
                }
                catch when (!_authSession.IsAuthenticated && _authSession.Version == retrySessionVersion + 1
                    && !cancellationToken.IsCancellationRequested)
                {
                    cleared = true;
                }
                if (!cleared)
                    ThrowAuthenticationFailure("로그인 정보가 변경되었습니다. 현재 업무를 다시 확인해 주세요.");
                throw new UnauthorizedAccessException("로그인 세션이 만료되었습니다. 다시 로그인해 주세요.");
            }
            return retryResponse;
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

    private HttpResponseMessage ValidateResponse(HttpResponseMessage response, long expectedSessionRevision, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_authSession.SessionRevision != expectedSessionRevision)
                throw new InvalidOperationException("로그인 정보가 변경되었습니다. 현재 업무를 다시 확인해 주세요.");
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private void ThrowAuthenticationFailure(string message)
    {
        if (_authSession.IsAuthenticated)
            throw new InvalidOperationException(message);
        throw new UnauthorizedAccessException(message);
    }

    private void EnsureSameSession(long expectedVersion)
    {
        if (_authSession.Version != expectedVersion)
            throw new InvalidOperationException("로그인 정보가 변경되었습니다. 현재 업무를 다시 확인해 주세요.");
    }

    private void ApplyRequestHeaders(HttpRequestMessage request)
    {
        request.Headers.Authorization = string.IsNullOrWhiteSpace(_authSession.AccessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);

        if (!request.Headers.Contains(OperatingMarketContextKeys.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(
                OperatingMarketContextKeys.HeaderName,
                _operatingProfileService.Current.MarketCode);
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage source,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (source.Content is not null)
        {
            var bytes = await source.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in source.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
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
