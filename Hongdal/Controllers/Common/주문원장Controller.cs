using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Application.Community;
using Hongdal.Services.Community;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Authorize]
[Route("api/v1/community/order-ledgers")]
public sealed class 주문원장Controller : ControllerBase
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

        return this.ToActionResult(await _useCase.하위원장연결Async(
            주문원장Id,
            request,
            CurrentUserId(),
            cancellationToken));
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

        return this.ToActionResult(await _useCase.하위원장분리Async(
            주문원장Id,
            하위원장Id,
            기대Revision,
            CurrentUserId(),
            cancellationToken));
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
