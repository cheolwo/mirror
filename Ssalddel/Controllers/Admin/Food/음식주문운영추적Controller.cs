using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Food;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Filters;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Food;

[SsalddelApiVersion(
    SsalddelProductVersion.V3_0,
    FeatureKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.FoodDelivery)]
[RequireVersionFeature(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[ApiController]
[Authorize(Policy = "서버관리자전용")]
[Route("api/v1/admin/food-orders")]
[SsalddelApiContractName("FoodOrderOperationsTraceController")]
public sealed class 음식주문운영추적Controller(
    I음식주문운영추적UseCase useCase,
    I음식배달중단검토UseCase? interruptionReviewUseCase = null,
    I음식주문기사정산UseCase? settlementUseCase = null) : ControllerBase
{
    [HttpPost("{orderNo}/simulate-driver-payout")]
    [SsalddelApiContractName("SimulateDriverPayout")]
    [SsalddelApiCapability(SsalddelCapability.FoodDelivery)]
    [SsalddelApiOperation(SsalddelOperation.Execute)]
    public async Task<IActionResult> 기사모의지급검증(
        string orderNo,
        [FromBody] FoodDeliverySimulatedPayoutRequest request,
        CancellationToken cancellationToken)
        => settlementUseCase is null
            ? StatusCode(StatusCodes.Status503ServiceUnavailable)
            : this.ToActionResult(await settlementUseCase.모의지급검증Async(orderNo, request, cancellationToken));

    [HttpGet("{orderNo}/operations-trace")]
    [SsalddelApiContractName("GetOperationsTrace")]
    public async Task<IActionResult> 조회(
        string orderNo,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await useCase.조회Async(orderNo, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("delivery-attempts/{attemptId}/interruption-review")]
    [SsalddelApiContractName("ReviewDeliveryInterruption")]
    public async Task<IActionResult> 중단검토(
        string attemptId,
        [FromBody] Ssalddel.Contracts.Food.음식배달중단검토요청 request,
        CancellationToken cancellationToken)
    {
        if (interruptionReviewUseCase is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        try
        {
            var result = await interruptionReviewUseCase.검토Async(attemptId, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status409Conflict);
        }
    }
}
