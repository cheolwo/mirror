using System.Reflection;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.LocalDispatchScenarios;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length is not (0 or 2) || args.Length == 2 && args[0] != "--output")
                throw new ArgumentException("--output <artifacts/local 아래 폴더>만 사용할 수 있습니다. 모델 실행 옵션은 없습니다.");
            var root = RepoRoot();
            var output = Path.GetFullPath(args.Length == 0
                ? Path.Combine(root, "artifacts/local/local-dispatch-scenarios-r45/campaign") : args[1], root);
            var allowed = Path.GetFullPath(Path.Combine(root, "artifacts/local")) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("결과는 저장소 artifacts/local 아래에만 저장합니다.");
            var source = Path.Combine(AppContext.BaseDirectory, "scenarios.json");
            var raw = File.ReadAllBytes(source);
            var spec = JsonSerializer.Deserialize<CampaignSpec>(raw, ScenarioCampaign.Json)
                ?? throw new ArgumentException("합성 입력 파일이 비어 있습니다.");
            ScenarioCampaign.Validate(spec);
            var observations = spec.Scenarios.ToDictionary(x => x.Id,
                _ => new List<ScenarioPolicyObservation>(), StringComparer.Ordinal);
            var results = (from scenario in spec.Scenarios
                           from throughput in ScenarioCampaign.Throughputs
                           from mode in ScenarioCampaign.Modes
                           select ScenarioCampaign.Run(spec, scenario, mode, throughput,
                               mode == "observed" && throughput == ScenarioAdvisoryEvidence.PlanningThroughput
                                   ? observations[scenario.Id].Add : null)).ToArray();
            var cases = ScenarioAdvisoryEvidence.Select(spec, results, observations);
            var checks = ScenarioSelfChecks.Verify(spec, results)
                .Concat(ScenarioSelfChecks.VerifyAdvisory(spec, results, observations, cases)).ToList();
            var sourceHashes = new Dictionary<string, string>();
            foreach (var path in new[] {
                "Ssalddel.Domain/배차/음식배달수요참여Policy.cs",
                "Ssalddel.Contracts/Common/Dispatch/음식배달수요참여Contracts.cs",
                "eng/local-dispatch-ai/ScenarioRunner/ScenarioCampaign.cs",
                "eng/local-dispatch-ai/ScenarioRunner/ScenarioAdvisoryEvidence.cs",
                "eng/local-dispatch-ai/ScenarioRunner/ScenarioSelfChecks.cs",
                "eng/local-dispatch-ai/ScenarioRunner/Program.cs" })
                sourceHashes[path] = ScenarioCampaign.Hash(File.ReadAllBytes(Path.Combine(root, path)));
            var campaignDigest = ScenarioCampaign.Hash(results);
            var fixtureHash = ScenarioCampaign.Hash(raw);
            var previous = PreviousCompatibleCampaign(output, fixtureHash, sourceHashes);
            if (previous is not null && previous != campaignDigest)
                throw new InvalidOperationException("동일 입력·제품 정책의 기존 전체 결과가 달라졌습니다. 이전 근거를 보존합니다.");
            var completedChecks = checks.ToArray();
            var runnerHash = ScenarioCampaign.Hash(File.ReadAllBytes(Assembly.GetExecutingAssembly().Location));
            var domainHash = ScenarioCampaign.Hash(File.ReadAllBytes(typeof(음식배달수요참여Policy).Assembly.Location));
            var evidenceId = ScenarioCampaign.Hash(new { fixtureHash, sourceHashes, runnerHash, domainHash, campaignDigest });
            var report = new
            {
                schemaVersion = 1, syntheticOnly = true, execution = "offline_assumed_ai",
                fixedEpochUtc = ScenarioCampaign.Epoch, spec.Seed, randomUsed = false,
                spec.HorizonMinutes, spec.DrainMinutes, policyVersion = 음식배달수요참여Policy.현재정책판본,
                fixtureSha256 = fixtureHash, sourceHashes,
                runnerAssemblySha256 = runnerHash, domainAssemblySha256 = domainHash, evidenceId,
                campaignDigest, scenarioCount = spec.Scenarios.Length, runCount = results.Length,
                modelCalls = 0, modelProcessStarts = 0, networkCalls = 0, operationalWrites = 0,
                operationalActivation = false, modelBlockRetained = true,
                decisionModel = "existing-domain-policy; synthetic FIFO sequential-service allocation",
                policyOffReference = "synthetic least-recent-assignments/FIFO; not actual server ETA/route selection",
                aiOracle = "known synthetic future arrivals; not measured AI prediction",
                safetyPassed = results.All(x => x.Violations.Length == 0) && completedChecks.All(x => x.Passed),
                reviewRequiredRuns = results.Count(x => x.ReviewStatus == "REVIEW_REQUIRED"),
                checks = completedChecks, results
            };
            Directory.CreateDirectory(output);
            var runOutput = Path.Combine(output, "runs", evidenceId);
            Directory.CreateDirectory(runOutput);
            Preserve(Path.Combine(runOutput, "campaign-results.json"), JsonSerializer.SerializeToUtf8Bytes(report, ScenarioCampaign.Json));
            Preserve(Path.Combine(runOutput, "report.md"), Encoding.UTF8.GetBytes(Report(spec, results, completedChecks, campaignDigest)));
            Preserve(Path.Combine(runOutput, "scenarios.json"), raw);
            var casesBytes = JsonSerializer.SerializeToUtf8Bytes(cases, ScenarioCampaign.Json);
            var advisoryBytes = JsonSerializer.SerializeToUtf8Bytes(ScenarioAdvisoryEvidence.Document(
                evidenceId, campaignDigest, fixtureHash, sourceHashes, cases, casesBytes), ScenarioCampaign.Json);
            Preserve(Path.Combine(runOutput, ScenarioAdvisoryEvidence.CasesFile), casesBytes);
            Preserve(Path.Combine(runOutput, "advisory-input.json"), advisoryBytes);
            // 실행별 근거는 불변 보존하고 최신 포인터/보고서만 갱신한다.
            File.WriteAllText(Path.Combine(output, "latest.json"), JsonSerializer.Serialize(new
            { evidenceId, campaignDigest, results = Path.GetRelativePath(output, Path.Combine(runOutput, "campaign-results.json")),
                report = Path.GetRelativePath(output, Path.Combine(runOutput, "report.md")),
                advisoryInput = Path.GetRelativePath(output, Path.Combine(runOutput, "advisory-input.json")) }, ScenarioCampaign.Json), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(output, "report.md"), Report(spec, results, completedChecks, campaignDigest), new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(output, ScenarioAdvisoryEvidence.CasesFile), casesBytes);
            File.WriteAllBytes(Path.Combine(output, "advisory-input.json"), advisoryBytes);
            Console.WriteLine(JsonSerializer.Serialize(new { report.scenarioCount, report.runCount,
                checks = completedChecks.Length, failedChecks = completedChecks.Count(x => !x.Passed), report.safetyPassed,
                report.reviewRequiredRuns, campaignDigest, report.modelCalls, report.networkCalls,
                report.operationalWrites, report.modelBlockRetained, previousCompatibleCampaignDigest = previous,
                previousCampaignResultsUnchanged = previous is null ? (bool?)null : previous == campaignDigest,
                advisoryCases = cases.Length,
                advisoryCasesDigest = ScenarioCampaign.Hash(casesBytes), output = runOutput }, ScenarioCampaign.Json));
            return report.safetyPassed ? 0 : 2;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine($"오프라인 시나리오 실패: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static string? PreviousCompatibleCampaign(string output, string fixtureHash,
        IReadOnlyDictionary<string, string> sourceHashes)
    {
        var pointer = Path.Combine(output, "latest.json");
        if (!File.Exists(pointer)) return null;
        using var latest = JsonDocument.Parse(File.ReadAllBytes(pointer));
        var resultPath = latest.RootElement.GetProperty("results").GetString()
            ?? throw new InvalidOperationException("이전 실행의 결과 경로가 없습니다.");
        var path = Path.GetFullPath(resultPath, output);
        if (!path.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("이전 결과 경로가 출력 폴더 밖입니다.");
        using var prior = JsonDocument.Parse(File.ReadAllBytes(path));
        var report = prior.RootElement;
        if (report.GetProperty("fixtureSha256").GetString() != fixtureHash) return null;
        foreach (var key in new[] { "Ssalddel.Domain/배차/음식배달수요참여Policy.cs",
            "Ssalddel.Contracts/Common/Dispatch/음식배달수요참여Contracts.cs" })
            if (report.GetProperty("sourceHashes").GetProperty(key).GetString() != sourceHashes[key]) return null;
        return report.GetProperty("campaignDigest").GetString()
            ?? throw new InvalidOperationException("이전 실행의 결과 해시가 없습니다.");
    }

    private static void Preserve(string path, byte[] content)
    {
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).SequenceEqual(content))
                throw new InvalidOperationException("같은 실행 식별자의 다른 근거를 덮어쓸 수 없습니다.");
            return;
        }
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(content);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Ssalddel.Domain/Ssalddel.Domain.csproj"))) return dir.FullName;
        throw new InvalidOperationException("Hongdal 저장소 안에서 빌드한 도구가 필요합니다.");
    }

    private static string Report(CampaignSpec spec, RunResult[] runs, ScenarioCheck[] checks, string digest)
    {
        var text = new StringBuilder("# AI 가정형 오프라인 운영 시나리오 결과\n\n");
        text.AppendLine($"합성 {spec.Scenarios.Length}개 사례 × 8개 비교 × 계획 처리량 3·3.5·4건/h = {runs.Length}회. " +
            $"고정 시계 {ScenarioCampaign.Epoch:O}, {spec.HorizonMinutes}분 접수 + {spec.DrainMinutes}분 잔여 처리. " +
            "난수 없이 같은 입력을 재현한다. 시나리오 실행 중 모델·네트워크 호출·운영 쓰기 0회, 차단 유지. 빌드의 패키지 복원은 별도다.\n");
        text.AppendLine($"안전 불변식 위반 {runs.Sum(x => x.Violations.Length)}건, 검증 {checks.Count(x => x.Passed)}/{checks.Length} 통과. " +
            $"배정 대기 10분 이상·모의 완료소요 45분 초과·미완료로 검토가 필요한 실행 {runs.Count(x => x.ReviewStatus == "REVIEW_REQUIRED")}회.\n");
        text.AppendLine("## 계획 처리량 3.5건/h 비교\n");
        text.AppendLine("p95는 배정된 신규 주문의 **배정까지 대기**이며 배송 완료까지의 시간이 아니다. 미배정 주문은 별도 열로 확인한다. " +
            "초기 수락 업무는 신규 건수에서 제외하고 완료 합계에는 포함한다. 아래 OFF는 합성 FIFO/배차 횟수 정렬 비교값이며 실제 서버의 거리·ETA 선정 결과가 아니다.\n");
        text.AppendLine("| 상황 | 신규 건수 | OFF 배정 p95(분) | 관측 배정 p95(분) | 모의 정확 배정 p95(분) | 과대 배정 p95(분) | 관측 완료소요 p95(분) | 관측 미배정/미완료 | 관측 10분 이상/45분 초과 |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var scenario in spec.Scenarios)
        {
            var group = runs.Where(x => x.ScenarioId == scenario.Id && x.PlanningThroughput == 3.5m).ToDictionary(x => x.Mode);
            var baseline = group["observed"];
            text.AppendLine($"| {scenario.Title} | {baseline.SubmittedOrders} | {group["policy_off"].AssignedWaitP95Minutes} | " +
                $"{baseline.AssignedWaitP95Minutes} | {group["oracle"].AssignedWaitP95Minutes} | {group["over"].AssignedWaitP95Minutes} | " +
                $"{baseline.CompletedLeadP95Minutes} | {baseline.UnassignedOrders}/{baseline.UnfinishedOrders} | " +
                $"{baseline.OrdersWaitedTenMinutes}/{baseline.OrdersLeadOver45Minutes} |");
        }
        text.AppendLine("\n## 해석과 운영 검토\n");
        text.AppendLine("- 완료소요는 합성 접수부터 순차 수행 종료까지다. 배정/완료 p95는 각각 배정/완료된 신규 주문만의 값이므로 미배정·미완료와 열린 업무 경과시간을 함께 본다. 10분·45분은 탐색용 검토 가정이며 실제 메뉴별 SLA·자동 취소 기준이 아니다.");
        var improvementCount = runs.Where(x => x.Mode == "oracle").Count(x =>
            x.AssignedWaitP95Minutes < runs.Single(y => y.ScenarioId == x.ScenarioId && y.PlanningThroughput == x.PlanningThroughput && y.Mode == "observed").AssignedWaitP95Minutes);
        text.AppendLine($"- 알려진 미래량을 준 모의 정확 예측도 관측 기준보다 배정 p95가 낮아진 조합은 {improvementCount}/{spec.Scenarios.Length * ScenarioCampaign.Throughputs.Length}개다. 이 모형의 예측 이득과 한계를 실제 AI 성능으로 확대하지 않는다.");
        text.AppendLine("- 과소 예측은 관측 수요의 바닥을 낮추지 않는다. 응답 없음·무효·만료도 예측을 버리고 관측 규칙으로 복귀한다. 각 실행에서 결정 해시 일치와 반복 재현을 검사한다.");
        text.AppendLine("- 과대 예측은 대기 기사 권고를 늘릴 수 있다. 모의 AI가 기존 관문·동시 3건을 바꾸지는 못하지만, 기사별 기회와 실제 수입을 보장하지 않는다.");
        text.AppendLine("- 기사 부족·긴 수행시간·수요 급증에서 생긴 지연은 AI 예측만으로 해결되지 않는다. 제안 반경/약속 시간, 접수 가능한 용량과 실제 대안을 운영자가 검토해야 한다.");
        text.AppendLine("- 수요 누락·만료는 0건으로 간주하지 않고 참여 제한을 보류한다. GPS 만료/신규 제안 OFF는 별도의 자격으로 가정하며 수락한 업무는 계속 보존한다.");
        text.AppendLine("- 장기 대기 권고는 실제 주문이 없으면 배차나 수입이 아니다. 권고 tick·최대 인원과 실제 가상 배정 건수를 분리한다.");
        text.AppendLine("- 기사별 건수 편차는 합류 시간·자격 제한이 다른 집단의 원시 차이다. 공정성 점수로 사용하지 않는다. 순차 수행시간은 고정 합성값이며 지리·조리·픽업 적시성·사고 복구·실제 지연/소득은 이 도구에서 입증하지 않는다.");
        text.AppendLine("- 서버 업무 회복의 별도 시험 기록은 `artifacts/local/local-dispatch-scenarios-r45/workflow-recovery/`에 있다. 이 시나리오 통과가 실제 네 역할 HTTP·DB·휴대폰·푸시·입금 성공을 뜻하지 않는다.");
        text.AppendLine($"\n결과 해시: `{digest}`. 전체 시간표·기사별 집계·검증은 실행별 `campaign-results.json`, 입력 사본은 `scenarios.json`이다. 최신 포인터는 `latest.json`이다. " +
            "실제 운영 활성화·AI 실행·차단 해제·APK·배포·commit/push는 수행하지 않았다.");
        return text.ToString();
    }
}
