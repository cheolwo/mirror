using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Ssalddel.Application.Admin.Settlement;
using Ssalddel.Contracts.Admin.Settlement;
using Ssalddel.ApiMetadata;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Settlement05;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[ApiController]
[Route("api/v1/admin/driver-settlements")]
[Authorize(Policy = "서버관리자전용")]
public sealed class 기사월정산관리Controller : ControllerBase
{
    private readonly ISender _sender;

    public 기사월정산관리Controller(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    public async Task<IActionResult> 목록조회([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? driverId)
    {
        var items = await _sender.Send(new 기사월정산관리목록조회Query(year, month, driverId));

        return Ok(items);
    }
}
