using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed class HongdalApiException : InvalidOperationException
{
    public HongdalApiException(
        string message,
        int statusCode,
        string operationName,
        string responseBody,
        string? traceId,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
        : base(message)
    {
        StatusCode = statusCode;
        OperationName = operationName;
        ResponseBody = responseBody;
        TraceId = traceId;
        FieldErrors = fieldErrors ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    public int StatusCode { get; }
    public string OperationName { get; }
    public string ResponseBody { get; }
    public string? TraceId { get; }
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }
}

/// <summary>
/// 역할별 타입드 API 서비스가 공통으로 사용하는 JSON 호출 계층입니다.
/// 인증 헤더와 ISMS-P 요청 암호화는 <see cref="HongdalProtectedApiClient"/>가 담당합니다.
/// </summary>
public interface IHongdalJsonApiClient
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

public sealed class HongdalJsonApiClient : IHongdalJsonApiClient
{
    private readonly HongdalProtectedApiClient _protectedClient;

    public HongdalJsonApiClient(HongdalProtectedApiClient protectedClient)
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
        var problem = ParseProblem(body);
        var statusCode = (int)response.StatusCode;
        var detail = problem.Message ?? (string.IsNullOrWhiteSpace(body) ? null : body);
        throw new HongdalApiException(
            $"{operationName} API 실패: HTTP {statusCode}{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $": {detail}")}",
            statusCode,
            operationName,
            body,
            problem.TraceId,
            problem.FieldErrors);
    }

    private static (string? Message, string? TraceId, IReadOnlyDictionary<string, string[]> FieldErrors) ParseProblem(
        string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null, new Dictionary<string, string[]>());
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var message = ReadString(root, "detail")
                          ?? ReadString(root, "message")
                          ?? ReadString(root, "title");
            var traceId = ReadString(root, "traceId");
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

            return (message, traceId, errors);
        }
        catch (JsonException)
        {
            return (null, null, new Dictionary<string, string[]>());
        }
    }

    private static string? ReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
