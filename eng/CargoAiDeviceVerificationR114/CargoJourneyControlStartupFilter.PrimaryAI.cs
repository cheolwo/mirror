using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

internal sealed partial class CargoJourneyControlStartupFilter
{
    // 구형 모델 시도 원장과 별도 보관한다. 과거 해시 결속과 완료/거절 상태는 바꾸지 않는다.
    private string PrimaryJournalPath(string id) => JournalPath(id) + ".primary.json";

    private async Task RecommendPrimaryAsync(HttpContext context, IServiceProvider services, SsalddelContext db,
        string requestId, string idempotencyKey, Dictionary<string, string> regions)
    {
        var ct = context.RequestAborted;
        var path = PrimaryJournalPath(requestId);
        await using var lease = await AcquireAsync(path + ".lock", ct);
        var commandHash = Hash(new { requestId, requestIdempotencyKey = idempotencyKey });
        var current = await ReadCaseAsync(db, requestId, regions, ct);
        var existing = ReadPrimary(path);
        if (existing is not null)
        {
            if (existing.RequestId != requestId || existing.IdempotencyKey != idempotencyKey || existing.CommandSha256 != commandHash)
                throw Fail("IdempotencyPayloadConflict");
            var replay = await ProofAsync(services, db, current.Request, current.Queue, current.Tags, null, ct);
            WriteEvidence("primary-replays", requestId, replay);
            await context.Response.WriteAsJsonAsync(new { replay = true, result = existing, proof = replay }, ct);
            return; // 결과 유실 때에도 새 추론/추천/라운드를 만들지 않는다.
        }
        if (current.Queue.추천기록존재() || current.Queue.추천라운드 != 0) throw Fail("PreexistingRecommendationNotAiProof");
        var review = await services.GetRequiredService<용달운송배차업무정책>().후보검토Async(current.Queue, cancellationToken: ct);
        WriteCandidateObservation(requestId, review);
        if (review.후보목록.Any(x => x.판정Code == "CandidateAllowed" && !AllowedDrivers.Contains(x.기사Id, StringComparer.Ordinal)))
            throw Fail("ForeignCandidateInTestScope");
        var state = new PrimaryAttempt
        {
            RequestId = requestId, IdempotencyKey = idempotencyKey, CommandSha256 = commandHash,
            StartedAtUtc = DateTime.UtcNow, Phase = "ApplyStarted", SourceSha256 = SourceFingerprint(),
            ExpectedRecommendationRound = current.Queue.추천라운드 + 1
        };
        SavePrimary(path, state); // mutation 전에 명령을 보존한다.
        try
        {
            var service = services.GetRequiredService<I배차대기원장전환Service>();
            var result = current.Queue.배차큐단계 == 상태값.배차큐단계.계획배차
                ? await service.계획배차에서추천으로전환Async(requestId, ct)
                : await service.추천대기처리Async(requestId, ct);
            state.TransitionCode = result.결과코드;
            state.SelectedDriverId = result.기사Id;
            var canonical = await ReadCaseAsync(db, requestId, regions, ct);
            state.Applied = RecommendationApplied(result, canonical.Queue.추천라운드,
                canonical.Queue.현재추천대상기사Id, state.ExpectedRecommendationRound);
            state.Phase = PrimaryPhase(result, state.Applied);
            SavePrimary(path, state);
            var proof = await ProofAsync(services, db, canonical.Request, canonical.Queue, canonical.Tags, null, ct);
            WriteEvidence("primary-proofs", requestId, proof);
            context.Response.StatusCode = state.Applied ? 200 : 409;
            await context.Response.WriteAsJsonAsync(new { replay = false, result = state, proof }, ct);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (state.Phase == "ApplyStarted")
            {
                state.Phase = "ApplyResultUnknown";
                SavePrimary(path, state);
            }
            throw;
        }
    }

    internal static bool RecommendationApplied(배차대기원장전환결과 result, int round, string? driver, int expectedRound)
        => result.전환여부 && result.결과코드 == 배차대기원장전환결과코드.추천시작됨
            && !string.IsNullOrWhiteSpace(result.기사Id) && result.기사Id == driver && expectedRound > 0 && round == expectedRound;

    internal static string PrimaryPhase(배차대기원장전환결과 result, bool recommendationProven)
        => recommendationProven ? "Applied" : result.전환여부 ? "TransitionProcessed" : "TransitionRejected";

    private object WriteCandidateObservation(string requestId, 용달운송후보검토결과 review)
    {
        var observed = new
        {
            atUtc = DateTime.UtcNow, requestId, synthetic = true, observationOnly = true,
            modelCalledByObservation = false, recommendationAppliedByObservation = false,
            stopCode = review.중단사유Code,
            selectedRuleDriverId = AllowedDrivers.Contains(review.선정후보?.DriverId, StringComparer.Ordinal) ? review.선정후보?.DriverId : null,
            candidates = review.후보목록.Where(x => AllowedDrivers.Contains(x.기사Id, StringComparer.Ordinal)).Select(x => new
            {
                driverId = x.기사Id, code = x.판정Code, distanceKm = x.거리Km, agingScore = x.대기점수,
                score = x.추천점수, pickupArrivalUtc = x.픽업도착예상시각Utc
            }).ToArray(),
            outsideAllowedDriverCount = review.후보목록.Count(x => !AllowedDrivers.Contains(x.기사Id, StringComparer.Ordinal))
        };
        WriteEvidence("primary-candidate-observations", requestId, observed);
        return observed;
    }

