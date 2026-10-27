using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Progress;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[ApiController]
[Route("api/v1/transport-request-ledgers")]
[Authorize(Policy = "물류운영사용자전용")]
public sealed class 운송원장Controller : ControllerBase
{
    private readonly ISender _sender;

    public 운송원장Controller(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{requestId}/events")]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    public async Task<IActionResult> 이벤트조회(
        string requestId,
        [FromQuery] DateTime? sinceUtc,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new 운송원장이벤트조회Query(requestId, sinceUtc), cancellationToken);
        return this.ToActionResult(result);
    }
}
