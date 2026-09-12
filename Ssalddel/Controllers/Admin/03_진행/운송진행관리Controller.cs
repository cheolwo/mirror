using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Ssalddel.Application.Admin.Progress;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.ApiMetadata;
using Microsoft.EntityFrameworkCore;

namespace Ssalddel.Controllers.Admin.Progress03;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[ApiController]
[Route("api/v1/admin/transports")]
[Authorize(Policy = "서버관리자전용")]
public sealed class 운송진행관리Controller : ControllerBase
{
    private readonly ISender _sender;
    private readonly I비정상운송사건운영UseCase _incidentUseCase;

    public 운송진행관리Controller(
        ISender sender,
        I비정상운송사건운영UseCase incidentUseCase)
    {
        _sender = sender;
        _incidentUseCase = incidentUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> 운송목록조회([FromQuery] string? 상태)
    {
        var items = await _sender.Send(new 관리자운송목록조회Query(상태));

        return Ok(items);
    }

    [HttpGet("events")]
    public async Task<IActionResult> 운송이벤트조회([FromQuery] string? requestId)
    {
        var items = await _sender.Send(new 관리자운송이벤트조회Query(requestId));

        return Ok(items);
    }

    [HttpGet("incidents")]
    public async Task<IActionResult> 문제사건목록조회(
        [FromQuery] string? 상태Code,
        CancellationToken cancellationToken)
        => Ok(await _incidentUseCase.목록조회Async(상태Code, cancellationToken));

    [HttpPut("incidents/{incidentStableId}/decision")]
    public async Task<IActionResult> 문제사건검토(
        string incidentStableId,
        [FromBody] 비정상운송사건검토요청 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _incidentUseCase.검토Async(incidentStableId, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status409Conflict);
        }
    }
}
