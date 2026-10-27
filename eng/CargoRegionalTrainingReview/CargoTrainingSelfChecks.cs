using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ssalddel.Contracts.Admin.DispatchTraining;

namespace CargoRegionalTrainingReview;

/// <summary>운영 DB·모델을 호출하지 않고 별도 사본에서만 검토·내보내기 경계를 검사한다.</summary>
public static class CargoTrainingSelfChecks
{
    private const string RegionRelativePath = "eng/local-dispatch-ai/cargo-regional-training/legal-dong-regions.json";

    public static async Task<object> RunAsync(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var directory = Path.GetFullPath(Path.Combine(root, "artifacts/local/cargo-regional-training-r113/selfchecks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        var sourcePaths = CargoTrainingPlanCases.SourcePaths.Append("Ssalddel/Ssalddel.csproj").ToList();
        using (var regions = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, RegionRelativePath))))
            sourcePaths.Add(regions.RootElement.GetProperty("source").GetProperty("archivePath").GetString()!);
        sourcePaths = sourcePaths.Distinct(StringComparer.Ordinal).ToList();
        var originals = sourcePaths.ToDictionary(x => x, x => CargoTrainingStore.FileHash(Path.Combine(root, x)), StringComparer.Ordinal);
        var results = new List<CheckResult>();
        var fixtureIndex = 0;

        Fixture NewFixture(ICargoTrainingModel? model = null, bool planReview = false)
        {
            var path = Path.Combine(directory, $"fixture-{++fixtureIndex:00}");
            var copy = Path.GetFullPath(Path.Combine(path, "repo-copy"));
            foreach (var relative in sourcePaths)
            {
                var destination = Path.GetFullPath(Path.Combine(copy, relative));
                Require(destination.StartsWith(copy + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "사본 경계 밖 경로입니다.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(root, relative), destination, overwrite: false);
            }
            File.WriteAllText(Path.Combine(path, "SELF-CHECK-ONLY.json"), JsonSerializer.Serialize(new
            {
                syntheticApprovalOnly = true, realHumanApproval = false, modelCalled = false,
                operatingDatabaseUsed = false, weightTrainingPerformed = false,
                purpose = "자동 시험 사본이며 실제 사람의 승인 또는 학습 자산이 아니다."
            }, CargoTrainingStore.Json), Encoding.UTF8);
            var output = Path.Combine(path, "isolated-feedback");
            return new(copy, output, new CargoTrainingStore(copy, output, model ?? new StubModel(_ => throw new InvalidOperationException("UnexpectedModelCall")), planReview));
        }

        async Task Check(string name, Func<Task> check)
        {
            try { await check(); results.Add(new(name, true, "통과")); }
            catch (Exception e) { results.Add(new(name, false, e.GetType().Name + ": " + e.Message)); }
        }

        await Check("official-region-provenance-and-private-scope", async () =>
        {
            var f = NewFixture();
            var workspace = await f.Store.LoadAsync();
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(f.Repo, RegionRelativePath)));
            var rows = doc.RootElement.GetProperty("regions").EnumerateArray().ToArray();
            Require(rows.Length == 15 && rows.All(r => Regex.IsMatch(r.GetProperty("legalDongCode").GetString()!, "^[0-9]{10}$")
                && r.GetProperty("sourceLineNumber").GetInt32() > 1 && IsHash(r.GetProperty("sourceRowSha256").GetString()!)), "공식 코드·원문 행 근거가 필요합니다.");
            var legalCodes = rows.Select(r => r.GetProperty("legalDongCode").GetString()).ToHashSet();
            Require(workspace.Cases.All(c => legalCodes.Contains(c.PickupRegion.LegalDongCode) && legalCodes.Contains(c.DropoffRegion.LegalDongCode)
                && c.Candidates.All(d => legalCodes.Contains(d.CurrentRegion.LegalDongCode) && legalCodes.Contains(d.AvailableRegion.LegalDongCode))), "모든 법정동은 보관 원문 목록에 있어야 합니다.");
            Require(IsHash(workspace.DatasetSha256) && IsHash(workspace.PolicySha256) && IsHash(workspace.RegionSourceSha256), "자료 판본 해시를 보존해야 합니다.");
            Require(!workspace.WeightsTrained && !workspace.OperationalDispatchEnabled && workspace.TrainingEligibleCount == 0, "초기 상태를 실제 학습·운영·사람 승인으로 표시하면 안 됩니다.");
            var scope = doc.RootElement.GetProperty("scope");
            foreach (var key in new[] { "coordinateDataIncluded", "streetAddressDataIncluded", "customerDataIncluded", "administrativeDongCodesIncluded" })
                Require(!scope.GetProperty(key).GetBoolean(), "고객·상세 주소·좌표를 포함하면 안 됩니다.");
            var dataset = File.ReadAllText(Path.Combine(f.Store.OutputDirectory, "dataset.json"));
            Require(!Regex.IsMatch(dataset, "01[016789][- ]?[0-9]{3,4}[- ]?[0-9]{4}") && !dataset.Contains("@", StringComparison.Ordinal), "합성 자료에 전화·이메일 식별값이 없어야 합니다.");
        });

