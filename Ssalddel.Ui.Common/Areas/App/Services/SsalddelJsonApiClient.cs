using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public sealed class SsalddelApiException : InvalidOperationException
{
    public SsalddelApiException(
        string message,
        int statusCode,
        string operationName,
        string responseBody,
        string? traceId,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null,
        string? failureClassCode = null,
        string? responsibilityRoleCode = null,
        bool requiresStateRefresh = false,
        string? retryPolicyCode = null,
        IReadOnlyList<string>? availableRecoveryActions = null,
        long? currentRevision = null,
        DateTime? retryAfterUtc = null,
        string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        OperationName = operationName;
        ResponseBody = responseBody;
        TraceId = traceId;
        FieldErrors = fieldErrors ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        FailureClassCode = failureClassCode;
        ResponsibilityRoleCode = responsibilityRoleCode;
        RequiresStateRefresh = requiresStateRefresh;
        RetryPolicyCode = retryPolicyCode;
        AvailableRecoveryActions = availableRecoveryActions ?? [];
        CurrentRevision = currentRevision;
        RetryAfterUtc = retryAfterUtc;
        ErrorCode = errorCode;
    }

    public int StatusCode { get; }
    public string OperationName { get; }
    public string ResponseBody { get; }
    public string? TraceId { get; }
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }
    public string? FailureClassCode { get; }
    public string? ResponsibilityRoleCode { get; }
    public bool RequiresStateRefresh { get; }
    public string? RetryPolicyCode { get; }
    public IReadOnlyList<string> AvailableRecoveryActions { get; }
    public long? CurrentRevision { get; }
    public DateTime? RetryAfterUtc { get; }
    public string? ErrorCode { get; }
}

public sealed record SsalddelApiProblem(
    string? Message,
    string? TraceId,
    string? ErrorCode,
    IReadOnlyDictionary<string, string[]> FieldErrors,
    string? FailureClassCode,
    string? ResponsibilityRoleCode,
    bool RequiresStateRefresh,
    string? RetryPolicyCode,
    IReadOnlyList<string> AvailableRecoveryActions,
    long? CurrentRevision,
    DateTime? RetryAfterUtc);

public static class SsalddelApiProblemParser
{
    public static SsalddelApiProblem Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Empty();
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("errors", out var errorElement)
                && errorElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in errorElement.EnumerateObject())
                {
                    errors[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                        ? property.Value.EnumerateArray()
                            .Where(item => item.ValueKind == JsonValueKind.String)
                            .Select(item => item.GetString() ?? string.Empty)
                            .Where(item => !string.IsNullOrWhiteSpace(item))
                            .ToArray()
                        : [property.Value.ToString()];
                }
            }

            var actions = root.TryGetProperty("availableRecoveryActions", out var actionElement)
                          && actionElement.ValueKind == JsonValueKind.Array
                ? actionElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : [];

            return new SsalddelApiProblem(
                ReadString(root, "detail") ?? ReadString(root, "message") ?? ReadString(root, "title"),
                ReadString(root, "traceId"),
                ReadString(root, "errorCode"),
                errors,
                ReadString(root, "failureClassCode"),
                ReadString(root, "responsibilityRoleCode"),
                ReadBoolean(root, "requiresStateRefresh"),
                ReadString(root, "retryPolicyCode"),
                actions,
                ReadInt64(root, "currentRevision"),
                ReadDateTime(root, "retryAfterUtc"));
        }
        catch (JsonException)
        {
            return Empty();
        }
    }

    private static SsalddelApiProblem Empty()
        => new(null, null, null, new Dictionary<string, string[]>(), null, null, false, null, [], null, null);

    private static string? ReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBoolean(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value)
           && value.ValueKind is JsonValueKind.True or JsonValueKind.False
           && value.GetBoolean();

    private static long? ReadInt64(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var parsed)
            ? parsed
            : null;

    private static DateTime? ReadDateTime(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.String
           && value.TryGetDateTime(out var parsed)
            ? parsed
            : null;
}

/// <summary>
/// 역할별 타입드 API 서비스가 공통으로 사용하는 JSON 호출 계층입니다.
/// 인증 헤더와 ISMS-P 요청 암호화는 <see cref="SsalddelProtectedApiClient"/>가 담당합니다.
/// </summary>
public interface ISsalddelJsonApiClient
{
    Task<TResponse?> GetAsync<TResponse>(
        string path,
        string operationName,
        bool allowNotFound = true,
        CancellationToken cancellationToken = default);

