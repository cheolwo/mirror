using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacySupport;

namespace Ssalddel.Controllers.Common;

[ApiController, Authorize]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelApiContractName("PrivacyRightsRequestsController")]
[Route("api/v1/common/privacy-rights-requests")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class 개인정보권리Controller(보호지원UseCase useCase) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> 접수(개인정보권리접수Request request, CancellationToken ct)
        => Run(() => useCase.권리접수Async(request, ct));
    [HttpGet]
    public Task<IActionResult> 목록([FromQuery] int page = 1, CancellationToken ct = default)
        => Run(() => useCase.목록Async(보호지원종류Codes.권리, page, ct: ct));
    [HttpGet("{caseId}")]
    public Task<IActionResult> 상세(string caseId, CancellationToken ct)
        => Run(() => useCase.상세Async(caseId, ct: ct));
    [HttpPost("{caseId}/commands")]
    public Task<IActionResult> 변경(string caseId, 보호지원CommandRequest request, CancellationToken ct)
        => Run(() => useCase.변경Async(caseId, request, ct: ct));
    [HttpGet("{caseId}/evidence")]
    public Task<IActionResult> 본인증거(string caseId, CancellationToken ct)
        => Run(() => useCase.증거조회Async(caseId, "own-request", ct: ct));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (보호지원Exception ex) { return StatusCode(ex.StatusCode, new ProblemDetails
        { Title = "개인정보 요청을 처리하지 못했습니다.", Detail = ex.Message, Status = ex.StatusCode, Extensions = { ["code"] = ex.Code } }); }
    }
}