        await Check("family-splits-are-disjoint-and-identities-unique", async () =>
        {
            var w = await NewFixture().Store.LoadAsync();
            Require(w.Cases.Count == 28 && w.Cases.Count(c => c.Split == "review") == 20 && w.Cases.Count(c => c.Split == "holdout") == 8, "검토20·평가8 사례를 구분합니다.");
            Require(w.Cases.GroupBy(c => c.FamilyId).All(g => g.Count() == 2 && g.Select(c => c.Split).Distinct().Count() == 1), "같은 조건군의 변형은 학습 검토와 평가로 나누면 안 됩니다.");
            Require(w.Cases.Select(c => c.CaseId).Distinct().Count() == w.Cases.Count && w.Cases.Select(c => c.InputSha256).Distinct().Count() == w.Cases.Count, "사례·입력 해시는 중복되면 안 됩니다.");
            Require(w.Cases.All(c => c.InputSha256 == CargoTrainingCases.InputFingerprint(c) && c.TaskType == "SmallQuick"), "다른 화물 유형이나 변경된 입력을 섞으면 안 됩니다.");
        });

        await Check("deterministic-generation-and-eligible-rule-selection", async () =>
        {
            var f = NewFixture(); var w = await f.Store.LoadAsync();
            var second = new CargoTrainingStore(f.Repo, f.Output, new StubModel(_ => throw new InvalidOperationException("UnexpectedModelCall")));
            Require((await second.LoadAsync()).DatasetSha256 == w.DatasetSha256, "같은 원본은 같은 자료 판본을 만들어야 합니다.");
            Require(w.Cases.All(c => c.Engine.Source == "current_cargo_rule" && (c.Engine.CandidateId is null
                ? c.Engine.Status == "hold" && c.Candidates.All(d => !d.Eligible)
                : c.Engine.Status == "selected" && c.Candidates.Any(d => d.Id == c.Engine.CandidateId && d.Eligible))), "기존 규칙 선택은 가능 후보만 포함해야 합니다.");
            Require(w.Cases.All(c => c.Candidates.All(d => !d.Eligible || d.Receiving && d.CommitmentsFeasible
                && d.CapacityKg >= c.WeightKg && d.CapacityLiters >= c.VolumeLiters && d.LocationAgeMinutes <= 5
                && d.PickupEtaMinutes == d.AvailableAfterMinutes + d.TravelAfterAvailableMinutes && d.PickupEtaMinutes <= c.PickupDeadlineMinutes)), "가능 후보의 적재·약속·가용시각·ETA를 검사합니다.");
            Require(w.Cases.SelectMany(c => c.Candidates).Any(d => d.TravelAfterAvailableMinutes is null && !d.Eligible), "미확인 ETA를0분으로 채우지 않습니다.");
        });

        await Check("busy-last-destination-and-counterfactual-wait", async () =>
        {
            var w = await NewFixture().Store.LoadAsync();
            var busy = w.Cases.Where(c => c.FamilyId == "busy_final_destination").ToArray();
            Require(busy.Length == 2 && busy.All(c => c.Candidates[0].CurrentRegion != c.Candidates[0].AvailableRegion
                && c.Candidates[0].PickupEtaMinutes == c.Candidates[0].AvailableAfterMinutes + 2), "수행 중 기사는 마지막 목적지 가용시각부터 계산합니다.");
            Require(busy[0].Candidates[0].Eligible && !busy[1].Candidates[0].Eligible, "기한을 넘는 다음 업무를 대기 점수로 복원하면 안 됩니다.");
            var waits = w.Cases.Where(c => c.FamilyId == "long_wait").ToArray();
            Require(waits[0].Engine.CandidateId == "C1" && waits[1].Engine.CandidateId == "C2", "다른 조건이 같은 변형에서는 오래 기다린 후보를 고려해야 합니다.");
        });

        await Check("returned-snapshots-cannot-mutate-canonical-input", async () =>
        {
            var f = NewFixture(); var w = await f.Store.LoadAsync(); var dataset = w.DatasetSha256;
            w.Cases[0].Candidates[0].ScoreParts["tampered"] = 9000;
            w.Cases[0].Candidates.Clear(); w.Cases.RemoveAt(1);
            var fresh = await f.Store.LoadAsync();
            Require(fresh.Cases.Count == 28 && fresh.Cases[0].Candidates.Count == 2 && !fresh.Cases[0].Candidates[0].ScoreParts.ContainsKey("tampered")
                && fresh.DatasetSha256 == dataset, "클라이언트가 반환 사본을 수정해도 정본은 유지해야 합니다.");
        });

        await Check("export-before-human-review-is-empty", async () =>
        {
            var export = await NewFixture().Store.ExportApprovedAsync();
            Require(export.ApprovedRows == 0 && new FileInfo(export.Path).Length == 0 && export.HoldoutRowsExcluded == 8 && !export.WeightsTrained, "규칙 정답만으로 학습 승인 자료를 생성하면 안 됩니다.");
        });

