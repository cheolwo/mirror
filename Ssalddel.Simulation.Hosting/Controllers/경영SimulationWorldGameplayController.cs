using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/sessions")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.SimulationSessionLifecycle,
    SsalddelCodeLayer.Api,
    "플레이어 심리·AreaSet 이동·호스팅·협동 건설의 세계 게임플레이 HTTP 경계를 제공한다.",
    StepKey = "api.world-gameplay",
    DependsOnStepKeys = new string[] { "contract.session-create" },
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    Effects = SsalddelCodeEffect.StateMutation,
    ReadsFrom = SsalddelCodeDataScope.SimulationState,
    WritesTo = SsalddelCodeDataScope.SimulationState,
    FlowOrder = 20,
    Boundary = "기존 route를 보존하며 운영 상태나 Unity 표현 상태를 직접 변경하지 않는다.")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class 경영SimulationWorldGameplayController(
    경영SimulationWorldGameplayService service) : SimulationApiControllerBase
{
    [HttpGet("{sessionStableId}/nature-minds")]
    public ActionResult<SimulationNatureMindStateSnapshot> GetNatureMinds(
        string sessionStableId)
        => Execute<SimulationNatureMindStateSnapshot>(() =>
            Ok(service.GetNatureMindState(sessionStableId)));

    [HttpGet("{sessionStableId}/town-npc-life")]
    public ActionResult<SimulationTownNpcLifeStateSnapshot> GetTownNpcLife(
        string sessionStableId)
        => Execute<SimulationTownNpcLifeStateSnapshot>(() =>
            Ok(service.GetTownNpcLifeState(sessionStableId)));

    [HttpGet("{sessionStableId}/nature-minds/{playerStableId}/farm-interpretation")]
    public ActionResult<SimulationNatureFarmInterpretationSnapshot>
        GetNatureFarmInterpretation(string sessionStableId, string playerStableId)
        => Execute<SimulationNatureFarmInterpretationSnapshot>(() =>
            Ok(service.GetNatureFarmInterpretation(sessionStableId, playerStableId)));

    [HttpGet("{sessionStableId}/players/{playerStableId}/area-access")]
    public ActionResult<SimulationPlayerAreaAccessStateSnapshot> GetAreaAccess(
        string sessionStableId, string playerStableId)
        => Execute<SimulationPlayerAreaAccessStateSnapshot>(() =>
            Ok(service.GetPlayerAreaAccess(sessionStableId, playerStableId)));

    [HttpPost("{sessionStableId}/area-traversal-previews")]
    public ActionResult<SimulationAreaTraversalPreviewSnapshot> PreviewAreaTraversal(
        string sessionStableId, [FromBody] SimulationAreaTraversalPreviewRequest request)
        => Execute<SimulationAreaTraversalPreviewSnapshot>(() =>
            Ok(service.PreviewAreaTraversal(sessionStableId, request)));

    [HttpPost("{sessionStableId}/area-traversals/confirm")]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmAreaTraversal(
        string sessionStableId, [FromBody] SimulationAreaTraversalConfirmRequest request)
        => Execute<경영SimulationSessionSnapshot>(() =>
            Ok(service.ConfirmAreaTraversal(sessionStableId, request)));

    [HttpGet("{sessionStableId}/hosted-world")]
    public ActionResult<SimulationHostedWorldStateSnapshot> GetHostedWorld(
        string sessionStableId)
        => Execute<SimulationHostedWorldStateSnapshot>(() =>
            Ok(service.GetHostedWorldState(sessionStableId)));

    [HttpPost("{sessionStableId}/hosted-world/open-previews")]
    public ActionResult<SimulationHostedWorldPreviewSnapshot> PreviewOpenHostedWorld(
        string sessionStableId, [FromBody] SimulationHostedWorldOpenPreviewRequest request)
        => Execute<SimulationHostedWorldPreviewSnapshot>(() =>
            Ok(service.PreviewOpenHostedWorld(sessionStableId, request)));

    [HttpPost("{sessionStableId}/hosted-world/open/confirm")]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmOpenHostedWorld(
        string sessionStableId, [FromBody] SimulationHostedWorldOpenConfirmRequest request)
        => Execute<경영SimulationSessionSnapshot>(() =>
            Ok(service.ConfirmOpenHostedWorld(sessionStableId, request)));

    [HttpPost("{sessionStableId}/hosted-world/join-previews")]
    public ActionResult<SimulationHostedWorldPreviewSnapshot> PreviewJoinHostedWorld(
        string sessionStableId, [FromBody] SimulationHostedWorldJoinPreviewRequest request)
        => Execute<SimulationHostedWorldPreviewSnapshot>(() =>
            Ok(service.PreviewJoinHostedWorld(sessionStableId, request)));

    [HttpPost("{sessionStableId}/hosted-world/join/confirm")]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmJoinHostedWorld(
        string sessionStableId, [FromBody] SimulationHostedWorldJoinConfirmRequest request)
        => Execute<경영SimulationSessionSnapshot>(() =>
            Ok(service.ConfirmJoinHostedWorld(sessionStableId, request)));

    [HttpPost("{sessionStableId}/hosted-world/guest-action-previews")]
    public ActionResult<SimulationHostedWorldPreviewSnapshot> PreviewHostedGuestAction(
        string sessionStableId, [FromBody] SimulationHostedGuestActionPreviewRequest request)
        => Execute<SimulationHostedWorldPreviewSnapshot>(() =>
            Ok(service.PreviewHostedGuestAction(sessionStableId, request)));

    [HttpPost("{sessionStableId}/hosted-world/guest-actions/confirm")]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmHostedGuestAction(
        string sessionStableId, [FromBody] SimulationHostedGuestActionConfirmRequest request)
        => Execute<경영SimulationSessionSnapshot>(() =>
            Ok(service.ConfirmHostedGuestAction(sessionStableId, request)));

    [HttpGet("{sessionStableId}/coop-construction")]
    public ActionResult<SimulationCoopConstructionStateSnapshot> GetCoopConstruction(
        string sessionStableId)
        => Execute<SimulationCoopConstructionStateSnapshot>(() =>
            Ok(service.GetCoopConstructionState(sessionStableId)));

    [HttpPost("{sessionStableId}/coop-construction/contribution-previews")]
    public ActionResult<SimulationCoopConstructionPreviewSnapshot> PreviewCoopContribution(
        string sessionStableId, [FromBody] SimulationCoopContributionPreviewRequest request)
        => Execute<SimulationCoopConstructionPreviewSnapshot>(() =>
            Ok(service.PreviewCoopContribution(sessionStableId, request)));

    [HttpPost("{sessionStableId}/coop-construction/contributions/confirm")]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmCoopContribution(
        string sessionStableId, [FromBody] SimulationCoopContributionConfirmRequest request)
        => Execute<경영SimulationSessionSnapshot>(() =>
            Ok(service.ConfirmCoopContribution(sessionStableId, request)));

    [HttpPost("{sessionStableId}/coop-construction/demolition-previews")]
    public ActionResult<SimulationCoopConstructionPreviewSnapshot> PreviewCoopDemolition(
        string sessionStableId, [FromBody] SimulationCoopProtectedActionPreviewRequest request)
        => Execute<SimulationCoopConstructionPreviewSnapshot>(() =>
            Ok(service.PreviewCoopDemolition(sessionStableId, request)));

    [HttpPost("{sessionStableId}/coop-construction/demolitions/confirm")]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmCoopDemolition(
        string sessionStableId, [FromBody] SimulationCoopProtectedActionConfirmRequest request)
        => Execute<경영SimulationSessionSnapshot>(() =>
            Ok(service.ConfirmCoopDemolition(sessionStableId, request)));

    [HttpPost("{sessionStableId}/coop-construction/restore-previews")]
    public ActionResult<SimulationCoopConstructionPreviewSnapshot> PreviewCoopRestore(
        string sessionStableId, [FromBody] SimulationCoopProtectedActionPreviewRequest request)
        => Execute<SimulationCoopConstructionPreviewSnapshot>(() =>
            Ok(service.PreviewCoopRestore(sessionStableId, request)));

    [HttpPost("{sessionStableId}/coop-construction/restores/confirm")]
    public ActionResult<경영SimulationSessionSnapshot> ConfirmCoopRestore(
        string sessionStableId, [FromBody] SimulationCoopProtectedActionConfirmRequest request)
        => Execute<경영SimulationSessionSnapshot>(() =>
            Ok(service.ConfirmCoopRestore(sessionStableId, request)));

    [HttpGet("{sessionStableId}/gameplay-observability")]
    public ActionResult<SimulationGameplayObservabilitySnapshot> GetGameplayObservability(
        string sessionStableId)
        => Execute<SimulationGameplayObservabilitySnapshot>(() =>
            Ok(service.GetGameplayObservability(sessionStableId)));
}
