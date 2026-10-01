using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Notifications;
using Hongdal.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[ApiController]
[Route("api/v1/mobile/push/installations")]
[Authorize]
public sealed class MobilePushInstallationsController : ControllerBase
{
    private readonly IHongdalMobilePushInstallationService _service;

    public MobilePushInstallationsController(IHongdalMobilePushInstallationService service)
    {
        _service = service;
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(
        [FromBody] HongdalMobilePushInstallationUpsertRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.UpsertAsync(CurrentUserId(), request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{installationId}")]
    public async Task<IActionResult> Deactivate(
        string installationId,
        CancellationToken cancellationToken)
        => await _service.DeactivateAsync(CurrentUserId(), installationId, cancellationToken)
            ? NoContent()
            : NotFound();

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");
}