        await Check("approval-replay-payload-conflict-and-restart", async () =>
        {
            var f = NewFixture(); var w = await f.Store.LoadAsync(); var c = w.Cases.First(x => x.Split == "review");
            var command = Answer(w, c);
            var once = await f.Store.RecordAsync(command); var replay = await f.Store.RecordAsync(command);
            Require(Item(once, c.CaseId).ReviewRevision == 1 && Item(replay, c.CaseId).ReviewRevision == 1, "같은 요청은 한 번만 기록해야 합니다.");
            await Reject(() => f.Store.RecordAsync(command with { Note = "다른 명령 내용" }), "RequestConflict");
            await Reject(() => f.Store.RecordAsync(command with { RequestId = Guid.NewGuid().ToString(), Outcome = "defer", AcceptableCandidateIds = [] }), "RevisionConflict");
            var restart = new CargoTrainingStore(f.Repo, f.Output, new StubModel(_ => throw new InvalidOperationException("UnexpectedModelCall")));
            var restored = Item(await restart.LoadAsync(), c.CaseId);
            Require(restored.Review?.RequestId == command.RequestId && restored.ReviewRevision == 1 && restored.Review.TrainingEligible, "재시작 후 동일 자료·승인 기록을 복원해야 합니다.");
            var export = await restart.ExportApprovedAsync();
            Require(export.ApprovedRows == 1 && CargoTrainingStore.FileHash(export.Path) == export.Sha256, "시험 사본의 승인만 내보내고 바이트 해시를 보존합니다.");
            using var row = JsonDocument.Parse(File.ReadLines(export.Path).Single());
            Require(row.RootElement.GetProperty("reviewRequestId").GetString() == command.RequestId && row.RootElement.GetProperty("inputSha256").GetString() == c.InputSha256, "내보낸 행은 정확한 사람 검토 요청·입력에 연결해야 합니다.");
        });

        await Check("holdout-review-never-enters-training-export", async () =>
        {
            var f = NewFixture(); var w = await f.Store.LoadAsync();
            foreach (var c in w.Cases.Where(x => x.Split == "holdout"))
            {
                w = await f.Store.RecordAsync(Answer(w, c));
                Require(!Item(w, c.CaseId).Review!.TrainingEligible, "평가 사례의 사람 확인을 학습 허가로 바꾸면 안 됩니다.");
            }
            var export = await f.Store.ExportApprovedAsync();
            Require(export.ApprovedRows == 0 && export.HoldoutRowsExcluded == 8, "평가 사례는 승인해도 학습 export에서 제외해야 합니다.");
        });

        await Check("defer-and-review-withdrawal-are-not-exported", async () =>
        {
            var f = NewFixture(); var w = await f.Store.LoadAsync(); var c = w.Cases.First(x => x.Split == "review");
            w = await f.Store.RecordAsync(Answer(w, c)); c = Item(w, c.CaseId);
            await f.Store.RecordAsync(Answer(w, c) with { Outcome = "defer", AcceptableCandidateIds = [], AcceptHold = false });
            Require((await f.Store.ExportApprovedAsync()).ApprovedRows == 0, "최신 검토를 보류로 바꾸면 과거 승인을 내보내지 않습니다.");
        });

        await Check("review-input-dataset-and-eligible-answer-guards", async () =>
        {
            var f = NewFixture(); var w = await f.Store.LoadAsync(); var c = w.Cases.First(x => x.Candidates.Any(d => !d.Eligible) && x.Split == "review");
            var command = Answer(w, c);
            await Reject(() => f.Store.RecordAsync(command with { DatasetSha256 = new string('0', 64) }), "DatasetChanged");
            await Reject(() => f.Store.RecordAsync(command with { InputSha256 = new string('0', 64) }), "InputChanged");
            await Reject(() => f.Store.CompareAsync(c.CaseId, new string('0', 64)), "InputChanged");
            await Reject(() => f.Store.RecordAsync(command with { AcceptableCandidateIds = [c.Candidates.First(d => !d.Eligible).Id] }), "IneligibleCandidate");
            await Reject(() => f.Store.RecordAsync(command with { AcceptableCandidateIds = ["INVENTED"] }), "IneligibleCandidate");
            await Reject(() => f.Store.RecordAsync(command with { AcceptableCandidateIds = [], AcceptHold = false }), "NoDecision");
            await Reject(() => f.Store.RecordAsync(command with { Outcome = "defer" }), "DeferredHasAnswer");
            Require((await f.Store.LoadAsync()).TrainingEligibleCount == 0, "거절한 명령은 부분 저장하면 안 됩니다.");
        });

        await Check("two-instances-enforce-one-revision-writer", async () =>
        {
            var f = NewFixture(); var w = await f.Store.LoadAsync(); var c = w.Cases.First();
            var second = new CargoTrainingStore(f.Repo, f.Output, new StubModel(_ => throw new InvalidOperationException("UnexpectedModelCall")));
            async Task<string> Try(CargoTrainingStore store)
            {
                try { await store.RecordAsync(Answer(w, c)); return "saved"; }
                catch (CargoTrainingReviewException e) { return e.Code; }
            }
            var outcomes = await Task.WhenAll(Task.Run(() => Try(f.Store)), Task.Run(() => Try(second)));
            Require(outcomes.Count(x => x == "saved") == 1 && outcomes.Count(x => x == "RevisionConflict") == 1
                && Item(await second.LoadAsync(), c.CaseId).ReviewRevision == 1, "동시 변경은 한 번만 저장하고 다른 요청은 충돌해야 합니다.");
        });

