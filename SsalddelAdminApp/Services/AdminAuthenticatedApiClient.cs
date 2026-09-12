using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SsalddelAdminApp.Services;

public sealed class AdminAuthenticatedApiClient
{
    private readonly HttpClient httpClient;
    private readonly AdminAuthSession session;
    private readonly AdminAuthService authService;

    public AdminAuthenticatedApiClient(
        HttpClient httpClient,
        AdminAuthSession session,
        AdminAuthService authService)
    {
        this.httpClient = httpClient;
        this.session = session;
        this.authService = authService;
    }

    public async Task<T> GetAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRefreshAsync(HttpMethod.Get, path, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
               ?? throw new AdminApiException("서버 응답을 읽을 수 없습니다.", response.StatusCode);
    }

    public async Task<TResponse> PutAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRefreshAsync(HttpMethod.Put, path, cancellationToken, body);
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken)
               ?? throw new AdminApiException("서버 응답을 읽을 수 없습니다.", response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken,
        object? body = null)
    {
        var authenticationError = await authService.EnsureAccessTokenAsync(
            cancellationToken: cancellationToken);
        if (authenticationError is not null)
        {
            throw new AdminApiException(authenticationError, HttpStatusCode.Unauthorized);
        }

        var response = await SendOnceAsync(method, path, cancellationToken, body);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            authenticationError = await authService.EnsureAccessTokenAsync(
                forceRefresh: true,
                cancellationToken: cancellationToken);
            if (authenticationError is not null)
            {
                throw new AdminApiException(authenticationError, HttpStatusCode.Unauthorized);
            }

            response = await SendOnceAsync(method, path, cancellationToken, body);
        }

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, cancellationToken);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminApiException("살뜰 서비스에 연결할 수 없습니다.", null, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdminApiException("관리자 API 응답 시간이 초과되었습니다.", null, ex);
        }
    }

    private static async Task ThrowApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = response.StatusCode == HttpStatusCode.Unauthorized
            ? "관리자 로그인이 만료되었습니다. 다시 로그인해 주세요."
            : ReadProblemMessage(content);
        var statusCode = response.StatusCode;
        response.Dispose();
        throw new AdminApiException(
            string.IsNullOrWhiteSpace(message) ? "관리자 요청을 처리하지 못했습니다." : message,
            statusCode);
    }

    private static string ReadProblemMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            foreach (var name in new[] { "detail", "message", "title" })
            {
                if (document.RootElement.TryGetProperty(name, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
        }

        return content;
    }
}

public sealed class AdminApiException : Exception
{
    public AdminApiException(
        string message,
        HttpStatusCode? statusCode,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
