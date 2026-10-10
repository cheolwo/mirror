using System.Security.Claims;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Community;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ssalddel.Controllers.Common;

[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.Community)]
[ApiController]
[Authorize]
[Route("api/v1/community/order-ledgers")]
public sealed class 주문원장Controller : CommunityControllerBase
{
    private readonly I주문원장통합UseCase _useCase;
    private readonly I주문원장서명UseCase _서명UseCase;
    private readonly I주문원장공개요청Service _공개요청Service;
    private readonly ISender _sender;

    public 주문원장Controller(
        I주문원장통합UseCase useCase,
        I주문원장서명UseCase 서명UseCase,
        I주문원장공개요청Service 공개요청Service,
        ISender sender)
    {
        _useCase = useCase;
        _서명UseCase = 서명UseCase;
        _공개요청Service = 공개요청Service;
        _sender = sender;
    }

    [HttpGet("types")]
    [ProducesResponseType(typeof(IReadOnlyList<CommunityLedgerTemplateResponse>), StatusCodes.Status200OK)]
    public IActionResult 원장종류()
        => Ok(CommunityLedgerTemplateCatalog.주문원장종류);

    [HttpGet("mine")]
    [ProducesResponseType(typeof(주문자원장목록응답), StatusCodes.Status200OK)]
    public async Task<IActionResult> 내원장목록(
        [FromQuery] 주문자원장목록조회요청 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _useCase.주문자목록조회Async(
            CurrentUserId(),
            request,
            cancellationToken));

    [HttpGet("{주문원장Id}")]
    public async Task<IActionResult> 통합조회(
        string 주문원장Id,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _sender.Send(
            new 주문자주문원장조회Query(주문원장Id, CurrentUserId()),
            cancellationToken));

    [HttpGet("{주문원장Id}/views/orderer")]
    public async Task<IActionResult> 주문자조회(string 주문원장Id, CancellationToken cancellationToken)
        => this.ToActionResult(await _sender.Send(
            new 주문자주문원장조회Query(주문원장Id, CurrentUserId()),
            cancellationToken));

    [HttpGet("{주문원장Id}/views/seller")]
    public async Task<IActionResult> 판매자조회(string 주문원장Id, CancellationToken cancellationToken)
        => this.ToActionResult(await _sender.Send(
            new 판매자주문원장조회Query(주문원장Id, CurrentUserId()),
            cancellationToken));

    [HttpGet("{주문원장Id}/views/warehouse")]
    public async Task<IActionResult> 창고담당자조회(string 주문원장Id, CancellationToken cancellationToken)
        => this.ToActionResult(await _sender.Send(
            new 창고담당자주문원장조회Query(주문원장Id, CurrentUserId()),
            cancellationToken));

    [HttpGet("{주문원장Id}/views/transport")]
    public async Task<IActionResult> 운송담당자조회(string 주문원장Id, CancellationToken cancellationToken)
        => this.ToActionResult(await _sender.Send(
            new 운송담당자주문원장조회Query(주문원장Id, CurrentUserId()),
            cancellationToken));

    [HttpPost("{주문원장Id}/children")]
    public async Task<IActionResult> 하위원장연결(
        string 주문원장Id,
        [FromBody] 주문하위원장연결요청 request,
        CancellationToken cancellationToken)
    {
        var access = await _sender.Send(
            new 주문자주문원장조회Query(주문원장Id, CurrentUserId()),
            cancellationToken);
        if (access.IsFailed)
        {
            return this.ToActionResult(access);
        }

        var changed = await _useCase.하위원장연결Async(
            주문원장Id,
            request,
            CurrentUserId(),
            cancellationToken);
        return await 변경후보호조회Async(주문원장Id, changed, cancellationToken);
    }

    [HttpDelete("{주문원장Id}/children/{하위원장Id}")]
    public async Task<IActionResult> 하위원장분리(
        string 주문원장Id,
        string 하위원장Id,
        [FromQuery] long? 기대Revision,
        CancellationToken cancellationToken)
    {
        var access = await _sender.Send(
            new 주문자주문원장조회Query(주문원장Id, CurrentUserId()),
            cancellationToken);
        if (access.IsFailed)
        {
            return this.ToActionResult(access);
        }

        var changed = await _useCase.하위원장분리Async(
            주문원장Id,
            하위원장Id,
            기대Revision,
            CurrentUserId(),
            cancellationToken);
        return await 변경후보호조회Async(주문원장Id, changed, cancellationToken);
    }

    private async Task<IActionResult> 변경후보호조회Async(
        string 주문원장Id, FluentResults.Result<주문원장통합Dto> changed, CancellationToken cancellationToken)
    {
        if (changed.IsFailed) return this.ToActionResult(changed);
        // 변경 성공도 현재 권한으로 다시 조회합니다. 권한이 철회되거나 조회가 실패하면
        // 변경 UseCase가 조합한 내부 상세를 응답으로 돌려주지 않습니다.
        var current = await _sender.Send(new 주문자주문원장조회Query(주문원장Id, CurrentUserId()), cancellationToken);
        if (current.IsFailed) return this.ToActionResult(current);
        return Ok(주문원장변경응답Projector.생성(changed.Value, current.Value));
    }

    [HttpGet("{주문원장Id}/signature")]
    public async Task<IActionResult> 서명상태조회(
        string 주문원장Id,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _서명UseCase.조회Async(
            주문원장Id,
            CurrentUserId(),
            cancellationToken));

    [HttpPost("{주문원장Id}/signature-request")]
    public async Task<IActionResult> 서명요청준비(
        string 주문원장Id,
        [FromBody] 주문원장서명요청준비요청 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _서명UseCase.서명요청준비Async(
            주문원장Id,
            request,
            CurrentUserId(),
            cancellationToken));

    [HttpPost("{주문원장Id}/signatures")]
    public async Task<IActionResult> 서명등록(
        string 주문원장Id,
        [FromBody] 주문원장서명등록요청 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _서명UseCase.서명등록Async(
            주문원장Id,
            request,
            CurrentUserId(),
            cancellationToken));

    [HttpPost("{주문원장Id}/disclosure-requests")]
    public async Task<IActionResult> 원장공개요청(
        string 주문원장Id,
        [FromBody] 원장공개요청입력 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _공개요청Service.요청Async(
            주문원장Id,
            request,
            CurrentUserId(),
            cancellationToken));

    [HttpPost("{주문원장Id}/disclosure-requests/{요청Id}/decision")]
    public async Task<IActionResult> 원장공개결정(
        string 주문원장Id,
        string 요청Id,
        [FromBody] 원장공개결정입력 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _공개요청Service.결정Async(
            주문원장Id,
            요청Id,
            request,
            CurrentUserId(),
            cancellationToken));

    [HttpGet("disclosure-requests/inbox")]
    public async Task<IActionResult> 받은원장공개요청목록(CancellationToken cancellationToken)
        => Ok(await _공개요청Service.받은요청목록Async(CurrentUserId(), cancellationToken));

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.Identity?.Name
           ?? string.Empty;
}
