using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[Route("api/v1/community/map-applications/provisional-ledger")]
[SsalddelApiContractName("CommunityMapApplicationProvisionalLedgerController")]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
public sealed class 지도신청가원장Controller(
    I지도신청가원장UseCase useCase,
    I지도신청운송취소검토AdminWorkflow adminWorkflow) : CommunityControllerBase
{
    [HttpGet("by-map-marker")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> 내마커원장조회(
        [FromQuery] string markerId,
        [FromQuery] string? ledgerId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await useCase.내마커원장조회Async(
                markerId,
                ledgerId,
                CurrentUserId(),
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "지도 마커의 내 신청 원장을 조회할 수 없습니다.", Detail = ex.Message });
        }
    }

    [HttpGet("by-operational-source")]
    public async Task<IActionResult> 운영원본조회(
        [FromQuery] string workCode,
        [FromQuery] string operationalSourceType,
        [FromQuery] string operationalSourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await useCase.운영원본조회Async(
                workCode,
                operationalSourceType,
                operationalSourceId,
                CurrentUserId(),
                cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "지도 신청 원장을 조회할 수 없습니다.", Detail = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new ProblemDetails { Title = "지도 신청 원장을 조회할 수 없습니다.", Detail = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> 생성(
        [FromBody] 지도신청가원장생성Request request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await useCase.생성Async(
                request,
                CurrentUserId(),
                User.Identity?.Name ?? "신청자",
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "지도 신청 가원장을 만들 수 없습니다.", Detail = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new ProblemDetails { Title = "지원하지 않는 지도 신청 업무입니다.", Detail = ex.Message });
        }
    }

    [HttpPost("{ledgerId}/application-submission")]
    public Task<IActionResult> 신청제출반영(
        string ledgerId,
        [FromBody] 지도신청실원장전환Request request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            () => useCase.신청제출반영Async(ledgerId, request, CurrentUserId(), cancellationToken),
            "지도 신청 원장을 제출 상태로 전환할 수 없습니다.");

    [HttpPost("{ledgerId}/privacy-consent-withdrawal")]
    public Task<IActionResult> 동의철회반영(
        string ledgerId,
        [FromBody] 지도신청동의철회반영Request request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            () => useCase.동의철회반영Async(ledgerId, request, CurrentUserId(), cancellationToken),
            "개인정보 동의 철회를 원장에 반영할 수 없습니다.");

    [HttpPost("{ledgerId}/operational-cancellation")]
    public Task<IActionResult> 운영신청취소반영(
        string ledgerId,
        [FromBody] 지도신청운영취소반영Request request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            () => useCase.운영신청취소반영Async(ledgerId, request, CurrentUserId(), cancellationToken),
            "운영 신청 취소 결과를 원장에 반영할 수 없습니다.");

    [HttpPost("{ledgerId}/transport-cancellation-review")]
    public Task<IActionResult> 운송취소검토요청(
        string ledgerId,
        [FromBody] 지도신청운송취소검토요청Request request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            () => useCase.운송취소검토요청Async(ledgerId, request, CurrentUserId(), cancellationToken),
            "운송 취소 검토 요청을 원장에 기록할 수 없습니다.");

    [Authorize(Policy = "서버관리자전용")]
    [HttpGet("transport-cancellation-reviews")]
    public async Task<IActionResult> 관리자운송취소검토목록(CancellationToken cancellationToken)
        => Ok(await adminWorkflow.목록Async(cancellationToken));

    [Authorize(Policy = "서버관리자전용")]
    [HttpPost("{ledgerId}/transport-cancellation-review/decision")]
    public async Task<IActionResult> 관리자운송취소검토처리(
        string ledgerId,
        [FromBody] 지도신청운송취소검토처리Request request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => adminWorkflow.처리Async(ledgerId, request, CurrentUserId(), cancellationToken),
            "운송 취소 관리자 검토 결과를 원장에 반영할 수 없습니다.");
    }

    private static async Task<IActionResult> ExecuteAsync(
        Func<Task<지도신청가원장Response>> action,
        string title)
    {
        try
        {
            return new OkObjectResult(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(new ProblemDetails { Title = title, Detail = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ObjectResult(new ProblemDetails { Title = title, Detail = ex.Message })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new ProblemDetails { Title = title, Detail = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return new BadRequestObjectResult(new ProblemDetails { Title = title, Detail = ex.Message });
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? string.Empty;
}
