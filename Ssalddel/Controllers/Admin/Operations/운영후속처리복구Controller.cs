using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Operations;
using Ssalddel.Contracts.Admin.Operations;

namespace Ssalddel.Controllers.Admin.Operations;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[SsalddelApiVersion(SsalddelProductVersion.V3_5)]
[Route("api/v1/admin/operations/follow-up-recoveries")]
[SsalddelApiContractName("OperationsFollowUpRecoveryController")]
public sealed class 운영후속처리복구Controller(I운영후속처리복구UseCase useCase) : ControllerBase
{
    [HttpGet]
    [SsalddelApiContractName("ListFollowUpRecoveries")]
    public async Task<IActionResult> 목록(
        [FromQuery] bool includeCompleted = false,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await useCase.목록조회Async(includeCompleted, take, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{sourceCode}/{outboxId:long}/retry")]
    [SsalddelApiContractName("ScheduleFollowUpRecoveryRetry")]
    public async Task<IActionResult> 재시도예약(
        string sourceCode,
        long outboxId,
        [FromBody] 운영후속처리재시도요청Dto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await useCase.재시도예약Async(sourceCode, outboxId, request, cancellationToken);
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
