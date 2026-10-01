using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common;
using Hongdal.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[ApiController]
[Route("api/v1/auth")]
public class 인증Controller : ControllerBase
{
    private readonly I인증UseCase _인증UseCase;

    public 인증Controller(I인증UseCase 인증UseCase)
    {
        _인증UseCase = 인증UseCase;
    }

    [HttpPost("login")]
    public async Task<IActionResult> 로그인([FromBody] 로그인요청 request)
    {
        var result = await _인증UseCase.로그인Async(request, 요청Context생성());
        return this.ToActionResult(result);
    }

    [HttpPost("register/driver")]
    public async Task<IActionResult> 기사회원가입([FromBody] 기사회원가입요청 request)
    {
        var result = await _인증UseCase.기사회원가입Async(request);
        return this.ToActionResult(result);
    }

    [HongdalApiVersion(HongdalProductVersion.V2_5)]
    [HttpPost("register/orderer")]
    public async Task<IActionResult> 주문자회원가입([FromBody] 주문자회원가입요청 request)
    {
        var result = await _인증UseCase.주문자회원가입Async(request);
        return this.ToActionResult(result);
    }

    [HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
    [Authorize]
    [HttpPost("onboarding/connection-candidates")]
    public async Task<IActionResult> 가입온보딩인연후보조회([FromBody] 가입인연후보조회요청 request, CancellationToken cancellationToken)
    {
        var result = await _인증UseCase.가입온보딩인연후보조회Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HongdalApiVersion(HongdalProductVersion.V2_5)]
    [Authorize]
    [HttpPost("onboarding/orderer-group-scope")]
    public async Task<IActionResult> 주문자집단온보딩([FromBody] 주문자집단온보딩요청 request)
    {
        var result = await _인증UseCase.주문자집단온보딩Async(request, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return this.ToActionResult(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> 토큰갱신([FromBody] 토큰갱신요청 request)
    {
        var result = await _인증UseCase.토큰갱신Async(request);
        return this.ToActionResult(result);
    }

    private 인증요청Context 요청Context생성()
        => new(
            Request.Path.Value ?? "/api/v1/auth/login",
            HttpContext.TraceIdentifier,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Request.Headers.UserAgent.ToString());
}
