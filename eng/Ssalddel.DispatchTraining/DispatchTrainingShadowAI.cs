using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ssalddel.DispatchTraining;

/// <summary>익명 후보의 숫자만 받는다. 원장·주소·기사 식별자·자유 서술은 입력하지 않는다.</summary>
public sealed record DispatchTrainingShadowCandidate(
    string Token,
    decimal? Score,
    decimal? DistanceKm,
    decimal? AgingScore,
    int? PickupStage = null,
    decimal? ArrivalDifferenceMinutes = null,
    int? DemandPriority = null);

public sealed record DispatchTrainingShadowInput(
    string BaselineToken,
    IReadOnlyList<DispatchTrainingShadowCandidate> Candidates);

/// <summary>성공한 유효 토큰만 비교할 수 있다. 오류는 기존 선택에 일치한 것으로 세지 않는다.</summary>
public sealed record DispatchTrainingShadowResult(
    string StatusCode,
    string? SelectedToken,
    bool Comparable,
    bool? MatchesBaseline = null);

/// <summary>로컬 학습 도구의 비교 전용 호출이다. 실제 추천·수락·원장 변경 경로에 등록하지 않는다.</summary>
public sealed class DispatchTrainingShadowAI : IDisposable
{
    public const int SessionCallLimit = 15;
    public const int TimeoutSeconds = 2;
    public const string Model = "qwen3:1.7b";
    private const int MaxResponseBytes = 64 * 1024;
    private static readonly Uri Endpoint = new("http://127.0.0.1:12681/api/chat");
    private static readonly Regex TokenPattern = new("\\AC[1-9][0-9]{0,2}\\z", RegexOptions.CultureInvariant);
    private const string Instructions = "Compare anonymous dispatch candidates already evaluated by the server. "
        + "Select only one supplied candidate token. The baseline is the unchanged engine decision. "
        + "Do not create candidates, change eligibility, perform dispatch or invent missing values. "
        + "Prefer a longer waiter only when feasible pickup conditions are comparable; agingScore is not actual waiting minutes. "
        + "Demand/capacity has priority over time of day. Return exactly {\"candidateToken\":\"C1\"} with a supplied token, "
        + "without tools, commentary or additional keys.";

    private readonly HttpClient _client;
    private int _calls;

    public DispatchTrainingShadowAI(HttpMessageHandler? handler = null)
    {
        _client = new(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };
    }

    public int CallsUsed => Volatile.Read(ref _calls);

    public async Task<DispatchTrainingShadowResult> CompareAsync(
        DispatchTrainingShadowInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input is null || input.Candidates is null || input.Candidates.Count > 30
            || !Token(input.BaselineToken)
            || input.Candidates.Any(x => x is null || !Token(x.Token)
                || x.DistanceKm is < 0m || x.AgingScore is < 0m
                || x.PickupStage is < 0 or > 2 || x.ArrivalDifferenceMinutes is < 0m
                || x.DemandPriority is < 1)
            || input.Candidates.Select(x => x.Token).Distinct(StringComparer.Ordinal).Count() != input.Candidates.Count
            || !input.Candidates.Any(x => x.Token == input.BaselineToken))
            return Unavailable("InvalidInput");
        if (input.Candidates.Count < 2) return Unavailable("SingleCandidate");
        if (!ReserveCall()) return Unavailable("CallBudgetExhausted");

        // 입력 컬렉션을 복사해 호출 중 외부 변경으로 토큰 집합이 달라지지 않게 한다.
        var candidates = input.Candidates.ToArray();
        var tokens = candidates.Select(x => x.Token).ToArray();
        var body = new
        {
            model = Model,
            stream = false,
            think = false,
            options = new { temperature = 0, num_predict = 32 },
            format = new
            {
                type = "object", additionalProperties = false,
                required = new[] { "candidateToken" },
                properties = new { candidateToken = new { type = "string", @enum = tokens } }
            },
            messages = new[]
            {
                new { role = "system", content = Instructions },
                new { role = "user", content = JsonSerializer.Serialize(new
                {
                    baselineToken = input.BaselineToken,
                    candidates = candidates.Select(x => new
                    {
                        candidateToken = x.Token, score = x.Score, distanceKm = x.DistanceKm,
                        agingScore = x.AgingScore, pickupStage = x.PickupStage,
                        arrivalDifferenceMinutes = x.ArrivalDifferenceMinutes, demandPriority = x.DemandPriority
                    })
                }) }
            }
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return Unavailable("HttpFailure");
            if (response.Content.Headers.ContentLength is > MaxResponseBytes) return Unavailable("ResponseTooLarge");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var bytes = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                var count = await stream.ReadAsync(buffer, timeout.Token);
                if (count == 0) break;
                if (bytes.Length + count > MaxResponseBytes) return Unavailable("ResponseTooLarge");
                bytes.Write(buffer, 0, count);
            }
            using var envelope = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 12 });
            RejectDuplicateProperties(envelope.RootElement);
            if (envelope.RootElement.ValueKind != JsonValueKind.Object
                || !envelope.RootElement.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("role", out var role) || role.GetString() != "assistant"
                || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String
                || message.TryGetProperty("tool_calls", out var calls)
                    && (calls.ValueKind != JsonValueKind.Array || calls.GetArrayLength() != 0))
                return Unavailable("InvalidResponse");
            using var decision = JsonDocument.Parse(content.GetString()!, new JsonDocumentOptions { MaxDepth = 4 });
            RejectDuplicateProperties(decision.RootElement);
            if (decision.RootElement.ValueKind != JsonValueKind.Object) return Unavailable("InvalidResponse");
            var properties = decision.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 1 || properties[0].Name != "candidateToken"
                || properties[0].Value.ValueKind != JsonValueKind.String)
                return Unavailable("InvalidResponse");
            var token = properties[0].Value.GetString();
            if (token is null || !tokens.Contains(token, StringComparer.Ordinal)) return Unavailable("InvalidSelection");
            return new("Applied", token, true, token == input.BaselineToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Unavailable("Timeout"); }
        catch (HttpRequestException) { return Unavailable("NetworkFailure"); }
        catch (IOException) { return Unavailable("NetworkFailure"); }
        catch (JsonException) { return Unavailable("InvalidResponse"); }
        catch (InvalidOperationException) { return Unavailable("InvalidResponse"); }
    }

    private bool ReserveCall()
    {
        while (true)
        {
            var current = Volatile.Read(ref _calls);
            if (current >= SessionCallLimit) return false;
            if (Interlocked.CompareExchange(ref _calls, current + 1, current) == current) return true;
        }
    }

    private static bool Token(string? value) => value is not null && TokenPattern.IsMatch(value);
    private static DispatchTrainingShadowResult Unavailable(string code) => new(code, null, false);

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate response field.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in element.EnumerateArray()) RejectDuplicateProperties(value);
        }
    }

    public void Dispose() => _client.Dispose();
}
