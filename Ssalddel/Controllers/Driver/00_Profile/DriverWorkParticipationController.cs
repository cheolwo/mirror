using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Driver.Participation;
using Ssalddel.Contracts.Driver.Settings;
using 살뜰.도메인.공통;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Driver.Profile01;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.OperationalDispatchCore)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiCapability(SsalddelCapability.DriverWork)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Manage)]
[ApiController]
[Authorize(Roles = 역할명.기사)]
[Route("api/v1/driver/work-participation")]
public sealed class DriverWorkParticipationController(IDriverWorkParticipationUseCase useCase) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> 조회(CancellationToken ct) => this.ToActionResult(await useCase.GetAsync(Identity(), ct));

    [HttpPut]
    public async Task<IActionResult> 저장([FromBody] DriverWorkParticipationUpdate request, CancellationToken ct)
        => this.ToActionResult(await useCase.UpdateAsync(Identity(), request, ct));

    private string? Identity() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
}
