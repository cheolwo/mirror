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

    public AdminAuthenticatedApiClient(HttpClient httpClient, AdminAuthSession session, AdminAuthService authService)
    { this.httpClient = httpClient; this.session = session; this.authService = authService; }

    public Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        => ReadResponseAsync<T>(HttpMethod.Get, path, null, cancellationToken);

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var response = await SendWithRefreshAsync(HttpMethod.Delete, path, ct);
        EnsureSameAuthentication(response.Context);
    }

    public Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default)
        => ReadResponseAsync<TResponse>(HttpMethod.Post, path, body, cancellationToken);

    public Task<TResponse> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default)
        => ReadResponseAsync<TResponse>(HttpMethod.Put, path, body, cancellationToken);

    private async Task<T> ReadResponseAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await SendWithRefreshAsync(method, path, ct, body);
        var result = await response.Message.Content.ReadFromJsonAsync<T>(ct);
        EnsureSameAuthentication(response.Context);
        return result ?? throw new AdminApiException("서버 응답을 읽을 수 없습니다.", response.Message.StatusCode);
    }

    private async Task<AuthenticatedResponse> SendWithRefreshAsync(HttpMethod method, string path,
        CancellationToken cancellationToken, object? body = null)
    {
        var beforeRestore = session.CaptureAuthentication();
        await session.RestoreAsync(cancellationToken);
        if (!session.TryCaptureAuthenticationAfterRestore(beforeRestore, out var origin))
            throw new AdminApiException("관리자 로그인 상태가 변경되었습니다. 현재 계정으로 다시 조회해 주세요.", HttpStatusCode.Unauthorized);
        var authenticationError = await authService.EnsureAccessTokenForRequestAsync(
            forceRefresh: false, expected: origin, cancellationToken: cancellationToken);
        if (authenticationError is not null) throw new AdminApiException(authenticationError, HttpStatusCode.Unauthorized);
        var sending = session.CaptureAuthentication();
        EnsureSameAuthentication(origin);
        var response = await SendOnceAsync(method, path, cancellationToken, body, sending.AccessToken);
        EnsureSameAuthentication(origin, response);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            authenticationError = await authService.EnsureAccessTokenForRequestAsync(
                forceRefresh: true, expected: sending, cancellationToken: cancellationToken);
            if (authenticationError is not null) throw new AdminApiException(authenticationError, HttpStatusCode.Unauthorized);
            sending = session.CaptureAuthentication();
            EnsureSameAuthentication(origin);
            response = await SendOnceAsync(method, path, cancellationToken, body, sending.AccessToken);
            EnsureSameAuthentication(origin, response);
        }
        if (!response.IsSuccessStatusCode) await ThrowApiExceptionAsync(response, origin, cancellationToken);
        return new(response, origin);
    }

    private void EnsureSameAuthentication(AdminAuthenticationContext context, HttpResponseMessage? response = null)
    {
        if (session.IsSameAuthentication(context)) return;
        response?.Dispose();
        throw new AdminApiException("관리자 로그인 상태가 변경되었습니다. 현재 계정으로 다시 조회해 주세요.", HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path,
        CancellationToken cancellationToken, object? body, string? accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try { return await httpClient.SendAsync(request, cancellationToken); }
        catch (HttpRequestException ex) { throw new AdminApiException("살뜰 서비스에 연결할 수 없습니다.", null, ex); }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        { throw new AdminApiException("관리자 API 응답 시간이 초과되었습니다.", null, ex); }
    }

    private async Task ThrowApiExceptionAsync(HttpResponseMessage response, AdminAuthenticationContext context, CancellationToken ct)
    {
        string content;
        var statusCode = response.StatusCode;
        try { content = await response.Content.ReadAsStringAsync(ct); }
        finally { response.Dispose(); }
        EnsureSameAuthentication(context);
        var message = statusCode == HttpStatusCode.Unauthorized
            ? "관리자 로그인이 만료되었습니다. 다시 로그인해 주세요." : ReadProblemMessage(content);
        throw new AdminApiException(string.IsNullOrWhiteSpace(message) ? "관리자 요청을 처리하지 못했습니다." : message,
            statusCode, errorCode: ReadProblemErrorCode(content));
    }

    private static string? ReadProblemErrorCode(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("errorCode", out var code)
                && code.ValueKind == JsonValueKind.String ? code.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string ReadProblemMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return string.Empty;
            var message = Ssalddel.Client.Infrastructure.Http.ProblemMessagePropertyReader.Read(document.RootElement);
            if (message is not null) return message;
        }
        catch (JsonException) { }
        return string.Empty;
    }

    private sealed record AuthenticatedResponse(HttpResponseMessage Message, AdminAuthenticationContext Context) : IDisposable
    { public void Dispose() => Message.Dispose(); }
}

public sealed class AdminApiException : Exception
{
    public AdminApiException(string message, HttpStatusCode? statusCode, Exception? innerException = null, string? errorCode = null)
        : base(message, innerException) { StatusCode = statusCode; ErrorCode = errorCode; }
    public HttpStatusCode? StatusCode { get; }
    public string? ErrorCode { get; }
}
