using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Versioning;
using 살뜰.Services.Dispatch.Explanation;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
[Route("api/v1/cargo-dispatch-explanations")]
public sealed class 화물배차설명Controller(ICargoDispatchExplanationService service) : ControllerBase
{
    [HttpGet("{requestId}")]
    public Task<IActionResult> Get(string requestId, CancellationToken ct)
        => ExecuteAsync(async user => Ok(await service.GetAsync(user, requestId, ct)));

    [HttpGet("{requestId}/notice-context/{evidenceId}")]
    public Task<IActionResult> NoticeContext(string requestId, string evidenceId, CancellationToken ct)
        => ExecuteAsync(async user => Ok(await service.GetNoticeContextAsync(user, requestId, evidenceId, ct)));

    [HttpPost("{requestId}/feedback")]
    public Task<IActionResult> Submit(string requestId, [FromBody] CargoDispatchFeedbackRequest request, CancellationToken ct)
        => ExecuteAsync(async user => Ok(await service.SubmitFeedbackAsync(user, requestId, request, ct)));

    [HttpGet("{requestId}/feedback/{clientRequestId:guid}")]
    public Task<IActionResult> GetFeedback(string requestId, Guid clientRequestId, [FromQuery] string evidenceId, CancellationToken ct)
        => ExecuteAsync(async user =>
        {
            var result = await service.GetFeedbackAsync(user, requestId, evidenceId, clientRequestId, ct);
            return result is null ? NotFound() : Ok(result);
        });

    private async Task<IActionResult> ExecuteAsync(Func<string, Task<IActionResult>> action)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        try { return await action(userId); }
        catch (UnauthorizedAccessException) { return StatusCode(403, new ProblemDetails { Status = 403, Title = "본인 업무의 배차 설명만 확인할 수 있습니다." }); }
        catch (CargoDispatchExplanationNotFoundException) { return NotFound(); }
        catch (CargoDispatchFeedbackConflictException) { return Conflict(new ProblemDetails { Status = 409, Title = "같은 요청으로 다른 의견이 이미 접수됐습니다." }); }
        catch (ArgumentException) { return BadRequest(new ProblemDetails { Status = 400, Title = "배차 설명 또는 의견 입력을 확인해 주세요." }); }
        catch (CargoDispatchExplanationUnavailableException) { return Unavailable(); }
        catch (CargoDispatchFeedbackUnavailableException) { return Unavailable(); }
    }

    private ObjectResult Unavailable() => StatusCode(503, new ProblemDetails
    { Status = 503, Title = "배차 설명·의견 저장소를 확인할 수 없습니다. 접수 여부를 다시 확인해 주세요." });
}
