using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.TraditionalMarkets;
using Hongdal.Services.TraditionalMarkets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[ApiController]
[Authorize]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[Route("api/v1/traditional-market-councils")]
public sealed class 전통시장생활권협의Controller : ControllerBase
{
    private readonly I전통시장생활권협의Service _service;

    public 전통시장생활권협의Controller(I전통시장생활권협의Service service)
    {
        _service = service;
    }

    [HttpGet("mine")]
    public Task<ActionResult<전통시장생활권협의체목록응답>> 내협의체조회(CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.내협의체조회Async(CurrentUserId(), cancellationToken));

    [HttpGet("{councilId:guid}")]
    public async Task<ActionResult<전통시장생활권협의체상세응답>> 상세조회(
        Guid councilId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.상세조회Async(councilId, CurrentUserId(), cancellationToken);
            return result is null ? NotFoundProblem("생활권 협의체를 찾을 수 없습니다.") : Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbiddenProblem(ex.Message);
        }
    }

    [HttpPost]
    public Task<ActionResult<전통시장생활권협의체상세응답>> 생성(
        [FromBody] 전통시장생활권협의체생성요청 request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.생성Async(request, CurrentUserId(), cancellationToken));

    [HttpPost("{councilId:guid}/accept")]
    public Task<ActionResult<전통시장생활권협의체상세응답>> 참여수락(
        Guid councilId,
        [FromBody] 전통시장생활권협의체참여수락요청 request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.참여수락Async(councilId, request, CurrentUserId(), cancellationToken));

    [HttpPost("{councilId:guid}/agendas")]
    public Task<ActionResult<전통시장교역안건응답>> 안건생성(
        Guid councilId,
        [FromBody] 전통시장교역안건생성요청 request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.안건생성Async(councilId, request, CurrentUserId(), cancellationToken));

    [HttpPost("{councilId:guid}/agendas/{agendaId:guid}/decisions")]
    public Task<ActionResult<전통시장교역안건응답>> 안건결정(
        Guid councilId,
        Guid agendaId,
        [FromBody] 전통시장교역안건결정요청 request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.안건결정Async(councilId, agendaId, request, CurrentUserId(), cancellationToken));

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbiddenProblem(ex.Message);
        }
        catch (전통시장생활권협의ConcurrencyException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.Identity?.Name
           ?? string.Empty;

    private ObjectResult BadRequestProblem(string detail)
        => Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "전통시장 생활권 협의 요청이 올바르지 않습니다.",
            detail: detail);

    private ObjectResult NotFoundProblem(string detail)
        => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "전통시장 생활권 협의 정보를 찾을 수 없습니다.",
            detail: detail);

    private ObjectResult ForbiddenProblem(string detail)
        => Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "이 협의체에 접근할 수 없습니다.",
            detail: detail);

    private ObjectResult ConflictProblem(string detail)
        => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "협의 정보가 이미 변경되었습니다.",
            detail: detail);
}