        await Check("model-output-shape-identity-and-fact-validation", () =>
        {
            var w = NewFixture().Store.LoadAsync().GetAwaiter().GetResult(); var c = w.Cases.First(x => x.FamilyId == "long_wait");
            Require(CargoTrainingOllama.Validate(c, "{\"candidateId\":\"C1\",\"reasonCodes\":[\"LONG_WAIT\",\"CAPACITY_OK\"]}").Status == "valid", "지원되는 후보·사실만 정상 결과입니다.");
            foreach (var raw in new[]
            {
                "not json", "[]", "{\"candidateId\":\"INVENTED\",\"reasonCodes\":[\"REVIEW_NEEDED\"]}",
                "{\"candidateId\":\"C2\",\"reasonCodes\":[\"LONG_WAIT\"]}",
                "{\"candidateId\":\"C1\",\"reasonCodes\":[\"UNKNOWN_REASON\"]}",
                "{\"candidateId\":\"C1\",\"reasonCodes\":[\"RETURN_DIRECTION\"]}",
                "{\"candidateId\":\"C1\",\"reasonCodes\":[],\"extra\":true}"
            }) Require(CargoTrainingOllama.Validate(c, raw).Status == "invalid", "모델의 추가 지시·허위 사실·미확인 ID를 거절해야 합니다.");
            var excluded = w.Cases.First(x => x.FamilyId == "missing_eta" && x.Candidates.Any(d => !d.Eligible));
            Require(CargoTrainingOllama.Validate(excluded, "{\"candidateId\":\"C1\",\"reasonCodes\":[\"REVIEW_NEEDED\"]}").Status == "invalid", "실제 목록에 있는 부적격 후보도 거절해야 합니다.");
            var noCandidate = w.Cases.First(x => x.Candidates.All(d => !d.Eligible));
            Require(CargoTrainingOllama.Validate(noCandidate, "{\"candidateId\":\"hold\",\"reasonCodes\":[\"NO_ELIGIBLE\"]}").Status == "valid", "적격자 없는 보류를 합법적인 비교 결과로 유지합니다.");
            return Task.CompletedTask;
        });

        await Check("model-comparison-is-not-human-approval", async () =>
        {
            var fake = new StubModel(c => Decision(c, c.Candidates.First(d => d.Eligible).Id));
            var f = NewFixture(fake); var w = await f.Store.LoadAsync(); var c = w.Cases.First();
            var compared = Item(await f.Store.CompareAsync(c.CaseId, c.InputSha256), c.CaseId);
            Require(fake.Calls == 1 && compared.Ai.Status == "valid" && compared.Ai.RunId is not null && compared.Review is null
                && SameDecision(compared.Engine, c.Engine) && (await f.Store.ExportApprovedAsync()).ApprovedRows == 0, "모의 모델 결과는 규칙·사람 검토·학습과 분리합니다.");
            await Reject(() => f.Store.RecordAsync(Answer(w, c)), "ModelResultChanged");
        });

        await Check("model-result-change-invalidates-old-review-binding", async () =>
        {
            var f = NewFixture(new StubModel(c => Decision(c, c.Candidates.First(d => d.Eligible).Id)));
            var w = await f.Store.LoadAsync(); var id = w.Cases.First().CaseId; var c = Item(w, id);
            w = await f.Store.CompareAsync(id, c.InputSha256); var old = Answer(w, Item(w, id));
            w = await f.Store.CompareAsync(id, c.InputSha256);
            await Reject(() => f.Store.RecordAsync(old), "ModelResultChanged");
            Require(Item(w, id).Ai.RunId != old.ObservedModelRunId && (await f.Store.ExportApprovedAsync()).ApprovedRows == 0, "화면에서 본 모델 판본이 바뀌면 새로 확인해야 합니다.");
        });

        await Check("model-timeout-keeps-rule-and-is-not-valid-ai", async () =>
        {
            var f = NewFixture(new StubModel(c => new CargoTrainingDecision
            {
                Status = "timeout", Source = "selfcheck_fake", ErrorCode = "ModelTimeout", RunId = Guid.NewGuid().ToString("N"), InputSha256 = c.InputSha256
            }));
            var w = await f.Store.LoadAsync(); var c = w.Cases.First();
            var result = Item(await f.Store.CompareAsync(c.CaseId, c.InputSha256), c.CaseId);
            Require(result.Ai.Status == "timeout" && result.Ai.CandidateId is null && SameDecision(result.Engine, c.Engine) && result.Review is null, "시간초과를 AI 선택 성공으로 바꾸거나 규칙 결과를 지우면 안 됩니다.");
        });

        await Check("model-port-cannot-smuggle-ineligible-or-invented-id", async () =>
        {
            foreach (var selected in new[] { "INVENTED", "C1" })
            {
                var f = NewFixture(new StubModel(c => Decision(c, selected)));
                var w = await f.Store.LoadAsync(); var c = w.Cases.First(x => x.FamilyId == "missing_eta" && !x.Candidates[0].Eligible);
                var result = Item(await f.Store.CompareAsync(c.CaseId, c.InputSha256), c.CaseId);
                Require(result.Ai.Status == "invalid" && result.Ai.CandidateId is null && SameDecision(result.Engine, c.Engine), "모델 포트의 valid 주장보다 최종 입력 적격성을 우선해야 합니다.");
            }
        });

        await Check("model-port-input-hash-mismatch-is-invalid", async () =>
        {
            var f = NewFixture(new StubModel(c => Decision(c, "C1") with { InputSha256 = new string('0', 64) }));
            var w = await f.Store.LoadAsync(); var c = w.Cases.First();
            var result = Item(await f.Store.CompareAsync(c.CaseId, c.InputSha256), c.CaseId);
            Require(result.Ai.Status == "invalid" && result.Ai.CandidateId is null, "다른 입력의 모델 답을 현재 사례에 연결하면 안 됩니다.");
        });

