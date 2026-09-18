using System;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Application
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "절기 운영 캠페인의 조회·시작·Preview·Confirm을 권위 세션에 위임한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행,
        Boundary = "Application 조율이며 규칙을 복제하거나 Unity에 상태 권위를 주지 않는다.")]
    public sealed class Simulation절기운영CampaignService
    {
        private readonly 경영SimulationSessionAccessor sessions;

        public Simulation절기운영CampaignService(
            경영SimulationSessionAccessor sessionAccessor)
        {
            sessions = sessionAccessor
                ?? throw new ArgumentNullException(nameof(sessionAccessor));
        }

        public Simulation절기운영CampaignStateSnapshot Get(
            string sessionStableId)
            => sessions.Require(sessionStableId)
                .GetSeasonalOperationsCampaignState();

        public Simulation절기운영CampaignStateSnapshot Begin(
            string sessionStableId,
            Simulation절기운영CampaignStartRequest request)
            => sessions.Require(sessionStableId)
                .BeginSeasonalOperationsCampaign(request);

        public Simulation절기운영CampaignAdvancePreviewSnapshot PreviewAdvance(
            string sessionStableId,
            Simulation절기운영CampaignAdvancePreviewRequest request)
            => sessions.Require(sessionStableId)
                .PreviewSeasonalOperationsCampaignAdvance(request);

        public Simulation절기운영CampaignStateSnapshot ConfirmAdvance(
            string sessionStableId,
            Simulation절기운영CampaignAdvanceConfirmRequest request)
            => sessions.Require(sessionStableId)
                .ConfirmSeasonalOperationsCampaignAdvance(request);
    }
}
