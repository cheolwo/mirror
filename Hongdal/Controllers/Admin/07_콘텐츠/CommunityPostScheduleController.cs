using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Admin.Content07;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[ApiController]
[Route("api/v1/admin/community-post-schedules")]
[Authorize(Policy = "서버관리자전용")]
public sealed class CommunityPostScheduleController : ControllerBase
{
    private readonly I커뮤니티게시글예약발행UseCase _useCase;

    public CommunityPostScheduleController(I커뮤니티게시글예약발행UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] PlatformCommunityPostScheduleCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.예약Async(request, cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => this.ToActionResult(await _useCase.예약목록Async(status, take, cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
        => this.ToActionResult(await _useCase.예약취소Async(id, cancellationToken));
}