        await Check("model-port-mutation-cannot-change-eligibility-or-canonical-input", async () =>
        {
            var f = NewFixture(new StubModel(c =>
            {
                var index = c.Candidates.FindIndex(d => !d.Eligible);
                Require(index >= 0, "부적격 후보가 있는 시험 사례여야 합니다.");
                var id = c.Candidates[index].Id;
                c.Candidates[index] = c.Candidates[index] with { Eligible = true, ExclusionReasons = [], PickupEtaMinutes = 1 };
                return Decision(c, id);
            }));
            var w = await f.Store.LoadAsync(); var c = w.Cases.First(x => x.FamilyId == "missing_eta" && !x.Candidates[0].Eligible);
            var result = Item(await f.Store.CompareAsync(c.CaseId, c.InputSha256), c.CaseId);
            var fresh = Item(await f.Store.LoadAsync(), c.CaseId);
            Require(result.Ai.Status == "invalid" && result.Ai.CandidateId is null && !result.Candidates[0].Eligible
                && fresh.InputSha256 == CargoTrainingCases.InputFingerprint(fresh) && fresh.InputSha256 == c.InputSha256
                && SameDecision(result.Engine, c.Engine), "모델의 입력 사본 변경은 적격성·원본 입력·규칙 선택을 바꾸면 안 됩니다.");
        });

        await Check("late-or-failed-model-port-result-has-no-selected-candidate", async () =>
        {
            foreach (var status in new[] { "valid", "timeout", "invalid", "unavailable", "invented_status" })
            {
                var f = NewFixture(new StubModel(c => Decision(c, "C1") with { Status = status, ElapsedMilliseconds = 20_001 }));
                var w = await f.Store.LoadAsync(); var c = w.Cases.First();
                var result = Item(await f.Store.CompareAsync(c.CaseId, c.InputSha256), c.CaseId);
                Require(result.Ai.Status is "invalid" or "timeout" or "unavailable" && result.Ai.CandidateId is null
                    && SameDecision(result.Engine, c.Engine), "기한 초과·실패 상태는 선택된 후보를 남기지 않고 규칙 비교를 보존해야 합니다.");
            }
        });

        await Check("changed-policy-source-is-rejected", async () =>
        {
            var f = NewFixture();
            File.AppendAllText(Path.Combine(f.Repo, CargoTrainingCases.PolicySourcePaths.First(x => x.EndsWith(".cs", StringComparison.Ordinal))), "\n// selfcheck source changed\n");
            await Reject(() => f.Store.LoadAsync(), "SourceChanged");
            await Reject(() => f.Store.ExportApprovedAsync(), "SourceChanged");
        });

        await Check("extracted-store-sources-are-guarded-in-both-review-modes", async () =>
        {
            var extractedSources = new[]
            {
                "eng/CargoRegionalTrainingReview/CargoTrainingDatasetEvidence.cs",
                "eng/CargoRegionalTrainingReview/CargoTrainingEventLog.cs",
                "eng/CargoRegionalTrainingReview/CargoTrainingModelComparison.cs"
            };
            foreach (var planReview in new[] { false, true })
                foreach (var relative in extractedSources)
                {
                    var policySources = planReview ? CargoTrainingPlanCases.SourcePaths : CargoTrainingCases.PolicySourcePaths;
                    Require(policySources.Contains(relative, StringComparer.Ordinal), "추출한 책임의 소스가 정책 판본에 포함되어야 합니다: " + relative);
                    var f = NewFixture(planReview: planReview);
                    File.AppendAllText(Path.Combine(f.Repo, relative), "\n// selfcheck extracted source changed\n");
                    await Reject(() => f.Store.LoadAsync(), "SourceChanged");
                    await Reject(() => f.Store.ExportApprovedAsync(), "SourceChanged");
                }
        });

