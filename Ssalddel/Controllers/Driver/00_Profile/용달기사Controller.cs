using System.Security.Claims;
using Ssalddel.Application.Driver.Profile;
using Ssalddel.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Driver.Qualification;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Driver.Profile01;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiCapability(SsalddelCapability.DriverWork)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Manage)]
[ApiController]
[Route("api/v1/drivers")]
[Authorize]
public sealed class 용달기사Controller : ControllerBase
{
    private readonly I용달기사프로필UseCase _useCase;

    public 용달기사Controller(I용달기사프로필UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpPost("register")]
    public async Task<IActionResult> 용달기사등록([FromBody] 용달기사등록요청 request)
    {
        var result = await _useCase.등록Async(GetCurrentUserId(), request);
        if (result.IsFailed)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(내용달기사조회), new { }, result.Value);
    }

    [HttpGet("me")]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    public async Task<IActionResult> 내용달기사조회()
    {
        var result = await _useCase.내프로필조회Async(GetCurrentUserId());
        return this.ToActionResult(result);
    }

    [HttpGet("me/transport-qualification")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    public async Task<IActionResult> 내운송자격조회([FromServices] I운송자격UseCase qualification, CancellationToken cancellationToken)
        => this.ToActionResult(await qualification.본인조회Async(GetCurrentUserId(), cancellationToken));

    [HttpPost("me/transport-qualification")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    public async Task<IActionResult> 운송자격제출([FromServices] I운송자격UseCase qualification,
        [FromBody] 운송자격제출요청 request, CancellationToken cancellationToken)
        => this.ToActionResult(await qualification.제출Async(GetCurrentUserId(), request, cancellationToken));

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
    }
}

