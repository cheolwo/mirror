using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>격리 로컬 화물 시험에서 서버가 허용한 익명 후보만 비교한다. 설치·학습·배차 쓰기 기능은 없다.</summary>
internal sealed class CargoAiSelector
{
    internal const string Model = "qwen3:1.7b";
    internal const string Endpoint = "http://127.0.0.1:12681/";
    internal const int MaximumSeconds = 30;
    private const int MaximumBodyBytes = 64 * 1024;
    private const string Instruction = "Select one candidate token or hold for a SYNTHETIC local cargo dispatch test. "
        + "Only supplied SERVER_ALLOWED candidates can be selected; no tools or operational instructions. "
        + "Inputs are synthetic test conditions, not actual addresses, GPS, cargo or people. "
        + "Legal-dong codes are labels only and do not prove adjacency or travel time. "
        + "Respect existing commitments and missing information. A busy driver is available only after the supplied remaining time at the final stop. "
        + "When feasible pickup conditions and distance are comparable, prefer a larger waitingScore. "
        + "waitingScore is a policy score, NOT actual minutes. Never invent an ETA or waiting duration. "
        + "Reason rules: SERVER_ALLOWED requires selecting a supplied candidate, never hold. "
        + "LONGER_WAIT_SCORE requires that candidate's waitingScore is positive and at least every other known candidate waitingScore; all waitingScores must be known. "
        + "SHORTER_PICKUP_DISTANCE requires that candidate's distanceKm is no greater than every other known candidate distanceKm; all distances must be known. "
        + "LAST_DESTINATION requires that candidate's availableAfterMinutes is positive. SINGLE_ALLOWED requires exactly one supplied candidate. "
        + "REVIEW_NEEDED is allowed ONLY when candidateToken is hold; NEVER combine REVIEW_NEEDED with a candidate selection. "
        + "For a candidate selection use SERVER_ALLOWED and only other factually supported reasons. If information is insufficient, choose hold with REVIEW_NEEDED only. "
        + "Return only {candidateToken,reasonCodes} JSON matching the supplied candidate-specific schema. "
        + "Do not change fare, eligibility, deadlines or policies. The selected candidate will be rechecked by the server.";

