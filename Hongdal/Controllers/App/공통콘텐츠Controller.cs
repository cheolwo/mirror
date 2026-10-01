using Hongdal.Application.CommandProcessing;
using Hongdal.Controllers;
using Hongdal.Application.CommonContents.Commands;
using Hongdal.Application.CommonContents.Queries;
using Hongdal.Contracts.CommonContents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 홍달.Data;
using Hongdal.ApiMetadata;

namespace Hongdal.Controllers.App;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[ApiController]
[Route("api/v1/app/common-contents")]
[Authorize]
public sealed class 공통콘텐츠Controller : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public 공통콘텐츠Controller(ISender sender, ICurrentUserAccessor currentUserAccessor)
    {
        _sender = sender;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("widget")]
    public async Task<IActionResult> 위젯콘텐츠조회([FromQuery] string? 역할, [FromQuery] string? 위치, CancellationToken cancellationToken)
    {
        var resolvedRole = string.IsNullOrWhiteSpace(역할) ? ResolveAppRole(_currentUserAccessor.Role) : 역할;
        var resolvedLocation = string.IsNullOrWhiteSpace(위치) ? "home" : 위치;

        var 콘텐츠 = await _sender.Send(new 위젯콘텐츠조회Query(resolvedRole, resolvedLocation), cancellationToken);
        if (콘텐츠 is null)
        {
            return this.ToNotFoundProblem("위젯 콘텐츠를 찾을 수 없습니다.");
        }

        return Ok(콘텐츠);
    }

    [HttpPost("{콘텐츠Id:long}/watch/start")]
    public async Task<IActionResult> 시청시작(long 콘텐츠Id, [FromBody] 콘텐츠시청시작Request request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new 콘텐츠시청시작Command(콘텐츠Id, request.영상전체초), cancellationToken);
        if (result is null)
        {
            return this.ToNotFoundProblem("시청을 시작할 콘텐츠를 찾을 수 없습니다.");
        }

        return Ok(result);
    }

    [HttpPost("watch/{세션Id:long}/progress")]
    public async Task<IActionResult> 시청진행저장(long 세션Id, [FromBody] 콘텐츠시청진행Request request, CancellationToken cancellationToken)
    {
        var saved = await _sender.Send(new 콘텐츠시청진행Command(세션Id, request.현재시청초), cancellationToken);
        return saved ? NoContent() : this.ToNotFoundProblem("시청 세션을 찾을 수 없습니다.");
    }

    [HttpPost("watch/{세션Id:long}/complete")]
    public async Task<IActionResult> 시청완료(long 세션Id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new 콘텐츠시청완료Command(세션Id), cancellationToken);
        if (result is null)
        {
            return this.ToNotFoundProblem("시청 세션을 찾을 수 없습니다.");
        }

        return Ok(result);
    }

    [HttpGet("payment-benefits/estimate")]
    public async Task<IActionResult> 결제혜택견적([FromQuery] int baseFare, CancellationToken cancellationToken)
    {
        var 사용자Id = _currentUserAccessor.UserId;
        if (string.IsNullOrWhiteSpace(사용자Id))
        {
            return this.ToAuthenticationProblem("사용자 인증 정보가 없습니다.");
        }

        var result = await _sender.Send(new 결제혜택견적조회Query(사용자Id, Math.Max(0, baseFare)), cancellationToken);
        return Ok(result);
    }

    private static string ResolveAppRole(string? role)
    {
        if (string.Equals(role, 역할명.화주, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, 역할명.판매자, StringComparison.OrdinalIgnoreCase))
        {
            return "shipper";
        }

        if (string.Equals(role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase))
        {
            return "admin";
        }

        return "driver";
    }
}
