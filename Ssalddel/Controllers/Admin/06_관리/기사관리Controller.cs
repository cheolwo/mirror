using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Ssalddel.Controllers;
using Ssalddel.Application.Admin.Management;
using Ssalddel.Contracts.Admin.Management;
using Ssalddel.ApiMetadata;
using System.Security.Claims;
using Ssalddel.Application.Driver.Qualification;
using Ssalddel.Contracts.Driver.Profile;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Master06;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[ApiController]
[Route("api/v1/admin/drivers")]
[Authorize(Policy = "서버관리자전용")]
public sealed class 기사관리Controller : ControllerBase
{
    private readonly ISender _sender;

    public 기사관리Controller(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> 목록조회(
        [FromQuery] string? 운행상태,
        [FromQuery] string? 차량종류,
        [FromQuery] string? 활동지역검색어)
    {
        var items = await _sender.Send(new 관리자기사목록조회Query(운행상태, 차량종류, 활동지역검색어));

        return Ok(items);
    }

    [HttpGet("{driverId}")]
    public async Task<IActionResult> 단건조회(string driverId)
    {
        var item = await _sender.Send(new 관리자기사단건조회Query(driverId));

        if (item == null)
        {
            return this.ToNotFoundProblem("기사 정보를 찾을 수 없습니다.");
        }

        return Ok(item);
    }

    [HttpGet("{driverId}/dispatches")]
    public async Task<IActionResult> 기사별배차내역(string driverId)
    {
        var items = await _sender.Send(new 관리자기사배차내역조회Query(driverId));

        return Ok(items);
    }

    [HttpGet("{driverId}/transport-qualification")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    public async Task<IActionResult> 운송자격조회(string driverId, [FromServices] I운송자격UseCase qualification, CancellationToken cancellationToken)
        => this.ToActionResult(await qualification.관리자조회Async(driverId, cancellationToken));

    [HttpPost("{driverId}/transport-qualification/review")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    public async Task<IActionResult> 운송자격검토(string driverId, [FromServices] I운송자격UseCase qualification,
        [FromBody] 운송자격검토요청 request, CancellationToken cancellationToken)
        => this.ToActionResult(await qualification.검토Async(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), driverId, request, cancellationToken));
}
