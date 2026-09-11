using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/world-stream")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.SimulationWorldStreaming,
    SsalddelCodeLayer.Api,
    "타일 Recipe·Manifest·Layer·객체 Projection 조회 경계를 제공한다.",
    StepKey = "api.world-stream",
    DependsOnStepKeys = new string[] { "contract.stream-recipe" },
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.DerivedWorld | SsalddelCodeDataScope.SimulationState,
    FlowOrder = 20,
    Boundary = "조회와 eligibility Preview는 타일이나 업무 상태를 생성·확정하지 않는다.")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E4,
    "공간 WI의 실행 문맥·세계 발현 판정에 사용할 공간 조립 증거를 제공한다.",
    Boundary = "AreaSet·Graph·배치·통행은 조건부 입력이며 그 자체로 E4·E5를 완료하지 않는다.")]
public sealed class SimulationWorldStreamingController(
    SimulationWorldStreamingService service,
    SimulationWorldExplorationService exploration,
    SimulationWorldTileArtifactContentService artifactContent,
    SimulationWorldLandscapeCompositionService landscapeComposition,
    SimulationWorldAreaSetLandscapeGraphService areaSetGraphs,
    SimulationWorldActualE5SpatialService actualE5Spatial,
    SimulationWorldLayoutService worldLayouts,
    SimulationWorld상호작용GraphService interactionGraphs,
    SimulationWorld상호작용NetworkService interactionNetwork) : ControllerBase
{
    [HttpGet("recipes/{recipeId}")]
    [ProducesResponseType(typeof(SimulationWorldStreamRecipeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<SimulationWorldStreamRecipeResponse> Recipe(string recipeId)
        => service.TryGetRecipe(recipeId, out var value)
            ? Ok(value)
            : NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldStreamRecipeNotFound" });

    [HttpGet("tiles/{tileKey}/manifest")]
    [ProducesResponseType(typeof(SimulationWorldTileStreamManifestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<SimulationWorldTileStreamManifestResponse> Manifest(string tileKey)
        => service.TryGetManifest(tileKey, out var value)
            ? Ok(value)
            : NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldStreamTileNotFound" });

    [HttpGet("tiles/{tileKey}/landscape-compositions")]
    [ProducesResponseType(typeof(SimulationWorldLandscapeCompositionTileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorldLandscapeCompositionTileResponse>>
        LandscapeCompositions(string tileKey, CancellationToken cancellationToken)
    {
        var value = await areaSetGraphs.ReadTileFacadeAsync(tileKey, cancellationToken)
                    ?? await landscapeComposition.ReadLatestAsync(tileKey, cancellationToken);
        return value == null
            ? NotFound(new SimulationErrorResponse
                { ErrorCode = "SimulationWorldLandscapeCompositionNotFound" })
            : Ok(value);
    }

    [HttpGet("area-sets/{areaSetStableId}")]
    [ProducesResponseType(typeof(SimulationWorldAreaSetDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorldAreaSetDefinitionResponse>> AreaSet(
        string areaSetStableId, CancellationToken cancellationToken)
    {
        var value = await actualE5Spatial.ReadAreaSetAsync(areaSetStableId, cancellationToken)
                    ?? await areaSetGraphs.ReadAreaSetAsync(areaSetStableId, cancellationToken);
        return value == null
            ? NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldAreaSetNotFound" })
            : Ok(value);
    }

    [HttpGet("area-sets/{areaSetStableId}/landscape-graphs")]
    [ProducesResponseType(typeof(SimulationWorldLandscapeGraphIndexResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorldLandscapeGraphIndexResponse>> LandscapeGraphIndex(
        string areaSetStableId,
        [FromQuery] string? tileKey = null,
        [FromQuery] int radiusTiles = 4,
        CancellationToken cancellationToken = default)
    {
        var value = await actualE5Spatial.ReadGraphIndexAsync(
                        areaSetStableId, tileKey, radiusTiles, cancellationToken)
                    ?? (tileKey == null
                        ? null
                        : await areaSetGraphs.ReadGraphIndexAsync(
                            areaSetStableId, tileKey, radiusTiles, cancellationToken));
        return value == null
            ? NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldLandscapeGraphIndexNotFound" })
            : Ok(value);
    }

    [HttpGet("landscape-graphs/{landscapeGraphStableId}")]
    [ProducesResponseType(typeof(SimulationWorldLandscapeGraphResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorldLandscapeGraphResponse>> LandscapeGraph(
        string landscapeGraphStableId, CancellationToken cancellationToken)
    {
        var value = await actualE5Spatial.ReadGraphAsync(
                        landscapeGraphStableId, cancellationToken)
                    ?? await areaSetGraphs.ReadGraphAsync(
                        landscapeGraphStableId, cancellationToken);
        return value == null
            ? NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldLandscapeGraphNotFound" })
            : Ok(value);
    }

    [HttpGet("area-set-networks/{networkStableId}")]
    [ProducesResponseType(typeof(SimulationWorldAreaSetNetworkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorldAreaSetNetworkResponse>> AreaSetNetwork(
        string networkStableId,
        CancellationToken cancellationToken)
    {
        var value = await actualE5Spatial.ReadNetworkAsync(networkStableId, cancellationToken);
        return value == null
            ? NotFound(new SimulationErrorResponse
                { ErrorCode = "SimulationWorldAreaSetNetworkNotFound" })
            : Ok(value);
    }

    [HttpGet("world-layouts/{worldLayoutStableId}")]
    [ProducesResponseType(typeof(SimulationWorldLayoutDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorldLayoutDefinitionResponse>> WorldLayout(
        string worldLayoutStableId, CancellationToken cancellationToken)
    {
        var value = await worldLayouts.ReadDefinitionAsync(worldLayoutStableId, cancellationToken);
        return value == null
            ? NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldLayoutNotFound" })
            : Ok(value);
    }

    [HttpGet("world-layouts/{worldLayoutStableId}/grounding-binding")]
    [ProducesResponseType(typeof(SimulationWorldGroundingBindingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorldGroundingBindingResponse>> WorldGroundingBinding(
        string worldLayoutStableId, CancellationToken cancellationToken)
    {
        var value = await worldLayouts.ReadGroundingBindingAsync(worldLayoutStableId, cancellationToken);
        return value == null
            ? NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldGroundingBindingNotFound" })
            : Ok(value);
    }

    [HttpGet("world-layouts/{worldLayoutStableId}/grounding-readiness")]
    [ProducesResponseType(typeof(SimulationWorldGroundingReadinessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorldGroundingReadinessResponse>> WorldGroundingReadiness(
        string worldLayoutStableId, CancellationToken cancellationToken)
    {
        var value = await worldLayouts.ReadGroundingReadinessAsync(worldLayoutStableId, cancellationToken);
        return value == null
            ? NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldGroundingReadinessNotFound" })
            : Ok(value);
    }

    [HttpGet("area-set-networks/{networkStableId}/interaction-readiness")]
    [ProducesResponseType(typeof(SimulationWorld상호작용Network준비도Response),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SimulationWorld상호작용Network준비도Response>>
        AreaSetNetworkInteractionReadiness(
            string networkStableId,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await interactionNetwork.EvaluateAsync(
                networkStableId, cancellationToken));
        }
        catch (InvalidOperationException error)
        {
            var response = new SimulationErrorResponse { ErrorCode = error.Message };
            return error.Message == "SimulationWorldAreaSetNetworkNotFound"
                ? NotFound(response)
                : Conflict(response);
        }
    }

    [HttpGet("area-sets/{areaSetStableId}/interaction-graph-readiness")]
    [ProducesResponseType(typeof(SimulationWorld상호작용Graph준비도Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SimulationWorld상호작용Graph준비도Response>>
        InteractionGraphReadiness(
            string areaSetStableId,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await interactionGraphs.EvaluateAsync(areaSetStableId, cancellationToken));
        }
        catch (InvalidOperationException error)
        {
            var response = new SimulationErrorResponse { ErrorCode = error.Message };
            return error.Message == "SimulationWorldAreaSetNotFound"
                ? NotFound(response)
                : Conflict(response);
        }
    }

    [HttpGet("tiles/{tileKey}/artifacts/{layerCode}")]
    [ProducesResponseType(typeof(SimulationWorldTileArtifactDescriptorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<SimulationWorldTileArtifactDescriptorResponse> Artifact(
        string tileKey, string layerCode)
        => service.TryGetArtifact(tileKey, layerCode, out var value)
            ? Ok(value)
            : NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldStreamArtifactNotFound" });

    [HttpGet("tiles/{tileKey}/artifacts/{layerCode}/content")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status409Conflict)]
    public IActionResult ArtifactContent(string tileKey, string layerCode)
    {
        if (!service.TryGetArtifact(tileKey, layerCode, out var descriptor)
            || descriptor.StatusCode != SimulationWorldStreamCodes.Available)
            return NotFound(new SimulationErrorResponse
                { ErrorCode = "SimulationWorldStreamArtifactNotFound" });
        if (!artifactContent.TryResolve(descriptor, out var file, out var errorCode))
        {
            var error = new SimulationErrorResponse { ErrorCode = errorCode };
            return errorCode == SimulationWorldTileArtifactContentService.IntegrityMismatch
                ? Conflict(error)
                : NotFound(error);
        }

        return PhysicalFile(file.FullPath, file.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("tiles/{tileKey}/activities")]
    [ProducesResponseType(typeof(SimulationWorldTileActivityProjectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<SimulationWorldTileActivityProjectionResponse> Activities(string tileKey)
        => service.TryGetActivities(tileKey, out var value)
            ? Ok(value)
            : NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldStreamTileNotFound" });

    [HttpGet("tiles/{tileKey}/objects")]
    [ProducesResponseType(typeof(SimulationWorldTileObjectProjectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<SimulationWorldTileObjectProjectionResponse> Objects(string tileKey)
        => service.TryGetObjects(tileKey, out var value)
            ? Ok(value)
            : NotFound(new SimulationErrorResponse { ErrorCode = "SimulationWorldStreamTileNotFound" });

    [HttpGet("tiles/{tileKey}/building-item-rules")]
    [ProducesResponseType(typeof(SimulationWorldBuildingItemRulePackageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<SimulationWorldBuildingItemRulePackageResponse> BuildingItemRules(
        string tileKey)
    {
        try
        {
            return Ok(exploration.GetBuildingItemRules(tileKey));
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }

    [HttpPost("sessions/{sessionStableId}/tiles/{tileKey}/building-item-eligibility-preview")]
    [ProducesResponseType(typeof(SimulationWorldBuildingItemEligibilityPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<SimulationWorldBuildingItemEligibilityPreviewResponse> PreviewEligibility(
        string sessionStableId,
        string tileKey,
        [FromBody] SimulationWorldBuildingItemEligibilityPreviewRequest request)
    {
        try
        {
            return Ok(exploration.PreviewEligibility(sessionStableId, tileKey, request));
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }
}
