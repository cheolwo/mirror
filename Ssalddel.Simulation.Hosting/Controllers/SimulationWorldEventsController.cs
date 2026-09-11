using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions/{sessionStableId}/world-events")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class SimulationWorldEventsController(
    SimulationWorldEventProjectionService service,
    SimulationRegionalIncidentService incidentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SimulationWorldEventProjectionSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationWorldEventProjectionSnapshot> GetChanges(
        string sessionStableId,
        [FromQuery] long afterWorldRevision = -1)
    {
        try
        {
            return Ok(service.GetChanges(sessionStableId, afterWorldRevision));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("{eventStableId}/response-previews")]
    [ProducesResponseType(typeof(SimulationRegionalIncidentResponsePreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationRegionalIncidentResponsePreviewSnapshot> PreviewResponse(
        string sessionStableId,
        string eventStableId,
        [FromBody] SimulationRegionalIncidentResponsePreviewRequest request)
    {
        try
        {
            return Ok(incidentService.Preview(sessionStableId, eventStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("{eventStableId}/responses/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmResponse(
        string sessionStableId,
        string eventStableId,
        [FromBody] SimulationRegionalIncidentResponseConfirmRequest request)
    {
        try
        {
            return Ok(incidentService.Confirm(sessionStableId, eventStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("nature-threat/observation-previews")]
    [ProducesResponseType(typeof(SimulationNatureThreatObservationPreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationNatureThreatObservationPreviewSnapshot>
        PreviewNatureThreatObservation(
            string sessionStableId,
            [FromBody] SimulationNatureThreatObservationPreviewRequest request)
    {
        try
        {
            return Ok(incidentService.PreviewThreatObservation(sessionStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("nature-threat/observations/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmNatureThreatObservation(
        string sessionStableId,
        [FromBody] SimulationNatureThreatObservationConfirmRequest request)
    {
        try
        {
            return Ok(incidentService.ConfirmThreatObservation(sessionStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("nature-threat/retreat-previews")]
    [ProducesResponseType(typeof(SimulationNatureEmergencyRetreatPreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationNatureEmergencyRetreatPreviewSnapshot>
        PreviewNatureEmergencyRetreat(
            string sessionStableId,
            [FromBody] SimulationNatureEmergencyRetreatPreviewRequest request)
    {
        try
        {
            return Ok(incidentService.PreviewEmergencyRetreat(sessionStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("nature-threat/retreats/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmNatureEmergencyRetreat(
        string sessionStableId,
        [FromBody] SimulationNatureEmergencyRetreatConfirmRequest request)
    {
        try
        {
            return Ok(incidentService.ConfirmEmergencyRetreat(sessionStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("nature-threat/restoration-previews")]
    [ProducesResponseType(typeof(SimulationNatureRestorationPreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationNatureRestorationPreviewSnapshot>
        PreviewNatureRestoration(
            string sessionStableId,
            [FromBody] SimulationNatureRestorationPreviewRequest request)
    {
        try
        {
            return Ok(incidentService.PreviewRestoration(sessionStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("nature-threat/restorations/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmNatureRestoration(
        string sessionStableId,
        [FromBody] SimulationNatureRestorationConfirmRequest request)
    {
        try
        {
            return Ok(incidentService.ConfirmRestoration(sessionStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("nature-threat/party-recovery-previews")]
    [ProducesResponseType(typeof(SimulationNaturePartyRecoveryPreviewSnapshot),
        StatusCodes.Status200OK)]
    public ActionResult<SimulationNaturePartyRecoveryPreviewSnapshot>
        PreviewNaturePartyRecovery(
            string sessionStableId,
            [FromBody] SimulationNaturePartyRecoveryPreviewRequest request)
    {
        try
        {
            return Ok(incidentService.PreviewPartyRecovery(sessionStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("nature-threat/party-recoveries/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmNaturePartyRecovery(
        string sessionStableId,
        [FromBody] SimulationNaturePartyRecoveryConfirmRequest request)
    {
        try
        {
            return Ok(incidentService.ConfirmPartyRecovery(sessionStableId, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }
}
