using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;

namespace CargoRegionalTrainingReview;

/// <summary>로컬 사례 조회·사람 검토·승인 내보내기를 조율한다. 운영 배차·가중치 학습을 호출하지 않는다.</summary>
public sealed class CargoTrainingStore : ICargoRegionalTrainingReviewClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly CargoTrainingDatasetEvidence dataset;
    private readonly CargoTrainingEventLog eventLog;
    private readonly CargoTrainingModelComparison modelComparison;
    private CargoTrainingWorkspace baseline => dataset.Baseline;
    public string OutputDirectory => dataset.OutputDirectory;

    public CargoTrainingStore(string repoRoot, string outputRoot, ICargoTrainingModel? model = null, bool planReview = false)
    {
        dataset = new CargoTrainingDatasetEvidence(repoRoot, outputRoot, planReview);
        eventLog = new CargoTrainingEventLog(OutputDirectory);
        modelComparison = new CargoTrainingModelComparison(model ?? new CargoTrainingOllama());
    }

    public async Task<CargoTrainingWorkspace> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var lease = await eventLog.AcquireAsync(cancellationToken);
        dataset.VerifySources();
        return Snapshot(eventLog.Read());
    }

    public async Task<CargoTrainingWorkspace> RecordAsync(CargoTrainingReviewCommand command, CancellationToken cancellationToken = default)
    {
        await using var lease = await eventLog.AcquireAsync(cancellationToken);
        dataset.VerifySources();
        var history = eventLog.Read();
        var normalized = command with { AcceptableCandidateIds = command.AcceptableCandidateIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList() };
        var commandHash = Hash(JsonSerializer.SerializeToUtf8Bytes(normalized, Json));
        var previousRequest = history.FirstOrDefault(e => e.Kind == "review" && e.Review?.RequestId == command.RequestId);
        if (previousRequest is not null)
        {
            if (previousRequest.CommandSha256 != commandHash) throw Error("RequestConflict");
            return Snapshot(history);
        }
        if (!Guid.TryParse(command.RequestId, out _)) throw Error("InvalidRequestId");
        var workspace = Snapshot(history);
        var item = Find(workspace, command.CaseId, command.InputSha256);
        if (command.DatasetSha256 != baseline.DatasetSha256) throw Error("DatasetChanged");
        if (item.ReviewRevision != command.ExpectedRevision) throw Error("RevisionConflict");
        if (item.Ai.RunId != command.ObservedModelRunId) throw Error("ModelResultChanged");
        if (item.PlanSetSha256 != command.PlanSetSha256) throw Error("PlanEvidenceChanged");
        if (command.Outcome is not ("approve" or "correct" or "defer")) throw Error("InvalidOutcome");
        if (command.Note.Length > 1000) throw Error("NoteTooLong");
        var eligible = item.Candidates.Where(CargoTrainingPlanCases.IsSelectable).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (normalized.AcceptableCandidateIds.Any(id => !eligible.Contains(id))) throw Error("IneligibleCandidate");
        if (command.Outcome != "defer" && !normalized.AcceptHold && normalized.AcceptableCandidateIds.Count == 0) throw Error("NoDecision");
        if (command.Outcome == "defer" && (normalized.AcceptHold || normalized.AcceptableCandidateIds.Count != 0)) throw Error("DeferredHasAnswer");
        var review = new CargoTrainingReview
        {
            RequestId = command.RequestId, CaseId = item.CaseId, DatasetSha256 = baseline.DatasetSha256,
            InputSha256 = item.InputSha256, PolicySha256 = baseline.PolicySha256,
            PlanSetSha256 = item.PlanSetSha256,
            ObservedModelRunId = item.Ai.RunId, EngineCandidateId = item.Engine.CandidateId,
            Revision = item.ReviewRevision + 1, Outcome = command.Outcome,
            AcceptableCandidateIds = normalized.AcceptableCandidateIds.ToList(), AcceptHold = normalized.AcceptHold,
            Note = normalized.Note.Trim(), RecordedAtUtc = DateTimeOffset.UtcNow,
            TrainingEligible = item.Split == "review" && command.Outcome != "defer"
        };
        var ev = new CargoTrainingEvent("review", baseline.DatasetSha256, item.CaseId, item.InputSha256, commandHash, review, null);
        eventLog.Append(ev);
        history.Add(ev);
        return Snapshot(history);
    }

    public async Task<CargoTrainingWorkspace> CompareAsync(string caseId, string inputSha256, CancellationToken cancellationToken = default)
    {
        var current = await LoadAsync(cancellationToken);
        var item = Find(current, caseId, inputSha256);
        var decision = await modelComparison.CompareAsync(item, cancellationToken);
        await using var lease = await eventLog.AcquireAsync(cancellationToken);
        dataset.VerifySources();
        var history = eventLog.Read();
        Find(Snapshot(history), caseId, inputSha256);
        var ev = new CargoTrainingEvent("model", baseline.DatasetSha256, caseId, inputSha256, null, null, decision);
        eventLog.Append(ev); history.Add(ev);
        return Snapshot(history);
    }

    public async Task<CargoTrainingExport> ExportApprovedAsync(CancellationToken cancellationToken = default)
    {
        await using var lease = await eventLog.AcquireAsync(cancellationToken);
        dataset.VerifySources();
        var workspace = Snapshot(eventLog.Read());
        var approved = workspace.Cases.Where(c => c.Split == "review" && c.Review?.TrainingEligible == true
            && c.ReviewCurrent && c.Review.Outcome is "approve" or "correct").ToArray();
        var rows = approved.Select(c => JsonSerializer.Serialize(new
        {
            schema = "cargo-training-approved-example.v1", datasetSha256 = workspace.DatasetSha256,
            policySha256 = workspace.PolicySha256, caseId = c.CaseId, familyId = c.FamilyId, inputSha256 = c.InputSha256,
            input = CargoTrainingOllama.Input(c),
            source = c.Source, c.PlanSetSha256,
            acceptedPlans = c.Candidates.Where(x => c.Review!.AcceptableCandidateIds.Contains(x.Id) && x.Plan is not null)
                .Select(x => new { x.Id, x.Plan!.PlanId, x.Plan.PlanSha256, x.Plan.InputSha256 }),
            modelComparison = c.Ai, codexOpinion = c.CodexOpinion,
            humanAnswer = new { c.Review!.AcceptableCandidateIds, c.Review.AcceptHold },
            reviewRequestId = c.Review.RequestId, reviewRevision = c.Review.Revision,
            approvalSource = "explicit_local_human_review", weightsTrained = false
        }, Json));
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', rows) + (approved.Length > 0 ? "\n" : ""));
        var sha = Hash(bytes);
        var path = Path.Combine(OutputDirectory, "approved-" + sha + ".jsonl");
        CargoTrainingEventLog.WriteImmutable(path, bytes);
        var result = new CargoTrainingExport(path, sha, approved.Length, workspace.Cases.Count(c => c.Split == "holdout"), false);
        CargoTrainingEventLog.WriteImmutable(Path.ChangeExtension(path, ".manifest.json"), JsonSerializer.SerializeToUtf8Bytes(result, Json));
        return result;
    }

    private CargoTrainingWorkspace Snapshot(List<CargoTrainingEvent> history)
    {
        var result = Clone(baseline);
        foreach (var ev in history)
        {
            if (ev.DatasetSha256 != baseline.DatasetSha256) throw Error("HistoryDatasetChanged");
            var i = result.Cases.FindIndex(x => x.CaseId == ev.CaseId && x.InputSha256 == ev.InputSha256);
            if (i < 0) throw Error("HistoryInputChanged");
            var item = result.Cases[i];
            if (ev.Kind == "review" && ev.Review is not null)
            {
                if (ev.Review.Revision != item.ReviewRevision + 1) throw Error("HistoryRevisionCorrupt");
                ValidateRecordedReview(item, ev.Review);
                item = item with { Review = ev.Review };
            }
            else if (ev.Kind == "model" && ev.Decision is not null)
            {
                var decision = ev.Decision;
                CargoTrainingModelComparison.ValidateRecorded(item, decision);
                item = item with { Ai = decision };
            }
            else throw Error("HistoryCorrupt");
            result.Cases[i] = item;
        }
        return Clone(result);
    }

    private void ValidateRecordedReview(CargoTrainingCase item, CargoTrainingReview review)
    {
        if (review.CaseId != item.CaseId || review.InputSha256 != item.InputSha256
            || review.DatasetSha256 != baseline.DatasetSha256 || review.PolicySha256 != baseline.PolicySha256
            || review.ObservedModelRunId != item.Ai.RunId || review.EngineCandidateId != item.Engine.CandidateId
            || review.PlanSetSha256 != item.PlanSetSha256
            || !Guid.TryParse(review.RequestId, out _) || review.Outcome is not ("approve" or "correct" or "defer")
            || review.Actor != "local-human-reviewer" || review.Note is null || review.Note.Length > 1000
            || review.AcceptableCandidateIds is null || review.AcceptableCandidateIds.Distinct(StringComparer.Ordinal).Count() != review.AcceptableCandidateIds.Count
            || review.TrainingEligible != (item.Split == "review" && review.Outcome != "defer")) throw Error("HistoryCorrupt");
        if (review.AcceptableCandidateIds.Any(id => !item.Candidates.Any(c => c.Id == id && CargoTrainingPlanCases.IsSelectable(c)))
            || (review.Outcome == "defer" && (review.AcceptHold || review.AcceptableCandidateIds.Count != 0))
            || (review.Outcome != "defer" && !review.AcceptHold && review.AcceptableCandidateIds.Count == 0)) throw Error("HistoryCorrupt");
    }

    private static CargoTrainingCase Find(CargoTrainingWorkspace workspace, string id, string inputSha)
    {
        var item = workspace.Cases.SingleOrDefault(c => c.CaseId == id) ?? throw Error("CaseNotFound");
        if (item.InputSha256 != inputSha) throw Error("InputChanged");
        CargoTrainingPlanCases.Verify(item);
        return item;
    }

    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string FileHash(string path) => Hash(File.ReadAllBytes(path));
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, Json), Json)!;
    public static CargoTrainingReviewException Error(string code) => new(code);
}

public sealed record CargoTrainingExport(string Path, string Sha256, int ApprovedRows, int HoldoutRowsExcluded, bool WeightsTrained);
