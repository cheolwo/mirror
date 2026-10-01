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
[Route("api/v1/community/ledgers/{원장Id}/sharing")]
public sealed class 커뮤니티원장공유Controller : ControllerBase
{
    private readonly I커뮤니티원장공유Service _service;

    public 커뮤니티원장공유Controller(I커뮤니티원장공유Service service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string 원장Id, CancellationToken cancellationToken)
        => this.ToActionResult(await _service.설정조회Async(원장Id, CurrentUserId(), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update(
        string 원장Id,
        [FromBody] 커뮤니티원장공개설정변경Request request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _service.설정변경Async(원장Id, request, CurrentUserId(), cancellationToken));

    [HttpPost("reuse")]
    public async Task<IActionResult> Reuse(
        string 원장Id,
        [FromBody] 커뮤니티원장재사용Request request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _service.재사용Async(원장Id, request, CurrentUserId(), cancellationToken));

    private string? CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub");
}
