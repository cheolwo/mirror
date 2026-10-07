using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacySupport;

namespace Ssalddel.Controllers.Admin;

[ApiController, Authorize(Policy = 보호지원UseCase.AdminPolicy)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelApiContractName("CommercePrivacySupportController")]
[Route("api/v1/admin/privacy-support")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class 거래개인정보지원Controller(보호지원UseCase useCase) : ControllerBase
{
    [HttpGet("cases")]
    public Task<IActionResult> 목록([FromQuery] string? kind, [FromQuery] int page = 1, CancellationToken ct = default)
        => Run(() => useCase.목록Async(kind, page, admin: true, ct: ct));
    [HttpGet("cases/{caseId}")]
    public Task<IActionResult> 상세(string caseId, CancellationToken ct)
        => Run(() => useCase.상세Async(caseId, admin: true, ct: ct));
    [HttpPost("cases/{caseId}/commands")]
    public Task<IActionResult> 변경(string caseId, 보호지원CommandRequest request, CancellationToken ct)
        => Run(() => useCase.변경Async(caseId, request, admin: true, ct: ct));
    [HttpGet("cases/{caseId}/evidence")]
    public Task<IActionResult> 증거(string caseId, [FromQuery] string reasonCode, CancellationToken ct)
        => Run(() => useCase.증거조회Async(caseId, reasonCode, admin: true, ct: ct));
    [HttpPost("incidents")]
    public Task<IActionResult> 사고접수(개인정보사고접수Request request, CancellationToken ct)
        => Run(() => useCase.사고접수Async(request, ct));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (보호지원Exception ex) { return StatusCode(ex.StatusCode, new ProblemDetails
        { Title = "보호 지원 처리를 완료하지 못했습니다.", Detail = ex.Message, Status = ex.StatusCode, Extensions = { ["code"] = ex.Code } }); }
    }
}
