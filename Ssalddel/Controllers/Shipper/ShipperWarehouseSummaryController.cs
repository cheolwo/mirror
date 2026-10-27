using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Shipper.Queries;
using Ssalddel.Filters;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Shipper;

[ApiController]
[Authorize(Policy = "화주또는판매자전용")]
[Route("api/v1/shipper/warehouse-summary")]
[RequireVersionFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V2_5)]
[SsalddelApiFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
[SsalddelApiAudience(SsalddelActor.Shipper)]
[SsalddelApiCapability(SsalddelCapability.WarehouseFulfillment)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
public sealed class ShipperWarehouseSummaryController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> 요약조회(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        return Ok(await sender.Send(new ShipperWarehouseSummaryQuery(userId), cancellationToken));
    }
}
