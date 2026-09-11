using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Ssalddel.Simulation.Hosting;

/// <summary>
/// 기존 HTTP 계약 시험에서만 사용하는 고정 로그인 주체다.
/// 등록 자체가 Testing 환경과 명시 설정의 동시 충족으로 제한된다.
/// </summary>
internal sealed class SsalddelSimulationTestingAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "test:simulation-user"),
            new Claim("sub", "test:simulation-user"),
        ], Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, Scheme.Name)));
    }
}
