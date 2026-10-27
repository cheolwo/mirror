using System;
using System.Collections.Generic;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    /// <summary>센터·선반 출고는 기존 Session의 개정과 명령 로그에 속하는 가상 상태다.</summary>
    public sealed partial class 경영SimulationSessionAggregate
    {
        private SimulationFulfillmentStateSnapshot? fulfillmentState;
        private readonly Dictionary<string, AppliedFulfillmentCommand> appliedFulfillmentCommands =
            new Dictionary<string, AppliedFulfillmentCommand>(StringComparer.Ordinal);

        public SimulationFulfillmentStateSnapshot? GetFulfillmentState()
        {
            lock (gate) return SimulationFulfillmentEngine.Clone(fulfillmentState);
        }

        public SimulationFulfillmentPlanSnapshot PreviewFulfillment(
            SimulationFulfillmentOrderRequest request)
        {
            lock (gate)
            {
                if (fulfillmentState == null)
                    throw new SimulationConflictException("SimulationFulfillmentNotConfigured");
                return SimulationFulfillmentEngine.Preview(fulfillmentState, request);
            }
        }

        public 경영SimulationSessionSnapshot ConfirmFulfillment(SimulationFulfillmentCommand request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireStableId(request.CommandId, "SimulationCommandIdInvalid");
            if (request.ExpectedRevision < 0)
                throw new SimulationContractException("SimulationExpectedRevisionInvalid");

            lock (gate)
            {
                var command = SimulationFulfillmentEngine.CloneCommand(request);
                command.CommandId = command.CommandId.Trim();
                var key = SimulationFulfillmentEngine.BuildCommandKey(command);
                if (appliedFulfillmentCommands.TryGetValue(command.CommandId, out var applied))
                {
                    if (!string.Equals(key, applied.PayloadKey, StringComparison.Ordinal))
                        throw new SimulationConflictException("SimulationCommandPayloadConflict");
                    return Clone(applied.Snapshot);
                }
                if (HasDifferentKindCommand(command.CommandId)
                    || HasAppliedDecisionCommand(command.CommandId))
                    throw new SimulationConflictException("SimulationCommandKindConflict");
                if (command.ExpectedRevision != Revision)
                    throw new SimulationConflictException("SimulationExpectedRevisionMismatch");

                // 복제한 상태에서 전체 변경을 검증한 뒤 반영해 부분 예약을 남기지 않는다.
                var next = SimulationFulfillmentEngine.Apply(fulfillmentState, command);
                SimulationFulfillmentEngine.ValidateState(next);
                fulfillmentState = next;
                Revision++;
                commandLog.Add(new SimulationCommandLogEntrySnapshot
                {
                    Sequence = commandLog.Count + 1L,
                    CommandTypeCode = SimulationCommandTypeCodes.FulfillmentConfirm,
                    AppliedWorldTick = CurrentTick,
                    ResultingWorldRevision = Revision,
                    FulfillmentCommand = command,
                });
                var snapshot = CreateSnapshot();
                appliedFulfillmentCommands.Add(command.CommandId,
                    new AppliedFulfillmentCommand(key, Clone(snapshot)));
                return snapshot;
            }
        }

        private bool HasAppliedFulfillmentCommand(string commandId)
            => appliedFulfillmentCommands.ContainsKey(commandId);

        internal static void RequireFulfillmentReplayCommand(SimulationFulfillmentCommand command)
        {
            if (command == null) throw new SimulationContractException("SimulationCommandLogPayloadInvalid");
            RequireStableId(command.CommandId, "SimulationCommandIdInvalid");
            if (command.ExpectedRevision < 0)
                throw new SimulationContractException("SimulationExpectedRevisionInvalid");
            if (command.ActionCode != SimulationFulfillmentActionCodes.Configure
                && command.ActionCode != SimulationFulfillmentActionCodes.Reserve
                && command.ActionCode != SimulationFulfillmentActionCodes.Pick
                && command.ActionCode != SimulationFulfillmentActionCodes.PackReady
                && command.ActionCode != SimulationFulfillmentActionCodes.Cancel)
                throw new SimulationContractException("SimulationFulfillmentActionInvalid");
            SimulationFulfillmentEngine.BuildCommandKey(command);
        }

        private sealed class AppliedFulfillmentCommand
        {
            public AppliedFulfillmentCommand(string payloadKey, 경영SimulationSessionSnapshot snapshot)
            {
                PayloadKey = payloadKey;
                Snapshot = snapshot;
            }
            public string PayloadKey { get; }
            public 경영SimulationSessionSnapshot Snapshot { get; }
        }
    }
}
