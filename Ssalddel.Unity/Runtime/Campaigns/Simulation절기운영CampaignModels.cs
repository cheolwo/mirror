using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Data.Campaigns
{
    public sealed class Simulation절기운영CampaignApplyResult
    {
        public bool Accepted { get; set; }
        public bool RetainedLastSuccessful { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string SessionStableId { get; set; } = string.Empty;
        public string AreaStableId { get; set; } = string.Empty;
        public Simulation절기운영CampaignStateSnapshot? State { get; set; }
    }
}
