using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Services.Privacy;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[Route("api/v1/common/application-privacy-consents")]
[SsalddelApiContractName("ApplicationPrivacyConsentsController")]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
public sealed class 신청개인정보동의Controller(I신청개인정보동의증적Service service) : CommunityControllerBase
{
    [HttpPost]
    public async Task<IActionResult> 동의기록(
        [FromBody] 신청개인정보동의기록Request request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.동의기록Async(request, CurrentUserId(), cancellationToken);
            return CreatedAtAction(nameof(내증적조회), new { evidenceId = result.증적Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "개인정보 동의 요청이 올바르지 않습니다.", Detail = ex.Message });
        }
    }

    [HttpGet("{evidenceId:guid}")]
    public async Task<IActionResult> 내증적조회(Guid evidenceId, CancellationToken cancellationToken)
    {
        var result = await service.내증적조회Async(evidenceId, CurrentUserId(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{evidenceId:guid}/withdrawal")]
    public async Task<IActionResult> 동의철회(
        Guid evidenceId,
        [FromBody] 신청개인정보동의철회Request request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.철회Async(evidenceId, request, CurrentUserId(), cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? string.Empty;
}