        await Check("changed-official-archive-is-rejected-after-initialization", async () =>
        {
            var f = NewFixture();
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(f.Repo, RegionRelativePath)));
            var archive = Path.Combine(f.Repo, doc.RootElement.GetProperty("source").GetProperty("archivePath").GetString()!);
            File.AppendAllText(archive, "selfcheck archive corruption");
            await MustReject(() => f.Store.LoadAsync(), "초기화 후 공식 원문이 바뀌면 검토를 차단해야 합니다.");
        });

        await Check("changed-dataset-snapshot-is-rejected", async () =>
        {
            var f = NewFixture(); var file = Path.Combine(f.Store.OutputDirectory, "dataset.json");
            var data = JsonNode.Parse(File.ReadAllBytes(file))!; data["cases"]![0]!["fare"] = 99;
            File.WriteAllText(file, data.ToJsonString(CargoTrainingStore.Json), Encoding.UTF8);
            await Reject(() => f.Store.LoadAsync(), "DatasetChanged");
        });

        await Check("changed-provenance-snapshot-is-rejected", async () =>
        {
            var f = NewFixture(); var file = Path.Combine(f.Store.OutputDirectory, "sources.json");
            var data = JsonNode.Parse(File.ReadAllBytes(file))!; data["operationalDispatchEnabled"] = true;
            File.WriteAllText(file, data.ToJsonString(CargoTrainingStore.Json), Encoding.UTF8);
            await MustReject(() => f.Store.LoadAsync(), "출처·운영 경계 manifest가 바뀌면 차단해야 합니다.");
        });

        await Check("corrupt-feedback-is-not-silently-skipped", async () =>
        {
            var f = NewFixture(); var w = await f.Store.LoadAsync(); await f.Store.RecordAsync(Answer(w, w.Cases.First()));
            File.AppendAllText(Path.Combine(f.Store.OutputDirectory, "review-events.jsonl"), "{incomplete-json");
            await Reject(() => f.Store.LoadAsync(), "HistoryCorrupt");
        });

        await Check("well-formed-feedback-tampering-is-rejected-on-replay", async () =>
        {
            foreach (var field in new[] { "caseId", "inputSha256", "policySha256", "acceptableCandidateIds", "trainingEligible" })
            {
                var f = NewFixture(); var w = await f.Store.LoadAsync();
                var c = field == "trainingEligible" ? w.Cases.First(x => x.Split == "holdout") : w.Cases.First();
                await f.Store.RecordAsync(Answer(w, c));
                var file = Path.Combine(f.Store.OutputDirectory, "review-events.jsonl");
                var data = JsonNode.Parse(File.ReadLines(file).Single())!;
                var review = data["review"]!;
                switch (field)
                {
                    case "caseId": review[field] = "another-case"; break;
                    case "inputSha256":
                    case "policySha256": review[field] = new string('0', 64); break;
                    case "acceptableCandidateIds": review[field] = new JsonArray(JsonValue.Create("INVENTED")); break;
                    case "trainingEligible": review[field] = true; break;
                }
                File.WriteAllText(file, data.ToJsonString(CargoTrainingStore.Json) + "\n", Encoding.UTF8);
                await MustReject(() => f.Store.LoadAsync(), "정상 JSON이라도 중첩 검토의 " + field + " 변경을 거절해야 합니다.");
                await MustReject(() => f.Store.ExportApprovedAsync(), "변경된 검토 기록을 학습 자료로 내보내면 안 됩니다.");
            }
        });

        await Check("plan-revision-preserves-existing-28-inputs-and-starts-unreviewed", async () =>
        {
            var old = await NewFixture().Store.LoadAsync();
            var fresh = await NewFixture(planReview: true).Store.LoadAsync();
            Require(fresh.Cases.Count == 33 && fresh.DatasetId == "cargo-plan-training-r76" && fresh.DatasetSha256 != old.DatasetSha256,
                "기존 자료와 별도인 계획 검토 판본이 필요합니다.");
            Require(old.Cases.All(c => Item(fresh, c.CaseId).InputSha256 == c.InputSha256)
                && fresh.Cases.All(c => c.Review is null && c.Ai.Status == "not_run") && fresh.TrainingEligibleCount == 0,
                "기존 28사례 입력을 바꾸거나 새 사례를 미리 승인하면 안 됩니다.");
            var allPlans = fresh.Cases.Where(c => c.PlanSetSha256 is not null).SelectMany(c => c.Candidates).Select(c => c.Plan!).ToArray();
            Require(allPlans.All(p => p.Source == "synthetic" && p.AgreedDriverCompensation is null
                && p.PlanSha256 == CargoTrainingPlanCases.PlanFingerprint(p)), "합성 계획 근거와 미확인 보수를 보존해야 합니다.");
            var missing = fresh.Cases.Single(c => c.FamilyId == "plan_missing_all_handling");
            Require(missing.Candidates.All(c => !c.Eligible && c.Plan!.TotalMinutes is null
                && c.Plan.Stops.Any(s => s.HandlingMinutes is null) && c.Plan.Stops.All(s => s.ArrivalMinutes is null)),
                "누락된 작업시간을 0분이나 이후 도착 확정으로 바꾸면 안 됩니다.");
        });

        await Check("joint-capacity-and-deadline-results-come-from-shared-checks", async () =>
        {
            var w = await NewFixture(planReview: true).Store.LoadAsync();
            foreach (var family in new[] { "plan_overloaded", "plan_deadline", "plan_missing_handling" })
            {
                var c = w.Cases.Single(c => c.FamilyId == family);
                Require(!c.Candidates[0].Eligible && c.Candidates[1].Eligible && c.Engine.Status == "not_run" && c.Engine.CandidateId is null,
                    "함께 불가한 계획을 개별 가능하다는 이유로 허용하면 안 됩니다.");
            }
            var overload = w.Cases.Single(c => c.FamilyId == "plan_overloaded").Candidates[0].Plan!;
            Require(overload.Verified && overload.ReasonCode == "FreightSegment.CapacityExceeded"
                && overload.Segments.Any(s => s.WeightKg > 10), "구간 중량 초과의 실제 순수 검사 근거가 필요합니다.");
        });

        await Check("plan-order-time-and-loading-are-bound-to-input-fingerprint", async () =>
        {
            var c = (await NewFixture(planReview: true).Store.LoadAsync()).Cases.Single(c => c.FamilyId == "plan_compatible");
            var candidate = c.Candidates[0]; var p = candidate.Plan!;
            foreach (var changed in new[]
            {
                p with { Stops = p.Stops.AsEnumerable().Reverse().ToList() },
                p with { Stops = p.Stops.Select((s, i) => i == 0 ? s with { HandlingMinutes = 9 } : s).ToList() },
                p with { Segments = p.Segments.Select((s, i) => i == 1 ? s with { WeightKg = 99 } : s).ToList() },
                p with { InputSha256 = new string('0', 64) }
            })
            {
                Require(CargoTrainingPlanCases.PlanFingerprint(changed) != p.PlanSha256, "계획 근거 변경이 같은 계획 해시가 되면 안 됩니다.");
                var copy = c with { Candidates = [candidate with { Plan = changed }, c.Candidates[1]] };
                Require(CargoTrainingCases.InputFingerprint(copy) != c.InputSha256, "순서·작업시간·적재 근거를 입력 지문에 포함해야 합니다.");
                Require(CargoTrainingOllama.Validate(copy, "{\"candidateId\":\"P1\",\"reasonCodes\":[\"PLAN_VERIFIED\"]}").Status == "invalid",
                    "변경된 계획 근거의 모델 선택은 거절해야 합니다.");
            }
        });

        await Check("model-input-only-contains-feasible-plans", async () =>
        {
            var c = (await NewFixture(planReview: true).Store.LoadAsync()).Cases.Single(c => c.FamilyId == "plan_overloaded");
            using var input = JsonDocument.Parse(JsonSerializer.Serialize(CargoTrainingOllama.Input(c), CargoTrainingStore.Json));
            var candidates = input.RootElement.GetProperty("candidates").EnumerateArray().ToArray();
            Require(candidates.Length == 1 && candidates[0].GetProperty("id").GetString() == "P2"
                && candidates[0].GetProperty("plan").GetProperty("source").GetString() == "synthetic", "불가 계획을 모델에 전달하면 안 됩니다.");
            Require(CargoTrainingOllama.Validate(c, "{\"candidateId\":\"P1\",\"reasonCodes\":[\"PLAN_VERIFIED\"]}").Status == "invalid"
                && CargoTrainingOllama.Validate(c, "{\"candidateId\":\"P2\",\"reasonCodes\":[\"PLAN_VERIFIED\",\"PLAN_VERIFIED\"]}").Status == "invalid",
                "허용하지 않은 계획과 중복 근거 응답을 거절해야 합니다.");
        });

        await Check("no-feasible-plan-does-not-call-model-or-invent-a-run", async () =>
        {
            var model = new StubModel(_ => throw new InvalidOperationException("UnexpectedModelCall"));
            var f = NewFixture(model, planReview: true); var w = await f.Store.LoadAsync();
            var c = w.Cases.Single(c => c.FamilyId == "plan_missing_all_handling");
            await Reject(() => f.Store.CompareAsync(c.CaseId, c.InputSha256), "NoFeasiblePlan");
            Require(model.Calls == 0 && Item(await f.Store.LoadAsync(), c.CaseId).Ai.RunId is null,
                "가능 계획이 없으면 모델 실행 기록을 만들면 안 됩니다.");
        });

        await Check("plan-approval-and-export-bind-same-plan-set", async () =>
        {
            var f = NewFixture(planReview: true); var w = await f.Store.LoadAsync();
            var c = w.Cases.Single(c => c.FamilyId == "plan_overloaded");
            await Reject(() => f.Store.RecordAsync(Answer(w, c) with { PlanSetSha256 = new string('0', 64) }), "PlanEvidenceChanged");
            await Reject(() => f.Store.RecordAsync(Answer(w, c) with { AcceptableCandidateIds = ["P1"] }), "IneligibleCandidate");
            w = await f.Store.RecordAsync(Answer(w, c));
            var export = await f.Store.ExportApprovedAsync();
            var row = JsonNode.Parse(File.ReadLines(export.Path).Single())!;
            Require(export.ApprovedRows == 1 && row["planSetSha256"]!.GetValue<string>() == c.PlanSetSha256
                && row["acceptedPlans"]![0]!["planSha256"]!.GetValue<string>() == c.Candidates[1].Plan!.PlanSha256
                && row["source"]!.GetValue<string>() == "synthetic" && !row["weightsTrained"]!.GetValue<bool>(),
                "승인 자료는 사람이 확인한 같은 입력·계획을 담고 학습 완료로 승격하지 않아야 합니다.");
            var path = Path.Combine(f.Store.OutputDirectory, "review-events.jsonl");
            var eventNode = JsonNode.Parse(File.ReadLines(path).Single())!;
            eventNode["review"]!["planSetSha256"] = new string('0', 64);
            File.WriteAllText(path, eventNode.ToJsonString(CargoTrainingStore.Json) + "\n", Encoding.UTF8);
            await MustReject(() => f.Store.ExportApprovedAsync(), "계획 승인 근거 변조는 내보내기에서 거절해야 합니다.");
        });

        await Check("plan-model-raw-response-is-separate-and-revalidated", async () =>
        {
            var model = new StubModel(c => CargoTrainingOllama.Validate(c,
                "{\"candidateId\":\"P2\",\"reasonCodes\":[\"PLAN_VERIFIED\",\"LOAD_VERIFIED\"]}") with
                { RunId = Guid.NewGuid().ToString("N"), InputSha256 = c.InputSha256, Model = "selfcheck-fake-not-real-model" });
            var f = NewFixture(model, planReview: true); var w = await f.Store.LoadAsync();
            var c = w.Cases.Single(c => c.FamilyId == "plan_overloaded");
            w = await f.Store.CompareAsync(c.CaseId, c.InputSha256);
            var compared = Item(w, c.CaseId);
            Require(compared.Ai.Status == "valid" && compared.Ai.RawResponse is not null
                && compared.Ai.StructureValid == true && compared.Ai.FactsValid == true
                && compared.Ai.PlanSha256 == c.Candidates[1].Plan!.PlanSha256 && compared.Review is null
                && SameDecision(compared.Engine, c.Engine), "원응답·검증·규칙·사람 승인을 분리해야 합니다.");
            await f.Store.RecordAsync(Answer(w, compared));
            var renewed = await f.Store.CompareAsync(c.CaseId, c.InputSha256);
            Require(!Item(renewed, c.CaseId).ReviewCurrent && renewed.TrainingEligibleCount == 0
                && (await f.Store.ExportApprovedAsync()).ApprovedRows == 0,
                "저장된 승인 이후 모델 실행이 달라지면 새 사람 검토 전 내보내기를 막아야 합니다.");
            var malformed = NewFixture(new StubModel(x => Decision(x, "P2")), planReview: true);
            var item = (await malformed.Store.LoadAsync()).Cases.Single(x => x.FamilyId == "plan_overloaded");
            var bad = Item(await malformed.Store.CompareAsync(item.CaseId, item.InputSha256), item.CaseId);
            Require(bad.Ai.Status == "invalid" && bad.Ai.CandidateId is null, "원응답 없는 계획 선택 주장을 신뢰하면 안 됩니다.");
        });

        await Check("original-sources-remain-unchanged", () =>
        {
            Require(originals.All(entry => CargoTrainingStore.FileHash(Path.Combine(root, entry.Key)) == entry.Value), "시험은 운영 원본이나 소스 파일을 바꾸면 안 됩니다.");
            return Task.CompletedTask;
        });

        var report = new
        {
            schema = "cargo-regional-training-selfchecks.v1", success = results.All(x => x.Passed),
            count = results.Count, passed = results.Count(x => x.Passed), failed = results.Count(x => !x.Passed),
            failures = results.Where(x => !x.Passed).ToArray(), tests = results, directory,
            approvalsAreAutomatedTestOnly = true, actualHumanApprovals = 0, modelNetworkCalls = 0,
            operationalDatabaseUsed = false, weightsTrained = false,
            sourceFilesChecked = originals.Count
        };
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(report, CargoTrainingStore.Json), Encoding.UTF8);
        if (!report.success) Environment.ExitCode = 1;
        return report;
    }

    private static CargoTrainingCase Item(CargoTrainingWorkspace w, string id) => w.Cases.Single(c => c.CaseId == id);
    private static bool IsHash(string value) => Regex.IsMatch(value, "^[0-9a-f]{64}$");
    private static bool SameDecision(CargoTrainingDecision a, CargoTrainingDecision b)
        => JsonSerializer.Serialize(a, CargoTrainingStore.Json) == JsonSerializer.Serialize(b, CargoTrainingStore.Json);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static CargoTrainingReviewCommand Answer(CargoTrainingWorkspace w, CargoTrainingCase c) => new()
    {
        RequestId = Guid.NewGuid().ToString(), DatasetSha256 = w.DatasetSha256, CaseId = c.CaseId,
        InputSha256 = c.InputSha256, ExpectedRevision = c.ReviewRevision, ObservedModelRunId = c.Ai.RunId,
        PlanSetSha256 = c.PlanSetSha256,
        Outcome = "approve", AcceptableCandidateIds = c.Candidates.Where(CargoTrainingPlanCases.IsSelectable).Take(1).Select(d => d.Id).ToList(),
        AcceptHold = !c.Candidates.Any(CargoTrainingPlanCases.IsSelectable), Note = "SELF_CHECK_ONLY: 자동 시험 사본의 모의 승인. 실제 사람의 승인이 아님."
    };
    private static CargoTrainingDecision Decision(CargoTrainingCase c, string id) => new()
    {
        Status = "valid", Source = "selfcheck_fake", CandidateId = id, ReasonCodes = ["REVIEW_NEEDED"],
        RunId = Guid.NewGuid().ToString("N"), InputSha256 = c.InputSha256, Model = "selfcheck-fake-not-real-model"
    };
    private static async Task Reject(Func<Task> action, string code)
    {
        try { await action(); }
        catch (CargoTrainingReviewException e) when (e.Code == code) { return; }
        throw new InvalidOperationException("예상 거절 코드: " + code);
    }
    private static async Task MustReject(Func<Task> action, string message)
    {
        try { await action(); }
        catch (Exception e) when (e is CargoTrainingReviewException or InvalidDataException or JsonException) { return; }
        throw new InvalidOperationException(message);
    }
    private sealed record Fixture(string Repo, string Output, CargoTrainingStore Store);
    private sealed record CheckResult(string Name, bool Passed, string Detail);
    private sealed class StubModel(Func<CargoTrainingCase, CargoTrainingDecision> respond) : ICargoTrainingModel
    {
        public int Calls { get; private set; }
        public Task<CargoTrainingDecision> CompareAsync(CargoTrainingCase item, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls++;
            return Task.FromResult(respond(item));
        }
    }
}
