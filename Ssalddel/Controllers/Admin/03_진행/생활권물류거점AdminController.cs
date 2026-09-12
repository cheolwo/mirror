using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Filters;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Progress;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_5)]
[SsalddelApiFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.SsalddelMart)]
[RequireVersionFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[Route(NeighborhoodMicroHubRoutes.Admin)]
[SsalddelApiContractName("NeighborhoodMicroHubsAdminController")]
public sealed class 생활권물류거점AdminController(I생활권물류거점Service service) : ControllerBase
{
    [HttpPut("{id:guid}/review")]
    [SsalddelApiContractName("Review")]
    public Task<ActionResult<생활권물류거점Response>> 검토(
        Guid id, [FromBody] 생활권물류거점검토Request request, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.관리자검토Async(id, request, CurrentUserId(), cancellationToken));

    [HttpPost("{id:guid}/status")]
    [SsalddelApiContractName("ChangeStatus")]
    public Task<ActionResult<생활권물류거점Response>> 상태변경(
        Guid id, [FromBody] 생활권물류거점상태변경Request request, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.상태변경Async(id, request, CurrentUserId(), cancellationToken));

    private async Task<ActionResult<생활권물류거점Response>> ExecuteAsync(Func<Task<생활권물류거점Response>> action)
    {
        try { return Ok(await action()); }
        catch (KeyNotFoundException ex) { return Problem(statusCode: StatusCodes.Status404NotFound, title: "생활권 물류 거점을 찾을 수 없습니다.", detail: ex.Message); }
        catch (생활권물류거점ConcurrencyException ex) { return Problem(statusCode: StatusCodes.Status409Conflict, title: "생활권 물류 거점 정보가 이미 변경되었습니다.", detail: ex.Message); }
        catch (InvalidOperationException ex) { return Problem(statusCode: StatusCodes.Status400BadRequest, title: "생활권 물류 거점 상태를 변경할 수 없습니다.", detail: ex.Message); }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system";
}