    internal async Task<CargoAiSelection> SelectAsync(CargoAiInput input, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var runId = Guid.NewGuid().ToString("N");
        var serialized = JsonSerializer.Serialize(input, Json);
        var inputHash = Hash(Encoding.UTF8.GetBytes(serialized));
        var schema = BuildSchema(input);
        var promptHash = Hash(Encoding.UTF8.GetBytes(Instruction + "\n" + serialized + "\n" + JsonSerializer.Serialize(schema, Json)));
        string? modelDigest = null;
        string? rawEnvelope = null;
        CargoAiSelection Failure(string status, string code) => new()
        {
            Status = status, ErrorCode = code, RunId = runId, Model = Model, ModelDigest = modelDigest,
            InputSha256 = inputHash, PromptSha256 = promptHash, ElapsedMilliseconds = watch.ElapsedMilliseconds,
            RawEnvelope = rawEnvelope
        };
        if (input.Candidates.Count is < 1 or > 30 || input.Candidates.Select(x => x.CandidateToken).Distinct().Count() != input.Candidates.Count
            || input.Candidates.Any(x => !Regex.IsMatch(x.CandidateToken, "^C[1-9][0-9]?$", RegexOptions.CultureInvariant)
                || x.DistanceKm is < 0 || x.WaitingScore is < 0 || x.AvailableAfterMinutes is < 0 || x.PickupEtaMinutes is < 0))
            return Failure("invalid", "InvalidCandidateInput");

        using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(2) };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(Endpoint), Timeout = Timeout.InfiniteTimeSpan };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(MaximumSeconds));
        try
        {
            using var tagsResponse = await http.GetAsync("api/tags", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!tagsResponse.IsSuccessStatusCode) return Failure("unavailable", "ModelInventoryUnavailable");
            using var tags = JsonDocument.Parse(await ReadLimitedAsync(tagsResponse.Content, deadline.Token));
            RejectDuplicateProperties(tags.RootElement);
            var model = tags.RootElement.GetProperty("models").EnumerateArray()
                .SingleOrDefault(x => x.GetProperty("name").GetString() == Model);
            if (model.ValueKind != JsonValueKind.Object) return Failure("unavailable", "AllowedModelNotInstalled");
            modelDigest = model.GetProperty("digest").GetString();
            if (modelDigest is null || !Regex.IsMatch(modelDigest, "^(sha256:)?[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant))
                return Failure("invalid", "ModelDigestInvalid");
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/generate")
            {
                Content = JsonContent.Create(new
                {
                    model = Model, system = Instruction, prompt = serialized, format = schema, stream = false, think = false,
                    options = new { temperature = 0, num_predict = 100, num_ctx = 4096 }, keep_alive = "5m"
                })
            };
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) return Failure("unavailable", "ModelGenerationUnavailable");
            var body = await ReadLimitedAsync(response.Content, deadline.Token);
            rawEnvelope = Encoding.UTF8.GetString(body);
            using var envelope = JsonDocument.Parse(body);
            RejectDuplicateProperties(envelope.RootElement);
            if (!envelope.RootElement.GetProperty("done").GetBoolean()
                || envelope.RootElement.GetProperty("model").GetString() != Model)
                return Failure("invalid", "ModelEnvelopeIncomplete");
            var raw = envelope.RootElement.GetProperty("response").GetString() ?? "";
            var checkedResult = Validate(input, raw);
            return checkedResult with
            {
                RunId = runId, Model = Model, ModelDigest = modelDigest, InputSha256 = inputHash,
                PromptSha256 = promptHash, ElapsedMilliseconds = watch.ElapsedMilliseconds, RawEnvelope = rawEnvelope
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Failure("timeout", "ModelDeadlineExceeded"); }
        catch (HttpRequestException) { return Failure("unavailable", "LocalModelUnavailable"); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException or FormatException)
        { return Failure("invalid", "InvalidModelEnvelope"); }
    }

    /// <summary>선택지를 강제하지 않고 각 후보가 실제 수치로 뒷받침하는 이유만 생성 구조에 허용한다.</summary>
    internal static object BuildSchema(CargoAiInput input)
    {
        static object Branch(string token, string[] reasons) => new
        {
            type = "object", additionalProperties = false, required = new[] { "candidateToken", "reasonCodes" },
            properties = new
            {
                candidateToken = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { token } },
                reasonCodes = new { type = "array", minItems = 1, maxItems = Math.Min(4, reasons.Length), uniqueItems = true,
                    items = new Dictionary<string, object> { ["type"] = "string", ["enum"] = reasons } }
            }
        };
        var branches = input.Candidates.Select(candidate =>
        {
            var reasons = new List<string> { "SERVER_ALLOWED" };
            if (candidate.WaitingScore is > 0 && input.Candidates.All(x => x.WaitingScore is decimal score && candidate.WaitingScore.Value >= score))
                reasons.Add("LONGER_WAIT_SCORE");
            if (candidate.DistanceKm is decimal distance && input.Candidates.All(x => x.DistanceKm is decimal other && distance <= other))
                reasons.Add("SHORTER_PICKUP_DISTANCE");
            if (candidate.AvailableAfterMinutes is > 0) reasons.Add("LAST_DESTINATION");
            if (input.Candidates.Count == 1) reasons.Add("SINGLE_ALLOWED");
            return Branch(candidate.CandidateToken, reasons.ToArray());
        }).Append(Branch("hold", ["REVIEW_NEEDED"])).ToArray();
        return new { anyOf = branches };
    }

    internal static CargoAiSelection Validate(CargoAiInput input, string raw)
    {
        CargoAiSelection Invalid(string code) => new() { Status = "invalid", ErrorCode = code };
        try
        {
            using var parsed = JsonDocument.Parse(raw);
            var obj = parsed.RootElement;
            RejectDuplicateProperties(obj);
            if (obj.ValueKind != JsonValueKind.Object || obj.EnumerateObject().Count() != 2
                || !obj.TryGetProperty("candidateToken", out var tokenValue) || tokenValue.ValueKind != JsonValueKind.String
                || !obj.TryGetProperty("reasonCodes", out var codes) || codes.ValueKind != JsonValueKind.Array)
                return Invalid("InvalidDecisionShape");
            var token = tokenValue.GetString();
            var reasons = codes.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
            if (reasons.Length is < 1 or > 4 || reasons.Distinct(StringComparer.Ordinal).Count() != reasons.Length)
                return Invalid("InvalidReasonCodes");
            var candidate = input.Candidates.SingleOrDefault(x => x.CandidateToken == token);
            if (token != "hold" && candidate is null) return Invalid("UnlistedCandidate");
            foreach (var reason in reasons)
            {
                var supported = reason switch
                {
                    "SERVER_ALLOWED" => candidate is not null,
                    "LONGER_WAIT_SCORE" => candidate?.WaitingScore is decimal score && score > 0
                        && input.Candidates.All(x => x.WaitingScore is decimal other && score >= other),
                    "SHORTER_PICKUP_DISTANCE" => candidate?.DistanceKm is decimal distance
                        && input.Candidates.All(x => x.DistanceKm is decimal other && distance <= other),
                    "LAST_DESTINATION" => candidate?.AvailableAfterMinutes is > 0,
                    "SINGLE_ALLOWED" => candidate is not null && input.Candidates.Count == 1,
                    "REVIEW_NEEDED" => token == "hold",
                    _ => false
                };
                if (!supported) return Invalid("UnsupportedReasonFact");
            }
            return new() { Status = token == "hold" ? "hold" : "valid", CandidateToken = token == "hold" ? null : token, ReasonCodes = reasons };
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException)
        { return Invalid("InvalidDecisionShape"); }
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength is > MaximumBodyBytes) throw new InvalidDataException("ModelResponseTooLarge");
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, ct);
            if (count == 0) break;
            if (output.Length + count > MaximumBodyBytes) throw new InvalidDataException("ModelResponseTooLarge");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new InvalidDataException("DuplicateJsonProperty");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed record CargoAiInput(string PickupLegalDongCode, string DropoffLegalDongCode, IReadOnlyList<CargoAiCandidate> Candidates)
{
    public bool Synthetic => true;
    public bool ServerAllowedCandidatesOnly => true;
    public bool SpatialBoundaryVerified => false;
    public bool ActualTravelTimeVerified => false;
}

internal sealed record CargoAiCandidate(string CandidateToken, decimal? DistanceKm, decimal? BaseScore,
    decimal? WaitingScore, decimal? TotalScore, decimal? AvailableAfterMinutes, decimal? PickupEtaMinutes);

internal sealed record CargoAiSelection
{
    public string Status { get; init; } = "not_run";
    public string? CandidateToken { get; init; }
    public string[] ReasonCodes { get; init; } = [];
    public string? ErrorCode { get; init; }
    public string? RunId { get; init; }
    public string? Model { get; init; }
    public string? ModelDigest { get; init; }
    public string? InputSha256 { get; init; }
    public string? PromptSha256 { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public string? RawEnvelope { get; init; }
}
