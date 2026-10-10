using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Services.OperationsAlerts;

namespace Ssalddel.Controllers.Admin;

[ApiController, Authorize(Policy = "서버관리자전용"), Route("api/v1/admin/operations-alerts")]
[Ssalddel.ApiMetadata.SsalddelApiVersion(Ssalddel.ApiMetadata.SsalddelProductVersion.V0_0)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OperationsAlertsController(OperationsAlertCoordinator coordinator) : ControllerBase
{
    [HttpGet]
    public Task<OperationsAlertsPage> List([FromQuery] string? status, [FromQuery] int page = 1, CancellationToken ct = default)
        => coordinator.ListAsync(status, page, ct);
    [HttpGet("{id}")]
    public async Task<ActionResult<OperationsAlertDto>> Detail(string id, CancellationToken ct)
        => await coordinator.DetailAsync(id, ct) is { } row ? Ok(row) : NotFound();
    [HttpPost("{id}/acknowledge")]
    public async Task<ActionResult<OperationsAlertDto>> Acknowledge(string id, OperationsAlertAcknowledgeRequest input, CancellationToken ct)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized();
        try { await coordinator.AcknowledgeAsync(id, actor, input, ct); return Ok(await coordinator.DetailAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (OperationsAlertConflictException) { return Conflict(new { code = "OperationsAlertRevisionConflict", message = "최신 알림을 다시 확인하세요." }); }
    }
}
