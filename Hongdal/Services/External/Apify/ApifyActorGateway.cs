using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.External.Apify;

public sealed record ApifyActorSyncRequest(
    string ActorId,
    JsonElement Input,
    int TimeoutSeconds,
    int MemoryMegabytes,
    int? MaxItems = null,
    decimal? MaxTotalChargeUsd = null,
    string? Build = null);

public sealed record ApifyActorSyncResult(
    string ActorId,
    IReadOnlyList<JsonElement> Items);

public interface IApifyActorGateway
{
    Task<ApifyActorSyncResult> RunSyncGetDatasetItemsAsync(
        ApifyActorSyncRequest request,
        CancellationToken cancellationToken);
}

public sealed partial class ApifyActorGateway : IApifyActorGateway
{
    private readonly HttpClient _httpClient;
    private readonly ApifyOptions _options;

    public ApifyActorGateway(
        HttpClient httpClient,
        IOptions<ApifyOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ApifyActorSyncResult> RunSyncGetDatasetItemsAsync(
        ApifyActorSyncRequest request,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var actorId = NormalizeActorId(request.ActorId);
        EnsureActorAllowed(actorId);
        var runPath = BuildRunPath(actorId, request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, runPath);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        httpRequest.Content = JsonContent.Create(request.Input);

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadSafeErrorAsync(response, cancellationToken);
            throw new HttpRequestException(
                $"Apify Actor 실행에 실패했습니다. Actor={actorId}, HTTP {(int)response.StatusCode}: {message}",
                null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Apify 동기 실행 응답이 dataset item 배열이 아닙니다.");
        }

        var maxItems = request.MaxItems ?? int.MaxValue;
        var items = document.RootElement
            .EnumerateArray()
            .Take(maxItems)
            .Select(item => item.Clone())
            .ToArray();
        return new ApifyActorSyncResult(actorId, items);
    }

    private string BuildRunPath(string actorId, ApifyActorSyncRequest request)
    {
        var timeout = Math.Clamp(request.TimeoutSeconds, 30, 300);
        var memory = ValidateMemory(request.MemoryMegabytes);
        var chargeCap = ValidateChargeCap(request.MaxTotalChargeUsd ?? _options.MaxTotalChargeUsd);
        var query = new List<string>
        {
            $"timeout={timeout}",
            $"memory={memory}",
            $"maxTotalChargeUsd={chargeCap.ToString(CultureInfo.InvariantCulture)}"
        };

        if (request.MaxItems is not null)
        {
            if (request.MaxItems is < 1 or > 10_000)
            {
                throw new InvalidOperationException("Apify MaxItems는 1에서 10000 사이여야 합니다.");
            }

            query.Add($"maxItems={request.MaxItems.Value}");
        }

        if (!string.IsNullOrWhiteSpace(request.Build))
        {
            query.Add($"build={Uri.EscapeDataString(request.Build.Trim())}");
        }

        return $"actors/{actorId}/run-sync-get-dataset-items?{string.Join('&', query)}";
    }

    private decimal ValidateChargeCap(decimal requestedCap)
    {
        if (_options.MaxTotalChargeUsd <= 0)
        {
            throw new InvalidOperationException("Apify:MaxTotalChargeUsd는 0보다 커야 합니다.");
        }

        if (requestedCap <= 0 || requestedCap > _options.MaxTotalChargeUsd)
        {
            throw new InvalidOperationException(
                $"Apify 호출 비용 상한은 0보다 크고 전역 상한 {_options.MaxTotalChargeUsd} USD 이하여야 합니다.");
        }

        return requestedCap;
    }

    private static int ValidateMemory(int memoryMegabytes)
    {
        if (memoryMegabytes is < 128 or > 4096
            || (memoryMegabytes & (memoryMegabytes - 1)) != 0)
        {
            throw new InvalidOperationException(
                "Apify MemoryMegabytes는 128에서 4096 사이의 2의 거듭제곱이어야 합니다.");
        }

        return memoryMegabytes;
    }

    private static string NormalizeActorId(string actorId)
    {
        var normalized = actorId.Trim();
        if (!ActorIdPattern().IsMatch(normalized))
        {
            throw new InvalidOperationException("Apify ActorId 형식이 올바르지 않습니다.");
        }

        return normalized;
    }

    private void EnsureActorAllowed(string actorId)
    {
        if (!(_options.AllowedActorIds ?? []).Any(
                allowed => string.Equals(allowed?.Trim(), actorId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"허용되지 않은 Apify Actor입니다: {actorId}");
        }
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Apify Actor 실행이 비활성화되어 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            throw new InvalidOperationException("Apify:ApiToken 비밀 설정이 필요합니다.");
        }
    }

    private static async Task<string> ReadSafeErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return response.ReasonPhrase ?? "응답 본문 없음";
        }

        var singleLine = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= 800 ? singleLine : singleLine[..800];
    }

    [GeneratedRegex(
        "^[A-Za-z0-9_.-]+~[A-Za-z0-9_.-]+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ActorIdPattern();
}
