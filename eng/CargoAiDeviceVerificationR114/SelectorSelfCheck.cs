/// <summary>모델·서버·DB·파일을 실행하거나 바꾸지 않는 응답 계약과 적용 근거의 순수 반례 검사다.</summary>
public static class SelectorSelfCheck
{
    public static int Run()
    {
        var results = new List<object>();
        var pair = new CargoAiInput("1126010100", "1126010200",
        [
            new("C1", 1m, 80m, 6m, 86m, 0m, 4m),
            new("C2", 2m, 75m, 3m, 78m, 5m, 10m)
        ]);
        var single = pair with { Candidates = [pair.Candidates[0]] };

        void Decision(string name, CargoAiInput input, string raw, string expectedStatus, string? expectedToken = null)
        {
            var actual = CargoAiSelector.Validate(input, raw);
            Check(name, actual.Status == expectedStatus && actual.CandidateToken == expectedToken);
        }
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("R114SelectorSelfCheckFailed:" + name);
            results.Add(new { name, passed = true });
        }

        Decision("valid-selected-with-supported-facts", pair,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[\"SERVER_ALLOWED\",\"LONGER_WAIT_SCORE\",\"SHORTER_PICKUP_DISTANCE\"]}", "valid", "C1");
        Decision("valid-busy-final-destination", pair,
            "{\"candidateToken\":\"C2\",\"reasonCodes\":[\"SERVER_ALLOWED\",\"LAST_DESTINATION\"]}", "valid", "C2");
        Decision("valid-single-allowed", single,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[\"SINGLE_ALLOWED\"]}", "valid", "C1");
        Decision("valid-explicit-hold", pair,
            "{\"candidateToken\":\"hold\",\"reasonCodes\":[\"REVIEW_NEEDED\"]}", "hold");
        Decision("reject-unlisted-candidate", pair,
            "{\"candidateToken\":\"C3\",\"reasonCodes\":[\"SERVER_ALLOWED\"]}", "invalid");
        Decision("reject-duplicate-json-property", pair,
            "{\"candidateToken\":\"C1\",\"candidateToken\":\"C2\",\"reasonCodes\":[\"SERVER_ALLOWED\"]}", "invalid");
        Decision("reject-additional-property", pair,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[\"SERVER_ALLOWED\"],\"dispatch\":true}", "invalid");
        Decision("reject-unknown-reason", pair,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[\"CHANGE_FARE\"]}", "invalid");
        Decision("reject-empty-reasons", pair,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[]}", "invalid");
        Decision("reject-duplicate-reason", pair,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[\"SERVER_ALLOWED\",\"SERVER_ALLOWED\"]}", "invalid");
        Decision("reject-nonstring-token", pair,
            "{\"candidateToken\":1,\"reasonCodes\":[\"SERVER_ALLOWED\"]}", "invalid");
        Decision("reject-nonstring-reason", pair,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[1]}", "invalid");
        Decision("reject-unstructured-array", pair, "[\"C1\"]", "invalid");
        Decision("reject-malformed-json", pair, "{not-json}", "invalid");
        Decision("reject-shorter-waiter-long-wait-reason", pair,
            "{\"candidateToken\":\"C2\",\"reasonCodes\":[\"LONGER_WAIT_SCORE\"]}", "invalid");
        Decision("reject-farther-distance-short-distance-reason", pair,
            "{\"candidateToken\":\"C2\",\"reasonCodes\":[\"SHORTER_PICKUP_DISTANCE\"]}", "invalid");
        Decision("reject-idle-final-destination-reason", pair,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[\"LAST_DESTINATION\"]}", "invalid");
        Decision("reject-multiple-as-single", pair,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[\"SINGLE_ALLOWED\"]}", "invalid");
        Decision("reject-hold-as-server-allowed", pair,
            "{\"candidateToken\":\"hold\",\"reasonCodes\":[\"SERVER_ALLOWED\"]}", "invalid");
        Decision("reject-selection-as-hold-reason", pair,
            "{\"candidateToken\":\"C1\",\"reasonCodes\":[\"REVIEW_NEEDED\"]}", "invalid");
        var unknownDistance = pair with { Candidates = [pair.Candidates[0] with { DistanceKm = null }, pair.Candidates[1]] };
        Decision("reject-short-distance-when-comparison-missing", unknownDistance,
            "{\"candidateToken\":\"C2\",\"reasonCodes\":[\"SHORTER_PICKUP_DISTANCE\"]}", "invalid");
        var unknownWait = pair with { Candidates = [pair.Candidates[0] with { WaitingScore = null }, pair.Candidates[1]] };
        Decision("reject-long-wait-when-comparison-missing", unknownWait,
            "{\"candidateToken\":\"C2\",\"reasonCodes\":[\"LONGER_WAIT_SCORE\"]}", "invalid");

        R114Journal Attempt(string phase, bool applied = false) => new()
        {
            Phase = phase, Applied = applied, TransitionCode = applied ? "추천시작됨" : null,
            Selection = new() { Status = "valid", CandidateToken = "C1" }, SelectedDriverId = "cargo-journey-driver",
            ExpectedRecommendationRound = 1, TokenDriverMap = new() { ["C1"] = "cargo-journey-driver" }
        };
        Check("proof-requires-attempt-not-just-model-choice", !CargoJourneyControlStartupFilter.ApplicationAttemptRecorded(Attempt("ModelReturned")));
        Check("proof-rejects-not-applied", !CargoJourneyControlStartupFilter.ApplicationAttemptRecorded(Attempt("NotApplied")));
        Check("proof-rejects-transition-rejection", !CargoJourneyControlStartupFilter.ApplicationAttemptRecorded(Attempt("TransitionRejected")));
        Check("proof-rejects-applied-label-without-confirmed-response", !CargoJourneyControlStartupFilter.ApplicationAttemptRecorded(Attempt("Applied")));
        Check("proof-accepts-confirmed-application-phase", CargoJourneyControlStartupFilter.ApplicationAttemptRecorded(Attempt("Applied", true)));
        Check("proof-can-correlate-uncertain-apply-attempt", CargoJourneyControlStartupFilter.ApplicationAttemptRecorded(Attempt("ApplyResultUnknown")));
        Check("proof-requires-token-driver-binding", !CargoJourneyControlStartupFilter.ApplicationAttemptRecorded(new()
        {
            Phase = "ApplyStarted", Selection = new() { Status = "valid", CandidateToken = "C1" },
            SelectedDriverId = "cargo-journey-driver-b", ExpectedRecommendationRound = 1,
            TokenDriverMap = new() { ["C1"] = "cargo-journey-driver" }
        }));
        Check("proof-rejects-invalid-model-even-at-apply-phase", !CargoJourneyControlStartupFilter.ApplicationAttemptRecorded(new()
        {
            Phase = "ApplyStarted", Selection = new() { Status = "invalid", CandidateToken = "C1" },
            SelectedDriverId = "cargo-journey-driver", ExpectedRecommendationRound = 1,
            TokenDriverMap = new() { ["C1"] = "cargo-journey-driver" }
        }));
        Check("proof-rejects-no-attempt", !CargoJourneyControlStartupFilter.ApplicationAttemptRecorded(null));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = "r114-selector-pure-self-check.v1", passed = true, checks = results.Count, results,
            modelCalls = 0, fileMutations = 0, databaseMutations = 0, weightsTrained = false, deviceVerified = false }));
        return 0;
    }
}
