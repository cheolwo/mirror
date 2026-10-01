using System.Text.Json;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed class KamisJsonClient : IKamisJsonClient
{
    private const int MaxAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly ILogger<KamisJsonClient> _logger;

    public KamisJsonClient(
        HttpClient httpClient,
        ILogger<KamisJsonClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<JsonDocument> GetDocumentAsync(
        string requestPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPath);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    requestPath,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                }

                var isTransient = (int)response.StatusCode == 408
                                  || (int)response.StatusCode == 429
                                  || (int)response.StatusCode >= 500;
                if (!isTransient || attempt == MaxAttempts)
                {
                    throw new InvalidOperationException(
                        $"KAMIS HTTP 요청이 실패했습니다. 상태 코드={(int)response.StatusCode}");
                }

                _logger.LogWarning(
                    "KAMIS HTTP 요청을 재시도합니다. Attempt={Attempt}/{MaxAttempts}, StatusCode={StatusCode}",
                    attempt,
                    MaxAttempts,
                    (int)response.StatusCode);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(
                    "KAMIS 네트워크 요청을 재시도합니다. Attempt={Attempt}/{MaxAttempts}",
                    attempt,
                    MaxAttempts);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested
                                                && attempt < MaxAttempts)
            {
                _logger.LogWarning(
                    "KAMIS 시간 초과 요청을 재시도합니다. Attempt={Attempt}/{MaxAttempts}",
                    attempt,
                    MaxAttempts);
            }
            catch (HttpRequestException)
            {
                throw new InvalidOperationException("KAMIS 네트워크 요청에 실패했습니다.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("KAMIS 요청 제한 시간을 초과했습니다.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken);
        }

        throw new InvalidOperationException("KAMIS 요청 재시도 횟수를 초과했습니다.");
    }
}
