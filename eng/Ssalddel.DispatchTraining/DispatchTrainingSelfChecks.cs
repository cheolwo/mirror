using Ssalddel.Contracts.Admin.DispatchTraining;

namespace Ssalddel.DispatchTraining;

/// <summary>외부 API·모델·DB를 호출하지 않고 가짜 시계로 실시간 제어의 불변을 검사합니다.</summary>
public static class DispatchTrainingSelfChecks
{
    public static async Task<DispatchTrainingSelfCheckReport> RunAsync(string artifactRoot, CancellationToken ct = default)
    {
        var directory = Path.Combine(Path.GetFullPath(artifactRoot), "self-checks", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        List<DispatchTrainingSelfCheckResult> results = [];
        await Check("thirty-minute-fifteen-orders", async () =>
        {
            var fixture = NewFixture("normal");
            var started = await Start(fixture.Coordinator);
            for (var minute = 0; minute <= 30; minute++)
            {
                if (minute > 0) fixture.Clock.Advance(TimeSpan.FromMinutes(1));
                await fixture.Coordinator.TickAsync(ct);
            }
            var session = await fixture.Coordinator.ReadAsync(ct);
            Require(session.PhaseCode == DispatchTrainingPhaseCodes.Completed, "30분 뒤 기존 업무가 마감되어야 합니다.");
            Require(fixture.Actions.Submissions.Count == 15 && session.Counts.GeneratedOrders == 15, "주문은 정확히 15건이어야 합니다.");
            Require(fixture.Actions.Submissions.Select(x => x.Index).SequenceEqual(Enumerable.Range(0, 15)), "주문 인덱스가 중복되면 안 됩니다.");
            Require(fixture.Actions.Submissions.All(x => x.AtUtc == started.StartedAtUtc!.Value.AddSeconds(x.Index * 120)), "0~28분의 2분 간격이어야 합니다.");
            Require(session.DurationSeconds == 1800 && session.OrderIntervalSeconds == 120 && session.MaximumOrders == 15, "제품 기본 시간은 변경하면 안 됩니다.");
            Require(fixture.Actions.SupportCalls == 0 && session.ActivatedSupportDriverCount == 0, "지원 기사는 자동 참여하면 안 됩니다.");
        });
        await Check("pause-intake-continues-current-work-and-stop-drains", async () =>
        {
            var fixture = NewFixture("pause");
            fixture.Actions.AutoComplete = false;
            await Start(fixture.Coordinator);
            await fixture.Coordinator.TickAsync(ct);
            await Command(fixture.Coordinator, DispatchTrainingCommandCodes.Pause);
            var calls = fixture.Actions.TickCalls;
            fixture.Clock.Advance(TimeSpan.FromMinutes(5));
            await fixture.Coordinator.TickAsync(ct);
            Require(fixture.Actions.Submissions.Count == 1 && fixture.Actions.TickCalls > calls, "일시정지는 기존 업무 확인을 멈추면 안 됩니다.");
            await Command(fixture.Coordinator, DispatchTrainingCommandCodes.Stop);
            var draining = await fixture.Coordinator.TickAsync(ct);
            Require(draining.PhaseCode == DispatchTrainingPhaseCodes.Draining, "종료 버튼은 미완료 업무를 삭제하면 안 됩니다.");
            fixture.Actions.AutoComplete = true;
            var completed = await fixture.Coordinator.TickAsync(ct);
            Require(completed.PhaseCode == DispatchTrainingPhaseCodes.Completed, "기존 업무가 끝나면 마감해야 합니다.");
        });
        await Check("restart-pauses-and-never-backfills", async () =>
        {
            var fixture = NewFixture("restart");
            var started = await Start(fixture.Coordinator);
            await fixture.Coordinator.TickAsync(ct);
            fixture.Clock.Advance(TimeSpan.FromMinutes(7));
            var old = await fixture.Store.LoadAsync(ct) ?? throw new InvalidOperationException();
            var restarted = new DispatchTrainingCoordinator(fixture.Store, fixture.Actions, fixture.Clock);
            await restarted.InitializeAsync(ct);
            var paused = await restarted.TickAsync(ct);
            Require(paused.PhaseCode == DispatchTrainingPhaseCodes.Paused && fixture.Actions.Submissions.Count == 1, "재시작은 자동 접수를 재개하면 안 됩니다.");
            await Command(restarted, DispatchTrainingCommandCodes.Resume);
            await restarted.TickAsync(ct);
            Require(fixture.Actions.Submissions.Count == 1, "중단 중 놓친 주문을 한꺼번에 접수하면 안 됩니다.");
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));
            await restarted.TickAsync(ct);
            Require(fixture.Actions.Submissions.Count == 2 && fixture.Actions.Submissions[^1].Index == 4, "다음 원래 시간 슬롯에서만 재개해야 합니다.");
            var saved = await fixture.Store.LoadAsync(ct) ?? throw new InvalidOperationException();
            Require(saved.EventSequence > old.EventSequence && saved.RunId == started.RunId, "회차·사건 순번을 유지해야 합니다.");
            Require(saved.Events.Zip(saved.Events.Skip(1), (a, b) => b.Sequence > a.Sequence).All(x => x), "사건 순번은 재시작 뒤에도 단조 증가해야 합니다.");
        });
        await Check("late-tick-submits-one-current-slot", async () =>
        {
            var fixture = NewFixture("late");
            await Start(fixture.Coordinator);
            await fixture.Coordinator.TickAsync(ct);
            fixture.Clock.Advance(TimeSpan.FromMinutes(10));
            await fixture.Coordinator.TickAsync(ct);
            Require(fixture.Actions.Submissions.Count == 2 && fixture.Actions.Submissions[^1].Index == 5, "늦은 timer 호출은 주문 묶음 재전송으로 바뀌면 안 됩니다.");
        });
        await Check("unknown-submission-requeries-without-post-retry", async () =>
        {
            var fixture = NewFixture("unknown");
            fixture.Actions.UnknownSubmissions.Add(0);
            fixture.Actions.RevealOrders = false;
            await Start(fixture.Coordinator);
            var unknown = await fixture.Coordinator.TickAsync(ct);
            Require(unknown.Counts.GeneratedOrders == 0 && unknown.PendingIncidents.Any(x => x.KindCode == "OrderSubmissionUnknown"), "불명확한 접수를 성공으로 표시하면 안 됩니다.");
            await fixture.Coordinator.TickAsync(ct);
            Require(fixture.Actions.Submissions.Count == 1, "같은 접수 POST를 반복하면 안 됩니다.");
            await Command(fixture.Coordinator, DispatchTrainingCommandCodes.Stop);
            Require((await fixture.Coordinator.TickAsync(ct)).PhaseCode == DispatchTrainingPhaseCodes.Draining, "불명확한 접수는 자동 마감하면 안 됩니다.");
            fixture.Actions.RevealOrders = true;
            var reconciled = await fixture.Coordinator.TickAsync(ct);
            Require(reconciled.Counts.GeneratedOrders == 1 && reconciled.PhaseCode == DispatchTrainingPhaseCodes.Completed, "정본 재조회로 접수를 복구해야 합니다.");
            Require(fixture.Actions.Submissions.Count == 1, "복구에 새 접수를 사용하면 안 됩니다.");
        });
        await Check("one-failed-task-keeps-other-orders-running", async () =>
        {
            var fixture = NewFixture("failure");
            fixture.Actions.RejectedSubmissions.Add(3);
            await Start(fixture.Coordinator);
            for (var minute = 0; minute <= 30; minute++)
            {
                if (minute > 0) fixture.Clock.Advance(TimeSpan.FromMinutes(1));
                await fixture.Coordinator.TickAsync(ct);
            }
            var session = await fixture.Coordinator.ReadAsync(ct);
            Require(fixture.Actions.Submissions.Count == 15 && session.Counts.GeneratedOrders == 14 && session.Counts.FailedOrders == 1, "접수 한 건의 실패가 다른 일정을 중단하면 안 됩니다.");
            Require(session.PendingIncidents.Any(x => x.KindCode == "OrderSubmissionRejected"), "실패한 접수의 검토 기록을 남겨야 합니다.");
        });
        await Check("unknown-pre-submit-failure-reconciles-not-submitted-without-new-post", async () =>
        {
            var fixture = NewFixture("not-submitted");
            fixture.Actions.NotSubmittedAfterUnknown.Add(0);
            await Start(fixture.Coordinator);
            var unknown = await fixture.Coordinator.TickAsync(ct);
            Require(unknown.PendingIncidents.Any(x => x.KindCode == "OrderSubmissionUnknown"), "응답 유실 직후는 결과 불명확으로 보존해야 합니다.");
            var reconciled = await fixture.Coordinator.TickAsync(ct);
            Require(reconciled.Counts.GeneratedOrders == 0 && reconciled.Counts.FailedOrders == 1
                && reconciled.PendingIncidents.Any(x => x.KindCode == "OrderSubmissionNotSent"), "POST 전 실패 정본은 실패·검토 상태로 대조해야 합니다.");
            Require(fixture.Actions.Submissions.Count == 1, "미전송 확인을 위해 새 주문 POST를 보내면 안 됩니다.");
            fixture.Clock.Advance(TimeSpan.FromMinutes(2));
            await fixture.Coordinator.TickAsync(ct);
            Require(fixture.Actions.Submissions.Count == 2 && fixture.Actions.Submissions[^1].Index == 1,
                "다른 주문 슬롯은 계속 진행해야 합니다.");
            await Command(fixture.Coordinator, DispatchTrainingCommandCodes.Stop);
            var completed = await fixture.Coordinator.TickAsync(ct);
            Require(completed.PhaseCode == DispatchTrainingPhaseCodes.Completed && completed.Counts.FailedOrders == 1,
                "미전송 접수를 영구 Unknown으로 남기지 않고 기존 업무를 마감해야 합니다.");
            var saved = await fixture.Store.LoadAsync(ct) ?? throw new InvalidOperationException();
            Require(saved.Slots[0].StatusCode == "FailedReview" && saved.Slots[0].LastErrorCode == "SubmissionNotSent"
                && saved.Events.Any(x => x.KindCode == "OrderSubmissionUnknown")
                && saved.Events.Any(x => x.KindCode == "OrderSubmissionNotSent")
                && saved.Incidents.Any(x => x.KindCode == "OrderSubmissionUnknown" && x.StatusCode == "Resolved"),
                "기존 불명확 이력과 미전송 확인 근거를 함께 보존해야 합니다.");
        });
        await Check("control-cas-replay-and-support-explicit-only", async () =>
        {
            var fixture = NewFixture("commands");
            var initial = await fixture.Coordinator.ReadAsync(ct);
            var request = Request(initial);
            var first = await fixture.Coordinator.CommandAsync(DispatchTrainingCommandCodes.Start, request, ct);
            var replay = await fixture.Coordinator.CommandAsync(DispatchTrainingCommandCodes.Start, request, ct);
            Require(replay.Replayed && replay.Session.RunId == first.Session.RunId, "같은 제어 요청은 같은 회차를 재사용해야 합니다.");
            await RejectCode(() => fixture.Coordinator.CommandAsync(DispatchTrainingCommandCodes.Stop, request, ct), "ClientRequestIdReused");
            await RejectCode(() => fixture.Coordinator.CommandAsync(DispatchTrainingCommandCodes.Pause,
                request with { ClientRequestId = Guid.NewGuid() }, ct), "RevisionConflict");
            await fixture.Coordinator.TickAsync(ct);
            Require(fixture.Actions.SupportCalls == 0, "timer는 지원 참여를 명령하면 안 됩니다.");
            var current = await fixture.Coordinator.ReadAsync(ct);
            var support = new DispatchTrainingSupportDriversRequest
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Revision, RunId = current.RunId, Count = 1
            };
            var applied = await fixture.Coordinator.SupportDriversAsync(support, ct);
            await fixture.Coordinator.SupportDriversAsync(support, ct);
            Require(fixture.Actions.SupportCalls == 1 && applied.Session.ActivatedSupportDriverCount == 1
                && applied.Session.AppliedCommandIds.Contains(support.ClientRequestId), "지원 참여는 명시 요청 한 번으로만 증가해야 합니다.");
            current = await fixture.Coordinator.ReadAsync(ct);
            await RejectCode(() => fixture.Coordinator.SupportDriversAsync(new()
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Revision, RunId = current.RunId, Count = 2
            }, ct), "SupportDriverCapacityExceeded");
        });
        await Check("unknown-support-reconciles-total-without-reactivation", async () =>
        {
            var fixture = NewFixture("support-unknown");
            fixture.Actions.UnknownSupport = true;
            var started = await Start(fixture.Coordinator);
            var request = new DispatchTrainingSupportDriversRequest
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = started.Revision, RunId = started.RunId, Count = 2
            };
            var unknown = await fixture.Coordinator.SupportDriversAsync(request, ct);
            Require(!unknown.Session.AppliedCommandIds.Contains(request.ClientRequestId), "불명확한 참여를 처리 완료로 표시하면 안 됩니다.");
            await fixture.Coordinator.SupportDriversAsync(request, ct);
            var reconciled = await fixture.Coordinator.TickAsync(ct);
            Require(fixture.Actions.SupportCalls == 1 && reconciled.ActivatedSupportDriverCount == 2
                && reconciled.AppliedCommandIds.Contains(request.ClientRequestId), "전체 활성 수 재조회로 참여 결과를 확인해야 합니다.");
        });
        await Check("canonical-read-failure-preserves-counts-and-recovery", async () =>
        {
            var fixture = NewFixture("read-failure");
            fixture.Actions.AutoComplete = false;
            await Start(fixture.Coordinator);
            await fixture.Coordinator.TickAsync(ct);
            var before = await fixture.Coordinator.TickAsync(ct);
            fixture.Actions.FailRead = true;
            var failed = await fixture.Coordinator.TickAsync(ct);
            Require(failed.Counts.WaitingOrders == before.Counts.WaitingOrders && failed.PendingIncidents.Any(x => x.KindCode == "CanonicalReadUnavailable"), "조회 실패를 빈 업무 목록으로 덮으면 안 됩니다.");
            fixture.Actions.FailRead = false;
            var recovered = await fixture.Coordinator.TickAsync(ct);
            Require(!recovered.PendingIncidents.Any(x => x.KindCode == "CanonicalReadUnavailable"), "정본 복구 뒤 조회 장애를 해소해야 합니다.");
        });
        await Check("atomic-journal-and-corruption-not-reset", async () =>
        {
            var fixture = NewFixture("storage");
            await Start(fixture.Coordinator);
            await fixture.Coordinator.TickAsync(ct);
            var saved = await fixture.Store.LoadAsync(ct) ?? throw new InvalidOperationException();
            Require(saved.Slots[0].ClientRequestId == fixture.Actions.Submissions[0].RequestId, "영속 요청 번호와 전송 요청 번호가 같아야 합니다.");
            Require(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "원자 저장의 임시 파일이 남으면 안 됩니다.");
            var corrupt = Path.Combine(directory, "corrupt.json");
            await File.WriteAllTextAsync(corrupt, "{\"schemaVersion\":999}", ct);
            try { await new DispatchTrainingSessionStore(corrupt).LoadAsync(ct); throw new InvalidOperationException("손상된 기록이 자동 초기화되었습니다."); }
            catch (InvalidDataException) { }
            Require((await File.ReadAllTextAsync(corrupt, ct)).Contains("999"), "손상된 기록을 보존해야 합니다.");
        });
        await Check("archive-rejects-running-host-lease", async () =>
        {
            var fixture = NewFixture("archive-locked");
            await Start(fixture.Coordinator);
            var before = await File.ReadAllTextAsync(fixture.Store.JournalPath, ct);
            using (DispatchTrainingArchive.AcquireHostLease(fixture.Store.JournalPath))
            {
                var rejected = false;
                try { await DispatchTrainingArchive.ArchiveAsync(fixture.Store.JournalPath, ct: ct, hostPort: 0); }
                catch (IOException) { rejected = true; }
                Require(rejected, "실행 중인 호스트의 세션 기록을 보존·교체하면 안 됩니다.");
            }
            Require(await File.ReadAllTextAsync(fixture.Store.JournalPath, ct) == before, "거부된 보존 명령은 기존 세션을 바꾸면 안 됩니다.");
        });
        await Check("new-start-freezes-completed-original-and-refuses-conflicting-snapshot", async () =>
        {
            var fixture = NewFixture("completed-preservation");
            fixture.Actions.DecisionsToReport = [new()
            {
                DecisionId = "synthetic-preserved-decision", RuleStatusCode = "Selected",
                Summary = "가상 완료 회차의 규칙 관측", RecordedAtUtc = fixture.Clock.GetUtcNow().UtcDateTime
            }];
            fixture.Actions.FailRead = true;
            await Start(fixture.Coordinator);
            await fixture.Coordinator.TickAsync(ct);
            fixture.Actions.FailRead = false;
            await fixture.Coordinator.TickAsync(ct);
            await Command(fixture.Coordinator, DispatchTrainingCommandCodes.Stop);
            var completed = await fixture.Coordinator.TickAsync(ct);
            Require(completed.PhaseCode == DispatchTrainingPhaseCodes.Completed, "검사할 이전 회차가 완료되어야 합니다.");
            var originalBytes = await File.ReadAllBytesAsync(fixture.Store.JournalPath, ct);
            var first = await fixture.Store.PreserveCompletedRunAsync(completed.RunId!, completed.Revision, ct);
            var reused = await fixture.Store.PreserveCompletedRunAsync(completed.RunId!, completed.Revision, ct);
            Require(!first.Reused && reused.Reused && first.Sha256 == reused.Sha256
                && first.Sha256 == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(originalBytes)),
                "동일 원문은 해시를 대조해 재사용해야 합니다.");
            var request = Request(completed);
            await File.WriteAllTextAsync(first.Path, "conflicting-snapshot", ct);
            var rejected = false;
            try { await fixture.Coordinator.CommandAsync(DispatchTrainingCommandCodes.Start, request, ct); }
            catch (InvalidDataException) { rejected = true; }
            var unchanged = await fixture.Coordinator.ReadAsync(ct);
            Require(rejected && unchanged.RunId == completed.RunId && unchanged.PhaseCode == DispatchTrainingPhaseCodes.Completed
                && (await File.ReadAllBytesAsync(fixture.Store.JournalPath, ct)).SequenceEqual(originalBytes),
                "동결 파일 충돌 시 새 시작을 취소하고 기존 세션 원문을 유지해야 합니다.");
            await File.WriteAllBytesAsync(first.Path, originalBytes, ct);
            var started = await fixture.Coordinator.CommandAsync(DispatchTrainingCommandCodes.Start, request, ct);
            var frozen = await new DispatchTrainingSessionStore(first.Path).LoadAsync(ct) ?? throw new InvalidOperationException();
            Require(started.Session.RunId != completed.RunId && started.Session.PhaseCode == DispatchTrainingPhaseCodes.Running
                && (await File.ReadAllBytesAsync(first.Path, ct)).SequenceEqual(originalBytes)
                && frozen.PhaseCode == DispatchTrainingPhaseCodes.Completed
                && frozen.Incidents.Any(x => x.KindCode == "CanonicalReadUnavailable")
                && frozen.Decisions.Any(x => x.DecisionId == "synthetic-preserved-decision"),
                "새 회차에 앞서 C의 조회 장애 사건과 판단 관측을 포함한 Completed 원문을 보존해야 합니다.");
        });
        await Check("archive-preserves-unknown-and-related-originals-before-idle", async () =>
        {
            var fixture = NewFixture("archive-unknown");
            fixture.Actions.UnknownSubmissions.Add(0);
            fixture.Actions.RevealOrders = false;
            await Start(fixture.Coordinator);
            await fixture.Coordinator.TickAsync(ct);
            await Command(fixture.Coordinator, DispatchTrainingCommandCodes.Stop);
            var before = await File.ReadAllTextAsync(fixture.Store.JournalPath, ct);
            var actorsPath = Path.Combine(directory, "archive-actors.json");
            var readyPath = Path.Combine(directory, "archive-ready.json");
            await File.WriteAllTextAsync(actorsPath, "{\"synthetic\":true,\"orders\":[]}", ct);
            await File.WriteAllTextAsync(readyPath, "{\"synthetic\":true}", ct);
            var actionsBefore = fixture.Actions.Submissions.Count;
            var archived = await DispatchTrainingArchive.ArchiveAsync(fixture.Store.JournalPath,
                [actorsPath, readyPath, Path.Combine(directory, "missing-optional.json")], ct, hostPort: 0);
            Require(await File.ReadAllTextAsync(Path.Combine(archived.ArchiveDirectory, Path.GetFileName(fixture.Store.JournalPath)), ct) == before,
                "불명확한 접수와 사건 이력의 원문을 보존해야 합니다.");
            Require(await File.ReadAllTextAsync(Path.Combine(archived.ArchiveDirectory, "archive-actors.json"), ct)
                == await File.ReadAllTextAsync(actorsPath, ct), "actor 원문을 보존하고 현재 actor 기록도 유지해야 합니다.");
            Require(archived.Files.Count == 3 && archived.MissingRelatedFileNames.Contains("missing-optional.json")
                && archived.Files.All(x => x.Sha256.Length == 64), "보존 파일 해시와 누락된 선택 파일을 기록해야 합니다.");
            var restarted = new DispatchTrainingCoordinator(fixture.Store, fixture.Actions, fixture.Clock);
            var idle = await restarted.ReadAsync(ct);
            Require(idle.PhaseCode == DispatchTrainingPhaseCodes.Idle && idle.RunId is null && idle.Counts.GeneratedOrders == 0,
                "명시 보존 뒤 신규 회차를 준비할 Idle 제어 기록이어야 합니다.");
            Require(!archived.DatabaseMutated && !archived.OrdersCancelled && !archived.PreviousWorkReconciled
                && fixture.Actions.Submissions.Count == actionsBefore, "보존을 업무 취소·실제 업무 완료로 표시하거나 처리하면 안 됩니다.");
            using var acquiredAgain = DispatchTrainingArchive.AcquireHostLease(fixture.Store.JournalPath);
        });
        return new(directory, results);

        (DispatchTrainingCoordinator Coordinator, DispatchTrainingSessionStore Store, ManualTimeProvider Clock, FakeActions Actions) NewFixture(string name)
        {
            var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero));
            var actions = new FakeActions(clock);
            var store = new DispatchTrainingSessionStore(Path.Combine(directory, $"{name}.json"));
            return (new(store, actions, clock), store, clock, actions);
        }

        async Task Check(string name, Func<Task> action)
        {
            ct.ThrowIfCancellationRequested();
            try { await action(); results.Add(new(name, true, null)); }
            catch (Exception error) when (error is not OutOfMemoryException && !ct.IsCancellationRequested)
            { results.Add(new(name, false, error.Message)); }
        }

        async Task<DispatchTrainingSessionDto> Start(DispatchTrainingCoordinator coordinator)
            => (await coordinator.CommandAsync(DispatchTrainingCommandCodes.Start, Request(await coordinator.ReadAsync(ct)), ct)).Session;

        async Task Command(DispatchTrainingCoordinator coordinator, string code)
            => await coordinator.CommandAsync(code, Request(await coordinator.ReadAsync(ct)), ct);
    }

    private static DispatchTrainingCommandRequest Request(DispatchTrainingSessionDto session) => new()
    {
        ClientRequestId = Guid.NewGuid(), ExpectedRevision = session.Revision, RunId = session.RunId
    };
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static async Task RejectCode<T>(Func<Task<T>> action, string expected)
    {
        try { await action(); throw new InvalidOperationException($"거부해야 할 명령이 적용되었습니다: {expected}"); }
        catch (DispatchTrainingControlException error) when (error.Code == expected) { }
    }

    private sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _now = initial;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class FakeActions(TimeProvider clock) : IDispatchTrainingActions
    {
        private readonly Dictionary<Guid, (int Index, string Id)> _orders = [];
        private readonly Dictionary<Guid, int> _notSubmitted = [];
        private readonly HashSet<Guid> _supportRequests = [];
        private int _activated;
        public List<(int Index, Guid RequestId, DateTime AtUtc)> Submissions { get; } = [];
        public HashSet<int> UnknownSubmissions { get; } = [];
        public HashSet<int> RejectedSubmissions { get; } = [];
        public HashSet<int> NotSubmittedAfterUnknown { get; } = [];
        public bool AutoComplete { get; set; } = true;
        public bool RevealOrders { get; set; } = true;
        public bool UnknownSupport { get; set; }
        public bool FailRead { get; set; }
        public IReadOnlyList<DispatchTrainingDecisionDto> DecisionsToReport { get; set; } = [];
        public int TickCalls { get; private set; }
        public int SupportCalls { get; private set; }

        public Task<DispatchTrainingSubmissionResult> SubmitOrderAsync(string runId, int index, Guid requestId, CancellationToken ct = default)
        {
            Submissions.Add((index, requestId, clock.GetUtcNow().UtcDateTime));
            if (RejectedSubmissions.Contains(index)) return Task.FromResult(new DispatchTrainingSubmissionResult(DispatchTrainingActionOutcomes.Rejected));
            if (NotSubmittedAfterUnknown.Contains(index))
            {
                _notSubmitted.TryAdd(requestId, index);
                return Task.FromResult(new DispatchTrainingSubmissionResult(DispatchTrainingActionOutcomes.Unknown));
            }
            _orders.TryAdd(requestId, (index, $"synthetic-{runId}-{index}"));
            return Task.FromResult(new DispatchTrainingSubmissionResult(UnknownSubmissions.Contains(index)
                ? DispatchTrainingActionOutcomes.Unknown : DispatchTrainingActionOutcomes.Confirmed, _orders[requestId].Id));
        }

        public Task<DispatchTrainingSupportResult> ActivateSupportDriversAsync(string runId, Guid requestId, int count, CancellationToken ct = default)
        {
            SupportCalls++;
            if (_supportRequests.Add(requestId)) _activated += count;
            return Task.FromResult(new DispatchTrainingSupportResult(UnknownSupport
                ? DispatchTrainingActionOutcomes.Unknown : DispatchTrainingActionOutcomes.Confirmed, _activated));
        }

        public Task<DispatchTrainingTickResult> TickAsync(string runId, CancellationToken ct = default)
        {
            TickCalls++;
            if (FailRead) return Task.FromResult(new DispatchTrainingTickResult { CanonicalReadSucceeded = false });
            var orders = RevealOrders ? _orders : new Dictionary<Guid, (int Index, string Id)>();
            return Task.FromResult(new DispatchTrainingTickResult
            {
                WaitingOrders = AutoComplete ? 0 : orders.Count, CompletedOrders = AutoComplete ? orders.Count : 0,
                UnfinishedOrders = AutoComplete ? 0 : orders.Count, ActivatedSupportDriverCount = _activated,
                Decisions = DecisionsToReport,
                Orders = orders.Select(x => new DispatchTrainingOrderObservation
                {
                    Index = x.Value.Index, ClientRequestId = x.Key, CanonicalOrderId = x.Value.Id,
                    StateCode = AutoComplete ? "Completed" : "Waiting"
                }).Concat(RevealOrders ? _notSubmitted.Select(x => new DispatchTrainingOrderObservation
                {
                    Index = x.Value, ClientRequestId = x.Key, StateCode = "NotSubmitted", CanonicalOrderId = null
                }) : Enumerable.Empty<DispatchTrainingOrderObservation>()).ToArray(),
                Drivers = Enumerable.Range(0, 8).Select(index => new DispatchTrainingDriverDto
                {
                    DriverId = $"synthetic-driver-{index}", Label = $"가상 기사 {index + 1}", IsSupport = index >= 6,
                    StatusCode = index < 6 || index - 6 < _activated ? "Receiving" : "Standby"
                }).ToArray()
            });
        }
    }
}

public sealed record DispatchTrainingSelfCheckResult(string Name, bool Passed, string? Error);
public sealed record DispatchTrainingSelfCheckReport(string ArtifactDirectory, IReadOnlyList<DispatchTrainingSelfCheckResult> Checks)
{
    public bool Passed => Checks.All(x => x.Passed);
    public int PassedCount => Checks.Count(x => x.Passed);
    public int FailedCount => Checks.Count(x => !x.Passed);
    public bool UsesRealApis => false;
    public bool RunsModels => false;
}
