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
    "지역·타일별 대표 정보와 가까운 공개 객체의 제한된 상세정보를 제공한다.",
    StepKey = "api.world-region-summary",
    DependsOnStepKeys = new[] { "api.world-stream" },
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.DerivedWorld,
    FlowOrder = 25,
    Boundary = "요약 응답에는 상호명을 넣지 않고 명시적인 공개 상세 조회에서만 공개 공공데이터 상호명을 반환한다.")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class SimulationWorldRegionSummaryController(
    SimulationWorld지역표현요약Service service) : ControllerBase
{
    [HttpGet("regions/{regionStableId}/summary")]
    [ProducesResponseType(typeof(SimulationWorld지역표현요약Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorld지역표현요약Response>> RegionSummary(
        string regionStableId,
        [FromQuery] string lod = SimulationWorld지역표현요약LodCodes.L1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.지역요약조회Async(regionStableId, lod, cancellationToken));
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

    [HttpGet("tiles/{tileKey}/summary")]
    [ProducesResponseType(typeof(SimulationWorld지역표현요약Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorld지역표현요약Response>> TileSummary(
        string tileKey,
        [FromQuery] string lod = SimulationWorld지역표현요약LodCodes.L2,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.타일요약조회Async(tileKey, lod, cancellationToken));
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

    [HttpGet("objects/{objectStableId}/public-detail")]
    [ProducesResponseType(typeof(SimulationWorld공개객체상세Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SimulationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationWorld공개객체상세Response>> PublicDetail(
        string objectStableId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.공개객체상세조회Async(objectStableId, cancellationToken));
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }
}
