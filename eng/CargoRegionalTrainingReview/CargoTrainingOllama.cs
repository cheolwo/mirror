using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;

namespace CargoRegionalTrainingReview;

public interface ICargoTrainingModel
{
    Task<CargoTrainingDecision> CompareAsync(CargoTrainingCase item, CancellationToken cancellationToken = default);
}

/// <summary>기존 로컬 qwen 모델의 비교 추론만 허용한다. 모델 설치·학습·운영 배차 도구가 없다.</summary>
public sealed class CargoTrainingOllama : ICargoTrainingModel
{
    public const string AllowedModel = "qwen3:1.7b";
    public const string LoopbackEndpoint = "http://127.0.0.1:12681/";
    public const string Instruction = "You compare SYNTHETIC cargo dispatch candidates. Never invent facts or candidates. Every time, weight, vehicle and capacity is a synthetic fixture input, not observed travel or an operational snapshot. Only feasible candidates are supplied. A plan candidate identifies ONE entire driver-and-ordered-stops plan, never just an independently feasible order. Respect its typed pickup/dropoff order, loading segments, explicit handling times and plan/input hashes. Missing handling time is not zero. Region membership does not prove travel time or adjacency. Prefer longer-idle feasible choices only when existing commitments and deadlines are safe. Select one supplied candidate ID or hold. Return only candidateId and reasonCodes JSON supported by the supplied facts. No policy edits, fare changes or operational actions.";
    private static readonly string[] ReasonCodes = ["LONG_WAIT", "MEETS_DEADLINE", "CAPACITY_OK", "RETURN_DIRECTION", "LAST_DESTINATION", "NEAR_PICKUP", "REGION_MATCH", "NO_ELIGIBLE", "MISSING_DATA", "REVIEW_NEEDED", "PLAN_VERIFIED", "TIMING_VERIFIED", "LOAD_VERIFIED"];

    public static object Input(CargoTrainingCase c) => new
    {
        c.CaseId, c.TaskType, c.PickupRegion, c.DropoffRegion, c.WeightKg, c.VolumeLiters,
        c.PickupDeadlineMinutes, c.Fare,
        c.InputSha256, c.PlanSetSha256,
        candidates = c.Candidates.Where(CargoTrainingPlanCases.IsSelectable).Select(x => new
        {
            x.Id, x.Vehicle, x.CurrentRegion, x.AvailableRegion, x.AvailableAfterMinutes,
            x.TravelAfterAvailableMinutes, x.PickupEtaMinutes, x.IdleWaitMinutes,
            x.CapacityKg, x.CapacityLiters, x.LocationAgeMinutes, x.Receiving,
            x.CommitmentsFeasible, x.ReturnDirection, x.Eligible, x.Eligibility, x.Plan
        }),
        excludedCandidateCount = c.Candidates.Count(x => !CargoTrainingPlanCases.IsSelectable(x)),
        missingData = c.Candidates.Any(x => x.TravelAfterAvailableMinutes is null || x.Plan is { Verified: false }),
        valueOrigin = "synthetic_fixture",
        spatialAdjacencyVerified = false, actualTravelTimeVerified = false
    };

