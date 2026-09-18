using System;

namespace Ssalddel.Unity.Campaigns
{
    public static class SeasonalCampaignObservationSourceCodes
    {
        public const string Campaign = "Campaign";
        public const string Diorama = "Diorama";
        public const string Operations = "Operations";
        public const string Cards = "Cards";
        public const string Npc = "Npc";
    }

    public static class SeasonalCampaignSourceFreshnessCodes
    {
        public const string Fresh = "Fresh";
        public const string RefreshDelayed = "RefreshDelayed";
        public const string Missing = "Missing";
    }

    public sealed class SeasonalCampaignObservationSourceUpdate
    {
        public string SourceCode { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public long RevisionOrdinal { get; set; }
        public DateTime SuccessfulAtUtc { get; set; }
        public bool Succeeded { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
    }

    public sealed class SeasonalCampaignSourceDiagnostic
    {
        public string SourceCode { get; set; } = string.Empty;
        public string LastSuccessfulRevision { get; set; } = string.Empty;
        public long LastSuccessfulRevisionOrdinal { get; set; }
        public DateTime LastSuccessfulAtUtc { get; set; }
        public string FreshnessCode { get; set; } =
            SeasonalCampaignSourceFreshnessCodes.Missing;
        public string ErrorCode { get; set; } = string.Empty;
    }

    public sealed class SeasonalCampaignCompositionRevision
    {
        public string SourceCode { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public long RevisionOrdinal { get; set; }
    }

    public sealed class SeasonalCampaignScreenModel
    {
        public string SessionStableId { get; set; } = string.Empty;
        public string AreaStableId { get; set; } = string.Empty;
        public bool HasCampaign { get; set; }
        public string CampaignStableId { get; set; } = string.Empty;
        public string CampaignStateCode { get; set; } = string.Empty;
        public string CurrentPhaseCode { get; set; } = string.Empty;
        public long CampaignRevision { get; set; }
        public string[] AvailableActions { get; set; } = Array.Empty<string>();
        public string[] RequiredSourceCodes { get; set; } =
            Array.Empty<string>();
        public string[] DelayedRequiredSourceCodes { get; set; } =
            Array.Empty<string>();
        public bool ActionsDisabled { get; set; }
        public string[] DisabledActionReasons { get; set; } =
            Array.Empty<string>();
        public bool PresentationOnly { get; set; } = true;
    }

    public sealed class SeasonalCampaignObservationResult
    {
        public bool Accepted { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public SeasonalCampaignScreenModel ScreenModel { get; set; } =
            new SeasonalCampaignScreenModel();
        public SeasonalCampaignSourceDiagnostic[] SourceDiagnostics
            { get; set; } = Array.Empty<SeasonalCampaignSourceDiagnostic>();
        public SeasonalCampaignCompositionRevision[] CompositionRevisionSet
            { get; set; } = Array.Empty<SeasonalCampaignCompositionRevision>();
    }
}
