using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Driver.Settings;
using Ssalddel.Contracts.Driver.Settings;
using 살뜰.도메인.공통;

namespace Ssalddel.Controllers.Driver.Profile01;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiCapability(SsalddelCapability.DriverWork)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Manage)]
[ApiController]
[Authorize(Roles = 역할명.기사)]
[Route("api/v1/driver/return-area/settings")]
public sealed class 기사권역복귀설정Controller(I기사권역복귀설정UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 조회(CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.조회Async(현재기사Id(), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> 저장([FromBody] 기사권역복귀설정수정요청 request, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.저장Async(현재기사Id(), request, cancellationToken));

    private string? 현재기사Id() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
}
