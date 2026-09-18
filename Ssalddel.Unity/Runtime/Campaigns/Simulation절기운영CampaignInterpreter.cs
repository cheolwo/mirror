using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Data.Campaigns
{
    /// <summary>
    /// 서버가 확정한 절기 운영 Campaign 상태를 검증하고 마지막 정상 사본을
    /// 메모리에만 보존합니다. 구간이나 허용 조작을 Unity에서 다시 계산하지 않습니다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "절기 운영 Campaign의 schema·Session·지역·revision을 검증하고 읽기 전용 상태 사본을 유지한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2Unity권위Client,
        Boundary = "Unity 표현용 메모리 상태이며 Campaign 전이·운영 업무·저장 재생 권위를 갖지 않는다.")]
    public sealed class Simulation절기운영CampaignInterpreter
    {
        private readonly object gate = new object();
        private string currentSessionStableId = string.Empty;
        private string currentAreaStableId = string.Empty;
        private string currentFingerprint = string.Empty;
        private Simulation절기운영CampaignStateSnapshot? current;

        public Simulation절기운영CampaignApplyResult Apply(
            string sessionStableId,
            string expectedAreaStableId,
            Simulation절기운영CampaignStateSnapshot incoming)
        {
            if (incoming == null) throw new ArgumentNullException(nameof(incoming));
            var session = NormalizeRequired(sessionStableId,
                "SeasonalOperationsCampaignSessionRequired");
            var area = NormalizeRequired(expectedAreaStableId,
                "SeasonalOperationsCampaignAreaRequired");

            lock (gate)
            {
                var error = Validate(session, area, incoming);
                if (!string.IsNullOrEmpty(error)) return Snapshot(false, error);

                var fingerprint = Fingerprint(incoming);
                if (current != null)
                {
                    if (!string.Equals(currentSessionStableId, session,
                            StringComparison.Ordinal))
                        return Snapshot(false,
                            "SeasonalOperationsCampaignSessionMismatch");
                    if (!string.Equals(currentAreaStableId, area,
                            StringComparison.Ordinal))
                        return Snapshot(false,
                            "SeasonalOperationsCampaignAreaMismatch");
                    if (incoming.CampaignRevision < current.CampaignRevision)
                        return Snapshot(false,
                            "SeasonalOperationsCampaignRevisionStale");
                    if (incoming.CampaignRevision == current.CampaignRevision
                        && !string.Equals(currentFingerprint, fingerprint,
                            StringComparison.Ordinal))
                        return Snapshot(false,
                            "SeasonalOperationsCampaignRevisionConflict");
                }

                currentSessionStableId = session;
                currentAreaStableId = area;
                currentFingerprint = fingerprint;
                current = Clone(incoming);
                return Snapshot(true, string.Empty);
            }
        }

        public Simulation절기운영CampaignApplyResult RetainFailure(
            string errorCode)
        {
            var error = NormalizeRequired(errorCode,
                "SeasonalOperationsCampaignRefreshFailed");
            lock (gate) return Snapshot(false, error);
        }

        public Simulation절기운영CampaignApplyResult Current()
        {
            lock (gate) return Snapshot(current != null, string.Empty);
        }

        public void Clear()
        {
            lock (gate)
            {
                currentSessionStableId = string.Empty;
                currentAreaStableId = string.Empty;
                currentFingerprint = string.Empty;
                current = null;
            }
        }

        private string Validate(string session, string area,
            Simulation절기운영CampaignStateSnapshot value)
        {
            if (!string.Equals(value.SchemaVersion,
                    Simulation절기운영CampaignCodes.SchemaVersion,
                    StringComparison.Ordinal))
                return "SeasonalOperationsCampaignSchemaVersionUnsupported";
            if (!string.Equals(value.RuleRevision,
                    Simulation절기운영CampaignCodes.RuleRevision,
                    StringComparison.Ordinal))
                return "SeasonalOperationsCampaignRuleRevisionUnsupported";
            if (string.IsNullOrWhiteSpace(value.CampaignStableId)
                || string.IsNullOrWhiteSpace(value.DefinitionRevision)
                || value.CampaignRevision < 1)
                return "SeasonalOperationsCampaignIdentityInvalid";
            if (!string.Equals(value.AreaStableId?.Trim(), area,
                    StringComparison.Ordinal))
                return "SeasonalOperationsCampaignAreaMismatch";
            if (current != null && (!string.Equals(currentSessionStableId,
                    session, StringComparison.Ordinal)
                || !string.Equals(current.CampaignStableId,
                    value.CampaignStableId, StringComparison.Ordinal)))
                return "SeasonalOperationsCampaignSessionMismatch";
            if (!value.SimulationOnly || value.IsOperationalState)
                return "SeasonalOperationsCampaignAuthorityBoundaryInvalid";
            if (!string.Equals(value.StateCode,
                    Simulation절기운영CampaignCodes.Active,
                    StringComparison.Ordinal)
                && !string.Equals(value.StateCode,
                    Simulation절기운영CampaignCodes.Completed,
                    StringComparison.Ordinal))
                return "SeasonalOperationsCampaignStateInvalid";

            var phases = value.Phases
                ?? Array.Empty<Simulation절기운영CampaignPhaseDefinition>();
            if (phases.Length < 2 || value.PhaseCount != phases.Length
                || value.CurrentPhaseOrdinal < 1
                || value.CurrentPhaseOrdinal > phases.Length
                || phases.Any(phase => phase == null
                    || string.IsNullOrWhiteSpace(phase.PhaseCode))
                || phases.Select(phase => phase.PhaseCode)
                    .Distinct(StringComparer.Ordinal).Count() != phases.Length
                || !string.Equals(phases[value.CurrentPhaseOrdinal - 1]
                        .PhaseCode, value.CurrentPhaseCode,
                    StringComparison.Ordinal))
                return "SeasonalOperationsCampaignPhaseInvalid";

            var sources = value.SourceRevisions
                ?? Array.Empty<Simulation절기운영CampaignSourceRevision>();
            if (sources.Any(source => source == null
                    || string.IsNullOrWhiteSpace(source.SourceCode)
                    || string.IsNullOrWhiteSpace(source.Revision))
                || sources.Select(source => source.SourceCode)
                    .Distinct(StringComparer.Ordinal).Count() != sources.Length)
                return "SeasonalOperationsCampaignSourceRevisionInvalid";
            if (string.Equals(value.StateCode,
                    Simulation절기운영CampaignCodes.Completed,
                    StringComparison.Ordinal)
                && (value.AvailableActions?.Length ?? 0) != 0)
                return "SeasonalOperationsCampaignCompletedActionInvalid";
            return string.Empty;
        }

        private Simulation절기운영CampaignApplyResult Snapshot(
            bool accepted, string errorCode)
            => new Simulation절기운영CampaignApplyResult
            {
                Accepted = accepted,
                RetainedLastSuccessful = !accepted && current != null,
                ErrorCode = errorCode,
                SessionStableId = currentSessionStableId,
                AreaStableId = currentAreaStableId,
                State = Clone(current),
            };

        private static string NormalizeRequired(string value, string errorCode)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(errorCode);
            return value.Trim();
        }

        private static string Fingerprint(
            Simulation절기운영CampaignStateSnapshot state)
        {
            var canonical = string.Join("|", state.SchemaVersion,
                state.RuleRevision, state.CampaignStableId,
                state.DefinitionRevision, state.AreaStableId, state.StateCode,
                state.CurrentPhaseCode, state.CurrentPhaseOrdinal,
                state.PhaseCount, state.StartedAtTick, state.PhaseStartedAtTick,
                state.CampaignRevision,
                string.Join(";", (state.Phases ?? Array.Empty<
                    Simulation절기운영CampaignPhaseDefinition>()).Select(
                    phase => string.Join(",", phase.PhaseCode,
                        phase.MinimumElapsedTicks,
                        string.Join("+", phase.RequiredConditionCodes
                            ?? Array.Empty<string>()),
                        string.Join("+", phase.RequiredSourceCodes
                            ?? Array.Empty<string>())))),
                string.Join(";", (state.SourceRevisions ?? Array.Empty<
                    Simulation절기운영CampaignSourceRevision>()).Select(
                    source => source.SourceCode + "=" + source.Revision)),
                string.Join(",", state.AvailableActions
                    ?? Array.Empty<string>()));
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var result = new StringBuilder(bytes.Length * 2);
            foreach (var item in bytes) result.Append(item.ToString("x2"));
            return result.ToString();
        }

        private static Simulation절기운영CampaignStateSnapshot? Clone(
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
                Phases = (source.Phases ?? Array.Empty<
                    Simulation절기운영CampaignPhaseDefinition>()).Select(
                    phase => new Simulation절기운영CampaignPhaseDefinition
                    {
                        PhaseCode = phase.PhaseCode,
                        MinimumElapsedTicks = phase.MinimumElapsedTicks,
                        RequiredConditionCodes = (phase.RequiredConditionCodes
                            ?? Array.Empty<string>()).ToArray(),
                        RequiredSourceCodes = (phase.RequiredSourceCodes
                            ?? Array.Empty<string>()).ToArray(),
                    }).ToArray(),
                SourceRevisions = (source.SourceRevisions ?? Array.Empty<
                    Simulation절기운영CampaignSourceRevision>()).Select(
                    value => new Simulation절기운영CampaignSourceRevision
                    {
                        SourceCode = value.SourceCode,
                        Revision = value.Revision,
                    }).ToArray(),
                AvailableActions = (source.AvailableActions
                    ?? Array.Empty<string>()).ToArray(),
                Events = (source.Events ?? Array.Empty<
                    Simulation절기운영CampaignEventSnapshot>()).Select(
                    value => new Simulation절기운영CampaignEventSnapshot
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
    }
}
