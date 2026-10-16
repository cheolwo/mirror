using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;

namespace Ssalddel.DispatchTraining;

/// <summary>실시간 신규 접수 일정과 제어 요청의 복구를 소유합니다. 배차 판단·업무 완료는 소유하지 않습니다.</summary>
public sealed class DispatchTrainingCoordinator
{
    private readonly DispatchTrainingSessionStore _store;
    private readonly IDispatchTrainingActions _actions;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DispatchTrainingSessionState? _state;
    private bool _persistenceFault;

    public DispatchTrainingCoordinator(DispatchTrainingSessionStore store, IDispatchTrainingActions actions, TimeProvider clock)
    {
        _store = store;
        _actions = actions;
        _clock = clock;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { await InitializeCoreAsync(ct); }
        finally { _gate.Release(); }
    }

    public async Task<DispatchTrainingSessionDto> ReadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { await InitializeCoreAsync(ct); return ToDto(); }
        finally { _gate.Release(); }
    }

    public async Task<DispatchTrainingDecisionsResponse> DecisionsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeCoreAsync(ct);
            return new() { RunId = State.RunId, Revision = State.Revision, Items = State.Decisions.ToArray() };
        }
        finally { _gate.Release(); }
    }

    public async Task<DispatchTrainingIncidentsResponse> IncidentsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeCoreAsync(ct);
            return new() { RunId = State.RunId, Revision = State.Revision, Items = State.Incidents.ToArray() };
        }
        finally { _gate.Release(); }
    }

    public async Task<DispatchTrainingSessionDto> TickAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeCoreAsync(ct);
            if (State.RunId is null || State.PhaseCode is DispatchTrainingPhaseCodes.Idle
                or DispatchTrainingPhaseCodes.Completed or DispatchTrainingPhaseCodes.Failed) return ToDto();
            var changed = false;
            try
            {
                var observation = await _actions.TickAsync(State.RunId, ct);
                if (observation.CanonicalReadSucceeded)
                {
                    changed |= ApplyObservation(observation);
                    changed |= ResolveIncident("canonical-read");
                }
                else
                {
                    changed |= MarkReadFailure();
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                changed |= MarkReadFailure();
            }

            var now = UtcNow;
            if (State.PhaseCode is DispatchTrainingPhaseCodes.Running or DispatchTrainingPhaseCodes.Paused
                && now >= State.GenerationEndsAtUtc!.Value)
            {
                State.PhaseCode = DispatchTrainingPhaseCodes.Draining;
                SkipScheduledThrough(DateTime.MaxValue);
                AddEvent("GenerationEnded");
                changed = true;
            }

            if (State.PhaseCode == DispatchTrainingPhaseCodes.Running && !ct.IsCancellationRequested)
            {
                // 가장 최근 슬롯 하나만 접수합니다. 멈춰 있던 시간의 주문을 몰아서 전송하지 않습니다.
                var currentIndex = (int)Math.Floor((now - State.StartedAtUtc!.Value).TotalSeconds
                    / DispatchTrainingScenario.OrderIntervalSeconds);
                foreach (var missed in State.Slots.Where(x => x.StatusCode == "Scheduled" && x.Index < currentIndex))
                {
                    missed.StatusCode = "Skipped";
                    AddEvent("OrderSlotSkipped", missed.Index, missed.ClientRequestId, "MissedWindow");
                    changed = true;
                }
                var slot = State.Slots.SingleOrDefault(x => x.Index == currentIndex && x.StatusCode == "Scheduled" && x.DueAtUtc <= now);
                if (slot is not null)
                {
                    slot.StatusCode = "Submitting";
                    AddEvent("OrderSubmissionReserved", slot.Index, slot.ClientRequestId);
                    await PersistAsync(ct); // POST보다 요청 식별자를 먼저 확정합니다.
                    try
                    {
                        var result = await _actions.SubmitOrderAsync(State.RunId!, slot.Index, slot.ClientRequestId, ct);
                        if (result.OutcomeCode == DispatchTrainingActionOutcomes.Confirmed
                            && !string.IsNullOrWhiteSpace(result.CanonicalOrderId))
                        {
                            slot.StatusCode = "Confirmed";
                            slot.CanonicalOrderId = result.CanonicalOrderId;
                            AddEvent("OrderSubmissionConfirmed", slot.Index, slot.ClientRequestId);
                        }
                        else if (result.OutcomeCode == DispatchTrainingActionOutcomes.Rejected)
                        {
                            slot.StatusCode = "FailedReview";
                            slot.LastErrorCode = result.ErrorCode ?? "OrderRejected";
                            AddIncident($"order-{slot.Index}", "OrderSubmissionRejected", "해당 접수를 검토해야 합니다. 다른 업무는 계속 진행합니다.");
                            AddEvent("OrderSubmissionRejected", slot.Index, slot.ClientRequestId, slot.LastErrorCode);
                        }
                        else MarkSubmissionUnknown(slot, result.ErrorCode ?? "SubmissionOutcomeUnknown");
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        MarkSubmissionUnknown(slot, "SubmissionOutcomeUnknown");
                    }
                    changed = true;
                }
            }

            if (State.PhaseCode == DispatchTrainingPhaseCodes.Draining
                && State.LastObservation.CanonicalReadSucceeded && State.LastObservation.UnfinishedOrders == 0
                && !State.Slots.Any(x => x.StatusCode is "Submitting" or "Unknown")
                && !State.Commands.Any(x => x.RunId == State.RunId && x.OutcomeCode is "Pending" or "Unknown"))
            {
                State.PhaseCode = DispatchTrainingPhaseCodes.Completed;
                AddEvent("RunCompleted");
                changed = true;
            }
            if (changed) await PersistAsync(CancellationToken.None);
            return ToDto();
        }
        finally { _gate.Release(); }
    }

    public async Task<DispatchTrainingCommandResultDto> CommandAsync(string commandCode,
        DispatchTrainingCommandRequest request, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeCoreAsync(ct);
            if (commandCode == DispatchTrainingCommandCodes.SupportDrivers)
                throw Control("UseSupportDriversCommand", 400);
            var fingerprint = Fingerprint(commandCode, request, 0);
            var prior = FindReceipt(request.ClientRequestId, fingerprint);
            if (prior is not null) return Result(request.ClientRequestId, replayed: true);
            ValidateVersion(request);
            if (!AllowedCommands().Contains(commandCode)) throw Control("CommandNotAllowed", 409);

            switch (commandCode)
            {
                case DispatchTrainingCommandCodes.Start:
                    if (State.RunId is not null)
                        await _store.PreserveCompletedRunAsync(State.RunId, State.Revision, ct);
                    State.RunId = $"dispatch-training-{request.ClientRequestId:N}";
                    State.StartedAtUtc = UtcNow;
                    State.GenerationEndsAtUtc = State.StartedAtUtc.Value.AddSeconds(DispatchTrainingScenario.DurationSeconds);
                    State.PhaseCode = DispatchTrainingPhaseCodes.Running;
                    State.Slots = Enumerable.Range(0, DispatchTrainingScenario.MaximumOrders).Select(index => new DispatchTrainingOrderSlot
                    {
                        Index = index, DueAtUtc = DispatchTrainingScenario.DueAt(State.StartedAtUtc.Value, index),
                        ClientRequestId = DispatchTrainingScenario.OrderRequestId(State.RunId, index)
                    }).ToList();
                    State.Incidents = [];
                    State.Decisions = [];
                    State.LastObservation = new() { CanonicalReadSucceeded = false };
                    break;
                case DispatchTrainingCommandCodes.Pause:
                    State.PhaseCode = DispatchTrainingPhaseCodes.Paused;
                    SkipScheduledThrough(UtcNow);
                    break;
                case DispatchTrainingCommandCodes.Resume:
                    if (UtcNow >= State.GenerationEndsAtUtc!.Value) throw Control("GenerationWindowEnded", 409);
                    SkipScheduledThrough(UtcNow);
                    State.PhaseCode = DispatchTrainingPhaseCodes.Running;
                    break;
                case DispatchTrainingCommandCodes.Stop:
                    State.PhaseCode = DispatchTrainingPhaseCodes.Draining;
                    SkipScheduledThrough(DateTime.MaxValue);
                    break;
                default: throw Control("UnknownCommand", 400);
            }
            State.Commands.Add(new()
            {
                ClientRequestId = request.ClientRequestId, Fingerprint = fingerprint, CommandCode = commandCode,
                RunId = State.RunId, OutcomeCode = "Applied"
            });
            AddEvent("ControlApplied", clientRequestId: request.ClientRequestId, detailCode: commandCode);
            await PersistAsync(ct);
            return Result(request.ClientRequestId, replayed: false);
        }
        finally { _gate.Release(); }
    }

    public async Task<DispatchTrainingCommandResultDto> SupportDriversAsync(DispatchTrainingSupportDriversRequest request,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeCoreAsync(ct);
            if (request.Count is < 1 or > DispatchTrainingScenario.SupportDrivers) throw Control("InvalidSupportDriverCount", 400);
            var fingerprint = Fingerprint(DispatchTrainingCommandCodes.SupportDrivers, request, request.Count);
            var prior = FindReceipt(request.ClientRequestId, fingerprint);
            if (prior is not null) return Result(request.ClientRequestId, replayed: true);
            ValidateVersion(request);
            if (!AllowedCommands().Contains(DispatchTrainingCommandCodes.SupportDrivers)) throw Control("CommandNotAllowed", 409);
            var target = State.LastObservation.ActivatedSupportDriverCount + request.Count;
            if (target > DispatchTrainingScenario.SupportDrivers) throw Control("SupportDriverCapacityExceeded", 409);
            var receipt = new DispatchTrainingCommandReceipt
            {
                ClientRequestId = request.ClientRequestId, Fingerprint = fingerprint,
                CommandCode = DispatchTrainingCommandCodes.SupportDrivers, RunId = State.RunId,
                SupportTargetCount = target, OutcomeCode = "Pending"
            };
            State.Commands.Add(receipt);
            AddEvent("SupportParticipationReserved", clientRequestId: request.ClientRequestId);
            await PersistAsync(ct);
            try
            {
                var result = await _actions.ActivateSupportDriversAsync(State.RunId!, request.ClientRequestId, request.Count, ct);
                if (result.OutcomeCode == DispatchTrainingActionOutcomes.Confirmed
                    && result.ActivatedSupportDriverCount >= target
                    && result.ActivatedSupportDriverCount <= DispatchTrainingScenario.SupportDrivers)
                {
                    State.LastObservation = State.LastObservation with { ActivatedSupportDriverCount = result.ActivatedSupportDriverCount };
                    receipt.OutcomeCode = "Applied";
                    AddEvent("SupportParticipationConfirmed", clientRequestId: request.ClientRequestId);
                }
                else if (result.OutcomeCode == DispatchTrainingActionOutcomes.Rejected)
                {
                    receipt.OutcomeCode = "Rejected";
                    AddIncident($"support-{request.ClientRequestId:N}", "SupportParticipationRejected", "지원 참여가 확정되지 않았습니다. 현재 상태를 확인해 주세요.");
                    AddEvent("SupportParticipationRejected", clientRequestId: request.ClientRequestId, detailCode: result.ErrorCode);
                }
                else MarkSupportUnknown(receipt);
            }
            catch (Exception error) when (error is not OutOfMemoryException) { MarkSupportUnknown(receipt); }
            await PersistAsync(CancellationToken.None);
            return Result(request.ClientRequestId, replayed: false);
        }
        finally { _gate.Release(); }
    }

    private DispatchTrainingSessionState State => _state ?? throw new InvalidOperationException("훈련 기록이 준비되지 않았습니다.");
    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    private async Task InitializeCoreAsync(CancellationToken ct)
    {
        if (_persistenceFault) throw new IOException("훈련 기록 저장 실패로 신규 명령을 중단했습니다. 기록을 확인한 뒤 서버를 다시 시작해 주세요.");
        if (_state is not null) return;
        _state = await _store.LoadAsync(ct) ?? new();
        var changed = false;
        if (State.RunId is not null)
        {
            // 재시작 뒤 이전 프로세스의 관측을 최신 정본으로 간주하지 않습니다.
            State.LastObservation = State.LastObservation with { CanonicalReadSucceeded = false };
            if (State.PhaseCode == DispatchTrainingPhaseCodes.Running)
            {
                State.PhaseCode = DispatchTrainingPhaseCodes.Paused;
                SkipScheduledThrough(UtcNow);
                AddEvent("RestartGenerationPaused");
                AddIncident("restart", "RestartGenerationPaused", "서버 재시작으로 신규 접수를 멈췄습니다. 기존 업무 확인 후 직접 재개할 수 있습니다.");
                changed = true;
            }
            foreach (var slot in State.Slots.Where(x => x.StatusCode == "Submitting"))
            {
                MarkSubmissionUnknown(slot, "RestartDuringSubmission");
                changed = true;
            }
            foreach (var receipt in State.Commands.Where(x => x.OutcomeCode == "Pending"))
            {
                MarkSupportUnknown(receipt);
                changed = true;
            }
        }
        if (changed) await PersistAsync(ct);
    }

    private async Task PersistAsync(CancellationToken ct)
    {
        try { await _store.SaveAsync(State, ct); }
        catch { _persistenceFault = true; throw; }
    }

    private bool ApplyObservation(DispatchTrainingTickResult observation)
    {
        if (observation.ActivatedSupportDriverCount is < 0 or > DispatchTrainingScenario.SupportDrivers
            || observation.UnfinishedOrders < 0 || observation.WaitingOrders < 0 || observation.AssignedOrders < 0
            || observation.InProgressOrders < 0 || observation.CompletedOrders < 0 || observation.FailedOrders < 0)
            return MarkReadFailure();
        var changed = JsonSerializer.Serialize(State.LastObservation) != JsonSerializer.Serialize(observation);
        State.LastObservation = observation;
        if (changed) AddEvent("CanonicalObservationUpdated");
        foreach (var incident in observation.Incidents.Where(x => !string.IsNullOrWhiteSpace(x.IncidentId)))
        {
            var index = State.Incidents.FindIndex(x => x.IncidentId == incident.IncidentId);
            if (index >= 0 && State.Incidents[index] == incident) continue;
            if (index < 0) State.Incidents.Add(incident); else State.Incidents[index] = incident;
            AddEvent("ActorIncidentUpdated", detailCode: incident.KindCode);
            changed = true;
        }
        foreach (var decision in observation.Decisions.Where(x => !string.IsNullOrWhiteSpace(x.DecisionId)))
        {
            var index = State.Decisions.FindIndex(x => x.DecisionId == decision.DecisionId);
            if (index >= 0 && State.Decisions[index] == decision) continue;
            if (index < 0) State.Decisions.Add(decision); else State.Decisions[index] = decision;
            AddEvent("ActorDecisionUpdated");
            changed = true;
        }
        foreach (var order in observation.Orders)
        {
            var slot = State.Slots.SingleOrDefault(x => x.Index == order.Index && x.ClientRequestId == order.ClientRequestId);
            if (slot is null) continue;
            if (order.StateCode == "NotSubmitted")
            {
                // 준비 실패와 POST 결과 불명확을 구분하는 actor 정본 관측입니다. 재접수하지 않습니다.
                if (string.IsNullOrWhiteSpace(order.CanonicalOrderId) && (slot.StatusCode is "Unknown" or "Submitting"))
                {
                    slot.StatusCode = "FailedReview";
                    slot.LastErrorCode = "SubmissionNotSent";
                    ResolveIncident($"order-{slot.Index}");
                    AddIncident($"order-{slot.Index}-not-sent", "OrderSubmissionNotSent",
                        "접수 요청이 전송되지 않았음을 확인했습니다. 실패 기록을 검토하며 다른 업무는 계속 진행합니다.");
                    AddEvent("OrderSubmissionNotSent", slot.Index, slot.ClientRequestId, "SubmissionNotSent");
                    changed = true;
                }
                continue;
            }
            if (string.IsNullOrWhiteSpace(order.CanonicalOrderId)
                || order.StateCode is "Unknown" or "Missing") continue;
            if (slot.StatusCode is "Unknown" or "Submitting")
            {
                slot.StatusCode = "Confirmed";
                slot.CanonicalOrderId = order.CanonicalOrderId;
                slot.LastErrorCode = null;
                ResolveIncident($"order-{slot.Index}");
                AddEvent("OrderSubmissionReconciled", slot.Index, slot.ClientRequestId);
                changed = true;
            }
        }
        foreach (var receipt in State.Commands.Where(x => x.RunId == State.RunId
            && x.CommandCode == DispatchTrainingCommandCodes.SupportDrivers && x.OutcomeCode is "Pending" or "Unknown"))
        {
            if (observation.ActivatedSupportDriverCount < receipt.SupportTargetCount) continue;
            receipt.OutcomeCode = "Applied";
            ResolveIncident($"support-{receipt.ClientRequestId:N}");
            AddEvent("SupportParticipationReconciled", clientRequestId: receipt.ClientRequestId);
            changed = true;
        }
        return changed;
    }

    private bool MarkReadFailure()
    {
        var changed = State.LastObservation.CanonicalReadSucceeded;
        State.LastObservation = State.LastObservation with { CanonicalReadSucceeded = false };
        return AddIncident("canonical-read", "CanonicalReadUnavailable", "업무 상태를 확인하지 못했습니다. 이전 관측을 유지하며 독립 업무를 계속 확인합니다.") || changed;
    }

    private void MarkSubmissionUnknown(DispatchTrainingOrderSlot slot, string code)
    {
        slot.StatusCode = "Unknown";
        slot.LastErrorCode = code;
        AddIncident($"order-{slot.Index}", "OrderSubmissionUnknown", "접수 결과를 확인 중입니다. 같은 주문을 다시 접수하지 않습니다.");
        AddEvent("OrderSubmissionUnknown", slot.Index, slot.ClientRequestId, code);
    }

    private void MarkSupportUnknown(DispatchTrainingCommandReceipt receipt)
    {
        receipt.OutcomeCode = "Unknown";
        AddIncident($"support-{receipt.ClientRequestId:N}", "SupportParticipationUnknown", "지원 참여 결과를 확인 중입니다. 같은 참여 명령을 다시 보내지 않습니다.");
        AddEvent("SupportParticipationUnknown", clientRequestId: receipt.ClientRequestId);
    }

    private void SkipScheduledThrough(DateTime until)
    {
        foreach (var slot in State.Slots.Where(x => x.StatusCode == "Scheduled" && x.DueAtUtc <= until))
        {
            slot.StatusCode = "Skipped";
            AddEvent("OrderSlotSkipped", slot.Index, slot.ClientRequestId, "GenerationPausedOrEnded");
        }
    }

    private bool AddIncident(string suffix, string kind, string message)
    {
        var id = $"{State.RunId}|{suffix}";
        var index = State.Incidents.FindIndex(x => x.IncidentId == id);
        if (index >= 0 && IsPending(State.Incidents[index])) return false;
        var incident = new DispatchTrainingIncidentDto
        {
            IncidentId = id, KindCode = kind, StatusCode = "ReviewRequired", Message = message, RecordedAtUtc = UtcNow
        };
        if (index < 0) State.Incidents.Add(incident); else State.Incidents[index] = incident;
        AddEvent("IncidentRecorded", detailCode: kind);
        return true;
    }

    private bool ResolveIncident(string suffix)
    {
        var id = $"{State.RunId}|{suffix}";
        var index = State.Incidents.FindIndex(x => x.IncidentId == id);
        if (index < 0 || !IsPending(State.Incidents[index])) return false;
        State.Incidents[index] = State.Incidents[index] with { StatusCode = "Resolved" };
        AddEvent("IncidentResolved", detailCode: State.Incidents[index].KindCode);
        return true;
    }

    private void AddEvent(string kind, int? orderIndex = null, Guid? clientRequestId = null, string? detailCode = null)
    {
        State.Revision++;
        State.Events.Add(new(++State.EventSequence, UtcNow, kind, orderIndex, clientRequestId, detailCode));
    }

    private DispatchTrainingCommandReceipt? FindReceipt(Guid id, string fingerprint)
    {
        if (id == Guid.Empty) throw Control("ClientRequestIdRequired", 400);
        var receipt = State.Commands.SingleOrDefault(x => x.ClientRequestId == id);
        if (receipt is not null && !string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal))
            throw Control("ClientRequestIdReused", 409);
        return receipt;
    }

    private void ValidateVersion(DispatchTrainingCommandRequest request)
    {
        if (request.ExpectedRevision != State.Revision) throw Control("RevisionConflict", 409);
        if (!string.Equals(request.RunId, State.RunId, StringComparison.Ordinal)) throw Control("RunConflict", 409);
    }

    private IReadOnlyList<string> AllowedCommands()
    {
        List<string> commands = State.PhaseCode switch
        {
            DispatchTrainingPhaseCodes.Idle or DispatchTrainingPhaseCodes.Completed => [DispatchTrainingCommandCodes.Start],
            DispatchTrainingPhaseCodes.Running => [DispatchTrainingCommandCodes.Pause, DispatchTrainingCommandCodes.Stop],
            DispatchTrainingPhaseCodes.Paused => [DispatchTrainingCommandCodes.Resume, DispatchTrainingCommandCodes.Stop],
            _ => []
        };
        if (State.PhaseCode == DispatchTrainingPhaseCodes.Paused && UtcNow >= State.GenerationEndsAtUtc)
            commands.Remove(DispatchTrainingCommandCodes.Resume);
        if (State.PhaseCode is DispatchTrainingPhaseCodes.Running or DispatchTrainingPhaseCodes.Paused or DispatchTrainingPhaseCodes.Draining
            && State.LastObservation.ActivatedSupportDriverCount < DispatchTrainingScenario.SupportDrivers
            && !State.Commands.Any(x => x.RunId == State.RunId && x.OutcomeCode is "Pending" or "Unknown"))
            commands.Add(DispatchTrainingCommandCodes.SupportDrivers);
        return commands;
    }

    private DispatchTrainingSessionDto ToDto()
    {
        var observation = State.LastObservation;
        var pending = State.Incidents.Where(IsPending).ToArray();
        var next = State.PhaseCode == DispatchTrainingPhaseCodes.Running
            ? State.Slots.FirstOrDefault(x => x.StatusCode == "Scheduled") : null;
        return new()
        {
            RunId = State.RunId, Revision = State.Revision, PhaseCode = _persistenceFault ? DispatchTrainingPhaseCodes.Failed : State.PhaseCode,
            ServerTimeUtc = UtcNow, StartedAtUtc = State.StartedAtUtc, GenerationEndsAtUtc = State.GenerationEndsAtUtc,
            RemainingGenerationSeconds = State.GenerationEndsAtUtc is { } end
                ? (int)Math.Clamp(Math.Ceiling((end - UtcNow).TotalSeconds), 0, DispatchTrainingScenario.DurationSeconds) : 0,
            DurationSeconds = DispatchTrainingScenario.DurationSeconds,
            OrderIntervalSeconds = DispatchTrainingScenario.OrderIntervalSeconds, MaximumOrders = DispatchTrainingScenario.MaximumOrders,
            ActiveDriverCount = State.RunId is null ? 0 : observation.Drivers.Count > 0
                ? observation.Drivers.Count(x => !x.IsSupport) : DispatchTrainingScenario.ActiveDrivers,
            ActivatedSupportDriverCount = observation.ActivatedSupportDriverCount,
            AvailableSupportDriverCount = State.RunId is null ? DispatchTrainingScenario.SupportDrivers
                : Math.Max(0, DispatchTrainingScenario.SupportDrivers - observation.ActivatedSupportDriverCount),
            Counts = new()
            {
                GeneratedOrders = State.Slots.Count(x => x.StatusCode == "Confirmed"),
                WaitingOrders = observation.WaitingOrders, AssignedOrders = observation.AssignedOrders,
                InProgressOrders = observation.InProgressOrders, CompletedOrders = observation.CompletedOrders,
                FailedOrders = observation.FailedOrders + State.Slots.Count(x => x.StatusCode == "FailedReview"),
                PendingIncidents = pending.Length
            },
            NextEvent = next is null ? null : new()
            {
                KindCode = "OrderSubmission", DueAtUtc = next.DueAtUtc,
                Label = $"가상 주문 {next.Index + 1}"
            },
            Drivers = observation.Drivers.ToArray(), PendingIncidents = pending, LatestAiResult = observation.LatestAiResult,
            AllowedCommands = _persistenceFault ? [] : AllowedCommands(),
            AppliedCommandIds = State.Commands.Where(x => x.RunId == State.RunId && x.OutcomeCode == "Applied")
                .Select(x => x.ClientRequestId).ToArray()
        };
    }

    private static bool IsPending(DispatchTrainingIncidentDto incident) => incident.StatusCode is not ("Resolved" or "Closed");
    private DispatchTrainingCommandResultDto Result(Guid id, bool replayed) => new()
    {
        ClientRequestId = id, Replayed = replayed, Session = ToDto()
    };
    private static string Fingerprint(string code, DispatchTrainingCommandRequest request, int count)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", code, request.RunId,
            request.ExpectedRevision.ToString(CultureInfo.InvariantCulture), count.ToString(CultureInfo.InvariantCulture)))));
    private static DispatchTrainingControlException Control(string code, int status) => new(code, status);
}

public sealed class DispatchTrainingControlException(string code, int statusCode) : Exception(code)
{
    public string Code { get; } = code;
    public string ErrorCode => Code;
    public int StatusCode { get; } = statusCode;
}
