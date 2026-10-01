using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.CollectiveProcurement;
using Hongdal.Services.CollectiveProcurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[ApiController]
[Authorize]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[Route("api/v1/collective-procurement/plans")]
public sealed class CollectiveProcurementPlansController : ControllerBase
{
    private readonly ICollectiveProcurementPlanningService service;

    public CollectiveProcurementPlansController(ICollectiveProcurementPlanningService service)
    {
        this.service = service;
    }

    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] CreateCollectiveProcurementPlanRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var created = await service.CreateAsync(request, CurrentUserId(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { planId = created.PlanId }, created);
        });

    [HttpGet("{planId:guid}")]
    public Task<IActionResult> Get(Guid planId, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var result = await service.GetAsync(planId, CurrentUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        });

    [HttpPost("{planId:guid}/revisions")]
    public Task<IActionResult> Recalculate(
        Guid planId,
        [FromBody] RecalculateCollectiveProcurementPlanRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => Ok(await service.RecalculateAsync(
            planId,
            request,
            CurrentUserId(),
            cancellationToken)));

    [HttpPut("{planId:guid}/disclosure-consent")]
    public Task<IActionResult> UpdateDisclosureConsent(
        Guid planId,
        [FromBody] UpdateCollectiveProcurementDisclosureConsentRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => Ok(await service.UpdateDisclosureConsentAsync(
            planId,
            request,
            CurrentUserId(),
            cancellationToken)));

    [HttpPost("{planId:guid}/revisions/{calculationRevision:int}/acceptance")]
    public Task<IActionResult> AcceptRevision(
        Guid planId,
        int calculationRevision,
        [FromBody] AcceptCollectiveProcurementRevisionRequest request,
        CancellationToken cancellationToken)
    {
        request.CalculationRevision = calculationRevision;
        return ExecuteAsync(async () => Ok(await service.AcceptRevisionAsync(
            planId,
            request,
            CurrentUserId(),
            cancellationToken)));
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "공동조달계획을 찾을 수 없습니다.",
                detail: ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "공동조달계획에 접근할 권한이 없습니다.",
                detail: ex.Message);
        }
        catch (CollectiveProcurementPlanConcurrencyException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "공동조달계획이 이미 변경되었습니다.",
                detail: ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "공동조달계획 요청이 올바르지 않습니다.",
                detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "현재 공동조달계획 상태에서는 요청을 처리할 수 없습니다.",
                detail: ex.Message);
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");
}
