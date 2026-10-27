using System.Text.Json;

internal static class PrimaryAIProfileSelfCheck
{
    internal static int Run()
    {
        var checks = new List<object>();
        void Check(string name, bool passed)
        {
            if (!passed) throw new InvalidOperationException("R114PrimarySelfCheckFailed:" + name);
            checks.Add(new { name, passed });
        }
        foreach (var (name, uri, method, enabled, expected) in new[]
        {
            ("normal-host", "http://127.0.0.1:5364/api/v1/driver/home", HttpMethod.Get, false, true),
            ("profile-off", "http://127.0.0.1:12681/api/chat", HttpMethod.Post, false, false),
            ("model-post", "http://127.0.0.1:12681/api/chat", HttpMethod.Post, true, true),
            ("model-get", "http://127.0.0.1:12681/api/chat", HttpMethod.Get, true, false),
            ("model-pull", "http://127.0.0.1:12681/api/pull", HttpMethod.Post, true, false),
            ("model-other-port", "http://127.0.0.1:11434/api/chat", HttpMethod.Post, true, false),
            ("model-dns", "http://localhost:12681/api/chat", HttpMethod.Post, true, false),
            ("model-query", "http://127.0.0.1:12681/api/chat?q=1", HttpMethod.Post, true, false),
            ("model-user-info", "http://u@127.0.0.1:12681/api/chat", HttpMethod.Post, true, false),
            ("remote", "http://192.0.2.1:12681/api/chat", HttpMethod.Post, true, false),
            ("relative", "/api/chat", HttpMethod.Post, true, false)
        })
        {
            using var request = new HttpRequestMessage(method, new Uri(uri, UriKind.RelativeOrAbsolute));
            Check(name, CargoExternalHttpBlocker.Allowed(request, enabled) == expected);
        }
        var applied = CargoJourneyControlStartupFilter.ParsePrimaryAudit("{\"CandidateReason\":\"CargoDispatchAI.Applied\",\"FollowUpTransition\":\"RecommendationStarted\",\"TransitionResultCode\":\"추천시작됨\"}");
        Check("applied-canonical-audit", applied is { Status: "Applied", RecommendationStarted: true });
        var timeout = CargoJourneyControlStartupFilter.ParsePrimaryAudit("{\"CandidateReason\":\"CargoDispatchAI.Timeout\",\"FollowUpTransition\":\"RecommendationStarted\",\"TransitionResultCode\":\"추천시작됨\"}");
        Check("timeout-kept-distinct", timeout is { Status: "Timeout", RecommendationStarted: true });
        Check("invalid-audit", CargoJourneyControlStartupFilter.ParsePrimaryAudit("not json") is null);
        Check("array-audit", CargoJourneyControlStartupFilter.ParsePrimaryAudit("[]") is null);
        Check("duplicate-audit", CargoJourneyControlStartupFilter.ParsePrimaryAudit("{\"CandidateReason\":\"CargoDispatchAI.Applied\",\"candidateReason\":\"CargoDispatchAI.Timeout\"}") is null);
        Check("legacy-audit-not-primary", CargoJourneyControlStartupFilter.ParsePrimaryAudit("{\"CandidateReason\":\"Legacy\"}") is null);
        var noApply = CargoJourneyControlStartupFilter.ParsePrimaryAudit("{\"CandidateReason\":\"CargoDispatchAI.Applied\",\"FollowUpTransition\":\"None\",\"TransitionResultCode\":\"후보없음\"}");
        Check("model-choice-is-not-stored-recommendation", noApply is { Status: "Applied", RecommendationStarted: false });
        var noCandidate = 살뜰.Services.Dispatch.Queue.배차대기원장전환결과.전환됨("fixture", "후보없음", "");
        Check("public-transition-is-not-recommendation", !CargoJourneyControlStartupFilter.RecommendationApplied(noCandidate, 0, null, 1)
            && CargoJourneyControlStartupFilter.PrimaryPhase(noCandidate, false) == "TransitionProcessed");
        var recommended = 살뜰.Services.Dispatch.Queue.배차대기원장전환결과.전환됨("fixture", "추천시작됨", "", "driver");
        Check("recommendation-requires-matching-round-and-driver", CargoJourneyControlStartupFilter.RecommendationApplied(recommended, 1, "driver", 1));
        Check("recommendation-rejects-stale-round", !CargoJourneyControlStartupFilter.RecommendationApplied(recommended, 0, "driver", 1));
        Check("recommendation-rejects-different-driver", !CargoJourneyControlStartupFilter.RecommendationApplied(recommended, 1, "other", 1));
        var missingDriver = 살뜰.Services.Dispatch.Queue.배차대기원장전환결과.전환됨("fixture", "추천시작됨", "");
        Check("recommendation-rejects-missing-driver", !CargoJourneyControlStartupFilter.RecommendationApplied(missingDriver, 1, null, 1));
        Console.WriteLine(JsonSerializer.Serialize(new { schema = "r114-primary-profile-selfchecks.v1", passed = true, checks }));
        return 0;
    }
}
