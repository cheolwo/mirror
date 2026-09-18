using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private Simulation절기운영CampaignStateSnapshot? seasonalOperationsCampaignState;
        private readonly Dictionary<string, 적용된절기운영CampaignCommand>
            appliedSeasonalOperationsCampaignCommands =
                new Dictionary<string, 적용된절기운영CampaignCommand>(StringComparer.Ordinal);

        public Simulation절기운영CampaignStateSnapshot GetSeasonalOperationsCampaignState()
        {
            lock (gate)
            {
                return CloneSeasonalOperationsCampaignState(
                        seasonalOperationsCampaignState)
                    ?? new Simulation절기운영CampaignStateSnapshot();
            }
        }

        public Simulation절기운영CampaignStateSnapshot BeginSeasonalOperationsCampaign(
            Simulation절기운영CampaignStartRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ValidateCampaignCommandId(request.CommandId);
            var definition = ValidateAndCloneDefinition(request.Definition);
            var sources = NormalizeSourceRevisions(request.SourceRevisions);
            if (string.IsNullOrWhiteSpace(request.AreaStableId))
                throw new SimulationContractException(
                    "SeasonalOperationsCampaignAreaStableIdRequired");
            var signature = StartCommandSignature(request, definition, sources);

            lock (gate)
            {
                if (TryGetAppliedCampaignCommand(request.CommandId, signature,
                        out var applied))
                    return applied;
                if (request.ExpectedRevision != Revision)
                    throw new SimulationConflictException(
                        "SimulationExpectedRevisionMismatch");
                if (seasonalOperationsCampaignState != null
                    && string.Equals(seasonalOperationsCampaignState.StateCode,
                        Simulation절기운영CampaignCodes.Active,
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SeasonalOperationsCampaignAlreadyActive");

                var firstPhase = definition.Phases[0];
                Revision++;
                seasonalOperationsCampaignState =
                    new Simulation절기운영CampaignStateSnapshot
                    {
                        CampaignStableId = definition.CampaignStableId,
                        DefinitionRevision = definition.DefinitionRevision,
                        AreaStableId = request.AreaStableId.Trim(),
                        StateCode = Simulation절기운영CampaignCodes.Active,
                        CurrentPhaseCode = firstPhase.PhaseCode,
                        CurrentPhaseOrdinal = 1,
                        PhaseCount = definition.Phases.Length,
                        StartedAtTick = CurrentTick,
                        PhaseStartedAtTick = CurrentTick,
                        CampaignRevision = Revision,
                        Phases = ClonePhases(definition.Phases),
                        SourceRevisions = CloneSources(sources),
                        AvailableActions = new[]
                        {
                            Simulation절기운영CampaignCodes.PreviewAdvance,
                        },
                        Events = new[]
                        {
                            CampaignEvent(
                                Simulation절기운영CampaignCodes.CampaignStarted,
                                string.Empty, firstPhase.PhaseCode,
                                request.CommandId),
                        },
                    };
                var result = CloneSeasonalOperationsCampaignState(
                    seasonalOperationsCampaignState)!;
                appliedSeasonalOperationsCampaignCommands.Add(request.CommandId,
                    new 적용된절기운영CampaignCommand(signature, result));
                AppendSeasonalOperationsCampaignTransition(request.CommandId,
                    signature, result);
                return result;
            }
        }

        public Simulation절기운영CampaignAdvancePreviewSnapshot
            PreviewSeasonalOperationsCampaignAdvance(
                Simulation절기운영CampaignAdvancePreviewRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            lock (gate)
            {
                return BuildSeasonalOperationsCampaignAdvancePreview(request);
            }
        }

        public Simulation절기운영CampaignStateSnapshot
            ConfirmSeasonalOperationsCampaignAdvance(
                Simulation절기운영CampaignAdvanceConfirmRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ValidateCampaignCommandId(request.CommandId);
            if (request.PreviewRequest == null
                || string.IsNullOrWhiteSpace(request.ExpectedPreviewHash))
                throw new SimulationContractException(
                    "SeasonalOperationsCampaignConfirmRequestInvalid");
            var signature = ConfirmCommandSignature(request);

            lock (gate)
            {
                if (TryGetAppliedCampaignCommand(request.CommandId, signature,
                        out var applied))
                    return applied;
                var preview = BuildSeasonalOperationsCampaignAdvancePreview(
                    request.PreviewRequest);
                if (!string.Equals(preview.PreviewHash,
                        request.ExpectedPreviewHash.Trim(),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SeasonalOperationsCampaignPreviewMismatch");
                if (!preview.CanConfirm)
                    throw new SimulationConflictException(
                        "SeasonalOperationsCampaignAdvanceBlocked");

                var state = RequireActiveSeasonalOperationsCampaign(
                    request.PreviewRequest.ExpectedRevision);
                var fromPhase = state.CurrentPhaseCode;
                Revision++;
                state.SourceRevisions = CloneSources(preview.SourceRevisions);
                if (preview.CompletesCampaign)
                {
                    state.StateCode = Simulation절기운영CampaignCodes.Completed;
                    state.AvailableActions = Array.Empty<string>();
                    state.Events = AppendCampaignEvent(state.Events,
                        CampaignEvent(
                            Simulation절기운영CampaignCodes.CampaignCompleted,
                            fromPhase, fromPhase, request.CommandId));
                }
                else
                {
                    state.CurrentPhaseOrdinal++;
                    state.CurrentPhaseCode = preview.NextPhaseCode;
                    state.PhaseStartedAtTick = CurrentTick;
                    state.AvailableActions = new[]
                    {
                        Simulation절기운영CampaignCodes.PreviewAdvance,
                    };
                    state.Events = AppendCampaignEvent(state.Events,
                        CampaignEvent(
                            Simulation절기운영CampaignCodes.PhaseAdvanced,
                            fromPhase, state.CurrentPhaseCode,
                            request.CommandId));
                }
                state.CampaignRevision = Revision;
                var result = CloneSeasonalOperationsCampaignState(state)!;
                appliedSeasonalOperationsCampaignCommands.Add(request.CommandId,
                    new 적용된절기운영CampaignCommand(signature, result));
                AppendSeasonalOperationsCampaignTransition(request.CommandId,
                    signature, result);
                return result;
            }
        }

        internal void ReplaySeasonalOperationsCampaignTransition(
            string commandId, string commandSignature,
            Simulation절기운영CampaignStateSnapshot state)
        {
            ValidateCampaignCommandId(commandId);
            if (string.IsNullOrWhiteSpace(commandSignature) || state == null)
                throw new SimulationContractException(
                    "SeasonalOperationsCampaignReplayRecordInvalid");
            lock (gate)
            {
                if (state.CampaignRevision != Revision + 1)
                    throw new SimulationConflictException(
                        "SeasonalOperationsCampaignReplayRevisionMismatch");
                Revision++;
                seasonalOperationsCampaignState =
                    CloneSeasonalOperationsCampaignState(state);
                appliedSeasonalOperationsCampaignCommands.Add(commandId.Trim(),
                    new 적용된절기운영CampaignCommand(
                        commandSignature.Trim(), state));
                AppendSeasonalOperationsCampaignTransition(commandId,
                    commandSignature, state);
            }
        }

        private void AppendSeasonalOperationsCampaignTransition(
            string commandId, string commandSignature,
            Simulation절기운영CampaignStateSnapshot state)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes
                    .SeasonalOperationsCampaignStateTransition,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                SeasonalOperationsCampaignCommandId = commandId.Trim(),
                SeasonalOperationsCampaignCommandSignature =
                    commandSignature.Trim(),
                SeasonalOperationsCampaignState =
                    CloneSeasonalOperationsCampaignState(state),
            });

        private Simulation절기운영CampaignAdvancePreviewSnapshot
            BuildSeasonalOperationsCampaignAdvancePreview(
                Simulation절기운영CampaignAdvancePreviewRequest request)
        {
            var state = RequireActiveSeasonalOperationsCampaign(
                request.ExpectedRevision);
            if (!string.Equals(request.ExpectedPhaseCode?.Trim(),
                    state.CurrentPhaseCode, StringComparison.Ordinal))
                throw new SimulationConflictException(
                    "SeasonalOperationsCampaignPhaseMismatch");
            var phase = state.Phases[state.CurrentPhaseOrdinal - 1];
            var conditions = NormalizeCodes(request.SatisfiedConditionCodes,
                "SeasonalOperationsCampaignConditionCodeInvalid");
            var sources = NormalizeSourceRevisions(request.SourceRevisions);
            var sourceCodes = new HashSet<string>(
                sources.Select(value => value.SourceCode), StringComparer.Ordinal);
            var conditionCodes = new HashSet<string>(conditions,
                StringComparer.Ordinal);
            var blocked = new List<string>();
            if (CurrentTick - state.PhaseStartedAtTick
                < phase.MinimumElapsedTicks)
                blocked.Add("SeasonalOperationsCampaignMinimumTicksPending");
            if (phase.RequiredConditionCodes.Any(code =>
                    !conditionCodes.Contains(code)))
                blocked.Add("SeasonalOperationsCampaignConditionPending");
            if (phase.RequiredSourceCodes.Any(code =>
                    !sourceCodes.Contains(code)))
                blocked.Add("SeasonalOperationsCampaignSourceRevisionMissing");

            var completes = state.CurrentPhaseOrdinal == state.PhaseCount;
            var nextPhase = completes
                ? string.Empty
                : state.Phases[state.CurrentPhaseOrdinal].PhaseCode;
            var previewHash = CalculateAdvancePreviewHash(state, request,
                conditions, sources, nextPhase, completes, blocked);
            return new Simulation절기운영CampaignAdvancePreviewSnapshot
            {
                CampaignStableId = state.CampaignStableId,
                CampaignRevision = state.CampaignRevision,
                CurrentPhaseCode = state.CurrentPhaseCode,
                NextPhaseCode = nextPhase,
                CompletesCampaign = completes,
                CanConfirm = blocked.Count == 0,
                BlockReasonCodes = blocked.ToArray(),
                PreviewHash = previewHash,
                SourceRevisions = CloneSources(sources),
            };
        }

        private Simulation절기운영CampaignStateSnapshot
            RequireActiveSeasonalOperationsCampaign(long expectedRevision)
        {
            if (expectedRevision != Revision)
                throw new SimulationConflictException(
                    "SimulationExpectedRevisionMismatch");
            if (seasonalOperationsCampaignState == null
                || !string.Equals(seasonalOperationsCampaignState.StateCode,
                    Simulation절기운영CampaignCodes.Active,
                    StringComparison.Ordinal))
                throw new SimulationConflictException(
                    "SeasonalOperationsCampaignNotActive");
            return seasonalOperationsCampaignState;
        }

        private bool TryGetAppliedCampaignCommand(string commandId,
            string signature,
            out Simulation절기운영CampaignStateSnapshot state)
        {
            if (appliedSeasonalOperationsCampaignCommands.TryGetValue(commandId,
                    out var applied))
            {
                if (!string.Equals(applied.Signature, signature,
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SeasonalOperationsCampaignCommandPayloadConflict");
                state = CloneSeasonalOperationsCampaignState(applied.State)!;
                return true;
            }
            state = null!;
            return false;
        }

        private Simulation절기운영CampaignEventSnapshot CampaignEvent(
            string eventCode, string fromPhaseCode, string toPhaseCode,
            string commandId)
            => new Simulation절기운영CampaignEventSnapshot
            {
                EventCode = eventCode,
                FromPhaseCode = fromPhaseCode,
                ToPhaseCode = toPhaseCode,
                WorldTick = CurrentTick,
                WorldRevision = Revision,
                CommandId = commandId.Trim(),
            };

        private static Simulation절기운영CampaignDefinitionSnapshot
            ValidateAndCloneDefinition(
                Simulation절기운영CampaignDefinitionSnapshot? source)
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.CampaignStableId)
                || string.IsNullOrWhiteSpace(source.DefinitionRevision)
                || source.Phases == null || source.Phases.Length < 2)
                throw new SimulationContractException(
                    "SeasonalOperationsCampaignDefinitionInvalid");
            var phases = source.Phases.Select(value =>
            {
                if (value == null || string.IsNullOrWhiteSpace(value.PhaseCode)
                    || value.MinimumElapsedTicks < 0)
                    throw new SimulationContractException(
                        "SeasonalOperationsCampaignPhaseDefinitionInvalid");
                return new Simulation절기운영CampaignPhaseDefinition
                {
                    PhaseCode = value.PhaseCode.Trim(),
                    MinimumElapsedTicks = value.MinimumElapsedTicks,
                    RequiredConditionCodes = NormalizeCodes(
                        value.RequiredConditionCodes,
                        "SeasonalOperationsCampaignConditionCodeInvalid"),
                    RequiredSourceCodes = NormalizeCodes(
                        value.RequiredSourceCodes,
                        "SeasonalOperationsCampaignSourceCodeInvalid"),
                };
            }).ToArray();
            if (phases.Select(value => value.PhaseCode)
                .Distinct(StringComparer.Ordinal).Count() != phases.Length)
                throw new SimulationContractException(
                    "SeasonalOperationsCampaignPhaseDuplicate");
            return new Simulation절기운영CampaignDefinitionSnapshot
            {
                CampaignStableId = source.CampaignStableId.Trim(),
                DefinitionRevision = source.DefinitionRevision.Trim(),
                Phases = phases,
            };
        }

        private static Simulation절기운영CampaignSourceRevision[]
            NormalizeSourceRevisions(
                Simulation절기운영CampaignSourceRevision[]? values)
        {
            var normalized = (values ??
                    Array.Empty<Simulation절기운영CampaignSourceRevision>())
                .Select(value =>
                {
                    if (value == null || string.IsNullOrWhiteSpace(value.SourceCode)
                        || string.IsNullOrWhiteSpace(value.Revision))
                        throw new SimulationContractException(
                            "SeasonalOperationsCampaignSourceRevisionInvalid");
                    return new Simulation절기운영CampaignSourceRevision
                    {
                        SourceCode = value.SourceCode.Trim(),
                        Revision = value.Revision.Trim(),
                    };
                })
                .OrderBy(value => value.SourceCode, StringComparer.Ordinal)
                .ToArray();
            if (normalized.Select(value => value.SourceCode)
                .Distinct(StringComparer.Ordinal).Count() != normalized.Length)
                throw new SimulationContractException(
                    "SeasonalOperationsCampaignSourceDuplicate");
            return normalized;
        }

        private static string[] NormalizeCodes(string[]? values,
            string errorCode)
        {
            var normalized = (values ?? Array.Empty<string>())
                .Select(value => value?.Trim() ?? string.Empty).ToArray();
            if (normalized.Any(value => value.Length == 0))
                throw new SimulationContractException(errorCode);
            return normalized.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static void ValidateCampaignCommandId(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new SimulationContractException(
                    "SeasonalOperationsCampaignCommandIdRequired");
        }

        private string StartCommandSignature(
            Simulation절기운영CampaignStartRequest request,
            Simulation절기운영CampaignDefinitionSnapshot definition,
            Simulation절기운영CampaignSourceRevision[] sources)
            => HashSeasonalCampaignValue(string.Join("|",
                "start", request.ExpectedRevision, request.AreaStableId.Trim(),
                DefinitionSignature(definition), SourceSignature(sources)));

        private static string ConfirmCommandSignature(
            Simulation절기운영CampaignAdvanceConfirmRequest request)
            => HashSeasonalCampaignValue(string.Join("|", "confirm",
                request.ExpectedPreviewHash.Trim(),
                request.PreviewRequest.ExpectedRevision,
                request.PreviewRequest.ExpectedPhaseCode?.Trim() ?? string.Empty,
                string.Join(",", NormalizeCodes(
                    request.PreviewRequest.SatisfiedConditionCodes,
                    "SeasonalOperationsCampaignConditionCodeInvalid")),
                SourceSignature(NormalizeSourceRevisions(
                    request.PreviewRequest.SourceRevisions))));

        private string CalculateAdvancePreviewHash(
            Simulation절기운영CampaignStateSnapshot state,
            Simulation절기운영CampaignAdvancePreviewRequest request,
            string[] conditions,
            Simulation절기운영CampaignSourceRevision[] sources,
            string nextPhase,
            bool completes,
            IEnumerable<string> blocked)
            => HashSeasonalCampaignValue(string.Join("|",
                SessionStableId, ScenarioSeed,
                Simulation절기운영CampaignCodes.RuleRevision,
                state.CampaignStableId, state.DefinitionRevision,
                state.AreaStableId, state.CampaignRevision,
                request.ExpectedRevision, state.CurrentPhaseCode,
                state.CurrentPhaseOrdinal, CurrentTick, nextPhase, completes,
                string.Join(",", conditions), SourceSignature(sources),
                string.Join(",", blocked)));

        private static string DefinitionSignature(
            Simulation절기운영CampaignDefinitionSnapshot definition)
            => string.Join("|", definition.CampaignStableId,
                definition.DefinitionRevision,
                string.Join(";", definition.Phases.Select(value => string.Join(",",
                    value.PhaseCode, value.MinimumElapsedTicks,
                    string.Join("+", value.RequiredConditionCodes),
                    string.Join("+", value.RequiredSourceCodes)))));

        private static string SourceSignature(
            IEnumerable<Simulation절기운영CampaignSourceRevision> sources)
            => string.Join(";", sources.Select(value =>
                value.SourceCode + "=" + value.Revision));

        private static string HashSeasonalCampaignValue(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var item in bytes) builder.Append(item.ToString("x2"));
            return builder.ToString();
        }

        internal static Simulation절기운영CampaignStateSnapshot?
            CloneSeasonalOperationsCampaignState(
                Simulation절기운영CampaignStateSnapshot? source)
            => source == null ? null : new Simulation절기운영CampaignStateSnapshot
            {
                SchemaVersion = source.SchemaVersion,
                RuleRevision = source.RuleRevision,
                CampaignStableId = source.CampaignStableId,
                DefinitionRevision = source.DefinitionRevision,
                AreaStableId = source.AreaStableId,
                StateCode = source.StateCode,
                CurrentPhaseCode = source.CurrentPhaseCode,
                CurrentPhaseOrdinal = source.CurrentPhaseOrdinal,
                PhaseCount = source.PhaseCount,
                StartedAtTick = source.StartedAtTick,
                PhaseStartedAtTick = source.PhaseStartedAtTick,
                CampaignRevision = source.CampaignRevision,
                Phases = ClonePhases(source.Phases),
                SourceRevisions = CloneSources(source.SourceRevisions),
                AvailableActions = (source.AvailableActions
                    ?? Array.Empty<string>()).ToArray(),
                Events = (source.Events
                    ?? Array.Empty<Simulation절기운영CampaignEventSnapshot>())
                    .Select(value => new Simulation절기운영CampaignEventSnapshot
                    {
                        EventCode = value.EventCode,
                        FromPhaseCode = value.FromPhaseCode,
                        ToPhaseCode = value.ToPhaseCode,
                        WorldTick = value.WorldTick,
                        WorldRevision = value.WorldRevision,
                        CommandId = value.CommandId,
                    }).ToArray(),
                SimulationOnly = source.SimulationOnly,
                IsOperationalState = source.IsOperationalState,
            };

        internal static string BuildSeasonalOperationsCampaignStatePayloadKey(
            Simulation절기운영CampaignStateSnapshot? state)
        {
            if (state == null) return string.Empty;
            return string.Join("|", state.SchemaVersion, state.RuleRevision,
                state.CampaignStableId, state.DefinitionRevision,
                state.AreaStableId, state.StateCode, state.CurrentPhaseCode,
                state.CurrentPhaseOrdinal, state.PhaseCount,
                state.StartedAtTick, state.PhaseStartedAtTick,
                state.CampaignRevision,
                string.Join(";", (state.Phases ??
                    Array.Empty<Simulation절기운영CampaignPhaseDefinition>())
                    .Select(value => string.Join(",", value.PhaseCode,
                        value.MinimumElapsedTicks,
                        string.Join("+", value.RequiredConditionCodes ??
                            Array.Empty<string>()),
                        string.Join("+", value.RequiredSourceCodes ??
                            Array.Empty<string>())))),
                SourceSignature(state.SourceRevisions ??
                    Array.Empty<Simulation절기운영CampaignSourceRevision>()),
                string.Join(",", state.AvailableActions ?? Array.Empty<string>()),
                string.Join(";", (state.Events ??
                    Array.Empty<Simulation절기운영CampaignEventSnapshot>())
                    .Select(value => string.Join(",", value.EventCode,
                        value.FromPhaseCode, value.ToPhaseCode,
                        value.WorldTick, value.WorldRevision, value.CommandId))),
                state.SimulationOnly, state.IsOperationalState);
        }

        private static Simulation절기운영CampaignPhaseDefinition[] ClonePhases(
            IEnumerable<Simulation절기운영CampaignPhaseDefinition> values)
            => (values ?? Array.Empty<Simulation절기운영CampaignPhaseDefinition>())
                .Select(value => new Simulation절기운영CampaignPhaseDefinition
                {
                    PhaseCode = value.PhaseCode,
                    MinimumElapsedTicks = value.MinimumElapsedTicks,
                    RequiredConditionCodes = (value.RequiredConditionCodes
                        ?? Array.Empty<string>()).ToArray(),
                    RequiredSourceCodes = (value.RequiredSourceCodes
                        ?? Array.Empty<string>()).ToArray(),
                }).ToArray();

        private static Simulation절기운영CampaignSourceRevision[] CloneSources(
            IEnumerable<Simulation절기운영CampaignSourceRevision> values)
            => (values ?? Array.Empty<Simulation절기운영CampaignSourceRevision>())
                .Select(value => new Simulation절기운영CampaignSourceRevision
                {
                    SourceCode = value.SourceCode,
                    Revision = value.Revision,
                }).ToArray();

        private static Simulation절기운영CampaignEventSnapshot[]
            AppendCampaignEvent(
                IEnumerable<Simulation절기운영CampaignEventSnapshot> values,
                Simulation절기운영CampaignEventSnapshot next)
            => (values ?? Array.Empty<Simulation절기운영CampaignEventSnapshot>())
                .Concat(new[] { next }).ToArray();

        private sealed class 적용된절기운영CampaignCommand
        {
            public 적용된절기운영CampaignCommand(string signature,
                Simulation절기운영CampaignStateSnapshot state)
            {
                Signature = signature;
                State = CloneSeasonalOperationsCampaignState(state)!;
            }

            public string Signature { get; }
            public Simulation절기운영CampaignStateSnapshot State { get; }
        }
    }
}
