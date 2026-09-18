#if UNITY_5_3_OR_NEWER
using System;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Campaigns;
using UnityEngine;

namespace Ssalddel.Unity.OperationalTransport
{
    public sealed class UnityJsonSimulation절기운영CampaignDecoder
        : ISimulation절기운영CampaignDecoder
    {
        public Simulation절기운영CampaignStateSnapshot Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "SeasonalOperationsCampaignJsonEmpty", nameof(json));
            var wire = JsonUtility.FromJson<SeasonalCampaignStateWire>(json);
            if (wire == null)
                throw new FormatException(
                    "SeasonalOperationsCampaignJsonInvalid");
            return wire.ToContract();
        }
    }

    [Serializable]
    internal sealed class SeasonalCampaignStateWire
    {
        public string schemaVersion = string.Empty;
        public string ruleRevision = string.Empty;
        public string campaignStableId = string.Empty;
        public string definitionRevision = string.Empty;
        public string areaStableId = string.Empty;
        public string stateCode = string.Empty;
        public string currentPhaseCode = string.Empty;
        public int currentPhaseOrdinal;
        public int phaseCount;
        public int startedAtTick;
        public int phaseStartedAtTick;
        public long campaignRevision;
        public SeasonalCampaignPhaseWire[] phases =
            Array.Empty<SeasonalCampaignPhaseWire>();
        public SeasonalCampaignSourceWire[] sourceRevisions =
            Array.Empty<SeasonalCampaignSourceWire>();
        public string[] availableActions = Array.Empty<string>();
        public SeasonalCampaignEventWire[] events =
            Array.Empty<SeasonalCampaignEventWire>();
        public bool simulationOnly;
        public bool isOperationalState;

        public Simulation절기운영CampaignStateSnapshot ToContract()
            => new Simulation절기운영CampaignStateSnapshot
            {
                SchemaVersion = schemaVersion,
                RuleRevision = ruleRevision,
                CampaignStableId = campaignStableId,
                DefinitionRevision = definitionRevision,
                AreaStableId = areaStableId,
                StateCode = stateCode,
                CurrentPhaseCode = currentPhaseCode,
                CurrentPhaseOrdinal = currentPhaseOrdinal,
                PhaseCount = phaseCount,
                StartedAtTick = startedAtTick,
                PhaseStartedAtTick = phaseStartedAtTick,
                CampaignRevision = campaignRevision,
                Phases = Array.ConvertAll(phases ?? Array.Empty<
                    SeasonalCampaignPhaseWire>(), item => item.ToContract()),
                SourceRevisions = Array.ConvertAll(sourceRevisions
                    ?? Array.Empty<SeasonalCampaignSourceWire>(),
                    item => item.ToContract()),
                AvailableActions = availableActions ?? Array.Empty<string>(),
                Events = Array.ConvertAll(events ?? Array.Empty<
                    SeasonalCampaignEventWire>(), item => item.ToContract()),
                SimulationOnly = simulationOnly,
                IsOperationalState = isOperationalState,
            };
    }

    [Serializable]
    internal sealed class SeasonalCampaignPhaseWire
    {
        public string phaseCode = string.Empty;
        public int minimumElapsedTicks;
        public string[] requiredConditionCodes = Array.Empty<string>();
        public string[] requiredSourceCodes = Array.Empty<string>();

        public Simulation절기운영CampaignPhaseDefinition ToContract()
            => new Simulation절기운영CampaignPhaseDefinition
            {
                PhaseCode = phaseCode,
                MinimumElapsedTicks = minimumElapsedTicks,
                RequiredConditionCodes = requiredConditionCodes
                    ?? Array.Empty<string>(),
                RequiredSourceCodes = requiredSourceCodes
                    ?? Array.Empty<string>(),
            };
    }

    [Serializable]
    internal sealed class SeasonalCampaignSourceWire
    {
        public string sourceCode = string.Empty;
        public string revision = string.Empty;

        public Simulation절기운영CampaignSourceRevision ToContract()
            => new Simulation절기운영CampaignSourceRevision
            {
                SourceCode = sourceCode,
                Revision = revision,
            };
    }

    [Serializable]
    internal sealed class SeasonalCampaignEventWire
    {
        public string eventCode = string.Empty;
        public string fromPhaseCode = string.Empty;
        public string toPhaseCode = string.Empty;
        public int worldTick;
        public long worldRevision;
        public string commandId = string.Empty;

        public Simulation절기운영CampaignEventSnapshot ToContract()
            => new Simulation절기운영CampaignEventSnapshot
            {
                EventCode = eventCode,
                FromPhaseCode = fromPhaseCode,
                ToPhaseCode = toPhaseCode,
                WorldTick = worldTick,
                WorldRevision = worldRevision,
                CommandId = commandId,
            };
    }
}
#endif
