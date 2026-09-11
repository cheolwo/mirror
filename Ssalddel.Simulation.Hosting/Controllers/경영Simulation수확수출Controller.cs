using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class 경영Simulation수확수출Controller(
    경영Simulation수확수출Service service) : ControllerBase
{
    [HttpGet("{sessionStableId}/farm-choice-context")]
    [ProducesResponseType(typeof(SimulationFarmChoiceContextSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmChoiceContextSnapshot> Farm선택Context(
        string sessionStableId)
        => Ok(service.GetFarmChoiceContext(sessionStableId));

    [HttpPost("{sessionStableId}/farm-choice-previews")]
    [ProducesResponseType(typeof(SimulationFarmChoicePreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationFarmChoicePreviewSnapshot> Farm선택Preview(
        string sessionStableId,
        [FromBody] SimulationFarmChoicePreviewRequest request)
        => Ok(service.PreviewFarmChoice(sessionStableId, request));

    [HttpPost("{sessionStableId}/farm-choices/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> Farm선택Confirm(
        string sessionStableId,
        [FromBody] SimulationFarmChoiceConfirmRequest request)
        => Ok(service.ConfirmFarmChoice(sessionStableId, request));

    [HttpPost("{sessionStableId}/harvest-disposition-impact-previews")]
    [ProducesResponseType(typeof(SimulationHarvestDispositionImpactPreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<SimulationHarvestDispositionImpactPreviewSnapshot> PreviewHarvestImpact(
        string sessionStableId,
        [FromBody] SimulationHarvestDispositionImpactPreviewRequest request)
        => Ok(service.PreviewHarvestDispositionImpact(sessionStableId, request));

    [HttpPost("{sessionStableId}/harvest-disposition-impacts/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmHarvestImpact(
        string sessionStableId,
        [FromBody] SimulationHarvestDispositionImpactConfirmRequest request)
        => Ok(service.ConfirmHarvestDispositionImpact(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-preparation-previews")]
    [ProducesResponseType(typeof(Simulation수출준비PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation수출준비PreviewSnapshot> 수출준비Preview(
        string sessionStableId,
        [FromBody] Simulation수출준비PreviewRequest request)
        => Ok(service.Preview수출준비(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-preparations/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> 수출준비Confirm(
        string sessionStableId,
        [FromBody] Simulation수출준비ConfirmRequest request)
        => Ok(service.Confirm수출준비(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-rework-previews")]
    [ProducesResponseType(typeof(Simulation수출준비PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation수출준비PreviewSnapshot> 수출재작업Preview(
        string sessionStableId,
        [FromBody] Simulation수출재작업PreviewRequest request)
        => Ok(service.Preview수출재작업(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-reworks/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> 수출재작업Confirm(
        string sessionStableId,
        [FromBody] Simulation수출재작업ConfirmRequest request)
        => Ok(service.Confirm수출재작업(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-cargo-preparation-previews")]
    [ProducesResponseType(typeof(Simulation수출Cargo준비PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation수출Cargo준비PreviewSnapshot> 수출Cargo준비Preview(
        string sessionStableId,
        [FromBody] Simulation수출Cargo준비PreviewRequest request)
        => Ok(service.Preview수출Cargo준비(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-cargo-preparations/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> 수출Cargo준비Confirm(
        string sessionStableId,
        [FromBody] Simulation수출Cargo준비ConfirmRequest request)
        => Ok(service.Confirm수출Cargo준비(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-cargo-handoff-previews")]
    [ProducesResponseType(typeof(Simulation수출Cargo인계PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation수출Cargo인계PreviewSnapshot> 수출Cargo인계Preview(
        string sessionStableId,
        [FromBody] Simulation수출Cargo인계PreviewRequest request)
        => Ok(service.Preview수출Cargo인계(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-cargo-handoffs/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> 수출Cargo인계Confirm(
        string sessionStableId,
        [FromBody] Simulation수출Cargo인계ConfirmRequest request)
        => Ok(service.Confirm수출Cargo인계(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-port-receipt-previews")]
    [ProducesResponseType(typeof(Simulation수출항만인수PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation수출항만인수PreviewSnapshot> 수출항만인수Preview(
        string sessionStableId,
        [FromBody] Simulation수출항만인수PreviewRequest request)
        => Ok(service.Preview수출항만인수(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-port-receipts/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> 수출항만인수Confirm(
        string sessionStableId,
        [FromBody] Simulation수출항만인수ConfirmRequest request)
        => Ok(service.Confirm수출항만인수(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-readiness-review-previews")]
    [ProducesResponseType(typeof(Simulation수출준비성검토PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation수출준비성검토PreviewSnapshot> 수출준비성검토Preview(
        string sessionStableId,
        [FromBody] Simulation수출준비성검토PreviewRequest request)
        => Ok(service.Preview수출준비성검토(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-readiness-reviews/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> 수출준비성검토Confirm(
        string sessionStableId,
        [FromBody] Simulation수출준비성검토ConfirmRequest request)
        => Ok(service.Confirm수출준비성검토(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-shipment-plan-previews")]
    [ProducesResponseType(typeof(Simulation수출선적계획PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation수출선적계획PreviewSnapshot> 수출선적계획Preview(
        string sessionStableId,
        [FromBody] Simulation수출선적계획PreviewRequest request)
        => Ok(service.Preview수출선적계획(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-shipment-plans/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> 수출선적계획Confirm(
        string sessionStableId,
        [FromBody] Simulation수출선적계획ConfirmRequest request)
        => Ok(service.Confirm수출선적계획(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-shipment-execution-previews")]
    [ProducesResponseType(typeof(Simulation수출선적실행PreviewSnapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation수출선적실행PreviewSnapshot> 수출선적실행Preview(
        string sessionStableId,
        [FromBody] Simulation수출선적실행PreviewRequest request)
        => Ok(service.Preview수출선적실행(sessionStableId, request));

    [HttpPost("{sessionStableId}/export-shipment-executions/confirm")]
    [ProducesResponseType(typeof(경영SimulationSessionSnapshot), StatusCodes.Status200OK)]
    public ActionResult<경영SimulationSessionSnapshot> 수출선적실행Confirm(
        string sessionStableId,
        [FromBody] Simulation수출선적실행ConfirmRequest request)
        => Ok(service.Confirm수출선적실행(sessionStableId, request));

    [HttpGet("{sessionStableId}/harvest-route-outcomes")]
    [ProducesResponseType(typeof(Simulation수확판로결과Snapshot[]), StatusCodes.Status200OK)]
    public ActionResult<Simulation수확판로결과Snapshot[]> 수확판로결과목록(
        string sessionStableId)
        => Ok(service.Get수확판로결과목록(sessionStableId));

    [HttpGet("{sessionStableId}/harvest-route-outcomes/{harvestLotStableId}")]
    [ProducesResponseType(typeof(Simulation수확판로결과Snapshot), StatusCodes.Status200OK)]
    public ActionResult<Simulation수확판로결과Snapshot> 수확판로결과(
        string sessionStableId,
        string harvestLotStableId)
        => Ok(service.Get수확판로결과(sessionStableId, harvestLotStableId));
}