    public async Task<CargoTrainingDecision> CompareAsync(CargoTrainingCase item, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        var runId = Guid.NewGuid().ToString("N");
        string? modelDigest = null;
        var prompt = JsonSerializer.Serialize(Input(item), CargoTrainingStore.Json);
        var promptSha = CargoTrainingStore.Hash(System.Text.Encoding.UTF8.GetBytes(Instruction + "\n" + prompt));
        CargoTrainingDecision Failure(string code, string status = "unavailable") => new()
        {
            Status = status, Source = "ollama_shadow", RunId = runId, Model = AllowedModel, ModelDigest = modelDigest,
            InputSha256 = item.InputSha256, PromptSha256 = promptSha, ErrorCode = code, ElapsedMilliseconds = timer.ElapsedMilliseconds
        };
        using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(2) };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(LoopbackEndpoint), Timeout = Timeout.InfiniteTimeSpan };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var tagResponse = await http.GetAsync("api/tags", timeout.Token);
            tagResponse.EnsureSuccessStatusCode();
            using var tags = JsonDocument.Parse(await tagResponse.Content.ReadAsStringAsync(timeout.Token));
            foreach (var m in tags.RootElement.GetProperty("models").EnumerateArray())
                if (m.GetProperty("name").GetString() == AllowedModel) modelDigest = m.GetProperty("digest").GetString();
            if (string.IsNullOrWhiteSpace(modelDigest)) return Failure("AllowedModelNotInstalled");
            var ids = item.Candidates.Where(CargoTrainingPlanCases.IsSelectable).Select(c => c.Id).Append("hold").ToArray();
            var schema = new
            {
                type = "object", properties = new
                {
                    candidateId = new Dictionary<string, object> { ["type"] = "string", ["enum"] = ids },
                    reasonCodes = new { type = "array", items = new Dictionary<string, object> { ["type"] = "string", ["enum"] = ReasonCodes }, minItems = 1, maxItems = 4 }
                }, required = new[] { "candidateId", "reasonCodes" }, additionalProperties = false
            };
            using var response = await http.PostAsJsonAsync("api/generate", new
            {
                model = AllowedModel, system = Instruction, prompt, stream = false, think = false, format = schema,
                options = new { temperature = 0, num_predict = 160, num_ctx = 4096 }, keep_alive = "5m"
            }, timeout.Token);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            if (bytes.Length > 64 * 1024) return Failure("ModelResponseTooLarge", "invalid");
            using var body = JsonDocument.Parse(bytes);
            if (!body.RootElement.TryGetProperty("done", out var done) || !done.GetBoolean()) return Failure("ModelResponseIncomplete", "invalid");
            var raw = body.RootElement.GetProperty("response").GetString() ?? "";
            var result = Validate(item, raw);
            return result with { Source = "ollama_shadow", RunId = runId, Model = AllowedModel, ModelDigest = modelDigest,
                InputSha256 = item.InputSha256, PromptSha256 = promptSha, ElapsedMilliseconds = timer.ElapsedMilliseconds };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Failure("ModelTimeout", "timeout"); }
        catch (HttpRequestException) { return Failure("LocalModelUnavailable"); }
        catch (JsonException) { return Failure("InvalidModelEnvelope", "invalid"); }
        catch (KeyNotFoundException) { return Failure("InvalidModelEnvelope", "invalid"); }
        catch (InvalidOperationException) { return Failure("InvalidModelEnvelope", "invalid"); }
    }

    public static CargoTrainingDecision Validate(CargoTrainingCase item, string raw)
    {
        var responseSha = CargoTrainingStore.Hash(System.Text.Encoding.UTF8.GetBytes(raw));
        CargoTrainingDecision Invalid(string code, bool shape = false) => new()
        {
            Status = "invalid", Source = "ollama_shadow", ErrorCode = code, RawResponse = raw,
            ResponseSha256 = responseSha, StructureValid = shape, FactsValid = false
        };
        try
        {
            if (System.Text.Encoding.UTF8.GetByteCount(raw) > 64 * 1024) return Invalid("ModelResponseTooLarge");
            CargoTrainingPlanCases.Verify(item);
            using var parsed = JsonDocument.Parse(raw);
            var obj = parsed.RootElement;
            if (obj.ValueKind != JsonValueKind.Object || obj.EnumerateObject().Count() != 2) return Invalid("InvalidDecisionShape");
            var id = obj.GetProperty("candidateId").GetString();
            var reasons = obj.GetProperty("reasonCodes").EnumerateArray().Select(r => r.GetString() ?? "").ToList();
            if (reasons.Count is < 1 or > 4 || reasons.Distinct(StringComparer.Ordinal).Count() != reasons.Count
                || reasons.Any(r => !ReasonCodes.Contains(r, StringComparer.Ordinal))) return Invalid("InvalidReasonCode", true);
            var eligible = item.Candidates.Where(CargoTrainingPlanCases.IsSelectable).ToArray();
            var selected = eligible.SingleOrDefault(c => c.Id == id);
            if (id != "hold" && selected is null) return Invalid("IneligibleModelCandidate", true);
            foreach (var reason in reasons)
            {
                var supported = reason switch
                {
                    "LONG_WAIT" => selected is not null && selected.IdleWaitMinutes == eligible.Max(c => c.IdleWaitMinutes),
                    "MEETS_DEADLINE" => selected?.PickupEtaMinutes is decimal eta && eta <= item.PickupDeadlineMinutes,
                    "CAPACITY_OK" => selected is not null && selected.CapacityKg >= item.WeightKg && selected.CapacityLiters >= item.VolumeLiters,
                    "RETURN_DIRECTION" => selected?.ReturnDirection == true,
                    "LAST_DESTINATION" => selected?.AvailableAfterMinutes > 0,
                    "NEAR_PICKUP" => selected is not null && selected.PickupEtaMinutes == eligible.Min(c => c.PickupEtaMinutes),
                    "REGION_MATCH" => selected?.AvailableRegion.LegalDongCode == item.PickupRegion.LegalDongCode,
                    "NO_ELIGIBLE" => id == "hold" && eligible.Length == 0,
                    "MISSING_DATA" => item.Candidates.Any(c => c.TravelAfterAvailableMinutes is null || c.Plan is { Verified: false }),
                    "PLAN_VERIFIED" => selected?.Plan is { Verified: true, Feasible: true },
                    "TIMING_VERIFIED" => selected?.Plan?.TimingEvidence == "synthetic_explicit_schedule_checked",
                    "LOAD_VERIFIED" => selected?.Plan?.LoadingEvidence == "shared_segment_service_checked",
                    "REVIEW_NEEDED" => true,
                    _ => false
                };
                if (!supported) return Invalid("UnsupportedReasonFact", true);
            }
            return new()
            {
                Status = "valid", Source = "ollama_shadow", CandidateId = id == "hold" ? null : id, ReasonCodes = reasons,
                PlanSha256 = selected?.Plan?.PlanSha256, RawResponse = raw, ResponseSha256 = responseSha,
                StructureValid = true, FactsValid = true
            };
        }
        catch (CargoTrainingReviewException) { return Invalid("PlanEvidenceChanged"); }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return Invalid("InvalidDecisionShape"); }
    }
}