    Task<TResponse?> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        string operationName,
        bool allowNotFound = false,
        CancellationToken cancellationToken = default);

    Task<TResponse?> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest request,
        string operationName,
        bool allowNotFound = false,
        CancellationToken cancellationToken = default);

    Task<TResponse?> SendWithHeadersAsync<TResponse>(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string> headers,
        string operationName,
        bool allowNotFound = false,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("이 JSON API 클라이언트는 사용자 지정 요청 헤더를 지원하지 않습니다.");

    Task<TResponse?> SendWithHeadersAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest request,
        IReadOnlyDictionary<string, string> headers,
        string operationName,
        bool allowNotFound = false,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("이 JSON API 클라이언트는 사용자 지정 요청 헤더를 지원하지 않습니다.");

    Task SendAsync(
        HttpMethod method,
        string path,
        string operationName,
        CancellationToken cancellationToken = default);

    Task SendAsync<TRequest>(
        HttpMethod method,
        string path,
        TRequest request,
        string operationName,
        CancellationToken cancellationToken = default);
}

public sealed class SsalddelJsonApiClient : ISsalddelJsonApiClient
{
    private readonly SsalddelProtectedApiClient _protectedClient;

    public SsalddelJsonApiClient(SsalddelProtectedApiClient protectedClient)
    {
        _protectedClient = protectedClient;
    }

    public Task<TResponse?> GetAsync<TResponse>(
        string path,
        string operationName,
        bool allowNotFound = true,
        CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(HttpMethod.Get, path, operationName, allowNotFound, cancellationToken);

    public async Task<TResponse?> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        string operationName,
        bool allowNotFound = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(method, path, operationName, cancellationToken);
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

    public async Task<TResponse?> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest request,
        string operationName,
        bool allowNotFound = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(method, path, request, operationName, cancellationToken);
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

    public async Task<TResponse?> SendWithHeadersAsync<TResponse>(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string> headers,
        string operationName,
        bool allowNotFound = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(method, path, headers, operationName, cancellationToken);
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

    public async Task<TResponse?> SendWithHeadersAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest request,
        IReadOnlyDictionary<string, string> headers,
        string operationName,
        bool allowNotFound = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(method, path, request, headers, operationName, cancellationToken);
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

    public async Task SendAsync(
        HttpMethod method,
        string path,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(method, path, operationName, cancellationToken);
        await EnsureSuccessAsync(response, operationName, cancellationToken);
    }

    public async Task SendAsync<TRequest>(
        HttpMethod method,
        string path,
        TRequest request,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(method, path, request, operationName, cancellationToken);
        await EnsureSuccessAsync(response, operationName, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method,
        string path,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _protectedClient.SendAsync(method, path, cancellationToken);
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

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string> headers,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _protectedClient.SendAsync(method, path, headers, cancellationToken);
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

    private async Task<HttpResponseMessage> SendCoreAsync<TRequest>(
        HttpMethod method,
        string path,
        TRequest request,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _protectedClient.SendAsProtectedJsonAsync(method, path, request, cancellationToken);
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

    private async Task<HttpResponseMessage> SendCoreAsync<TRequest>(
        HttpMethod method,
        string path,
        TRequest request,
        IReadOnlyDictionary<string, string> headers,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _protectedClient.SendAsProtectedJsonAsync(
                method,
                path,
                request,
                headers,
                cancellationToken);
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
        var problem = SsalddelApiProblemParser.Parse(body);
        var statusCode = (int)response.StatusCode;
        var detail = problem.Message ?? (string.IsNullOrWhiteSpace(body) ? null : body);
        throw new SsalddelApiException(
            $"{operationName} API 실패: HTTP {statusCode}{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $": {detail}")}",
            statusCode,
            operationName,
            body,
            problem.TraceId,
            problem.FieldErrors,
            problem.FailureClassCode,
            problem.ResponsibilityRoleCode,
            problem.RequiresStateRefresh,
            problem.RetryPolicyCode,
            problem.AvailableRecoveryActions,
            problem.CurrentRevision,
            problem.RetryAfterUtc,
            problem.ErrorCode);
    }
}
