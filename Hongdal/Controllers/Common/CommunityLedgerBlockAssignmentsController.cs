using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Authorize]
[Route("api/v1/community/ledgers/{ledgerId}/blocks/{blockId}/assignees")]
public sealed class CommunityLedgerBlockAssignmentsController : ControllerBase
{
    private readonly ICommunityLedgerBlockAssignmentService _service;

    public CommunityLedgerBlockAssignmentsController(ICommunityLedgerBlockAssignmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        string ledgerId,
        string blockId,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _service.GetAsync(
            ledgerId,
            blockId,
            CurrentUserId(),
            cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update(
        string ledgerId,
        string blockId,
        [FromBody] CommunityLedgerBlockAssignmentUpdateRequest request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _service.UpdateAsync(
            ledgerId,
            blockId,
            request,
            CurrentUserId(),
            cancellationToken));

    private string? CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub");
}
