using System.Security.Claims;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common;
using Ssalddel.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ssalddel.Security;

namespace Ssalddel.Controllers.Common;

[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
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
    [EnableRateLimiting(RequestRateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> 로그인([FromBody] 로그인요청 request)
    {
        var result = await _인증UseCase.로그인Async(request, 요청Context생성());
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpGet("social/providers")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> 소셜로그인제공자([FromServices] IAuthenticationSchemeProvider schemes)
    {
        var configured = (await schemes.GetAllSchemesAsync()).Select(scheme => scheme.Name).ToHashSet(StringComparer.Ordinal);
        return Ok(소셜로그인ProviderIds.지원Provider목록
            .Where(id => configured.Contains(소셜로그인OAuth.Scheme(id)))
            .ToArray());
    }

    [AllowAnonymous]
    [HttpGet("social/{provider}/start")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EnableRateLimiting(RequestRateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> 소셜로그인시작(
        string provider, [FromQuery] string? surface, [FromQuery] string? flowId,
        [FromServices] IAuthenticationSchemeProvider schemes)
    {
        if (!소셜로그인ProviderIds.지원Provider목록.Contains(provider, StringComparer.Ordinal) ||
            await schemes.GetSchemeAsync(소셜로그인OAuth.Scheme(provider)) is null)
        {
            return NotFound();
        }
        if (surface is not ("community" or "integrated") || !소셜로그인OAuth.IsValidFlowId(flowId))
        {
            return BadRequest();
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = $"{소셜로그인OAuth.LoginPath(surface)}?social=complete",
            Items =
            {
                [소셜로그인OAuth.ProviderKey] = provider,
                [소셜로그인OAuth.SurfaceKey] = surface,
                [소셜로그인OAuth.FlowIdKey] = flowId
            }
        };
        return Challenge(properties, 소셜로그인OAuth.Scheme(provider));
    }

    [AllowAnonymous]
    [HttpPost("social/complete")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EnableRateLimiting(RequestRateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> 소셜로그인완료(
        [FromBody] 소셜로그인완료입력 request, [FromServices] IAuthenticationSchemeProvider schemes)
    {
        if (!소셜로그인OAuth.IsValidFlowId(request.FlowId))
        {
            return BadRequest();
        }

        var ticket = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        var providerId = ticket.Properties?.GetString(소셜로그인OAuth.ProviderKey);
        var subject = ticket.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!ticket.Succeeded || !소셜로그인ProviderIds.지원여부(providerId) || string.IsNullOrWhiteSpace(subject) ||
            await schemes.GetSchemeAsync(소셜로그인OAuth.Scheme(providerId!)) is null ||
            !string.Equals(ticket.Properties?.GetString(소셜로그인OAuth.FlowIdKey), request.FlowId, StringComparison.Ordinal))
        {
            return BadRequest();
        }

        로그인요청? credentials = null;
        if (request.UserNameOrEmail is not null || request.Password is not null)
        {
            if (string.IsNullOrWhiteSpace(request.UserNameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest();
            }
            credentials = new 로그인요청 { UserNameOrEmail = request.UserNameOrEmail, Password = request.Password };
        }

        var result = await _인증UseCase.소셜로그인Async(providerId!, subject, credentials, 요청Context생성());
        if (result.HasError(error => error.Metadata.TryGetValue("Code", out var code) &&
            Equals(code, 소셜로그인OAuth.LinkRequiredCode)))
        {
            return Conflict(new { code = 소셜로그인OAuth.LinkRequiredCode });
        }
        if (result.IsSuccess)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }
        return this.ToActionResult(result);
    }

    [HttpPost("register/driver")]
    [EnableRateLimiting(RequestRateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> 기사회원가입([FromBody] 기사회원가입요청 request)
    {
        var result = await _인증UseCase.기사회원가입Async(request);
        return this.ToActionResult(result);
    }

    [HttpPost("register/community")]
    [EnableRateLimiting(RequestRateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> 커뮤니티회원가입([FromBody] 커뮤니티회원가입요청 request)
    {
        var result = await _인증UseCase.커뮤니티회원가입Async(request);
        return this.ToActionResult(result);
    }

    [SsalddelApiVersion(SsalddelProductVersion.V2_5)]
    [HttpPost("register/orderer")]
    [EnableRateLimiting(RequestRateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> 주문자회원가입([FromBody] 주문자회원가입요청 request)
    {
        var result = await _인증UseCase.주문자회원가입Async(request);
        return this.ToActionResult(result);
    }

    [SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.Community)]
    [Authorize]
    [HttpPost("onboarding/connection-candidates")]
    public async Task<IActionResult> 가입온보딩친구후보조회([FromBody] 가입친구후보조회요청 request, CancellationToken cancellationToken)
    {
        var result = await _인증UseCase.가입온보딩친구후보조회Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [SsalddelApiVersion(SsalddelProductVersion.V2_5)]
    [Authorize]
    [HttpPost("onboarding/orderer-group-scope")]
    public async Task<IActionResult> 주문자집단온보딩([FromBody] 주문자집단온보딩요청 request)
    {
        var result = await _인증UseCase.주문자집단온보딩Async(request, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return this.ToActionResult(result);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(RequestRateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> 토큰갱신([FromBody] 토큰갱신요청 request)
    {
        var result = await _인증UseCase.토큰갱신Async(request);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPut("preferences/language")]
    public async Task<IActionResult> 표시언어설정([FromBody] 표시언어설정요청 request)
    {
        var result = await _인증UseCase.표시언어설정Async(
            request,
            User.FindFirstValue(ClaimTypes.NameIdentifier));
        return this.ToActionResult(result);
    }

    private 인증요청Context 요청Context생성()
        => new(
            Request.Path.Value ?? "/api/v1/auth/login",
            HttpContext.TraceIdentifier,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Request.Headers.UserAgent.ToString());

    public sealed record 소셜로그인완료입력(string? FlowId, string? UserNameOrEmail, string? Password);
}
