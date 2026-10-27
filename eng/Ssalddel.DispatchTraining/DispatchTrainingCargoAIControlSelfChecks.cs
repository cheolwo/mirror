namespace Ssalddel.DispatchTraining;

/// <summary>명시한 화물 AI 검증 프로필의 HTTP 허용 범위만 검사한다. 실제 요청·서버를 시작하지 않는다.</summary>
internal static class DispatchTrainingCargoAIControlSelfChecks
{
    internal static IReadOnlyList<DispatchTrainingSelfCheckResult> Run()
    {
        var checks = new List<DispatchTrainingSelfCheckResult>();
        foreach (var (name, uri, method, enabled, expected) in new[]
        {
            ("normal-host-read", "http://127.0.0.1:5324/api/v1/current", HttpMethod.Get, false, true),
            ("model-profile-off", "http://127.0.0.1:12681/api/chat", HttpMethod.Post, false, false),
            ("model-profile-on", "http://127.0.0.1:12681/api/chat", HttpMethod.Post, true, true),
            ("model-get", "http://127.0.0.1:12681/api/chat", HttpMethod.Get, true, false),
            ("model-other-api", "http://127.0.0.1:12681/api/pull", HttpMethod.Post, true, false),
            ("model-query", "http://127.0.0.1:12681/api/chat?copy=true", HttpMethod.Post, true, false),
            ("model-other-port", "http://127.0.0.1:11434/api/chat", HttpMethod.Post, true, false),
            ("model-dns", "http://localhost:12681/api/chat", HttpMethod.Post, true, false),
            ("model-other-host", "http://192.0.2.1:12681/api/chat", HttpMethod.Post, true, false),
            ("model-user-info", "http://user@127.0.0.1:12681/api/chat", HttpMethod.Post, true, false),
            ("relative", "/api/chat", HttpMethod.Post, true, false)
        })
        {
            using var request = new HttpRequestMessage(method, new Uri(uri, UriKind.RelativeOrAbsolute));
            var actual = DispatchTrainingHttpGuard.Allowed(request, enabled);
            checks.Add(new("cargo-ai-http:" + name, actual == expected, actual == expected ? null : "UnexpectedHttpBoundary"));
        }
        return checks;
    }
}
