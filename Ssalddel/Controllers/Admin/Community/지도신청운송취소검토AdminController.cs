using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;

namespace Ssalddel.Controllers.Admin.Community;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[Route("api/v1/admin/community/map-transport-cancellation-reviews")]
[SsalddelApiContractName("CommunityMapTransportCancellationReviewsAdminController")]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
public sealed class 지도신청운송취소검토AdminController(
    I지도신청운송취소검토AdminWorkflow workflow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 목록(CancellationToken cancellationToken)
        => Ok(await workflow.목록Async(cancellationToken));

    [HttpPost("{ledgerId}/decision")]
    public Task<IActionResult> 처리(
        string ledgerId,
        [FromBody] 지도신청운송취소검토처리Request request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            () => workflow.처리Async(ledgerId, request, CurrentUserId(), cancellationToken),
            "운송 취소 관리자 검토 결과를 반영할 수 없습니다.");

    private static async Task<IActionResult> ExecuteAsync(
        Func<Task<지도신청가원장Response>> action,
        string title)
    {
        try
        {
            return new OkObjectResult(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(new ProblemDetails { Title = title, Detail = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ObjectResult(new ProblemDetails { Title = title, Detail = ex.Message })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new ProblemDetails { Title = title, Detail = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return new BadRequestObjectResult(new ProblemDetails { Title = title, Detail = ex.Message });
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? string.Empty;
}
