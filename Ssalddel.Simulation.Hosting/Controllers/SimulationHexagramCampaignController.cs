using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/hexagram-campaign")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "HTTP 이야기 요청을 권위 서비스로 전달하고 계약·충돌·조회 오류를 응답한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter,
    Boundary = "HTTP 경계 구현이며 실제 서버 접속·인증 검증·플레이 완료는 별개다.")]
public sealed class SimulationHexagramCampaignController(
    SimulationHexagramCampaignService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SimulationHexagramCampaignStateSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationHexagramCampaignStateSnapshot> Get(
        string sessionStableId)
        => Execute(() => service.Get(sessionStableId));

    [HttpPost("enter")]
    public ActionResult<SimulationHexagramCampaignStateSnapshot> Enter(
        string sessionStableId,
        [FromBody] SimulationHexagramCampaignEnterRequest request)
        => Execute(() => service.Enter(sessionStableId, request));

    [HttpPost("line/complete")]
    public ActionResult<SimulationHexagramCampaignStateSnapshot> CompleteLine(
        string sessionStableId,
        [FromBody] SimulationHexagramCampaignLineCompleteRequest request)
        => Execute(() => service.CompleteLine(sessionStableId, request));

    [HttpPost("setback")]
    public ActionResult<SimulationHexagramCampaignStateSnapshot> Setback(
        string sessionStableId,
        [FromBody] SimulationHexagramCampaignSetbackRequest request)
        => Execute(() => service.RecordSetback(sessionStableId, request));

    [HttpPost("fail")]
    public ActionResult<SimulationHexagramCampaignStateSnapshot> Fail(
        string sessionStableId,
        [FromBody] SimulationHexagramCampaignFailureRequest request)
        => Execute(() => service.Fail(sessionStableId, request));

    [HttpPost("complete")]
    public ActionResult<SimulationHexagramCampaignStateSnapshot> Complete(
        string sessionStableId,
        [FromBody] SimulationHexagramCampaignCompleteRequest request)
        => Execute(() => service.Complete(sessionStableId, request));

    private ActionResult<T> Execute<T>(Func<T> action)
    {
        try { return Ok(action()); }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse
                { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse
                { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse
                { ErrorCode = error.ErrorCode });
        }
    }
}
