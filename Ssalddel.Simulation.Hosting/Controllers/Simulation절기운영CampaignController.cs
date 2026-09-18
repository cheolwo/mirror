using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/seasonal-operations-campaign")]
[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E2,
    "절기 운영 캠페인 조회·Preview·Confirm 요청을 권위 Application 서비스에 전달한다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter,
    Boundary = "HTTP 어댑터이며 캠페인 시작·구간 판정·운영 업무 변경을 자체 수행하지 않는다.")]
public sealed class Simulation절기운영CampaignController(
    Simulation절기운영CampaignService service) : SimulationApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Simulation절기운영CampaignStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation절기운영CampaignStateSnapshot> Get(
        string sessionStableId)
        => Execute<Simulation절기운영CampaignStateSnapshot>(() =>
            Ok(service.Get(sessionStableId)));

    [HttpPost("advance-previews")]
    [ProducesResponseType(
        typeof(Simulation절기운영CampaignAdvancePreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation절기운영CampaignAdvancePreviewSnapshot>
        PreviewAdvance(
            string sessionStableId,
            [FromBody] Simulation절기운영CampaignAdvancePreviewRequest request)
        => Execute<Simulation절기운영CampaignAdvancePreviewSnapshot>(() =>
            Ok(service.PreviewAdvance(sessionStableId, request)));

    [HttpPost("advance-commands")]
    [ProducesResponseType(typeof(Simulation절기운영CampaignStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<Simulation절기운영CampaignStateSnapshot>
        ConfirmAdvance(
            string sessionStableId,
            [FromBody] Simulation절기운영CampaignAdvanceConfirmRequest request)
        => Execute<Simulation절기운영CampaignStateSnapshot>(() =>
            Ok(service.ConfirmAdvance(sessionStableId, request)));
}
