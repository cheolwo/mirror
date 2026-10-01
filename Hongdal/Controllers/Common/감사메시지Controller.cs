using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hongdal.Application.Warehouse;
using 홍달.도메인.사용자;
using Hongdal.ApiMetadata;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Authorize]
[Route("api/v1/gratitude")]
public sealed class 감사메시지Controller : ControllerBase
{
    private readonly ISender _sender;

    public 감사메시지Controller(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("targets")]
    public async Task<IActionResult> 감사대상조회([FromQuery] long 상품Id, [FromQuery] long? 주문Id, [FromQuery] long? 통관절차Id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new 공개상품이력감사대상조회Query(상품Id, 주문Id, 통관절차Id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> 감사메시지작성([FromBody] 감사메시지작성요청 request, CancellationToken cancellationToken)
    {
        var command = new 감사메시지작성Command(
            request.상품Id,
            request.주문Id,
            request.통관절차Id,
            request.발신자구분,
            request.발신참여자Id,
            request.대상역할,
            request.대상참여자Id,
            request.대상표시명,
            request.메시지내용,
            request.공개가능여부,
            request.참여자Id,
            request.실행역할);

        var result = await _sender.Send(command, cancellationToken);
        return this.ToActionResult(result);
    }
}

public sealed class 감사메시지작성요청
{
    public long 상품Id { get; set; }
    public long? 주문Id { get; set; }
    public long? 통관절차Id { get; set; }
    public string 발신자구분 { get; set; } = "익명구매자";
    public string? 발신참여자Id { get; set; }
    public string 대상역할 { get; set; } = string.Empty;
    public string? 대상참여자Id { get; set; }
    public string 대상표시명 { get; set; } = string.Empty;
    public string 메시지내용 { get; set; } = string.Empty;
    public bool 공개가능여부 { get; set; } = true;
    public string 참여자Id { get; set; } = string.Empty;
    public 홍달역할유형 실행역할 { get; set; } = 홍달역할유형.주문자;
}
