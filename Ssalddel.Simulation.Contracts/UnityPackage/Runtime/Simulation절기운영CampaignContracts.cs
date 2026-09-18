using System;

namespace Ssalddel.Simulation.Contracts
{
    public static class Simulation절기운영CampaignCodes
    {
        public const string SchemaVersion = "simulation-seasonal-operations-campaign.v1";
        public const string RuleRevision = "seasonal-operations-campaign.r1";
        public const string Active = "Active";
        public const string Completed = "Completed";
        public const string PreviewAdvance = "PreviewAdvance";
        public const string ConfirmAdvance = "ConfirmAdvance";
        public const string CampaignStarted = "CampaignStarted";
        public const string PhaseAdvanced = "PhaseAdvanced";
        public const string CampaignCompleted = "CampaignCompleted";
    }

    /// <summary>
    /// Campaign 국면이 전진 판단에 요구할 수 있는 읽기 원천의 공개 코드입니다.
    /// Unity 화면의 구성 요소 이름이 아니라 서버 Preview에 제출하는 판본 원천을
    /// 식별하며 기존 문자열과 호환됩니다.
    /// </summary>
    public static class Simulation절기운영CampaignSourceCodes
    {
        public const string AdministrativeDongDiorama =
            "AdministrativeDongDiorama";
        public const string OperationsScene = "OperationsScene";
        public const string CardWorkspace = "CardWorkspace";
        public const string NpcObservation = "NpcObservation";
    }

    public sealed class Simulation절기운영CampaignSourceRevision
    {
        public string SourceCode { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
    }

    public sealed class Simulation절기운영CampaignPhaseDefinition
    {
        public string PhaseCode { get; set; } = string.Empty;
        public int MinimumElapsedTicks { get; set; }
        public string[] RequiredConditionCodes { get; set; } = Array.Empty<string>();
        public string[] RequiredSourceCodes { get; set; } = Array.Empty<string>();
    }

    public sealed class Simulation절기운영CampaignDefinitionSnapshot
    {
        public string CampaignStableId { get; set; } = string.Empty;
        public string DefinitionRevision { get; set; } = string.Empty;
        public Simulation절기운영CampaignPhaseDefinition[] Phases { get; set; } =
            Array.Empty<Simulation절기운영CampaignPhaseDefinition>();
    }

    public sealed class Simulation절기운영CampaignStartRequest
    {
        public string CommandId { get; set; } = string.Empty;
        public long ExpectedRevision { get; set; }
        public string AreaStableId { get; set; } = string.Empty;
        public Simulation절기운영CampaignDefinitionSnapshot Definition { get; set; } =
            new Simulation절기운영CampaignDefinitionSnapshot();
        public Simulation절기운영CampaignSourceRevision[] SourceRevisions { get; set; } =
            Array.Empty<Simulation절기운영CampaignSourceRevision>();
    }

    public sealed class Simulation절기운영CampaignAdvancePreviewRequest
    {
        public long ExpectedRevision { get; set; }
        public string ExpectedPhaseCode { get; set; } = string.Empty;
        public string[] SatisfiedConditionCodes { get; set; } = Array.Empty<string>();
        public Simulation절기운영CampaignSourceRevision[] SourceRevisions { get; set; } =
            Array.Empty<Simulation절기운영CampaignSourceRevision>();
    }

    public sealed class Simulation절기운영CampaignAdvanceConfirmRequest
    {
        public string CommandId { get; set; } = string.Empty;
        public string ExpectedPreviewHash { get; set; } = string.Empty;
        public Simulation절기운영CampaignAdvancePreviewRequest PreviewRequest { get; set; } =
            new Simulation절기운영CampaignAdvancePreviewRequest();
    }

    public sealed class Simulation절기운영CampaignAdvancePreviewSnapshot
    {
        public string SchemaVersion { get; set; } =
            Simulation절기운영CampaignCodes.SchemaVersion;
        public string RuleRevision { get; set; } =
            Simulation절기운영CampaignCodes.RuleRevision;
        public string CampaignStableId { get; set; } = string.Empty;
        public long CampaignRevision { get; set; }
        public string CurrentPhaseCode { get; set; } = string.Empty;
        public string NextPhaseCode { get; set; } = string.Empty;
        public bool CompletesCampaign { get; set; }
        public bool CanConfirm { get; set; }
        public string[] BlockReasonCodes { get; set; } = Array.Empty<string>();
        public string PreviewHash { get; set; } = string.Empty;
        public Simulation절기운영CampaignSourceRevision[] SourceRevisions { get; set; } =
            Array.Empty<Simulation절기운영CampaignSourceRevision>();
    }

    public sealed class Simulation절기운영CampaignEventSnapshot
    {
        public string EventCode { get; set; } = string.Empty;
        public string FromPhaseCode { get; set; } = string.Empty;
        public string ToPhaseCode { get; set; } = string.Empty;
        public int WorldTick { get; set; }
        public long WorldRevision { get; set; }
        public string CommandId { get; set; } = string.Empty;
    }

    public sealed class Simulation절기운영CampaignStateSnapshot
    {
        public string SchemaVersion { get; set; } =
            Simulation절기운영CampaignCodes.SchemaVersion;
        public string RuleRevision { get; set; } =
            Simulation절기운영CampaignCodes.RuleRevision;
        public string CampaignStableId { get; set; } = string.Empty;
        public string DefinitionRevision { get; set; } = string.Empty;
        public string AreaStableId { get; set; } = string.Empty;
        public string StateCode { get; set; } = string.Empty;
        public string CurrentPhaseCode { get; set; } = string.Empty;
        public int CurrentPhaseOrdinal { get; set; }
        public int PhaseCount { get; set; }
        public int StartedAtTick { get; set; }
        public int PhaseStartedAtTick { get; set; }
        public long CampaignRevision { get; set; }
        public Simulation절기운영CampaignPhaseDefinition[] Phases { get; set; } =
            Array.Empty<Simulation절기운영CampaignPhaseDefinition>();
        public Simulation절기운영CampaignSourceRevision[] SourceRevisions { get; set; } =
            Array.Empty<Simulation절기운영CampaignSourceRevision>();
        public string[] AvailableActions { get; set; } = Array.Empty<string>();
        public Simulation절기운영CampaignEventSnapshot[] Events { get; set; } =
            Array.Empty<Simulation절기운영CampaignEventSnapshot>();
        public bool SimulationOnly { get; set; } = true;
        public bool IsOperationalState { get; set; }
    }
}
