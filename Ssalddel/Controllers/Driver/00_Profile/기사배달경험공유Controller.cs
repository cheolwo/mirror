using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.DeliveryExperience;
using Ssalddel.Application.Driver.Settings;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;

namespace Ssalddel.Controllers.Driver.Profile01;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiCapability(SsalddelCapability.DriverWork)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Manage)]
[ApiController, Authorize(Roles = 역할명.기사)]
[Route("api/v1/driver/preferences/delivery-experience-sharing")]
public sealed class 기사배달경험공유Controller(기사배달경험공유UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 조회(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
        return this.ToActionResult(await useCase.조회Async(id, ct));
    }

    [HttpPut]
    public async Task<IActionResult> 저장(DeliveryExperienceSharingPreferenceRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
        return this.ToActionResult(await useCase.저장Async(id, request, ct));
    }
}
