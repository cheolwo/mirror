using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;

namespace Ssalddel.DispatchTraining.Experiences;

/// <summary>격리된 가상 JSON으로 경험 접수·조회·사람 검토만 검사합니다. 서버·DB·모델은 호출하지 않습니다.</summary>
public static class DispatchExperienceSelfChecks
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<DispatchExperienceSelfCheckReport> RunAsync(string root, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Path.GetFullPath(root), "experience-self-checks",
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        List<DispatchExperienceSelfCheckResult> checks = [];

        await Check("empty-corpus-has-no-completed-work-or-training", async () =>
        {
            var f = Fixture("empty");
            var summary = await f.Corpus.ReadAsync(cancellationToken);
            Require(summary.RunCount == 0 && summary.SnapshotCount == 0 && summary.Orders.Count == 0,
                "빈 누적 기록은 주문이나 완료 결과를 만들면 안 됩니다.");
            AssertScope(summary);
        });
        await Check("import-preserves-original-bytes-and-source-copy", async () =>
        {
            var f = Fixture("source");
            var state = State();
            await Save(f.Source, state);
            var before = await File.ReadAllBytesAsync(f.Source, cancellationToken);
            var imported = await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var after = await File.ReadAllBytesAsync(f.Source, cancellationToken);
            Require(before.AsSpan().SequenceEqual(after), "접수기는 원본 session을 수정하면 안 됩니다.");
            Require(imported.SnapshotSha256.Equals(Hash(before), StringComparison.OrdinalIgnoreCase),
                "접수 해시는 실제 원문 바이트에 결속해야 합니다.");
            var preserved = await File.ReadAllBytesAsync(SnapshotFile(f, state, "source.json"), cancellationToken);
            Require(before.AsSpan().SequenceEqual(preserved),
                "동결 원문 사본이 입력과 같아야 합니다.");
            var summary = await f.Corpus.ReadAsync(cancellationToken);
            Require(summary.Orders.Count == 2 && summary.Orders.All(x => x.RunId == state.RunId),
                "아직 접수하지 않은 시간 슬롯을 실제 경험 주문으로 집계하면 안 됩니다.");
            Require(summary.TotalSlotCount == 15 && summary.ScheduledSlotCount == 13 && summary.SkippedSlotCount == 0,
                "회차의 전체·예약·생략 슬롯 수는 실제 경험 건수와 구분해야 합니다.");
        });
        await Check("replay-and-restart-do-not-duplicate-orders", async () =>
        {
            var f = Fixture("replay");
            await Save(f.Source, State());
            var first = await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var replay = await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var restarted = new DispatchExperienceCorpus(f.CorpusRoot);
            var replayAfterRestart = await restarted.ImportAsync(f.Source, cancellationToken);
            var summary = await restarted.ReadAsync(cancellationToken);
            Require(!first.Reused && replay.Reused && replayAfterRestart.Reused && first.SnapshotSha256 == replay.SnapshotSha256,
                "같은 입력은 재시작 뒤에도 같은 snapshot을 재사용해야 합니다.");
            Require(summary.SnapshotCount == 1 && summary.RunCount == 1 && summary.Orders.Count == 2,
                "반복 접수를 새 주문·회차로 누적하면 안 됩니다.");
        });
        await Check("latest-revision-wins-and-old-import-never-rolls-back", async () =>
        {
            var f = Fixture("revisions");
            var old = State();
            await Save(f.Source, old);
            var first = await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var latest = State(old.RunId!, revision: 2, observedState: "Completed");
            await Save(f.Source, latest);
            var second = await f.Corpus.ImportAsync(f.Source, cancellationToken);
            await Save(f.Source, old);
            Require((await f.Corpus.ImportAsync(f.Source, cancellationToken)).Reused, "이전 원문은 기존 보관본을 재사용해야 합니다.");
            var summary = await f.Corpus.ReadAsync(cancellationToken);
            Require(summary.SnapshotCount == 2 && summary.RunCount == 1 && summary.Orders.Count == 2
                && summary.Orders.All(x => x.Revision == 2 && x.SnapshotSha256 == second.SnapshotSha256)
                && first.SnapshotSha256 != second.SnapshotSha256,
                "과거 판본 재접수는 최신 주문 관측을 되돌리거나 같은 주문을 중복 집계하면 안 됩니다.");
        });
        await Check("same-run-revision-with-different-source-is-rejected", async () =>
        {
            var f = Fixture("source-conflict");
            var state = State();
            await Save(f.Source, state);
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            state.LastObservation = Observation(state, "Completed");
            await Save(f.Source, state);
            await Reject(() => f.Corpus.ImportAsync(f.Source, cancellationToken), "SourceRevisionConflict");
            Require((await f.Corpus.ReadAsync(cancellationToken)).SnapshotCount == 1,
                "같은 회차·판본의 다른 원문을 두 번째 사실로 저장하면 안 됩니다.");
        });
        await Check("independent-runs-accumulate-without-merging-order-identities", async () =>
        {
            var f = Fixture("multiple-runs");
            var first = State();
            await Save(f.Source, first); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var second = State();
            await Save(f.Source, second); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var summary = await f.Corpus.ReadAsync(cancellationToken);
            Require(summary.RunCount == 2 && summary.SnapshotCount == 2 && summary.Orders.Count == 4
                && summary.Orders.Select(x => x.ExperienceId).Distinct(StringComparer.Ordinal).Count() == 4,
                "같은 가상 정본 ID 표기를 쓴 별개 회차도 회차·요청에 따라 다른 경험으로 누적해야 합니다.");
            Require(summary.TotalSlotCount == 30 && summary.ScheduledSlotCount == 26,
                "독립 회차의 슬롯 집계와 실제 경험 누적을 구분해야 합니다.");
        });
        await Check("observation-index-link-mismatch-is-rejected", async () =>
        {
            var f = Fixture("wrong-index"); var state = State();
            state.LastObservation = state.LastObservation with
            { Orders = [state.LastObservation.Orders[0] with { Index = 999 }] };
            await Save(f.Source, state);
            await Reject(() => f.Corpus.ImportAsync(f.Source, cancellationToken), "ObservationIndexMismatch");
        });
        await Check("observation-request-link-mismatch-is-rejected", async () =>
        {
            var f = Fixture("wrong-request"); var state = State();
            state.LastObservation = state.LastObservation with
            { Orders = [state.LastObservation.Orders[0] with { ClientRequestId = Guid.NewGuid() }] };
            await Save(f.Source, state);
            await Reject(() => f.Corpus.ImportAsync(f.Source, cancellationToken), "ObservationRequestMismatch");
        });
        await Check("observation-canonical-link-mismatch-is-rejected", async () =>
        {
            var f = Fixture("wrong-canonical"); var state = State();
            state.LastObservation = state.LastObservation with
            { Orders = [state.LastObservation.Orders[0] with { CanonicalOrderId = "synthetic-other-order" }] };
            await Save(f.Source, state);
            await Reject(() => f.Corpus.ImportAsync(f.Source, cancellationToken), "ObservationCanonicalMismatch");
        });
        await Check("failed-canonical-read-and-completed-phase-do-not-prove-delivery", async () =>
        {
            var f = Fixture("failed-read"); var state = State(observedState: "Completed");
            state.PhaseCode = DispatchTrainingPhaseCodes.Completed;
            state.LastObservation = state.LastObservation with { CanonicalReadSucceeded = false };
            await Save(f.Source, state);
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var orders = (await f.Corpus.ReadAsync(cancellationToken)).Orders;
            Require(orders.Count == 2 && orders.All(x => !x.CanonicalReadSucceeded && x.ObservedStateCode == "Unknown"),
                "조회 실패 시 보존된 Completed 관측과 회차 종료를 전달 완료로 확정하면 안 됩니다.");
            Require(orders.All(x => x.MissingEvidence.Count > 0), "미확인 근거를 결과에서 확인할 수 있어야 합니다.");
        });
        await Check("attempted-unconfirmed-orders-remain-while-unused-slots-stay-separate", async () =>
        {
            var f = Fixture("attempted-slots"); var state = State();
            state.Slots[2].StatusCode = "Unknown";
            state.Slots[3].StatusCode = "Submitting";
            state.Slots[4].StatusCode = "FailedReview";
            state.Slots[5].StatusCode = "Skipped";
            await Save(f.Source, state); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var summary = await f.Corpus.ReadAsync(cancellationToken);
            Require(summary.Orders.Count == 5 && summary.Orders.Count(x => x.CanonicalOrderId is null) == 3,
                "정본 ID가 없는 접수 시도도 미확정 경험으로 보존해야 합니다.");
            Require(summary.TotalSlotCount == 15 && summary.ScheduledSlotCount == 9 && summary.SkippedSlotCount == 1,
                "예약·생략은 경험 누락이 아니라 별도 슬롯 집계여야 합니다.");
            Require(summary.Orders.Where(x => x.OrderIndex >= 2).All(x => !x.CanonicalReadSucceeded && x.ObservedStateCode == "Unknown"),
                "접수 시도만으로 업무 결과를 추정하면 안 됩니다.");
        });
        await Check("completed-observation-without-canonical-order-link-remains-unknown", async () =>
        {
            var f = Fixture("missing-canonical"); var state = State(observedState: "Completed");
            state.Slots[0].StatusCode = "Unknown"; state.Slots[0].CanonicalOrderId = null;
            state.LastObservation = Observation(state, "Completed");
            await Save(f.Source, state); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var order = (await f.Corpus.ReadAsync(cancellationToken)).Orders.Single(x => x.OrderIndex == 0);
            Require(!order.CanonicalReadSucceeded && order.ObservedStateCode == "Unknown"
                && order.MissingEvidence.Contains("CanonicalOrderLink", StringComparer.Ordinal),
                "정본 연결 없이 표시된 완료 상태는 실제 전달 근거가 아닙니다.");
        });
        await Check("unlinked-decisions-incidents-and-events-remain-counted", async () =>
        {
            var f = Fixture("unlinked"); var state = State();
            state.Decisions =
            [
                new() { DecisionId = "linked", OrderId = state.Slots[0].CanonicalOrderId, RecordedAtUtc = state.StartedAtUtc!.Value,
                    RuleStatusCode = "Selected", Summary = "가상 연결 판단" },
                new() { DecisionId = "unlinked", OrderId = "synthetic-unlinked-order", RecordedAtUtc = state.StartedAtUtc.Value,
                    RuleStatusCode = "NoCandidate", Summary = "가상 미연결 판단" }
            ];
            state.Incidents = [new() { IncidentId = "unlinked-incident", OrderId = "synthetic-unlinked-order",
                KindCode = "TestOnly", StatusCode = "Pending", Message = "가상 미연결", RecordedAtUtc = state.StartedAtUtc.Value }];
            state.Events.Add(new(3, state.StartedAtUtc.Value, "SyntheticUnlinked", DetailCode: "test-only"));
            state.EventSequence = 3;
            await Save(f.Source, state);
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var summary = await f.Corpus.ReadAsync(cancellationToken);
            Require(summary.UnlinkedDecisionCount == 1 && summary.UnlinkedIncidentCount == 1 && summary.UnlinkedEventCount == 1,
                "주문 연결이 없는 판단·문제·사건은 삭제하거나 임의 주문에 배정하면 안 됩니다.");
            Require(summary.Orders.Sum(x => x.Decisions.Count) == 1
                && summary.Orders.Single(x => x.OrderIndex == 0).Decisions.Single().DecisionId == "linked",
                "정본 주문 ID가 일치하는 판단만 연결해야 합니다.");
        });
        await Check("historical-events-with-reused-index-remain-unlinked-without-resetting-source", async () =>
        {
            var f = Fixture("historical-events"); var state = State();
            var oldRequest = DispatchTrainingScenario.OrderRequestId("dispatch-training-previous-run", 0);
            var start = state.StartedAtUtc!.Value;
            state.Events.Add(new(3, start.AddDays(-1), "OrderSubmissionConfirmed", 0, oldRequest));
            state.Events.Add(new(4, start, "SyntheticWithoutRequest", 0));
            state.Events.Add(new(5, start.AddMinutes(3), "SyntheticCurrentOrder", 0, state.Slots[0].ClientRequestId));
            state.EventSequence = 5;
            await Save(f.Source, state);
            var original = await File.ReadAllBytesAsync(f.Source, cancellationToken);
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var summary = await f.Corpus.ReadAsync(cancellationToken);
            var order = summary.Orders.Single(x => x.OrderIndex == 0);
            Require(summary.UnlinkedEventCount == 2 && summary.Orders.Sum(x => x.Events.Count) == 3,
                "같은 순번의 과거 회차 사건과 요청 없는 사건은 손상이 아니라 미연결 이력으로 보존해야 합니다.");
            Require(order.Events.Select(x => x.Sequence).SequenceEqual(new long[] { 1, 5 })
                && order.Events.All(x => x.ClientRequestId == state.Slots[0].ClientRequestId),
                "순번만으로 사건을 연결하지 않고 현재 요청 식별자도 일치해야 합니다.");
            var preserved = await File.ReadAllBytesAsync(SnapshotFile(f, state, "source.json"), cancellationToken);
            Require(original.AsSpan().SequenceEqual(preserved)
                && Encoding.UTF8.GetString(preserved).Contains(oldRequest.ToString("D"), StringComparison.Ordinal),
                "미연결 과거 이력을 제거하거나 원문을 현재 회차만으로 초기화하면 안 됩니다.");
        });
        await Check("malformed-source-is-rejected-without-partial-import", async () =>
        {
            var f = Fixture("malformed");
            await File.WriteAllTextAsync(f.Source, "{\"schemaVersion\":", cancellationToken);
            await Reject(() => f.Corpus.ImportAsync(f.Source, cancellationToken));
            Require((await f.Corpus.ReadAsync(cancellationToken)).SnapshotCount == 0, "손상 원문을 부분 완료로 접수하면 안 됩니다.");
        });
        await Check("persisted-source-tampering-fails-closed", async () =>
        {
            var f = Fixture("source-tamper"); var state = State();
            await Save(f.Source, state); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var path = SnapshotFile(f, state, "source.json");
            await File.AppendAllTextAsync(path, " ", cancellationToken);
            await Reject(() => new DispatchExperienceCorpus(f.CorpusRoot).ReadAsync(cancellationToken));
            Require((await File.ReadAllTextAsync(path, cancellationToken)).EndsWith(' '), "손상 근거를 자동 초기화하면 안 됩니다.");
        });
        await Check("persisted-order-projection-tampering-fails-closed", async () =>
        {
            var f = Fixture("projection-tamper"); var state = State();
            await Save(f.Source, state); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            await File.AppendAllTextAsync(SnapshotFile(f, state, "orders.json"), " ", cancellationToken);
            await Reject(() => new DispatchExperienceCorpus(f.CorpusRoot).ReadAsync(cancellationToken));
        });
        await Check("review-replay-restart-and-payload-conflict", async () =>
        {
            var f = Fixture("review-replay"); await Save(f.Source, State());
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var order = (await f.Corpus.ReadAsync(cancellationToken)).Orders.First();
            var command = Answer(order);
            var first = await f.Corpus.ReviewAsync(command, cancellationToken);
            var replay = await new DispatchExperienceCorpus(f.CorpusRoot).ReviewAsync(command, cancellationToken);
            Require(first.ReviewRevision == 1 && !first.Reused && replay.Reused && replay.ReviewRevision == 1,
                "같은 검토 요청은 재시작 후에도 한 번만 기록해야 합니다.");
            await Reject(() => f.Corpus.ReviewAsync(command with { Note = "다른 내용" }, cancellationToken), "RequestConflict");
            var latest = (await f.Corpus.ReadAsync(cancellationToken)).Orders.Single(x => x.ExperienceId == order.ExperienceId);
            Require(latest.ReviewRevision == 1 && latest.ReviewCurrent && latest.ReviewOutcome == "approve",
                "충돌한 재전송은 기존 사람 의견을 바꾸면 안 됩니다.");
        });
        await Check("stale-review-revision-and-unknown-snapshot-are-rejected", async () =>
        {
            var f = Fixture("review-conflicts"); await Save(f.Source, State());
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var order = (await f.Corpus.ReadAsync(cancellationToken)).Orders.First();
            await f.Corpus.ReviewAsync(Answer(order), cancellationToken);
            await Reject(() => f.Corpus.ReviewAsync(Answer(order), cancellationToken), "RevisionConflict");
            await Reject(() => f.Corpus.ReviewAsync(Answer(order) with
                { ExpectedReviewRevision = 1, SnapshotSha256 = new string('0', 64) }, cancellationToken), "SnapshotChanged");
        });
        await Check("invalid-human-review-commands-never-append", async () =>
        {
            var f = Fixture("invalid-reviews"); await Save(f.Source, State());
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var order = (await f.Corpus.ReadAsync(cancellationToken)).Orders.First();
            foreach (var invalid in new[]
            {
                Answer(order) with { RequestId = Guid.Empty },
                Answer(order) with { Outcome = "automatic-approval" },
                Answer(order) with { ExpectedReviewRevision = -1 },
                Answer(order) with { Note = new string('x', 1001) }
            }) await Reject(() => f.Corpus.ReviewAsync(invalid, cancellationToken), "InvalidReviewCommand");
            Require((await f.Corpus.ReadAsync(cancellationToken)).Orders.All(x => x.ReviewRevision == 0 && !x.ReviewCurrent),
                "잘못된 사람 검토 명령은 의견·승인을 추가하면 안 됩니다.");
        });
        await Check("explicit-defer-supersedes-old-approval-without-enabling-training", async () =>
        {
            var f = Fixture("defer"); await Save(f.Source, State());
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var order = (await f.Corpus.ReadAsync(cancellationToken)).Orders.First();
            await f.Corpus.ReviewAsync(Answer(order), cancellationToken);
            var current = (await f.Corpus.ReadAsync(cancellationToken)).Orders.Single(x => x.ExperienceId == order.ExperienceId);
            await f.Corpus.ReviewAsync(Answer(current) with { Outcome = "defer" }, cancellationToken);
            var deferred = (await f.Corpus.ReadAsync(cancellationToken)).Orders.Single(x => x.ExperienceId == order.ExperienceId);
            Require(deferred.ReviewRevision == 2 && deferred.ReviewCurrent && deferred.ReviewOutcome == "defer"
                && !deferred.TrainingEligible, "최신 보류 의견을 과거 승인으로 표시하거나 학습 적격으로 바꾸면 안 됩니다.");
        });
        await Check("new-snapshot-invalidates-opinion-without-deleting-history", async () =>
        {
            var f = Fixture("review-snapshot"); var state = State();
            await Save(f.Source, state); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var old = (await f.Corpus.ReadAsync(cancellationToken)).Orders.First();
            await f.Corpus.ReviewAsync(Answer(old), cancellationToken);
            var reviewFiles = Directory.GetFiles(Path.Combine(f.CorpusRoot, "reviews"), "*.json");
            var hashes = reviewFiles.ToDictionary(x => x, x => Hash(File.ReadAllBytes(x)), StringComparer.OrdinalIgnoreCase);
            await Save(f.Source, State(state.RunId!, revision: 2, observedState: "Completed"));
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var latest = (await f.Corpus.ReadAsync(cancellationToken)).Orders.Single(x => x.ExperienceId == old.ExperienceId);
            Require(!latest.ReviewCurrent && latest.ReviewRevision == 1 && latest.ReviewOutcome == "approve"
                && latest.ReviewSnapshotSha256 == old.SnapshotSha256 && latest.SnapshotSha256 != old.SnapshotSha256,
                "새 관측은 과거 의견을 보존하되 현재 승인으로 승계하면 안 됩니다.");
            Require(hashes.All(x => File.Exists(x.Key) && Hash(File.ReadAllBytes(x.Key)) == x.Value),
                "새 snapshot 접수가 과거 검토 사건을 수정하면 안 됩니다.");
            await Reject(() => f.Corpus.ReviewAsync(Answer(old) with { ExpectedReviewRevision = 1 }, cancellationToken), "SnapshotChanged");
            var next = await f.Corpus.ReviewAsync(Answer(latest) with { Outcome = "correct" }, cancellationToken);
            Require(next.ReviewRevision == 2 && !next.Reused, "현재 판본의 명시적 정정은 별도 검토 revision으로 기록해야 합니다.");
        });
        await Check("two-instances-allow-one-review-cas-winner", async () =>
        {
            var f = Fixture("concurrent"); await Save(f.Source, State());
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var order = (await f.Corpus.ReadAsync(cancellationToken)).Orders.First();
            var second = new DispatchExperienceCorpus(f.CorpusRoot);
            var results = await Task.WhenAll(Try(f.Corpus, Answer(order)), Try(second, Answer(order)));
            Require(results.Count(x => x == "saved") == 1 && results.Count(x => x == "rejected") == 1,
                "두 인스턴스의 같은 검토 revision 작성은 하나만 성공해야 합니다.");
            Require((await second.ReadAsync(cancellationToken)).Orders.Single(x => x.ExperienceId == order.ExperienceId).ReviewRevision == 1,
                "경쟁한 검토를 두 번 저장하면 안 됩니다.");

            async Task<string> Try(DispatchExperienceCorpus corpus, DispatchExperienceReviewCommand command)
            {
                try { await corpus.ReviewAsync(command, cancellationToken); return "saved"; }
                catch (DispatchExperienceException error) when (error.Code == "RevisionConflict") { return "rejected"; }
            }
        });
        await Check("report-publication-holds-corpus-lock-until-review-and-import-can-follow", async () =>
        {
            var f = Fixture("publication-lock"); var state = State();
            await Save(f.Source, state); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var other = new DispatchExperienceCorpus(f.CorpusRoot);
            var order = (await f.Corpus.ReadAsync(cancellationToken)).Orders.First();
            await VerifyPublicationBlocks(async () => { await other.ReviewAsync(Answer(order), cancellationToken); });
            Require((await f.Corpus.ReadAsync(cancellationToken)).Orders.Single(x => x.ExperienceId == order.ExperienceId).ReviewRevision == 1,
                "보고서 게시 뒤 대기하던 사람 검토가 정상 진행해야 합니다.");
            await Save(f.Source, State(state.RunId!, revision: 2, observedState: "Completed"));
            await VerifyPublicationBlocks(async () => { await other.ImportAsync(f.Source, cancellationToken); });
            var current = (await f.Corpus.ReadAsync(cancellationToken)).Orders.Single(x => x.ExperienceId == order.ExperienceId);
            Require(current.Revision == 2 && !current.ReviewCurrent && current.ReviewRevision == 1,
                "보고서 게시 뒤 새 관측을 접수하고 과거 의견의 유효성도 다시 판정해야 합니다.");

            async Task VerifyPublicationBlocks(Func<Task> mutate)
            {
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var publication = f.Corpus.ReadAndPublishAsync(async (_, token) =>
                {
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(token);
                }, cancellationToken);
                Task? mutation = null;
                try
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
                    mutation = mutate();
                    await Task.Delay(100, cancellationToken);
                    Require(!mutation.IsCompleted, "보고서 게시 콜백이 실행 중일 때 다른 인스턴스가 검토·접수를 확정하면 안 됩니다.");
                }
                finally
                {
                    release.TrySetResult(true);
                    await publication;
                    if (mutation is not null) await mutation;
                }
            }
        });
        await Check("persisted-human-review-tampering-fails-closed", async () =>
        {
            var f = Fixture("review-tamper"); await Save(f.Source, State());
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var order = (await f.Corpus.ReadAsync(cancellationToken)).Orders.First();
            await f.Corpus.ReviewAsync(Answer(order), cancellationToken);
            var path = Directory.GetFiles(Path.Combine(f.CorpusRoot, "reviews"), "*.json").Single();
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            Require(text.Contains("approve", StringComparison.Ordinal), "검토 변조 fixture가 실제 승인 사건을 찾아야 합니다.");
            await File.WriteAllTextAsync(path, text.Replace("approve", "correct", StringComparison.Ordinal), cancellationToken);
            await Reject(() => new DispatchExperienceCorpus(f.CorpusRoot).ReadAsync(cancellationToken));
        });
        await Check("explicit-review-never-enables-training-or-operational-dispatch", async () =>
        {
            var f = Fixture("scope"); await Save(f.Source, State(observedState: "Completed"));
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var orders = (await f.Corpus.ReadAsync(cancellationToken)).Orders;
            Require(orders.All(x => x.CanonicalReadSucceeded && x.ObservedStateCode == "Completed"),
                "정상 정본 조회로 확인한 완료와 미확인 결과는 구분해야 합니다.");
            foreach (var order in orders) await f.Corpus.ReviewAsync(Answer(order), cancellationToken);
            AssertScope(await f.Corpus.ReadAsync(cancellationToken));
        });
        await Check("pending-temporary-files-are-not-imported-or-counted", async () =>
        {
            var f = Fixture("temporary"); var state = State();
            await Save(f.Source, state); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var pending = Path.Combine(f.CorpusRoot, "runs", Hash(Encoding.UTF8.GetBytes(state.RunId!)), "99.pending.tmp");
            await File.WriteAllTextAsync(pending, "{incomplete", cancellationToken);
            var pendingReview = Path.Combine(f.CorpusRoot, ".pending-review-" + Guid.NewGuid().ToString("N") + ".tmp");
            await File.WriteAllTextAsync(pendingReview, "{incomplete", cancellationToken);
            var pendingImport = Path.Combine(f.CorpusRoot, ".pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pendingImport);
            await File.WriteAllTextAsync(Path.Combine(pendingImport, "source.json"), "{incomplete", cancellationToken);
            var summary = await new DispatchExperienceCorpus(f.CorpusRoot).ReadAsync(cancellationToken);
            Require(summary.SnapshotCount == 1 && summary.Orders.Count == 2 && File.Exists(pending)
                && File.Exists(pendingReview) && Directory.Exists(pendingImport),
                "미확정 임시 파일은 정본으로 읽거나 자동 삭제하면 안 됩니다.");
        });

        await Check("representative-packet-preserves-case-numbers-across-new-observations", async () =>
        {
            var f = Fixture("review-packet"); var state = State();
            await Save(f.Source, state); await f.Corpus.ImportAsync(f.Source, cancellationToken);
            await Publish();
            var path = Directory.GetFiles(Path.Combine(f.CorpusRoot, "reports"), "review-*.json").Single();
            var before = await File.ReadAllBytesAsync(path, cancellationToken);
            using (var packet = JsonDocument.Parse(before))
            {
                var source = packet.RootElement.GetProperty("sourceSummaryReport").GetString()!;
                Require(File.Exists(Path.Combine(f.CorpusRoot, "reports", source)), "검토 목록의 주문 근거 판본이 있어야 합니다.");
                Require(!packet.RootElement.GetProperty("selection").GetProperty("humanApprovalCreated").GetBoolean(),
                    "고정 검토 목록을 만들면서 사람 승인을 생성하면 안 됩니다.");
            }
            await Publish();
            Require(Directory.GetFiles(Path.Combine(f.CorpusRoot, "reports"), "review-*.json").Length == 1,
                "같은 자료의 재발행은 고정 검토 목록을 중복 생성하면 안 됩니다.");
            await Save(f.Source, State(state.RunId, 2, "Completed"));
            await f.Corpus.ImportAsync(f.Source, cancellationToken); await Publish();
            var preserved = await File.ReadAllBytesAsync(path, cancellationToken);
            Require(Directory.GetFiles(Path.Combine(f.CorpusRoot, "reports"), "review-*.json").Length == 2
                && before.AsSpan().SequenceEqual(preserved),
                "새 관측으로 대표 번호가 바뀌어도 과거 고정 목록은 유지해야 합니다.");
            async Task Publish() => await f.Corpus.ReadAndPublishAsync(
                (summary, token) => DispatchExperienceReport.WriteAsync(f.CorpusRoot, summary, token), cancellationToken);
        });
        await Check("representative-packet-tampering-is-rejected-without-overwrite", async () =>
        {
            var f = Fixture("review-packet-tamper"); await Save(f.Source, State());
            await f.Corpus.ImportAsync(f.Source, cancellationToken);
            var summary = await f.Corpus.ReadAsync(cancellationToken);
            await DispatchExperienceReport.WriteAsync(f.CorpusRoot, summary, cancellationToken);
            var path = Directory.GetFiles(Path.Combine(f.CorpusRoot, "reports"), "review-*.json").Single();
            await File.WriteAllTextAsync(path, "{tampered", cancellationToken);
            await Reject(async () => { await DispatchExperienceReport.WriteAsync(f.CorpusRoot, summary, cancellationToken); return true; },
                "ReviewPacketSnapshotCorrupt");
            Require(await File.ReadAllTextAsync(path, cancellationToken) == "{tampered",
                "손상된 과거 검토 자료를 조용히 덮어쓰면 안 됩니다.");
        });

        return new(directory, checks);

        FixtureFiles Fixture(string name)
        {
            var location = Path.Combine(directory, name);
            Directory.CreateDirectory(location);
            var corpusRoot = Path.Combine(location, "corpus");
            return new(location, Path.Combine(location, "source.json"), corpusRoot, new(corpusRoot));
        }

        async Task Save(string path, DispatchTrainingSessionState state)
            => await File.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(state, Json), cancellationToken);

        async Task Check(string name, Func<Task> action)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { await action(); checks.Add(new(name, true, null)); }
            catch (Exception error) when (error is not OutOfMemoryException && !cancellationToken.IsCancellationRequested)
            { checks.Add(new(name, false, error.Message)); }
        }
    }

    private static DispatchTrainingSessionState State(string? runId = null, long revision = 1, string observedState = "Waiting")
    {
        runId ??= "dispatch-training-" + Guid.NewGuid().ToString("N");
        var start = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
        var state = new DispatchTrainingSessionState
        {
            RunId = runId, Revision = revision, PhaseCode = DispatchTrainingPhaseCodes.Running,
            StartedAtUtc = start, GenerationEndsAtUtc = start.AddSeconds(DispatchTrainingScenario.DurationSeconds), EventSequence = 2,
            Slots = Enumerable.Range(0, DispatchTrainingScenario.MaximumOrders).Select(index => new DispatchTrainingOrderSlot
            {
                Index = index, DueAtUtc = DispatchTrainingScenario.DueAt(start, index),
                ClientRequestId = DispatchTrainingScenario.OrderRequestId(runId, index),
                StatusCode = index < 2 ? "Confirmed" : "Scheduled",
                CanonicalOrderId = index < 2 ? "synthetic-order-" + index : null
            }).ToList(),
            Events =
            [
                new(1, start, "OrderSubmissionConfirmed", 0, DispatchTrainingScenario.OrderRequestId(runId, 0)),
                new(2, start.AddMinutes(2), "OrderSubmissionConfirmed", 1, DispatchTrainingScenario.OrderRequestId(runId, 1))
            ]
        };
        state.LastObservation = Observation(state, observedState);
        return state;
    }

    private static DispatchTrainingTickResult Observation(DispatchTrainingSessionState state, string code) => new()
    {
        CanonicalReadSucceeded = true,
        WaitingOrders = code == "Waiting" ? 2 : 0,
        CompletedOrders = code == "Completed" ? 2 : 0,
        UnfinishedOrders = code == "Completed" ? 0 : 2,
        Orders = state.Slots.Take(2).Select(slot => new DispatchTrainingOrderObservation
        {
            Index = slot.Index, ClientRequestId = slot.ClientRequestId,
            CanonicalOrderId = slot.CanonicalOrderId, StateCode = code
        }).ToArray()
    };

    private static DispatchExperienceReviewCommand Answer(DispatchExperienceOrder order)
        => new(Guid.NewGuid(), order.ExperienceId, order.SnapshotSha256, order.ReviewRevision, "approve", "격리 가상 사례의 명시적 검토");

    private static string SnapshotFile(FixtureFiles fixture, DispatchTrainingSessionState state, string name)
        => Path.Combine(fixture.CorpusRoot, "runs", Hash(Encoding.UTF8.GetBytes(state.RunId!)),
            state.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), name);

    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));

    private static void AssertScope(DispatchExperienceSummary summary)
    {
        Require(summary.Synthetic && !summary.TrainingEligible && !summary.WeightsTrained && !summary.OperationalDispatchEnabled,
            "경험 접수·사람 검토를 실운영이나 모델 학습으로 승격하면 안 됩니다.");
        Require(summary.Orders.All(x => x.Synthetic && !x.TrainingEligible && !x.WeightsTrained && !x.OperationalDispatchEnabled),
            "개별 경험 역시 합성·학습 제외·운영 OFF를 유지해야 합니다.");
    }

    private static async Task Reject<T>(Func<Task<T>> action, string? expectedCode = null)
    {
        try { await action(); }
        catch (DispatchExperienceException error) when (expectedCode is null || error.Code == expectedCode) { return; }
        catch (InvalidDataException) when (expectedCode is null) { return; }
        catch (JsonException) when (expectedCode is null) { return; }
        throw new InvalidOperationException("거부해야 하는 입력 또는 저장 손상을 허용했습니다.");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed record FixtureFiles(string Directory, string Source, string CorpusRoot, DispatchExperienceCorpus Corpus);
}

public sealed record DispatchExperienceSelfCheckResult(string Name, bool Passed, string? Failure);
public sealed record DispatchExperienceSelfCheckReport(string ArtifactDirectory, IReadOnlyList<DispatchExperienceSelfCheckResult> Checks)
{
    public bool Passed => Checks.All(x => x.Passed);
    public int PassedCount => Checks.Count(x => x.Passed);
    public int FailedCount => Checks.Count(x => !x.Passed);
}
