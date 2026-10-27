using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;
using static CargoRegionalTrainingReview.CargoTrainingStore;

namespace CargoRegionalTrainingReview;

/// <summary>모델 비교 실행 제한과 새 결과·저장된 결과의 근거 검증만 담당한다.</summary>
internal sealed class CargoTrainingModelComparison(ICargoTrainingModel model)
{
    private readonly SemaphoreSlim modelGate = new(1, 1);

    public async Task<CargoTrainingDecision> CompareAsync(CargoTrainingCase item, CancellationToken cancellationToken)
    {
        if (item.PlanSetSha256 is not null && !item.Candidates.Any(CargoTrainingPlanCases.IsSelectable))
            throw Error("NoFeasiblePlan");
        // This port can only return an advisory. It cannot write operational ledgers.
        if (!await modelGate.WaitAsync(0, cancellationToken)) throw Error("ReviewBusy");
        CargoTrainingDecision decision;
        try
        {
            decision = await model.CompareAsync(Clone(item), cancellationToken).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        catch (TimeoutException)
        {
            decision = new() { Status = "timeout", Source = "ollama_shadow", ErrorCode = "ModelDeadlineExceeded", RunId = Guid.NewGuid().ToString("N"), InputSha256 = item.InputSha256 };
        }
        finally { modelGate.Release(); }
        return Normalize(item, decision);
    }

    private static CargoTrainingDecision Normalize(CargoTrainingCase item, CargoTrainingDecision decision)
    {
        if (decision.Status == "not_run") throw Error("ModelResultMissing");
        if (decision.Status == "valid")
        {
            var checkedResult = CargoTrainingOllama.Validate(item, JsonSerializer.Serialize(new { candidateId = decision.CandidateId ?? "hold", reasonCodes = decision.ReasonCodes }, Json));
            if (decision.InputSha256 != item.InputSha256 || !Guid.TryParse(decision.RunId, out _))
                checkedResult = new() { Status = "invalid", ErrorCode = "ModelInputOrRunMismatch" };
            if (decision.ElapsedMilliseconds > 20_000)
                checkedResult = new() { Status = "timeout", ErrorCode = "ModelDeadlineExceeded" };
            if (item.PlanSetSha256 is not null && decision.PlanSha256 != checkedResult.PlanSha256)
                checkedResult = new() { Status = "invalid", ErrorCode = "ModelPlanMismatch" };
            if (item.PlanSetSha256 is not null && (decision.RawResponse is null
                || decision.ResponseSha256 != Hash(Encoding.UTF8.GetBytes(decision.RawResponse))
                || decision.RawResponse.Length > 64 * 1024))
                checkedResult = new() { Status = "invalid", ErrorCode = "ModelRawEvidenceMissing" };
            if (decision.RawResponse is not null)
            {
                var rawCheck = CargoTrainingOllama.Validate(item, decision.RawResponse);
                if (rawCheck.Status != "valid" || rawCheck.CandidateId != checkedResult.CandidateId
                    || !rawCheck.ReasonCodes.SequenceEqual(checkedResult.ReasonCodes))
                    checkedResult = new() { Status = "invalid", ErrorCode = "ModelRawEvidenceMismatch" };
            }
            decision = decision with { Status = checkedResult.Status, CandidateId = checkedResult.CandidateId,
                ReasonCodes = checkedResult.ReasonCodes, ErrorCode = checkedResult.ErrorCode,
                PlanSha256 = checkedResult.PlanSha256, StructureValid = checkedResult.StructureValid, FactsValid = checkedResult.FactsValid };
        }
        if (decision.Status is not ("valid" or "invalid" or "timeout" or "unavailable"))
            decision = decision with { Status = "invalid", CandidateId = null, ReasonCodes = [], ErrorCode = "UnknownModelStatus" };
        if (decision.Status != "valid") decision = decision with { CandidateId = null, ReasonCodes = [], PlanSha256 = null, FactsValid = false };
        if (decision.RawResponse is { } rawResponse)
        {
            var size = Encoding.UTF8.GetByteCount(rawResponse);
            decision = decision with { ResponseSha256 = Hash(Encoding.UTF8.GetBytes(rawResponse)) };
            if (size > 64 * 1024) decision = decision with
            {
                Status = "invalid", CandidateId = null, ReasonCodes = [], PlanSha256 = null, RawResponse = null,
                StructureValid = false, FactsValid = false, ErrorCode = "ModelResponseTooLarge"
            };
        }
        return decision with { RunId = Guid.TryParse(decision.RunId, out _) ? decision.RunId : Guid.NewGuid().ToString("N"), InputSha256 = item.InputSha256 };
    }

    public static void ValidateRecorded(CargoTrainingCase item, CargoTrainingDecision decision)
    {
        if (decision.InputSha256 != item.InputSha256 || !Guid.TryParse(decision.RunId, out _)
            || decision.Status is not ("valid" or "invalid" or "timeout" or "unavailable")) throw Error("HistoryCorrupt");
        if (decision.RawResponse is { } raw && (Encoding.UTF8.GetByteCount(raw) > 64 * 1024
            || decision.ResponseSha256 != Hash(Encoding.UTF8.GetBytes(raw)))) throw Error("HistoryCorrupt");
        if (decision.Status != "valid" && decision.FactsValid == true) throw Error("HistoryCorrupt");
        if (decision.Status == "valid")
        {
            var check = CargoTrainingOllama.Validate(item, JsonSerializer.Serialize(new { candidateId = decision.CandidateId ?? "hold", reasonCodes = decision.ReasonCodes }, Json));
            if (check.Status != "valid" || decision.ElapsedMilliseconds > 20_000) throw Error("HistoryCorrupt");
            if (item.PlanSetSha256 is not null && (decision.RawResponse is null
                || decision.ResponseSha256 != Hash(Encoding.UTF8.GetBytes(decision.RawResponse))
                || decision.PlanSha256 != check.PlanSha256)) throw Error("HistoryCorrupt");
            if (decision.RawResponse is not null)
            {
                var rawCheck = CargoTrainingOllama.Validate(item, decision.RawResponse);
                if (rawCheck.Status != "valid" || rawCheck.CandidateId != check.CandidateId
                    || !rawCheck.ReasonCodes.SequenceEqual(check.ReasonCodes)) throw Error("HistoryCorrupt");
            }
        }
        else if (decision.CandidateId is not null || decision.ReasonCodes.Count != 0) throw Error("HistoryCorrupt");
    }
}
