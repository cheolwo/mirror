using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Services.AgriculturalFisheries.ImportReadiness;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[ApiController]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[Route("api/v1/agricultural-fisheries/import-readiness")]
public sealed class MeatImportReadinessController : ControllerBase
{
    private readonly IMeatImportReadinessService _service;

    public MeatImportReadinessController(IMeatImportReadinessService service)
    {
        _service = service;
    }

    [HttpGet("diagram")]
    [AllowAnonymous]
    public ActionResult<MeatImportReadinessDiagramResponse> GetDiagram()
        => Ok(_service.GetDiagram());

    [HttpGet("cases/mine")]
    [Authorize]
    public Task<ActionResult<MeatImportReadinessCaseListResponse>> ListMine(CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.ListMineAsync(CurrentUserId(), cancellationToken));

    [HttpGet("cases/{caseId}")]
    [Authorize]
    public async Task<ActionResult<MeatImportReadinessCaseResponse>> GetCase(
        string caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetCaseAsync(caseId, CurrentUserId(), cancellationToken);
            return result is null ? NotFoundProblem("육류 수입 준비 작업공간을 찾을 수 없습니다.") : Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbiddenProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
    }

    [HttpPost("cases")]
    [Authorize]
    public async Task<ActionResult<MeatImportReadinessCaseResponse>> CreateCase(
        [FromBody] CreateMeatImportReadinessCaseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateCaseAsync(
                request,
                CurrentUserId(),
                CurrentDisplayName(),
                cancellationToken);
            return CreatedAtAction(nameof(GetCase), new { caseId = result.CaseId }, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbiddenProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
    }

    [HttpPut("cases/{caseId}/steps/{stepCode}/status")]
    [Authorize]
    public Task<ActionResult<MeatImportReadinessCaseResponse>> UpdateStepStatus(
        string caseId,
        string stepCode,
        [FromBody] UpdateMeatImportReadinessStepStatusRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.UpdateStepStatusAsync(
            caseId,
            stepCode,
            request,
            CurrentUserId(),
            CurrentDisplayName(),
            cancellationToken));

    [HttpPost("cases/{caseId}/steps/{stepCode}/evidences")]
    [Authorize]
    public Task<ActionResult<MeatImportReadinessCaseResponse>> AddEvidence(
        string caseId,
        string stepCode,
        [FromBody] AddMeatImportReadinessEvidenceRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.AddEvidenceAsync(
            caseId,
            stepCode,
            request,
            CurrentUserId(),
            CurrentDisplayName(),
            cancellationToken));

    [HttpPost("cases/{caseId}/steps/{stepCode}/discussions")]
    [Authorize]
    public Task<ActionResult<MeatImportReadinessCaseResponse>> AddDiscussion(
        string caseId,
        string stepCode,
        [FromBody] AddMeatImportReadinessDiscussionRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.AddDiscussionAsync(
            caseId,
            stepCode,
            request,
            CurrentUserId(),
            CurrentDisplayName(),
            cancellationToken));

    [HttpPost("cases/{caseId}/steps/{stepCode}/discussions/{discussionId}/resolve")]
    [Authorize]
    public Task<ActionResult<MeatImportReadinessCaseResponse>> ResolveDiscussion(
        string caseId,
        string stepCode,
        string discussionId,
        [FromBody] ResolveMeatImportReadinessDiscussionRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.ResolveDiscussionAsync(
            caseId,
            stepCode,
            discussionId,
            request,
            CurrentUserId(),
            CurrentDisplayName(),
            cancellationToken));

    [HttpPost("cases/{caseId}/steps/{stepCode}/acknowledgements")]
    [Authorize]
    public Task<ActionResult<MeatImportReadinessCaseResponse>> AcknowledgeStep(
        string caseId,
        string stepCode,
        [FromBody] AcknowledgeMeatImportReadinessStepRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _service.AcknowledgeStepAsync(
            caseId,
            stepCode,
            request,
            CurrentUserId(),
            CurrentDisplayName(),
            cancellationToken));

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
        catch (MeatImportReadinessConcurrencyException ex)
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
           ?? User.FindFirstValue("sub")
           ?? string.Empty;

    private string CurrentDisplayName()
        => User.Identity?.Name
           ?? User.FindFirstValue("name")
           ?? "참여자";

    private ObjectResult BadRequestProblem(string detail)
        => Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "육류 수입 준비 요청이 올바르지 않습니다.",
            detail: detail);

    private ObjectResult NotFoundProblem(string detail)
        => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "육류 수입 준비 정보를 찾을 수 없습니다.",
            detail: detail);

    private ObjectResult ForbiddenProblem(string detail)
        => Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "이 육류 수입 준비 작업공간에 접근할 수 없습니다.",
            detail: detail);

    private ObjectResult ConflictProblem(string detail)
        => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "육류 수입 준비 정보가 이미 변경되었습니다.",
            detail: detail);
}
