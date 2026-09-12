using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Filters;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize(Policy = "운영사용자전용")]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_5)]
[SsalddelApiFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.SsalddelMart)]
[RequireVersionFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[Route(NeighborhoodMicroHubRoutes.Common)]
[SsalddelApiContractName("NeighborhoodMicroHubsController")]
public sealed class 생활권물류거점Controller(I생활권물류거점Service service) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [SsalddelApiContractName("BrowsePublic")]
    public async Task<ActionResult<IReadOnlyList<생활권물류거점공개Response>>> 공개조회(
        [FromQuery] string? 생활권Key,
        CancellationToken cancellationToken)
        => Ok(await service.공개조회Async(생활권Key, cancellationToken));

    [HttpGet("mine")]
    [SsalddelApiContractName("BrowseMine")]
    public async Task<ActionResult<IReadOnlyList<생활권물류거점Response>>> 내신청조회(CancellationToken cancellationToken)
        => Ok(await service.내신청조회Async(cancellationToken));

    [HttpPost]
    [SsalddelApiContractName("Apply")]
    public async Task<ActionResult<생활권물류거점Response>> 신청(
        [FromBody] 생활권물류거점신청Request request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await service.신청Async(request, cancellationToken)); }
        catch (UnauthorizedAccessException ex) { return Problem(statusCode: StatusCodes.Status403Forbidden, title: "지도 신청 가원장 연결 권한이 없습니다.", detail: ex.Message); }
        catch (InvalidOperationException ex) { return BadRequestProblem(ex.Message); }
    }

    [HttpPut("{id:guid}/consents/owner")]
    [SsalddelApiContractName("SetOwnerConsent")]
    public Task<ActionResult<생활권물류거점Response>> 소유자동의(
        Guid id, [FromBody] 생활권물류거점동의Request request, CancellationToken cancellationToken)
        => ConsentAsync(() => service.소유자동의Async(id, request, cancellationToken));

    [HttpPut("{id:guid}/consents/site-manager")]
    [SsalddelApiContractName("SetSiteManagerConsent")]
    public Task<ActionResult<생활권물류거점Response>> 관리주체동의(
        Guid id, [FromBody] 생활권물류거점동의Request request, CancellationToken cancellationToken)
        => ConsentAsync(() => service.관리주체동의Async(id, request, cancellationToken));

    [HttpPost("{id:guid}/reservations")]
    [Authorize(Policy = "서버관리자전용")]
    [SsalddelApiContractName("ReserveCapacity")]
    public async Task<ActionResult<생활권물류거점예약Response>> 용량예약(
        Guid id, [FromBody] 생활권물류거점예약Request request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.예약Async(id, request, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFoundProblem(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequestProblem(ex.Message); }
    }

    [HttpPost("{id:guid}/reservations/{reservationId:guid}/complete")]
    [Authorize(Policy = "서버관리자전용")]
    [SsalddelApiContractName("CompleteHandoff")]
    public async Task<ActionResult<생활권물류거점완료Response>> 인계완료(
        Guid id, Guid reservationId, [FromQuery] string idempotencyKey, CancellationToken cancellationToken)
    {
        try { return Ok(await service.인계완료Async(id, reservationId, idempotencyKey, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFoundProblem(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequestProblem(ex.Message); }
    }

    private async Task<ActionResult<생활권물류거점Response>> ConsentAsync(Func<Task<생활권물류거점Response>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException ex) { return Problem(statusCode: StatusCodes.Status403Forbidden, title: "생활권 물류 거점 동의 권한이 없습니다.", detail: ex.Message); }
        catch (KeyNotFoundException ex) { return NotFoundProblem(ex.Message); }
        catch (생활권물류거점ConcurrencyException ex) { return ConflictProblem(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequestProblem(ex.Message); }
    }

    private ObjectResult BadRequestProblem(string detail) => Problem(statusCode: StatusCodes.Status400BadRequest, title: "생활권 물류 거점 요청이 올바르지 않습니다.", detail: detail);
    private ObjectResult NotFoundProblem(string detail) => Problem(statusCode: StatusCodes.Status404NotFound, title: "생활권 물류 거점을 찾을 수 없습니다.", detail: detail);
    private ObjectResult ConflictProblem(string detail) => Problem(statusCode: StatusCodes.Status409Conflict, title: "생활권 물류 거점 정보가 이미 변경되었습니다.", detail: detail);
}
