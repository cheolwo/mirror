using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Community;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[Route(공간공동운영Routes.Api)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Ledger, SsalddelModuleKind.Api,
    "공간 공동운영 제안·비구속 참여·조건 합의와 전문 원장 연결",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "공개 소개는 별도 동의한 동네·역할만 제공하고 실제 입고·배송·정산 권위를 만들지 않습니다.")]
public sealed class 공간공동운영Controller(I공간공동운영UseCase useCase) : CommunityControllerBase
{
    [HttpGet("public")]
    [AllowAnonymous]
    [SsalddelApiContractName("BrowseSpaceCooperations")]
    public async Task<IActionResult> 공개([FromQuery] string? regionKey, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        if (page is < 1 or > 10000 || regionKey?.Length > 100) return BadRequest("조회할 동네와 페이지를 확인해 주세요.");
        return Ok(await useCase.공개Async(regionKey, page, cancellationToken));
    }

    [HttpGet("mine")]
    [SsalddelApiContractName("ListMySpaceCooperations")]
    public async Task<IActionResult> 내목록(CancellationToken cancellationToken)
    { Response.Headers.CacheControl = "private, no-store"; return Ok(await useCase.내목록Async(cancellationToken)); }

    [HttpGet("{ledgerId}/invitation")]
    [AllowAnonymous]
    [SsalddelApiContractName("GetSpaceCooperationInvitation")]
    public async Task<IActionResult> 초대조회(string ledgerId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var item = await useCase.초대조회Async(ledgerId, cancellationToken);
        return item is null ? this.ToNotFoundProblem("공개 중인 제안을 찾을 수 없습니다.") : Ok(item);
    }

    [HttpGet("requests/{clientRequestId:guid}")]
    [SsalddelApiContractName("GetSpaceCooperationRequestResult")]
    public async Task<IActionResult> 요청결과(Guid clientRequestId, [FromQuery] string? ledgerId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var item = await useCase.요청결과Async(clientRequestId, ledgerId, cancellationToken);
        return item is null ? this.ToNotFoundProblem("현재 계정의 처리 결과가 확인되지 않았습니다.") : Ok(item);
    }

    [HttpGet("{ledgerId}")]
    [SsalddelApiContractName("GetMySpaceCooperation")]
    public async Task<IActionResult> 상세(string ledgerId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var item = await useCase.상세Async(ledgerId, cancellationToken);
        return item is null ? this.ToNotFoundProblem("현재 계정의 공간 제안을 찾을 수 없습니다.") : Ok(item);
    }

    [HttpPost]
    [SsalddelApiContractName("CreateSpaceCooperation")]
    public async Task<IActionResult> 생성([FromBody] 공간공동운영생성Request request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return this.ToActionResult(await useCase.생성Async(request, cancellationToken));
    }

    [HttpGet("{ledgerId}/cycles/{cycleId}/candidates")]
    [SsalddelApiContractName("ListSpaceCooperationReferenceCandidates")]
    public async Task<IActionResult> 연결후보(string ledgerId, string cycleId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var items = await useCase.연결후보Async(ledgerId, cycleId, cancellationToken);
        return items is null ? this.ToNotFoundProblem("현재 운영 담당자의 준비 회차를 찾을 수 없습니다.") : Ok(items);
    }

    [HttpPost("{ledgerId}/commands")]
    [SsalddelApiContractName("ChangeSpaceCooperation")]
    public async Task<IActionResult> 변경(string ledgerId, [FromBody] 공간공동운영명령Request request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return this.ToActionResult(await useCase.변경Async(ledgerId, request, cancellationToken));
    }
}
