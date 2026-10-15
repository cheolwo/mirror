using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Driver.Settlement;
using 살뜰.Data;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Driver.Settlement06;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiCapability(SsalddelCapability.Settlement)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[ApiController]
[Authorize(Roles = 역할명.기사)]
[Route("api/v1/driver/payout-preparations")]
public sealed class 기사지급준비Controller : DriverControllerBase
{
    private readonly I기사지급준비UseCase _useCase;

    public 기사지급준비Controller(I기사지급준비UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet]
    public async Task<IActionResult> 월별조회(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.월별조회Async(
            현재기사Id(),
            year,
            month,
            cancellationToken);
        return this.ToActionResult(result);
    }
}