    private async Task<PrimaryCanonicalProof> PrimaryProofAsync(SsalddelContext db, string requestId, 운송원장 queue, CancellationToken ct)
    {
        var attempt = ReadPrimary(PrimaryJournalPath(requestId));
        var events = await db.운송이벤트.AsNoTracking()
            .Where(x => x.의뢰Id == requestId && x.이벤트타입 == 운송이벤트유형.배차엔진판단감사)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.이벤트시각, x.메타데이터 }).ToArrayAsync(ct);
        foreach (var item in events.Reverse())
        {
            if (attempt is null || item.이벤트시각 < attempt.StartedAtUtc.AddSeconds(-1)) continue;
            var audit = ParsePrimaryAudit(item.메타데이터);
            if (audit is null) continue;
            var canonicalMatches = queue.추천라운드 == attempt.ExpectedRecommendationRound
                && queue.추천라운드 > 0 && (queue.현재추천대상기사Id is not null || queue.확정기사Id is not null || queue.기사_운송자 is not null)
                && (attempt.SelectedDriverId is null || attempt.SelectedDriverId == queue.현재추천대상기사Id
                    || attempt.SelectedDriverId == queue.확정기사Id || attempt.SelectedDriverId == queue.기사_운송자);
            var recommendation = audit.RecommendationStarted && canonicalMatches;
            return new(audit.Status, recommendation, recommendation && audit.Status != "Applied", item.Id,
                CargoAiSelector.Hash(Encoding.UTF8.GetBytes(item.메타데이터)), attempt.Phase, canonicalMatches,
                recommendation && audit.Status == "Applied");
        }
        return new("NotRecorded", false, false, null, null, attempt?.Phase, false, false);
    }

    internal static PrimaryAudit? ParsePrimaryAudit(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return null;
            string Read(string name) => root.EnumerateObject().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                .Value is { ValueKind: JsonValueKind.String } text ? text.GetString() ?? "" : "";
            var match = Regex.Match(Read("CandidateReason"), @"\bCargoDispatchAI\.([A-Za-z]+)\b", RegexOptions.CultureInvariant);
            return match.Success ? new(match.Groups[1].Value,
                Read("FollowUpTransition") == "RecommendationStarted" && Read("TransitionResultCode") == 배차대기원장전환결과코드.추천시작됨) : null;
        }
        catch (JsonException) { return null; }
    }

    private static PrimaryAttempt? ReadPrimary(string path)
    {
        if (!File.Exists(path)) return null;
        var attempt = JsonSerializer.Deserialize<PrimaryAttempt>(File.ReadAllBytes(path), Json) ?? throw Fail("PrimaryAttemptJournalInvalid");
        if (attempt.Schema != "r114-primary-cargo-attempt.v1" || attempt.Sequence <= 0 || string.IsNullOrWhiteSpace(attempt.LastEventSha256))
            throw Fail("PrimaryAttemptJournalInvalid");
        var eventPath = Path.Combine(path + ".events", $"{attempt.Sequence:D4}-{attempt.LastEventSha256}.json");
        if (!File.Exists(eventPath) || CargoAiSelector.Hash(File.ReadAllBytes(eventPath)) != attempt.LastEventSha256)
            throw Fail("PrimaryAttemptEventMissing");
        using var snapshot = JsonDocument.Parse(File.ReadAllBytes(eventPath));
        var digest = attempt.LastEventSha256;
        attempt.LastEventSha256 = snapshot.RootElement.GetProperty("previousEventSha256").GetString();
        if (Hash(attempt) != Hash(snapshot.RootElement.GetProperty("attempt"))) throw Fail("PrimaryAttemptStateMismatch");
        attempt.LastEventSha256 = digest;
        return attempt;
    }

    private static void SavePrimary(string path, PrimaryAttempt attempt)
    {
        attempt.Sequence++;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { previousEventSha256 = attempt.LastEventSha256, attempt }, Json);
        var digest = CargoAiSelector.Hash(bytes);
        Directory.CreateDirectory(path + ".events");
        DurableWrite(Path.Combine(path + ".events", $"{attempt.Sequence:D4}-{digest}.json"), bytes, false);
        attempt.LastEventSha256 = digest;
        DurableWrite(path, JsonSerializer.SerializeToUtf8Bytes(attempt, Json), true);
    }
}

internal sealed class PrimaryAttempt
{
    public string Schema { get; init; } = "r114-primary-cargo-attempt.v1";
    public required string RequestId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string CommandSha256 { get; init; }
    public required string SourceSha256 { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public int ExpectedRecommendationRound { get; init; }
    public string? SelectedDriverId { get; set; }
    public string Phase { get; set; } = "ApplyStarted";
    public string? TransitionCode { get; set; }
    public bool Applied { get; set; }
    public int Sequence { get; set; }
    public string? LastEventSha256 { get; set; }
}

internal sealed record PrimaryAudit(string Status, bool RecommendationStarted);
internal sealed record PrimaryCanonicalProof(string Status, bool RecommendationApplied, bool FallbackUsed,
    long? AuditEventId, string? AuditPayloadSha256, string? Phase, bool CanonicalMatches,
    bool AiSelectionApplied);
